// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Summary of backend performance state for external consumption.
/// </summary>
public class BackendPerformanceStateSummary
{
    /// <summary>
    /// Gets or sets the backend identifier.
    /// </summary>
    /// <value>The backend id.</value>
    public string BackendId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the current utilization.
    /// </summary>
    /// <value>The current utilization.</value>
    public double CurrentUtilization { get; set; }
    /// <summary>
    /// Gets or sets the recent average execution time ms.
    /// </summary>
    /// <value>The recent average execution time ms.</value>
    public double RecentAverageExecutionTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the recent execution count.
    /// </summary>
    /// <value>The recent execution count.</value>
    public int RecentExecutionCount { get; set; }
    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    /// <value>The last execution time.</value>
    public DateTimeOffset LastExecutionTime { get; set; }
}
