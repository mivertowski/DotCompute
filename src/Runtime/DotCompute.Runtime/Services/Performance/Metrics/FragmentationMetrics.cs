// <copyright file="FragmentationMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Metrics;

/// <summary>
/// Memory fragmentation metrics.
/// Analyzes memory fragmentation patterns and efficiency.
/// </summary>
public class FragmentationMetrics
{
    /// <summary>
    /// Gets the fragmentation percentage.
    /// Percentage of memory lost to fragmentation (0-100).
    /// </summary>
    public double FragmentationPercent { get; init; }

    /// <summary>
    /// Gets the largest free block size.
    /// Size of the largest contiguous free memory block in bytes.
    /// </summary>
    public long LargestFreeBlockBytes { get; init; }

    /// <summary>
    /// Gets the number of free blocks.
    /// Count of non-contiguous free memory regions.
    /// </summary>
    public int FreeBlockCount { get; init; }

    /// <summary>
    /// Gets the average free block size.
    /// Mean size of free memory blocks in bytes.
    /// </summary>
    public long AverageFreeBlockBytes { get; init; }
}
