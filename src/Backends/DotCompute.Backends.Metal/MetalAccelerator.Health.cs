// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using DotCompute.Abstractions.Health;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Telemetry;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Accelerators;

/// <summary>
/// Metal accelerator health monitoring implementation.
/// </summary>
public sealed partial class MetalAccelerator
{
    /// <summary>
    /// Gets a comprehensive health snapshot of the Metal device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the device health snapshot.</returns>
    /// <remarks>
    /// <para>
    /// This method integrates with the existing Metal telemetry and health monitoring system.
    /// Unlike CUDA which can query detailed hardware metrics via NVML, Metal provides limited
    /// hardware introspection capabilities.
    /// </para>
    ///
    /// <para>
    /// <b>Available Metrics:</b>
    /// - Overall health status from circuit breakers and error tracking
    /// - Memory pressure levels (system-wide, not GPU-specific)
    /// - Component health (memory, device, kernel, compiler)
    /// - Error counts and consecutive failures
    /// </para>
    ///
    /// <para>
    /// <b>Limitations on Apple Silicon:</b>
    /// - No detailed power consumption metrics
    /// - No clock frequency reporting
    /// - No PCIe metrics (unified memory architecture)
    /// - Limited thermal sensors (OS-level only)
    /// - No per-GPU memory metrics (unified memory)
    /// </para>
    ///
    /// <para>
    /// <b>Performance:</b> Typically takes less than 1ms as it queries in-memory telemetry data
    /// rather than hardware sensors.
    /// </para>
    /// </remarks>
    public override ValueTask<DeviceHealthSnapshot> GetHealthSnapshotAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // If telemetry is not enabled, return basic unavailable snapshot
        if (_telemetryManager == null)
        {
            return ValueTask.FromResult(DeviceHealthSnapshot.CreateUnavailable(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "Metal",
                reason: "Metal telemetry not enabled - configure MetalTelemetryOptions to enable health monitoring"
            ));
        }

