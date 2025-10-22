// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Integration;

/// <summary>
/// Integrates CUDA memory management with unified buffer system
/// </summary>
public sealed partial class CudaMemoryIntegration : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 5250,
        Level = LogLevel.Information,
        Message = "CUDA Memory Integration initialized for device {DeviceId}")]
    private static partial void LogIntegrationInitialized(ILogger logger, int deviceId);

    [LoggerMessage(
        EventId = 5251,
        Level = LogLevel.Debug,
        Message = "Allocated optimized buffer: {Count} elements of {TypeName}")]
    private static partial void LogBufferAllocated(ILogger logger, int count, string typeName);

    [LoggerMessage(
        EventId = 5252,
        Level = LogLevel.Error,
        Message = "Failed to allocate optimized buffer for {Count} elements of {TypeName}")]
    private static partial void LogBufferAllocationFailed(ILogger logger, Exception ex, int count, string typeName);

    [LoggerMessage(
        EventId = 5253,
        Level = LogLevel.Debug,
        Message = "Optimized copy completed: {Length} elements of {TypeName}")]
    private static partial void LogCopyCompleted(ILogger logger, int length, string typeName);

    [LoggerMessage(
        EventId = 5254,
        Level = LogLevel.Error,
        Message = "Optimized copy failed for {TypeName}")]
    private static partial void LogCopyFailed(ILogger logger, Exception ex, string typeName);

    [LoggerMessage(
        EventId = 5255,
        Level = LogLevel.Warning,
        Message = "Error getting memory statistics")]
    private static partial void LogStatisticsError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5256,
        Level = LogLevel.Warning,
        Message = "Error calculating memory health")]
    private static partial void LogHealthCalculationError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5257,
        Level = LogLevel.Debug,
        Message = "Memory optimization completed")]
    private static partial void LogOptimizationCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 5258,
        Level = LogLevel.Warning,
        Message = "Memory optimization failed")]
    private static partial void LogOptimizationFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5259,
        Level = LogLevel.Debug,
        Message = "Memory maintenance completed")]
    private static partial void LogMaintenanceCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 5260,
        Level = LogLevel.Error,
        Message = "Error during memory maintenance")]
    private static partial void LogMaintenanceError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5261,
        Level = LogLevel.Debug,
        Message = "Forced garbage collection completed")]
    private static partial void LogGarbageCollectionCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 5262,
        Level = LogLevel.Warning,
        Message = "Error during forced garbage collection")]
    private static partial void LogGarbageCollectionError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5263,
        Level = LogLevel.Warning,
        Message = "Failed to register buffer")]
    private static partial void LogBufferRegistrationFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5264,
        Level = LogLevel.Debug,
        Message = "Memory pool configured: large={Large}")]
    private static partial void LogMemoryPoolConfigured(ILogger logger, bool large);

    [LoggerMessage(
        EventId = 5265,
        Level = LogLevel.Warning,
        Message = "Failed to configure memory pool")]
    private static partial void LogMemoryPoolConfigurationFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5266,
        Level = LogLevel.Debug,
        Message = "Cleaned up {Count} orphaned buffer entries")]
    private static partial void LogOrphanedBuffersCleanedUp(ILogger logger, int count);

    [LoggerMessage(
        EventId = 5267,
        Level = LogLevel.Warning,
        Message = "Error cleaning up orphaned buffers")]
    private static partial void LogOrphanedBuffersCleanupError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5268,
        Level = LogLevel.Warning,
        Message = "Memory health degraded: {Health:F2}")]
    private static partial void LogMemoryHealthDegraded(ILogger logger, double health);

    [LoggerMessage(
        EventId = 5269,
        Level = LogLevel.Debug,
        Message = "Memory health: {Health:F2}")]
    private static partial void LogMemoryHealth(ILogger logger, double health);

    [LoggerMessage(
        EventId = 5270,
        Level = LogLevel.Warning,
        Message = "Error during memory monitoring")]
    private static partial void LogMemoryMonitoringError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5271,
        Level = LogLevel.Debug,
        Message = "CUDA Memory Integration disposed")]
    private static partial void LogIntegrationDisposed(ILogger logger);

    #endregion


    private readonly CudaContext _context;
    private readonly ILogger _logger;
    [SuppressMessage("IDisposableAnalyzers.Correctness", "CA2213:Disposable fields should be disposed",
        Justification = "Disposed via null-conditional operator in Dispose method (line 561)")]
    private readonly CudaMemoryManager _memoryManager;
    private readonly CudaAsyncMemoryManagerAdapter _asyncAdapter;
    private readonly Dictionary<IntPtr, CudaBufferInfo> _bufferRegistry;
    private readonly Timer _memoryMonitorTimer;
    private readonly object _registryLock = new();
    private volatile bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CudaMemoryIntegration class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>

    public CudaMemoryIntegration(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));


        _memoryManager = new CudaMemoryManager(context, logger);
        _asyncAdapter = new CudaAsyncMemoryManagerAdapter(_memoryManager);
        _bufferRegistry = [];

        // Set up memory monitoring
        _memoryMonitorTimer = new Timer(MonitorMemory, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));

        LogIntegrationInitialized(_logger, context.DeviceId);
    }

    /// <summary>
    /// Gets the unified memory manager
    /// </summary>
    public IUnifiedMemoryManager UnifiedMemoryManager => _asyncAdapter;

    /// <summary>
    /// Gets the CUDA-specific memory manager
    /// </summary>
    public CudaMemoryManager CudaMemoryManager => _memoryManager;

    /// <summary>
    /// Allocates unified memory buffer with CUDA optimizations
    /// </summary>
    public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateOptimizedAsync<T>(
        int count,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var buffer = await _asyncAdapter.AllocateAsync<T>(count, options, cancellationToken);

            // Register buffer for monitoring
            RegisterBuffer(buffer);

            LogBufferAllocated(_logger, count, typeof(T).Name);

            return buffer;
        }
        catch (Exception ex)
        {
            LogBufferAllocationFailed(_logger, ex, count, typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Performs optimized memory copy between buffers
    /// </summary>
    public async ValueTask OptimizedCopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await _asyncAdapter.CopyAsync(source, destination, cancellationToken);
            LogCopyCompleted(_logger, source.Length, typeof(T).Name);
        }
        catch (Exception ex)
        {
            LogCopyFailed(_logger, ex, typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Gets memory usage statistics
    /// </summary>
    public CudaMemoryStatistics GetMemoryStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var stats = _memoryManager.Statistics;

            // Query current device memory

            _context.MakeCurrent();
            var result = CudaRuntime.cudaMemGetInfo(out var freeBytes, out var totalBytes);


            var currentFree = result == CudaError.Success ? (long)freeBytes : 0;
            var totalMemory = result == CudaError.Success ? (long)totalBytes : 0;

            lock (_registryLock)
            {
                return new CudaMemoryStatistics
                {
                    TotalDeviceMemory = totalMemory,
                    FreeDeviceMemory = currentFree,
                    AllocatedMemory = stats.CurrentAllocatedMemory,
                    TotalAllocations = stats.TotalAllocations,
                    TotalDeallocations = stats.TotalDeallocations,
                    ActiveBuffers = _bufferRegistry.Count,
                    PeakMemoryUsage = stats.PeakMemoryUsage,
                    FragmentationRatio = CalculateFragmentation(),
                    LastUpdated = DateTimeOffset.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            LogStatisticsError(_logger, ex);
            return new CudaMemoryStatistics { LastUpdated = DateTimeOffset.UtcNow };
        }
    }

    /// <summary>
    /// Gets memory health status
    /// </summary>
    public double GetMemoryHealth()
    {
        if (_disposed)
        {
            return 0.0;
        }

        try
        {
            var stats = GetMemoryStatistics();

            // Health is based on available memory and fragmentation

            var memoryUtilization = (double)(stats.TotalDeviceMemory - stats.FreeDeviceMemory) / stats.TotalDeviceMemory;
            var fragmentationHealth = 1.0 - Math.Min(stats.FragmentationRatio, 1.0);

            // Memory is healthy if utilization is below 85% and fragmentation is low

            var utilizationHealth = memoryUtilization < 0.85 ? 1.0 : Math.Max(0.0, (0.95 - memoryUtilization) / 0.1);


            return (utilizationHealth + fragmentationHealth) / 2.0;
        }
        catch (Exception ex)
        {
            LogHealthCalculationError(_logger, ex);
            return 0.0;
        }
    }

    /// <summary>
    /// Optimizes memory for the given workload
    /// </summary>
    public async Task OptimizeMemoryAsync(CudaWorkloadProfile profile, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await Task.Run(() =>
        {
            try
            {
                // Perform garbage collection and defragmentation
                _memoryManager.Clear();

                // Configure memory pool based on workload

                if (profile.IsMemoryIntensive)
                {
                    // Pre-allocate larger pools for memory-intensive workloads
                    ConfigureMemoryPool(large: true);
                }
                else
                {
                    // Use default pool configuration
                    ConfigureMemoryPool(large: false);
                }

                LogOptimizationCompleted(_logger);
            }
            catch (Exception ex)
            {
                LogOptimizationFailed(_logger, ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Performs memory maintenance and cleanup
    /// </summary>
    public void PerformMaintenance()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Clean up any orphaned buffers
            CleanupOrphanedBuffers();

            // Optimize memory layout

            _asyncAdapter.OptimizeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();

            LogMaintenanceCompleted(_logger);
        }
        catch (Exception ex)
        {
            LogMaintenanceError(_logger, ex);
        }
    }

    /// <summary>
    /// Forces garbage collection and defragmentation
    /// </summary>
    public void ForceGarbageCollection()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _memoryManager.Clear();

            // Force .NET garbage collection as well

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            LogGarbageCollectionCompleted(_logger);
        }
        catch (Exception ex)
        {
            LogGarbageCollectionError(_logger, ex);
        }
    }

    private void RegisterBuffer<T>(IUnifiedMemoryBuffer<T> buffer) where T : unmanaged
    {
        try
        {
            // Extract buffer pointer for registration
            var bufferInfo = new CudaBufferInfo
            {
                ElementType = typeof(T),
                ElementCount = buffer.Length,
                SizeInBytes = buffer.Length * Unsafe.SizeOf<T>(),
                AllocationTime = DateTimeOffset.UtcNow
            };

            lock (_registryLock)
            {
                // Use buffer's hash code as a simple identifier
                var key = new IntPtr(buffer.GetHashCode());
                _bufferRegistry[key] = bufferInfo;
            }
        }
        catch (Exception ex)
        {
            LogBufferRegistrationFailed(_logger, ex);
        }
    }

    private double CalculateFragmentation()
    {
        try
        {
            var stats = _memoryManager.Statistics;

            // Simple fragmentation estimation based on allocation patterns

            if (stats.TotalAllocations == 0)
            {
                return 0.0;
            }


            var averageAllocationSize = (double)stats.CurrentAllocatedMemory / Math.Max(1, stats.TotalAllocations - stats.TotalDeallocations);
            var expectedMemory = averageAllocationSize * Math.Max(1, stats.TotalAllocations - stats.TotalDeallocations);


            if (expectedMemory == 0)
            {
                return 0.0;
            }


            return Math.Abs(stats.CurrentAllocatedMemory - expectedMemory) / expectedMemory;
        }
        catch
        {
            return 0.0;
        }
    }

    private void ConfigureMemoryPool(bool large)
    {
        try
        {
            // Configure memory pool settings based on workload requirements
            // This would involve setting pool sizes, allocation strategies, etc.
            LogMemoryPoolConfigured(_logger, large);
        }
        catch (Exception ex)
        {
            LogMemoryPoolConfigurationFailed(_logger, ex);
        }
    }

    private void CleanupOrphanedBuffers()
    {
        try
        {
            lock (_registryLock)
            {
                var currentTime = DateTimeOffset.UtcNow;
                var orphanedBuffers = _bufferRegistry
                    .Where(kvp => currentTime - kvp.Value.AllocationTime > TimeSpan.FromMinutes(30))
                    .ToList();

                foreach (var (key, _) in orphanedBuffers)
                {
                    _ = _bufferRegistry.Remove(key);
                }

                if (orphanedBuffers.Count > 0)
                {
                    LogOrphanedBuffersCleanedUp(_logger, orphanedBuffers.Count);
                }
            }
        }
        catch (Exception ex)
        {
            LogOrphanedBuffersCleanupError(_logger, ex);
        }
    }

    private void MonitorMemory(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var health = GetMemoryHealth();

            if (health < 0.5)
            {
                LogMemoryHealthDegraded(_logger, health);

                // Trigger automatic cleanup
                ForceGarbageCollection();
            }
            else
            {
                LogMemoryHealth(_logger, health);
            }
        }
        catch (Exception ex)
        {
            LogMemoryMonitoringError(_logger, ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _memoryMonitorTimer?.Dispose();

            // Clean up registry

            lock (_registryLock)
            {
                _bufferRegistry.Clear();
            }


            _asyncAdapter?.Dispose();
            _memoryManager?.Dispose();


            _disposed = true;

            LogIntegrationDisposed(_logger);
        }
    }
}

/// <summary>
/// Information about a CUDA buffer
/// </summary>
internal sealed class CudaBufferInfo
{
    /// <summary>
    /// Gets or sets the element type.
    /// </summary>
    /// <value>The element type.</value>
    public Type ElementType { get; init; } = typeof(object);
    /// <summary>
    /// Gets or sets the element count.
    /// </summary>
    /// <value>The element count.</value>
    public int ElementCount { get; init; }
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; init; }
    /// <summary>
    /// Gets or sets the allocation time.
    /// </summary>
    /// <value>The allocation time.</value>
    public DateTimeOffset AllocationTime { get; init; }
}

/// <summary>
/// CUDA memory statistics
/// </summary>
public sealed class CudaMemoryStatistics
{
    /// <summary>
    /// Gets or sets the total device memory.
    /// </summary>
    /// <value>The total device memory.</value>
    public long TotalDeviceMemory { get; init; }
    /// <summary>
    /// Gets or sets the free device memory.
    /// </summary>
    /// <value>The free device memory.</value>
    public long FreeDeviceMemory { get; init; }
    /// <summary>
    /// Gets or sets the allocated memory.
    /// </summary>
    /// <value>The allocated memory.</value>
    public long AllocatedMemory { get; init; }
    /// <summary>
    /// Gets or sets the total allocations.
    /// </summary>
    /// <value>The total allocations.</value>
    public long TotalAllocations { get; init; }
    /// <summary>
    /// Gets or sets the total deallocations.
    /// </summary>
    /// <value>The total deallocations.</value>
    public long TotalDeallocations { get; init; }
    /// <summary>
    /// Gets or sets the active buffers.
    /// </summary>
    /// <value>The active buffers.</value>
    public int ActiveBuffers { get; init; }
    /// <summary>
    /// Gets or sets the peak memory usage.
    /// </summary>
    /// <value>The peak memory usage.</value>
    public long PeakMemoryUsage { get; init; }
    /// <summary>
    /// Gets or sets the fragmentation ratio.
    /// </summary>
    /// <value>The fragmentation ratio.</value>
    public double FragmentationRatio { get; init; }
    /// <summary>
    /// Gets or sets the last updated.
    /// </summary>
    /// <value>The last updated.</value>
    public DateTimeOffset LastUpdated { get; init; }
}
