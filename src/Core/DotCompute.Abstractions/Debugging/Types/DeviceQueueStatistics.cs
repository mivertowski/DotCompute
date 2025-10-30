// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging.Types;

/// <summary>
/// Statistics for device work queue performance and utilization.
/// Provides metrics for monitoring queue health and load balancing effectiveness.
/// </summary>
public sealed class DeviceQueueStatistics
{
    /// <summary>
    /// Gets or sets the device index this queue belongs to.
    /// </summary>
    public int DeviceIndex { get; set; }

    /// <summary>
    /// Gets or sets the current size of the work queue.
    /// </summary>
    public int CurrentQueueSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of work items enqueued since creation.
    /// </summary>
    public long TotalEnqueued { get; set; }

    /// <summary>
    /// Gets or sets the total number of work items dequeued.
    /// </summary>
    public long TotalDequeued { get; set; }

    /// <summary>
    /// Gets or sets the total number of work items stolen by other devices.
    /// </summary>
    public long TotalStolen { get; set; }

    /// <summary>
    /// Gets or sets the peak queue size observed.
    /// </summary>
    public int PeakQueueSize { get; set; }

    /// <summary>
    /// Gets or sets the average queue depth over time.
    /// </summary>
    public double AverageQueueDepth { get; set; }

    /// <summary>
    /// Gets or sets the total wait time for work items in the queue.
    /// </summary>
    public TimeSpan TotalWaitTime { get; set; }

    /// <summary>
    /// Gets or sets the average wait time per work item.
    /// </summary>
    public TimeSpan AverageWaitTime { get; set; }

    /// <summary>
    /// Gets or sets the queue utilization percentage (0-100).
    /// </summary>
    public double UtilizationPercentage { get; set; }

    /// <summary>
    /// Gets or sets the steal rate (steals per second).
    /// </summary>
    public double StealRate { get; set; }

    /// <summary>
    /// Gets or sets the throughput in work items per second.
    /// </summary>
    public double ThroughputPerSecond { get; set; }
}
