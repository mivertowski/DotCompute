// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Native.Exceptions;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using static DotCompute.Backends.CUDA.Native.CudaRuntime;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// CUDA-based lock-free message queue for inter-kernel communication.
/// </summary>
/// <typeparam name="T">Message payload type (must be unmanaged).</typeparam>
/// <remarks>
/// Implements a GPU-resident lock-free ring buffer using CUDA atomics.
/// The queue is optimized for high throughput concurrent access from
/// multiple GPU threads.
/// </remarks>
public sealed class CudaMessageQueue<T> : IMessageQueue<T> where T : unmanaged
{
    private readonly ILogger<CudaMessageQueue<T>> _logger;
    private readonly int _capacity;
    private IntPtr _deviceBuffer;
    private IntPtr _deviceHead;
    private IntPtr _deviceTail;
    private IntPtr _hostHead;
    private IntPtr _hostTail;
    private IntPtr _context;
    private int _deviceId = -1;  // Device ID for primary context release
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

            // Set context as current for this thread (CUDA contexts are thread-local)
            var ctxResult = cuCtxSetCurrent(_context);
            if (ctxResult != CudaError.Success)
            {
                _logger.LogError("cuCtxSetCurrent failed in Count getter: {Error}", ctxResult);
                return 0;
            }

            // Read head/tail from device
            int head, tail;
            CudaApi.cuMemcpyDtoH(_hostHead, _deviceHead, (nuint)sizeof(int));
            CudaApi.cuMemcpyDtoH(_hostTail, _deviceTail, (nuint)sizeof(int));

            head = Marshal.ReadInt32(_hostHead);
            tail = Marshal.ReadInt32(_hostTail);

            // Calculate size with proper wraparound handling
            // Ring buffer keeps 1 slot empty, so max count is capacity - 1
            var size = (tail - head + _capacity) % _capacity;

            return size;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaMessageQueue{T}"/> class.
    /// </summary>
    /// <param name="capacity">Queue capacity (must be power of 2).</param>
    /// <param name="logger">Logger instance.</param>
    public CudaMessageQueue(int capacity, ILogger<CudaMessageQueue<T>> logger)
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
        return new CudaDevicePointerBuffer(_deviceBuffer, bufferSizeInBytes, MemoryOptions.None);
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer GetHeadPtr()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized. Call InitializeAsync first.");
        }

        return new CudaDevicePointerBuffer(_deviceHead, sizeof(int), MemoryOptions.None);
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer GetTailPtr()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized. Call InitializeAsync first.");
        }

        return new CudaDevicePointerBuffer(_deviceTail, sizeof(int), MemoryOptions.None);
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

#pragma warning disable XFIX003 // Use LoggerMessage.Define - initialization code, not hot path
        _logger.LogInformation(
            "Initializing CUDA message queue with capacity {Capacity} for type {Type}",
            _capacity,
            typeof(T).Name);
