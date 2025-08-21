// <copyright file="MemoryPressureMonitor.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Logging;
using DotCompute.Core.Recovery.Memory.Types;

namespace DotCompute.Core.Recovery.Memory.Monitoring;

/// <summary>
/// Monitors system memory pressure.
/// Tracks memory usage and provides real-time pressure information.
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

    /// <summary>
    /// Initializes a new instance of the MemoryPressureMonitor class.
    /// </summary>
    /// <param name="logger">Logger for monitoring events.</param>
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

    /// <summary>
    /// Gets the current memory pressure information.
    /// </summary>
    /// <returns>Current memory pressure state.</returns>
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

    private MemoryPressureInfo CalculateMemoryPressure()
    {
        var gcMemory = GC.GetTotalMemory(false);
        var totalMemory = GC.GetTotalMemory(true); // Force GC for more accurate reading

        long availableMemory = 0;
        try
        {
#if WINDOWS
            if (_availableMemoryCounter != null)
            {
                availableMemory = (long)(_availableMemoryCounter.NextValue()) * 1024 * 1024; // Convert MB to bytes
            }
            else
            {
                availableMemory = Math.Max(0, Environment.WorkingSet - gcMemory);
            }
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
    /// Disposes the monitor and releases resources.
    /// </summary>
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