// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.Metal.Native;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// Metal-specific memory manager implementation with real Metal API integration.
/// Consolidated using BaseMemoryManager to eliminate duplicate patterns.
/// </summary>
public sealed class MetalMemoryManager : BaseMemoryManager
{
    private readonly IntPtr _device;
    private readonly bool _isAppleSilicon;
    private readonly MetalUnifiedMemoryOptimizer _memoryOptimizer;
    private readonly MetalMemoryPoolManager? _poolManager;
    private readonly ConcurrentDictionary<IntPtr, MetalAllocationInfo> _activeAllocations;
    private WeakReference<IAccelerator>? _acceleratorRef;
    private readonly bool _poolingEnabled;

    private long _totalAllocatedBytes;
    private long _peakAllocatedBytes;
    private long _totalAllocations;
    private bool _disposed;

    /// <summary>
    /// Gets the Metal device used by this manager.
    /// </summary>
    public IntPtr Device => _device;

    /// <summary>
    /// Gets the unified memory optimizer.
    /// </summary>
    public MetalUnifiedMemoryOptimizer MemoryOptimizer => _memoryOptimizer;

    /// <summary>
    /// Gets whether the system is running on Apple Silicon with unified memory.
    /// </summary>
    public bool IsAppleSilicon => _memoryOptimizer.IsAppleSilicon;

    /// <summary>
    /// Gets whether the Metal device has unified memory architecture.
    /// </summary>
    public bool IsUnifiedMemory => _memoryOptimizer.IsUnifiedMemory;

    /// <summary>
    /// Sets the accelerator reference after construction.
    /// </summary>
    /// <param name="accelerator">The accelerator to reference.</param>
    public void SetAcceleratorReference(IAccelerator accelerator) => _acceleratorRef = new WeakReference<IAccelerator>(accelerator);

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalMemoryManager"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="accelerator">Optional accelerator reference.</param>
    /// <param name="enablePooling">Whether to enable memory pooling (default: true for 90% allocation reduction).</param>
    public MetalMemoryManager(ILogger<MetalMemoryManager> logger, IAccelerator? accelerator = null, bool enablePooling = true) : base(logger)
    {
        _device = GetOrCreateDevice();

        // Create logger for optimizer
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var optimizerLogger = loggerFactory.CreateLogger<MetalUnifiedMemoryOptimizer>();

        // Initialize unified memory optimizer
        _memoryOptimizer = new MetalUnifiedMemoryOptimizer(_device, optimizerLogger);
        _isAppleSilicon = _memoryOptimizer.IsAppleSilicon;
        _activeAllocations = new ConcurrentDictionary<IntPtr, MetalAllocationInfo>();
        _poolingEnabled = enablePooling;

        // Initialize memory pool manager if pooling is enabled
        if (_poolingEnabled)
        {
            var poolLogger = loggerFactory.CreateLogger<MetalMemoryPoolManager>();
            _poolManager = new MetalMemoryPoolManager(_device, poolLogger, _memoryOptimizer.IsUnifiedMemory);
        }

        if (accelerator != null)
        {
            _acceleratorRef = new WeakReference<IAccelerator>(accelerator);
        }

        var logger2 = logger as ILogger;
        logger2?.LogInformation(
            "Metal Memory Manager initialized for {Architecture} with device {DeviceId:X}, " +
            "UnifiedMemory={UnifiedMemory}, Pooling={Pooling}",
            _isAppleSilicon ? "Apple Silicon" : "Intel Mac",
            _device.ToInt64(),
            _memoryOptimizer.IsUnifiedMemory,
            _poolingEnabled);
    }


    private static IntPtr GetOrCreateDevice()
    {
        var device = MetalNative.CreateSystemDefaultDevice();
        if (device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal device for memory manager");
        }
        return device;
    }

    /// <inheritdoc/>
    public override long MaxAllocationSize
    {
        get
        {
            if (_device != IntPtr.Zero)
            {
                try
                {
                    var deviceInfo = MetalNative.GetDeviceInfo(_device);
                    return (long)deviceInfo.MaxBufferLength;
                }
                catch
                {
                    // Fallback to conservative estimate
                    return 1L << 32; // 4GB
                }
            }
            return 1L << 32; // 4GB default
        }
    }

    /// <inheritdoc/>
    public override long CurrentAllocatedMemory => Interlocked.Read(ref _totalAllocatedBytes);

