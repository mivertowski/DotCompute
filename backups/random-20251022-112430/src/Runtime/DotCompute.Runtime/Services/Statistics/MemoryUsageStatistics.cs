// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Statistics;

/// <summary>
/// Memory usage statistics
/// </summary>
public class MemoryUsageStatistics
{
    /// <summary>
    /// Gets the total allocated memory across all pools
    /// </summary>
    public long TotalAllocated { get; init; }

    /// <summary>
    /// Gets the total available memory across all pools
    /// </summary>
    public long TotalAvailable { get; init; }

    /// <summary>
    /// Gets the fragmentation percentage
    /// </summary>
    public double FragmentationPercentage { get; init; }

    /// <summary>
    /// Gets per-accelerator statistics
    /// </summary>
    public Dictionary<string, AcceleratorMemoryStatistics> PerAcceleratorStats { get; init; } = [];

    /// <summary>
    /// Gets the timestamp when these statistics were collected
    /// </summary>
    public DateTime CollectedAt { get; init; } = DateTime.UtcNow;
}