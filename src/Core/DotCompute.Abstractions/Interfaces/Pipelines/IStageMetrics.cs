// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces.Pipelines.Interfaces;

/// <summary>
/// Interface for individual pipeline stage performance metrics.
/// Provides detailed insights into the performance characteristics of specific pipeline stages.
/// </summary>
public interface IStageMetrics
{
    /// <summary>
    /// Gets the unique identifier for the pipeline stage being monitored.
    /// Used to correlate metrics to specific stages within the pipeline.
    /// </summary>
    /// <value>The stage identifier as a string.</value>
    public string StageId { get; }

    /// <summary>
    /// Gets the human-readable name of the pipeline stage.
    /// Provides context for understanding what operation this stage performs.
    /// </summary>
    /// <value>The stage name as a string.</value>
    public string StageName { get; }

    /// <summary>
    /// Gets the total number of times this stage has been executed.
    /// Provides context for the statistical significance of the metrics.
    /// </summary>
    /// <value>The execution count as a long integer.</value>
    public long ExecutionCount { get; }

    /// <summary>
    /// Gets the average execution time for this stage across all executions.
    /// Provides a baseline measure of stage performance.
    /// </summary>
    /// <value>The average execution time as a TimeSpan.</value>
    public TimeSpan AverageExecutionTime { get; }

    /// <summary>
    /// Gets the minimum execution time observed for this stage.
    /// Indicates the best-case performance for this stage.
    /// </summary>
    /// <value>The minimum execution time as a TimeSpan.</value>
    public TimeSpan MinExecutionTime { get; }

    /// <summary>
    /// Gets the maximum execution time observed for this stage.
    /// Indicates the worst-case performance for this stage.
    /// </summary>
    /// <value>The maximum execution time as a TimeSpan.</value>
    public TimeSpan MaxExecutionTime { get; }

    /// <summary>
    /// Gets the total cumulative execution time for this stage.
    /// Used for calculating resource utilization and identifying bottlenecks.
    /// </summary>
    /// <value>The total execution time as a TimeSpan.</value>
    public TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Gets the average memory usage for this stage across all executions.
    /// Provides insight into the memory footprint of this stage.
    /// </summary>
    /// <value>The average memory usage in bytes as a long integer.</value>
    public long AverageMemoryUsage { get; }

    /// <summary>
    /// Gets the success rate for this stage as a percentage of successful executions.
    /// Indicates the reliability and stability of this specific stage.
    /// </summary>
    /// <value>The success rate as a double between 0.0 and 1.0.</value>
    public double SuccessRate { get; }

    /// <summary>
    /// Gets the total number of errors that occurred during stage execution.
    /// Provides insight into the reliability of this stage.
    /// </summary>
    /// <value>The error count as a long integer.</value>
    public long ErrorCount { get; }

    /// <summary>
    /// Gets custom metrics specific to this pipeline stage.
    /// Allows for stage-specific performance measurements and analysis.
    /// </summary>
    /// <value>A read-only dictionary mapping metric names to their values.</value>
    public IReadOnlyDictionary<string, double> CustomMetrics { get; }
}