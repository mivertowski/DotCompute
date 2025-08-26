// <copyright file="AnalysisTypes.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.CUDA.Analysis.Types;

/// <summary>
/// Represents coalescing analysis results for memory access patterns.
/// </summary>
public sealed class CoalescingAnalysis
{
    /// <summary>
    /// Gets or sets the coalescing efficiency percentage (0-100).
    /// </summary>
    public double EfficiencyPercent { get; set; }

    /// <summary>
    /// Gets or sets the number of memory transactions.
    /// </summary>
    public int TransactionCount { get; set; }

    /// <summary>
    /// Gets or sets the ideal number of transactions.
    /// </summary>
    public int IdealTransactionCount { get; set; }

    /// <summary>
    /// Gets or sets the memory access pattern detected.
    /// </summary>
    public MemoryAccessPattern AccessPattern { get; set; }

    /// <summary>
    /// Gets or sets identified coalescing issues.
    /// </summary>
    public List<CoalescingIssue> Issues { get; set; } = new();

    /// <summary>
    /// Gets or sets optimization recommendations.
    /// </summary>
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Represents strided access analysis results.
/// </summary>
public sealed class StridedAccessAnalysis
{
    /// <summary>
    /// Gets or sets the detected stride length.
    /// </summary>
    public int Stride { get; set; }

    /// <summary>
    /// Gets or sets the access regularity score (0-1).
    /// </summary>
    public double RegularityScore { get; set; }

    /// <summary>
    /// Gets or sets the cache efficiency for this stride pattern.
    /// </summary>
    public double CacheEfficiency { get; set; }

    /// <summary>
    /// Gets or sets whether the stride is optimal for the hardware.
    /// </summary>
    public bool IsOptimal { get; set; }

    /// <summary>
    /// Gets or sets suggested optimizations.
    /// </summary>
    public List<string> OptimizationSuggestions { get; set; } = new();
}

/// <summary>
/// Represents 2D matrix access pattern analysis.
/// </summary>
public sealed class Matrix2DAccessAnalysis
{
    /// <summary>
    /// Gets or sets the access pattern type (row-major, column-major, tiled).
    /// </summary>
    public string AccessPattern { get; set; } = "unknown";

    /// <summary>
    /// Gets or sets the tile size if tiled access is detected.
    /// </summary>
    public (int Width, int Height) TileSize { get; set; }

    /// <summary>
    /// Gets or sets the memory coalescing efficiency for this pattern.
    /// </summary>
    public double CoalescingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the cache hit ratio.
    /// </summary>
    public double CacheHitRatio { get; set; }

    /// <summary>
    /// Gets or sets whether the access pattern is cache-friendly.
    /// </summary>
    public bool IsCacheFriendly { get; set; }
}

/// <summary>
/// Represents runtime coalescing profile data.
/// </summary>
public sealed class RuntimeCoalescingProfile
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string? KernelName { get; set; }

    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the memory throughput in GB/s.
    /// </summary>
    public double MemoryThroughputGBs { get; set; }

    /// <summary>
    /// Gets or sets the coalescing analysis results.
    /// </summary>
    public CoalescingAnalysis? CoalescingResults { get; set; }

    /// <summary>
    /// Gets or sets performance counters.
    /// </summary>
    public Dictionary<string, long> PerformanceCounters { get; set; } = new();
}

/// <summary>
/// Represents coalescing comparison between different patterns.
/// </summary>
public sealed class CoalescingComparison
{
    /// <summary>
    /// Gets or sets the baseline coalescing analysis.
    /// </summary>
    public CoalescingAnalysis? Baseline { get; set; }

    /// <summary>
    /// Gets or sets the optimized coalescing analysis.
    /// </summary>
    public CoalescingAnalysis? Optimized { get; set; }

    /// <summary>
    /// Gets or sets the performance improvement percentage.
    /// </summary>
    public double ImprovementPercent { get; set; }

    /// <summary>
    /// Gets or sets the comparison summary.
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}