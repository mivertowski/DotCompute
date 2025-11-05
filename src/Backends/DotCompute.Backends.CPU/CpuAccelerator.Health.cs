// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Health;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// CPU accelerator health monitoring implementation.
/// </summary>
public sealed partial class CpuAccelerator
{
    #region Health Monitoring LoggerMessage Delegates

    private static readonly Action<ILogger, Exception?> _logHealthSnapshotError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3001, nameof(_logHealthSnapshotError)),
            "Failed to collect health snapshot for CPU accelerator");

    private static readonly Action<ILogger, Exception?> _logSensorReadingsError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3002, nameof(_logSensorReadingsError)),
            "Failed to collect sensor readings for CPU accelerator");

    private static readonly Action<ILogger, Exception?> _logSensorBuildWarning =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(3003, nameof(_logSensorBuildWarning)),
            "Failed to collect some sensor readings");

    private static readonly Action<ILogger, Exception?> _logCpuUsageWarning =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(3004, nameof(_logCpuUsageWarning)),
            "Failed to calculate CPU usage");

    private static readonly Action<ILogger, Exception?> _logHealthScoreWarning =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(3005, nameof(_logHealthScoreWarning)),
            "Failed to calculate health score");

    private static readonly Action<ILogger, Exception?> _logStatusMessageWarning =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(3006, nameof(_logStatusMessageWarning)),
            "Failed to build status message");

    #endregion

    private Process? _currentProcess;
    private DateTime _lastCpuCheck = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
    private double _lastCpuUsage;

    /// <summary>
    /// Gets a comprehensive health snapshot of the CPU accelerator.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the device health snapshot.</returns>
    /// <remarks>
    /// <para>
    /// CPU health monitoring uses .NET's cross-platform System.Diagnostics APIs
    /// to collect process-level and system-level metrics.
    /// </para>
    ///
    /// <para>
    /// <b>Available Metrics:</b>
    /// - CPU utilization (process-level percentage)
    /// - Memory usage (working set, private memory, virtual memory)
    /// - Thread count (active threads in thread pool)
    /// - Garbage collection metrics (Gen0, Gen1, Gen2 collections)
    /// - System memory information (total physical memory)
    /// </para>
    ///
    /// <para>
    /// <b>Cross-Platform Support:</b>
    /// - Windows: Full metrics available
    /// - Linux: Full metrics available via /proc
    /// - macOS: Full metrics available via system APIs
    /// </para>
    ///
    /// <para>
    /// <b>Limitations:</b>
    /// - CPU utilization is process-level, not system-wide
    /// - No hardware temperature sensors
    /// - No power consumption metrics
    /// - No individual core utilization breakdown
    /// </para>
    ///
    /// <para>
    /// <b>Performance:</b> Typically sub-millisecond as metrics are queried
    /// from OS-cached process information.
    /// </para>
    /// </remarks>
    public override ValueTask<DeviceHealthSnapshot> GetHealthSnapshotAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Ensure we have the current process reference
            _currentProcess ??= Process.GetCurrentProcess();

            // Build sensor readings from process and system metrics
            var sensorReadings = BuildSensorReadings();

            // Calculate health score based on resource utilization
            var healthScore = CalculateHealthScore();

            // Determine status based on resource pressure
            var status = DetermineHealthStatus();

            // Build status message
            var statusMessage = BuildStatusMessage();

            var snapshot = new DeviceHealthSnapshot
            {
                DeviceId = Info.Id,
                DeviceName = Info.Name,
                BackendType = "CPU",
                Timestamp = DateTimeOffset.UtcNow,
                HealthScore = healthScore,
                Status = status,
                IsAvailable = true, // CPU is always available
                SensorReadings = sensorReadings,
                ErrorCount = 0, // CPU backend doesn't track errors separately
                ConsecutiveFailures = 0,
                IsThrottling = IsThrottling(),
                StatusMessage = statusMessage
            };

            return ValueTask.FromResult(snapshot);
        }
        catch (Exception ex)
        {
            _logHealthSnapshotError(_logger, ex);
            return ValueTask.FromResult(DeviceHealthSnapshot.CreateUnavailable(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "CPU",
                reason: $"Error collecting metrics: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// Gets current sensor readings from the CPU accelerator.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the collection of sensor readings.</returns>
    /// <remarks>
    /// CPU sensor readings include process-level metrics and system-level information.
    /// All metrics are cross-platform compatible.
    /// </remarks>
    public override ValueTask<IReadOnlyList<SensorReading>> GetSensorReadingsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            _currentProcess ??= Process.GetCurrentProcess();
            var readings = BuildSensorReadings();
            return ValueTask.FromResult<IReadOnlyList<SensorReading>>(readings);
        }
        catch (Exception ex)
        {
            _logSensorReadingsError(_logger, ex);
            return ValueTask.FromResult<IReadOnlyList<SensorReading>>(Array.Empty<SensorReading>());
        }
    }

    /// <summary>
    /// Builds sensor readings from process and system metrics.
    /// </summary>
    private IReadOnlyList<SensorReading> BuildSensorReadings()
    {
        if (_currentProcess == null)
        {
            return Array.Empty<SensorReading>();
        }

        var readings = new List<SensorReading>(10);

        try
        {
            // Refresh process information
            _currentProcess.Refresh();

            // 1. CPU Utilization (percentage)
            var cpuUsage = CalculateCpuUsage();
            readings.Add(SensorReading.Create(
                SensorType.ComputeUtilization,
                cpuUsage,
                minValue: 0.0,
                maxValue: 100.0,
                name: "CPU Utilization"
            ));

            // 2. Working Set Memory (bytes)
            var workingSet = (double)_currentProcess.WorkingSet64;
            readings.Add(SensorReading.Create(
                SensorType.MemoryUsedBytes,
                workingSet,
                minValue: 0.0,
                name: "Working Set Memory"
            ));

            // 3. Private Memory (bytes)
            var privateMemory = (double)_currentProcess.PrivateMemorySize64;
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                privateMemory,
                minValue: 0.0,
                name: "Private Memory"
            ));

            // 4. Virtual Memory (bytes)
            var virtualMemory = (double)_currentProcess.VirtualMemorySize64;
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                virtualMemory,
                minValue: 0.0,
                name: "Virtual Memory"
            ));

            // 5. Thread Count
            var threadCount = (double)_currentProcess.Threads.Count;
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                threadCount,
                minValue: 1.0,
                name: "Thread Count"
            ));

            // 6. GC Generation 0 Collections
            var gen0Collections = (double)GC.CollectionCount(0);
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                gen0Collections,
                minValue: 0.0,
                name: "GC Gen0 Collections"
            ));

            // 7. GC Generation 1 Collections
            var gen1Collections = (double)GC.CollectionCount(1);
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                gen1Collections,
                minValue: 0.0,
                name: "GC Gen1 Collections"
            ));

            // 8. GC Generation 2 Collections
            var gen2Collections = (double)GC.CollectionCount(2);
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                gen2Collections,
                minValue: 0.0,
                name: "GC Gen2 Collections"
            ));

            // 9. GC Total Memory
            var gcTotalMemory = (double)GC.GetTotalMemory(forceFullCollection: false);
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                gcTotalMemory,
                minValue: 0.0,
                name: "GC Total Memory"
            ));

            // 10. System Total Physical Memory (if available)
            if (TryGetTotalPhysicalMemory(out var totalMemory))
            {
                readings.Add(SensorReading.Create(
                    SensorType.MemoryTotalBytes,
                    (double)totalMemory,
                    name: "Total Physical Memory"
                ));

                // 11. Memory Utilization Percentage
                var memoryUtilization = (workingSet / totalMemory) * 100.0;
                readings.Add(SensorReading.Create(
                    SensorType.MemoryUtilization,
                    memoryUtilization,
                    minValue: 0.0,
                    maxValue: 100.0,
                    name: "Memory Utilization"
                ));
            }
        }
        catch (Exception ex)
        {
            _logSensorBuildWarning(_logger, ex);
        }

        return readings;
    }

    /// <summary>
    /// Calculates CPU usage percentage for the current process.
    /// </summary>
    private double CalculateCpuUsage()
    {
        if (_currentProcess == null)
        {
            return 0.0;
        }

        try
        {
            var currentTime = DateTime.UtcNow;
            var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;

            // Calculate CPU usage since last check
            var timeDiff = (currentTime - _lastCpuCheck).TotalMilliseconds;
            var processorTimeDiff = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;

            if (timeDiff > 0)
            {
                var cpuUsage = (processorTimeDiff / (Environment.ProcessorCount * timeDiff)) * 100.0;
                _lastCpuUsage = Math.Clamp(cpuUsage, 0.0, 100.0);
            }

            // Update for next calculation
            _lastCpuCheck = currentTime;
            _lastTotalProcessorTime = currentTotalProcessorTime;

            return _lastCpuUsage;
        }
        catch (Exception ex)
        {
            _logCpuUsageWarning(_logger, ex);
            return _lastCpuUsage; // Return last known value
        }
    }

    /// <summary>
    /// Attempts to get total physical memory from the system.
    /// </summary>
    private static bool TryGetTotalPhysicalMemory(out long totalBytes)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: Use performance counter or WMI
                var gcMemoryInfo = GC.GetGCMemoryInfo();
                totalBytes = gcMemoryInfo.TotalAvailableMemoryBytes;
                return totalBytes > 0;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux: Read from /proc/meminfo
                if (File.Exists("/proc/meminfo"))
                {
                    var lines = File.ReadAllLines("/proc/meminfo");
                    var memTotalLine = lines.FirstOrDefault(l => l.StartsWith("MemTotal:", StringComparison.Ordinal));
                    if (memTotalLine != null)
                    {
                        var parts = memTotalLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                        {
                            totalBytes = kb * 1024; // Convert KB to bytes
                            return true;
                        }
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS: Use GC memory info as approximation
                var gcMemoryInfo = GC.GetGCMemoryInfo();
                totalBytes = gcMemoryInfo.TotalAvailableMemoryBytes;
                return totalBytes > 0;
            }

            // Fallback: Use GC memory info
            var memoryInfo = GC.GetGCMemoryInfo();
            totalBytes = memoryInfo.TotalAvailableMemoryBytes;
            return totalBytes > 0;
        }
        catch
        {
            totalBytes = 0;
            return false;
        }
    }

    /// <summary>
    /// Calculates health score based on resource utilization.
    /// </summary>
    private double CalculateHealthScore()
    {
        if (_currentProcess == null)
        {
            return 0.5;
        }

        try
        {
            var baseScore = 1.0;

            // Penalize high CPU usage (>80%)
            var cpuUsage = CalculateCpuUsage();
            if (cpuUsage > 80.0)
            {
                baseScore -= 0.2; // 20% penalty
            }
            else if (cpuUsage > 60.0)
            {
                baseScore -= 0.1; // 10% penalty
            }

            // Penalize high memory usage if we can determine it
            if (TryGetTotalPhysicalMemory(out var totalMemory))
            {
                var workingSet = _currentProcess.WorkingSet64;
                var memoryUtilization = (double)workingSet / totalMemory;

                if (memoryUtilization > 0.8)
                {
                    baseScore -= 0.2; // 20% penalty
                }
                else if (memoryUtilization > 0.6)
                {
                    baseScore -= 0.1; // 10% penalty
                }
            }

            // Penalize excessive thread count (>100 threads is usually problematic)
            var threadCount = _currentProcess.Threads.Count;
            if (threadCount > 100)
            {
                baseScore -= 0.1; // 10% penalty
            }

            return Math.Clamp(baseScore, 0.0, 1.0);
        }
        catch (Exception ex)
        {
            _logHealthScoreWarning(_logger, ex);
            return 0.5;
        }
    }

    /// <summary>
    /// Determines health status based on resource pressure.
    /// </summary>
    private DeviceHealthStatus DetermineHealthStatus()
    {
        try
        {
            var healthScore = CalculateHealthScore();

            if (healthScore >= 0.8)
            {
                return DeviceHealthStatus.Healthy;
            }
            else if (healthScore >= 0.5)
            {
                return DeviceHealthStatus.Warning;
            }
            else
            {
                return DeviceHealthStatus.Critical;
            }
        }
        catch
        {
            return DeviceHealthStatus.Unknown;
        }
    }

    /// <summary>
    /// Determines if the CPU is throttling based on resource pressure.
    /// </summary>
    private bool IsThrottling()
    {
        if (_currentProcess == null)
        {
            return false;
        }

        try
        {
            // Consider throttling if CPU usage is consistently very high
            var cpuUsage = CalculateCpuUsage();
            if (cpuUsage > 90.0)
            {
                return true;
            }

            // Check for memory pressure
            if (TryGetTotalPhysicalMemory(out var totalMemory))
            {
                var workingSet = _currentProcess.WorkingSet64;
                var memoryUtilization = (double)workingSet / totalMemory;
                if (memoryUtilization > 0.9)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Builds human-readable status message.
    /// </summary>
    private string BuildStatusMessage()
    {
        if (_currentProcess == null)
        {
            return "CPU accelerator not initialized";
        }

        var messages = new List<string>();

        try
        {
            // CPU usage
            var cpuUsage = CalculateCpuUsage();
            messages.Add($"CPU: {cpuUsage:F1}%");

            // Memory usage
            var workingSet = _currentProcess.WorkingSet64;
            var workingSetMB = workingSet / (1024.0 * 1024.0);
            messages.Add($"Memory: {workingSetMB:F1} MB");

            // Thread count
            var threadCount = _currentProcess.Threads.Count;
            messages.Add($"Threads: {threadCount}");

            // SIMD capabilities
            var simdInfo = Intrinsics.SimdCapabilities.GetSummary();
            messages.Add($"SIMD: {simdInfo}");

            // Performance mode
            messages.Add($"Mode: {_options.PerformanceMode}");

            // Warnings
            if (cpuUsage > 80.0)
            {
                messages.Add("High CPU usage");
            }

            if (TryGetTotalPhysicalMemory(out var totalMemory))
            {
                var memoryUtilization = (double)workingSet / totalMemory;
                if (memoryUtilization > 0.8)
                {
                    messages.Add("High memory pressure");
                }
            }
        }
        catch (Exception ex)
        {
            _logStatusMessageWarning(_logger, ex);
            messages.Add("Status unavailable");
        }

        return string.Join("; ", messages);
    }
}
