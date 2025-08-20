// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CUDA backend has dynamic logging requirements

namespace DotCompute.Backends.CUDA.Memory
{

/// <summary>
/// Memory pool for CUDA unified memory management with size-based bucket allocation.
/// Optimized for 8GB VRAM with fragmentation prevention and automatic cleanup.
/// </summary>
public sealed class CudaMemoryPool : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly Dictionary<int, ConcurrentQueue<PooledBuffer>> _pools;
    private readonly ReaderWriterLockSlim _poolLock;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    // Pool configuration optimized for 8GB VRAM
    private static readonly int[] BucketSizes = {
        1 * 1024,       // 1KB
        4 * 1024,       // 4KB
        16 * 1024,      // 16KB
        64 * 1024,      // 64KB
        256 * 1024,     // 256KB
        1 * 1024 * 1024,   // 1MB
        4 * 1024 * 1024,   // 4MB
        16 * 1024 * 1024,  // 16MB
        64 * 1024 * 1024,  // 64MB
        256 * 1024 * 1024, // 256MB
        512 * 1024 * 1024, // 512MB
    };

    private const int MaxBuffersPerBucket = 32;
    private const int CleanupIntervalSeconds = 30;
    private const double MaxPoolMemoryRatio = 0.3; // 30% of total memory for pooling
    
    // Statistics
    private long _totalAllocations;
    private long _poolHits;
    private long _poolMisses;
    private long _totalPooledMemory;
    private long _peakPooledMemory;

    public CudaMemoryPool(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _pools = new Dictionary<int, ConcurrentQueue<PooledBuffer>>();
        _poolLock = new ReaderWriterLockSlim();

        // Initialize pools for each bucket size
        foreach (var size in BucketSizes)
        {
            _pools[size] = new ConcurrentQueue<PooledBuffer>();
        }

        // Start cleanup timer
        _cleanupTimer = new Timer(PerformCleanup, null,
            TimeSpan.FromSeconds(CleanupIntervalSeconds),
            TimeSpan.FromSeconds(CleanupIntervalSeconds));

        _logger.LogInformation("Initialized CUDA memory pool with {BucketCount} size buckets", BucketSizes.Length);
    }

    /// <summary>
    /// Attempts to get a buffer from the pool
    /// </summary>
    public bool TryGetFromPool(long sizeInBytes, MemoryOptions options, out PooledBufferInfo buffer)
    {
        buffer = default;
        
        if (_disposed)
            {
                return false;
            }

            Interlocked.Increment(ref _totalAllocations);
        
        var bucketSize = GetBucketSize(sizeInBytes);
        if (bucketSize == -1)
        {
            Interlocked.Increment(ref _poolMisses);
            return false; // Size too large for pooling
        }

        _poolLock.EnterReadLock();
        try
        {
            if (_pools.TryGetValue(bucketSize, out var queue) && queue.TryDequeue(out var pooledBuffer))
            {
                // Check if buffer is still valid and matches requirements
                if (IsBufferValid(pooledBuffer, sizeInBytes, options))
                {
                    buffer = new PooledBufferInfo
                    {
                        DevicePointer = pooledBuffer.DevicePointer,
                        Size = pooledBuffer.Size,
                        Options = pooledBuffer.Options
                    };
                    
                    Interlocked.Increment(ref _poolHits);
                    Interlocked.Add(ref _totalPooledMemory, -bucketSize);
                    
                    _logger.LogDebug("Retrieved {Size}KB buffer from pool (bucket: {BucketSize}KB)",
                        sizeInBytes / 1024, bucketSize / 1024);
                    
                    return true;
                }
                else
                {
                    // Buffer is invalid, dispose it
                    DisposePooledBuffer(pooledBuffer);
                }
            }
        }
        finally
        {
            _poolLock.ExitReadLock();
        }

        Interlocked.Increment(ref _poolMisses);
        return false;
    }

    /// <summary>
    /// Returns a buffer to the pool for reuse
    /// </summary>
    public bool TryReturnToPool(CudaMemoryBuffer buffer)
    {
        if (_disposed || buffer.IsDisposed)
            {
                return false;
            }

            var sizeInBytes = buffer.SizeInBytes;
        var bucketSize = GetBucketSize(sizeInBytes);
        
        if (bucketSize == -1)
            {
                return false; // Size too large for pooling
            }

            _poolLock.EnterReadLock();
        try
        {
            if (_pools.TryGetValue(bucketSize, out var queue))
            {
                // Check pool capacity
                if (queue.Count >= MaxBuffersPerBucket)
                {
                    _logger.LogDebug("Pool bucket {BucketSize}KB is full, disposing buffer", bucketSize / 1024);
                    return false;
                }

                // Check total pool memory limit
                var maxPoolMemory = GetMaxPoolMemory();
                if (_totalPooledMemory + sizeInBytes > maxPoolMemory)
                {
                    _logger.LogDebug("Pool memory limit reached, disposing buffer");
                    return false;
                }

                var pooledBuffer = new PooledBuffer
                {
                    DevicePointer = buffer.DevicePointer,
                    Size = sizeInBytes,
                    Options = buffer.Options,
                    CreatedTime = DateTime.UtcNow
                };
                
                queue.Enqueue(pooledBuffer);
                
                Interlocked.Add(ref _totalPooledMemory, sizeInBytes);
                _peakPooledMemory = Math.Max(_peakPooledMemory, _totalPooledMemory);
                
                _logger.LogDebug("Returned {Size}KB buffer to pool (bucket: {BucketSize}KB)",
                    sizeInBytes / 1024, bucketSize / 1024);
                
                return true;
            }
        }
        finally
        {
            _poolLock.ExitReadLock();
        }

        return false;
    }

