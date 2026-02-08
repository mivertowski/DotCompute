// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.Pooling;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Pooling;

/// <summary>
/// Abstract base class for size-based memory buffer pools.
/// Implements power-of-2 bucketing strategy for efficient memory allocation.
/// </summary>
/// <typeparam name="TBuffer">The type of memory buffer being pooled.</typeparam>
/// <remarks>
/// <para>
/// This base class implements the common patterns shared across memory pool managers:
/// </para>
/// <list type="bullet">
/// <item><description>Power-of-2 size bucketing for efficient allocation matching</description></item>
/// <item><description>Per-bucket statistics tracking</description></item>
/// <item><description>Configurable min/max pool sizes</description></item>
/// <item><description>Automatic maintenance with stale buffer cleanup</description></item>
/// <item><description>Thread-safe operations using ConcurrentDictionary</description></item>
/// </list>
/// <para>
/// Used by CUDA, Metal, and OpenCL memory pool managers.
/// </para>
/// </remarks>
public abstract class SizeBasedMemoryPoolBase<TBuffer> : IAsyncDisposable, IDisposable where TBuffer : class
{
    private readonly ConcurrentDictionary<long, BufferBucket> _buckets = new();
    private readonly ILogger _logger;
    private readonly Timer? _maintenanceTimer;
    private bool _disposed;

    // Configuration
    private readonly long _minBucketSize;
    private readonly long _maxBucketSize;
    private readonly int _maxBuffersPerBucket;
    private readonly long _maxTotalPoolBytes;
    private readonly TimeSpan _maintenanceInterval;

    // Statistics - modified via Interlocked operations
    private long _totalAllocations;
    private long _poolHits;
    private long _poolMisses;
    private long _totalBytesAllocated;
    private long _currentBytesInPools;
    private long _peakBytesInPools;
    private long _maintenanceCount;

    /// <summary>
    /// Gets the logger instance for this pool.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Gets whether this pool has been disposed.
    /// </summary>
    protected bool IsDisposed => _disposed;

    /// <summary>
    /// Gets the name of this pool manager for logging purposes.
    /// </summary>
    protected abstract string ManagerName { get; }

    /// <summary>
    /// Gets the pool hit rate as a fraction (0.0 to 1.0).
    /// </summary>
    public double HitRate
    {
        get
        {
            var total = Interlocked.Read(ref _totalAllocations);
            var hits = Interlocked.Read(ref _poolHits);
            return total > 0 ? (double)hits / total : 0.0;
        }
    }

    /// <summary>
    /// Gets the total bytes currently in pools.
    /// </summary>
    public long TotalBytesInPools => Interlocked.Read(ref _currentBytesInPools);

    /// <summary>
    /// Gets the total bytes allocated through this pool manager.
    /// </summary>
    public long TotalBytesAllocated => Interlocked.Read(ref _totalBytesAllocated);

    /// <summary>
    /// Gets the peak bytes that were held in pools.
    /// </summary>
    public long PeakBytesInPools => Interlocked.Read(ref _peakBytesInPools);

    /// <summary>
    /// Gets comprehensive statistics about pool performance.
    /// </summary>
    public MemoryPoolStatistics Statistics => new()
    {
        TotalAllocations = Interlocked.Read(ref _totalAllocations),
        PoolHits = Interlocked.Read(ref _poolHits),
        PoolMisses = Interlocked.Read(ref _poolMisses),
        HitRate = HitRate,
        TotalBytesAllocated = Interlocked.Read(ref _totalBytesAllocated),
        TotalBytesInPools = Interlocked.Read(ref _currentBytesInPools),
        PeakBytesInPools = Interlocked.Read(ref _peakBytesInPools),
        BucketCount = _buckets.Count,
        MaintenanceCount = Interlocked.Read(ref _maintenanceCount)
    };

    /// <summary>
    /// Represents a bucket of buffers for a specific size class.
    /// </summary>
    protected sealed class BufferBucket
    {
        private readonly ConcurrentBag<PooledBufferEntry<TBuffer>> _buffers = new();
        private long _currentCount;
        private long _totalAllocations;
        private long _hits;
        private long _misses;

        /// <summary>
        /// Gets the size of buffers in this bucket.
        /// </summary>
        public long BucketSize { get; }

        /// <summary>
        /// Gets the maximum number of buffers in this bucket.
        /// </summary>
        public int MaxBuffers { get; }

