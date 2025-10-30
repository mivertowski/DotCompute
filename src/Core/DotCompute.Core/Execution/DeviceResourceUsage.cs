// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution;

/// <summary>
/// Represents resource usage metrics for a specific device during execution.
/// </summary>
public class DeviceResourceUsage
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public required string DeviceId { get; set; }
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    /// <value>The end time.</value>
    public DateTimeOffset EndTime { get; set; }
    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    /// <value>The total execution time.</value>
    public TimeSpan TotalExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the initial memory usage.
    /// </summary>
    /// <value>The initial memory usage.</value>
    public long InitialMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the peak memory usage.
    /// </summary>
    /// <value>The peak memory usage.</value>
    public long PeakMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the average utilization.
    /// </summary>
    /// <value>The average utilization.</value>
    public double AverageUtilization { get; set; }
}
