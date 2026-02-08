// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.OpenCL.Interop;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL.RingKernels;

/// <summary>
/// OpenCL-based lock-free message queue for inter-kernel communication.
/// </summary>
/// <typeparam name="T">Message payload type (must be unmanaged).</typeparam>
/// <remarks>
/// Implements a GPU-resident lock-free ring buffer using OpenCL atomics.
/// The queue is optimized for high throughput concurrent access from
/// multiple work-groups.
/// </remarks>
public sealed class OpenCLMessageQueue<T> : IMessageQueue<T> where T : unmanaged
{
    private readonly ILogger<OpenCLMessageQueue<T>> _logger;
    private readonly int _capacity;
    private readonly OpenCLContext _context;

    private MemObject _deviceBuffer;
    private MemObject _deviceHead;
    private MemObject _deviceTail;
    private IntPtr _hostHead;
    private IntPtr _hostTail;
    private bool _initialized;
    private bool _disposed;

    private long _totalEnqueued;
    private long _totalDequeued;
    private long _totalDropped;
    private long _lastEnqueueTicks;
    private long _lastDequeueTicks;
    private long _firstEnqueueTicks;
    private long _totalLatencyMicroseconds;
    private long _latencySampleCount;

    /// <inheritdoc/>
    public int Capacity { get; }

    /// <inheritdoc/>
    public bool IsEmpty => Count == 0;

    /// <inheritdoc/>
    public bool IsFull => Count >= Capacity - 1;

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            if (!_initialized || _disposed)
            {
                return 0;
            }

            // Read head/tail from device
            int head, tail;

            var headResult = OpenCLNative.clEnqueueReadBuffer(
                _context.CommandQueue,
                _deviceHead,
                1u, // blocking
                UIntPtr.Zero,
                (nuint)sizeof(int),
                _hostHead,
                0,
                null,
                out _);

            if (headResult != CLResultCode.Success)
            {
                _logger.LogError("Failed to read head pointer: {Error}", headResult);
                return 0;
            }

            var tailResult = OpenCLNative.clEnqueueReadBuffer(
                _context.CommandQueue,
                _deviceTail,
                1u, // blocking
                UIntPtr.Zero,
                (nuint)sizeof(int),
                _hostTail,
                0,
                null,
                out _);

            if (tailResult != CLResultCode.Success)
            {
                _logger.LogError("Failed to read tail pointer: {Error}", tailResult);
                return 0;
            }

            head = Marshal.ReadInt32(_hostHead);
            tail = Marshal.ReadInt32(_hostTail);

            // Calculate size with proper wraparound handling
            var size = (tail - head + _capacity) % _capacity;
            return size;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLMessageQueue{T}"/> class.
    /// </summary>
    /// <param name="capacity">Queue capacity (must be power of 2).</param>
    /// <param name="context">OpenCL context.</param>
    /// <param name="logger">Logger instance.</param>
    public OpenCLMessageQueue(int capacity, OpenCLContext context, ILogger<OpenCLMessageQueue<T>> logger)
    {
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
        {
            throw new ArgumentException("Capacity must be a positive power of 2", nameof(capacity));
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _capacity = capacity;
        Capacity = capacity;
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer GetBuffer()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized. Call InitializeAsync first.");
        }

        var messageSize = Unsafe.SizeOf<KernelMessage<T>>();
        long bufferSizeInBytes = messageSize * _capacity;
        return new OpenCLDeviceBufferWrapper(_deviceBuffer, bufferSizeInBytes);
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer GetHeadPtr()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized. Call InitializeAsync first.");
        }