        /// <summary>
        /// Gets the current number of buffers in this bucket.
        /// </summary>
        public int CurrentCount => (int)Interlocked.Read(ref _currentCount);

        /// <summary>
        /// Gets the total allocations from this bucket.
        /// </summary>
        public long TotalAllocations => Interlocked.Read(ref _totalAllocations);

        /// <summary>
        /// Gets the number of hits (successful pool retrievals).
        /// </summary>
        public long Hits => Interlocked.Read(ref _hits);

        /// <summary>
        /// Gets the number of misses (new allocations required).
        /// </summary>
        public long Misses => Interlocked.Read(ref _misses);

        /// <summary>
        /// Gets the hit rate for this bucket.
        /// </summary>
        public double HitRate
        {
            get
            {
                var total = Interlocked.Read(ref _totalAllocations);
                var hits = Interlocked.Read(ref _hits);
                return total > 0 ? (double)hits / total : 0.0;
            }
        }

        /// <summary>
        /// Initializes a new buffer bucket.
        /// </summary>
        /// <param name="bucketSize">The size of buffers in this bucket.</param>
        /// <param name="maxBuffers">Maximum number of buffers to hold.</param>
        public BufferBucket(long bucketSize, int maxBuffers)
        {
            BucketSize = bucketSize;
            MaxBuffers = maxBuffers;
        }

        /// <summary>
        /// Tries to take a buffer from this bucket.
        /// </summary>
        /// <param name="entry">The buffer entry if successful.</param>
        /// <returns>True if a buffer was retrieved; false if bucket is empty.</returns>
        public bool TryTake(out PooledBufferEntry<TBuffer>? entry)
        {
            Interlocked.Increment(ref _totalAllocations);

            if (_buffers.TryTake(out entry))
            {
                Interlocked.Decrement(ref _currentCount);
                Interlocked.Increment(ref _hits);
                return true;
            }

            Interlocked.Increment(ref _misses);
            entry = null;
            return false;
        }

        /// <summary>
        /// Tries to add a buffer to this bucket.
        /// </summary>
        /// <param name="entry">The buffer entry to add.</param>
        /// <returns>True if added; false if bucket is full.</returns>
        public bool TryAdd(PooledBufferEntry<TBuffer> entry)
        {
            if (CurrentCount >= MaxBuffers)
            {
                return false;
            }

            _buffers.Add(entry);
            Interlocked.Increment(ref _currentCount);
            return true;
        }