        try
        {
            // Get comprehensive telemetry report
            var telemetryReport = _telemetryManager.GenerateProductionReport();
            var healthReport = _telemetryManager.HealthMonitor?.GetDetailedHealthReport();

            if (healthReport == null)
            {
                return ValueTask.FromResult(DeviceHealthSnapshot.CreateUnavailable(
                    deviceId: Info.Id,
                    deviceName: Info.Name,
                    backendType: "Metal",
                    reason: "Metal health monitor not available"
                ));
            }

            // Convert Metal health status to unified health status
            var status = ConvertHealthStatus(healthReport.OverallHealth);

            // Convert 0-100 health score to 0.0-1.0 scale
            var healthScore = ConvertHealthScore(telemetryReport.Snapshot, telemetryReport);

            // Build sensor readings from available metrics
            var sensorReadings = BuildSensorReadings(telemetryReport, healthReport);

            // Build status message
            var statusMessage = BuildStatusMessage(healthReport, telemetryReport);

            // Get error counts from telemetry
            var errorCount = (int)telemetryReport.Snapshot.TotalErrors;

            // Estimate consecutive failures from recent error patterns
            var consecutiveFailures = EstimateConsecutiveFailures(healthReport);

            // Check if throttling (high memory pressure or critical component health)
            var isThrottling = IsThrottling(healthReport);

            var snapshot = new DeviceHealthSnapshot
            {
                DeviceId = Info.Id,
                DeviceName = Info.Name,
                BackendType = "Metal",
                Timestamp = DateTimeOffset.UtcNow,
                HealthScore = healthScore,
                Status = status,
                IsAvailable = true,
                SensorReadings = sensorReadings,
                ErrorCount = errorCount,
                ConsecutiveFailures = consecutiveFailures,
                IsThrottling = isThrottling,
                StatusMessage = statusMessage
            };

            return ValueTask.FromResult(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect health snapshot for Metal device {DeviceName}", Info.Name);
            return ValueTask.FromResult(DeviceHealthSnapshot.CreateUnavailable(
                deviceId: Info.Id,
                deviceName: Info.Name,
                backendType: "Metal",
                reason: $"Error collecting metrics: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// Gets current sensor readings from the Metal device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the collection of sensor readings.</returns>
    /// <remarks>
    /// Metal provides fewer hardware sensors than CUDA due to Apple's platform restrictions.
    /// Available sensors focus on system-level metrics and component health rather than
    /// low-level hardware metrics.
    /// </remarks>
    public override ValueTask<IReadOnlyList<SensorReading>> GetSensorReadingsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_telemetryManager == null)
        {
            return ValueTask.FromResult<IReadOnlyList<SensorReading>>(Array.Empty<SensorReading>());
        }

        try
        {
            var telemetryReport = _telemetryManager.GenerateProductionReport();
            var healthReport = _telemetryManager.HealthMonitor?.GetDetailedHealthReport();

            if (healthReport == null)
            {
                return ValueTask.FromResult<IReadOnlyList<SensorReading>>(Array.Empty<SensorReading>());
            }

            var readings = BuildSensorReadings(telemetryReport, healthReport);
            return ValueTask.FromResult<IReadOnlyList<SensorReading>>(readings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect sensor readings for Metal device {DeviceName}", Info.Name);
            return ValueTask.FromResult<IReadOnlyList<SensorReading>>(Array.Empty<SensorReading>());
        }
    }

    /// <summary>
    /// Converts Metal HealthStatus to unified DeviceHealthStatus.
    /// </summary>
    private static DeviceHealthStatus ConvertHealthStatus(HealthStatus metalStatus)
    {
        return metalStatus switch
        {
            HealthStatus.Healthy => DeviceHealthStatus.Healthy,
            HealthStatus.Degraded => DeviceHealthStatus.Warning,
            HealthStatus.Critical => DeviceHealthStatus.Critical,
            _ => DeviceHealthStatus.Unknown
        };
    }

    /// <summary>
    /// Converts Metal health metrics to unified 0.0-1.0 scale.
    /// </summary>
    private static double ConvertHealthScore(MetalTelemetrySnapshot snapshot, MetalProductionReport report)
    {
        // Metal health system uses HealthStatus enum, convert to numeric score
        var baseScore = snapshot.HealthStatus switch
        {
            HealthStatus.Healthy => 0.95,
            HealthStatus.Degraded => 0.70,
            HealthStatus.Critical => 0.40,
            _ => 0.50
        };

        // Adjust based on error rate
        var totalOperations = snapshot.TotalOperations;
        var errorRate = totalOperations > 0 ? (double)snapshot.TotalErrors / totalOperations : 0.0;

        // Penalize based on error rate
        var errorPenalty = Math.Min(0.3, errorRate * 2.0); // Up to 30% penalty
        baseScore -= errorPenalty;

        // Calculate success rate from operation metrics
        var successRate = CalculateSuccessRate(snapshot);

        // Add performance bonus for high success rate
        var performanceBonus = Math.Min(0.05, (successRate - 0.95) * 0.5); // Up to 5% bonus
        baseScore += performanceBonus;

        return Math.Clamp(baseScore, 0.0, 1.0);
    }

    /// <summary>
    /// Calculates overall success rate from operation metrics.
    /// </summary>
    private static double CalculateSuccessRate(MetalTelemetrySnapshot snapshot)
    {
        if (snapshot.OperationMetrics.Count == 0)
        {
            return 1.0;
        }

        var totalExecutions = snapshot.OperationMetrics.Values.Sum(m => m.TotalExecutions);
        var successfulExecutions = snapshot.OperationMetrics.Values.Sum(m => m.SuccessfulExecutions);

        return totalExecutions > 0 ? (double)successfulExecutions / totalExecutions : 1.0;
    }

    /// <summary>
    /// Builds sensor readings from Metal telemetry and health data.
    /// </summary>
    private static IReadOnlyList<SensorReading> BuildSensorReadings(MetalProductionReport telemetryReport, MetalHealthReport healthReport)
    {
        var readings = new List<SensorReading>(8);

        // System memory metrics (Metal uses unified memory on Apple Silicon)
        if (healthReport.SystemMetrics.TryGetValue("total_memory_bytes", out var totalMemoryObj))
        {
            var totalMemory = Convert.ToDouble(totalMemoryObj, CultureInfo.InvariantCulture);

            if (healthReport.SystemMetrics.TryGetValue("working_set_bytes", out var workingSetObj))
            {
                var workingSet = Convert.ToDouble(workingSetObj, CultureInfo.InvariantCulture);
                var memoryUsed = workingSet; // Working set approximates used memory
                var memoryFree = Math.Max(0, totalMemory - memoryUsed);

                readings.Add(SensorReading.Create(
                    SensorType.MemoryUsedBytes,
                    memoryUsed,
                    minValue: 0.0,
                    maxValue: totalMemory,
                    name: "System Memory Used"
                ));

                readings.Add(SensorReading.Create(
                    SensorType.MemoryTotalBytes,
                    totalMemory,
                    name: "Total System Memory"
                ));

                readings.Add(SensorReading.Create(
                    SensorType.MemoryFreeBytes,
                    memoryFree,
                    minValue: 0.0,
                    maxValue: totalMemory,
                    name: "Free System Memory"
                ));

                // Memory utilization percentage
                var memoryUtilization = (memoryUsed / totalMemory) * 100.0;
                readings.Add(SensorReading.Create(
                    SensorType.MemoryUtilization,
                    memoryUtilization,
                    minValue: 0.0,
                    maxValue: 100.0,
                    name: "Memory Utilization"
                ));
            }
        }

        // Compute utilization (estimated from operation metrics)
        var avgExecutionTime = CalculateAverageExecutionTime(telemetryReport.Snapshot);
        var totalOps = telemetryReport.Snapshot.TotalOperations;

        // Estimate utilization based on operation frequency and duration
        // Higher operation count with lower average duration suggests efficient utilization
        var estimatedUtilization = totalOps > 0 && avgExecutionTime.TotalMilliseconds > 0
            ? Math.Min(100.0, (totalOps / Math.Max(1.0, avgExecutionTime.TotalMilliseconds)) * 10.0)
            : 0.0;

        readings.Add(SensorReading.Create(
            SensorType.ComputeUtilization,
            estimatedUtilization,
            minValue: 0.0,
            maxValue: 100.0,
            name: "Estimated GPU Utilization"
        ));

        // Component health metrics (Memory, Device, Kernel) as custom sensors
        AddComponentHealthSensors(readings, healthReport);

        return readings;
    }

    /// <summary>
    /// Calculates average execution time across all operations.
    /// </summary>
    private static TimeSpan CalculateAverageExecutionTime(MetalTelemetrySnapshot snapshot)
    {
        if (snapshot.OperationMetrics.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var totalExecutions = snapshot.OperationMetrics.Values.Sum(m => m.TotalExecutions);
        var totalTime = snapshot.OperationMetrics.Values.Aggregate(TimeSpan.Zero, (acc, m) => acc + m.TotalExecutionTime);

        return totalExecutions > 0
            ? TimeSpan.FromMilliseconds(totalTime.TotalMilliseconds / totalExecutions)
            : TimeSpan.Zero;
    }

    /// <summary>
    /// Adds component health as custom sensors.
    /// </summary>
    private static void AddComponentHealthSensors(List<SensorReading> readings, MetalHealthReport healthReport)
    {
        if (healthReport.ComponentHealthMap.TryGetValue("Memory", out var memoryHealth))
        {
            var memoryHealthValue = ConvertComponentHealthToValue(memoryHealth);
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                memoryHealthValue,
                minValue: 0.0,
                maxValue: 100.0,
                name: "Memory Component Health"
            ));
        }

        if (healthReport.ComponentHealthMap.TryGetValue("Device", out var deviceHealth))
        {
            var deviceHealthValue = ConvertComponentHealthToValue(deviceHealth);
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                deviceHealthValue,
                minValue: 0.0,
                maxValue: 100.0,
                name: "Device Component Health"
            ));
        }

        if (healthReport.ComponentHealthMap.TryGetValue("Kernel", out var kernelHealth))
        {
            var kernelHealthValue = ConvertComponentHealthToValue(kernelHealth);
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                kernelHealthValue,
                minValue: 0.0,
                maxValue: 100.0,
                name: "Kernel Component Health"
            ));
        }
    }

    /// <summary>
    /// Converts component health to a numeric value (0-100).
    /// </summary>
    private static double ConvertComponentHealthToValue(ComponentHealth componentHealth)
    {
        return componentHealth.Status switch
        {
            HealthStatus.Healthy => 100.0,
            HealthStatus.Degraded => 60.0,
            HealthStatus.Critical => 20.0,
            _ => 50.0
        };
    }

    /// <summary>
    /// Builds human-readable status message.
    /// </summary>
    private static string BuildStatusMessage(MetalHealthReport healthReport, MetalProductionReport telemetryReport)
    {
        var messages = new List<string>();

        if (healthReport.OverallHealth == HealthStatus.Healthy)
        {
            messages.Add("Operating normally");
        }

        // Check for high error rate
        var errorRate = telemetryReport.Snapshot.TotalOperations > 0
            ? (double)telemetryReport.Snapshot.TotalErrors / telemetryReport.Snapshot.TotalOperations
            : 0.0;

        if (errorRate > 0.05) // > 5% error rate
        {
            messages.Add($"High error rate: {errorRate:P1}");
        }

        // Check circuit breaker states
        if (healthReport.CircuitBreakerStates.TryGetValue("Memory", out var memoryState) && memoryState == CircuitBreakerState.Open)
        {
            messages.Add("Memory circuit breaker open");
        }

        if (healthReport.CircuitBreakerStates.TryGetValue("Device", out var deviceState) && deviceState == CircuitBreakerState.Open)
        {
            messages.Add("Device circuit breaker open");
        }

        if (healthReport.CircuitBreakerStates.TryGetValue("Kernel", out var kernelState) && kernelState == CircuitBreakerState.Open)
        {
            messages.Add("Kernel circuit breaker open");
        }

        // Add recommendations if any
        if (healthReport.Recommendations.Count > 0)
        {
            messages.Add($"{healthReport.Recommendations.Count} health recommendations available");
        }

        return messages.Count > 0 ? string.Join("; ", messages) : "Operating normally";
    }

    /// <summary>
    /// Estimates consecutive failures from recent error patterns.
    /// </summary>
    private static int EstimateConsecutiveFailures(MetalHealthReport healthReport)
    {
        var recentErrors = healthReport.RecentEvents
            .Where(e => e.EventType == HealthEventType.Error)
            .OrderByDescending(e => e.Timestamp)
            .Take(10)
            .ToList();

        if (recentErrors.Count == 0)
        {
            return 0;
        }

        // Count consecutive errors from most recent
        var consecutiveCount = 0;
        var lastEventTime = DateTimeOffset.MaxValue;

        foreach (var error in recentErrors)
        {
            // Consider events within 1 minute as consecutive
            if (lastEventTime == DateTimeOffset.MaxValue || (lastEventTime - error.Timestamp) < TimeSpan.FromMinutes(1))
            {
                consecutiveCount++;
                lastEventTime = error.Timestamp;
            }
            else
            {
                break;
            }
        }

        return consecutiveCount;
    }

    /// <summary>
    /// Determines if the device is throttling based on circuit breakers and component health.
    /// </summary>
    private static bool IsThrottling(MetalHealthReport healthReport)
    {
        // Check if any circuit breakers are open (indicating throttling to prevent failures)
        var hasOpenCircuitBreaker = healthReport.CircuitBreakerStates.Values.Any(state => state == CircuitBreakerState.Open);

        // Check for critical component health
        var hasCriticalComponent = healthReport.ComponentHealthMap.Values.Any(c => c.Status == HealthStatus.Critical);

        return hasOpenCircuitBreaker || hasCriticalComponent;
    }
}
