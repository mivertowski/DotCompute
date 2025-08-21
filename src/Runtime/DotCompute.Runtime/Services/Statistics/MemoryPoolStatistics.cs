// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Statistics;

/// <summary>
/// Memory pool statistics
/// </summary>
public class MemoryPoolStatistics
{
    /// <summary>
    /// Gets the number of allocations performed
    /// </summary>
    public long AllocationCount { get; init; }

    /// <summary>
    /// Gets the number of deallocations performed
    /// </summary>
    public long DeallocationCount { get; init; }

    /// <summary>
    /// Gets the total bytes allocated
    /// </summary>
    public long TotalBytesAllocated { get; init; }

    /// <summary>
    /// Gets the total bytes deallocated
    /// </summary>
    public long TotalBytesDeallocated { get; init; }

    /// <summary>
    /// Gets the peak memory usage
    /// </summary>
    public long PeakMemoryUsage { get; init; }

    /// <summary>
    /// Gets the average allocation size
    /// </summary>
    public double AverageAllocationSize { get; init; }

    /// <summary>
    /// Gets the number of defragmentation operations performed
    /// </summary>
    public int DefragmentationCount { get; init; }
}