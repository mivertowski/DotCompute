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
/// Bridges host-side named message queues to GPU-resident Span buffers.
/// </summary>
/// <typeparam name="T">The message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
/// <remarks>
/// <para>
/// <b>Architecture:</b>
/// </para>
/// <code>
/// User → SendToNamedQueueAsync() → NamedQueue (CPU)
///                                        ↓
///                                   PumpThread (background)
///                                        ↓
///                                   Serialize → PinnedStagingBuffer
///                                        ↓
///                                   Batch DMA Transfer (cuMemcpy/clWrite/MTLCopy)
///                                        ↓
///                                   GpuResidentQueue (Span&lt;byte&gt;) ← Kernel polls
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
    private readonly Func<ReadOnlyMemory<byte>, Task<bool>> _gpuTransferFunc;
    private readonly MessageQueueOptions _options;

    private long _messagesTransferred;
    private long _messagesSerialized;
    private long _messagesDropped;
    private long _bytesTransferred;
    private readonly Stopwatch _uptime;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueueBridge{T}"/> class.
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
    {
        _namedQueue = namedQueue ?? throw new ArgumentNullException(nameof(namedQueue));
        _gpuTransferFunc = gpuTransferFunc ?? throw new ArgumentNullException(nameof(gpuTransferFunc));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serializer = serializer ?? new DefaultMessageSerializer<T>();
        _logger = logger ?? NullLogger.Instance;

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
            "MessageQueueBridge<{MessageType}> started: Capacity={Capacity}, MessageSize={MessageSize}",
            typeof(T).Name, options.Capacity, _serializer.MaxSerializedSize);
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
    /// Background pump thread that transfers messages from named queue to GPU.
    /// </summary>
    private void PumpThreadLoop()
    {
        _logger.LogDebug("Pump thread started");

        var batchBuffer = new byte[_options.Capacity * _serializer.MaxSerializedSize];
        var spinWaitCount = 0;
        const int MaxSpinWait = 1000; // Spin for 1000 iterations before yielding
        const int MaxYieldWait = 100; // Yield for 100 iterations before sleeping

        try
        {
            while (!_pumpCts.Token.IsCancellationRequested)
            {
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

                        // Stage for GPU transfer (use full messageBytes buffer, not sliced)
                        // MemoryPack writes variable-length data, but PinnedStagingBuffer expects fixed-size
                        if (_stagingBuffer.TryEnqueue(messageBytes)) // Pass full buffer, not sliced to actualSize
                        {
                            serializedCount++;
                            Interlocked.Increment(ref _messagesSerialized);
                            spinWaitCount = 0; // Reset backoff on success
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
                            var success = _gpuTransferFunc(transferSlice).GetAwaiter().GetResult();
                            #pragma warning restore VSTHRD002

                            if (success)
                            {
                                Interlocked.Add(ref _messagesTransferred, dequeueCount);
                                Interlocked.Add(ref _bytesTransferred, transferSize);
                                spinWaitCount = 0; // Reset backoff on success

                                _logger.LogTrace(
                                    "Transferred {Count} messages ({Bytes} bytes) to GPU",
                                    dequeueCount, transferSize);
                            }
                            else
                            {
                                _logger.LogWarning("GPU transfer failed for {Count} messages", dequeueCount);
                                Interlocked.Add(ref _messagesDropped, dequeueCount);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error transferring {Count} messages to GPU", dequeueCount);
                            Interlocked.Add(ref _messagesDropped, dequeueCount);
                        }
                    }
                }

                // Phase 3: Adaptive backoff when idle
                if (serializedCount == 0 && _stagingBuffer.IsEmpty)
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
            "Disposing MessageQueueBridge<{MessageType}>: Transferred={Transferred}, Dropped={Dropped}",
            typeof(T).Name, MessagesTransferred, MessagesDropped);

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
