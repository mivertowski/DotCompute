// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CUDA backend has dynamic logging requirements

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// CUDA unified memory manager implementing IMemoryManager with unified memory support.
/// Optimized for 8GB VRAM with memory pooling and automatic migration.
/// </summary>
public sealed class CudaUnifiedMemoryManager : IMemoryManager, IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly CudaMemoryPool _memoryPool;
    private readonly CudaMemoryStatistics _statistics;
    private readonly ConcurrentDictionary<IMemoryBuffer, CudaUnifiedMemoryBuffer> _buffers = new();
    private readonly Timer _memoryPressureTimer;
    private bool _disposed;

    // Configuration for 8GB VRAM optimization
    private const long MaxDeviceMemory = 8L * 1024 * 1024 * 1024; // 8GB
    private const long MemoryPressureThreshold = (long)(MaxDeviceMemory * 0.85); // 85% threshold
    private const long UnifiedMemoryPreferredSize = 2L * 1024 * 1024 * 1024; // 2GB for unified
    
    public CudaUnifiedMemoryManager(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _memoryPool = new CudaMemoryPool(context, logger);
        _statistics = new CudaMemoryStatistics();
        
        // Set up memory pressure monitoring
        _memoryPressureTimer = new Timer(CheckMemoryPressure, null, 
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        
        _logger.LogInformation("Initialized CUDA unified memory manager for device {DeviceId}", context.DeviceId);
        
        // Configure unified memory optimizations
        ConfigureUnifiedMemoryOptimizations();
    }

    public async ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes, 
        MemoryOptions options = MemoryOptions.None, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (sizeInBytes <= 0)
            throw new ArgumentException("Size must be greater than zero", nameof(sizeInBytes));

        try
        {
            _logger.LogDebug("Allocating {Size}MB of CUDA unified memory with options {Options}", 
                sizeInBytes / (1024 * 1024), options);

            // Check if we should use unified memory or regular device memory
            var useUnifiedMemory = ShouldUseUnifiedMemory(sizeInBytes, options);
            
            CudaUnifiedMemoryBuffer buffer;
            
            if (useUnifiedMemory)
            {
                buffer = await AllocateUnifiedMemoryAsync(sizeInBytes, options, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                buffer = await AllocateDeviceMemoryAsync(sizeInBytes, options, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!_buffers.TryAdd(buffer, buffer))
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
                throw new MemoryException("Failed to track allocated buffer");
            }

            _statistics.RecordAllocation(sizeInBytes);
            
            _logger.LogDebug("Successfully allocated CUDA buffer of {Size}MB", 
                sizeInBytes / (1024 * 1024));
            
            return buffer;
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger.LogError(ex, "Failed to allocate CUDA unified memory");
            throw new MemoryException($"Failed to allocate {sizeInBytes} bytes of CUDA memory", ex);
        }
    }

    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source, 
        MemoryOptions options = MemoryOptions.None, 
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        var sizeInBytes = source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken).ConfigureAwait(false);
        
        try
        {
            await buffer.CopyFromHostAsync(source, 0, cancellationToken).ConfigureAwait(false);
            return buffer;
        }
        catch
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        ThrowIfDisposed();
        
        ArgumentNullException.ThrowIfNull(buffer);
        
        if (!_buffers.TryGetValue(buffer, out var cudaBuffer))
            throw new ArgumentException("Buffer was not allocated by this memory manager", nameof(buffer));

        return cudaBuffer.CreateView(offset, length);
    }

    public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var sizeInBytes = count * elementSize;
        return AllocateAsync(sizeInBytes);
    }

    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        
        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var sizeInBytes = data.Length * elementSize;
        
        if (sizeInBytes > buffer.SizeInBytes)
        {
            throw new ArgumentException("Data size exceeds buffer capacity", nameof(data));
        }

        if (!_buffers.TryGetValue(buffer, out var cudaBuffer))
        {
            throw new ArgumentException("Buffer was not allocated by this memory manager", nameof(buffer));
        }

        try
        {
            _context.MakeCurrent();
            
            unsafe
            {
                fixed (T* dataPtr = data)
                {
                    var result = CudaRuntime.cudaMemcpyAsync(
                        cudaBuffer.DevicePointer,
                        new IntPtr(dataPtr),
                        (ulong)sizeInBytes,
                        CudaMemcpyKind.HostToDevice,
                        _context.Stream);
                    
                    CudaRuntime.CheckError(result, "Host to device copy");
                    _context.Synchronize();
                }
            }
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger.LogError(ex, "Failed to copy data to device");
            throw new MemoryException("Failed to copy data to device", ex);
        }
    }

    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        
        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var sizeInBytes = data.Length * elementSize;
        
        if (sizeInBytes > buffer.SizeInBytes)
        {
            throw new ArgumentException("Data size exceeds buffer capacity", nameof(data));
        }

        if (!_buffers.TryGetValue(buffer, out var cudaBuffer))
        {
            throw new ArgumentException("Buffer was not allocated by this memory manager", nameof(buffer));
        }

        try
        {
            _context.MakeCurrent();
            
            unsafe
            {
                fixed (T* dataPtr = data)
                {
                    var result = CudaRuntime.cudaMemcpyAsync(
                        new IntPtr(dataPtr),
                        cudaBuffer.DevicePointer,
                        (ulong)sizeInBytes,
                        CudaMemcpyKind.DeviceToHost,
                        _context.Stream);
                    
                    CudaRuntime.CheckError(result, "Device to host copy");
                    _context.Synchronize();
                }
            }
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger.LogError(ex, "Failed to copy data from device");
            throw new MemoryException("Failed to copy data from device", ex);
        }
    }

    public void Free(IMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        
        if (_buffers.TryRemove(buffer, out var cudaBuffer))
        {
            try
            {
                // Try to return to pool first before disposing
                var returnedToPool = false;
                if (cudaBuffer is CudaUnifiedMemoryBuffer unifiedBuffer && !unifiedBuffer.IsUnifiedMemory)
                {
                    // Only non-unified memory can be pooled effectively
                    var wrappedBuffer = new CudaMemoryBuffer(_context, unifiedBuffer.DevicePointer, 
                        unifiedBuffer.SizeInBytes, unifiedBuffer.Options, _logger);
                    returnedToPool = _memoryPool.TryReturnToPool(wrappedBuffer);
                }
                
                if (!returnedToPool)
                {
                    cudaBuffer.Dispose();
                }
                
                _statistics.RecordDeallocation(buffer.SizeInBytes);
                
                _logger.LogDebug("{Action} CUDA unified buffer of {Size}MB", 
                    returnedToPool ? "Returned to pool" : "Freed",
                    buffer.SizeInBytes / (1024 * 1024));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing CUDA buffer during free");
                throw;
            }
        }
    }

    private async ValueTask<CudaUnifiedMemoryBuffer> AllocateUnifiedMemoryAsync(
        long sizeInBytes, 
        MemoryOptions options, 
        CancellationToken cancellationToken)
    {
        // Try to get from pool first
        if (_memoryPool.TryGetFromPool(sizeInBytes, options, out var pooledInfo))
        {
            _logger.LogDebug("Retrieved {Size}MB buffer from pool", sizeInBytes / (1024 * 1024));
            return new CudaUnifiedMemoryBuffer(_context, pooledInfo.DevicePointer, 
                sizeInBytes, options, false, _logger);
        }

        return await Task.Run(() =>
        {
            _context.MakeCurrent();
            
            var devicePointer = IntPtr.Zero;
            var result = CudaRuntime.cudaMallocManaged(ref devicePointer, (ulong)sizeInBytes, 1);
            
            if (result != CudaError.Success)
            {
                throw new MemoryException(
                    $"Failed to allocate unified CUDA memory: {CudaRuntime.GetErrorString(result)}");
            }

            var buffer = new CudaUnifiedMemoryBuffer(
                _context, devicePointer, sizeInBytes, options, true, _logger);

            // Optimize unified memory placement
            OptimizeUnifiedMemoryPlacement(buffer);
            
            return buffer;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<CudaUnifiedMemoryBuffer> AllocateDeviceMemoryAsync(
        long sizeInBytes, 
        MemoryOptions options, 
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            _context.MakeCurrent();
            
            var devicePointer = IntPtr.Zero;
            var result = CudaRuntime.cudaMalloc(ref devicePointer, (ulong)sizeInBytes);
            
            if (result != CudaError.Success)
            {
                throw new MemoryException(
                    $"Failed to allocate CUDA device memory: {CudaRuntime.GetErrorString(result)}");
            }

            return new CudaUnifiedMemoryBuffer(
                _context, devicePointer, sizeInBytes, options, false, _logger);
        }, cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldUseUnifiedMemory(long sizeInBytes, MemoryOptions options)
    {
        // Use unified memory for:
        // 1. Small to medium allocations (< 512MB) for better host/device coordination
        // 2. HostVisible flag explicitly set
        // 3. When device memory is under pressure
        
        var memStats = _statistics.GetCurrentStatistics();
        var isMemoryPressure = memStats.UsedMemoryBytes > MemoryPressureThreshold;
        var isSmallAllocation = sizeInBytes < 512 * 1024 * 1024; // < 512MB
        var isHostVisible = options.HasFlag(MemoryOptions.HostVisible);
        
        return isHostVisible || (isSmallAllocation && !isMemoryPressure);
    }

    private void OptimizeUnifiedMemoryPlacement(CudaUnifiedMemoryBuffer buffer)
    {
        try
        {
            // Set preferred location to device for better performance
            var result = CudaRuntime.cudaMemAdvise(
                buffer.DevicePointer, 
                (ulong)buffer.SizeInBytes, 
                CudaMemoryAdvise.SetPreferredLocation, 
                _context.DeviceId);
            
            if (result == CudaError.Success)
            {
                // Mark as accessed by device for better migration hints
                CudaRuntime.cudaMemAdvise(
                    buffer.DevicePointer,
                    (ulong)buffer.SizeInBytes,
                    CudaMemoryAdvise.SetAccessedBy,
                    _context.DeviceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to optimize unified memory placement");
            // Continue execution - optimization failed but memory is still valid
        }
    }

    private void ConfigureUnifiedMemoryOptimizations()
    {
        try
        {
            _context.MakeCurrent();
            
            // Check device capabilities for unified memory
            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, _context.DeviceId);
            
            if (result == CudaError.Success)
            {
                var hasUnifiedAddressing = props.UnifiedAddressing == 1;
                var hasManagedMemory = props.ManagedMemory == 1;
                var hasConcurrentManagedAccess = props.ConcurrentManagedAccess == 1;
                
                _logger.LogInformation(
                    "CUDA Device {DeviceId} unified memory support: " +
                    "UnifiedAddressing={UnifiedAddressing}, ManagedMemory={ManagedMemory}, " +
                    "ConcurrentManagedAccess={ConcurrentManagedAccess}",
                    _context.DeviceId, hasUnifiedAddressing, hasManagedMemory, 
                    hasConcurrentManagedAccess);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure unified memory optimizations");
        }
    }

    private void CheckMemoryPressure(object? state)
    {
        if (_disposed) return;

        try
        {
            var stats = _statistics.GetCurrentStatistics();
            var memoryPressure = (double)stats.UsedMemoryBytes / stats.TotalMemoryBytes;
            
            if (memoryPressure > 0.90) // 90% threshold for aggressive cleanup
            {
                _logger.LogWarning("High memory pressure detected: {Pressure:P1}", memoryPressure);
                
                // Trigger pool cleanup
                _memoryPool.HandleMemoryPressure(memoryPressure);
                
                // Force garbage collection to release managed references
                GC.Collect(2, GCCollectionMode.Optimized, false);
            }
            else if (memoryPressure > 0.75) // 75% threshold for proactive cleanup
            {
                _logger.LogInformation("Moderate memory pressure detected: {Pressure:P1}", memoryPressure);
                _memoryPool.HandleMemoryPressure(memoryPressure);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory pressure check");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _memoryPressureTimer?.Dispose();
            
            // Dispose all tracked buffers
            foreach (var buffer in _buffers.Values)
            {
                try
                {
                    buffer.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing CUDA buffer during cleanup");
                }
            }
            
            _buffers.Clear();
            
            _memoryPool?.Dispose();
            _statistics?.Dispose();
            
            _logger.LogInformation("CUDA unified memory manager disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA unified memory manager disposal");
        }
        finally
        {
            _disposed = true;
        }
    }
}