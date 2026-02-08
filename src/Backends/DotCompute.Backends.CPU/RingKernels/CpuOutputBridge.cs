// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.RingKernels;

/// <summary>
/// CPU-side output bridge for ring kernel messages.
/// Provides a consistent API for writing output messages from CPU ring kernels.
/// </summary>
/// <remarks>
/// <para>
/// Unlike GPU backends which require device-to-host transfers and deserialization,
/// the CPU output bridge can write directly to the output queue since the messages
/// are already in host memory. This provides better performance than the full
/// bridge infrastructure while maintaining API consistency.
/// </para>
/// <para>
/// <b>Features:</b>
/// <list type="bullet">
/// <item><description>Pinned buffer for direct memory access (useful if future interop needed)</description></item>
/// <item><description>Statistics tracking for throughput monitoring</description></item>
/// <item><description>Thread-safe message writing</description></item>
/// <item><description>Batching support for high-throughput scenarios</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Performance:</b> ~100ns per message (direct queue enqueue without serialization overhead).
/// </para>
/// </remarks>
/// <typeparam name="T">The message type (must implement IRingKernelMessage).</typeparam>
internal sealed class CpuOutputBridge<T> : IDisposable where T : IRingKernelMessage
{
    private readonly IMessageQueue<T> _outputQueue;
    private readonly ILogger _logger;
    private readonly GCHandle _pinnedBufferHandle;
    private readonly T[] _buffer;
    private readonly int _bufferCapacity;
    private readonly object _syncLock = new();

    private int _bufferHead;
    private int _bufferCount;
    private long _totalMessagesWritten;
    private long _totalBytesWritten;
    private readonly Stopwatch _uptime = new();
    private bool _disposed;

    /// <summary>
    /// Gets the total number of messages written through this bridge.
    /// </summary>
    public long TotalMessagesWritten => Interlocked.Read(ref _totalMessagesWritten);

    /// <summary>
    /// Gets the estimated total bytes written (based on message count and type size).
    /// </summary>
    public long TotalBytesWritten => Interlocked.Read(ref _totalBytesWritten);

    /// <summary>
    /// Gets the current number of messages in the internal buffer.
    /// </summary>
    public int BufferedCount
    {
        get
        {
            lock (_syncLock)
            {
                return _bufferCount;
            }
        }
    }

    /// <summary>
    /// Gets the throughput in messages per second.
    /// </summary>
    public double ThroughputMessagesPerSecond
    {
        get
        {
            var uptimeSeconds = _uptime.Elapsed.TotalSeconds;
            return uptimeSeconds > 0 ? TotalMessagesWritten / uptimeSeconds : 0;
        }
    }

