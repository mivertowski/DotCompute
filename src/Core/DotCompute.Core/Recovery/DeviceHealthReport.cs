// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery;

/// <summary>
/// Comprehensive health report for all monitored devices
/// </summary>
public class DeviceHealthReport
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the device health.
    /// </summary>
    /// <value>The device health.</value>
    public Dictionary<string, DeviceHealthStatus> DeviceHealth { get; } = [];
    /// <summary>
    /// Gets or sets the overall health.
    /// </summary>
    /// <value>The overall health.</value>
    public double OverallHealth { get; set; }
    /// <summary>
    /// Gets or sets the active kernels.
    /// </summary>
    /// <value>The active kernels.</value>
    public int ActiveKernels { get; set; }
    /// <summary>
    /// Gets or sets the total recovery attempts.
    /// </summary>
    /// <value>The total recovery attempts.</value>
    public long TotalRecoveryAttempts { get; set; }
    /// <summary>
    /// Gets or sets the global success rate.
    /// </summary>
    /// <value>The global success rate.</value>
    public double GlobalSuccessRate { get; set; }
    /// <summary>
    /// Gets to string.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override string ToString()
        => $"Health={OverallHealth:P2}, Devices={DeviceHealth.Count}, ActiveKernels={ActiveKernels}, SuccessRate={GlobalSuccessRate:P2}";
}