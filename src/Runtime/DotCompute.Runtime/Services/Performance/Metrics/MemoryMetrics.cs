// <copyright file="MemoryMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Metrics;

/// <summary>
/// Memory performance and usage metrics.
/// Tracks memory allocation, deallocation, and fragmentation statistics.
/// </summary>
public class MemoryMetrics
{
    /// <summary>
    /// Gets the total allocated memory in bytes.
    /// Cumulative memory allocated during the period.
    /// </summary>
    public long TotalAllocatedBytes { get; init; }

    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// Maximum memory consumption observed.
    /// </summary>
    public long PeakMemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets the average memory usage in bytes.
    /// Mean memory consumption over the period.
    /// </summary>
    public long AverageMemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets the number of allocations.
    /// Total count of memory allocation operations.
    /// </summary>
    public long AllocationCount { get; init; }

    /// <summary>
    /// Gets the number of deallocations.
    /// Total count of memory deallocation operations.
    /// </summary>
    public long DeallocationCount { get; init; }

    /// <summary>
    /// Gets fragmentation metrics.
    /// Memory fragmentation analysis and statistics.
    /// </summary>
    public FragmentationMetrics Fragmentation { get; init; } = new();
}