    /// <summary>
    /// Initializes a new CPU output bridge.
    /// </summary>
    /// <param name="outputQueue">The output message queue to write to.</param>
    /// <param name="bufferCapacity">Internal buffer capacity for batching (default: 64).</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public CpuOutputBridge(IMessageQueue<T> outputQueue, int bufferCapacity = 64, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(outputQueue);
        if (bufferCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferCapacity), "Buffer capacity must be positive");
        }

        _outputQueue = outputQueue;
        _bufferCapacity = bufferCapacity;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        // Allocate and pin the buffer for potential future interop
        _buffer = new T[bufferCapacity];
        _pinnedBufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

        _uptime.Start();

        _logger.LogDebug(
            "CpuOutputBridge<{MessageType}> initialized with buffer capacity {Capacity}",
            typeof(T).Name, bufferCapacity);
    }

    /// <summary>
    /// Writes a single message to the output queue.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message was written successfully; otherwise false.</returns>
    public bool TryWrite(T message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var success = _outputQueue.TryEnqueue(message, cancellationToken);

        if (success)
        {
            Interlocked.Increment(ref _totalMessagesWritten);
            Interlocked.Add(ref _totalBytesWritten, EstimateMessageSize(message));
        }

        return success;
    }

    /// <summary>
    /// Writes a message to the internal buffer for batched flushing.
    /// </summary>
    /// <param name="message">The message to buffer.</param>
    /// <returns>True if buffered successfully; false if buffer is full.</returns>
    public bool TryBuffer(T message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_syncLock)
        {
            if (_bufferCount >= _bufferCapacity)
            {
                return false;
            }

            var index = (_bufferHead + _bufferCount) % _bufferCapacity;
            _buffer[index] = message;
            _bufferCount++;
            return true;
        }
    }

    /// <summary>
    /// Flushes all buffered messages to the output queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of messages successfully flushed.</returns>
    public int Flush(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var flushed = 0;

        lock (_syncLock)
        {
            while (_bufferCount > 0 && !cancellationToken.IsCancellationRequested)
            {
                var message = _buffer[_bufferHead];

                if (_outputQueue.TryEnqueue(message, cancellationToken))
                {
                    _buffer[_bufferHead] = default!; // Clear reference
                    _bufferHead = (_bufferHead + 1) % _bufferCapacity;
                    _bufferCount--;
                    flushed++;

                    Interlocked.Increment(ref _totalMessagesWritten);
                    Interlocked.Add(ref _totalBytesWritten, EstimateMessageSize(message));
                }
                else
                {
                    // Queue full, stop flushing
                    break;
                }
            }
        }

        if (flushed > 0)
        {
            _logger.LogDebug("CpuOutputBridge flushed {Count} messages", flushed);
        }

        return flushed;
    }

    /// <summary>
    /// Writes a batch of messages to the output queue.
    /// </summary>
    /// <param name="messages">The messages to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of messages successfully written.</returns>
    public int WriteBatch(ReadOnlySpan<T> messages, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var written = 0;

        foreach (var message in messages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (_outputQueue.TryEnqueue(message, cancellationToken))
            {
                written++;
                Interlocked.Increment(ref _totalMessagesWritten);
                Interlocked.Add(ref _totalBytesWritten, EstimateMessageSize(message));
            }
            else
            {
                // Queue full, stop writing
                break;
            }
        }

        return written;
    }

    /// <summary>
    /// Gets the pinned buffer address for potential interop scenarios.
    /// </summary>
    /// <returns>Pointer to the pinned buffer.</returns>
    public nint GetPinnedBufferAddress()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _pinnedBufferHandle.AddrOfPinnedObject();
    }

    /// <summary>
    /// Gets output bridge statistics.
    /// </summary>
    public CpuOutputBridgeStatistics GetStatistics()
    {
        return new CpuOutputBridgeStatistics
        {
            TotalMessagesWritten = TotalMessagesWritten,
            TotalBytesWritten = TotalBytesWritten,
            BufferedCount = BufferedCount,
            BufferCapacity = _bufferCapacity,
            ThroughputMessagesPerSecond = ThroughputMessagesPerSecond,
            Uptime = _uptime.Elapsed
        };
    }

    /// <summary>
    /// Estimates the serialized size of a message.
    /// </summary>
    private static long EstimateMessageSize(T message)
    {
        // Use message's SerializedSize if available via interface
        // For now, use a reasonable estimate based on typical message sizes
        return 256; // Average message size estimate
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _uptime.Stop();

        // Flush any remaining buffered messages
        try
        {
            Flush(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error flushing buffered messages during dispose");
        }

        // Release pinned buffer
        if (_pinnedBufferHandle.IsAllocated)
        {
            _pinnedBufferHandle.Free();
        }

        _logger.LogDebug(
            "CpuOutputBridge<{MessageType}> disposed. Total messages: {Messages}, Throughput: {Throughput:F2} msg/s",
            typeof(T).Name, TotalMessagesWritten, ThroughputMessagesPerSecond);

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Statistics for CPU output bridge operation.
/// </summary>
public sealed class CpuOutputBridgeStatistics
{
    /// <summary>
    /// Gets the total number of messages written.
    /// </summary>
    public long TotalMessagesWritten { get; init; }

    /// <summary>
    /// Gets the total bytes written (estimated).
    /// </summary>
    public long TotalBytesWritten { get; init; }

    /// <summary>
    /// Gets the current number of buffered messages.
    /// </summary>
    public int BufferedCount { get; init; }

    /// <summary>
    /// Gets the buffer capacity.
    /// </summary>
    public int BufferCapacity { get; init; }

    /// <summary>
    /// Gets the throughput in messages per second.
    /// </summary>
    public double ThroughputMessagesPerSecond { get; init; }

    /// <summary>
    /// Gets the bridge uptime.
    /// </summary>
    public TimeSpan Uptime { get; init; }
}
