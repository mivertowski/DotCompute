// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Analysis;

/// <summary>
/// Contains comprehensive analysis results for a performance profile.
/// Provides insights into execution patterns, bottlenecks, and optimization opportunities.
/// </summary>
public sealed class ProfileAnalysis
{
    /// <summary>
    /// Gets or sets the timestamp when this analysis was performed.
    /// Used for tracking when the analysis was generated and its freshness.
    /// </summary>
    /// <value>The analysis timestamp as a DateTimeOffset.</value>
    public DateTimeOffset AnalysisTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the total execution time for all operations in the profile.
    /// Represents the sum of all operation durations, including parallel operations.
    /// </summary>
    /// <value>The total execution time in milliseconds.</value>
    public double TotalExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the average kernel execution time across all kernels.
    /// Provides insight into the typical duration of compute operations.
    /// </summary>
    /// <value>The average kernel execution time in milliseconds.</value>
    public double AverageKernelExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the overall throughput in operations per second.
    /// Measures the aggregate processing rate across all operations.
    /// </summary>
    /// <value>The overall throughput in operations per second.</value>
    public double OverallThroughput { get; set; }

    /// <summary>
    /// Gets or sets the total amount of memory transferred during profiling.
    /// Indicates the scale of memory operations captured in the profile.
    /// </summary>
    /// <value>The total memory transferred in bytes.</value>
    public long TotalMemoryTransferred { get; set; }

    /// <summary>
    /// Gets or sets the average memory bandwidth across all memory operations.
    /// Provides insight into memory subsystem performance.
    /// </summary>
    /// <value>The average memory bandwidth in GB/s.</value>
    public double AverageMemoryBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the average occupancy percentage across all kernel executions.
    /// Indicates how effectively compute resources were utilized overall.
    /// </summary>
    /// <value>The average occupancy as a decimal (0.0 to 1.0).</value>
    public double AverageOccupancy { get; set; }

    /// <summary>
    /// Gets or sets the average cache hit rate across all operations.
    /// Measures the effectiveness of caching mechanisms during profiling.
    /// </summary>
    /// <value>The average cache hit rate as a decimal (0.0 to 1.0).</value>
    public double AverageCacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the device utilization efficiency score.
    /// Indicates how effectively the available device resources were used.
    /// </summary>
    /// <value>The device utilization efficiency as a decimal (0.0 to 1.0).</value>
    public double DeviceUtilizationEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the parallelism efficiency score.
    /// Indicates how effectively parallel processing capabilities were utilized.
    /// </summary>
    /// <value>The parallelism efficiency as a decimal (0.0 to 1.0).</value>
    public double ParallelismEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the list of identified performance bottlenecks.
    /// Each bottleneck includes location, severity, and impact information.
    /// </summary>
    /// <value>A list of bottleneck description strings.</value>
    public List<string> IdentifiedBottlenecks { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of optimization recommendations based on the analysis.
    /// Provides actionable suggestions for improving performance.
    /// </summary>
    /// <value>A list of optimization recommendation strings.</value>
    public List<string> OptimizationRecommendations { get; set; } = [];
}