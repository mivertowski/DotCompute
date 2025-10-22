// <copyright file="CoalescingAnalysis.cs" company="DotCompute Project">
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
    /// Gets or initializes identified coalescing issues.
    /// </summary>
    public IList<CoalescingIssue> Issues { get; init; } = [];

    /// <summary>
    /// Gets or initializes optimization recommendations.
    /// </summary>
    public IList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets or initializes performance optimization suggestions specific to this analysis.
    /// </summary>
    public IList<string> Optimizations { get; init; } = [];

    /// <summary>
    /// Gets or sets the actual bytes transferred during memory operations.
    /// </summary>
    public long ActualBytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets the useful bytes transferred (without wasted bandwidth).
    /// </summary>
    public long UsefulBytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets architecture-specific notes and observations.
    /// </summary>
    public IList<string> ArchitectureNotes { get; } = [];
}