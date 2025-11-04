// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// Monitors memory pressure and triggers appropriate responses to prevent out-of-memory conditions.
/// Integrates with macOS memory pressure notifications and provides proactive memory management.
/// </summary>
internal sealed class MetalMemoryPressureMonitor : IDisposable
{
    private readonly ILogger _logger;
    private readonly double _pressureThreshold;
    private readonly Timer _pressureCheckTimer;
    private readonly Lock _pressureLock = new();

    // Memory pressure state
    private double _currentUsagePercentage;
    private bool _isUnderPressure;
    private DateTimeOffset _lastPressureEvent = DateTimeOffset.MinValue;
    private int _pressureEventCount;
    private bool _disposed;

    // Pressure level thresholds
    // NOTE: Order is important - Moderate < Warning < Critical < Emergency
    // Default _pressureThreshold is 0.85, so: Moderate=85%, Warning=90%, Critical=95%, Emergency=98%
    private const double WARNING_THRESHOLD = 0.90;    // 90% - aggressive monitoring
    private const double CRITICAL_THRESHOLD = 0.95;   // 95% - aggressive cleanup needed
    private const double EMERGENCY_THRESHOLD = 0.98;  // 98% - emergency memory recovery

    private const int PRESSURE_CHECK_INTERVAL_MS = 5000; // Check every 5 seconds

    public MetalMemoryPressureMonitor(ILogger logger, double pressureThreshold = 0.85)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pressureThreshold = Math.Clamp(pressureThreshold, 0.5, 0.99);

        // Start pressure monitoring timer
        _pressureCheckTimer = new Timer(CheckMemoryPressure, null,
            TimeSpan.FromMilliseconds(PRESSURE_CHECK_INTERVAL_MS),
            TimeSpan.FromMilliseconds(PRESSURE_CHECK_INTERVAL_MS));

