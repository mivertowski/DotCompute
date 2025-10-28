// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Historical performance statistics for a backend on a specific workload.
/// </summary>
public class BackendPerformanceStats
{
    /// <summary>
    /// Gets or sets the sample count.
    /// </summary>
    /// <value>The sample count.</value>
    public int SampleCount { get; set; }
    /// <summary>
    /// Gets or sets the average execution time ms.
    /// </summary>
    /// <value>The average execution time ms.</value>
    public double AverageExecutionTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the execution time std dev.
    /// </summary>
    /// <value>The execution time std dev.</value>
    public double ExecutionTimeStdDev { get; set; }
    /// <summary>
    /// Gets or sets the min execution time ms.
    /// </summary>
    /// <value>The min execution time ms.</value>
    public double MinExecutionTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the max execution time ms.
    /// </summary>
    /// <value>The max execution time ms.</value>
    public double MaxExecutionTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the average throughput.
    /// </summary>
    /// <value>The average throughput.</value>
    public double AverageThroughput { get; set; }
    /// <summary>
    /// Gets or sets the average memory usage.
    /// </summary>
    /// <value>The average memory usage.</value>
    public double AverageMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the reliability score.
    /// </summary>
    /// <value>The reliability score.</value>
    public float ReliabilityScore { get; set; } // 0-1 based on success rate
    /// <summary>
    /// Gets or sets the last updated.
    /// </summary>
    /// <value>The last updated.</value>
    public DateTimeOffset LastUpdated { get; set; }
}