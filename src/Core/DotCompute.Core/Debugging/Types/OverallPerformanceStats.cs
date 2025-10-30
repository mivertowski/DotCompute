// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Debugging.Types;

/// <summary>
/// Overall performance statistics across all backends.
/// </summary>
public class OverallPerformanceStats
{
    /// <summary>
    /// Gets or sets the total executions.
    /// </summary>
    /// <value>The total executions.</value>
    public int TotalExecutions { get; set; }
    /// <summary>
    /// Gets or sets the successful executions.
    /// </summary>
    /// <value>The successful executions.</value>
    public int SuccessfulExecutions { get; set; }
    /// <summary>
    /// Gets or sets the failed executions.
    /// </summary>
    /// <value>The failed executions.</value>
    public int FailedExecutions { get; set; }
    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    /// <value>The average execution time.</value>
    public double AverageExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the total memory used.
    /// </summary>
    /// <value>The total memory used.</value>
    public long TotalMemoryUsed { get; set; }
}
