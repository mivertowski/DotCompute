// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Integration;

/// <summary>
/// Integrates CUDA memory management with unified buffer system
/// </summary>
public sealed class CudaMemoryIntegration : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly CudaMemoryManager _memoryManager;
    private readonly CudaAsyncMemoryManagerAdapter _asyncAdapter;
    private readonly Dictionary<IntPtr, CudaBufferInfo> _bufferRegistry;
    private readonly Timer _memoryMonitorTimer;
    private readonly object _registryLock = new();
    private volatile bool _disposed;

    public CudaMemoryIntegration(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _memoryManager = new CudaMemoryManager(context, logger);
        _asyncAdapter = new CudaAsyncMemoryManagerAdapter(_memoryManager);
        _bufferRegistry = new Dictionary<IntPtr, CudaBufferInfo>();

        // Set up memory monitoring
        _memoryMonitorTimer = new Timer(MonitorMemory, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));

        _logger.LogInfoMessage($"CUDA Memory Integration initialized for device {context.DeviceId}");
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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaMemoryIntegration));
        }

        try
        {
            var buffer = await _asyncAdapter.AllocateAsync<T>(count, options, cancellationToken);
            
            // Register buffer for monitoring
            RegisterBuffer(buffer);
            
            _logger.LogDebugMessage($"Allocated optimized buffer: {count} elements of {typeof(T).Name}");
            
            return buffer;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to allocate optimized buffer for {count} elements of {typeof(T).Name}");
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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaMemoryIntegration));
        }

        try
        {
            await _asyncAdapter.CopyAsync(source, destination, cancellationToken);
            _logger.LogDebugMessage($"Optimized copy completed: {source.Length} elements of {typeof(T).Name}");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Optimized copy failed for {typeof(T).Name}");
            throw;
        }
    }

    /// <summary>
    /// Gets memory usage statistics
    /// </summary>
    public CudaMemoryStatistics GetMemoryStatistics()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaMemoryIntegration));
        }

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
            _logger.LogWarning(ex, "Error getting memory statistics");
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
            _logger.LogWarning(ex, "Error calculating memory health");
            return 0.0;
        }
    }

    /// <summary>
    /// Optimizes memory for the given workload
    /// </summary>
    public async Task OptimizeMemoryAsync(CudaWorkloadProfile profile, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaMemoryIntegration));
        }

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
                
                _logger.LogDebugMessage("Memory optimization completed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Memory optimization failed");
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
            _asyncAdapter.OptimizeAsync().AsTask().Wait(TimeSpan.FromSeconds(30));
            
            _logger.LogDebugMessage("Memory maintenance completed");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during memory maintenance");
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
            
            _logger.LogDebugMessage("Forced garbage collection completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during forced garbage collection");
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
            _logger.LogWarning(ex, "Failed to register buffer");
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
            _logger.LogDebugMessage($"Memory pool configured: large={large}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure memory pool");
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
                    _bufferRegistry.Remove(key);
                }

                if (orphanedBuffers.Count > 0)
                {
                    _logger.LogDebugMessage($"Cleaned up {orphanedBuffers.Count} orphaned buffer entries");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up orphaned buffers");
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
                _logger.LogWarning("Memory health degraded: {Health:F2}", health);
                
                // Trigger automatic cleanup
                ForceGarbageCollection();
            }
            else
            {
                _logger.LogDebugMessage($"Memory health: {health:F2}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during memory monitoring");
        }
    }

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
            
            _logger.LogDebugMessage("CUDA Memory Integration disposed");
        }
    }
}

/// <summary>
/// Information about a CUDA buffer
/// </summary>
internal sealed class CudaBufferInfo
{
    public Type ElementType { get; init; } = typeof(object);
    public int ElementCount { get; init; }
    public long SizeInBytes { get; init; }
    public DateTimeOffset AllocationTime { get; init; }
}

/// <summary>
/// CUDA memory statistics
/// </summary>
public sealed class CudaMemoryStatistics
{
    public long TotalDeviceMemory { get; init; }
    public long FreeDeviceMemory { get; init; }
    public long AllocatedMemory { get; init; }
    public long TotalAllocations { get; init; }
    public long TotalDeallocations { get; init; }
    public int ActiveBuffers { get; init; }
    public long PeakMemoryUsage { get; init; }
    public double FragmentationRatio { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}