        /// <summary>
        /// Clears all buffers from this bucket.
        /// </summary>
        /// <param name="cleanupAction">Action to cleanup each buffer.</param>
        /// <returns>The number of buffers cleared.</returns>
        public int Clear(Action<TBuffer> cleanupAction)
        {
            var count = 0;
            while (_buffers.TryTake(out var entry))
            {
                cleanupAction(entry.Buffer);
                Interlocked.Decrement(ref _currentCount);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Trims excess buffers from this bucket.
        /// </summary>
        /// <param name="targetCount">Target number of buffers to keep.</param>
        /// <param name="cleanupAction">Action to cleanup each buffer.</param>
        /// <returns>The number of buffers trimmed.</returns>
        public int TrimExcess(int targetCount, Action<TBuffer> cleanupAction)
        {
            var trimmed = 0;
            while (CurrentCount > targetCount && _buffers.TryTake(out var entry))
            {
                cleanupAction(entry.Buffer);
                Interlocked.Decrement(ref _currentCount);
                trimmed++;
            }
            return trimmed;
        }

        /// <summary>
        /// Removes stale buffers that haven't been used recently.
        /// </summary>
        /// <param name="staleThreshold">Time after which a buffer is considered stale.</param>
        /// <param name="cleanupAction">Action to cleanup each buffer.</param>
        /// <returns>The number of stale buffers removed.</returns>
        public int RemoveStaleBuffers(TimeSpan staleThreshold, Action<TBuffer> cleanupAction)
        {
            var now = DateTimeOffset.UtcNow;
            var removed = 0;
            var keepList = new List<PooledBufferEntry<TBuffer>>();

            while (_buffers.TryTake(out var entry))
            {
                Interlocked.Decrement(ref _currentCount);

                if (now - entry.LastAccessTime > staleThreshold)
                {
                    cleanupAction(entry.Buffer);
                    removed++;
                }
                else
                {
                    keepList.Add(entry);
                }
            }

            // Add back the non-stale buffers
            foreach (var entry in keepList)
            {
                _buffers.Add(entry);
                Interlocked.Increment(ref _currentCount);
            }

            return removed;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SizeBasedMemoryPoolBase{TBuffer}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="minBucketSize">Minimum buffer size to pool (default: 256 bytes).</param>
    /// <param name="maxBucketSize">Maximum buffer size to pool (default: 256 MB).</param>
    /// <param name="maxBuffersPerBucket">Maximum buffers per size bucket (default: 16).</param>
    /// <param name="maxTotalPoolBytes">Maximum total bytes in pool (default: 1 GB).</param>
    /// <param name="maintenanceInterval">Interval for automatic maintenance (default: 5 minutes).</param>
    protected SizeBasedMemoryPoolBase(
        ILogger logger,
        long minBucketSize = 256,
        long maxBucketSize = 256 * 1024 * 1024,
        int maxBuffersPerBucket = 16,
        long maxTotalPoolBytes = 1024L * 1024 * 1024,
        TimeSpan maintenanceInterval = default)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _minBucketSize = minBucketSize;
        _maxBucketSize = maxBucketSize;
        _maxBuffersPerBucket = maxBuffersPerBucket;
        _maxTotalPoolBytes = maxTotalPoolBytes;
        _maintenanceInterval = maintenanceInterval == default ? TimeSpan.FromMinutes(5) : maintenanceInterval;

        // Initialize buckets for power-of-2 sizes
        InitializeBuckets();

        // Start maintenance timer
        if (_maintenanceInterval > TimeSpan.Zero)
        {
            _maintenanceTimer = new Timer(
                MaintenanceCallback,
                null,
                _maintenanceInterval,
                _maintenanceInterval);
        }

        _logger.LogInformation(
            "{ManagerName}: Initialized (MinSize={MinSize}, MaxSize={MaxSize}, MaxPerBucket={MaxPerBucket}, MaxTotal={MaxTotal})",
            ManagerName, _minBucketSize, _maxBucketSize, _maxBuffersPerBucket, _maxTotalPoolBytes);
    }

    private void InitializeBuckets()
    {
        var size = _minBucketSize;
        while (size <= _maxBucketSize)
        {
            _buckets[size] = new BufferBucket(size, _maxBuffersPerBucket);
            size *= 2;
        }
    }

    /// <summary>
    /// Tries to get a buffer from the pool.
    /// </summary>
    /// <param name="sizeInBytes">The required buffer size.</param>
    /// <returns>A buffer entry if found in pool; null otherwise.</returns>
    protected PooledBufferEntry<TBuffer>? TryGetFromPool(long sizeInBytes)
    {
        ThrowIfDisposed();

        Interlocked.Increment(ref _totalAllocations);

        var bucketSize = GetBucketSize(sizeInBytes);

        if (bucketSize > _maxBucketSize)
        {
            // Too large for pool
            Interlocked.Increment(ref _poolMisses);
            return null;
        }

        if (!_buckets.TryGetValue(bucketSize, out var bucket))
        {
            Interlocked.Increment(ref _poolMisses);
            return null;
        }

        if (bucket.TryTake(out var entry))
        {
            Interlocked.Increment(ref _poolHits);
            Interlocked.Add(ref _currentBytesInPools, -bucketSize);

            _logger.LogTrace("{ManagerName}: Pool hit for {Size} bytes (bucket={BucketSize})",
                ManagerName, sizeInBytes, bucketSize);

            entry!.LastAccessTime = DateTimeOffset.UtcNow;
            entry.UseCount++;
            return entry;
        }

        Interlocked.Increment(ref _poolMisses);
        return null;
    }

    /// <summary>
    /// Returns a buffer to the pool.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    /// <param name="bufferSize">The actual size of the buffer.</param>
    /// <returns>True if the buffer was added to the pool; false if it was rejected.</returns>
    protected bool ReturnToPool(TBuffer buffer, long bufferSize)
    {
        if (_disposed || buffer == null)
        {
            return false;
        }

        var bucketSize = GetBucketSize(bufferSize);

        if (bucketSize < _minBucketSize || bucketSize > _maxBucketSize)
        {
            _logger.LogTrace("{ManagerName}: Buffer size {Size} out of pooling range, discarding",
                ManagerName, bufferSize);
            return false;
        }

        // Check total pool size limit
        var currentPoolSize = Interlocked.Read(ref _currentBytesInPools);
        if (currentPoolSize + bucketSize > _maxTotalPoolBytes)
        {
            _logger.LogDebug("{ManagerName}: Pool at capacity ({Current}/{Max} bytes), discarding buffer",
                ManagerName, currentPoolSize, _maxTotalPoolBytes);
            return false;
        }

        if (!_buckets.TryGetValue(bucketSize, out var bucket))
        {
            return false;
        }

        var entry = new PooledBufferEntry<TBuffer>
        {
            Buffer = buffer,
            Size = bufferSize,
            PooledAt = DateTimeOffset.UtcNow,
            LastAccessTime = DateTimeOffset.UtcNow,
            UseCount = 0
        };

        if (!bucket.TryAdd(entry))
        {
            _logger.LogTrace("{ManagerName}: Bucket {BucketSize} full, discarding buffer",
                ManagerName, bucketSize);
            return false;
        }

        var newPoolSize = Interlocked.Add(ref _currentBytesInPools, bucketSize);
        UpdatePeakPoolSize(newPoolSize);

        _logger.LogTrace("{ManagerName}: Buffer returned to pool (size={Size}, bucket={BucketSize}, pool={PoolSize})",
            ManagerName, bufferSize, bucketSize, newPoolSize);

        return true;
    }

    /// <summary>
    /// Records bytes allocated (for statistics tracking).
    /// </summary>
    /// <param name="bytes">The number of bytes allocated.</param>
    protected void RecordAllocation(long bytes)
    {
        Interlocked.Add(ref _totalBytesAllocated, bytes);
    }

    /// <summary>
    /// Records bytes freed (for statistics tracking).
    /// </summary>
    /// <param name="bytes">The number of bytes freed.</param>
    protected void RecordDeallocation(long bytes)
    {
        Interlocked.Add(ref _totalBytesAllocated, -bytes);
    }

    /// <summary>
    /// Gets the bucket size for a requested allocation size.
    /// </summary>
    /// <param name="requestedSize">The requested size in bytes.</param>
    /// <returns>The bucket size (rounded up to next power of 2).</returns>
    protected long GetBucketSize(long requestedSize)
    {
        if (requestedSize <= _minBucketSize)
        {
            return _minBucketSize;
        }

        // Round up to next power of 2
        var size = _minBucketSize;
        while (size < requestedSize && size < _maxBucketSize)
        {
            size *= 2;
        }

        return size;
    }

    /// <summary>
    /// Gets a bucket for a specific size, or null if size is out of range.
    /// </summary>
    /// <param name="bucketSize">The bucket size.</param>
    /// <returns>The bucket, or null if not found.</returns>
    protected BufferBucket? GetBucket(long bucketSize)
    {
        return _buckets.TryGetValue(bucketSize, out var bucket) ? bucket : null;
    }

    /// <summary>
    /// Cleans up a buffer when it cannot be returned to the pool.
    /// </summary>
    /// <param name="buffer">The buffer to clean up.</param>
    protected abstract void CleanupBuffer(TBuffer buffer);

    private void UpdatePeakPoolSize(long currentSize)
    {
        long currentPeak;
        do
        {
            currentPeak = _peakBytesInPools;
            if (currentSize <= currentPeak)
            {
                break;
            }
        } while (Interlocked.CompareExchange(ref _peakBytesInPools, currentSize, currentPeak) != currentPeak);
    }

    private void MaintenanceCallback(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            PerformMaintenance();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ManagerName}: Error during maintenance", ManagerName);
        }
    }

