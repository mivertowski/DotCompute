// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines.Enums;
using DotCompute.Core.Pipelines.Metrics;

namespace DotCompute.Core.Pipelines.Interfaces;

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
    string PipelineId { get; }

    /// <summary>
    /// Gets the total number of pipeline executions recorded.
    /// Provides context for the statistical significance of the metrics.
    /// </summary>
    /// <value>The total execution count as a long integer.</value>
    long ExecutionCount { get; }

    /// <summary>
    /// Gets the number of successful pipeline executions.
    /// Used to calculate success rate and reliability metrics.
    /// </summary>
    /// <value>The successful execution count as a long integer.</value>
    long SuccessfulExecutionCount { get; }

    /// <summary>
    /// Gets the number of failed pipeline executions.
    /// Used to identify reliability issues and failure patterns.
    /// </summary>
    /// <value>The failed execution count as a long integer.</value>
    long FailedExecutionCount { get; }

    /// <summary>
    /// Gets the average execution time across all recorded executions.
    /// Provides a baseline measure of pipeline performance.
    /// </summary>
    /// <value>The average execution time as a TimeSpan.</value>
    TimeSpan AverageExecutionTime { get; }

    /// <summary>
    /// Gets the minimum execution time observed across all executions.
    /// Indicates the best-case performance for this pipeline.
    /// </summary>
    /// <value>The minimum execution time as a TimeSpan.</value>
    TimeSpan MinExecutionTime { get; }

    /// <summary>
    /// Gets the maximum execution time observed across all executions.
    /// Indicates the worst-case performance for this pipeline.
    /// </summary>
    /// <value>The maximum execution time as a TimeSpan.</value>
    TimeSpan MaxExecutionTime { get; }

    /// <summary>
    /// Gets the total cumulative execution time across all executions.
    /// Used for calculating resource utilization and efficiency metrics.
    /// </summary>
    /// <value>The total execution time as a TimeSpan.</value>
    TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Gets the throughput in executions per second.
    /// Measures the processing rate of the pipeline over time.
    /// </summary>
    /// <value>The throughput as a double representing executions per second.</value>
    double Throughput { get; }

    /// <summary>
    /// Gets the success rate as a percentage of successful executions.
    /// Indicates the reliability and stability of the pipeline.
    /// </summary>
    /// <value>The success rate as a double between 0.0 and 1.0.</value>
    double SuccessRate { get; }

    /// <summary>
    /// Gets the average memory usage across all executions.
    /// Provides insight into the memory footprint of the pipeline.
    /// </summary>
    /// <value>The average memory usage in bytes as a long integer.</value>
    long AverageMemoryUsage { get; }

    /// <summary>
    /// Gets the peak memory usage observed across all executions.
    /// Indicates the maximum memory requirements of the pipeline.
    /// </summary>
    /// <value>The peak memory usage in bytes as a long integer.</value>
    long PeakMemoryUsage { get; }

    /// <summary>
    /// Gets performance metrics broken down by individual pipeline stages.
    /// Allows identification of bottlenecks and optimization opportunities.
    /// </summary>
    /// <value>A read-only dictionary mapping stage names to their metrics.</value>
    IReadOnlyDictionary<string, IStageMetrics> StageMetrics { get; }

    /// <summary>
    /// Gets custom metrics specific to this pipeline implementation.
    /// Allows for domain-specific performance measurements.
    /// </summary>
    /// <value>A read-only dictionary mapping metric names to their values.</value>
    IReadOnlyDictionary<string, double> CustomMetrics { get; }

    /// <summary>
    /// Gets time series data showing performance metrics over time.
    /// Enables trend analysis and performance monitoring.
    /// </summary>
    /// <value>A read-only list of time series metric data points.</value>
    IReadOnlyList<TimeSeriesMetric> TimeSeries { get; }

    /// <summary>
    /// Resets all collected metrics to their initial state.
    /// Useful for starting fresh measurement periods.
    /// </summary>
    void Reset();

    /// <summary>
    /// Exports the collected metrics in the specified format.
    /// Enables integration with external monitoring and analysis tools.
    /// </summary>
    /// <param name="format">The desired export format for the metrics data.</param>
    /// <returns>A string representation of the metrics in the specified format.</returns>
    string Export(MetricsExportFormat format);
}