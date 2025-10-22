// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Performance;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Debugging.Types;

/// <summary>
/// Comprehensive performance report for kernel execution analysis.
/// Aggregates performance metrics, trends, and recommendations over a time window.
/// </summary>
public sealed class PerformanceReport
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the time window covered by this report.
    /// </summary>
    public TimeSpan AnalysisTimeWindow { get; set; }

    /// <summary>
    /// Gets or sets the total number of executions analyzed.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the number of successful executions.
    /// </summary>
    public int SuccessfulExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions.
    /// </summary>
    public int FailedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the success rate (0-1).
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average execution time across all runs.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the minimum execution time observed.
    /// </summary>
    public TimeSpan MinExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the maximum execution time observed.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the median execution time.
    /// </summary>
    public TimeSpan MedianExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the standard deviation of execution times.
    /// </summary>
    public TimeSpan ExecutionTimeStdDev { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile execution time.
    /// </summary>
    public TimeSpan Percentile95 { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile execution time.
    /// </summary>
    public TimeSpan Percentile99 { get; set; }

    /// <summary>
    /// Gets or sets the average throughput in operations per second.
    /// </summary>
    public double AverageThroughput { get; set; }

    /// <summary>
    /// Gets or sets the peak throughput observed.
    /// </summary>
    public double PeakThroughput { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the overall performance score (0-100).
    /// </summary>
    public double PerformanceScore { get; set; }

    /// <summary>
    /// Gets or sets the performance trend direction.
    /// </summary>
    public TrendDirection PerformanceTrend { get; set; }

    /// <summary>
    /// Gets or sets backend-specific performance metrics.
    /// </summary>
    public Dictionary<string, PerformanceMetrics> BackendMetrics { get; init; } = [];

    /// <summary>
    /// Gets or sets detected performance anomalies.
    /// </summary>
    public IList<PerformanceAnomaly> Anomalies { get; init; } = [];

    /// <summary>
    /// Gets or sets identified bottlenecks.
    /// </summary>
    public IList<string> Bottlenecks { get; init; } = [];

    /// <summary>
    /// Gets or sets performance recommendations.
    /// </summary>
    public IList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets or sets performance trends over time.
    /// </summary>
    public IList<PerformanceTrend> Trends { get; init; } = [];

    /// <summary>
    /// Gets or sets additional report metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
