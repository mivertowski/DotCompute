// <copyright file="MemoryPoolStatistics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Recovery.Memory.Types;

/// <summary>
/// Statistics for a memory pool.
/// Provides metrics about pool usage and maintenance operations.
/// </summary>
public class MemoryPoolStatistics
{
    /// <summary>
    /// Gets or sets the pool identifier.
    /// Unique name of the memory pool.
    /// </summary>
    public string PoolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total allocated memory in bytes.
    /// Amount of memory currently in use.
    /// </summary>
    public long TotalAllocated { get; set; }

    /// <summary>
    /// Gets or sets the total available memory in bytes.
    /// Amount of memory free for allocation.
    /// </summary>
    public long TotalAvailable { get; set; }

    /// <summary>
    /// Gets or sets the number of active allocations.
    /// Current count of outstanding allocations.
    /// </summary>
    public int ActiveAllocations { get; set; }

    /// <summary>
    /// Gets or sets the last cleanup timestamp.
    /// When the pool was last cleaned.
    /// </summary>
    public DateTimeOffset LastCleanup { get; set; }

    /// <summary>
    /// Gets or sets the last defragmentation timestamp.
    /// When the pool was last defragmented.
    /// </summary>
    public DateTimeOffset LastDefragmentation { get; set; }

    /// <summary>
    /// Gets or sets the cleanup count.
    /// Total number of cleanup operations performed.
    /// </summary>
    public int CleanupCount { get; set; }

    /// <summary>
    /// Gets or sets the defragmentation count.
    /// Total number of defragmentation operations performed.
    /// </summary>
    public int DefragmentationCount { get; set; }

    /// <summary>
    /// Gets or sets the utilization ratio.
    /// Percentage of pool memory in use (0.0 to 1.0).
    /// </summary>
    public double UtilizationRatio { get; set; }

    /// <summary>
    /// Returns a string representation of the statistics.
    /// </summary>
    /// <returns>Summary of pool statistics.</returns>
    public override string ToString()
        => $"Pool {PoolId}: {TotalAllocated / 1024 / 1024}MB allocated, {ActiveAllocations} active, {UtilizationRatio:P1} utilized";
}