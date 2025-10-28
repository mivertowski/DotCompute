// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Metrics;

namespace DotCompute.Abstractions.Interfaces.Pipelines.Interfaces;

/// <summary>
/// Interface for pipeline performance metrics collection and analysis.
/// Provides comprehensive insights into pipeline execution patterns and resource utilization.
/// </summary>
public interface IPipelineMetrics
{
    /// <summary>
    /// Gets the unique identifier for the pipeline being monitored.
    /// Used to correlate metrics across multiple pipeline executions.
    /// </summary>
    /// <value>The pipeline identifier as a string.</value>
    public string PipelineId { get; }

    /// <summary>
    /// Gets the total number of pipeline executions recorded.
    /// Provides context for the statistical significance of the metrics.
    /// </summary>
    /// <value>The total execution count as a long integer.</value>
    public long ExecutionCount { get; }

    /// <summary>
    /// Gets the number of successful pipeline executions.
    /// Used to calculate success rate and reliability metrics.
    /// </summary>
    /// <value>The successful execution count as a long integer.</value>
    public long SuccessfulExecutionCount { get; }

    /// <summary>
    /// Gets the number of failed pipeline executions.
    /// Used to identify reliability issues and failure patterns.
    /// </summary>
    /// <value>The failed execution count as a long integer.</value>
    public long FailedExecutionCount { get; }

    /// <summary>
    /// Gets the average execution time across all recorded executions.
    /// Provides a baseline measure of pipeline performance.
    /// </summary>
    /// <value>The average execution time as a TimeSpan.</value>
    public TimeSpan AverageExecutionTime { get; }

    /// <summary>
    /// Gets the minimum execution time observed across all executions.
    /// Indicates the best-case performance for this pipeline.
    /// </summary>
    /// <value>The minimum execution time as a TimeSpan.</value>
    public TimeSpan MinExecutionTime { get; }

    /// <summary>
    /// Gets the maximum execution time observed across all executions.
    /// Indicates the worst-case performance for this pipeline.
    /// </summary>
    /// <value>The maximum execution time as a TimeSpan.</value>
    public TimeSpan MaxExecutionTime { get; }

    /// <summary>
    /// Gets the total cumulative execution time across all executions.
    /// Used for calculating resource utilization and efficiency metrics.
    /// </summary>
    /// <value>The total execution time as a TimeSpan.</value>
    public TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Gets the throughput in executions per second.
    /// Measures the processing rate of the pipeline over time.
    /// </summary>
    /// <value>The throughput as a double representing executions per second.</value>
    public double Throughput { get; }

    /// <summary>
    /// Gets the success rate as a percentage of successful executions.
    /// Indicates the reliability and stability of the pipeline.
    /// </summary>
    /// <value>The success rate as a double between 0.0 and 1.0.</value>
    public double SuccessRate { get; }

    /// <summary>
    /// Gets the average memory usage across all executions.
    /// Provides insight into the memory footprint of the pipeline.
    /// </summary>
    /// <value>The average memory usage in bytes as a long integer.</value>
    public long AverageMemoryUsage { get; }

    /// <summary>
    /// Gets the peak memory usage observed across all executions.
    /// Indicates the maximum memory requirements of the pipeline.
    /// </summary>
    /// <value>The peak memory usage in bytes as a long integer.</value>
    public long PeakMemoryUsage { get; }

    /// <summary>
    /// Gets performance metrics broken down by individual pipeline stages.
    /// Allows identification of bottlenecks and optimization opportunities.
    /// </summary>
    /// <value>A read-only dictionary mapping stage names to their metrics.</value>
    public IReadOnlyDictionary<string, IStageMetrics> StageMetrics { get; }

    /// <summary>
    /// Gets custom metrics specific to this pipeline implementation.
    /// Allows for domain-specific performance measurements.
    /// </summary>
    /// <value>A read-only dictionary mapping metric names to their values.</value>
    public IReadOnlyDictionary<string, double> CustomMetrics { get; }

    /// <summary>
    /// Gets time series data showing performance metrics over time.
    /// Enables trend analysis and performance monitoring.
    /// </summary>
    /// <value>A read-only list of time series metric data points.</value>
    public IReadOnlyList<TimeSeriesMetric> TimeSeries { get; }

    /// <summary>
    /// Gets the total number of stages in the pipeline.
    /// Used for complexity analysis and performance optimization decisions.
    /// </summary>
    /// <value>The number of pipeline stages as an integer.</value>
    public int StageCount { get; }

    /// <summary>
    /// Gets the items processed per second across the entire pipeline.
    /// Provides high-level throughput measurement for capacity planning.
    /// </summary>
    /// <value>The throughput as items processed per second.</value>
    public double ItemThroughputPerSecond { get; }

    /// <summary>
    /// Gets the cache hit rate as a percentage (0.0 to 1.0).
    /// Indicates the effectiveness of caching strategies.
    /// </summary>
    /// <value>The cache hit ratio as a double between 0.0 and 1.0.</value>
    public double CacheHitRatio { get; }

    /// <summary>
    /// Resets all collected metrics to their initial state.
    /// Useful for starting fresh measurement periods.
    /// </summary>
    public void Reset();

    /// <summary>
    /// Exports the collected metrics in the specified format.
    /// Enables integration with external monitoring and analysis tools.
    /// </summary>
    /// <param name="format">The desired export format for the metrics data.</param>
    /// <returns>A string representation of the metrics in the specified format.</returns>
    public string Export(MetricsExportFormat format);
}