    /// <summary>
    /// Performs maintenance on the pool (trimming excess buffers, removing stale entries).
    /// </summary>
    /// <returns>The number of buffers cleaned up.</returns>
    public virtual int PerformMaintenance()
    {
        if (_disposed)
        {
            return 0;
        }

        Interlocked.Increment(ref _maintenanceCount);
        var totalCleaned = 0;

        foreach (var bucket in _buckets.Values)
        {
            var targetCount = bucket.MaxBuffers / 2;
            var cleaned = bucket.TrimExcess(targetCount, CleanupBuffer);

            if (cleaned > 0)
            {
                Interlocked.Add(ref _currentBytesInPools, -cleaned * bucket.BucketSize);
                totalCleaned += cleaned;
            }
        }

        if (totalCleaned > 0)
        {
            _logger.LogDebug("{ManagerName}: Maintenance cleaned up {Count} buffers",
                ManagerName, totalCleaned);
        }

        return totalCleaned;
    }

    /// <summary>
    /// Clears all pools, cleaning up all buffers.
    /// </summary>
    public virtual void ClearPools()
    {
        ThrowIfDisposed();

        _logger.LogInformation("{ManagerName}: Clearing all pools", ManagerName);

        var totalCleared = 0;
        var freedBytes = 0L;

        foreach (var bucket in _buckets.Values)
        {
            var count = bucket.Clear(CleanupBuffer);
            totalCleared += count;
            freedBytes += count * bucket.BucketSize;
        }

        Interlocked.Exchange(ref _currentBytesInPools, 0);

        _logger.LogInformation("{ManagerName}: Cleared {Count} buffers, freed {Bytes} bytes",
            ManagerName, totalCleared, freedBytes);
    }

