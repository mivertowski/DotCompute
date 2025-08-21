// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Context information for memory recovery operations
/// </summary>
public class MemoryRecoveryContext
{
    public string Operation { get; set; } = string.Empty;
    public long RequestedBytes { get; set; }
    public string? PoolId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Configuration for memory recovery behavior
/// </summary>
public class MemoryRecoveryConfiguration
{
    public TimeSpan DefragmentationInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan AllocationRetryDelay { get; set; } = TimeSpan.FromMilliseconds(50);
    public int EmergencyReserveSizeMB { get; set; } = 64;
    public bool EnableLargeObjectHeapCompaction { get; set; } = true;
    public bool EnablePeriodicDefragmentation { get; set; } = true;
    public double MemoryPressureThreshold { get; set; } = 0.85; // 85%
    public int MaxAllocationRetries { get; set; } = 3;

    public static MemoryRecoveryConfiguration Default => new();

    public override string ToString()

        => $"Reserve={EmergencyReserveSizeMB}MB, DefragInterval={DefragmentationInterval}, PressureThreshold={MemoryPressureThreshold:P0}";
}

/// <summary>
/// Memory recovery strategy types
/// </summary>
public enum MemoryRecoveryStrategyType
{
    SimpleGarbageCollection,
    DefragmentationWithGC,
    AggressiveCleanup,
    EmergencyRecovery
}

/// <summary>
/// Result of memory defragmentation operation
/// </summary>
public class MemoryDefragmentationResult
{
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public long MemoryFreed { get; set; }
    public long MemoryBefore { get; set; }
    public long MemoryAfter { get; set; }
    public string? Error { get; set; }

    public double CompressionRatio => MemoryBefore > 0 ? (double)MemoryFreed / MemoryBefore : 0.0;

    public override string ToString()
        => Success

            ? $"Success: Freed {MemoryFreed / 1024 / 1024}MB in {Duration.TotalMilliseconds}ms (ratio: {CompressionRatio:P1})"
            : $"Failed: {Error}";
}

/// <summary>
/// Memory pressure levels
/// </summary>
public enum MemoryPressureLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Information about current memory pressure
/// </summary>
public class MemoryPressureInfo
{
    public MemoryPressureLevel Level { get; set; }
    public double PressureRatio { get; set; }
    public long TotalMemory { get; set; }
    public long AvailableMemory { get; set; }
    public long UsedMemory { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double GCPressure { get; set; }

    public override string ToString()

        => $"Level={Level}, Pressure={PressureRatio:P1}, Available={AvailableMemory / 1024 / 1024}MB";
}

/// <summary>
/// Monitors system memory pressure
/// </summary>
public sealed class MemoryPressureMonitor : IDisposable
{
    private readonly ILogger _logger;
    private readonly Timer _monitorTimer;
    private volatile MemoryPressureInfo _currentPressure;
#if WINDOWS
    private readonly System.Diagnostics.PerformanceCounter? _availableMemoryCounter;
#endif
    private bool _disposed;

    public MemoryPressureMonitor(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));


        try
        {
#if WINDOWS
            _availableMemoryCounter = new System.Diagnostics.PerformanceCounter("Memory", "Available MBytes");
#endif
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize memory performance counter");
        }

        _currentPressure = CalculateMemoryPressure();


        _monitorTimer = new Timer(UpdateMemoryPressure, null,

            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public MemoryPressureInfo GetCurrentPressure() => _currentPressure;

    private void UpdateMemoryPressure(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            _currentPressure = CalculateMemoryPressure();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating memory pressure information");
        }
    }

    private static MemoryPressureInfo CalculateMemoryPressure()
    {
        var gcMemory = GC.GetTotalMemory(false);
        var totalMemory = GC.GetTotalMemory(true); // Force GC for more accurate reading


        long availableMemory = 0;
        try
        {
#if WINDOWS
            availableMemory = (long)(_availableMemoryCounter?.NextValue() ?? 0) * 1024 * 1024; // Convert MB to bytes
#else
            // Fallback calculation for non-Windows platforms
            availableMemory = Math.Max(0, Environment.WorkingSet - gcMemory);
#endif
        }
        catch
        {
            // Fallback calculation
            availableMemory = Math.Max(0, Environment.WorkingSet - gcMemory);
        }

        var systemTotalMemory = availableMemory + gcMemory;
        var usedMemory = systemTotalMemory - availableMemory;
        var pressureRatio = systemTotalMemory > 0 ? (double)usedMemory / systemTotalMemory : 0.0;


        var level = pressureRatio switch
        {
            >= 0.95 => MemoryPressureLevel.Critical,
            >= 0.85 => MemoryPressureLevel.High,
            >= 0.70 => MemoryPressureLevel.Medium,
            _ => MemoryPressureLevel.Low
        };

        return new MemoryPressureInfo
        {
            Level = level,
            PressureRatio = pressureRatio,
            TotalMemory = systemTotalMemory,
            AvailableMemory = availableMemory,
            UsedMemory = usedMemory,
            Timestamp = DateTimeOffset.UtcNow,
            GCPressure = GC.CollectionCount(2) / (double)Math.Max(1, GC.CollectionCount(0))
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _monitorTimer?.Dispose();
#if WINDOWS
            _availableMemoryCounter?.Dispose();
#endif
            _disposed = true;
        }
    }
}

/// <summary>
/// Interface for memory pools that support recovery operations
/// </summary>
public interface IMemoryPool : IDisposable
{
    public string PoolId { get; }
    public long TotalAllocated { get; }
    public long TotalAvailable { get; }
    public int ActiveAllocations { get; }


