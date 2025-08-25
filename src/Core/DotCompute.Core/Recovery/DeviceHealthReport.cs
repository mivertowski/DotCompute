// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery;

/// <summary>
/// Comprehensive health report for all monitored devices
/// </summary>
public class DeviceHealthReport
{
    public DateTimeOffset Timestamp { get; set; }
    public Dictionary<string, DeviceHealthStatus> DeviceHealth { get; set; } = [];
    public double OverallHealth { get; set; }
    public int ActiveKernels { get; set; }
    public long TotalRecoveryAttempts { get; set; }
    public double GlobalSuccessRate { get; set; }

    public override string ToString()
        => $"Health={OverallHealth:P2}, Devices={DeviceHealth.Count}, ActiveKernels={ActiveKernels}, SuccessRate={GlobalSuccessRate:P2}";
}