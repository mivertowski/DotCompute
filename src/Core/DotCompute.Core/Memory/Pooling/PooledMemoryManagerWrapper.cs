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
/// Eviction policy for pooled memory management.
/// </summary>
public enum EvictionPolicy
{
    /// <summary>
    /// Evict the least recently used items first.
    /// </summary>
    LeastRecentlyUsed,

    /// <summary>
    /// Evict the least frequently used items first.
    /// </summary>
    LeastFrequentlyUsed,

    /// <summary>
    /// Evict the oldest items first (FIFO).
    /// </summary>
    FirstInFirstOut,

    /// <summary>
    /// Evict based on size - largest items first to free most memory quickly.
    /// </summary>
    LargestFirst
}

/// <summary>
/// Configuration options for the pooled memory manager.
/// </summary>
public sealed class PooledMemoryOptions
{
    /// <summary>
    /// Gets or sets the maximum pool size in bytes.
    /// </summary>
    public long MaxPoolSizeBytes { get; set; } = 512 * 1024 * 1024; // 512 MB

    /// <summary>
    /// Gets or sets the eviction policy.
    /// </summary>
    public EvictionPolicy EvictionPolicy { get; set; } = EvictionPolicy.LeastRecentlyUsed;

    /// <summary>
    /// Gets or sets whether to enable defragmentation.
    /// </summary>
    public bool EnableDefragmentation { get; set; } = true;

    /// <summary>
    /// Gets or sets the fragmentation threshold that triggers defragmentation (0.0 - 1.0).
    /// </summary>
    public double DefragmentationThreshold { get; set; } = 0.3; // 30%