    /// <summary>
    /// Handles memory pressure by releasing buffers from pools
    /// </summary>
    public void HandleMemoryPressure(double pressure)
    {
        if (_disposed)
            {
                return;
            }

            var buffersToRelease = pressure switch
        {
            > 0.95 => 0.8, // Release 80% of pooled buffers
            > 0.90 => 0.6, // Release 60% of pooled buffers
            > 0.80 => 0.4, // Release 40% of pooled buffers
            > 0.75 => 0.2, // Release 20% of pooled buffers
            _ => 0.0
        };

        if (buffersToRelease > 0)
        {
            _logger.LogInformation("Handling memory pressure {Pressure:P1}, releasing {Ratio:P0} of pooled buffers",
                pressure, buffersToRelease);
            
            ReleasePools(buffersToRelease);
        }
    }

    /// <summary>
    /// Gets memory pool statistics
    /// </summary>
    public CudaMemoryPoolStatistics GetStatistics()
    {
        _poolLock.EnterReadLock();
        try
        {
            var bucketStats = new Dictionary<int, int>();
            var totalBuffersPooled = 0;

            foreach (var kvp in _pools)
            {
                var count = kvp.Value.Count;
                bucketStats[kvp.Key] = count;
                totalBuffersPooled += count;
            }

            var hitRate = _totalAllocations > 0 ? (double)_poolHits / _totalAllocations : 0.0;

            return new CudaMemoryPoolStatistics
            {
                TotalAllocations = _totalAllocations,
                PoolHits = _poolHits,
                PoolMisses = _poolMisses,
                HitRate = hitRate,
                TotalBuffersPooled = totalBuffersPooled,
                TotalPooledMemoryBytes = _totalPooledMemory,
                PeakPooledMemoryBytes = _peakPooledMemory,
                BucketStatistics = bucketStats
            };
        }
        finally
        {
            _poolLock.ExitReadLock();
        }
    }

    private int GetBucketSize(long requestedSize)
    {
        // Find the smallest bucket that can accommodate the requested size
        foreach (var bucketSize in BucketSizes)
        {
            if (requestedSize <= bucketSize)
                {
                    return bucketSize;
                }
            }
        
        return -1; // Too large for pooling
    }

    private bool IsBufferValid(PooledBuffer pooledBuffer, long requestedSize, MemoryOptions options)
    {
        // Check if device pointer is still valid
        if (pooledBuffer.DevicePointer == IntPtr.Zero)
            {
                return false;
            }

            // Check size compatibility
            if (pooledBuffer.Size < requestedSize)
            {
                return false;
            }

            // Check options compatibility
            if (pooledBuffer.Options != options)
            {
                return false;
            }

            // Check age - don't reuse very old buffers
            var age = DateTime.UtcNow - pooledBuffer.CreatedTime;
        if (age > TimeSpan.FromMinutes(10))
            {
                return false;
            }

            return true;
    }
    
