// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Runtime.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace DotCompute.Runtime.Services.Memory;

/// <summary>
/// Implementation of memory pool service
/// </summary>
public class MemoryPoolService : IMemoryPoolService, IDisposable
{
    private readonly AdvancedMemoryOptions _options;
    private readonly ILogger<MemoryPoolService> _logger;
    private readonly ConcurrentDictionary<string, IMemoryPool> _pools = new();
    private bool _disposed;

    public MemoryPoolService(
        IOptions<AdvancedMemoryOptions> options,
        ILogger<MemoryPoolService> logger)
    {
        _options = options?.Value ?? new AdvancedMemoryOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IMemoryPool GetPool(string acceleratorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acceleratorId);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MemoryPoolService));
        }

        return _pools.GetOrAdd(acceleratorId, id =>
        {
            _logger.LogDebug("Creating default memory pool for accelerator {AcceleratorId}", id);

            var initialSize = _options.MaxPoolSizeMB * 1024L * 1024L / 4; // Start with 1/4 of max
            var maxSize = _options.MaxPoolSizeMB * 1024L * 1024L;

            return new DefaultMemoryPool(id, initialSize, maxSize, _logger);
        });
    }

    public async Task<IMemoryPool> CreatePoolAsync(string acceleratorId, long initialSize, long maxSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acceleratorId);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MemoryPoolService));
        }

        if (initialSize <= 0 || maxSize <= 0 || initialSize > maxSize)
        {
            throw new ArgumentException("Invalid pool sizes");
        }

        _logger.LogDebug("Creating custom memory pool for accelerator {AcceleratorId} (initial: {InitialSizeMB}MB, max: {MaxSizeMB}MB)",
            acceleratorId, initialSize / 1024 / 1024, maxSize / 1024 / 1024);

        var pool = new DefaultMemoryPool(acceleratorId, initialSize, maxSize, _logger);
        _pools[acceleratorId] = pool;

        await Task.CompletedTask; // Placeholder for async initialization
        return pool;
    }

    public MemoryUsageStatistics GetUsageStatistics()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MemoryPoolService));
        }

        var totalAllocated = 0L;
        var totalAvailable = 0L;
        var perAcceleratorStats = new Dictionary<string, AcceleratorMemoryStatistics>();

        foreach (var kvp in _pools)
        {
            var pool = kvp.Value;
            var stats = pool.GetStatistics();

            totalAllocated += pool.UsedSize;
            totalAvailable += pool.AvailableSize;

            perAcceleratorStats[kvp.Key] = new AcceleratorMemoryStatistics
            {
                AcceleratorId = kvp.Key,
                TotalMemory = pool.TotalSize,
                AllocatedMemory = pool.UsedSize,
                AvailableMemory = pool.AvailableSize,
                ActiveAllocations = (int)stats.AllocationCount - (int)stats.DeallocationCount,
                LargestAvailableBlock = pool.AvailableSize // Simplified
            };
        }

        var fragmentationPercentage = totalAllocated > 0
            ? (double)(totalAllocated + totalAvailable - GetLargestContiguousBlock()) / totalAllocated * 100.0
            : 0.0;

        return new MemoryUsageStatistics
        {
            TotalAllocated = totalAllocated,
            TotalAvailable = totalAvailable,
            FragmentationPercentage = fragmentationPercentage,
            PerAcceleratorStats = perAcceleratorStats
        };
    }

    public async Task OptimizeMemoryUsageAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MemoryPoolService));
        }

        _logger.LogDebug("Optimizing memory usage across {PoolCount} pools", _pools.Count);

        var tasks = _pools.Values.Select(pool => pool.DefragmentAsync());
        await Task.WhenAll(tasks);

        _logger.LogInformation("Memory optimization completed for {PoolCount} pools", _pools.Count);
    }

    public async Task<long> ReleaseUnusedMemoryAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MemoryPoolService));
        }

        var totalReleased = 0L;

        foreach (var pool in _pools.Values)
        {
            // In a real implementation, this would release unused blocks back to the system
            await pool.DefragmentAsync();
            // totalReleased += pool.ReleaseUnused(); // Would be implemented in actual memory pool
        }

        _logger.LogDebug("Released {ReleasedMB}MB of unused memory", totalReleased / 1024 / 1024);
        return totalReleased;
    }

    private long GetLargestContiguousBlock() => _pools.Values.Max(p => p.AvailableSize);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Disposing MemoryPoolService");

        foreach (var pool in _pools.Values)
        {
            try
            {
                pool.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing memory pool for accelerator {AcceleratorId}",
                    pool.AcceleratorId);
            }
        }

        _pools.Clear();
        _disposed = true;
    }
}