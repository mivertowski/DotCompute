// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Diagnostics;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CUDA backend has dynamic logging requirements

namespace DotCompute.Backends.CUDA.Memory
{

/// <summary>
/// Advanced CUDA memory manager with pooling, pinned memory optimization, and async streams.
/// Optimized for RTX 2000 series (8GB VRAM) with comprehensive memory pressure management.
/// </summary>
public sealed class CudaMemoryManager : ISyncMemoryManager, IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<ISyncMemoryBuffer, CudaMemoryBuffer> _buffers = new();
    private readonly CudaMemoryPool _memoryPool;
    private readonly CudaMemoryStatistics _statistics;
    private readonly ReaderWriterLockSlim _bufferLock = new();
    private readonly SemaphoreSlim _allocationSemaphore;
    
    // Pinned memory management for optimal transfers
    private readonly ConcurrentDictionary<IntPtr, PinnedMemoryInfo> _pinnedMemoryCache = new();
    private readonly Timer _memoryPressureTimer;
    
    // Async stream management for concurrent operations
    private readonly IntPtr[] _asyncStreams;
    private volatile int _currentStreamIndex;
    private const int AsyncStreamCount = 4;
    
    // Memory alignment for optimal performance (RTX 2000 series optimizations)
    private const int OptimalAlignment = 256; // 256-byte alignment for coalesced access
    private const long MinPinnedSize = 64 * 1024; // 64KB minimum for pinned memory benefits
    private const long MaxPinnedCacheSize = 512 * 1024 * 1024; // 512MB cache limit
    
    // Memory pressure thresholds
    private const double MemoryPressureThreshold = 0.85; // 85%
    private const double CriticalMemoryThreshold = 0.95; // 95%
    
    private bool _disposed;
    
    public CudaMemoryManager(CudaContext context, ILogger logger, int maxConcurrentAllocations = 32)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _allocationSemaphore = new SemaphoreSlim(maxConcurrentAllocations, maxConcurrentAllocations);
        
        _memoryPool = new CudaMemoryPool(_context, _logger);
        _statistics = new CudaMemoryStatistics(_logger);
        
        // Initialize async streams for concurrent memory operations
        _asyncStreams = new IntPtr[AsyncStreamCount];
        InitializeAsyncStreams();
        
        // Set up memory pressure monitoring (every 2 seconds)
        _memoryPressureTimer = new Timer(CheckMemoryPressure, null, 
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        
        _logger.LogInformation("Initialized advanced CUDA memory manager for device {DeviceId}", _context.DeviceId);
    }

