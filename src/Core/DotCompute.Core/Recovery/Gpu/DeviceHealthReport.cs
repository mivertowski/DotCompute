// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Gpu;

/// <summary>
/// Provides a comprehensive health report for all monitored GPU devices in the system.
/// Aggregates device-specific health data into system-wide metrics and statistics.
/// </summary>
/// <remarks>
/// This class serves as the primary data structure for system-wide GPU health monitoring.
/// It combines individual device health information with global metrics to provide
/// a complete picture of the GPU subsystem's operational status.
/// </remarks>
public class DeviceHealthReport
{
    /// <summary>
    /// Gets or sets the timestamp when this health report was generated.
    /// </summary>
    /// <value>The UTC timestamp of report generation.</value>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the health status information for each monitored device.
    /// </summary>
    /// <value>A dictionary mapping device IDs to their respective health status information.</value>
    /// <remarks>
    /// The key is the device identifier, and the value contains comprehensive
    /// health metrics for that specific device.
    /// </remarks>
    public Dictionary<string, DeviceHealthStatus> DeviceHealth { get; } = [];

    /// <summary>
    /// Gets or sets the overall health score for the entire GPU subsystem.
    /// </summary>
    /// <value>A value between 0.0 and 1.0 representing the overall system health (1.0 = perfect health).</value>
    /// <remarks>
    /// This metric is calculated based on the health status of all devices,
    /// their error rates, and recovery success rates. It provides a quick
    /// assessment of the entire GPU subsystem's operational status.
    /// </remarks>
    public double OverallHealth { get; set; }

    /// <summary>
    /// Gets or sets the current number of active kernel executions across all devices.
    /// </summary>
    /// <value>The count of kernels currently executing on monitored devices.</value>
    /// <remarks>
    /// This metric helps understand the current workload and can be used
    /// to correlate health issues with system load.
    /// </remarks>
    public int ActiveKernels { get; set; }

    /// <summary>
    /// Gets or sets the total number of recovery attempts across all devices.
    /// </summary>
    /// <value>The cumulative count of all recovery operations attempted system-wide.</value>
    public long TotalRecoveryAttempts { get; set; }

    /// <summary>
    /// Gets or sets the global success rate for recovery operations across all devices.
    /// </summary>
    /// <value>A value between 0.0 and 1.0 representing the percentage of successful recoveries.</value>
    /// <remarks>
    /// This metric is calculated as the ratio of successful recoveries to total
    /// recovery attempts across all monitored devices. It provides insight into
    /// the effectiveness of the recovery system.
    /// </remarks>
    public double GlobalSuccessRate { get; set; }

    /// <summary>
    /// Returns a string representation of the key health metrics in the report.
    /// </summary>
    /// <returns>A formatted string containing the main health indicators.</returns>
    /// <remarks>
    /// The string includes overall health percentage, number of devices,
    /// active kernel count, and global success rate formatted for readability.
    /// </remarks>
    public override string ToString()
        => $"Health={OverallHealth:P2}, Devices={DeviceHealth.Count}, ActiveKernels={ActiveKernels}, SuccessRate={GlobalSuccessRate:P2}";
}