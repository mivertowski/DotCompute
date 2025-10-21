// <copyright file="CoalescingComparison.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Analysis.Types;

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

    /// <summary>
    /// Gets the dictionary of analyses indexed by pattern name.
    /// </summary>
    public Dictionary<string, CoalescingAnalysis> Analyses { get; } = [];

    /// <summary>
    /// Gets or sets the name of the pattern with the best efficiency.
    /// </summary>
    public string? BestPattern { get; set; }

    /// <summary>
    /// Gets or sets the name of the pattern with the worst efficiency.
    /// </summary>
    public string? WorstPattern { get; set; }

    /// <summary>
    /// Gets or sets the best efficiency value observed.
    /// </summary>
    public double BestEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the worst efficiency value observed.
    /// </summary>
    public double WorstEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the potential performance improvement if worst pattern is optimized to match best.
    /// </summary>
    public double ImprovementPotential { get; set; }

    /// <summary>
    /// Gets or initializes the list of recommendations based on the comparison.
    /// </summary>
    public IList<string> Recommendations { get; init; } = [];
}