    private void DisposePooledBuffer(PooledBuffer pooledBuffer)
    {
        if (pooledBuffer.DevicePointer != IntPtr.Zero)
        {
            try
            {
                var result = CudaRuntime.cudaFree(pooledBuffer.DevicePointer);
                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to free pooled device memory: {Error}", 
                        CudaRuntime.GetErrorString(result));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error freeing pooled device memory");
            }
        }
    }

    private long GetMaxPoolMemory()
    {
        try
        {
            CudaRuntime.cudaMemGetInfo(out var free, out var total);
            return (long)(total * MaxPoolMemoryRatio);
        }
        catch
        {
            // Fallback to conservative estimate
            return 2L * 1024 * 1024 * 1024; // 2GB
        }
    }

    private void ReleasePools(double ratio)
    {
        _poolLock.EnterWriteLock();
        try
        {
            var releasedBuffers = 0;
            var releasedMemory = 0L;

            foreach (var kvp in _pools)
            {
                var queue = kvp.Value;
                var bucketSize = kvp.Key;
                var buffersToRelease = (int)(queue.Count * ratio);

                for (int i = 0; i < buffersToRelease && queue.TryDequeue(out var pooledBuffer); i++)
                {
                    try
                    {
                        DisposePooledBuffer(pooledBuffer);
                        releasedBuffers++;
                        releasedMemory += bucketSize;
                        Interlocked.Add(ref _totalPooledMemory, -bucketSize);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing pooled buffer during memory pressure cleanup");
                    }
                }
            }

            _logger.LogInformation("Released {BufferCount} pooled buffers, freed {MemoryMB}MB",
                releasedBuffers, releasedMemory / (1024 * 1024));
        }
        finally
        {
            _poolLock.ExitWriteLock();
        }
    }

    private void PerformCleanup(object? state)
    {
        if (_disposed)
            {
                return;
            }

            try
        {
            var cutoffTime = DateTime.UtcNow - TimeSpan.FromMinutes(5);
            var cleanedBuffers = 0;
            var cleanedMemory = 0L;

            _poolLock.EnterWriteLock();
            try
            {
                foreach (var kvp in _pools)
                {
                    var queue = kvp.Value;
                    var bucketSize = kvp.Key;
                    var tempList = new List<PooledBuffer>();

                    // Drain queue and separate old from recent buffers
                    while (queue.TryDequeue(out var pooledBuffer))
                    {
                        if (pooledBuffer.CreatedTime < cutoffTime || pooledBuffer.DevicePointer == IntPtr.Zero)
                        {
                            try
                            {
                                DisposePooledBuffer(pooledBuffer);
                                cleanedBuffers++;
                                cleanedMemory += bucketSize;
                                Interlocked.Add(ref _totalPooledMemory, -bucketSize);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error disposing old pooled buffer during cleanup");
                            }
                        }
                        else
                        {
                            tempList.Add(pooledBuffer);
                        }
                    }

                    // Re-enqueue valid buffers
                    foreach (var buffer in tempList)
                    {
                        queue.Enqueue(buffer);
                    }
                }
            }
            finally
            {
                _poolLock.ExitWriteLock();
            }

            if (cleanedBuffers > 0)
            {
                _logger.LogDebug("Cleaned up {BufferCount} old pooled buffers, freed {MemoryMB}MB",
                    cleanedBuffers, cleanedMemory / (1024 * 1024));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory pool cleanup");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            {
                return;
            }

            try
        {
            _cleanupTimer?.Dispose();

            _poolLock.EnterWriteLock();
            try
            {
                var totalBuffersDisposed = 0;
                var totalMemoryDisposed = 0L;

                foreach (var kvp in _pools)
                {
                    var queue = kvp.Value;
                    var bucketSize = kvp.Key;

                    while (queue.TryDequeue(out var pooledBuffer))
                    {
                        try
                        {
                            DisposePooledBuffer(pooledBuffer);
                            totalBuffersDisposed++;
                            totalMemoryDisposed += bucketSize;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error disposing pooled buffer during pool disposal");
                        }
                    }
                }

                _pools.Clear();

                var stats = GetStatistics();
                _logger.LogInformation(
                    "CUDA memory pool disposed: {BuffersDisposed} buffers, {MemoryMB}MB freed. " +
                    "Final stats: {TotalAllocations} allocations, {HitRate:P1} hit rate",
                    totalBuffersDisposed, totalMemoryDisposed / (1024 * 1024),
                    stats.TotalAllocations, stats.HitRate);
            }
            finally
            {
                _poolLock.ExitWriteLock();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA memory pool disposal");
        }
        finally
        {
            _poolLock?.Dispose();
            _disposed = true;
        }
    }

    private struct PooledBuffer
    {
        public IntPtr DevicePointer { get; set; }
        public long Size { get; set; }
        public MemoryOptions Options { get; set; }
        public DateTime CreatedTime { get; set; }
    }
    
    /// <summary>
    /// Information about a buffer retrieved from the pool
    /// </summary>
    public struct PooledBufferInfo
    {
        public IntPtr DevicePointer { get; set; }
        public long Size { get; set; }
        public MemoryOptions Options { get; set; }
    }
}

/// <summary>
/// Statistics for CUDA memory pool performance tracking
/// </summary>
public readonly struct CudaMemoryPoolStatistics
{
    public long TotalAllocations { get; init; }
    public long PoolHits { get; init; }
    public long PoolMisses { get; init; }
    public double HitRate { get; init; }
    public int TotalBuffersPooled { get; init; }
    public long TotalPooledMemoryBytes { get; init; }
    public long PeakPooledMemoryBytes { get; init; }
    public Dictionary<int, int> BucketStatistics { get; init; }

    public double PoolEfficiency => TotalAllocations > 0 ? HitRate : 0.0;
    public long TotalPooledMemoryMB => TotalPooledMemoryBytes / (1024 * 1024);
    public long PeakPooledMemoryMB => PeakPooledMemoryBytes / (1024 * 1024);
}}
