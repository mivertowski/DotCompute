// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Recovery.Types;

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Represents the result of a memory defragmentation operation
/// </summary>
public class MemoryDefragmentationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the defragmentation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the amount of memory freed (in bytes)
    /// </summary>
    public long MemoryFreed { get; set; }

    /// <summary>
    /// Gets or sets the time taken for defragmentation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the memory fragmentation percentage before defragmentation
    /// </summary>
    public double FragmentationBefore { get; set; }

    /// <summary>
    /// Gets or sets the memory fragmentation percentage after defragmentation
    /// </summary>
    public double FragmentationAfter { get; set; }

    /// <summary>
    /// Gets or sets the largest contiguous memory block before defragmentation
    /// </summary>
    public long LargestBlockBefore { get; set; }

    /// <summary>
    /// Gets or sets the largest contiguous memory block after defragmentation
    /// </summary>
    public long LargestBlockAfter { get; set; }

    /// <summary>
    /// Gets or sets any error that occurred during defragmentation
    /// </summary>
    public Exception? Error { get; set; }

    /// <summary>
    /// Gets or sets additional statistics and metrics
    /// </summary>
    public Dictionary<string, object> Statistics { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of memory blocks that were moved
    /// </summary>
    public int BlocksMoved { get; set; }

    /// <summary>
    /// Gets or sets the improvement percentage in memory utilization
    /// </summary>
    public double ImprovementPercentage => FragmentationBefore > 0 
        ? ((FragmentationBefore - FragmentationAfter) / FragmentationBefore) * 100.0 
        : 0.0;
}



/// <summary>
/// Represents the trend in memory usage over time
/// </summary>
public enum MemoryTrend
{
    /// <summary>
    /// Memory usage is decreasing
    /// </summary>
    Decreasing,

    /// <summary>
    /// Memory usage is stable
    /// </summary>
    Stable,

    /// <summary>
    /// Memory usage is increasing slowly
    /// </summary>
    Increasing,

    /// <summary>
    /// Memory usage is increasing rapidly
    /// </summary>
    Surging
}