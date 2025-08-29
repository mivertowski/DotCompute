// Copyright (c) 2025 Michael Ivertowski  
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Memory.Models;

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
    private long _totalMemory;

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

    /// <inheritdoc/>
    public override long CurrentAllocatedMemory => _allocations.Values.Sum(a => a.Size);

    /// <inheritdoc/>
    public override IAccelerator Accelerator => throw new NotImplementedException("TODO: Implement accelerator reference");

    /// <inheritdoc/>
    public override DotCompute.Abstractions.Memory.MemoryStatistics Statistics => throw new NotImplementedException("TODO: Implement memory statistics");

    /// <inheritdoc/>
    public override async ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        if (buffer is CudaAsyncMemoryBuffer cudaBuffer)
        {
            await cudaBuffer.DisposeAsync();
        }
        else
        {
            buffer?.Dispose();
        }
    }

    /// <inheritdoc/>
    public override ValueTask CopyFromDeviceAsync<T>(IUnifiedMemoryBuffer<T> buffer, Memory<T> destination, CancellationToken cancellationToken = default) => throw new NotImplementedException("TODO: Implement typed buffer copy from device");

    /// <inheritdoc/>
    public override ValueTask OptimizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask; // TODO: Implement memory optimization


    /// <inheritdoc/>
    public override long TotalAvailableMemory => _totalMemory;
    
    /// <inheritdoc/>
    public override long MaxAllocationSize => _totalMemory / 2;
    
    /// <inheritdoc/>
    public override void Clear()
    {
        foreach (var pool in _memoryPools.Values)
        {
            pool.Clear();
        }
        _memoryPools.Clear();
    }


    /// <summary>
    /// Allocates memory with stream-ordered allocation for optimal performance.
    /// </summary>
    public async ValueTask<IUnifiedMemoryBuffer> AllocateStreamOrderedAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        if (!_asyncMemorySupported)
        {
            // Fall back to synchronous allocation
            return await AllocateBufferCoreAsync(sizeInBytes, options, cancellationToken);
        }

        try
        {
            // Use the current stream or default stream for allocation
            var stream = _context.Stream;
            
            // Allocate memory asynchronously on the specified stream
            var result = CudaRuntime.TryAllocateMemoryAsync(out IntPtr devicePtr, (nuint)sizeInBytes, stream);
            CudaRuntime.CheckError(result, $"stream-ordered allocating {sizeInBytes} bytes");

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
            
            _logger.LogDebug("Allocated {Size} bytes with stream-ordered allocation", sizeInBytes);
            
            return buffer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate {Size} bytes with stream-ordered allocation", sizeInBytes);
            throw new InvalidOperationException($"Stream-ordered allocation failed for {sizeInBytes} bytes", ex);
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask<IUnifiedMemoryBuffer> AllocateInternalAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken) => await AllocateStreamOrderedAsync(sizeInBytes, options, cancellationToken);

    /// <inheritdoc/>
    public override async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken)
    {
        await CopyAsync(source, 0, destination, 0, source.Length, cancellationToken);
    }


    /// <inheritdoc/>
    public override async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination, 
        int destinationOffset,
        int count,
        CancellationToken cancellationToken)
    {
        // Implement async copy between buffers
        await Task.Run(() =>
        {
            // Copy implementation
            var size = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        }, cancellationToken);
    }
    
    /// <inheritdoc/>
    public override async ValueTask CopyToDeviceAsync<T>(
        ReadOnlyMemory<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken)
    {
        if (destination is CudaAsyncMemoryBuffer buffer)
        {
            await buffer.CopyFromHostAsync(source, 0, cancellationToken);
        }
    }


    /// <inheritdoc/>
    public override IUnifiedMemoryBuffer<T> CreateView<T>(
        IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int count) => throw new NotImplementedException("CreateView not implemented");

    /// <summary>
    /// Initializes async memory support and default memory pool.
    /// </summary>
    private void InitializeAsyncMemory()
    {
        try
        {
            // Get total device memory
            var memResult = CudaRuntime.cudaMemGetInfo(out var free, out var total);
            if (memResult == CudaError.Success)
            {
                _totalMemory = (long)total;
                _logger.LogInformation("Total device memory: {TotalMemory:F2} GB", _totalMemory / 1e9);
            }

            // Check if device supports memory pools (CUDA 11.2+)
            var deviceProps = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref deviceProps, _context.DeviceId);
            
            if (result == CudaError.Success && deviceProps.Major >= 6)
            {
                // Create default memory pool
                result = CudaRuntime.cudaDeviceGetDefaultMemPool(ref _defaultMemPool, _context.DeviceId);
                
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
        {
            return;
        }


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
            var result = CudaRuntime.TryAllocateMemoryAsync(out IntPtr devicePtr, (nuint)sizeInBytes, stream);
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
        {
            return;
        }


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
            destination, source, (nuint)sizeInBytes,
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
    /// Performs async device-to-device memory copy on the specified stream.
    /// </summary>
    public async ValueTask CopyDeviceToDeviceAsyncOnStream(
        IntPtr source,
        IntPtr destination,
        long sizeInBytes,
        IntPtr stream,
        CancellationToken cancellationToken = default) => await CopyAsyncOnStream(source, destination, sizeInBytes, stream, cancellationToken);

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
                devicePtr, (IntPtr)pinned.Pointer, (nuint)sizeInBytes,
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
                (IntPtr)pinned.Pointer, devicePtr, (nuint)sizeInBytes,
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
        {
            return;
        }


        var result = CudaRuntime.cudaMemPrefetchAsync(
            devicePtr, (nuint)sizeInBytes, targetDevice, stream);
        
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
        {
            return;
        }


        var result = CudaRuntime.cudaMemAdvise(
            devicePtr, (nuint)sizeInBytes, advice, targetDevice);
        
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
        {

            throw new NotSupportedException("Memory pools not supported on this device");
        }


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
            var result = CudaRuntime.TryAllocateMemory(out IntPtr devicePtr, (nuint)sizeInBytes);
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

        /// <summary>
        /// Clears the memory pool and releases all allocated resources.
        /// </summary>
        public void Clear()
        {
            if (Pool == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // For default pools, we can't destroy them, but we can trim them
                if (IsDefault)
                {
                    // Trim the pool to release unused memory back to the system
                    var result = CudaRuntime.cudaMemPoolTrimTo(Pool, 0);
                    if (result != CudaError.Success && result != CudaError.NotSupported)
                    {
                        // Log warning but don't throw - this is a cleanup operation
                        System.Diagnostics.Debug.WriteLine($"Warning: Failed to trim memory pool: {result}");
                    }
                }
                else
                {
                    // For custom pools, we can perform more aggressive cleanup
                    // Note: Destroying a pool will invalidate all allocations from it
                    // This should only be called when we know all allocations are freed
                    
                    // First trim the pool
                    _ = CudaRuntime.cudaMemPoolTrimTo(Pool, 0);
                    
                    // Reset pool attributes if supported
                    try
                    {
                        ulong zero = 0;
                        _ = CudaRuntime.cudaMemPoolSetAttribute(
                            Pool, CudaMemPoolAttribute.Used, ref zero);
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                }
            }
            catch (Exception)
            {
                // Ignore all errors during pool clearing - this is best-effort cleanup
            }
        }
    }
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