#pragma warning restore XFIX003

        await Task.Run(() =>
        {
            // Initialize CUDA Driver API (safe to call multiple times)
            var initResult = cuInit(0);
            if (initResult is not CudaError.Success and not ((CudaError)4)) // 4 = CUDA_ERROR_ALREADY_INITIALIZED
            {
                throw new InvalidOperationException($"Failed to initialize CUDA Driver API: {initResult}");
            }

            // Get device handle for device 0
            var getDeviceResult = CudaRuntime.cuDeviceGet(out var device, 0);
            if (getDeviceResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to get CUDA device: {getDeviceResult}");
            }

            // Initialize Runtime API first (activates primary context)
            // This is REQUIRED for memory allocation to work with primary context
            var setDeviceResult = CudaRuntime.cudaSetDevice(device);
            if (setDeviceResult != CudaError.Success)
            {
                _logger.LogWarning("cudaSetDevice({Device}) returned {Error}, continuing anyway", device, setDeviceResult);
            }

            // Retain primary context for Driver API operations
            var ctxResult = CudaRuntime.cuDevicePrimaryCtxRetain(ref _context, device);
            if (ctxResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to retain primary CUDA context: {ctxResult}");
            }

            _deviceId = device;  // Store for cleanup

            // Set as current for Driver API operations
            var setCtxResult = CudaRuntime.cuCtxSetCurrent(_context);
            if (setCtxResult != CudaError.Success)
            {
                CudaRuntime.cuDevicePrimaryCtxRelease(device);
                _deviceId = -1;
                throw new InvalidOperationException($"Failed to set primary context as current: {setCtxResult}");
            }

            // Calculate sizes - use Unsafe.SizeOf for generic types
            var messageSize = Unsafe.SizeOf<KernelMessage<T>>();
            var bufferSize = (nuint)(messageSize * _capacity);
            var atomicSize = (nuint)sizeof(int);

            // Allocate device memory using Runtime API (works with primary context)
            var bufferResult = CudaRuntime.cudaMalloc(ref _deviceBuffer, (ulong)bufferSize);
            if (bufferResult != CudaError.Success)
            {
                throw new CudaException(
                    $"Failed to allocate CUDA device buffer ({bufferSize} bytes): {CudaRuntime.GetErrorString(bufferResult)}",
                    bufferResult);
            }

            var headResult = CudaRuntime.cudaMalloc(ref _deviceHead, (ulong)atomicSize);
            if (headResult != CudaError.Success)
            {
                CudaRuntime.cudaFree(_deviceBuffer);
                _deviceBuffer = IntPtr.Zero;
                throw new CudaException(
                    $"Failed to allocate CUDA head pointer: {CudaRuntime.GetErrorString(headResult)}",
                    headResult);
            }

            var tailResult = CudaRuntime.cudaMalloc(ref _deviceTail, (ulong)atomicSize);
            if (tailResult != CudaError.Success)
            {
                CudaRuntime.cudaFree(_deviceBuffer);
                CudaRuntime.cudaFree(_deviceHead);
                _deviceBuffer = IntPtr.Zero;
                _deviceHead = IntPtr.Zero;
                throw new CudaException(
                    $"Failed to allocate CUDA tail pointer: {CudaRuntime.GetErrorString(tailResult)}",
                    tailResult);
            }

            // Allocate host pinned memory for fast transfers
            _hostHead = Marshal.AllocHGlobal(sizeof(int));
            _hostTail = Marshal.AllocHGlobal(sizeof(int));

            // Initialize head/tail to zero
            var zero = 0;
            Marshal.WriteInt32(_hostHead, zero);
            Marshal.WriteInt32(_hostTail, zero);

            // Use Runtime API for memory copies (works with primary context)
            var headCopyResult = CudaRuntime.cudaMemcpy(_deviceHead, _hostHead, atomicSize, CudaMemcpyKind.HostToDevice);
            if (headCopyResult != CudaError.Success)
            {
                CleanupDeviceMemory();
                throw new InvalidOperationException($"Failed to initialize CUDA head pointer: {CudaRuntime.GetErrorString(headCopyResult)}");
            }

            var tailCopyResult = CudaRuntime.cudaMemcpy(_deviceTail, _hostTail, atomicSize, CudaMemcpyKind.HostToDevice);
            if (tailCopyResult != CudaError.Success)
            {
                CleanupDeviceMemory();
                throw new InvalidOperationException($"Failed to initialize CUDA tail pointer: {CudaRuntime.GetErrorString(tailCopyResult)}");
            }

            // Zero the buffer using Runtime API (consistent with cudaMalloc above)
            var memsetResult = CudaRuntime.cudaMemset(_deviceBuffer, 0, bufferSize);
            if (memsetResult != CudaError.Success)
            {
                CleanupDeviceMemory();
                throw new InvalidOperationException($"Failed to zero CUDA buffer: {CudaRuntime.GetErrorString(memsetResult)}");
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
            // Set context as current for this thread (CUDA contexts are thread-local)
            var ctxResult = cuCtxSetCurrent(_context);
            if (ctxResult != CudaError.Success)
            {
                _logger.LogError("cuCtxSetCurrent failed in TryEnqueueAsync: {Error}", ctxResult);
                return false;
            }

            // Read current tail
            CudaApi.cuMemcpyDtoH(_hostTail, _deviceTail, (nuint)sizeof(int));
            var currentTail = Marshal.ReadInt32(_hostTail);

            var nextTail = (currentTail + 1) & (_capacity - 1);

            // Read current head
            CudaApi.cuMemcpyDtoH(_hostHead, _deviceHead, (nuint)sizeof(int));
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
                var targetOffset = _deviceBuffer + (currentTail * messageSize);
                var copyResult = CudaApi.cuMemcpyHtoD(targetOffset, messagePtr, (nuint)messageSize);
                if (copyResult != CudaError.Success)
                {
                    _logger.LogError("cuMemcpyHtoD failed: {Error}", copyResult);
                    Interlocked.Increment(ref _totalDropped);
                    return false;
                }

                // Synchronize to ensure write completes (using Driver API for consistency)
                var syncResult = cuCtxSynchronize();
                if (syncResult != CudaError.Success)
                {
                    _logger.LogError("cuCtxSynchronize after write failed: {Error}", syncResult);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(messagePtr);
            }

            // Update tail
            Marshal.WriteInt32(_hostTail, nextTail);
            CudaApi.cuMemcpyHtoD(_deviceTail, _hostTail, (nuint)sizeof(int));
            cuCtxSynchronize();

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
            // Set context as current for this thread (CUDA contexts are thread-local)
            var ctxResult = cuCtxSetCurrent(_context);
            if (ctxResult != CudaError.Success)
            {
                _logger.LogError("cuCtxSetCurrent failed in TryDequeueAsync: {Error}", ctxResult);
                return null;
            }

            // Read current head
            CudaApi.cuMemcpyDtoH(_hostHead, _deviceHead, (nuint)sizeof(int));
            var currentHead = Marshal.ReadInt32(_hostHead);

            // Read current tail
            CudaApi.cuMemcpyDtoH(_hostTail, _deviceTail, (nuint)sizeof(int));
            var currentTail = Marshal.ReadInt32(_hostTail);

            // Check if empty
            if (currentHead == currentTail)
            {
                return (KernelMessage<T>?)null;
            }

            // Read message from buffer
            var messageSize = Unsafe.SizeOf<KernelMessage<T>>();
            var messagePtr = Marshal.AllocHGlobal(messageSize);
            KernelMessage<T> message;

            try
            {
                var sourceOffset = _deviceBuffer + (currentHead * messageSize);
                var copyResult = CudaApi.cuMemcpyDtoH(messagePtr, sourceOffset, (nuint)messageSize);
                if (copyResult != CudaError.Success)
                {
                    _logger.LogError("cuMemcpyDtoH failed: {Error}", copyResult);
                    return null;
                }

                var syncResult = cuCtxSynchronize();
                if (syncResult != CudaError.Success)
                {
                    _logger.LogError("cuCtxSynchronize after read failed: {Error}", syncResult);
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
            CudaApi.cuMemcpyHtoD(_deviceHead, _hostHead, (nuint)sizeof(int));
            cuCtxSynchronize();

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
            // Set context as current for this thread (CUDA contexts are thread-local)
            var ctxResult = cuCtxSetCurrent(_context);
            if (ctxResult != CudaError.Success)
            {
                _logger.LogError("cuCtxSetCurrent failed in ClearAsync: {Error}", ctxResult);
                return;
            }

            // Reset head and tail to zero
            var zero = 0;
            Marshal.WriteInt32(_hostHead, zero);
            Marshal.WriteInt32(_hostTail, zero);

            CudaApi.cuMemcpyHtoD(_deviceHead, _hostHead, (nuint)sizeof(int));
            CudaApi.cuMemcpyHtoD(_deviceTail, _hostTail, (nuint)sizeof(int));

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

        _logger.LogDebug("Disposing CUDA message queue");

        await Task.Run(() =>
        {
            // Free device memory using Runtime API (allocated with cudaMalloc)
            if (_deviceBuffer != IntPtr.Zero)
            {
                CudaRuntime.cudaFree(_deviceBuffer);
                _deviceBuffer = IntPtr.Zero;
            }

            if (_deviceHead != IntPtr.Zero)
            {
                CudaRuntime.cudaFree(_deviceHead);
                _deviceHead = IntPtr.Zero;
            }

            if (_deviceTail != IntPtr.Zero)
            {
                CudaRuntime.cudaFree(_deviceTail);
                _deviceTail = IntPtr.Zero;
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

            // Release primary context reference (don't destroy, it's shared)
            if (_deviceId >= 0)
            {
                CudaRuntime.cuDevicePrimaryCtxRelease(_deviceId);
                _deviceId = -1;
            }
            _context = IntPtr.Zero;
        });

        _disposed = true;
        _logger.LogInformation("CUDA message queue disposed");
    }

    /// <summary>
    /// Cleans up device memory allocations. Used during initialization failure.
    /// </summary>
    private void CleanupDeviceMemory()
    {
        if (_deviceBuffer != IntPtr.Zero)
        {
            CudaRuntime.cudaFree(_deviceBuffer);
            _deviceBuffer = IntPtr.Zero;
        }

        if (_deviceHead != IntPtr.Zero)
        {
            CudaRuntime.cudaFree(_deviceHead);
            _deviceHead = IntPtr.Zero;
        }

        if (_deviceTail != IntPtr.Zero)
        {
            CudaRuntime.cudaFree(_deviceTail);
            _deviceTail = IntPtr.Zero;
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
    }
}
