// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.RingKernels;

/// <summary>
/// Metal-based lock-free message queue for inter-kernel communication.
/// </summary>
/// <typeparam name="T">Message payload type (must be unmanaged).</typeparam>
/// <remarks>
/// Implements a GPU-resident lock-free ring buffer using Metal atomic operations.
/// The queue is optimized for high throughput concurrent access from
/// multiple GPU threads using Metal memory fences and threadgroup barriers.
/// </remarks>
public sealed class MetalMessageQueue<T> : IMessageQueue<T> where T : unmanaged
{
    private readonly ILogger<MetalMessageQueue<T>> _logger;
    private readonly int _capacity;
    private IntPtr _device;
    private IntPtr _buffer;          // MTLBuffer for message data
    private IntPtr _headBuffer;      // MTLBuffer for atomic head counter
    private IntPtr _tailBuffer;      // MTLBuffer for atomic tail counter
    private IntPtr _hostHead;        // Host memory for head value
    private IntPtr _hostTail;        // Host memory for tail value
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
    public bool IsFull => Count >= Capacity - 1; // Ring buffer keeps 1 slot empty

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            if (!_initialized || _disposed)
            {
                return 0;
            }

            // Read head/tail from Metal buffers
            var headPtr = MetalNative.GetBufferContents(_headBuffer);
            var tailPtr = MetalNative.GetBufferContents(_tailBuffer);

            var head = Marshal.ReadInt32(headPtr);
            var tail = Marshal.ReadInt32(tailPtr);

            // Calculate size with proper wraparound handling
            var size = (tail - head + _capacity) % _capacity;

            return size;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalMessageQueue{T}"/> class.
    /// </summary>
    /// <param name="capacity">Queue capacity (must be power of 2).</param>
    /// <param name="logger">Logger instance.</param>
    public MetalMessageQueue(int capacity, ILogger<MetalMessageQueue<T>> logger)
    {
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
        {
            throw new ArgumentException("Capacity must be a positive power of 2", nameof(capacity));
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        return new MetalDeviceBufferWrapper(_buffer, bufferSizeInBytes, MemoryOptions.None);
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer GetHeadPtr()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized. Call InitializeAsync first.");
        }

        return new MetalDeviceBufferWrapper(_headBuffer, sizeof(int), MemoryOptions.None);
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer GetTailPtr()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized. Call InitializeAsync first.");
        }

        return new MetalDeviceBufferWrapper(_tailBuffer, sizeof(int), MemoryOptions.None);
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        _logger.LogInformation(
            "Initializing Metal message queue with capacity {Capacity} for type {Type}",
            _capacity,
            typeof(T).Name);

