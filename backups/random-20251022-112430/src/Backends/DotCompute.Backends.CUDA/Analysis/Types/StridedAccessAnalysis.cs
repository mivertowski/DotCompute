// <copyright file="StridedAccessAnalysis.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Analysis.Types;

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
    public IList<string> Recommendations { get; } = [];

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
    public IList<string> OptimizationSuggestions { get; } = [];
}