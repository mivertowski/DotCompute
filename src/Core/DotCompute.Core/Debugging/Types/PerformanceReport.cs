// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Optimization.Performance;

namespace DotCompute.Core.Debugging.Types;

/// <summary>
/// Comprehensive performance report for kernel execution.
/// </summary>
public class PerformanceReport
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public required string KernelName { get; set; }
    /// <summary>
    /// Gets or sets the time window.
    /// </summary>
    /// <value>The time window.</value>
    public TimeSpan TimeWindow { get; set; }
    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    /// <value>The execution count.</value>
    public int ExecutionCount { get; set; }
    /// <summary>
    /// Gets or sets the backends.
    /// </summary>
    /// <value>The backends.</value>
    public Dictionary<string, BackendPerformanceStats> Backends { get; } = [];
    /// <summary>
    /// Gets or sets the overall stats.
    /// </summary>
    /// <value>The overall stats.</value>
    public OverallPerformanceStats? OverallStats { get; set; }
    /// <summary>
    /// Gets or sets the summary.
    /// </summary>
    /// <value>The summary.</value>
    public string Summary { get; set; } = string.Empty;
}