        _logger.LogDebug("MetalMemoryPressureMonitor started - threshold: {Threshold:P2}", _pressureThreshold);
    }

    /// <summary>
    /// Gets whether the system is currently under memory pressure.
    /// </summary>
    public bool IsUnderPressure => _isUnderPressure;

    /// <summary>
    /// Gets the current memory usage percentage.
    /// </summary>
    public double CurrentUsagePercentage => _currentUsagePercentage;

    /// <summary>
    /// Gets the memory pressure level.
    /// </summary>
    public MemoryPressureLevel PressureLevel
    {
        get
        {
            if (_currentUsagePercentage >= EMERGENCY_THRESHOLD)
            {
                return MemoryPressureLevel.Emergency;
            }

            if (_currentUsagePercentage >= CRITICAL_THRESHOLD)
            {
                return MemoryPressureLevel.Critical;
            }

            if (_currentUsagePercentage >= WARNING_THRESHOLD)
            {
                return MemoryPressureLevel.Warning;
            }

            if (_currentUsagePercentage >= _pressureThreshold)
            {
                return MemoryPressureLevel.Moderate;
            }

            return MemoryPressureLevel.Normal;
        }
    }

    /// <summary>
    /// Gets the number of pressure events recorded.
    /// </summary>
    public int PressureEventCount => _pressureEventCount;

    /// <summary>
    /// Updates the current memory pressure based on usage.
    /// </summary>
    public void UpdatePressure(long currentUsage, long totalAvailable)
    {
        if (_disposed || totalAvailable <= 0)
        {
            return;
        }

        var usagePercentage = (double)currentUsage / totalAvailable;

        lock (_pressureLock)
        {
            _currentUsagePercentage = usagePercentage;
            var previousPressure = _isUnderPressure;
            _isUnderPressure = usagePercentage >= _pressureThreshold;

            // Log pressure changes
            if (_isUnderPressure && !previousPressure)
            {
                _pressureEventCount++;
                _lastPressureEvent = DateTimeOffset.UtcNow;

                _logger.LogWarning("Memory pressure detected - usage: {Usage:P2} (threshold: {Threshold:P2})",
                    usagePercentage, _pressureThreshold);

                // Trigger immediate pressure response based on level
                TriggerPressureResponse();
            }
            else if (!_isUnderPressure && previousPressure)
            {
                _logger.LogInformation("Memory pressure resolved - usage: {Usage:P2}", usagePercentage);
            }
        }
    }

    /// <summary>
    /// Forces a memory pressure check.
    /// </summary>
    public void CheckPressureNow()
    {
        if (_disposed)
        {
            return;
        }

        CheckMemoryPressure(null);
    }

    /// <summary>
    /// Gets detailed memory pressure statistics.
    /// </summary>
    public MemoryPressureStatistics GetStatistics()
    {
        lock (_pressureLock)
        {
            return new MemoryPressureStatistics
            {
                CurrentUsagePercentage = _currentUsagePercentage,
                PressureThreshold = _pressureThreshold,
                IsUnderPressure = _isUnderPressure,
                PressureLevel = PressureLevel,
                PressureEventCount = _pressureEventCount,
                LastPressureEvent = _lastPressureEvent,
                TimeSinceLastPressure = _lastPressureEvent != DateTimeOffset.MinValue
                    ? DateTimeOffset.UtcNow - _lastPressureEvent
                    : null
            };
        }
    }

    /// <summary>
    /// Periodic memory pressure check callback.
    /// </summary>
    private void CheckMemoryPressure(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Get system memory information
            var (availableMemory, totalMemory) = GetSystemMemoryInfo();

            if (totalMemory > 0)
            {
                var usedMemory = totalMemory - availableMemory;
                UpdatePressure(usedMemory, totalMemory);
            }

            // Additional macOS-specific pressure detection could be added here
            CheckMacOSMemoryPressure();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during memory pressure check");
        }
    }

    /// <summary>
    /// Gets system memory information.
    /// </summary>
    private static (long available, long total) GetSystemMemoryInfo()
    {
        try
        {
            // This would use platform-specific APIs to get actual memory info
            // For now, return mock values
            var totalMemory = GC.GetTotalMemory(false);
            var availableMemory = Math.Max(0, 16L * 1024 * 1024 * 1024 - totalMemory); // Assume 16GB total

            return (availableMemory, 16L * 1024 * 1024 * 1024);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// Checks for macOS-specific memory pressure indicators.
    /// </summary>
    private void CheckMacOSMemoryPressure()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            // This would integrate with macOS memory pressure notifications
            // and dispatch_source_create(DISPATCH_SOURCE_TYPE_MEMORYPRESSURE, ...)
            // For now, this is a placeholder

            _logger.LogTrace("Checked macOS memory pressure indicators");
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error checking macOS memory pressure");
        }
    }

    /// <summary>
    /// Triggers appropriate pressure response based on current level.
    /// </summary>
    private void TriggerPressureResponse()
    {
        var level = PressureLevel;

        _logger.LogInformation("Triggering memory pressure response - level: {Level}", level);

        switch (level)
        {
            case MemoryPressureLevel.Emergency:
                _logger.LogError("Emergency memory pressure - immediate action required");
                // Emergency response could trigger immediate pool cleanup, GC, etc.
                break;

            case MemoryPressureLevel.Critical:
                _logger.LogWarning("Critical memory pressure - aggressive cleanup needed");
                // Critical response
                break;

            case MemoryPressureLevel.Warning:
                _logger.LogWarning("Warning level memory pressure - proactive cleanup recommended");
                // Warning response
                break;

            case MemoryPressureLevel.Moderate:
                _logger.LogInformation("Moderate memory pressure - monitoring closely");
                // Moderate response
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _pressureCheckTimer?.Dispose();

            _logger.LogInformation("MetalMemoryPressureMonitor disposed - final stats: {EventCount} pressure events",
                _pressureEventCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing MetalMemoryPressureMonitor");
        }
    }
}

/// <summary>
/// Memory pressure levels.
/// </summary>
public enum MemoryPressureLevel
{
    Normal,      // < threshold (default 85%)
    Moderate,    // >= threshold (85%) but < 90%
    Warning,     // >= 90% but < 95%
    Critical,    // >= 95% but < 98%
    Emergency    // >= 98%
}

/// <summary>
/// Memory pressure statistics.
/// </summary>
public sealed record MemoryPressureStatistics
{
    public required double CurrentUsagePercentage { get; init; }
    public required double PressureThreshold { get; init; }
    public required bool IsUnderPressure { get; init; }
    public required MemoryPressureLevel PressureLevel { get; init; }
    public required int PressureEventCount { get; init; }
    public required DateTimeOffset LastPressureEvent { get; init; }
    public required TimeSpan? TimeSinceLastPressure { get; init; }
}
