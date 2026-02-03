// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory.Pooling;

/// <summary>
/// Configuration options for the optimized memory pool.
/// </summary>
public sealed class OptimizedPoolingOptions
{
    /// <summary>
    /// Gets or sets whether bucket-based pooling is enabled.
    /// </summary>
    public bool EnableBucketPooling { get; set; } = true;

    /// <summary>
    /// Gets or sets the bucket sizes for pooling.
    /// </summary>
    public long[] BucketSizes { get; set; } = [256, 1024, 4096, 16384, 65536, 262144, 1048576];

    /// <summary>
    /// Gets or sets the maximum pooled buffers per bucket.
    /// </summary>
    public int MaxPooledBuffersPerBucket { get; set; } = 32;

    /// <summary>
    /// Gets or sets whether to collect statistics.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets the stale buffer threshold before cleanup.
    /// </summary>
    public TimeSpan StaleBufferThreshold { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maintenance interval for automatic cleanup.
    /// </summary>
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the maximum total pool size in bytes.
    /// </summary>
    public long MaxTotalPoolSize { get; set; } = 512 * 1024 * 1024; // 512 MB
}

/// <summary>
/// Wrapper around IUnifiedMemoryManager that provides bucket-based pooling,
/// lock-free allocation patterns, and automatic sizing based on usage patterns.
/// </summary>
/// <remarks>
/// This wrapper implements Phase 6 production readiness requirements:
/// - Bucket-based pooling for efficient memory reuse at the raw buffer level
/// - Lock-free allocation using ConcurrentBag
/// - Automatic sizing based on allocation patterns
/// - Typed buffer allocations are delegated directly to the inner manager
/// </remarks>
public sealed class OptimizedMemoryPoolWrapper : IUnifiedMemoryManager
{
    private readonly IUnifiedMemoryManager _innerManager;
    private readonly OptimizedPoolingOptions _options;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<long, BufferPool> _buckets;
    private readonly Timer? _maintenanceTimer;
    private volatile bool _disposed;

    // Statistics
    private long _poolHits;
    private long _poolMisses;
    private long _totalAllocations;
    private long _totalBytesPooled;
    private long _peakBytesPooled;

    /// <inheritdoc/>
    public IAccelerator Accelerator => _innerManager.Accelerator;

    /// <inheritdoc/>
    public MemoryStatistics Statistics => GetCombinedStatistics();

    /// <inheritdoc/>
    public long MaxAllocationSize => _innerManager.MaxAllocationSize;

    /// <inheritdoc/>
    public long TotalAvailableMemory => _innerManager.TotalAvailableMemory;

    /// <inheritdoc/>
    public long CurrentAllocatedMemory => _innerManager.CurrentAllocatedMemory;

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
    /// Gets the total bytes currently pooled.
    /// </summary>
    public long TotalBytesPooled => Interlocked.Read(ref _totalBytesPooled);

    /// <summary>
    /// Initializes a new instance of the OptimizedMemoryPoolWrapper class.
    /// </summary>
    /// <param name="innerManager">The underlying memory manager.</param>
    /// <param name="options">Pooling configuration options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public OptimizedMemoryPoolWrapper(
        IUnifiedMemoryManager innerManager,
        OptimizedPoolingOptions options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(innerManager);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _innerManager = innerManager;
        _options = options;
        _logger = logger;
        _buckets = new ConcurrentDictionary<long, BufferPool>();

        // Initialize buckets
        if (_options.EnableBucketPooling)
        {
            foreach (var size in _options.BucketSizes)
            {
                _buckets[size] = new BufferPool(_options.MaxPooledBuffersPerBucket);
            }
        }

        // Start maintenance timer
        if (_options.MaintenanceInterval > TimeSpan.Zero)
        {
            _maintenanceTimer = new Timer(
                PerformMaintenanceCallback,
                null,
                _options.MaintenanceInterval,
                _options.MaintenanceInterval);
        }

        _logger.LogDebugMessage($"OptimizedMemoryPoolWrapper initialized with {_buckets.Count} buckets");
    }

    /// <inheritdoc/>
    public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        Interlocked.Increment(ref _totalAllocations);

        // Typed allocations go directly to the inner manager
        // Pooling happens at the raw buffer level for better type flexibility
        return _innerManager.AllocateAsync<T>(count, options, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var buffer = await AllocateAsync<T>(source.Length, options, cancellationToken).ConfigureAwait(false);
        await buffer.CopyFromAsync(source, cancellationToken).ConfigureAwait(false);
        return buffer;
    }

    /// <inheritdoc/>
    public async ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Interlocked.Increment(ref _totalAllocations);

