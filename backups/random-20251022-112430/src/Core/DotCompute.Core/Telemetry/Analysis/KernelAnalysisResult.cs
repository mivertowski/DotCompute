// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Telemetry.Enums;

namespace DotCompute.Core.Telemetry.Analysis;

/// <summary>
/// Contains comprehensive analysis results for a specific kernel's performance over time.
/// Provides statistical insights and trend analysis for kernel execution patterns.
/// </summary>
public sealed class KernelAnalysisResult
{
    /// <summary>
    /// Gets or sets the name of the kernel that was analyzed.
    /// Identifies the specific compute operation this analysis covers.
    /// </summary>
    /// <value>The kernel name as a string.</value>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status of the analysis operation.
    /// Indicates whether the analysis completed successfully or encountered issues.
    /// </summary>
    /// <value>The analysis status from the AnalysisStatus enumeration.</value>
    public AnalysisStatus Status { get; set; }

    /// <summary>
    /// Gets or sets an optional message describing the analysis status.
    /// Typically used to provide error details when status is not successful.
    /// </summary>
    /// <value>The status message as a string or null if not provided.</value>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the time window covered by this analysis.
    /// Indicates the duration of data that was analyzed.
    /// </summary>
    /// <value>The time window as a TimeSpan.</value>
    public TimeSpan TimeWindow { get; set; }

    /// <summary>
    /// Gets or sets the total number of kernel executions analyzed.
    /// Provides context for the statistical significance of the results.
    /// </summary>
    /// <value>The execution count as an integer.</value>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the average execution time across all analyzed executions.
    /// Provides a baseline measure of kernel performance.
    /// </summary>
    /// <value>The average execution time in milliseconds.</value>
    public double AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the minimum execution time observed.
    /// Indicates the best-case performance for this kernel.
    /// </summary>
    /// <value>The minimum execution time in milliseconds.</value>
    public double MinExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the maximum execution time observed.
    /// Indicates the worst-case performance for this kernel.
    /// </summary>
    /// <value>The maximum execution time in milliseconds.</value>
    public double MaxExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the standard deviation of execution times.
    /// Measures the variability in kernel performance.
    /// </summary>
    /// <value>The execution time standard deviation in milliseconds.</value>
    public double ExecutionTimeStdDev { get; set; }

    /// <summary>
    /// Gets or sets the average throughput across all executions.
    /// Measures the typical processing rate of the kernel.
    /// </summary>
    /// <value>The average throughput in operations per second.</value>
    public double AverageThroughput { get; set; }

    /// <summary>
    /// Gets or sets the average occupancy across all executions.
    /// Indicates how effectively the kernel utilized compute resources.
    /// </summary>
    /// <value>The average occupancy as a decimal (0.0 to 1.0).</value>
    public double AverageOccupancy { get; set; }

    /// <summary>
    /// Gets or sets the average cache hit rate across all executions.
    /// Measures the effectiveness of caching for this kernel.
    /// </summary>
    /// <value>The average cache hit rate as a decimal (0.0 to 1.0).</value>
    public double AverageCacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the average memory bandwidth utilization.
    /// Indicates how effectively the kernel used memory bandwidth.
    /// </summary>
    /// <value>The average memory bandwidth in GB/s.</value>
    public double AverageMemoryBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the average warp efficiency (GPU-specific).
    /// Measures how effectively SIMD execution units were utilized.
    /// </summary>
    /// <value>The average warp efficiency as a decimal (0.0 to 1.0).</value>
    public double AverageWarpEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the average branch divergence (GPU-specific).
    /// Measures the impact of conditional branches on performance.
    /// </summary>
    /// <value>The average branch divergence as a decimal (0.0 to 1.0).</value>
    public double AverageBranchDivergence { get; set; }

    /// <summary>
    /// Gets or sets the average memory coalescing efficiency.
    /// Measures how well memory accesses were optimized.
    /// </summary>
    /// <value>The average memory coalescing as a decimal (0.0 to 1.0).</value>
    public double AverageMemoryCoalescing { get; set; }

    /// <summary>
    /// Gets or sets the distribution of executions across different devices.
    /// Maps device IDs to the number of executions on each device.
    /// </summary>
    /// <value>A dictionary mapping device IDs to execution counts.</value>
    public Dictionary<string, int> DeviceDistribution { get; init; } = [];

    /// <summary>
    /// Gets or sets the performance trend observed over the analysis period.
    /// Indicates whether performance is improving, stable, or degrading.
    /// </summary>
    /// <value>The performance trend from the PerformanceTrend enumeration.</value>
    public PerformanceTrend PerformanceTrend { get; set; }

    /// <summary>
    /// Gets or sets the list of optimization recommendations specific to this kernel.
    /// Provides actionable suggestions for improving kernel performance.
    /// </summary>
    /// <value>A list of optimization recommendation strings.</value>
    public IList<string> OptimizationRecommendations { get; } = [];
}