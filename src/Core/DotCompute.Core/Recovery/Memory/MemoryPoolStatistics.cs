// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Memory;

/// <summary>
/// Provides comprehensive statistics and metrics for a memory pool, including
/// usage information, operation history, and performance indicators.
/// </summary>
/// <remarks>
/// This class serves as a data transfer object containing all relevant metrics
/// for monitoring and analyzing memory pool performance. It includes both
/// current state information and historical operation data that can be used
/// for trend analysis and optimization decisions.
/// </remarks>
public class MemoryPoolStatistics
{
    /// <summary>
    /// Gets or sets the unique identifier of the memory pool.
    /// </summary>
    /// <value>
    /// The pool identifier string. Defaults to an empty string.
    /// </value>
    public string PoolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total amount of memory currently allocated from the pool, in bytes.
    /// </summary>
    /// <value>
    /// The number of bytes currently allocated and in use.
    /// </value>
    /// <remarks>
    /// This represents memory that has been allocated from the pool and is
    /// currently being used by the application. It forms part of the
    /// utilization ratio calculation.
    /// </remarks>
    public long TotalAllocated { get; set; }

    /// <summary>
    /// Gets or sets the total amount of memory available for allocation in the pool, in bytes.
    /// </summary>
    /// <value>
    /// The number of bytes available for immediate allocation.
    /// </value>
    /// <remarks>
    /// This represents memory that is ready for allocation without requiring
    /// additional system memory requests. Combined with TotalAllocated,
    /// it represents the total pool capacity.
    /// </remarks>
    public long TotalAvailable { get; set; }

    /// <summary>
    /// Gets or sets the current number of active memory allocations in the pool.
    /// </summary>
    /// <value>
    /// The count of currently active allocation handles or references.
    /// </value>
    /// <remarks>
    /// This metric helps identify allocation patterns and potential memory leaks.
    /// A high number relative to the allocated memory might indicate many
    /// small allocations, while a low number might indicate fewer large allocations.
    /// </remarks>
    public int ActiveAllocations { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last cleanup operation performed on the pool.
    /// </summary>
    /// <value>
    /// A <see cref="DateTimeOffset"/> indicating when the last cleanup was performed.
    /// </value>
    /// <remarks>
    /// This can be used to determine how recently the pool was maintained
    /// and whether periodic cleanup operations are occurring as expected.
    /// </remarks>
    public DateTimeOffset LastCleanup { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last defragmentation operation performed on the pool.
    /// </summary>
    /// <value>
    /// A <see cref="DateTimeOffset"/> indicating when the last defragmentation was performed.
    /// </value>
    /// <remarks>
    /// This helps track defragmentation frequency and can be used to schedule
    /// future defragmentation operations based on pool usage patterns.
    /// </remarks>
    public DateTimeOffset LastDefragmentation { get; set; }

    /// <summary>
    /// Gets or sets the total number of cleanup operations performed on the pool.
    /// </summary>
    /// <value>
    /// The count of cleanup operations executed throughout the pool's lifetime.
    /// </value>
    /// <remarks>
    /// This metric includes both standard cleanup and emergency cleanup operations.
    /// A high cleanup count might indicate frequent memory pressure or
    /// inefficient memory usage patterns.
    /// </remarks>
    public int CleanupCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of defragmentation operations performed on the pool.
    /// </summary>
    /// <value>
    /// The count of defragmentation operations executed throughout the pool's lifetime.
    /// </value>
    /// <remarks>
    /// Frequent defragmentation might indicate high memory fragmentation,
    /// suggesting the need for allocation strategy adjustments or more
    /// frequent maintenance operations.
    /// </remarks>
    public int DefragmentationCount { get; set; }

    /// <summary>
    /// Gets or sets the utilization ratio of the memory pool.
    /// </summary>
    /// <value>
    /// A value between 0.0 and 1.0 representing the fraction of total pool capacity currently allocated.
    /// </value>
    /// <remarks>
    /// This ratio is calculated as TotalAllocated / (TotalAllocated + TotalAvailable).
    /// Values closer to 1.0 indicate higher utilization, which might suggest
    /// the need for pool expansion or more aggressive cleanup strategies.
    /// A value of 0.0 indicates an empty pool.
    /// </remarks>
    /// <example>
    /// If TotalAllocated is 800MB and TotalAvailable is 200MB,
    /// the UtilizationRatio would be 0.8 (80%).
    /// </example>
    public double UtilizationRatio { get; set; }

    /// <summary>
    /// Returns a string representation of the memory pool statistics.
    /// </summary>
    /// <returns>
    /// A formatted string containing the pool ID, allocated memory in MB,
    /// active allocation count, and utilization ratio as a percentage.
    /// </returns>
    /// <example>
    /// "Pool MainPool: 512MB allocated, 1024 active, 80.0% utilized"
    /// </example>
    public override string ToString()
        => $"Pool {PoolId}: {TotalAllocated / 1024 / 1024}MB allocated, {ActiveAllocations} active, {UtilizationRatio:P1} utilized";
}