        return new OpenCLDeviceBufferWrapper(_deviceHead, sizeof(int));
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer GetTailPtr()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized. Call InitializeAsync first.");
        }

        return new OpenCLDeviceBufferWrapper(_deviceTail, sizeof(int));
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        _logger.LogInformation(
            "Initializing OpenCL message queue with capacity {Capacity} for type {Type}",
            _capacity,
            typeof(T).Name);

        await Task.Run(() =>
        {
            // Calculate sizes
            var messageSize = Unsafe.SizeOf<KernelMessage<T>>();
            var bufferSize = (nuint)(messageSize * _capacity);
            var atomicSize = (nuint)sizeof(int);

            // Allocate device memory
            _deviceBuffer = _context.CreateBuffer(
                MemoryFlags.ReadWrite,
                bufferSize);

            _deviceHead = _context.CreateBuffer(
                MemoryFlags.ReadWrite,
                atomicSize);

            _deviceTail = _context.CreateBuffer(
                MemoryFlags.ReadWrite,
                atomicSize);

            // Allocate host pinned memory for fast transfers
            _hostHead = Marshal.AllocHGlobal(sizeof(int));
            _hostTail = Marshal.AllocHGlobal(sizeof(int));

            // Initialize head/tail to zero
            var zero = 0;
            Marshal.WriteInt32(_hostHead, zero);
            Marshal.WriteInt32(_hostTail, zero);

            // Write initial values to device
            var headWriteResult = OpenCLNative.clEnqueueWriteBuffer(
                _context.CommandQueue,
                _deviceHead,
                1u, // blocking
                UIntPtr.Zero,
                atomicSize,
                _hostHead,
                0,
                null,
                out _);

            OpenCLException.ThrowIfError((OpenCLError)headWriteResult, "Write head initial value");

            var tailWriteResult = OpenCLNative.clEnqueueWriteBuffer(
                _context.CommandQueue,
                _deviceTail,
                1u, // blocking
                UIntPtr.Zero,
                atomicSize,
                _hostTail,
                0,
                null,
                out _);

            OpenCLException.ThrowIfError((OpenCLError)tailWriteResult, "Write tail initial value");

            // Zero the buffer
            unsafe
            {
                byte zero_byte = 0;
                var fillResult = OpenCLNative.clEnqueueFillBuffer(
                    _context.CommandQueue,
                    _deviceBuffer,
                    (nint)(&zero_byte),
                    (nuint)1,
                    UIntPtr.Zero,
                    bufferSize,
                    0,
                    null,
                    out _);

                OpenCLException.ThrowIfError((OpenCLError)fillResult, "Fill buffer with zeros");
            }

            _initialized = true;
            _logger.LogDebug("Message queue initialized successfully");

        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryEnqueueAsync(
        KernelMessage<T> message,
        CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized");
        }

        return await Task.Run(() =>
        {
            // Read current tail
            var tailReadResult = OpenCLNative.clEnqueueReadBuffer(
                _context.CommandQueue,
                _deviceTail,
                1u,
                UIntPtr.Zero,
                (nuint)sizeof(int),
                _hostTail,
                0,
                null,
                out _);

            if (tailReadResult != CLResultCode.Success)
            {
                _logger.LogError("Failed to read tail: {Error}", tailReadResult);
                Interlocked.Increment(ref _totalDropped);
                return false;
            }

            var currentTail = Marshal.ReadInt32(_hostTail);
            var nextTail = (currentTail + 1) & (_capacity - 1);

            // Read current head
            var headReadResult = OpenCLNative.clEnqueueReadBuffer(
                _context.CommandQueue,
                _deviceHead,
                1u,
                UIntPtr.Zero,
                (nuint)sizeof(int),
                _hostHead,
                0,
                null,
                out _);

            if (headReadResult != CLResultCode.Success)
            {
                _logger.LogError("Failed to read head: {Error}", headReadResult);
                Interlocked.Increment(ref _totalDropped);
                return false;
            }

            var currentHead = Marshal.ReadInt32(_hostHead);

            // Check if full
            if (nextTail == currentHead)
            {
                Interlocked.Increment(ref _totalDropped);
                return false;
            }

            // Write message to buffer
            var messageSize = Unsafe.SizeOf<KernelMessage<T>>();
            var messagePtr = Marshal.AllocHGlobal(messageSize);
            try
            {
                unsafe
                {
                    Unsafe.Write(messagePtr.ToPointer(), message);
                }

                var targetOffset = (nuint)(currentTail * messageSize);
                var writeResult = OpenCLNative.clEnqueueWriteBuffer(
                    _context.CommandQueue,
                    _deviceBuffer,
                    1u,
                    (UIntPtr)targetOffset,
                    (nuint)messageSize,
                    messagePtr,
                    0,
                    null,
                    out _);

                if (writeResult != CLResultCode.Success)
                {
                    _logger.LogError("Failed to write message: {Error}", writeResult);
                    Interlocked.Increment(ref _totalDropped);
                    return false;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(messagePtr);
            }

            // Update tail
            Marshal.WriteInt32(_hostTail, nextTail);
            var tailWriteResult = OpenCLNative.clEnqueueWriteBuffer(
                _context.CommandQueue,
                _deviceTail,
                1u,
                UIntPtr.Zero,
                (nuint)sizeof(int),
                _hostTail,
                0,
                null,
                out _);

            if (tailWriteResult != CLResultCode.Success)
            {
                _logger.LogError("Failed to update tail: {Error}", tailWriteResult);
                return false;
            }

            // Track timing
            var currentTicks = DateTime.UtcNow.Ticks;
            Interlocked.Exchange(ref _lastEnqueueTicks, currentTicks);

            if (Interlocked.Read(ref _firstEnqueueTicks) == 0)
            {
                Interlocked.CompareExchange(ref _firstEnqueueTicks, currentTicks, 0);
            }

            Interlocked.Increment(ref _totalEnqueued);
            return true;

        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<KernelMessage<T>?> TryDequeueAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized");
        }

        return await Task.Run<KernelMessage<T>?>(() =>
        {
            // Read current head
            var headReadResult = OpenCLNative.clEnqueueReadBuffer(
                _context.CommandQueue,
                _deviceHead,
                1u,
                UIntPtr.Zero,
                (nuint)sizeof(int),
                _hostHead,
                0,
                null,
                out _);

            if (headReadResult != CLResultCode.Success)
            {
                _logger.LogError("Failed to read head: {Error}", headReadResult);
                return default;
            }

            var currentHead = Marshal.ReadInt32(_hostHead);

            // Read current tail
            var tailReadResult = OpenCLNative.clEnqueueReadBuffer(
                _context.CommandQueue,
                _deviceTail,
                1u,
                UIntPtr.Zero,
                (nuint)sizeof(int),
                _hostTail,
                0,
                null,
                out _);

            if (tailReadResult != CLResultCode.Success)
            {
                _logger.LogError("Failed to read tail: {Error}", tailReadResult);
                return default;
            }

            var currentTail = Marshal.ReadInt32(_hostTail);

            // Check if empty
            if (currentHead == currentTail)
            {
                return default;
            }

            // Read message from buffer
            var messageSize = Unsafe.SizeOf<KernelMessage<T>>();
            var messagePtr = Marshal.AllocHGlobal(messageSize);
            KernelMessage<T> message;

            try
            {
                var sourceOffset = (nuint)(currentHead * messageSize);
                var readResult = OpenCLNative.clEnqueueReadBuffer(
                    _context.CommandQueue,
                    _deviceBuffer,
                    1u,
                    (UIntPtr)sourceOffset,
                    (nuint)messageSize,
                    messagePtr,
                    0,
                    null,
                    out _);

                if (readResult != CLResultCode.Success)
                {
                    _logger.LogError("Failed to read message: {Error}", readResult);
                    return default;
                }

                unsafe
                {
                    message = Unsafe.Read<KernelMessage<T>>(messagePtr.ToPointer());
                }
            }
            finally
            {
                Marshal.FreeHGlobal(messagePtr);
            }

            // Update head
            var nextHead = (currentHead + 1) & (_capacity - 1);
            Marshal.WriteInt32(_hostHead, nextHead);
            var headWriteResult = OpenCLNative.clEnqueueWriteBuffer(
                _context.CommandQueue,
                _deviceHead,
                1u,
                UIntPtr.Zero,
                (nuint)sizeof(int),
                _hostHead,
                0,
                null,
                out _);

            if (headWriteResult != CLResultCode.Success)
            {
                _logger.LogError("Failed to update head: {Error}", headWriteResult);
                return default;
            }

            // Track timing
            var currentTicks = DateTime.UtcNow.Ticks;
            Interlocked.Exchange(ref _lastDequeueTicks, currentTicks);

            if (message.Timestamp > 0)
            {
                var latencyTicks = currentTicks - message.Timestamp;
                var latencyMicroseconds = latencyTicks / 10;
                Interlocked.Add(ref _totalLatencyMicroseconds, latencyMicroseconds);
                Interlocked.Increment(ref _latencySampleCount);
            }

            Interlocked.Increment(ref _totalDequeued);
            return message;

        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EnqueueAsync(
        KernelMessage<T> message,
        TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        var deadline = timeout == default
            ? DateTime.MaxValue
            : DateTime.UtcNow + timeout;

        while (true)
        {
            if (await TryEnqueueAsync(message, cancellationToken))
            {
                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Failed to enqueue message within timeout period");
            }

            await Task.Delay(1, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<KernelMessage<T>> DequeueAsync(
        TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        var deadline = timeout == default
            ? DateTime.MaxValue
            : DateTime.UtcNow + timeout;

        while (true)
        {
            var message = await TryDequeueAsync(cancellationToken);
            if (message.HasValue)
            {
                return message.Value;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Failed to dequeue message within timeout period");
            }

            await Task.Delay(1, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            return;
        }

        await Task.Run(() =>
        {
            // Reset head and tail to zero
            var zero = 0;
            Marshal.WriteInt32(_hostHead, zero);
            Marshal.WriteInt32(_hostTail, zero);

            OpenCLNative.clEnqueueWriteBuffer(
                _context.CommandQueue,
                _deviceHead,
                1u,
                UIntPtr.Zero,
                (nuint)sizeof(int),
                _hostHead,
                0,
                null,
                out _);

            OpenCLNative.clEnqueueWriteBuffer(
                _context.CommandQueue,
                _deviceTail,
                1u,
                UIntPtr.Zero,
                (nuint)sizeof(int),
                _hostTail,
                0,
                null,
                out _);

            _logger.LogDebug("Message queue cleared");
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MessageQueueStatistics> GetStatisticsAsync()
    {
        var totalEnqueued = Interlocked.Read(ref _totalEnqueued);
        var totalDequeued = Interlocked.Read(ref _totalDequeued);
        var totalDropped = Interlocked.Read(ref _totalDropped);
        var firstEnqueueTicks = Interlocked.Read(ref _firstEnqueueTicks);
        var lastEnqueueTicks = Interlocked.Read(ref _lastEnqueueTicks);
        var lastDequeueTicks = Interlocked.Read(ref _lastDequeueTicks);
        var totalLatencyUs = Interlocked.Read(ref _totalLatencyMicroseconds);
        var latencySamples = Interlocked.Read(ref _latencySampleCount);

        // Calculate throughput
        double enqueueThroughput = 0;
        double dequeueThroughput = 0;

        if (totalEnqueued > 0 && firstEnqueueTicks > 0 && lastEnqueueTicks > firstEnqueueTicks)
        {
            var elapsedSeconds = TimeSpan.FromTicks(lastEnqueueTicks - firstEnqueueTicks).TotalSeconds;
            if (elapsedSeconds > 0)
            {
                enqueueThroughput = totalEnqueued / elapsedSeconds;
            }
        }

        if (totalDequeued > 0 && firstEnqueueTicks > 0 && lastDequeueTicks > firstEnqueueTicks)
        {
            var elapsedSeconds = TimeSpan.FromTicks(lastDequeueTicks - firstEnqueueTicks).TotalSeconds;
            if (elapsedSeconds > 0)
            {
                dequeueThroughput = totalDequeued / elapsedSeconds;
            }
        }

        var averageLatencyUs = latencySamples > 0 ? (double)totalLatencyUs / latencySamples : 0;

        var stats = new MessageQueueStatistics
        {
            TotalEnqueued = totalEnqueued,
            TotalDequeued = totalDequeued,
            TotalDropped = totalDropped,
            Utilization = (double)Count / Capacity,
            EnqueueThroughput = enqueueThroughput,
            DequeueThroughput = dequeueThroughput,
            AverageLatencyUs = averageLatencyUs
        };

        return Task.FromResult(stats);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Disposing OpenCL message queue");

        await Task.Run(() =>
        {
            if (_deviceBuffer != default)
            {
                OpenCLNative.clReleaseMemObject(_deviceBuffer);
                _deviceBuffer = default;
            }

            if (_deviceHead != default)
            {
                OpenCLNative.clReleaseMemObject(_deviceHead);
                _deviceHead = default;
            }

            if (_deviceTail != default)
            {
                OpenCLNative.clReleaseMemObject(_deviceTail);
                _deviceTail = default;
            }

            if (_hostHead != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_hostHead);
                _hostHead = IntPtr.Zero;
            }

            if (_hostTail != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_hostTail);
                _hostTail = IntPtr.Zero;
            }
        });

        _disposed = true;
        _logger.LogInformation("OpenCL message queue disposed");
    }
}