    /// <summary>
    /// Gets or sets whether to track usage statistics.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of buffers to track.
    /// </summary>
    public int MaxTrackedBuffers { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the high watermark ratio that triggers eviction (0.0 - 1.0).
    /// </summary>
    public double HighWatermarkRatio { get; set; } = 0.9; // 90%

    /// <summary>
    /// Gets or sets the low watermark ratio to evict down to (0.0 - 1.0).
    /// </summary>
    public double LowWatermarkRatio { get; set; } = 0.7; // 70%

    /// <summary>
    /// Gets or sets the minimum time a buffer must be idle before eviction.
    /// </summary>
    public TimeSpan MinIdleTime { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Wrapper around IUnifiedMemoryManager that provides pool size management,
/// eviction policies, and fragmentation handling.
/// </summary>
/// <remarks>
/// This wrapper implements Phase 6 production readiness requirements:
/// - Pool size management with configurable limits
/// - LRU and other eviction policies
/// - Fragmentation detection and handling
/// </remarks>
public sealed class PooledMemoryManagerWrapper : IUnifiedMemoryManager
{
    private readonly IUnifiedMemoryManager _innerManager;
    private readonly PooledMemoryOptions _options;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<IUnifiedMemoryBuffer, BufferMetadata> _trackedBuffers;
    private readonly SemaphoreSlim _evictionLock;
    private volatile bool _disposed;

    // Statistics
    private long _currentPooledBytes;
    private long _peakPooledBytes;
    private long _totalEvictions;
    private long _totalDefragmentations;
    private long _allocations;
    private long _frees;

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
    /// Gets the current pooled bytes.
    /// </summary>
    public long CurrentPooledBytes => Interlocked.Read(ref _currentPooledBytes);

    /// <summary>
    /// Gets the fragmentation ratio (0.0 - 1.0).
    /// </summary>
    public double FragmentationRatio => CalculateFragmentation();

    /// <summary>
    /// Initializes a new instance of the PooledMemoryManagerWrapper class.
    /// </summary>
    /// <param name="innerManager">The underlying memory manager.</param>
    /// <param name="options">Pooling configuration options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public PooledMemoryManagerWrapper(
        IUnifiedMemoryManager innerManager,
        PooledMemoryOptions options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(innerManager);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _innerManager = innerManager;
        _options = options;
        _logger = logger;
        _trackedBuffers = new ConcurrentDictionary<IUnifiedMemoryBuffer, BufferMetadata>();
        _evictionLock = new SemaphoreSlim(1, 1);

        _logger.LogDebugMessage($"PooledMemoryManagerWrapper initialized with max pool size {_options.MaxPoolSizeBytes} bytes");
    }

    /// <inheritdoc/>
    public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        var sizeInBytes = count * Unsafe.SizeOf<T>();
        Interlocked.Increment(ref _allocations);

        // Check if we need to evict before allocating
        await EnsureCapacityAsync(sizeInBytes, cancellationToken).ConfigureAwait(false);

        var buffer = await _innerManager.AllocateAsync<T>(count, options, cancellationToken).ConfigureAwait(false);

        // Track the allocation
        TrackBuffer(buffer, sizeInBytes);

        return buffer;
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
        Interlocked.Increment(ref _allocations);

        // Check if we need to evict before allocating
        await EnsureCapacityAsync(sizeInBytes, cancellationToken).ConfigureAwait(false);

        var buffer = await _innerManager.AllocateRawAsync(sizeInBytes, options, cancellationToken).ConfigureAwait(false);

        // Track the allocation
        TrackBuffer(buffer, sizeInBytes);

        return buffer;
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
        UpdateLastAccess(source);
        UpdateLastAccess(destination);
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
        UpdateLastAccess(source);
        UpdateLastAccess(destination);
        return _innerManager.CopyAsync(source, sourceOffset, destination, destinationOffset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyToDeviceAsync<T>(
        ReadOnlyMemory<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        UpdateLastAccess(destination);
        return _innerManager.CopyToDeviceAsync(source, destination, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyFromDeviceAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        Memory<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        UpdateLastAccess(source);
        return _innerManager.CopyFromDeviceAsync(source, destination, cancellationToken);
    }

    /// <inheritdoc/>
    public void Free(IUnifiedMemoryBuffer buffer)
    {
        if (_trackedBuffers.TryRemove(buffer, out var metadata))
        {
            Interlocked.Add(ref _currentPooledBytes, -metadata.Size);
            Interlocked.Increment(ref _frees);
        }

        _innerManager.Free(buffer);
    }

    /// <inheritdoc/>
    public async ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        if (_trackedBuffers.TryRemove(buffer, out var metadata))
        {
            Interlocked.Add(ref _currentPooledBytes, -metadata.Size);
            Interlocked.Increment(ref _frees);
        }

        await _innerManager.FreeAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Check for fragmentation and defragment if needed
        if (_options.EnableDefragmentation)
        {
            var fragmentation = CalculateFragmentation();
            if (fragmentation > _options.DefragmentationThreshold)
            {
                await DefragmentAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await _innerManager.OptimizeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        ThrowIfDisposed();

        _trackedBuffers.Clear();
        Interlocked.Exchange(ref _currentPooledBytes, 0);

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

    private void TrackBuffer(IUnifiedMemoryBuffer buffer, long size)
    {
        var metadata = new BufferMetadata
        {
            Size = size,
            AllocatedAt = DateTimeOffset.UtcNow,
            LastAccessTime = DateTimeOffset.UtcNow,
            AccessCount = 1
        };

        if (_trackedBuffers.TryAdd(buffer, metadata))
        {
            var newTotal = Interlocked.Add(ref _currentPooledBytes, size);
            UpdatePeakPooled(newTotal);
        }
    }

    private void UpdateLastAccess(IUnifiedMemoryBuffer buffer)
    {
        if (_trackedBuffers.TryGetValue(buffer, out var metadata))
        {
            metadata.LastAccessTime = DateTimeOffset.UtcNow;
            Interlocked.Increment(ref metadata.AccessCount);
        }
    }

    private void UpdatePeakPooled(long current)
    {
        long peak;
        do
        {
            peak = _peakPooledBytes;
            if (current <= peak) break;
        } while (Interlocked.CompareExchange(ref _peakPooledBytes, current, peak) != peak);
    }

    private async ValueTask EnsureCapacityAsync(long requiredBytes, CancellationToken cancellationToken)
    {
        var currentPooled = Interlocked.Read(ref _currentPooledBytes);
        var highWatermark = (long)(_options.MaxPoolSizeBytes * _options.HighWatermarkRatio);

        if (currentPooled + requiredBytes <= highWatermark)
        {
            return;
        }

        await _evictionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check after acquiring lock
            currentPooled = Interlocked.Read(ref _currentPooledBytes);
            if (currentPooled + requiredBytes <= highWatermark)
            {
                return;
            }

            // Evict down to low watermark
            var lowWatermark = (long)(_options.MaxPoolSizeBytes * _options.LowWatermarkRatio);
            var targetToFree = currentPooled - lowWatermark + requiredBytes;

            await EvictBuffersAsync(targetToFree, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _evictionLock.Release();
        }
    }

    private async ValueTask EvictBuffersAsync(long targetToFree, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var minIdleTime = _options.MinIdleTime;

        // Get candidates ordered by eviction policy
        var candidates = _trackedBuffers
            .Where(kvp => now - kvp.Value.LastAccessTime >= minIdleTime)
            .Select(kvp => new { Buffer = kvp.Key, Metadata = kvp.Value })
            .ToList();

        // Sort by eviction policy
        var ordered = _options.EvictionPolicy switch
        {
            EvictionPolicy.LeastRecentlyUsed => candidates.OrderBy(c => c.Metadata.LastAccessTime),
            EvictionPolicy.LeastFrequentlyUsed => candidates.OrderBy(c => c.Metadata.AccessCount),
            EvictionPolicy.FirstInFirstOut => candidates.OrderBy(c => c.Metadata.AllocatedAt),
            EvictionPolicy.LargestFirst => candidates.OrderByDescending(c => c.Metadata.Size),
            _ => candidates.OrderBy(c => c.Metadata.LastAccessTime)
        };

        long freedBytes = 0;
        foreach (var candidate in ordered)
        {
            if (freedBytes >= targetToFree)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (_trackedBuffers.TryRemove(candidate.Buffer, out var metadata))
            {
                await _innerManager.FreeAsync(candidate.Buffer, cancellationToken).ConfigureAwait(false);
                freedBytes += metadata.Size;
                Interlocked.Add(ref _currentPooledBytes, -metadata.Size);
                Interlocked.Increment(ref _totalEvictions);
            }
        }

        if (freedBytes > 0)
        {
            _logger.LogDebugMessage($"Evicted {freedBytes} bytes ({_options.EvictionPolicy})");
        }
    }

    private double CalculateFragmentation()
    {
        // Estimate fragmentation based on allocation patterns
        // Higher fragmentation when many small allocations with gaps
        var buffers = _trackedBuffers.Values.ToList();
        if (buffers.Count < 2)
        {
            return 0.0;
        }

        var sizes = buffers.Select(b => b.Size).OrderBy(s => s).ToList();
        var avgSize = sizes.Average();
        var variance = sizes.Sum(s => Math.Pow(s - avgSize, 2)) / sizes.Count;
        var stdDev = Math.Sqrt(variance);

        // Coefficient of variation as fragmentation proxy
        var cv = avgSize > 0 ? stdDev / avgSize : 0;

        // Normalize to 0-1 range (CV > 1 is high fragmentation)
        return Math.Min(cv, 1.0);
    }

    private async ValueTask DefragmentAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _totalDefragmentations);
        _logger.LogInfoMessage("Starting defragmentation");

        // Defragmentation strategy: compact similar-sized allocations
        // This is a simplified implementation - real defrag would require
        // buffer migration which depends on backend capabilities

        await _innerManager.OptimizeAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInfoMessage("Defragmentation completed");
    }

    private MemoryStatistics GetCombinedStatistics()
    {
        var inner = _innerManager.Statistics;
        return new MemoryStatistics
        {
            TotalAllocated = inner.TotalAllocated,
            CurrentUsed = Interlocked.Read(ref _currentPooledBytes),
            PeakUsage = Interlocked.Read(ref _peakPooledBytes),
            ActiveAllocations = _trackedBuffers.Count,
            TotalAllocationCount = Interlocked.Read(ref _allocations),
            TotalDeallocationCount = Interlocked.Read(ref _frees),
            PoolHitRate = inner.PoolHitRate,
            FragmentationPercentage = FragmentationRatio * 100
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, GetType());

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _evictionLock.Dispose();
        _trackedBuffers.Clear();
        _innerManager.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _evictionLock.Dispose();
        _trackedBuffers.Clear();
        await _innerManager.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class BufferMetadata
    {
        public long Size { get; init; }
        public DateTimeOffset AllocatedAt { get; init; }
        public DateTimeOffset LastAccessTime { get; set; }
        public long AccessCount;
    }
}
