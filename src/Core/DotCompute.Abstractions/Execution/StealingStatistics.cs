// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Execution;

/// <summary>
/// Statistics for work-stealing execution coordination.
/// Tracks stealing behavior and load balancing effectiveness.
/// </summary>
public class StealingStatistics
{
    /// <summary>
    /// Gets or sets the total number of steal attempts.
    /// </summary>
    public long TotalStealAttempts { get; set; }

    /// <summary>
    /// Gets or sets the number of successful steals.
    /// </summary>
    public long SuccessfulSteals { get; set; }

    /// <summary>
    /// Gets or sets the number of failed steal attempts.
    /// </summary>
    public long FailedSteals { get; set; }

    /// <summary>
    /// Gets or sets the steal success rate (0-1).
    /// </summary>
    public double StealSuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average time spent stealing work.
    /// </summary>
    public TimeSpan AverageStealTime { get; set; }

    /// <summary>
    /// Gets or sets the total work items stolen.
    /// </summary>
    public long TotalItemsStolen { get; set; }

    /// <summary>
    /// Gets or sets the load balance effectiveness score (0-1).
    /// Higher scores indicate better load distribution.
    /// </summary>
    public double LoadBalanceEffectiveness { get; set; }

    /// <summary>
    /// Gets or sets the average queue depth across all devices.
    /// </summary>
    public double AverageQueueDepth { get; set; }

    /// <summary>
    /// Gets or sets the maximum queue imbalance observed.
    /// </summary>
    public int MaxQueueImbalance { get; set; }

    /// <summary>
    /// Gets or sets per-device stealing statistics.
    /// </summary>
    public Dictionary<int, DeviceStealingStats> DeviceStats { get; init; } = [];
}

/// <summary>
/// Stealing statistics for a specific device.
/// </summary>
public sealed class DeviceStealingStats
{
    /// <summary>
    /// Gets or sets the device index.
    /// </summary>
    public int DeviceIndex { get; set; }

    /// <summary>
    /// Gets or sets work items stolen from this device.
    /// </summary>
    public long ItemsStolenFrom { get; set; }

    /// <summary>
    /// Gets or sets work items this device stole from others.
    /// </summary>
    public long ItemsStolenBy { get; set; }

    /// <summary>
    /// Gets or sets the net steal balance (stolen by - stolen from).
    /// Positive values indicate this device took more work, negative means it gave more work.
    /// </summary>
    public long NetStealBalance { get; set; }
}

/// <summary>
/// Alias for StealingStatistics to match naming convention.
/// </summary>
public sealed class WorkStealingStatistics : StealingStatistics
{
}