    public ISyncMemoryBuffer Allocate(long sizeInBytes, MemoryOptions options = MemoryOptions.None)
    {
        ThrowIfDisposed();

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(sizeInBytes));
        }

        return AllocateAsync(sizeInBytes, options, CancellationToken.None).GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Asynchronously allocates memory with advanced pooling and pressure management
    /// </summary>
    public async Task<ISyncMemoryBuffer> AllocateAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(sizeInBytes));
        }

        // Use semaphore to limit concurrent allocations and prevent memory fragmentation
        await _allocationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogDebug("Allocating {Size}MB of CUDA memory with options {Options}", 
                sizeInBytes / (1024 * 1024), options);

            // Check memory pressure before allocation
            await HandleMemoryPressureAsync(sizeInBytes).ConfigureAwait(false);

            CudaMemoryBuffer buffer;
            
            // Try to get from pool first
            if (_memoryPool.TryGetFromPool(sizeInBytes, options, out var pooledBuffer))
            {
                buffer = new CudaMemoryBuffer(_context, pooledBuffer.DevicePointer, sizeInBytes, options, _logger);
                _logger.LogDebug("Retrieved {Size}MB buffer from memory pool", sizeInBytes / (1024 * 1024));
            }
            else
            {
                // Allocate new buffer with optimal alignment
                var alignedSize = AlignSize(sizeInBytes, OptimalAlignment);
                buffer = await AllocateNewBufferAsync(alignedSize, options, cancellationToken).ConfigureAwait(false);
            }

            // Track the buffer
            _bufferLock.EnterWriteLock();
            try
            {
                if (!_buffers.TryAdd(buffer, buffer))
                {
                    buffer.Dispose();
                    throw new MemoryException("Failed to track allocated buffer");
                }
            }
            finally
            {
                _bufferLock.ExitWriteLock();
            }

            // Update statistics
            _statistics.RecordAllocation(sizeInBytes);
            _statistics.RecordOperationTime("allocation", stopwatch.Elapsed);

            _logger.LogDebug("Successfully allocated CUDA buffer of {Size}MB in {Time}ms", 
                sizeInBytes / (1024 * 1024), stopwatch.ElapsedMilliseconds);
                
            return buffer;
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger.LogError(ex, "Failed to allocate CUDA memory");
            throw new MemoryException($"Failed to allocate {sizeInBytes} bytes of CUDA memory", ex);
        }
        finally
        {
            _allocationSemaphore.Release();
        }
    }

    public ISyncMemoryBuffer AllocateAligned(long sizeInBytes, int alignment, MemoryOptions options = MemoryOptions.None)
    {
        ThrowIfDisposed();

        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
        {
            throw new ArgumentException("Alignment must be a power of 2", nameof(alignment));
        }

        // Ensure minimum optimal alignment for RTX 2000 series
        var effectiveAlignment = Math.Max(alignment, OptimalAlignment);
        var alignedSize = AlignSize(sizeInBytes, effectiveAlignment);

        _logger.LogDebug("Allocating aligned memory: {Size}MB aligned to {Alignment} bytes", 
            alignedSize / (1024 * 1024), effectiveAlignment);
            
        return Allocate(alignedSize, options);
    }

    public void Copy(ISyncMemoryBuffer source, ISyncMemoryBuffer destination, long sizeInBytes, long sourceOffset = 0, long destinationOffset = 0)
    {
        ThrowIfDisposed();
        ValidateCopyParameters(source, destination, sizeInBytes, sourceOffset, destinationOffset);

        try
        {
            var srcBuffer = GetCudaBuffer(source);
            var dstBuffer = GetCudaBuffer(destination);

            var srcPtr = new IntPtr(srcBuffer.DevicePointer.ToInt64() + sourceOffset);
            var dstPtr = new IntPtr(dstBuffer.DevicePointer.ToInt64() + destinationOffset);

            _logger.LogDebug("Copying {Size} bytes from GPU buffer to GPU buffer", sizeInBytes);

            var result = CudaRuntime.cudaMemcpyAsync(
                dstPtr,
                srcPtr,
                (ulong)sizeInBytes,
                CudaMemcpyKind.DeviceToDevice,
                _context.Stream);

            CudaRuntime.CheckError(result, "GPU to GPU copy");
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger.LogError(ex, "Failed to copy memory between GPU buffers");
            throw new MemoryException("Failed to copy memory between GPU buffers", ex);
        }
    }

    public unsafe void CopyFromHost(void* source, ISyncMemoryBuffer destination, long sizeInBytes, long destinationOffset = 0)
    {
        ThrowIfDisposed();

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ValidateBuffer(destination, sizeInBytes, destinationOffset);
        
        CopyFromHostAsync(new IntPtr(source), destination, sizeInBytes, destinationOffset, CancellationToken.None)
            .GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Optimized host-to-device copy with pinned memory and async streams
    /// </summary>
    public async Task CopyFromHostAsync(IntPtr source, ISyncMemoryBuffer destination, long sizeInBytes, 
        long destinationOffset = 0, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateBuffer(destination, sizeInBytes, destinationOffset);

        var stopwatch = Stopwatch.StartNew();
        var srcPtr = source;
        
        try
        {
            var dstBuffer = GetCudaBuffer(destination);
            var dstPtr = new IntPtr(dstBuffer.DevicePointer.ToInt64() + destinationOffset);

            _logger.LogDebug("Copying {Size}MB from host to GPU", sizeInBytes / (1024 * 1024));

            // Use pinned memory for large transfers to improve bandwidth
            if (sizeInBytes >= MinPinnedSize)
            {
                await CopyFromHostPinnedAsync(srcPtr, dstPtr, sizeInBytes, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await CopyFromHostDirectAsync(srcPtr, dstPtr, sizeInBytes, cancellationToken).ConfigureAwait(false);
            }

            _statistics.RecordHostToDeviceTransfer(sizeInBytes);
            _statistics.RecordOperationTime("h2d_copy", stopwatch.Elapsed);
            
            _logger.LogDebug("Host to GPU copy completed: {Size}MB in {Time}ms ({Bandwidth:F1} MB/s)", 
                sizeInBytes / (1024 * 1024), stopwatch.ElapsedMilliseconds,
                (sizeInBytes / (1024.0 * 1024.0)) / stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger.LogError(ex, "Failed to copy memory from host to GPU");
            throw new MemoryException("Failed to copy memory from host to GPU", ex);
        }
    }

    public unsafe void CopyToHost(ISyncMemoryBuffer source, void* destination, long sizeInBytes, long sourceOffset = 0)
    {
        ThrowIfDisposed();

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        ValidateBuffer(source, sizeInBytes, sourceOffset);
        
        CopyToHostAsync(source, new IntPtr(destination), sizeInBytes, sourceOffset, CancellationToken.None)
            .GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Optimized device-to-host copy with pinned memory and async streams
    /// </summary>
    public async Task CopyToHostAsync(ISyncMemoryBuffer source, IntPtr destination, long sizeInBytes, 
        long sourceOffset = 0, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateBuffer(source, sizeInBytes, sourceOffset);

        var stopwatch = Stopwatch.StartNew();
        var dstPtr = destination;
        
        try
        {
            var srcBuffer = GetCudaBuffer(source);
            var srcPtr = new IntPtr(srcBuffer.DevicePointer.ToInt64() + sourceOffset);

            _logger.LogDebug("Copying {Size}MB from GPU to host", sizeInBytes / (1024 * 1024));

            // Use pinned memory for large transfers to improve bandwidth
            if (sizeInBytes >= MinPinnedSize)
            {
                await CopyToHostPinnedAsync(srcPtr, dstPtr, sizeInBytes, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await CopyToHostDirectAsync(srcPtr, dstPtr, sizeInBytes, cancellationToken).ConfigureAwait(false);
            }

            _statistics.RecordDeviceToHostTransfer(sizeInBytes);
            _statistics.RecordOperationTime("d2h_copy", stopwatch.Elapsed);
            
            _logger.LogDebug("GPU to host copy completed: {Size}MB in {Time}ms ({Bandwidth:F1} MB/s)", 
                sizeInBytes / (1024 * 1024), stopwatch.ElapsedMilliseconds,
                (sizeInBytes / (1024.0 * 1024.0)) / stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger.LogError(ex, "Failed to copy memory from GPU to host");
            throw new MemoryException("Failed to copy memory from GPU to host", ex);
        }
    }

    public void Fill(ISyncMemoryBuffer buffer, byte value, long sizeInBytes, long offset = 0)
    {
        ThrowIfDisposed();
        ValidateBuffer(buffer, sizeInBytes, offset);

        try
        {
            var cudaBuffer = GetCudaBuffer(buffer);
            var ptr = new IntPtr(cudaBuffer.DevicePointer.ToInt64() + offset);

            _logger.LogDebug("Filling {Size} bytes with value {Value}", sizeInBytes, value);

            var result = CudaRuntime.cudaMemsetAsync(ptr, value, (ulong)sizeInBytes, _context.Stream);
            CudaRuntime.CheckError(result, "Memory fill");
        }
        catch (Exception ex) when (ex is not MemoryException)
        {
            _logger.LogError(ex, "Failed to fill GPU memory");
            throw new MemoryException("Failed to fill GPU memory", ex);
        }
    }

    public void Zero(ISyncMemoryBuffer buffer)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(buffer);

        Fill(buffer, 0, buffer.SizeInBytes);
    }

    public void Free(ISyncMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        _bufferLock.EnterWriteLock();
        try
        {
            if (_buffers.TryRemove(buffer, out var cudaBuffer))
            {
                // Try to return to pool first
                if (!_memoryPool.TryReturnToPool(cudaBuffer))
                {
                    cudaBuffer.Dispose();
                }
                
                _statistics.RecordDeallocation(buffer.SizeInBytes);
                _logger.LogDebug("Freed CUDA buffer of {Size}MB", buffer.SizeInBytes / (1024 * 1024));
            }
        }
        finally
        {
            _bufferLock.ExitWriteLock();
        }
    }

    public MemoryStatistics GetStatistics()
    {
        ThrowIfDisposed();

        try
        {
            var stats = _statistics.GetCurrentStatistics();
            var poolStats = _memoryPool.GetStatistics();

            _bufferLock.EnterReadLock();
            long allocated = 0;
            var allocationCount = 0;
            
            try
            {
                foreach (var buffer in _buffers.Values)
                {
                    allocated += buffer.SizeInBytes;
                    allocationCount++;
                }
            }
            finally
            {
                _bufferLock.ExitReadLock();
            }

            return new MemoryStatistics
            {
                TotalMemory = stats.TotalMemoryBytes,
                UsedMemory = stats.UsedMemoryBytes,
                FreeMemory = stats.FreeMemoryBytes,
                AllocatedMemory = allocated,
                AllocationCount = allocationCount,
                PeakMemory = stats.PeakMemoryBytes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory statistics");
            throw new MemoryException("Failed to get CUDA memory statistics", ex);
        }
    }
    
    /// <summary>
    /// Gets comprehensive CUDA memory statistics including performance metrics
    /// </summary>
    public CudaMemoryStatisticsSnapshot GetDetailedStatistics()
    {
        ThrowIfDisposed();
        return _statistics.GetCurrentStatistics();
    }

    public void Reset()
    {
        ThrowIfDisposed();

        _logger.LogInformation("Resetting CUDA memory manager");

        _bufferLock.EnterWriteLock();
        try
        {
            foreach (var buffer in _buffers.Values)
            {
                try
                {
                    buffer.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing buffer during reset");
                }
            }

            _buffers.Clear();
        }
        finally
        {
            _bufferLock.ExitWriteLock();
        }
        
        // Clear pinned memory cache
        ClearPinnedMemoryCache();
        
        // Reset statistics
        _statistics.Reset();
    }

    private CudaMemoryBuffer GetCudaBuffer(ISyncMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (_buffers.TryGetValue(buffer, out var cudaBuffer))
        {
            return cudaBuffer;
        }

        throw new ArgumentException("Buffer was not allocated by this memory manager", nameof(buffer));
    }

    private static void ValidateBuffer(ISyncMemoryBuffer buffer, long sizeInBytes, long offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be greater than zero", nameof(sizeInBytes));
        }

        if (offset < 0)
        {
            throw new ArgumentException("Offset cannot be negative", nameof(offset));
        }

        if (offset + sizeInBytes > buffer.SizeInBytes)
        {
            throw new ArgumentException("Operation would exceed buffer bounds");
        }
    }

    private static void ValidateCopyParameters(ISyncMemoryBuffer source, ISyncMemoryBuffer destination,
        long sizeInBytes, long sourceOffset, long destinationOffset)
    {
        ValidateBuffer(source, sizeInBytes, sourceOffset);
        ValidateBuffer(destination, sizeInBytes, destinationOffset);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    
    #region Advanced Memory Management Implementation
    
    private void InitializeAsyncStreams()
    {
        try
        {
            _context.MakeCurrent();
            
            for (int i = 0; i < AsyncStreamCount; i++)
            {
                var result = CudaRuntime.cudaStreamCreateWithFlags(ref _asyncStreams[i], 1); // Non-blocking stream
                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to create async stream {Index}: {Error}", i, CudaRuntime.GetErrorString(result));
                    _asyncStreams[i] = _context.Stream; // Fallback to default stream
                }
            }
            
            _logger.LogDebug("Initialized {Count} async streams for memory operations", AsyncStreamCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize async streams");
            // Fill with default stream as fallback
            for (int i = 0; i < AsyncStreamCount; i++)
            {
                _asyncStreams[i] = _context.Stream;
            }
        }
    }
    
    private void DisposeAsyncStreams()
    {
        try
        {
            _context.MakeCurrent();
            
            for (int i = 0; i < AsyncStreamCount; i++)
            {
                if (_asyncStreams[i] != IntPtr.Zero && _asyncStreams[i] != _context.Stream)
                {
                    var result = CudaRuntime.cudaStreamDestroy(_asyncStreams[i]);
                    if (result != CudaError.Success)
                    {
                        _logger.LogWarning("Failed to destroy async stream {Index}: {Error}", i, CudaRuntime.GetErrorString(result));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing async streams");
        }
    }
    
    private IntPtr GetNextAsyncStream()
    {
        var index = Interlocked.Increment(ref _currentStreamIndex) % AsyncStreamCount;
        return _asyncStreams[index];
    }
    
    private async Task<CudaMemoryBuffer> AllocateNewBufferAsync(long sizeInBytes, MemoryOptions options, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            _context.MakeCurrent();
            return new CudaMemoryBuffer(_context, sizeInBytes, options, _logger);
        }, cancellationToken).ConfigureAwait(false);
    }

        private static long AlignSize(long size, int alignment) => ((size + alignment - 1) / alignment) * alignment;

        private async Task HandleMemoryPressureAsync(long requestedSize)
    {
        var stats = _statistics.GetCurrentStatistics();
        var projectedUsage = (double)(stats.UsedMemoryBytes + requestedSize) / stats.TotalMemoryBytes;
        
        if (projectedUsage > MemoryPressureThreshold)
        {
            _logger.LogWarning("Memory pressure detected: {Usage:P1}, attempting cleanup", projectedUsage);
            
            // Trigger pool cleanup
            _memoryPool.HandleMemoryPressure(projectedUsage);
            
            // Clear old pinned memory
            ClearPinnedMemoryCache();
            
            // Force garbage collection if critical
            if (projectedUsage > CriticalMemoryThreshold)
            {
                _logger.LogWarning("Critical memory pressure: {Usage:P1}, forcing GC", projectedUsage);
                
                await Task.Run(() =>
                {
                    GC.Collect(2, GCCollectionMode.Aggressive, true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(2, GCCollectionMode.Aggressive, true);
                }).ConfigureAwait(false);
            }
        }
    }
    
    private void CheckMemoryPressure(object? state)
    {
        if (_disposed)
            {
                return;
            }

            try
        {
            var stats = _statistics.GetCurrentStatistics();
            var pressure = stats.MemoryPressure;
            
            if (pressure > MemoryPressureThreshold)
            {
                _logger.LogInformation("Memory pressure check: {Pressure:P1}", pressure);
                _memoryPool.HandleMemoryPressure(pressure);
                
                if (pressure > CriticalMemoryThreshold)
                {
                    ClearPinnedMemoryCache();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory pressure check");
        }
    }
    
    private async Task CopyFromHostPinnedAsync(IntPtr srcPtr, IntPtr dstPtr, long sizeInBytes, CancellationToken cancellationToken)
    {
        // Try to use cached pinned memory or allocate new
        IntPtr pinnedPtr = await GetOrAllocatePinnedMemoryAsync(sizeInBytes, cancellationToken).ConfigureAwait(false);
        
        await Task.Run(() =>
        {
            _context.MakeCurrent();
            
            // Copy to pinned memory first
            unsafe
            {
                Buffer.MemoryCopy(srcPtr.ToPointer(), pinnedPtr.ToPointer(), sizeInBytes, sizeInBytes);
            }
            
            // Then async copy to device using optimal stream
            var stream = GetNextAsyncStream();
            var result = CudaRuntime.cudaMemcpyAsync(dstPtr, pinnedPtr, (ulong)sizeInBytes, 
                CudaMemcpyKind.HostToDevice, stream);
            
            CudaRuntime.CheckError(result, "Pinned host to device copy");
            
            // Synchronize this stream
            CudaRuntime.cudaStreamSynchronize(stream);
            
        }, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task CopyFromHostDirectAsync(IntPtr srcPtr, IntPtr dstPtr, long sizeInBytes, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            _context.MakeCurrent();
            
            var stream = GetNextAsyncStream();
            var result = CudaRuntime.cudaMemcpyAsync(dstPtr, srcPtr, (ulong)sizeInBytes, 
                CudaMemcpyKind.HostToDevice, stream);
            
            CudaRuntime.CheckError(result, "Direct host to device copy");
            
            CudaRuntime.cudaStreamSynchronize(stream);
            
        }, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task CopyToHostPinnedAsync(IntPtr srcPtr, IntPtr dstPtr, long sizeInBytes, CancellationToken cancellationToken)
    {
        IntPtr pinnedPtr = await GetOrAllocatePinnedMemoryAsync(sizeInBytes, cancellationToken).ConfigureAwait(false);
        
        await Task.Run(() =>
        {
            _context.MakeCurrent();
            
            // Async copy from device to pinned memory
            var stream = GetNextAsyncStream();
            var result = CudaRuntime.cudaMemcpyAsync(pinnedPtr, srcPtr, (ulong)sizeInBytes, 
                CudaMemcpyKind.DeviceToHost, stream);
            
            CudaRuntime.CheckError(result, "Device to pinned memory copy");
            
            // Synchronize to ensure data is available
            CudaRuntime.cudaStreamSynchronize(stream);
            
            // Copy from pinned to destination
            unsafe
            {
                Buffer.MemoryCopy(pinnedPtr.ToPointer(), dstPtr.ToPointer(), sizeInBytes, sizeInBytes);
            }
        }, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task CopyToHostDirectAsync(IntPtr srcPtr, IntPtr dstPtr, long sizeInBytes, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            _context.MakeCurrent();
            
            var stream = GetNextAsyncStream();
            var result = CudaRuntime.cudaMemcpyAsync(dstPtr, srcPtr, (ulong)sizeInBytes, 
                CudaMemcpyKind.DeviceToHost, stream);
            
            CudaRuntime.CheckError(result, "Direct device to host copy");
            
            // Must synchronize for device-to-host to ensure data is available
            CudaRuntime.cudaStreamSynchronize(stream);
            
        }, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task<IntPtr> GetOrAllocatePinnedMemoryAsync(long sizeInBytes, CancellationToken cancellationToken)
    {
        // Try to find a suitable cached pinned memory block
        foreach (var kvp in _pinnedMemoryCache)
        {
            var info = kvp.Value;
            if (info.Size >= sizeInBytes && !info.InUse && 
                DateTime.UtcNow - info.LastUsed < TimeSpan.FromMinutes(5))
            {
                info.InUse = true;
                info.LastUsed = DateTime.UtcNow;
                return kvp.Key;
            }
        }
        
        // Allocate new pinned memory
        return await Task.Run(() =>
        {
            _context.MakeCurrent();
            
            var alignedSize = AlignSize(sizeInBytes, OptimalAlignment);
            var pinnedPtr = IntPtr.Zero;
            
            var result = CudaRuntime.cudaMallocHost(ref pinnedPtr, (ulong)alignedSize);
            CudaRuntime.CheckError(result, "Pinned memory allocation");
            
            var info = new PinnedMemoryInfo
            {
                Size = alignedSize,
                InUse = true,
                LastUsed = DateTime.UtcNow
            };
            
            _pinnedMemoryCache[pinnedPtr] = info;
            
            _logger.LogDebug("Allocated {Size}MB of pinned memory", alignedSize / (1024 * 1024));
            
            return pinnedPtr;
            
        }, cancellationToken).ConfigureAwait(false);
    }
    
    private void ClearPinnedMemoryCache()
    {
        var cutoffTime = DateTime.UtcNow - TimeSpan.FromMinutes(2);
        var toRemove = new List<IntPtr>();
        
        foreach (var kvp in _pinnedMemoryCache)
        {
            var info = kvp.Value;
            if (!info.InUse && info.LastUsed < cutoffTime)
            {
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (var ptr in toRemove)
        {
            if (_pinnedMemoryCache.TryRemove(ptr, out var info))
            {
                try
                {
                    CudaRuntime.cudaFreeHost(ptr);
                    _logger.LogDebug("Freed {Size}MB of cached pinned memory", info.Size / (1024 * 1024));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error freeing pinned memory");
                }
            }
        }
        
        if (toRemove.Count > 0)
        {
            _logger.LogDebug("Cleared {Count} pinned memory blocks from cache", toRemove.Count);
        }
    }
    
    #endregion
    
    #region Helper Classes
    
    private class PinnedMemoryInfo
    {
        public long Size { get; set; }
        public bool InUse { get; set; }
        public DateTime LastUsed { get; set; }
    }
    
    #endregion

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                _memoryPressureTimer?.Dispose();
                
                Reset();
                
                // Dispose async streams
                DisposeAsyncStreams();
                
                // Dispose resources
                _memoryPool?.Dispose();
                _statistics?.Dispose();
                _bufferLock?.Dispose();
                _allocationSemaphore?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during CUDA memory manager disposal");
            }
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
}
