// <copyright file="AnalysisTypes.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Types;
using DotCompute.Abstractions;

namespace DotCompute.Backends.CUDA.Analysis.Types;

/// <summary>
/// Represents coalescing analysis results for memory access patterns.
/// </summary>
public sealed class CoalescingAnalysis
{
    /// <summary>
    /// Gets or sets the name of the kernel being analyzed.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the analysis was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the coalescing efficiency percentage (0-100).
    /// </summary>
    public double EfficiencyPercent { get; set; }

    /// <summary>
    /// Gets or sets the coalescing efficiency as a ratio (0-1).
    /// </summary>
    public double CoalescingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the amount of wasted bandwidth in bytes.
    /// </summary>
    public long WastedBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the optimal access size for this memory pattern.
    /// </summary>
    public int OptimalAccessSize { get; set; }

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

    /// <summary>
    /// Gets or sets performance optimization suggestions specific to this analysis.
    /// </summary>
    public List<string> Optimizations { get; set; } = new();
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
    /// Gets or sets the element size in bytes.
    /// </summary>
    public int ElementSize { get; set; }

    /// <summary>
    /// Gets or sets the number of threads accessing memory.
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Gets or sets the number of memory transactions per warp.
    /// </summary>
    public int TransactionsPerWarp { get; set; }

    /// <summary>
    /// Gets or sets the memory access efficiency (0-1).
    /// </summary>
    public double Efficiency { get; set; }

    /// <summary>
    /// Gets or sets whether the access pattern is coalesced.
    /// </summary>
    public bool IsCoalesced { get; set; }

    /// <summary>
    /// Gets or sets the bandwidth utilization percentage.
    /// </summary>
    public double BandwidthUtilization { get; set; }

    /// <summary>
    /// Gets or sets optimization recommendations.
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

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
    /// Gets or sets the number of rows in the matrix.
    /// </summary>
    public int Rows { get; set; }

    /// <summary>
    /// Gets or sets the number of columns in the matrix.
    /// </summary>
    public int Columns { get; set; }

    /// <summary>
    /// Gets or sets the memory access order.
    /// </summary>
    public AccessOrder AccessOrder { get; set; }

    /// <summary>
    /// Gets or sets the element size in bytes.
    /// </summary>
    public int ElementSize { get; set; }

    /// <summary>
    /// Gets or sets the block dimension X.
    /// </summary>
    public int BlockDimX { get; set; }

    /// <summary>
    /// Gets or sets the block dimension Y.
    /// </summary>
    public int BlockDimY { get; set; }

    /// <summary>
    /// Gets or sets whether the access pattern is optimal.
    /// </summary>
    public bool IsOptimal { get; set; }

    /// <summary>
    /// Gets or sets the coalescing factor for the access pattern.
    /// </summary>
    public double CoalescingFactor { get; set; }

    /// <summary>
    /// Gets or sets the number of memory transactions per block.
    /// </summary>
    public int TransactionsPerBlock { get; set; }

    /// <summary>
    /// Gets or sets the bandwidth efficiency percentage.
    /// </summary>
    public double BandwidthEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the tile analysis results.
    /// </summary>
    public TileAnalysis TileAnalysis { get; set; } = new();

    /// <summary>
    /// Gets or sets optimization recommendations.
    /// </summary>
    public List<string> Optimizations { get; set; } = new();

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

/// <summary>
/// Represents memory access order enumeration.
/// </summary>
public enum AccessOrder
{
    /// <summary>
    /// Row-major access pattern.
    /// </summary>
    RowMajor,

    /// <summary>
    /// Column-major access pattern.
    /// </summary>
    ColumnMajor,

    /// <summary>
    /// Tiled access pattern.
    /// </summary>
    Tiled
}