    /// <summary>
    /// Throws an ObjectDisposedException if this pool has been disposed.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by this pool.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(); false if from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _maintenanceTimer?.Dispose();
            ClearPools();

            var stats = Statistics;
            _logger.LogInformation(
                "{ManagerName}: Disposed - Stats: Allocations={Allocations}, HitRate={HitRate:P1}, TotalBytes={TotalBytes}",
                ManagerName, stats.TotalAllocations, stats.HitRate, stats.TotalBytesAllocated);
        }

        _disposed = true;
    }

    /// <inheritdoc/>
    public virtual async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _maintenanceTimer?.Dispose();

        try
        {
            ClearPools();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ManagerName}: Error clearing pools during disposal", ManagerName);
        }

        var stats = Statistics;
        _logger.LogInformation(
            "{ManagerName}: Disposed - Stats: Allocations={Allocations}, HitRate={HitRate:P1}, TotalBytes={TotalBytes}",
            ManagerName, stats.TotalAllocations, stats.HitRate, stats.TotalBytesAllocated);

        _disposed = true;
        GC.SuppressFinalize(this);

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>
/// Represents a pooled buffer entry with metadata.
/// </summary>
/// <typeparam name="TBuffer">The type of buffer.</typeparam>
public sealed class PooledBufferEntry<TBuffer> where TBuffer : class
{
    /// <summary>
    /// Gets the pooled buffer.
    /// </summary>
    public required TBuffer Buffer { get; init; }

    /// <summary>
    /// Gets the buffer size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Gets when the buffer was added to the pool.
    /// </summary>
    public DateTimeOffset PooledAt { get; init; }

    /// <summary>
    /// Gets or sets the last time this buffer was accessed.
    /// </summary>
    public DateTimeOffset LastAccessTime { get; set; }

    /// <summary>
    /// Gets or sets the number of times this buffer has been reused.
    /// </summary>
    public long UseCount { get; set; }
}

/// <summary>
/// Statistics for a size-based memory pool.
/// </summary>
public sealed class MemoryPoolStatistics
{
    /// <summary>
    /// Gets the total number of allocations.
    /// </summary>
    public long TotalAllocations { get; init; }

    /// <summary>
    /// Gets the number of pool hits.
    /// </summary>
    public long PoolHits { get; init; }

    /// <summary>
    /// Gets the number of pool misses.
    /// </summary>
    public long PoolMisses { get; init; }

    /// <summary>
    /// Gets the hit rate as a fraction (0.0 to 1.0).
    /// </summary>
    public double HitRate { get; init; }

    /// <summary>
    /// Gets the total bytes allocated through this pool.
    /// </summary>
    public long TotalBytesAllocated { get; init; }

    /// <summary>
    /// Gets the total bytes currently in pools.
    /// </summary>
    public long TotalBytesInPools { get; init; }

    /// <summary>
    /// Gets the peak bytes that were held in pools.
    /// </summary>
    public long PeakBytesInPools { get; init; }

    /// <summary>
    /// Gets the number of size buckets.
    /// </summary>
    public int BucketCount { get; init; }

    /// <summary>
    /// Gets the number of maintenance operations performed.
    /// </summary>
    public long MaintenanceCount { get; init; }
}
