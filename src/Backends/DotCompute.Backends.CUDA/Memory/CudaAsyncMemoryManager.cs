// Copyright (c) 2025 Michael Ivertowski  
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// Production-grade CUDA async memory manager with stream-ordered allocation,
/// memory pools, and async transfers.
/// </summary>
public sealed class CudaAsyncMemoryManager : BaseMemoryManager
{
    private readonly CudaContext _context;
    private readonly ConcurrentDictionary<IntPtr, MemoryPoolInfo> _memoryPools;
    private readonly ConcurrentDictionary<IntPtr, AsyncAllocationInfo> _allocations;
    private readonly ILogger<CudaAsyncMemoryManager> _logger;
    private IntPtr _defaultMemPool = IntPtr.Zero;
    private bool _asyncMemorySupported;

    public CudaAsyncMemoryManager(
        CudaContext context,
        ILogger<CudaAsyncMemoryManager> logger)
        : base(logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
        _memoryPools = new ConcurrentDictionary<IntPtr, MemoryPoolInfo>();
        _allocations = new ConcurrentDictionary<IntPtr, AsyncAllocationInfo>();
        
        InitializeAsyncMemory();
    }

    /// <summary>
    /// Gets whether async memory operations are supported.
    /// </summary>
    public bool AsyncMemorySupported => _asyncMemorySupported;

