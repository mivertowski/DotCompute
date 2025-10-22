// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Statistics;

/// <summary>
/// Memory statistics for a specific accelerator
/// </summary>
public class AcceleratorMemoryStatistics
{
    /// <summary>
    /// Gets the accelerator ID
    /// </summary>
    public required string AcceleratorId { get; init; }

    /// <summary>
    /// Gets the total memory size
    /// </summary>
    public long TotalMemory { get; init; }

    /// <summary>
    /// Gets the allocated memory size
    /// </summary>
    public long AllocatedMemory { get; init; }

    /// <summary>
    /// Gets the available memory size
    /// </summary>
    public long AvailableMemory { get; init; }

    /// <summary>
    /// Gets the number of active allocations
    /// </summary>
    public int ActiveAllocations { get; init; }

    /// <summary>
    /// Gets the largest available block size
    /// </summary>
    public long LargestAvailableBlock { get; init; }
}