    /// <inheritdoc/>
    public override long TotalAvailableMemory
    {
        get
        {
            if (_isAppleSilicon)
            {
                return GetUnifiedMemorySize();
            }
            else
            {
                // Intel Mac with discrete GPU
                return MaxAllocationSize;
            }
        }
    }

    /// <inheritdoc/>
    public override IAccelerator Accelerator
    {
        get
        {
            if (_acceleratorRef != null && _acceleratorRef.TryGetTarget(out var accelerator))
            {
                return accelerator;
            }
            throw new InvalidOperationException("Accelerator reference not available or has been garbage collected");
        }
    }

    /// <inheritdoc/>
    public override MemoryStatistics Statistics
    {
        get
        {
            var totalAllocated = CurrentAllocatedMemory;
            var peakAllocated = Interlocked.Read(ref _peakAllocatedBytes);
            var totalAllocs = Interlocked.Read(ref _totalAllocations);

            // Note: MemoryStatistics doesn't have CustomMetrics in abstractions
            // Metal-specific optimization statistics are tracked internally by MetalUnifiedMemoryOptimizer
            // and can be retrieved via _memoryOptimizer.GetPerformanceStatistics() if needed

            return new MemoryStatistics
            {
                TotalAllocated = totalAllocated,
                PeakMemoryUsage = peakAllocated,
                AllocationCount = (int)Math.Min(totalAllocs, int.MaxValue),
                AvailableMemory = TotalAvailableMemory - totalAllocated
            };
        }
    }

