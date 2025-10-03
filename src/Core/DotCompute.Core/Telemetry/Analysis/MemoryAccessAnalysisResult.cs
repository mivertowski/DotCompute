// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Telemetry.Enums;

namespace DotCompute.Core.Telemetry.Analysis;

/// <summary>
/// Contains comprehensive analysis results for memory access patterns and performance.
/// Provides insights into memory subsystem utilization and optimization opportunities.
/// </summary>
public sealed class MemoryAccessAnalysisResult
{
    /// <summary>
    /// Gets or sets the status of the memory access analysis operation.
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
    /// Gets or sets the total number of memory operations analyzed.
    /// Provides context for the statistical significance of the results.
    /// </summary>
    /// <value>The total operation count as an integer.</value>
    public int TotalOperations { get; set; }

    /// <summary>
    /// Gets or sets the average memory bandwidth across all operations.
    /// Provides a baseline measure of memory subsystem performance.
    /// </summary>
    /// <value>The average bandwidth in GB/s.</value>
    public double AverageBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the peak memory bandwidth observed.
    /// Indicates the maximum memory performance achieved.
    /// </summary>
    /// <value>The peak bandwidth in GB/s.</value>
    public double PeakBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the total amount of data transferred across all operations.
    /// Indicates the scale of memory activity analyzed.
    /// </summary>
    /// <value>The total bytes transferred as a long integer.</value>
    public long TotalBytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets the distribution of memory access patterns observed.
    /// Maps access pattern names to the number of operations using each pattern.
    /// </summary>
    /// <value>A dictionary mapping access patterns to operation counts.</value>
    public Dictionary<string, int> AccessPatternDistribution { get; } = [];

    /// <summary>
    /// Gets or sets the average memory coalescing efficiency.
    /// Measures how well memory accesses were optimized across all operations.
    /// </summary>
    /// <value>The average coalescing efficiency as a decimal (0.0 to 1.0).</value>
    public double AverageCoalescingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the average cache hit rate across all memory operations.
    /// Measures the effectiveness of caching mechanisms.
    /// </summary>
    /// <value>The average cache hit rate as a decimal (0.0 to 1.0).</value>
    public double AverageCacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the distribution of transfer directions observed.
    /// Maps transfer direction names to the number of operations in each direction.
    /// </summary>
    /// <value>A dictionary mapping transfer directions to operation counts.</value>
    public Dictionary<string, int> TransferDirectionDistribution { get; } = [];

    /// <summary>
    /// Gets or sets the bandwidth utilization per device.
    /// Maps device IDs to their memory bandwidth utilization percentages.
    /// </summary>
    /// <value>A dictionary mapping device IDs to bandwidth utilization.</value>
    public Dictionary<string, double> DeviceBandwidthUtilization { get; } = [];

    /// <summary>
    /// Gets or sets the memory segment usage statistics.
    /// Maps memory segment names to their detailed usage statistics.
    /// </summary>
    /// <value>A dictionary mapping segment names to usage statistics.</value>
    public Dictionary<string, MemorySegmentStats> MemorySegmentUsage { get; } = [];

    /// <summary>
    /// Gets or sets the list of optimization recommendations for memory access.
    /// Provides actionable suggestions for improving memory subsystem performance.
    /// </summary>
    /// <value>A list of optimization recommendation strings.</value>
    public IList<string> OptimizationRecommendations { get; } = [];
}