    public Task CleanupAsync(CancellationToken cancellationToken = default);
    public Task DefragmentAsync(CancellationToken cancellationToken = default);
    public Task EmergencyCleanupAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tracks state and provides recovery operations for a memory pool
/// </summary>
public class MemoryPoolState
{
    private readonly IMemoryPool _pool;
    private readonly object _lock = new();
    private DateTimeOffset _lastCleanup = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastDefragmentation = DateTimeOffset.UtcNow;
    private int _cleanupCount;
    private int _defragmentationCount;

    public string PoolId { get; }
    public DateTimeOffset LastCleanup => _lastCleanup;
    public DateTimeOffset LastDefragmentation => _lastDefragmentation;
    public int CleanupCount => _cleanupCount;
    public int DefragmentationCount => _defragmentationCount;

    public MemoryPoolState(string poolId, IMemoryPool pool)
    {
        PoolId = poolId ?? throw new ArgumentNullException(nameof(poolId));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
    }

    public async Task PerformCleanupAsync(CancellationToken cancellationToken = default)
    {
        await _pool.CleanupAsync(cancellationToken);


        lock (_lock)
        {
            _lastCleanup = DateTimeOffset.UtcNow;
            _cleanupCount++;
        }
    }

    public async Task PerformDefragmentationAsync(CancellationToken cancellationToken = default)
    {
        await _pool.DefragmentAsync(cancellationToken);


        lock (_lock)
        {
            _lastDefragmentation = DateTimeOffset.UtcNow;
            _defragmentationCount++;
        }
    }

    public async Task PerformEmergencyCleanupAsync(CancellationToken cancellationToken = default)
    {
        await _pool.EmergencyCleanupAsync(cancellationToken);


        lock (_lock)
        {
            _lastCleanup = DateTimeOffset.UtcNow;
            _cleanupCount++;
        }
    }

    public MemoryPoolStatistics GetStatistics()
    {
        return new MemoryPoolStatistics
        {
            PoolId = PoolId,
            TotalAllocated = _pool.TotalAllocated,
            TotalAvailable = _pool.TotalAvailable,
            ActiveAllocations = _pool.ActiveAllocations,
            LastCleanup = _lastCleanup,
            LastDefragmentation = _lastDefragmentation,
            CleanupCount = _cleanupCount,
            DefragmentationCount = _defragmentationCount,
            UtilizationRatio = _pool.TotalAllocated + _pool.TotalAvailable > 0

                ? (double)_pool.TotalAllocated / (_pool.TotalAllocated + _pool.TotalAvailable)
                : 0.0
        };
    }
}

/// <summary>
/// Statistics for a memory pool
/// </summary>
public class MemoryPoolStatistics
{
    public string PoolId { get; set; } = string.Empty;
    public long TotalAllocated { get; set; }
    public long TotalAvailable { get; set; }
    public int ActiveAllocations { get; set; }
    public DateTimeOffset LastCleanup { get; set; }
    public DateTimeOffset LastDefragmentation { get; set; }
    public int CleanupCount { get; set; }
    public int DefragmentationCount { get; set; }
    public double UtilizationRatio { get; set; }

    public override string ToString()

        => $"Pool {PoolId}: {TotalAllocated / 1024 / 1024}MB allocated, {ActiveAllocations} active, {UtilizationRatio:P1} utilized";
}

/// <summary>
/// Exception thrown when memory allocation fails after recovery attempts
/// </summary>
public class MemoryAllocationException : Exception
{
    public MemoryAllocationException(string message) : base(message) { }
    public MemoryAllocationException(string message, Exception innerException) : base(message, innerException) { }
}