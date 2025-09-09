// <copyright file="Matrix2DAccessAnalysis.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.CUDA.Analysis.Types;

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
    public DotCompute.Abstractions.Types.TileAnalysis TileAnalysis { get; set; } = new();

    /// <summary>
    /// Gets or sets optimization recommendations.
    /// </summary>
    public List<string> Optimizations { get; set; } = [];

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