// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Provides statistical information about memory usage and allocation patterns.
/// </summary>
public sealed class MemoryStatistics
{
    /// <summary>
    /// Gets the total amount of memory allocated in bytes.
    /// </summary>
    public long TotalAllocated { get; init; }

    /// <summary>
    /// Gets the current amount of memory in use in bytes.
    /// </summary>
    public long CurrentUsage { get; init; }

    /// <summary>
    /// Gets the current used memory in bytes.
    /// </summary>
    public long CurrentUsed { get; init; }

    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// </summary>
    public long PeakUsage { get; init; }

    /// <summary>
    /// Gets the number of allocations performed.
    /// </summary>
    public long AllocationCount { get; init; }

    /// <summary>
    /// Gets the number of deallocations performed.
    /// </summary>
    public long DeallocationCount { get; init; }

    /// <summary>
    /// Gets the number of currently active allocations.
    /// </summary>
    public long ActiveAllocations { get; init; }

    /// <summary>
    /// Gets the amount of memory available for allocation in bytes.
    /// </summary>
    public long AvailableMemory { get; init; }

    /// <summary>
    /// Gets the total memory capacity in bytes.
    /// </summary>
    public long TotalCapacity { get; init; }

    /// <summary>
    /// Gets the fragmentation percentage (0-100).
    /// </summary>
    public double FragmentationPercentage { get; init; }

    /// <summary>
    /// Gets the average allocation size in bytes.
    /// </summary>
    public double AverageAllocationSize { get; init; }

    /// <summary>
    /// Gets the total number of allocations made.
    /// </summary>
    public long TotalAllocationCount { get; init; }
    
    /// <summary>
    /// Gets the total number of deallocations made.
    /// </summary>
    public long TotalDeallocationCount { get; init; }
    
    /// <summary>
    /// Gets the cache hit rate for pooled allocations.
    /// </summary>
    public double PoolHitRate { get; init; }

    /// <summary>
    /// Gets the total memory bytes available in the system.
    /// </summary>
    public long TotalMemoryBytes { get; init; }

    /// <summary>
    /// Gets the used memory bytes in the system.
    /// </summary>
    public long UsedMemoryBytes { get; init; }

    /// <summary>
    /// Gets the available memory bytes for allocation.
    /// </summary>
    public long AvailableMemoryBytes { get; init; }

    /// <summary>
    /// Gets the peak memory usage in bytes across all allocations.
    /// </summary>
    public long PeakMemoryUsageBytes { get; init; }
    
    /// <summary>
    /// Gets the total amount of memory freed in bytes.
    /// </summary>
    public long TotalFreed { get; init; }
    
    /// <summary>
    /// Gets the current number of active buffers.
    /// </summary>
    public long ActiveBuffers { get; init; }
    
    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; init; }
}