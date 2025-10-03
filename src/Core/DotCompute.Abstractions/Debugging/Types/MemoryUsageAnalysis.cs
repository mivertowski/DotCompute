// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging.Types;

/// <summary>
/// Comprehensive memory usage analysis for kernel execution.
/// Provides detailed insights into memory consumption patterns, efficiency, and optimization opportunities.
/// </summary>
public sealed class MemoryUsageAnalysis
{
    /// <summary>
    /// Gets or sets the kernel name being analyzed.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of execution samples analyzed.
    /// </summary>
    public int SampleCount { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage across all executions in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage observed in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the minimum memory usage observed in bytes.
    /// </summary>
    public long MinimumMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the memory efficiency score (0-1).
    /// Higher scores indicate more consistent and predictable memory usage.
    /// Calculated as 1 - coefficient_of_variation.
    /// </summary>
    public double MemoryEfficiencyScore { get; set; }

    /// <summary>
    /// Gets or sets the variance in memory usage across executions.
    /// </summary>
    public double MemoryVariance { get; set; }

    /// <summary>
    /// Gets or sets the standard deviation of memory usage.
    /// </summary>
    public double MemoryStandardDeviation { get; set; }

    /// <summary>
    /// Gets or sets the recommended memory allocation size for optimal performance.
    /// Based on statistical analysis of historical usage patterns.
    /// </summary>
    public long RecommendedMemoryAllocation { get; set; }

    /// <summary>
    /// Gets or sets the memory allocation overhead percentage.
    /// Indicates how much over-allocation occurs relative to actual usage.
    /// </summary>
    public double AllocationOverheadPercentage { get; set; }

    /// <summary>
    /// Gets or sets the memory fragmentation score (0-1).
    /// Higher scores indicate more fragmentation issues.
    /// </summary>
    public double FragmentationScore { get; set; }

    /// <summary>
    /// Gets or sets the memory pooling effectiveness score (0-1).
    /// Measures how effectively memory pooling reduces allocations.
    /// </summary>
    public double PoolingEffectivenessScore { get; set; }

    /// <summary>
    /// Gets or sets the analysis timestamp.
    /// </summary>
    public DateTime AnalysisTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the time window covered by this analysis.
    /// </summary>
    public TimeSpan AnalysisTimeWindow { get; set; }

    /// <summary>
    /// Gets or sets memory usage recommendations.
    /// </summary>
    public IList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets or sets detected memory access patterns.
    /// </summary>
    public IList<string> AccessPatterns { get; init; } = [];

    /// <summary>
    /// Gets or sets memory pressure indicators.
    /// </summary>
    public Dictionary<string, double> PressureIndicators { get; init; } = [];
}