        await Task.Run(() =>
        {
            // Get or create Metal device
            _device = MetalNative.CreateSystemDefaultDevice();
            if (_device == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create Metal device");
            }

            // Calculate sizes
            var messageSize = Unsafe.SizeOf<KernelMessage<T>>();
            var bufferSize = (nuint)(messageSize * _capacity);
            var atomicSize = (nuint)sizeof(int);

            // Allocate Metal buffers (using Shared storage mode for unified memory on Apple Silicon)
            _buffer = MetalNative.CreateBuffer(_device, bufferSize, MetalStorageMode.Shared);
            _headBuffer = MetalNative.CreateBuffer(_device, atomicSize, MetalStorageMode.Shared);
            _tailBuffer = MetalNative.CreateBuffer(_device, atomicSize, MetalStorageMode.Shared);

            // Allocate host memory for fast access
            _hostHead = Marshal.AllocHGlobal(sizeof(int));
            _hostTail = Marshal.AllocHGlobal(sizeof(int));

            // Initialize head/tail to zero
            Marshal.WriteInt32(_hostHead, 0);
            Marshal.WriteInt32(_hostTail, 0);

            // Copy initial values to Metal buffers
            var headPtr = MetalNative.GetBufferContents(_headBuffer);
            var tailPtr = MetalNative.GetBufferContents(_tailBuffer);

            // Copy zero values to Metal buffers using unsafe code
            unsafe
            {
                *(int*)headPtr.ToPointer() = 0;
                *(int*)tailPtr.ToPointer() = 0;
            }

            // Mark modified ranges (required for Shared buffers)
            MetalNative.DidModifyRange(_headBuffer, 0, sizeof(int));
            MetalNative.DidModifyRange(_tailBuffer, 0, sizeof(int));

            // Zero the message buffer
            var bufferPtr = MetalNative.GetBufferContents(_buffer);
            unsafe
            {
                NativeMemory.Clear(bufferPtr.ToPointer(), (nuint)bufferSize);
            }
            MetalNative.DidModifyRange(_buffer, 0, (long)bufferSize);

            _initialized = true;
            _logger.LogDebug("Metal message queue initialized successfully");

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
            // Read current tail from Metal buffer
            var tailPtr = MetalNative.GetBufferContents(_tailBuffer);
            var currentTail = Marshal.ReadInt32(tailPtr);

            var nextTail = (currentTail + 1) & (_capacity - 1);

            // Read current head
            var headPtr = MetalNative.GetBufferContents(_headBuffer);
            var currentHead = Marshal.ReadInt32(headPtr);

            // Check if full
            if (nextTail == currentHead)
            {
                Interlocked.Increment(ref _totalDropped);
                return false;
            }

            // Write message to buffer
            var messageSize = Unsafe.SizeOf<KernelMessage<T>>();
            var bufferPtr = MetalNative.GetBufferContents(_buffer);
            var targetOffset = bufferPtr + (currentTail * messageSize);

            unsafe
            {
                Unsafe.Write(targetOffset.ToPointer(), message);
            }

            // Mark modified range
            MetalNative.DidModifyRange(_buffer, currentTail * messageSize, messageSize);

            // Update tail
            Marshal.WriteInt32(tailPtr, nextTail);
            MetalNative.DidModifyRange(_tailBuffer, 0, sizeof(int));

            // Track timing for throughput calculation
            var currentTicks = DateTime.UtcNow.Ticks;
            Interlocked.Exchange(ref _lastEnqueueTicks, currentTicks);

            // Set first enqueue time if this is the first message
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

        return await Task.Run(() =>
        {
            // Read current head from Metal buffer
            var headPtr = MetalNative.GetBufferContents(_headBuffer);
            var currentHead = Marshal.ReadInt32(headPtr);

            // Read current tail
            var tailPtr = MetalNative.GetBufferContents(_tailBuffer);
            var currentTail = Marshal.ReadInt32(tailPtr);

            // Check if empty
            if (currentHead == currentTail)
            {
                return (KernelMessage<T>?)null;
            }

            // Read message from buffer
            var messageSize = Unsafe.SizeOf<KernelMessage<T>>();
            var bufferPtr = MetalNative.GetBufferContents(_buffer);
            var sourceOffset = bufferPtr + (currentHead * messageSize);

            KernelMessage<T> message;
            unsafe
            {
                message = Unsafe.Read<KernelMessage<T>>(sourceOffset.ToPointer());
            }

            // Update head
            var nextHead = (currentHead + 1) & (_capacity - 1);
            Marshal.WriteInt32(headPtr, nextHead);
            MetalNative.DidModifyRange(_headBuffer, 0, sizeof(int));

            // Track timing for throughput and latency calculation
            var currentTicks = DateTime.UtcNow.Ticks;
            Interlocked.Exchange(ref _lastDequeueTicks, currentTicks);

            // Calculate latency if message has timestamp
            if (message.Timestamp > 0)
            {
                var latencyTicks = currentTicks - message.Timestamp;
                var latencyMicroseconds = latencyTicks / 10; // Convert 100ns ticks to microseconds
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
            var headPtr = MetalNative.GetBufferContents(_headBuffer);
            var tailPtr = MetalNative.GetBufferContents(_tailBuffer);

            Marshal.WriteInt32(headPtr, 0);
            Marshal.WriteInt32(tailPtr, 0);

            MetalNative.DidModifyRange(_headBuffer, 0, sizeof(int));
            MetalNative.DidModifyRange(_tailBuffer, 0, sizeof(int));

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

        // Calculate throughput (messages per second)
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

        // Calculate average latency
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

        _logger.LogDebug("Disposing Metal message queue");

        await Task.Run(() =>
        {
            if (_buffer != IntPtr.Zero)
            {
                MetalNative.ReleaseBuffer(_buffer);
                _buffer = IntPtr.Zero;
            }

            if (_headBuffer != IntPtr.Zero)
            {
                MetalNative.ReleaseBuffer(_headBuffer);
                _headBuffer = IntPtr.Zero;
            }

            if (_tailBuffer != IntPtr.Zero)
            {
                MetalNative.ReleaseBuffer(_tailBuffer);
                _tailBuffer = IntPtr.Zero;
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

            if (_device != IntPtr.Zero)
            {
                MetalNative.ReleaseDevice(_device);
                _device = IntPtr.Zero;
            }
        });

        _disposed = true;
        _logger.LogInformation("Metal message queue disposed");
    }
}
