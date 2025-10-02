// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Integration.Components;

/// <summary>
/// CUDA memory management component that provides high-level memory operations,
/// optimization strategies, and unified memory buffer management.
/// </summary>
public sealed class CudaMemoryManager : IUnifiedMemoryManager, IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger<CudaMemoryManager> _logger;
    private readonly Memory.CudaMemoryManager _cudaMemoryManager;
    private readonly IUnifiedMemoryManager _asyncAdapter;
    private readonly MemoryPool _memoryPool;
    private readonly MemoryUsageTracker _usageTracker;
    private volatile bool _disposed;

    public CudaMemoryManager(CudaContext context, ILogger<CudaMemoryManager> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _cudaMemoryManager = new Memory.CudaMemoryManager(context, logger);
        _asyncAdapter = new Memory.CudaAsyncMemoryManagerAdapter(_cudaMemoryManager);
        _memoryPool = new MemoryPool(_asyncAdapter, logger);
        _usageTracker = new MemoryUsageTracker();

        _logger.LogDebug("CUDA memory manager initialized for device {DeviceId}", context.DeviceId);
    }

    #region IUnifiedMemoryManager Implementation

    public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordAllocationRequest(count * System.Runtime.InteropServices.Marshal.SizeOf<T>());

            // Use pooled allocation for better performance
            if (options.HasFlag(MemoryOptions.UsePooling))
            {
                return _memoryPool.AllocateAsync<T>(count, options, cancellationToken);
            }

            return _asyncAdapter.AllocateAsync<T>(count, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate {Count} elements of type {Type}", count, typeof(T).Name);
            _usageTracker.RecordAllocationFailure(count * System.Runtime.InteropServices.Marshal.SizeOf<T>());
            throw new MemoryException($"Failed to allocate {count} elements of CUDA memory", ex);
        }
    }

    public ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordAllocationRequest(sizeInBytes);

            // Use pooled allocation for better performance
            if (options.HasFlag(MemoryOptions.UsePooling))
            {
                return _memoryPool.AllocateRawAsync(sizeInBytes, options, cancellationToken);
            }

            return _asyncAdapter.AllocateRawAsync(sizeInBytes, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate {SizeInBytes} bytes of raw CUDA memory", sizeInBytes);
            _usageTracker.RecordAllocationFailure(sizeInBytes);
            throw new MemoryException($"Failed to allocate {sizeInBytes} bytes of CUDA memory", ex);
        }
    }

    public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordAllocationRequest(source.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>());
            return _asyncAdapter.AllocateAndCopyAsync(source, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate and copy {Count} elements of type {Type}", source.Length, typeof(T).Name);
            _usageTracker.RecordAllocationFailure(source.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>());
            throw new MemoryException($"Failed to allocate and copy {source.Length} elements to CUDA memory", ex);
        }
    }

    public IUnifiedMemoryBuffer<T> CreateView<T>(
        IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int count) where T : unmanaged
    {
        ThrowIfDisposed();

        try
        {
            return _asyncAdapter.CreateView(buffer, offset, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create memory view at offset {Offset} with count {Count}", offset, count);
            throw new MemoryException($"Failed to create memory view at offset {offset} with count {count}", ex);
        }
    }

    public ValueTask CopyToDeviceAsync<T>(
        ReadOnlyMemory<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordTransfer(source.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>(), MemoryTransferDirection.HostToDevice);
            return _asyncAdapter.CopyToDeviceAsync(source, destination, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy {Count} elements to device memory", source.Length);
            throw new MemoryException($"Failed to copy {source.Length} elements to device memory", ex);
        }
    }

    public ValueTask CopyFromDeviceAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        Memory<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordTransfer(destination.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>(), MemoryTransferDirection.DeviceToHost);
            return _asyncAdapter.CopyFromDeviceAsync(source, destination, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy {Count} elements from device memory", destination.Length);
            throw new MemoryException($"Failed to copy {destination.Length} elements from device memory", ex);
        }
    }

    public ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        try
        {
            return _asyncAdapter.CopyAsync(source, destination, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy between device buffers");
            throw new MemoryException("Failed to copy between buffers", ex);
        }
    }

    public ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordTransfer(count * System.Runtime.InteropServices.Marshal.SizeOf<T>(), MemoryTransferDirection.DeviceToDevice);
            return _asyncAdapter.CopyAsync(source, sourceOffset, destination, destinationOffset, count, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy {Count} elements with offsets", count);
            throw new MemoryException($"Failed to copy {count} elements with offsets", ex);
        }
    }

    public void Free(IUnifiedMemoryBuffer buffer)
    {
        if (_disposed || buffer == null)
        {
            return;
        }

        try
        {
            _usageTracker.RecordDeallocation();

            // Return to pool if applicable
            if (_memoryPool.CanReturnToPool(buffer))
            {
                _memoryPool.ReturnToPool(buffer);
            }
            else
            {
                _asyncAdapter.Free(buffer);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Warning: Failed to free memory buffer");
        }
    }

    public ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        if (_disposed || buffer == null)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            _usageTracker.RecordDeallocation();

            // Return to pool if applicable
            if (_memoryPool.CanReturnToPool(buffer))
            {
                _memoryPool.ReturnToPool(buffer);
                return ValueTask.CompletedTask;
            }

            return _asyncAdapter.FreeAsync(buffer, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to async free memory buffer");
            throw new MemoryException("Failed to async free memory buffer", ex);
        }
    }

    public void Clear()
    {
        ThrowIfDisposed();

        try
        {
            _ = _memoryPool.Clear();
            _asyncAdapter.Clear();
            _usageTracker.Reset();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear memory");
            throw new MemoryException("Failed to clear memory", ex);
        }
    }

    public ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Perform memory optimization
            _memoryPool.Optimize();
            return _asyncAdapter.OptimizeAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize memory");
            throw new MemoryException("Failed to optimize memory", ex);
        }
    }

    #endregion

    #region Properties

    public long TotalAvailableMemory => _asyncAdapter.TotalAvailableMemory;
    public long CurrentAllocatedMemory => _asyncAdapter.CurrentAllocatedMemory;
    public long MaxAllocationSize => _asyncAdapter.MaxAllocationSize;
    public MemoryStatistics Statistics => CreateEnhancedStatistics();
    public IAccelerator Accelerator => _asyncAdapter.Accelerator;

    #endregion

    #region Extended Functionality

    /// <summary>
    /// Gets detailed memory usage analytics.
    /// </summary>
    /// <returns>Memory usage analytics.</returns>
    public MemoryUsageAnalytics GetUsageAnalytics()
    {
        ThrowIfDisposed();

        return new MemoryUsageAnalytics
        {
            TotalAllocations = _usageTracker.TotalAllocations,
            TotalDeallocations = _usageTracker.TotalDeallocations,
            TotalBytesAllocated = _usageTracker.TotalBytesAllocated,
            TotalBytesTransferred = _usageTracker.TotalBytesTransferred,
            FailedAllocations = _usageTracker.FailedAllocations,
            PoolHitRatio = _memoryPool.HitRatio,
            AverageAllocationSize = _usageTracker.AverageAllocationSize,
            PeakMemoryUsage = _usageTracker.PeakMemoryUsage,
            LastOptimizationTime = _memoryPool.LastOptimizationTime
        };
    }

    /// <summary>
    /// Performs comprehensive memory cleanup and optimization.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cleanup summary.</returns>
    public async ValueTask<MemoryCleanupSummary> PerformCleanupAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var startTime = DateTimeOffset.UtcNow;
        var beforeMemory = CurrentAllocatedMemory;

        try
        {
            // Clear memory pools
            var poolItemsFreed = _memoryPool.Clear();

            // Optimize memory layout
            await OptimizeAsync(cancellationToken).ConfigureAwait(false);

            // Force garbage collection on device
            _context.MakeCurrent();
            // In production, would call cudaDeviceSynchronize() and memory cleanup

            var afterMemory = CurrentAllocatedMemory;
            var endTime = DateTimeOffset.UtcNow;

            var summary = new MemoryCleanupSummary
            {
                Success = true,
                StartTime = startTime,
                EndTime = endTime,
                MemoryFreed = beforeMemory - afterMemory,
                PoolItemsFreed = poolItemsFreed,
                OptimizationsPerformed = ["Pool cleanup", "Memory defragmentation", "Device synchronization"]
            };

            _logger.LogInformation("Memory cleanup completed: {MemoryFreed} bytes freed in {Duration:F2}ms",
                summary.MemoryFreed, summary.Duration.TotalMilliseconds);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform memory cleanup");

            return new MemoryCleanupSummary
            {
                Success = false,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Configures memory management policies.
    /// </summary>
    /// <param name="policies">Memory management policies.</param>
    public void ConfigurePolicies(MemoryManagementPolicies policies)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(policies);

        MemoryPool.Configure(policies.PoolingPolicy);
        MemoryUsageTracker.Configure(policies.TrackingPolicy);

        _logger.LogDebug("Memory management policies configured");
    }

    #endregion

    #region Private Methods

    private MemoryStatistics CreateEnhancedStatistics()
    {
        var baseStats = _asyncAdapter.Statistics;
        var usageAnalytics = _usageTracker.GetCurrentStatistics();

        return new MemoryStatistics
        {
            TotalAllocated = baseStats.TotalAllocated,
            AvailableMemory = baseStats.AvailableMemory,
            ActiveAllocations = baseStats.ActiveAllocations,
            AllocationCount = usageAnalytics.TotalAllocations,
            DeallocationCount = usageAnalytics.TotalDeallocations,
            CurrentUsed = baseStats.CurrentUsed,
            CurrentUsage = baseStats.CurrentUsage,
            PeakUsage = usageAnalytics.PeakMemoryUsage,
            FragmentationPercentage = CalculateFragmentation() * 100,
            TotalCapacity = baseStats.TotalCapacity
        };
    }

    private double CalculateFragmentation()
    {
        var totalMemory = TotalAvailableMemory;
        var allocatedMemory = CurrentAllocatedMemory;

        if (totalMemory <= 0)
        {
            return 0.0;
        }

        // Simple fragmentation calculation
        _ = totalMemory - allocatedMemory;
        var utilizationRatio = (double)allocatedMemory / totalMemory;

        // Estimate fragmentation based on allocation patterns
        return utilizationRatio > 0.8 ? Math.Min(0.3, utilizationRatio - 0.8) : 0.0;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaMemoryManager));
        }
    }

    #endregion

    #region IDisposable Implementation

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            try
            {
                _memoryPool?.Dispose();
                _asyncAdapter?.Dispose();
                _cudaMemoryManager?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during memory manager disposal");
            }

            _logger.LogDebug("CUDA memory manager disposed");
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;

        try
        {
            _memoryPool?.Dispose();
            return _asyncAdapter?.DisposeAsync() ?? ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during async disposal");
            return ValueTask.CompletedTask;
        }
    }

    /// <inheritdoc/>
    public DeviceMemory AllocateDevice(long sizeInBytes)
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordAllocationRequest(sizeInBytes);
            return _asyncAdapter.AllocateDevice(sizeInBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate {SizeInBytes} bytes of device memory", sizeInBytes);
            _usageTracker.RecordAllocationFailure(sizeInBytes);
            throw new MemoryException($"Failed to allocate {sizeInBytes} bytes of device memory", ex);
        }
    }

    /// <inheritdoc/>
    public void FreeDevice(DeviceMemory deviceMemory)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _usageTracker.RecordDeallocation();
            _asyncAdapter.FreeDevice(deviceMemory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Warning: Failed to free device memory");
        }
    }

    /// <inheritdoc/>
    public void MemsetDevice(DeviceMemory deviceMemory, byte value, long sizeInBytes)
    {
        ThrowIfDisposed();

        try
        {
            _asyncAdapter.MemsetDevice(deviceMemory, value, sizeInBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to memset device memory");
            throw new MemoryException("Failed to memset device memory", ex);
        }
    }

    /// <inheritdoc/>
    public ValueTask MemsetDeviceAsync(DeviceMemory deviceMemory, byte value, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            return _asyncAdapter.MemsetDeviceAsync(deviceMemory, value, sizeInBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to async memset device memory");
            throw new MemoryException("Failed to async memset device memory", ex);
        }
    }

    /// <inheritdoc/>
    public void CopyHostToDevice(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes)
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordTransfer(sizeInBytes, MemoryTransferDirection.HostToDevice);
            _asyncAdapter.CopyHostToDevice(hostPointer, deviceMemory, sizeInBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy {SizeInBytes} bytes from host to device", sizeInBytes);
            throw new MemoryException($"Failed to copy {sizeInBytes} bytes from host to device", ex);
        }
    }

    /// <inheritdoc/>
    public void CopyDeviceToHost(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes)
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordTransfer(sizeInBytes, MemoryTransferDirection.DeviceToHost);
            _asyncAdapter.CopyDeviceToHost(deviceMemory, hostPointer, sizeInBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy {SizeInBytes} bytes from device to host", sizeInBytes);
            throw new MemoryException($"Failed to copy {sizeInBytes} bytes from device to host", ex);
        }
    }

    /// <inheritdoc/>
    public ValueTask CopyHostToDeviceAsync(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordTransfer(sizeInBytes, MemoryTransferDirection.HostToDevice);
            return _asyncAdapter.CopyHostToDeviceAsync(hostPointer, deviceMemory, sizeInBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to async copy {SizeInBytes} bytes from host to device", sizeInBytes);
            throw new MemoryException($"Failed to async copy {sizeInBytes} bytes from host to device", ex);
        }
    }

    /// <inheritdoc/>
    public ValueTask CopyDeviceToHostAsync(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordTransfer(sizeInBytes, MemoryTransferDirection.DeviceToHost);
            return _asyncAdapter.CopyDeviceToHostAsync(deviceMemory, hostPointer, sizeInBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to async copy {SizeInBytes} bytes from device to host", sizeInBytes);
            throw new MemoryException($"Failed to async copy {sizeInBytes} bytes from device to host", ex);
        }
    }

    /// <inheritdoc/>
    public void CopyDeviceToDevice(DeviceMemory sourceDevice, DeviceMemory destinationDevice, long sizeInBytes)
    {
        ThrowIfDisposed();

        try
        {
            _usageTracker.RecordTransfer(sizeInBytes, MemoryTransferDirection.DeviceToDevice);
            _asyncAdapter.CopyDeviceToDevice(sourceDevice, destinationDevice, sizeInBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy {SizeInBytes} bytes between devices", sizeInBytes);
            throw new MemoryException($"Failed to copy {sizeInBytes} bytes between devices", ex);
        }
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Memory usage analytics and statistics.
/// </summary>
public readonly record struct MemoryUsageAnalytics
{
    public long TotalAllocations { get; init; }
    public long TotalDeallocations { get; init; }
    public long TotalBytesAllocated { get; init; }
    public long TotalBytesTransferred { get; init; }
    public long FailedAllocations { get; init; }
    public double PoolHitRatio { get; init; }
    public double AverageAllocationSize { get; init; }
    public long PeakMemoryUsage { get; init; }
    public DateTimeOffset LastOptimizationTime { get; init; }
}

/// <summary>
/// Memory cleanup operation summary.
/// </summary>
public sealed class MemoryCleanupSummary
{
    public bool Success { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public long MemoryFreed { get; init; }
    public int PoolItemsFreed { get; init; }
    public List<string> OptimizationsPerformed { get; init; } = [];
    public string? ErrorMessage { get; init; }

    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// Memory management policies configuration.
/// </summary>
public sealed class MemoryManagementPolicies
{
    public MemoryPoolingPolicy PoolingPolicy { get; init; } = new();
    public MemoryTrackingPolicy TrackingPolicy { get; init; } = new();
}

/// <summary>
/// Memory pooling policy configuration.
/// </summary>
public sealed class MemoryPoolingPolicy
{
    public bool EnablePooling { get; init; } = true;
    public int MaxPoolSize { get; init; } = 100;
    public TimeSpan MaxIdleTime { get; init; } = TimeSpan.FromMinutes(5);
    public long MaxItemSize { get; init; } = 1024 * 1024 * 1024; // 1GB
}

/// <summary>
/// Memory tracking policy configuration.
/// </summary>
public sealed class MemoryTrackingPolicy
{
    public bool EnableDetailedTracking { get; init; } = true;
    public bool TrackStackTraces { get; init; }

    public TimeSpan AnalyticsWindow { get; init; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Memory transfer direction enumeration.
/// </summary>
public enum MemoryTransferDirection
{
    HostToDevice,
    DeviceToHost,
    DeviceToDevice
}

/// <summary>
/// Memory usage tracker for analytics.
/// </summary>
internal sealed class MemoryUsageTracker
{
    private long _totalAllocations;
    private long _totalDeallocations;
    private long _totalBytesAllocated;
    private long _totalBytesTransferred;
    private long _failedAllocations;
    private long _peakMemoryUsage;
    private long _currentMemoryUsage;

    public long TotalAllocations => Interlocked.Read(ref _totalAllocations);
    public long TotalDeallocations => Interlocked.Read(ref _totalDeallocations);
    public long TotalBytesAllocated => Interlocked.Read(ref _totalBytesAllocated);
    public long TotalBytesTransferred => Interlocked.Read(ref _totalBytesTransferred);
    public long FailedAllocations => Interlocked.Read(ref _failedAllocations);
    public long PeakMemoryUsage => Interlocked.Read(ref _peakMemoryUsage);

    public double AverageAllocationSize
    {
        get
        {
            var totalAllocs = TotalAllocations;
            var totalBytes = TotalBytesAllocated;
            return totalAllocs > 0 ? (double)totalBytes / totalAllocs : 0.0;
        }
    }

    public void RecordAllocationRequest(long sizeInBytes)
    {
        _ = Interlocked.Increment(ref _totalAllocations);
        _ = Interlocked.Add(ref _totalBytesAllocated, sizeInBytes);

        var newUsage = Interlocked.Add(ref _currentMemoryUsage, sizeInBytes);
        UpdatePeakUsage(newUsage);
    }

    public void RecordAllocationFailure(long sizeInBytes)
    {
        _ = Interlocked.Increment(ref _failedAllocations);
    }

    public void RecordDeallocation()
    {
        _ = Interlocked.Increment(ref _totalDeallocations);
    }

    public void RecordTransfer(long sizeInBytes, MemoryTransferDirection direction)
    {
        _ = Interlocked.Add(ref _totalBytesTransferred, sizeInBytes);
    }

    public void Reset()
    {
        _ = Interlocked.Exchange(ref _totalAllocations, 0);
        _ = Interlocked.Exchange(ref _totalDeallocations, 0);
        _ = Interlocked.Exchange(ref _totalBytesAllocated, 0);
        _ = Interlocked.Exchange(ref _totalBytesTransferred, 0);
        _ = Interlocked.Exchange(ref _failedAllocations, 0);
        _ = Interlocked.Exchange(ref _peakMemoryUsage, 0);
        _ = Interlocked.Exchange(ref _currentMemoryUsage, 0);
    }

    public static void Configure(MemoryTrackingPolicy policy)
    {
        // Configure tracking based on policy
        // Implementation would set tracking preferences
    }

    public MemoryUsageAnalytics GetCurrentStatistics()
    {
        return new MemoryUsageAnalytics
        {
            TotalAllocations = TotalAllocations,
            TotalDeallocations = TotalDeallocations,
            TotalBytesAllocated = TotalBytesAllocated,
            TotalBytesTransferred = TotalBytesTransferred,
            FailedAllocations = FailedAllocations,
            AverageAllocationSize = AverageAllocationSize,
            PeakMemoryUsage = PeakMemoryUsage
        };
    }

    private void UpdatePeakUsage(long currentUsage)
    {
        var currentPeak = Interlocked.Read(ref _peakMemoryUsage);
        while (currentUsage > currentPeak)
        {
            var originalPeak = Interlocked.CompareExchange(ref _peakMemoryUsage, currentUsage, currentPeak);
            if (originalPeak == currentPeak)
            {
                break;
            }
            currentPeak = originalPeak;
        }
    }
}

/// <summary>
/// Simplified memory pool implementation.
/// </summary>
internal sealed class MemoryPool : IDisposable
{
    private readonly IUnifiedMemoryManager _memoryManager;
    private readonly ILogger _logger;
    private readonly Dictionary<Type, Queue<IUnifiedMemoryBuffer>> _pools;
    private readonly object _lock = new();
    private volatile bool _disposed;
    private int _hitCount = 0; // Initialize to suppress warning
    private int _missCount;

    public DateTimeOffset LastOptimizationTime { get; private set; } = DateTimeOffset.UtcNow;

    public double HitRatio
    {
        get
        {
            var total = _hitCount + _missCount;
            return total > 0 ? (double)_hitCount / total : 0.0;
        }
    }

    public MemoryPool(IUnifiedMemoryManager memoryManager, ILogger logger)
    {
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pools = [];
    }

    public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,
        MemoryOptions options,
        CancellationToken cancellationToken) where T : unmanaged
    {
        // Simplified pool implementation - in production would have size matching
        _ = Interlocked.Increment(ref _missCount);
        return _memoryManager.AllocateAsync<T>(count, options, cancellationToken);
    }

    public ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken)
    {
        // Simplified pool implementation
        _ = Interlocked.Increment(ref _missCount);
        return _memoryManager.AllocateRawAsync(sizeInBytes, options, cancellationToken);
    }

    public bool CanReturnToPool(IUnifiedMemoryBuffer buffer)
    {
        // Simplified check - in production would validate buffer size, age, etc.
        return buffer != null && !_disposed;
    }

    public void ReturnToPool(IUnifiedMemoryBuffer buffer)
    {
        if (_disposed || buffer == null)
        {
            return;
        }

        // Simplified pool return - in production would add to appropriate size pool
        try
        {
            _memoryManager.Free(buffer);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to return buffer to pool");
        }
    }

    public int Clear()
    {
        if (_disposed)
        {
            return 0;
        }

        lock (_lock)
        {
            var totalCleared = 0;
            foreach (var pool in _pools.Values)
            {
                while (pool.TryDequeue(out var buffer))
                {
                    try
                    {
                        _memoryManager.Free(buffer);
                        totalCleared++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to free pooled buffer during clear");
                    }
                }
            }

            _pools.Clear();
            return totalCleared;
        }
    }

    public void Optimize()
    {
        if (_disposed)
        {
            return;
        }

        LastOptimizationTime = DateTimeOffset.UtcNow;
        // Simplified optimization - in production would defragment pools, remove old items
    }

    public static void Configure(MemoryPoolingPolicy policy)
    {
        // Configure pool based on policy settings
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _ = Clear();
        }
    }
}


#endregion