    /// <summary>
    /// Initializes async memory support and default memory pool.
    /// </summary>
    private void InitializeAsyncMemory()
    {
        try
        {
            // Check if device supports memory pools (CUDA 11.2+)
            var deviceProps = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref deviceProps, _context.DeviceId);
            
            if (result == CudaError.Success && deviceProps.Major >= 6)
            {
                // Create default memory pool
                result = CudaRuntime.cudaDeviceGetDefaultMemPool(out _defaultMemPool, _context.DeviceId);
                
                if (result == CudaError.Success && _defaultMemPool != IntPtr.Zero)
                {
                    _asyncMemorySupported = true;
                    ConfigureDefaultMemoryPool();
                    _logger.LogInformation("Async memory operations initialized with default pool");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Async memory operations not available, falling back to sync");
            _asyncMemorySupported = false;
        }
    }

    /// <summary>
    /// Configures the default memory pool with optimal settings.
    /// </summary>
    private void ConfigureDefaultMemoryPool()
    {
        if (_defaultMemPool == IntPtr.Zero)
            return;

        try
        {
            // Set release threshold (memory returned to OS when exceeded)
            ulong releaseThreshold = 2UL * 1024 * 1024 * 1024; // 2GB
            var result = CudaRuntime.cudaMemPoolSetAttribute(
                _defaultMemPool,
                CudaMemPoolAttribute.ReleaseThreshold,
                ref releaseThreshold);
            
            if (result != CudaError.Success)
            {
                _logger.LogWarning("Failed to set memory pool release threshold: {Error}", result);
            }

            // Enable reuse of allocations
            int reuseEnabled = 1;
            result = CudaRuntime.cudaMemPoolSetAttribute(
                _defaultMemPool,
                CudaMemPoolAttribute.ReuseAllowOpportunistic,
                ref reuseEnabled);

            // Track pool info
            _memoryPools.TryAdd(_defaultMemPool, new MemoryPoolInfo
            {
                Pool = _defaultMemPool,
                IsDefault = true,
                ReleaseThreshold = releaseThreshold,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure default memory pool");
        }
    }

    /// <summary>
    /// Allocates memory asynchronously on the specified stream.
    /// </summary>
    public async ValueTask<IUnifiedMemoryBuffer> AllocateAsyncOnStream(
        long sizeInBytes,
        IntPtr stream,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        if (!_asyncMemorySupported)
        {
            // Fall back to synchronous allocation
            return await AllocateAsync(sizeInBytes, options, cancellationToken);
        }

        try
        {
            // Allocate memory on stream
            var result = CudaRuntime.cudaMallocAsync(out IntPtr devicePtr, (ulong)sizeInBytes, stream);
            CudaRuntime.CheckError(result, $"async allocating {sizeInBytes} bytes");

            // Track allocation
            var allocationInfo = new AsyncAllocationInfo
            {
                Pointer = devicePtr,
                Size = sizeInBytes,
                Stream = stream,
                Pool = _defaultMemPool,
                AllocatedAt = DateTimeOffset.UtcNow
            };
            
            _allocations.TryAdd(devicePtr, allocationInfo);

            // Create buffer wrapper
            var buffer = new CudaAsyncMemoryBuffer(
                devicePtr, sizeInBytes, stream, this, options);

            TrackBuffer(buffer, sizeInBytes);
            
            _logger.LogDebug("Allocated {Size} bytes asynchronously on stream", sizeInBytes);
            
            return buffer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate {Size} bytes asynchronously", sizeInBytes);
            throw new InvalidOperationException($"Async allocation failed for {sizeInBytes} bytes", ex);
        }
    }

    /// <summary>
    /// Frees memory asynchronously on the specified stream.
    /// </summary>
    public void FreeAsyncOnStream(IntPtr devicePtr, IntPtr stream)
    {
        ThrowIfDisposed();
        
        if (!_asyncMemorySupported || devicePtr == IntPtr.Zero)
            return;

        try
        {
            var result = CudaRuntime.cudaFreeAsync(devicePtr, stream);
            
            if (result != CudaError.Success)
            {
                _logger.LogWarning("Failed to free memory asynchronously: {Error}", result);
            }
            
            _allocations.TryRemove(devicePtr, out _);
            
            _logger.LogDebug("Freed memory asynchronously on stream");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error freeing async memory");
        }
    }

    /// <summary>
    /// Performs async memory copy between device buffers.
    /// </summary>
    public async ValueTask CopyAsyncOnStream(
        IntPtr source,
        IntPtr destination,
        long sizeInBytes,
        IntPtr stream,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        var result = CudaRuntime.cudaMemcpyAsync(
            destination, source, (ulong)sizeInBytes,
            CudaMemcpyKind.DeviceToDevice, stream);
        
        CudaRuntime.CheckError(result, $"async copying {sizeInBytes} bytes");

        // Optionally wait for completion
        if (!cancellationToken.IsCancellationRequested)
        {
            await Task.Run(() =>
            {
                var syncResult = CudaRuntime.cudaStreamSynchronize(stream);
                CudaRuntime.CheckError(syncResult, "synchronizing copy stream");
            }, cancellationToken);
        }
        
        _logger.LogDebug("Copied {Size} bytes asynchronously", sizeInBytes);
    }

    /// <summary>
    /// Performs async memory copy from host to device.
    /// </summary>
    public async ValueTask CopyFromHostAsyncOnStream<T>(
        ReadOnlyMemory<T> source,
        IntPtr devicePtr,
        IntPtr stream,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        var sizeInBytes = source.Length * Unsafe.SizeOf<T>();
        
        using var pinned = source.Pin();
        unsafe
        {
            var result = CudaRuntime.cudaMemcpyAsync(
                devicePtr, (IntPtr)pinned.Pointer, (ulong)sizeInBytes,
                CudaMemcpyKind.HostToDevice, stream);
            
            CudaRuntime.CheckError(result, $"async copying {sizeInBytes} bytes from host");
        }

        // Optionally wait for completion
        if (!cancellationToken.IsCancellationRequested)
        {
            await Task.Run(() =>
            {
                var syncResult = CudaRuntime.cudaStreamSynchronize(stream);
                CudaRuntime.CheckError(syncResult, "synchronizing host-to-device stream");
            }, cancellationToken);
        }
        
        _logger.LogDebug("Copied {Size} bytes from host asynchronously", sizeInBytes);
    }

    /// <summary>
    /// Performs async memory copy from device to host.
    /// </summary>
    public async ValueTask CopyToHostAsyncOnStream<T>(
        IntPtr devicePtr,
        Memory<T> destination,
        IntPtr stream,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        var sizeInBytes = destination.Length * Unsafe.SizeOf<T>();
        
        using var pinned = destination.Pin();
        unsafe
        {
            var result = CudaRuntime.cudaMemcpyAsync(
                (IntPtr)pinned.Pointer, devicePtr, (ulong)sizeInBytes,
                CudaMemcpyKind.DeviceToHost, stream);
            
            CudaRuntime.CheckError(result, $"async copying {sizeInBytes} bytes to host");
        }

        // Wait for completion (required for host memory safety)
        await Task.Run(() =>
        {
            var syncResult = CudaRuntime.cudaStreamSynchronize(stream);
            CudaRuntime.CheckError(syncResult, "synchronizing device-to-host stream");
        }, cancellationToken);
        
        _logger.LogDebug("Copied {Size} bytes to host asynchronously", sizeInBytes);
    }

    /// <summary>
    /// Prefetches memory to a specific device.
    /// </summary>
    public void PrefetchAsync(IntPtr devicePtr, long sizeInBytes, int targetDevice, IntPtr stream)
    {
        ThrowIfDisposed();
        
        if (!_asyncMemorySupported)
            return;

        var result = CudaRuntime.cudaMemPrefetchAsync(
            devicePtr, (ulong)sizeInBytes, targetDevice, stream);
        
        if (result == CudaError.Success)
        {
            _logger.LogDebug("Prefetched {Size} bytes to device {Device}", sizeInBytes, targetDevice);
        }
        else if (result != CudaError.NotSupported)
        {
            _logger.LogWarning("Failed to prefetch memory: {Error}", result);
        }
    }

    /// <summary>
    /// Provides memory usage hints for optimization.
    /// </summary>
    public void MemAdvise(IntPtr devicePtr, long sizeInBytes, CudaMemoryAdvise advice, int targetDevice)
    {
        ThrowIfDisposed();
        
        if (!_asyncMemorySupported)
            return;

        var result = CudaRuntime.cudaMemAdvise(
            devicePtr, (ulong)sizeInBytes, advice, targetDevice);
        
        if (result == CudaError.Success)
        {
            _logger.LogDebug("Set memory advice {Advice} for {Size} bytes", advice, sizeInBytes);
        }
        else if (result != CudaError.NotSupported)
        {
            _logger.LogWarning("Failed to set memory advice: {Error}", result);
        }
    }

    /// <summary>
    /// Creates a custom memory pool with specific properties.
    /// </summary>
    public IntPtr CreateMemoryPool(MemoryPoolProperties properties)
    {
        ThrowIfDisposed();
        
        if (!_asyncMemorySupported)
            throw new NotSupportedException("Memory pools not supported on this device");

        var poolProps = new CudaMemPoolProps
        {
            AllocType = CudaMemAllocationType.Pinned,
            HandleTypes = CudaMemAllocationHandleType.None,
            Location = new CudaMemLocation
            {
                Type = CudaMemLocationType.Device,
                Id = _context.DeviceId
            }
        };

        var result = CudaRuntime.cudaMemPoolCreate(out IntPtr pool, ref poolProps);
        CudaRuntime.CheckError(result, "creating memory pool");

        // Configure pool
        if (properties.ReleaseThreshold > 0)
        {
            CudaRuntime.cudaMemPoolSetAttribute(
                pool, CudaMemPoolAttribute.ReleaseThreshold,
                ref properties.ReleaseThreshold);
        }

        _memoryPools.TryAdd(pool, new MemoryPoolInfo
        {
            Pool = pool,
            IsDefault = false,
            ReleaseThreshold = (ulong)properties.ReleaseThreshold,
            CreatedAt = DateTimeOffset.UtcNow
        });

        _logger.LogInformation("Created custom memory pool with release threshold {Threshold:N0} bytes",
            properties.ReleaseThreshold);

        return pool;
    }

    /// <summary>
    /// Gets memory pool statistics.
    /// </summary>
    public MemoryPoolStatistics GetPoolStatistics(IntPtr? pool = null)
    {
        var targetPool = pool ?? _defaultMemPool;
        if (targetPool == IntPtr.Zero)
        {
            return new MemoryPoolStatistics();
        }

        try
        {
            CudaRuntime.cudaMemPoolGetAttribute(
                targetPool, CudaMemPoolAttribute.Used,
                out ulong usedMemory);
            
            CudaRuntime.cudaMemPoolGetAttribute(
                targetPool, CudaMemPoolAttribute.Reserved,
                out ulong reservedMemory);

            return new MemoryPoolStatistics
            {
                UsedBytes = (long)usedMemory,
                ReservedBytes = (long)reservedMemory,
                AllocationCount = _allocations.Count(a => a.Value.Pool == targetPool)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get pool statistics");
            return new MemoryPoolStatistics();
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask<IUnifiedMemoryBuffer> AllocateBufferCoreAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken)
    {
        // Default stream allocation if async not supported
        if (!_asyncMemorySupported)
        {
            var result = CudaRuntime.cudaMalloc(out IntPtr devicePtr, (ulong)sizeInBytes);
            CudaRuntime.CheckError(result, $"allocating {sizeInBytes} bytes");
            
            return new CudaAsyncMemoryBuffer(
                devicePtr, sizeInBytes, IntPtr.Zero, this, options);
        }

        // Use default stream for standard allocation
        return await AllocateAsyncOnStream(
            sizeInBytes, IntPtr.Zero, options, cancellationToken);
    }

    /// <inheritdoc/>
    protected override IUnifiedMemoryBuffer CreateViewCore(IUnifiedMemoryBuffer buffer, long offset, long length)
    {
        if (buffer is CudaAsyncMemoryBuffer cudaBuffer)
        {
            return new CudaAsyncMemoryBufferView(cudaBuffer, offset, length);
        }
        
        throw new ArgumentException("Buffer must be a CUDA async memory buffer", nameof(buffer));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Free all async allocations
            foreach (var allocation in _allocations.Values)
            {
                try
                {
                    FreeAsyncOnStream(allocation.Pointer, allocation.Stream);
                }
                catch { /* Best effort */ }
            }
            
            // Destroy custom memory pools
            foreach (var poolInfo in _memoryPools.Values.Where(p => !p.IsDefault))
            {
                try
                {
                    CudaRuntime.cudaMemPoolDestroy(poolInfo.Pool);
                }
                catch { /* Best effort */ }
            }
            
            _allocations.Clear();
            _memoryPools.Clear();
        }
        
        base.Dispose(disposing);
    }

    /// <summary>
    /// Information about an async allocation.
    /// </summary>
    private sealed class AsyncAllocationInfo
    {
        public IntPtr Pointer { get; init; }
        public long Size { get; init; }
        public IntPtr Stream { get; init; }
        public IntPtr Pool { get; init; }
        public DateTimeOffset AllocatedAt { get; init; }
    }

    /// <summary>
    /// Information about a memory pool.
    /// </summary>
    private sealed class MemoryPoolInfo
    {
        public IntPtr Pool { get; init; }
        public bool IsDefault { get; init; }
        public ulong ReleaseThreshold { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }
}

/// <summary>
/// CUDA async memory buffer implementation.
/// </summary>
internal sealed class CudaAsyncMemoryBuffer : IUnifiedMemoryBuffer
{
    private readonly IntPtr _devicePtr;
    private readonly long _sizeInBytes;
    private readonly IntPtr _stream;
    private readonly CudaAsyncMemoryManager _manager;
    private readonly MemoryOptions _options;
    private bool _disposed;

    public CudaAsyncMemoryBuffer(
        IntPtr devicePtr,
        long sizeInBytes,
        IntPtr stream,
        CudaAsyncMemoryManager manager,
        MemoryOptions options)
    {
        _devicePtr = devicePtr;
        _sizeInBytes = sizeInBytes;
        _stream = stream;
        _manager = manager;
        _options = options;
    }

    public IntPtr DevicePointer => _devicePtr;
    public long SizeInBytes => _sizeInBytes;
    public MemoryOptions Options => _options;
    public bool IsDisposed => _disposed;

    public async ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        var destPtr = _devicePtr + (nint)offset;
        await _manager.CopyFromHostAsyncOnStream(source, destPtr, _stream, cancellationToken);
    }

    public async ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        var srcPtr = _devicePtr + (nint)offset;
        await _manager.CopyToHostAsyncOnStream(srcPtr, destination, _stream, cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _manager.FreeAsyncOnStream(_devicePtr, _stream);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await Task.Run(() => _manager.FreeAsyncOnStream(_devicePtr, _stream));
        }
    }
}

/// <summary>
/// View over a CUDA async memory buffer.
/// </summary>
internal sealed class CudaAsyncMemoryBufferView : IUnifiedMemoryBuffer
{
    private readonly CudaAsyncMemoryBuffer _parent;
    private readonly long _offset;
    private readonly long _length;

    public CudaAsyncMemoryBufferView(CudaAsyncMemoryBuffer parent, long offset, long length)
    {
        _parent = parent;
        _offset = offset;
        _length = length;
    }

    public long SizeInBytes => _length;
    public MemoryOptions Options => _parent.Options;
    public bool IsDisposed => _parent.IsDisposed;

    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        => _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);

    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        => _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);

    public void Dispose() { /* View doesn't own memory */ }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Memory pool properties.
/// </summary>
public sealed class MemoryPoolProperties
{
    public long ReleaseThreshold { get; init; } = 2L * 1024 * 1024 * 1024; // 2GB default
    public bool AllowOpportunisticReuse { get; init; } = true;
}

/// <summary>
/// Memory pool statistics.
/// </summary>
public sealed class MemoryPoolStatistics
{
    public long UsedBytes { get; init; }
    public long ReservedBytes { get; init; }
    public int AllocationCount { get; init; }
}