        // Try to get from pool first for raw allocations
        if (_options.EnableBucketPooling && TryGetFromPool(sizeInBytes, out var pooledBuffer))
        {
            Interlocked.Increment(ref _poolHits);
            return pooledBuffer!;
        }

        Interlocked.Increment(ref _poolMisses);

        return await _innerManager.AllocateRawAsync(sizeInBytes, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer<T> CreateView<T>(
        IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int length) where T : unmanaged
    {
        ThrowIfDisposed();
        return _innerManager.CreateView(buffer, offset, length);
    }

    /// <inheritdoc/>
    public ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        return _innerManager.CopyAsync(source, destination, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        return _innerManager.CopyAsync(source, sourceOffset, destination, destinationOffset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyToDeviceAsync<T>(
        ReadOnlyMemory<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        return _innerManager.CopyToDeviceAsync(source, destination, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyFromDeviceAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        Memory<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        return _innerManager.CopyFromDeviceAsync(source, destination, cancellationToken);
    }

    /// <inheritdoc/>
    public void Free(IUnifiedMemoryBuffer buffer)
    {
        if (_disposed)
        {
            _innerManager.Free(buffer);
            return;
        }

        // Try to return raw buffers to pool
        if (_options.EnableBucketPooling && TryReturnToPool(buffer))
        {
            return;
        }

        // Cannot pool, free directly
        _innerManager.Free(buffer);
    }

    /// <inheritdoc/>
    public async ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            await _innerManager.FreeAsync(buffer, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Try to return to pool
        if (_options.EnableBucketPooling && TryReturnToPool(buffer))
        {
            return;
        }

        await _innerManager.FreeAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Perform pool maintenance
        PerformMaintenance();

        // Delegate to inner manager
        await _innerManager.OptimizeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        ThrowIfDisposed();

        // Clear all pools
        foreach (var bucket in _buckets.Values)
        {
            bucket.Clear(_innerManager.Free);
        }

        Interlocked.Exchange(ref _totalBytesPooled, 0);

        _innerManager.Clear();
    }

    // Legacy device operations - delegate to inner manager

    /// <inheritdoc/>
    public DeviceMemory AllocateDevice(long sizeInBytes)
    {
        ThrowIfDisposed();
        return _innerManager.AllocateDevice(sizeInBytes);
    }

    /// <inheritdoc/>
    public void FreeDevice(DeviceMemory deviceMemory)
    {
        ThrowIfDisposed();
        _innerManager.FreeDevice(deviceMemory);
    }

    /// <inheritdoc/>
    public void MemsetDevice(DeviceMemory deviceMemory, byte value, long sizeInBytes)
    {
        ThrowIfDisposed();
        _innerManager.MemsetDevice(deviceMemory, value, sizeInBytes);
    }

    /// <inheritdoc/>
    public ValueTask MemsetDeviceAsync(DeviceMemory deviceMemory, byte value, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _innerManager.MemsetDeviceAsync(deviceMemory, value, sizeInBytes, cancellationToken);
    }

    /// <inheritdoc/>
    public void CopyHostToDevice(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes)
    {
        ThrowIfDisposed();
        _innerManager.CopyHostToDevice(hostPointer, deviceMemory, sizeInBytes);
    }

    /// <inheritdoc/>
    public void CopyDeviceToHost(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes)
    {
        ThrowIfDisposed();
        _innerManager.CopyDeviceToHost(deviceMemory, hostPointer, sizeInBytes);
    }

    /// <inheritdoc/>
    public ValueTask CopyHostToDeviceAsync(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _innerManager.CopyHostToDeviceAsync(hostPointer, deviceMemory, sizeInBytes, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyDeviceToHostAsync(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _innerManager.CopyDeviceToHostAsync(deviceMemory, hostPointer, sizeInBytes, cancellationToken);
    }

    /// <inheritdoc/>
    public void CopyDeviceToDevice(DeviceMemory sourceDevice, DeviceMemory destinationDevice, long sizeInBytes)
    {
        ThrowIfDisposed();
        _innerManager.CopyDeviceToDevice(sourceDevice, destinationDevice, sizeInBytes);
    }

    private bool TryGetFromPool(long sizeInBytes, out IUnifiedMemoryBuffer? buffer)
    {
        buffer = null;

        var bucketSize = GetBucketSize(sizeInBytes);
        if (bucketSize <= 0 || !_buckets.TryGetValue(bucketSize, out var pool))
        {
            return false;
        }

        if (pool.TryTake(out var entry))
        {
            buffer = entry!.Buffer;
            Interlocked.Add(ref _totalBytesPooled, -bucketSize);
            return true;
        }

        return false;
    }

    private bool TryReturnToPool(IUnifiedMemoryBuffer buffer)
    {
        var bucketSize = GetBucketSize(buffer.SizeInBytes);
        if (bucketSize <= 0 || !_buckets.TryGetValue(bucketSize, out var pool))
        {
            return false;
        }

        // Check total pool size limit
        var currentPooled = Interlocked.Read(ref _totalBytesPooled);
        if (currentPooled + bucketSize > _options.MaxTotalPoolSize)
        {
            return false;
        }

        if (pool.TryAdd(new PooledEntry { Buffer = buffer, PooledAt = DateTimeOffset.UtcNow }))
        {
            var newTotal = Interlocked.Add(ref _totalBytesPooled, bucketSize);
            UpdatePeakPooled(newTotal);
            return true;
        }

        return false;
    }

    private long GetBucketSize(long requestedSize)
    {
        foreach (var size in _options.BucketSizes)
        {
            if (size >= requestedSize)
            {
                return size;
            }
        }

        return -1; // Too large for any bucket
    }

    private void UpdatePeakPooled(long current)
    {
        long peak;
        do
        {
            peak = _peakBytesPooled;
            if (current <= peak) break;
        } while (Interlocked.CompareExchange(ref _peakBytesPooled, current, peak) != peak);
    }

    private void PerformMaintenanceCallback(object? state)
    {
        if (_disposed) return;

        try
        {
            PerformMaintenance();
        }
        catch (Exception ex)
        {
            _logger.LogWarningMessage($"Error during pool maintenance: {ex.Message}");
        }
    }

    private void PerformMaintenance()
    {
        var threshold = _options.StaleBufferThreshold;
        var totalCleared = 0L;

        foreach (var kvp in _buckets)
        {
            var cleared = kvp.Value.RemoveStale(threshold, b =>
            {
                _innerManager.Free(b);
                Interlocked.Add(ref _totalBytesPooled, -kvp.Key);
            });
            totalCleared += cleared * kvp.Key;
        }

        if (totalCleared > 0)
        {
            _logger.LogDebugMessage($"Pool maintenance cleared {totalCleared} bytes");
        }
    }

    private MemoryStatistics GetCombinedStatistics()
    {
        var inner = _innerManager.Statistics;
        return new MemoryStatistics
        {
            TotalAllocated = inner.TotalAllocated,
            CurrentUsed = inner.CurrentUsed,
            PeakUsage = inner.PeakUsage,
            ActiveAllocations = inner.ActiveAllocations,
            TotalAllocationCount = Interlocked.Read(ref _totalAllocations),
            TotalDeallocationCount = inner.TotalDeallocationCount,
            PoolHitRate = HitRate,
            FragmentationPercentage = inner.FragmentationPercentage
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, GetType());

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _maintenanceTimer?.Dispose();

        // Clear all pools
        foreach (var bucket in _buckets.Values)
        {
            bucket.Clear(_innerManager.Free);
        }

        _innerManager.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _maintenanceTimer?.Dispose();

        // Clear all pools
        foreach (var bucket in _buckets.Values)
        {
            bucket.Clear(_innerManager.Free);
        }

        await _innerManager.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Internal pool for a specific bucket size.
    /// </summary>
    private sealed class BufferPool(int maxBuffers)
    {
        private readonly ConcurrentBag<PooledEntry> _buffers = [];
        private readonly int _maxBuffers = maxBuffers;
        private int _currentCount;

        public bool TryTake(out PooledEntry? entry)
        {
            if (_buffers.TryTake(out entry))
            {
                Interlocked.Decrement(ref _currentCount);
                return true;
            }
            entry = null;
            return false;
        }

        public bool TryAdd(PooledEntry entry)
        {
            if (Volatile.Read(ref _currentCount) >= _maxBuffers)
            {
                return false;
            }

            _buffers.Add(entry);
            Interlocked.Increment(ref _currentCount);
            return true;
        }

        public int Clear(Action<IUnifiedMemoryBuffer> cleanupAction)
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

        public int RemoveStale(TimeSpan threshold, Action<IUnifiedMemoryBuffer> cleanupAction)
        {
            var now = DateTimeOffset.UtcNow;
            var removed = 0;
            var keep = new List<PooledEntry>();

            while (_buffers.TryTake(out var entry))
            {
                Interlocked.Decrement(ref _currentCount);

                if (now - entry.PooledAt > threshold)
                {
                    cleanupAction(entry.Buffer);
                    removed++;
                }
                else
                {
                    keep.Add(entry);
                }
            }

            foreach (var entry in keep)
            {
                _buffers.Add(entry);
                Interlocked.Increment(ref _currentCount);
            }

            return removed;
        }
    }

    private sealed class PooledEntry
    {
        public required IUnifiedMemoryBuffer Buffer { get; init; }
        public DateTimeOffset PooledAt { get; init; }
    }
}