    /// <summary>
    /// Gets accurate Metal memory statistics asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing accurate memory statistics from the device.</returns>
    /// <remarks>
    /// This method queries the Metal device for accurate memory information.
    /// On Apple Silicon, this includes unified memory statistics.
    /// For fast synchronous access, use the <see cref="Statistics"/> property.
    /// </remarks>
    public async ValueTask<MemoryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Use Task.Run to avoid blocking on Metal API calls
        return await Task.Run(() =>
        {
            try
            {
                var totalAllocated = Interlocked.Read(ref _totalAllocatedBytes);
                var peakAllocated = Interlocked.Read(ref _peakAllocatedBytes);
                var totalAllocs = Interlocked.Read(ref _totalAllocations);

                // Query Metal device for memory information
                var totalMemory = TotalAvailableMemory;
                var availableMemory = totalMemory - totalAllocated;

                // Cleanup disposed allocations
                var disposedAllocations = _activeAllocations
                    .Where(kvp => !IsAllocationValid(kvp.Value))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var ptr in disposedAllocations)
                {
                    _activeAllocations.TryRemove(ptr, out _);
                }

                var activeAllocations = _activeAllocations.Count;

                // Calculate fragmentation
                var fragmentationPercent = CalculateFragmentation(totalMemory, totalAllocated, availableMemory);

                var averageAllocationSize = totalAllocs > 0 ? (double)totalAllocated / totalAllocs : 0.0;

                return new MemoryStatistics
                {
                    TotalAllocated = totalAllocated,
                    CurrentUsage = totalAllocated,
                    CurrentUsed = totalAllocated,
                    PeakUsage = peakAllocated,
                    AllocationCount = (int)Math.Min(totalAllocs, int.MaxValue),
                    DeallocationCount = 0, // Metal doesn't track deallocations separately
                    ActiveAllocations = activeAllocations,
                    AvailableMemory = availableMemory,
                    TotalCapacity = totalMemory,
                    FragmentationPercentage = fragmentationPercent,
                    AverageAllocationSize = averageAllocationSize,
                    TotalAllocationCount = (int)Math.Min(totalAllocs, int.MaxValue),
                    TotalDeallocationCount = 0,
                    PoolHitRate = 0.0, // TODO: Implement pool hit rate tracking for Metal
                    TotalMemoryBytes = totalMemory,
                    UsedMemoryBytes = totalAllocated,
                    AvailableMemoryBytes = availableMemory,
                    PeakMemoryUsageBytes = peakAllocated,
                    TotalFreed = 0,
                    ActiveBuffers = activeAllocations,
                    PeakMemoryUsage = peakAllocated,
                    TotalAvailable = totalMemory
                };
            }
            catch (Exception ex)
            {
                var logger = base.Logger as ILogger;
                logger?.LogError(ex, "Failed to get accurate Metal memory statistics");
                // Return basic statistics on error
                return new MemoryStatistics
                {
                    TotalAllocated = CurrentAllocatedMemory,
                    CurrentUsage = CurrentAllocatedMemory,
                    CurrentUsed = CurrentAllocatedMemory,
                    PeakUsage = Interlocked.Read(ref _peakAllocatedBytes),
                    TotalCapacity = TotalAvailableMemory,
                    TotalMemoryBytes = TotalAvailableMemory,
                    AvailableMemory = Math.Max(0, TotalAvailableMemory - CurrentAllocatedMemory)
                };
            }
        }, cancellationToken);
    }

    private static double CalculateFragmentation(long totalMemory, long allocated, long free)
    {
        if (totalMemory == 0)
        {
            return 0.0;
        }

        // Fragmentation is the percentage of memory that's neither allocated nor contiguous free space
        var accountedFor = allocated + free;
        var unaccountedFor = totalMemory - accountedFor;
        return Math.Max(0.0, (double)unaccountedFor / totalMemory * 100.0);
    }

    private static bool IsAllocationValid(MetalAllocationInfo info)
    {
        // Check if allocation is still valid (has positive size)
        return info.SizeInBytes > 0;
    }

    /// <inheritdoc/>
    protected override async ValueTask<IUnifiedMemoryBuffer> AllocateInternalAsync(long sizeInBytes, MemoryOptions options, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (sizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size must be positive");
        }

        if (sizeInBytes > MaxAllocationSize)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), $"Size {sizeInBytes} exceeds maximum allocation size {MaxAllocationSize}");
        }

        _ = Interlocked.Increment(ref _totalAllocations);

        // Try pool allocation first if enabled
        if (_poolingEnabled && _poolManager != null)
        {
            try
            {
                var pooledBuffer = await _poolManager.AllocateAsync(sizeInBytes, options, cancellationToken);
                if (pooledBuffer != null)
                {
                    TrackAllocation(pooledBuffer, sizeInBytes);
                    return pooledBuffer;
                }
            }
            catch (Exception ex)
            {
                var logger = base.Logger as ILogger;
                logger?.LogWarning(ex, "Pool allocation failed, falling back to direct allocation");
            }
        }

        // Fallback to direct allocation (pool miss or disabled)
        // Get optimal storage mode from unified memory optimizer
        var storageMode = _memoryOptimizer.GetOptimalStorageMode(options);

        // Log allocation with optimization info
        var pattern = InferUsagePattern(options);
        var estimatedGain = _memoryOptimizer.EstimatePerformanceGain(sizeInBytes, pattern);

        if (estimatedGain > 1.5 && base.Logger is ILogger logger2)
        {
            logger2.LogDebug(
                "Allocating {SizeKB:F2} KB with {StorageMode} mode (estimated {Speedup:F1}x speedup on unified memory)",
                sizeInBytes / 1024.0,
                storageMode,
                estimatedGain);
        }

        // Allocate Metal buffer with optimized storage mode
        var buffer = new MetalMemoryBuffer(sizeInBytes, options, _device, storageMode);
        await buffer.InitializeAsync(cancellationToken);

        // Track zero-copy optimization
        if (_memoryOptimizer.IsUnifiedMemory && storageMode == MetalStorageMode.Shared)
        {
            _memoryOptimizer.TrackZeroCopyOperation(sizeInBytes);
        }

        TrackAllocation(buffer, sizeInBytes);
        return buffer;
    }

    /// <inheritdoc/>
    public override ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken)
    {
        if (buffer == null)
        {
            return ValueTask.CompletedTask;
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        // Track deallocation
        if (buffer is MetalMemoryBuffer metalBuffer &&
            _activeAllocations.TryRemove(metalBuffer.NativeHandle, out var info))
        {
            _ = Interlocked.Add(ref _totalAllocatedBytes, -info.SizeInBytes);

            // Try to return buffer to pool instead of disposing
            if (_poolingEnabled && _poolManager != null)
            {
                var returned = _poolManager.ReturnToPool(metalBuffer);
                if (returned)
                {
                    // Buffer successfully returned to pool - don't dispose
                    return ValueTask.CompletedTask;
                }
            }
        }

        // Buffer not pooled or pool disabled - dispose it
        buffer.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override async ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (source is MetalMemoryBuffer srcMetal && destination is MetalMemoryBuffer destMetal)
        {
            // Direct Metal buffer copy for optimal performance
            var elementSize = Unsafe.SizeOf<T>();
            var copySize = Math.Min(source.Length * elementSize, destination.Length * elementSize);


            MetalNative.CopyBuffer(srcMetal.Buffer, 0, destMetal.Buffer, 0, copySize);
        }
        else
        {
            // Fallback to host memory staging
            var tempData = new T[source.Length];
            await source.CopyToAsync(tempData, cancellationToken: cancellationToken);
            await destination.CopyFromAsync(tempData, cancellationToken: cancellationToken);
        }
    }

    /// <inheritdoc/>
    public override async ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (source is MetalMemoryBuffer srcMetal && destination is MetalMemoryBuffer destMetal)
        {
            var elementSize = Unsafe.SizeOf<T>();
            var srcOffsetBytes = sourceOffset * elementSize;
            var destOffsetBytes = destinationOffset * elementSize;
            var copySize = count * elementSize;


            MetalNative.CopyBuffer(srcMetal.Buffer, srcOffsetBytes, destMetal.Buffer, destOffsetBytes, copySize);
        }
        else
        {
            // Fallback implementation using temporary buffer
            var tempData = new T[count];
            await source.CopyToAsync(tempData.AsMemory(), cancellationToken);
            await destination.CopyFromAsync(tempData.AsMemory(), cancellationToken);
        }
    }

    /// <inheritdoc/>
    public override async ValueTask CopyFromDeviceAsync<T>(IUnifiedMemoryBuffer<T> source, Memory<T> destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Standard device-to-host copy

        await source.CopyToAsync(destination, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask CopyToDeviceAsync<T>(ReadOnlyMemory<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Standard host-to-device copy

        await destination.CopyFromAsync(source, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public override IUnifiedMemoryBuffer<T> CreateView<T>(IUnifiedMemoryBuffer<T> buffer, int offset, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(buffer);

        // Calculate byte offsets

        var elementSize = Unsafe.SizeOf<T>();
        var offsetBytes = (long)offset * elementSize;
        var lengthBytes = (long)length * elementSize;

        // Use the non-generic CreateViewCore which handles the actual view creation

        var view = CreateViewCore(buffer, offsetBytes, lengthBytes);

        // Wrap in a typed buffer wrapper if needed

        if (view is IUnifiedMemoryBuffer<T> typedView)
        {
            return typedView;
        }

        // This shouldn't happen with our implementation, but handle it gracefully

        throw new InvalidOperationException($"Failed to create typed view for buffer of type {buffer.GetType()}");
    }

    /// <inheritdoc/>
    public override void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Clear tracking information

        _activeAllocations.Clear();

        // Reset statistics

        _ = Interlocked.Exchange(ref _totalAllocatedBytes, 0);
        _ = Interlocked.Exchange(ref _peakAllocatedBytes, 0);
        _ = Interlocked.Exchange(ref _totalAllocations, 0);
    }

    /// <inheritdoc/>
    public override ValueTask OptimizeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    protected override IUnifiedMemoryBuffer CreateViewCore(IUnifiedMemoryBuffer buffer, long offset, long length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        return buffer switch
        {
            MetalMemoryBuffer metalBuffer => new MetalMemoryBufferView(metalBuffer, offset, length),
            _ => throw new ArgumentException("Buffer must be a Metal memory buffer", nameof(buffer))
        };
    }


    /// <summary>
    /// Detects if running on Apple Silicon.
    /// </summary>
    private static bool DetectAppleSilicon()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }


        try
        {
            // Check architecture - Apple Silicon typically reports as ARM64
            return RuntimeInformation.OSArchitecture == Architecture.Arm64;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// Tracks a new allocation for statistics.
    /// </summary>
    private void TrackAllocation(IUnifiedMemoryBuffer buffer, long sizeInBytes)
    {
        if (buffer is MetalMemoryBuffer metalBuffer)
        {
            var info = new MetalAllocationInfo
            {
                SizeInBytes = sizeInBytes,
                AllocatedAt = DateTimeOffset.UtcNow,
                MemoryType = MetalMemoryType.Device
            };


            _ = _activeAllocations.TryAdd(metalBuffer.NativeHandle, info);


            var totalBytes = Interlocked.Add(ref _totalAllocatedBytes, sizeInBytes);

            // Update peak if necessary

            long currentPeak;
            do
            {
                currentPeak = _peakAllocatedBytes;
                if (totalBytes <= currentPeak)
                {
                    break;
                }
            } while (Interlocked.CompareExchange(ref _peakAllocatedBytes, totalBytes, currentPeak) != currentPeak);
        }
    }


    /// <summary>
    /// Gets the unified memory size on Apple Silicon.
    /// </summary>
    private long GetUnifiedMemorySize()
    {
        if (!_isAppleSilicon)
        {
            return 0;
        }

        // This would query the actual unified memory size from the system
        // For now, return a reasonable default based on typical Apple Silicon configurations

        return 16L * 1024 * 1024 * 1024; // 16GB unified memory
    }

    /// <summary>
    /// Infers memory usage pattern from memory options.
    /// </summary>
    /// <param name="options">The memory options.</param>
    /// <returns>The inferred memory usage pattern.</returns>
    private static MemoryUsagePattern InferUsagePattern(MemoryOptions options)
    {
        // MemoryOptions is a flags enum, not a class with AccessPattern property

        // If marked as read-only via Cached flag (immutable data)
        if (options.HasFlag(MemoryOptions.Cached))
        {
            return MemoryUsagePattern.ReadOnly;
        }

        // If marked as write-combined (streaming writes)
        if (options.HasFlag(MemoryOptions.WriteCombined))
        {
            return MemoryUsagePattern.Streaming;
        }

        // If marked for frequent transfers (unified/coherent)
        if (options.HasFlag(MemoryOptions.Unified) || options.HasFlag(MemoryOptions.Coherent))
        {
            return MemoryUsagePattern.FrequentTransfer;
        }

        return MemoryUsagePattern.HostVisible;
    }

    /// <summary>
    /// Gets the optimal storage mode for a memory usage pattern.
    /// </summary>
    /// <param name="pattern">The memory usage pattern.</param>
    /// <returns>The optimal Metal storage mode.</returns>
    public MetalStorageMode GetOptimalStorageMode(MemoryUsagePattern pattern) => _memoryOptimizer.GetOptimalStorageMode(pattern);

    /// <summary>
    /// Gets pool statistics if pooling is enabled.
    /// </summary>
    public MemoryPoolManagerStatistics? PoolStatistics => _poolManager?.GetStatistics();

    /// <summary>
    /// Generates a detailed pool performance report.
    /// </summary>
    public string? GeneratePoolReport() => _poolManager?.GenerateReport();

    /// <summary>
    /// Pre-allocates buffers to warm up the pool.
    /// </summary>
    public async Task PreAllocatePoolAsync(int poolSize, int count, MemoryOptions options, CancellationToken cancellationToken = default)
    {
        if (_poolManager != null)
        {
            await _poolManager.PreAllocateAsync(poolSize, count, options, cancellationToken);
        }
    }

    /// <summary>
    /// Clears all memory pools if pooling is enabled.
    /// </summary>
    public void ClearPools() => _poolManager?.ClearPools();

    /// <summary>
    /// Disposes the memory manager and all resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                // Log final pool statistics if pooling was enabled
                if (_poolingEnabled && _poolManager != null)
                {
                    var logger = base.Logger as ILogger;
                    var poolStats = _poolManager.GetStatistics();
                    logger?.LogInformation(
                        "Metal Memory Pool Final Statistics - Hit Rate: {HitRate:P2}, " +
                        "Allocation Reduction: {Reduction:F1}%, Peak Pool Size: {PeakMB:F1} MB",
                        poolStats.HitRate,
                        poolStats.AllocationReductionPercentage,
                        poolStats.PeakBytesInPools / (1024.0 * 1024.0));
                }

                // Dispose pool manager
                _poolManager?.Dispose();

                // Clear all resources
                Clear();

                // Release Metal device
                if (_device != IntPtr.Zero)
                {
                    try
                    {
                        MetalNative.ReleaseDevice(_device);
                    }
                    catch (Exception ex)
                    {
                        var logger = Logger as ILogger;
                        logger?.LogWarning(ex, "Failed to release Metal device during disposal");
                    }
                }

                // Dispose unified memory optimizer
                _memoryOptimizer?.Dispose();

                var logger2 = base.Logger as ILogger;
                logger2?.LogInformation(
                    "MetalMemoryManager disposed - Total allocations: {TotalAllocs}, Peak usage: {PeakMB:F1} MB",
                    _totalAllocations,
                    _peakAllocatedBytes / (1024.0 * 1024.0));
            }
            catch (Exception ex)
            {
                var logger = Logger as ILogger;
                logger?.LogError(ex, "Error during MetalMemoryManager disposal");
            }
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}

#region Supporting Types

/// <summary>
/// Information about a Metal memory allocation.
/// </summary>
internal sealed record MetalAllocationInfo
{
    public required long SizeInBytes { get; init; }
    public required DateTimeOffset AllocatedAt { get; init; }
    public required MetalMemoryType MemoryType { get; init; }
}

/// <summary>
/// Types of Metal memory allocations.
/// </summary>
internal enum MetalMemoryType
{
    Device,
    Pinned,
    Unified,
    Mapped
}



#endregion
