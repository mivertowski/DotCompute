// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Core.Recovery.Types;
using System.Diagnostics;

namespace DotCompute.Core.Recovery.Memory;

/// <summary>
/// Monitors system memory pressure continuously and provides real-time memory usage information.
/// </summary>
/// <remarks>
/// This class provides continuous monitoring of system memory usage through a background timer.
/// It supports both Windows performance counters and cross-platform fallback mechanisms
/// for determining available memory. The monitor automatically updates pressure information
/// at regular intervals and provides thread-safe access to current memory state.
/// </remarks>
public sealed class MemoryPressureMonitor : IDisposable
{
    private readonly ILogger _logger;
    private readonly Timer _monitorTimer;
    private volatile MemoryPressureInfo _currentPressure;
#if WINDOWS
    private readonly PerformanceCounter? _availableMemoryCounter;
#endif
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryPressureMonitor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for recording monitoring events and errors.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="logger"/> is null.
    /// </exception>
    /// <remarks>
    /// The constructor initializes the monitoring system, attempts to set up platform-specific
    /// performance counters, calculates initial memory pressure, and starts a background timer
    /// that updates pressure information every 30 seconds.
    /// </remarks>
    public MemoryPressureMonitor(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        try
        {
#if WINDOWS
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
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

    /// <summary>
    /// Gets the current memory pressure information.
    /// </summary>
    /// <returns>
    /// A <see cref="MemoryPressureInfo"/> object containing the most recent memory pressure data.
    /// </returns>
    /// <remarks>
    /// This method returns a snapshot of memory pressure that is updated automatically
    /// by the background monitoring timer. The returned information is thread-safe
    /// and represents the state at the time of the last update cycle.
    /// </remarks>
    public MemoryPressureInfo GetCurrentPressure() => _currentPressure;

    /// <summary>
    /// Updates the memory pressure information in the background timer callback.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    /// <remarks>
    /// This method is called periodically by the internal timer to refresh
    /// memory pressure data. It includes error handling to prevent monitoring
    /// failures from affecting application stability.
    /// </remarks>
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

    /// <summary>
    /// Calculates current memory pressure using platform-appropriate mechanisms.
    /// </summary>
    /// <returns>
    /// A new <see cref="MemoryPressureInfo"/> object with current memory statistics.
    /// </returns>
    /// <remarks>
    /// This method uses Windows performance counters when available, falling back
    /// to cross-platform estimates based on GC information and working set data.
    /// The pressure level is determined by comparing the usage ratio against
    /// predefined thresholds (95% = Critical, 85% = High, 70% = Medium, &lt;70% = Low).
    /// </remarks>
    private MemoryPressureInfo CalculateMemoryPressure()
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

    /// <summary>
    /// Releases all resources used by the <see cref="MemoryPressureMonitor"/>.
    /// </summary>
    /// <remarks>
    /// This method stops the monitoring timer and releases any platform-specific
    /// resources such as performance counters. It can be called multiple times safely.
    /// </remarks>
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
