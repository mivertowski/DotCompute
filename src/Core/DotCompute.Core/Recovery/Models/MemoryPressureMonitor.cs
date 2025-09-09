// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using global::System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Core.Recovery.Types;
using DotCompute.Core.Recovery.Memory;

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Monitors system memory pressure and provides real-time pressure information.
/// </summary>
public class MemoryPressureMonitor : IDisposable
{
    private readonly ILogger<MemoryPressureMonitor> _logger;
    private readonly Timer _monitoringTimer;
    private long _lastTotalMemory;
    private long _lastAvailableMemory;
    private volatile MemoryPressureInfo _currentPressure;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryPressureMonitor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="updateInterval">The interval between pressure updates. Defaults to 5 seconds.</param>
    public MemoryPressureMonitor(ILogger<MemoryPressureMonitor> logger, TimeSpan? updateInterval = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var interval = updateInterval ?? TimeSpan.FromSeconds(5);


        _currentPressure = CalculateMemoryPressure();
        _monitoringTimer = new Timer(UpdateMemoryPressure, null, interval, interval);


        _logger.LogInfoMessage("Memory pressure monitor initialized with {interval.TotalSeconds}s update interval");
    }

    /// <summary>
    /// Gets the current memory pressure information.
    /// </summary>
    /// <returns>The current memory pressure information.</returns>
    public MemoryPressureInfo GetCurrentPressure() => _currentPressure;

    /// <summary>
    /// Forces an immediate update of memory pressure information.
    /// </summary>
    /// <returns>The updated memory pressure information.</returns>
    public MemoryPressureInfo ForceUpdate()
    {
        _currentPressure = CalculateMemoryPressure();
        return _currentPressure;
    }

    private void UpdateMemoryPressure(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            _currentPressure = CalculateMemoryPressure();

            // Log if pressure has increased significantly

            if (_currentPressure.Level >= MemoryPressureLevel.High)
            {
                _logger.LogWarningMessage($"High memory pressure detected: {_currentPressure.Level} ({_currentPressure.PressureRatio})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error updating memory pressure information");
        }
    }

    private MemoryPressureInfo CalculateMemoryPressure()
    {
        var managedMemory = GC.GetTotalMemory(false);
        var totalMemory = Environment.WorkingSet;
        var availableMemory = totalMemory - managedMemory;

        // Calculate various memory metrics

        var usedMemory = totalMemory - availableMemory;
        var memoryLoadRatio = (double)usedMemory / totalMemory;
        var managedRatio = (double)managedMemory / totalMemory;
        var fragmentationRatio = 0.0; // Simplified for compatibility

        // Determine pressure level based on multiple factors

        var pressureLevel = DeterminePressureLevel(memoryLoadRatio, managedRatio, fragmentationRatio);

        // Calculate pressure trend

        var trend = CalculatePressureTrend(totalMemory, availableMemory);


        _lastTotalMemory = totalMemory;
        _lastAvailableMemory = availableMemory;


        return new MemoryPressureInfo
        {
            Level = pressureLevel,
            TotalMemory = totalMemory,
            AvailableMemory = availableMemory,
            UsedMemory = usedMemory,
            FragmentedMemory = 0, // Simplified for compatibility
            PressureRatio = memoryLoadRatio,
            ManagedRatio = managedRatio,
            FragmentationRatio = fragmentationRatio,
            Trend = (MemoryTrend)(int)trend,
            Timestamp = DateTimeOffset.UtcNow,
            Generation0Collections = GC.CollectionCount(0),
            Generation1Collections = GC.CollectionCount(1),
            Generation2Collections = GC.CollectionCount(2),
            TotalCommittedBytes = managedMemory,
            PromotedBytes = 0, // Simplified for compatibility
            PinnedObjectsCount = 0, // Simplified for compatibility
            FinalizationPendingCount = 0 // Simplified for compatibility
        };
    }

    private static MemoryPressureLevel DeterminePressureLevel(double memoryLoadRatio, double managedRatio, double fragmentationRatio)
    {
        // Weighted pressure calculation
        var weightedPressure = (memoryLoadRatio * 0.5) + (managedRatio * 0.3) + (fragmentationRatio * 0.2);


        return weightedPressure switch
        {
            < 0.5 => MemoryPressureLevel.Low,
            < 0.75 => MemoryPressureLevel.Medium,
            < 0.9 => MemoryPressureLevel.High,
            _ => MemoryPressureLevel.Critical
        };
    }

    private MemoryPressureTrend CalculatePressureTrend(long currentTotal, long currentAvailable)
    {
        if (_lastTotalMemory == 0 || _lastAvailableMemory == 0)
        {
            return MemoryPressureTrend.Stable;
        }


        var previousUsed = _lastTotalMemory - _lastAvailableMemory;
        var currentUsed = currentTotal - currentAvailable;
        var usedDelta = currentUsed - previousUsed;

        // Consider a 5% change as significant

        var threshold = _lastTotalMemory * 0.05;


        if (Math.Abs(usedDelta) < threshold)
        {

            return MemoryPressureTrend.Stable;
        }


        return usedDelta > 0 ? MemoryPressureTrend.Increasing : MemoryPressureTrend.Decreasing;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _monitoringTimer?.Dispose();
            _disposed = true;
            _logger.LogInfoMessage("Memory pressure monitor disposed");
        }
    }
}

/// <summary>
/// Represents the trend of memory pressure over time.
/// </summary>
public enum MemoryPressureTrend
{
    /// <summary>
    /// Memory pressure is decreasing.
    /// </summary>
    Decreasing,

    /// <summary>
    /// Memory pressure is stable.
    /// </summary>
    Stable,

    /// <summary>
    /// Memory pressure is increasing.
    /// </summary>
    Increasing,


    /// <summary>
    /// Memory pressure is rapidly increasing.
    /// </summary>
    RapidlyIncreasing
}
