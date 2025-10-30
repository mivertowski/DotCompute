// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines.Enums;

namespace DotCompute.Core.Pipelines.Metrics;

/// <summary>
/// Contains metrics quantifying the impact of various optimization techniques applied to pipeline execution.
/// Provides insights into the effectiveness of different optimization strategies.
/// </summary>
public sealed class OptimizationImpactMetrics
{
    /// <summary>
    /// Gets the overall speedup factor achieved through optimization.
    /// Represents the ratio of optimized execution time to unoptimized time.
    /// </summary>
    /// <value>The speedup factor as a double (e.g., 2.5 means 2.5x faster).</value>
    public required double SpeedupFactor { get; init; }

    /// <summary>
    /// Gets the total memory savings achieved through optimization.
    /// Represents the reduction in memory usage compared to unoptimized execution.
    /// </summary>
    /// <value>The memory savings in bytes as a long integer.</value>
    public required long MemorySavingsBytes { get; init; }

    /// <summary>
    /// Gets the reduction in kernel launches achieved through optimization.
    /// Indicates the effectiveness of kernel fusion and batching techniques.
    /// </summary>
    /// <value>The reduction in kernel launches as an integer.</value>
    public required int ReducedKernelLaunches { get; init; }

    /// <summary>
    /// Gets the reduction in memory transfers achieved through optimization.
    /// Indicates the effectiveness of data locality and caching optimizations.
    /// </summary>
    /// <value>The reduction in memory transfers as an integer.</value>
    public required int ReducedMemoryTransfers { get; init; }

    /// <summary>
    /// Gets the breakdown of optimization impact by type.
    /// Maps each optimization type to its individual contribution to performance improvement.
    /// </summary>
    /// <value>A read-only dictionary mapping optimization types to their impact factors.</value>
    public required IReadOnlyDictionary<OptimizationType, double> OptimizationBreakdown { get; init; }
}
