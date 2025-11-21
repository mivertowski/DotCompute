// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using DotCompute.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

[assembly: SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Infrastructure code with pump thread, logging not in hot path", Scope = "type", Target = "~T:DotCompute.Core.Messaging.MessageQueueBridge`1")]

namespace DotCompute.Core.Messaging;

/// <summary>
/// Specifies the direction of message flow in a <see cref="MessageQueueBridge{T}"/>.
/// </summary>
public enum BridgeDirection
{
    /// <summary>
    /// Transfers messages from host NamedQueue to device buffer (Host → Device).
    /// </summary>
    HostToDevice,

    /// <summary>
    /// Transfers messages from device buffer to host NamedQueue (Device → Host).
    /// </summary>
    DeviceToHost,

    /// <summary>
    /// Supports bidirectional message flow (both Host → Device and Device → Host).
    /// </summary>
    Bidirectional
}

/// <summary>
/// Bridges host-side named message queues to GPU-resident Span buffers with bidirectional support.
/// </summary>
/// <typeparam name="T">The message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
/// <remarks>
/// <para>
/// <b>Architecture (Bidirectional):</b>
/// </para>
/// <code>
/// Host → Device (HostToDevice or Bidirectional):
/// User → SendToNamedQueueAsync() → NamedQueue (CPU)
///                                        ↓
///                                   PumpThread (background)
///                                        ↓
///                                   Serialize → PinnedStagingBuffer
///                                        ↓
///                                   Batch DMA Transfer (cuMemcpy/clWrite/MTLCopy)
///                                        ↓
///                                   GpuResidentQueue (Span&lt;byte&gt;) ← Kernel polls
///
/// Device → Host (DeviceToHost or Bidirectional):
/// Kernel writes → GpuResidentQueue (Span&lt;byte&gt;)
///                                        ↓
///                                   PumpThread (background)
///                                        ↓
///                                   Batch DMA Transfer (cuMemcpyDtoH/clRead/MTLCopy)
///                                        ↓
///                                   Deserialize → NamedQueue (CPU)
///                                        ↓
///                                   User ← ReceiveFromNamedQueueAsync()
/// </code>
/// <para>
/// <b>Performance Optimizations:</b>
/// - Lock-free staging buffer for multi-producer scalability
/// - Adaptive polling: Busy-wait (10ns) → Yield (100ns) → Sleep (1ms) backoff
/// - Batch DMA transfers: 1-512 messages per transfer (configurable)
/// - Zero-copy via pinned memory
/// - Sub-microsecond serialization with DefaultMessageSerializer
/// </para>
/// <para>
/// <b>Backpressure Handling:</b>
/// - Block: Pump thread blocks until GPU queue has space
/// - Reject: Named queue rejects new messages when staging buffer is full
/// - DropOldest: Named queue drops oldest message to make space
/// - DropNew: Named queue silently drops new message when staging full
/// </para>
/// </remarks>
public sealed class MessageQueueBridge<T> : IAsyncDisposable
    where T : IRingKernelMessage, new()
{
    private readonly IMessageQueue<T> _namedQueue;
    private readonly PinnedStagingBuffer _stagingBuffer;
    private readonly IMessageSerializer<T> _serializer;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _pumpCts;
    private readonly Thread _pumpThread;
    private readonly Func<ReadOnlyMemory<byte>, Task<bool>>? _hostToDeviceFunc;
    private readonly Func<Memory<byte>, Task<int>>? _deviceToHostFunc;
    private readonly BridgeDirection _direction;
    private readonly MessageQueueOptions _options;

    private long _messagesTransferred;
    private long _messagesSerialized;
    private long _messagesDropped;
    private long _bytesTransferred;
    private readonly Stopwatch _uptime;
    private bool _disposed;

    // Telemetry for diagnosing bridge behavior
    private long _pollAttempts;
    private long _successfulReads;
    private long _garbageMessages;
    private long _validationFailures;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueBridge{T}"/> class for Host → Device transfers.
    /// </summary>
    /// <param name="namedQueue">The host-side named message queue.</param>
    /// <param name="gpuTransferFunc">
    /// Function to transfer a batch of serialized messages to GPU.
    /// Takes a ReadOnlyMemory&lt;byte&gt; of serialized messages and returns true if successful.
    /// </param>
    /// <param name="options">Queue configuration options.</param>
    /// <param name="serializer">Message serializer (optional, uses default JSON serializer if null).</param>
    /// <param name="logger">Logger instance (optional).</param>
    public MessageQueueBridge(
        IMessageQueue<T> namedQueue,
        Func<ReadOnlyMemory<byte>, Task<bool>> gpuTransferFunc,
        MessageQueueOptions options,
        IMessageSerializer<T>? serializer = null,
        ILogger? logger = null)
        : this(namedQueue, BridgeDirection.HostToDevice, gpuTransferFunc, null, options, serializer, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueBridge{T}"/> class with specified direction.
    /// </summary>
    /// <param name="namedQueue">The host-side named message queue.</param>
    /// <param name="direction">The direction of message flow.</param>
    /// <param name="hostToDeviceFunc">
    /// Function to transfer messages from host to device (required for HostToDevice and Bidirectional).
    /// Takes a ReadOnlyMemory&lt;byte&gt; of serialized messages and returns true if successful.
    /// </param>
    /// <param name="deviceToHostFunc">
    /// Function to transfer messages from device to host (required for DeviceToHost and Bidirectional).
    /// Takes a Memory&lt;byte&gt; buffer and returns the number of bytes read.
    /// </param>
    /// <param name="options">Queue configuration options.</param>
    /// <param name="serializer">Message serializer (optional, uses default JSON serializer if null).</param>
    /// <param name="logger">Logger instance (optional).</param>
    public MessageQueueBridge(
        IMessageQueue<T> namedQueue,
        BridgeDirection direction,
        Func<ReadOnlyMemory<byte>, Task<bool>>? hostToDeviceFunc,
        Func<Memory<byte>, Task<int>>? deviceToHostFunc,
        MessageQueueOptions options,
        IMessageSerializer<T>? serializer = null,
        ILogger? logger = null)
    {
        _namedQueue = namedQueue ?? throw new ArgumentNullException(nameof(namedQueue));
        _direction = direction;
        _hostToDeviceFunc = hostToDeviceFunc;
        _deviceToHostFunc = deviceToHostFunc;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serializer = serializer ?? new DefaultMessageSerializer<T>();
        _logger = logger ?? NullLogger.Instance;

        // Validate required functions based on direction
        if (direction is BridgeDirection.HostToDevice or BridgeDirection.Bidirectional)
        {
            if (hostToDeviceFunc == null)
            {
                throw new ArgumentNullException(nameof(hostToDeviceFunc), "Host to device function is required for HostToDevice or Bidirectional mode");
            }
        }

        if (direction is BridgeDirection.DeviceToHost or BridgeDirection.Bidirectional)
        {
            if (deviceToHostFunc == null)
            {
                throw new ArgumentNullException(nameof(deviceToHostFunc), "Device to host function is required for DeviceToHost or Bidirectional mode");
            }
        }

        // Create pinned staging buffer
        // Capacity: Match named queue capacity for 1:1 staging
        // MessageSize: Use max serialized size from serializer
        _stagingBuffer = new PinnedStagingBuffer(
            capacity: NextPowerOfTwo(options.Capacity),
            messageSize: _serializer.MaxSerializedSize);

        _pumpCts = new CancellationTokenSource();
        _uptime = Stopwatch.StartNew();

        // Start background pump thread
        _pumpThread = new Thread(PumpThreadLoop)
        {
            Name = $"MessageQueueBridge-{typeof(T).Name}",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal // Prioritize message transfers
        };

        _pumpThread.Start();

        _logger.LogInformation(
            "MessageQueueBridge<{MessageType}> started: Direction={Direction}, Capacity={Capacity}, MessageSize={MessageSize}",
            typeof(T).Name, _direction, options.Capacity, _serializer.MaxSerializedSize);
    }

    /// <summary>
    /// Gets the host-side named message queue for enqueue operations.
    /// </summary>
    public IMessageQueue<T> NamedQueue => _namedQueue;

    /// <summary>
    /// Gets the total number of messages successfully transferred to GPU.
    /// </summary>
    public long MessagesTransferred => Interlocked.Read(ref _messagesTransferred);

    /// <summary>
    /// Gets the total number of messages serialized to the staging buffer.
    /// </summary>
    public long MessagesSerialized => Interlocked.Read(ref _messagesSerialized);

    /// <summary>
    /// Gets the total number of messages dropped due to backpressure.
    /// </summary>
    public long MessagesDropped => Interlocked.Read(ref _messagesDropped);

    /// <summary>
    /// Gets the total number of bytes transferred to GPU.
    /// </summary>
    public long BytesTransferred => Interlocked.Read(ref _bytesTransferred);

    /// <summary>
    /// Gets the bridge uptime.
    /// </summary>
    public TimeSpan Uptime => _uptime.Elapsed;

    /// <summary>
    /// Gets the average transfer rate in messages per second.
    /// </summary>
    public double MessagesPerSecond
    {
        get
        {
            var elapsed = _uptime.Elapsed.TotalSeconds;
            return elapsed > 0 ? MessagesTransferred / elapsed : 0;
        }
    }

    /// <summary>
    /// Gets the average throughput in megabytes per second.
    /// </summary>
    public double MegabytesPerSecond
    {
        get
        {
            var elapsed = _uptime.Elapsed.TotalSeconds;
            return elapsed > 0 ? (BytesTransferred / (1024.0 * 1024.0)) / elapsed : 0;
        }
    }

    /// <summary>
    /// Gets the total number of device-to-host poll attempts.
    /// </summary>
    public long PollAttempts => Interlocked.Read(ref _pollAttempts);

    /// <summary>
    /// Gets the total number of successful reads from device buffer.
    /// </summary>
    public long SuccessfulReads => Interlocked.Read(ref _successfulReads);

    /// <summary>
    /// Gets the total number of garbage messages detected (uninitialized memory).
    /// </summary>
    public long GarbageMessages => Interlocked.Read(ref _garbageMessages);

    /// <summary>
    /// Gets the total number of message validation failures.
    /// </summary>
    public long ValidationFailures => Interlocked.Read(ref _validationFailures);

    /// <summary>
    /// Validates whether a message is legitimate or garbage from uninitialized memory.
    /// </summary>
    /// <param name="message">The message to validate.</param>
    /// <param name="messageBytes">The raw serialized bytes.</param>
    /// <returns>True if the message appears valid; otherwise, false.</returns>
    private static bool IsValidMessage(T? message, ReadOnlySpan<byte> messageBytes)
    {
        // Null check
        if (message == null)
        {
            return false;
        }

        // Check if bytes are all zeros (uninitialized GPU memory pattern)
        var isAllZeros = true;
        for (var i = 0; i < Math.Min(messageBytes.Length, 64); i++) // Check first 64 bytes
        {
            if (messageBytes[i] != 0)
            {
                isAllZeros = false;
                break;
            }
        }

        if (isAllZeros)
        {
            return false; // Likely uninitialized memory
        }

        // Check MessageId is not default (Guid.Empty)
        if (message.MessageId == Guid.Empty)
        {
            return false;
        }

        // Check MessageType is valid string (not null, empty, or absurdly long)
        if (string.IsNullOrWhiteSpace(message.MessageType) || message.MessageType.Length > 1000)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Background pump thread that transfers messages based on configured direction.
    /// </summary>
    private void PumpThreadLoop()
    {
        _logger.LogDebug("Pump thread started (Direction={Direction})", _direction);

        var batchBuffer = new byte[_options.Capacity * _serializer.MaxSerializedSize];
        var spinWaitCount = 0;
        const int MaxSpinWait = 1000; // Spin for 1000 iterations before yielding
        const int MaxYieldWait = 100; // Yield for 100 iterations before sleeping

        try
        {
            while (!_pumpCts.Token.IsCancellationRequested)
            {
                var didWork = false;

                // Host → Device pumping
                if (_direction is BridgeDirection.HostToDevice or BridgeDirection.Bidirectional)
                {
                    didWork |= PumpHostToDevice(batchBuffer);
                }

                // Device → Host pumping
                if (_direction is BridgeDirection.DeviceToHost or BridgeDirection.Bidirectional)
                {
                    didWork |= PumpDeviceToHost(batchBuffer);
                }

                // Adaptive backoff when idle
                if (!didWork)
                {
                    spinWaitCount++;

                    if (spinWaitCount < MaxSpinWait)
                    {
                        // Busy-wait (10ns latency)
                        Thread.SpinWait(10);
                    }
                    else if (spinWaitCount < MaxSpinWait + MaxYieldWait)
                    {
                        // Yield to other threads (100ns latency)
                        Thread.Yield();
                    }
                    else
                    {
                        // Sleep to reduce CPU usage (1ms latency)
                        Thread.Sleep(1);
                        spinWaitCount = MaxSpinWait + MaxYieldWait; // Cap backoff
                    }
                }
                else
                {
                    spinWaitCount = 0; // Reset backoff on activity
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pump thread crashed");
        }
        finally
        {
            _logger.LogDebug(
                "Pump thread stopped: Transferred={Transferred}, Dropped={Dropped}, Uptime={Uptime:F2}s",
                MessagesTransferred, MessagesDropped, Uptime.TotalSeconds);
        }
    }

    /// <summary>
    /// Pumps messages from host NamedQueue to device buffer.
    /// </summary>
    /// <returns>True if any work was done; otherwise, false.</returns>
    private bool PumpHostToDevice(byte[] batchBuffer)
    {
        var didWork = false;

        // Phase 1: Dequeue messages from named queue and serialize to staging buffer
        var serializedCount = 0;

        while (_namedQueue.TryDequeue(out var message) && serializedCount < _options.Capacity)
        {
            try
            {
                // Serialize message
                var messageBytes = batchBuffer.AsSpan(
                    serializedCount * _serializer.MaxSerializedSize,
                    _serializer.MaxSerializedSize);

                var actualSize = _serializer.Serialize(message!, messageBytes);

                // Diagnostic logging when enabled (byte-level message inspection)
                if (MessageDiagnostics.IsEnabled)
                {
                    var actualBytes = messageBytes.Slice(0, actualSize);
                    _logger.LogDebug(
                        "Serialized message {MessageId} ({ByteCount} bytes):\n{HexDump}",
                        message?.MessageId,
                        actualSize,
                        MessageDiagnostics.FormatHexDump(actualBytes, $"{typeof(T).Name} (Host→Device)"));
                }

                // Stage for GPU transfer (use full messageBytes buffer, not sliced)
                // MemoryPack writes variable-length data, but PinnedStagingBuffer expects fixed-size
                if (_stagingBuffer.TryEnqueue(messageBytes)) // Pass full buffer, not sliced to actualSize
                {
                    serializedCount++;
                    Interlocked.Increment(ref _messagesSerialized);
                    didWork = true;
                }
                else
                {
                    // Staging buffer full, handle backpressure
                    Interlocked.Increment(ref _messagesDropped);
                    _logger.LogWarning(
                        "Staging buffer full, dropped message {MessageId} (BackpressureStrategy={Strategy})",
                        message?.MessageId, _options.BackpressureStrategy);

                    // Re-enqueue to named queue if strategy is Block
                    if (_options.BackpressureStrategy == BackpressureStrategy.Block)
                    {
                        _namedQueue.TryEnqueue(message!, CancellationToken.None);
                    }

                    break; // Stop dequeuing, let GPU catch up
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing message {MessageId}", message?.MessageId);
                Interlocked.Increment(ref _messagesDropped);
            }
        }

        // Phase 2: Batch transfer from staging buffer to GPU
        if (!_stagingBuffer.IsEmpty)
        {
            var transferBuffer = batchBuffer.AsMemory(0, _options.Capacity * _serializer.MaxSerializedSize);
            var dequeueCount = _stagingBuffer.DequeueBatch(
                transferBuffer.Span,
                maxMessages: Math.Min(512, _options.Capacity)); // Max 512 messages per batch

            if (dequeueCount > 0)
            {
                var transferSize = dequeueCount * _serializer.MaxSerializedSize;
                var transferSlice = transferBuffer[..transferSize];

                try
                {
                    // Transfer to GPU (async, but wait for completion in pump thread)
                    #pragma warning disable VSTHRD002 // Intentional blocking in background pump thread
                    var success = _hostToDeviceFunc!(transferSlice).GetAwaiter().GetResult();
                    #pragma warning restore VSTHRD002

                    if (success)
                    {
                        Interlocked.Add(ref _messagesTransferred, dequeueCount);
                        Interlocked.Add(ref _bytesTransferred, transferSize);
                        didWork = true;

                        _logger.LogTrace(
                            "Transferred {Count} messages ({Bytes} bytes) Host → Device",
                            dequeueCount, transferSize);
                    }
                    else
                    {
                        _logger.LogWarning("Host → Device transfer failed for {Count} messages", dequeueCount);
                        Interlocked.Add(ref _messagesDropped, dequeueCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error transferring {Count} messages Host → Device", dequeueCount);
                    Interlocked.Add(ref _messagesDropped, dequeueCount);
                }
            }
        }

        return didWork;
    }

    /// <summary>
    /// Pumps messages from device buffer to host NamedQueue.
    /// </summary>
    /// <returns>True if any work was done; otherwise, false.</returns>
    private bool PumpDeviceToHost(byte[] batchBuffer)
    {
        var didWork = false;
        Interlocked.Increment(ref _pollAttempts); // Track every poll attempt

        try
        {
            // Read batch of messages from device
            var readBuffer = batchBuffer.AsMemory(0, _options.Capacity * _serializer.MaxSerializedSize);

            #pragma warning disable VSTHRD002 // Intentional blocking in background pump thread
            var bytesRead = _deviceToHostFunc!(readBuffer).GetAwaiter().GetResult();
            #pragma warning restore VSTHRD002

            if (bytesRead > 0)
            {
                Interlocked.Increment(ref _successfulReads); // Track successful reads

                // Deserialize and validate messages
                var messageCount = bytesRead / _serializer.MaxSerializedSize;
                var validCount = 0;
                var garbageCount = 0;

                for (var i = 0; i < messageCount; i++)
                {
                    try
                    {
                        var messageBytes = readBuffer.Span.Slice(
                            i * _serializer.MaxSerializedSize,
                            _serializer.MaxSerializedSize);

                        var message = _serializer.Deserialize(messageBytes);

                        // Diagnostic logging when enabled (byte-level message inspection)
                        if (MessageDiagnostics.IsEnabled)
                        {
                            _logger.LogDebug(
                                "Received message from device (index={Index}):\n{HexDump}",
                                i,
                                MessageDiagnostics.FormatHexDump(messageBytes, $"{typeof(T).Name} (Device→Host)"));
                        }

                        // CRITICAL: Validate message before counting as transferred
                        if (!IsValidMessage(message, messageBytes))
                        {
                            garbageCount++;
                            Interlocked.Increment(ref _garbageMessages);
                            continue; // Skip garbage messages
                        }

                        // Message is valid - attempt to enqueue
                        if (message != null)
                        {
                            if (_namedQueue.TryEnqueue(message, CancellationToken.None))
                            {
                                validCount++;
                                Interlocked.Increment(ref _messagesTransferred);
                                Interlocked.Add(ref _bytesTransferred, _serializer.MaxSerializedSize);
                                didWork = true;
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "Failed to enqueue valid message {MessageId} from device (queue full)",
                                    message.MessageId);
                                Interlocked.Increment(ref _messagesDropped);
                            }
                        }
                        else
                        {
                            Interlocked.Increment(ref _validationFailures);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deserializing message from device (index={Index})", i);
                        Interlocked.Increment(ref _validationFailures);
                    }
                }

                // Log telemetry if garbage detected (indicates GPU kernel not executing)
                if (garbageCount > 0)
                {
                    _logger.LogWarning(
                        "Device → Host poll detected {Garbage}/{Total} garbage messages (uninitialized GPU memory). " +
                        "Valid messages: {Valid}. Total polls: {Polls}, Successful reads: {Reads}",
                        garbageCount, messageCount, validCount, PollAttempts, SuccessfulReads);
                }
                else if (validCount > 0)
                {
                    _logger.LogTrace(
                        "Transferred {Count} valid messages ({Bytes} bytes) Device → Host",
                        validCount, bytesRead);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring messages Device → Host");
        }

        return didWork;
    }

    /// <summary>
    /// Calculates the next power of two greater than or equal to the specified value.
    /// </summary>
    private static int NextPowerOfTwo(int value)
    {
        if (value <= 0)
        {
            return 1;
        }

        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        value++;

        return value;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _logger.LogInformation(
            "Disposing MessageQueueBridge<{MessageType}>: " +
            "Transferred={Transferred}, Dropped={Dropped}, " +
            "PollAttempts={Polls}, SuccessfulReads={Reads}, " +
            "GarbageMessages={Garbage}, ValidationFailures={Failures}",
            typeof(T).Name, MessagesTransferred, MessagesDropped,
            PollAttempts, SuccessfulReads, GarbageMessages, ValidationFailures);

        // Stop pump thread
        await _pumpCts.CancelAsync();

        // Wait for pump thread to exit (with timeout)
        if (_pumpThread.IsAlive)
        {
            if (!_pumpThread.Join(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("Pump thread did not exit gracefully, aborting");
            }
        }

        // Dispose resources
        _pumpCts.Dispose();
        _stagingBuffer.Dispose();
        _namedQueue.Dispose();

        _uptime.Stop();

        await Task.CompletedTask;
    }
}
