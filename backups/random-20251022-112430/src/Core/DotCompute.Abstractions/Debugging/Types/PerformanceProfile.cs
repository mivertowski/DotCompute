// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Debugging.Types;

/// <summary>
/// Comprehensive performance profile for a kernel across multiple executions.
/// Tracks historical performance data and trends for optimization guidance.
/// </summary>
public sealed class PerformanceProfile
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the backend type this profile is for.
    /// </summary>
    public string BackendType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the accelerator type.
    /// </summary>
    public AcceleratorType AcceleratorType { get; set; }

    /// <summary>
    /// Gets or sets the total number of executions profiled.
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
    /// Gets or sets the average execution time.
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
    public TimeSpan Percentile95ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile execution time.
    /// </summary>
    public TimeSpan Percentile99ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage observed in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the average throughput in operations per second.
    /// </summary>
    public double AverageThroughput { get; set; }

    /// <summary>
    /// Gets or sets the peak throughput observed in operations per second.
    /// </summary>
    public double PeakThroughput { get; set; }

    /// <summary>
    /// Gets or sets the first execution timestamp.
    /// </summary>
    public DateTime FirstExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the most recent execution timestamp.
    /// </summary>
    public DateTime LastExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the time span covered by this profile.
    /// </summary>
    public TimeSpan ProfileTimeSpan { get; set; }

    /// <summary>
    /// Gets or sets the performance trend direction.
    /// </summary>
    public TrendDirection PerformanceTrend { get; set; }

    /// <summary>
    /// Gets or sets the trend confidence score (0-1).
    /// </summary>
    public double TrendConfidence { get; set; }

    /// <summary>
    /// Gets or sets detected performance anomalies.
    /// </summary>
    public IList<PerformanceAnomaly> Anomalies { get; init; } = [];

    /// <summary>
    /// Gets or sets identified bottlenecks.
    /// </summary>
    public IList<BottleneckType> Bottlenecks { get; init; } = [];

    /// <summary>
    /// Gets or sets optimization recommendations.
    /// </summary>
    public IList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets or sets the profile metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
