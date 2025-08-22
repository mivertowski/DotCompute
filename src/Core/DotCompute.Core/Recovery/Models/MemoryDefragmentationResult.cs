// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

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
    public Dictionary<string, object> Statistics { get; set; } = new();

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
/// Represents information about memory pressure in the system
/// </summary>
public class MemoryPressureInfo
{
    /// <summary>
    /// Gets or sets the current memory pressure level
    /// </summary>
    public MemoryPressureLevel Level { get; set; } = MemoryPressureLevel.Normal;

    /// <summary>
    /// Gets or sets the total available memory (in bytes)
    /// </summary>
    public long TotalAvailableMemory { get; set; }

    /// <summary>
    /// Gets or sets the currently used memory (in bytes)
    /// </summary>
    public long UsedMemory { get; set; }

    /// <summary>
    /// Gets or sets the free memory (in bytes)
    /// </summary>
    public long FreeMemory { get; set; }

    /// <summary>
    /// Gets or sets the memory usage percentage
    /// </summary>
    public double UsagePercentage { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this information was collected
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the trend in memory usage
    /// </summary>
    public MemoryTrend Trend { get; set; } = MemoryTrend.Stable;

    /// <summary>
    /// Gets or sets the rate of memory allocation (bytes per second)
    /// </summary>
    public double AllocationRate { get; set; }

    /// <summary>
    /// Gets or sets the rate of memory deallocation (bytes per second)
    /// </summary>
    public double DeallocationRate { get; set; }

    /// <summary>
    /// Gets or sets the list of top memory consumers
    /// </summary>
    public List<MemoryConsumer> TopConsumers { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of garbage collections that have occurred
    /// </summary>
    public long GarbageCollectionCount { get; set; }

    /// <summary>
    /// Gets or sets recommended actions based on current memory pressure
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Represents different levels of memory pressure
/// </summary>
public enum MemoryPressureLevel
{
    /// <summary>
    /// Normal memory usage - no action needed
    /// </summary>
    Normal,

    /// <summary>
    /// Elevated memory usage - monitoring recommended
    /// </summary>
    Elevated,

    /// <summary>
    /// High memory usage - optimization recommended
    /// </summary>
    High,

    /// <summary>
    /// Critical memory usage - immediate action required
    /// </summary>
    Critical,

    /// <summary>
    /// Extreme memory pressure - emergency measures required
    /// </summary>
    Extreme
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