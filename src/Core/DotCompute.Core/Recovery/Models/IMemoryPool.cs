// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Defines the contract for a memory pool that can be managed by the recovery system.
/// </summary>
public interface IMemoryPool : IDisposable
{
    /// <summary>
    /// Gets the unique identifier for this memory pool.
    /// </summary>
    string PoolId { get; }

    /// <summary>
    /// Gets the total capacity of the memory pool in bytes.
    /// </summary>
    long TotalCapacity { get; }

    /// <summary>
    /// Gets the currently allocated memory in bytes.
    /// </summary>
    long AllocatedMemory { get; }

    /// <summary>
    /// Gets the available memory in bytes.
    /// </summary>
    long AvailableMemory { get; }

    /// <summary>
    /// Gets the current fragmentation level as a percentage (0.0 to 1.0).
    /// </summary>
    double FragmentationLevel { get; }

    /// <summary>
    /// Allocates memory from the pool.
    /// </summary>
    /// <param name="size">The size in bytes to allocate.</param>
    /// <returns>A handle to the allocated memory, or null if allocation fails.</returns>
    IMemoryHandle? Allocate(long size);

    /// <summary>
    /// Releases memory back to the pool.
    /// </summary>
    /// <param name="handle">The memory handle to release.</param>
    void Release(IMemoryHandle handle);

    /// <summary>
    /// Performs a cleanup operation on the pool.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the cleanup operation.</returns>
    Task CleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs defragmentation of the memory pool.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the defragmentation operation.</returns>
    Task DefragmentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the memory pool.
    /// </summary>
    /// <returns>The memory pool statistics.</returns>
    MemoryPoolStatistics GetStatistics();
}

/// <summary>
/// Represents a handle to allocated memory.
/// </summary>
public interface IMemoryHandle : IDisposable
{
    /// <summary>
    /// Gets the size of the allocated memory in bytes.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Gets a pointer to the allocated memory.
    /// </summary>
    IntPtr Pointer { get; }

    /// <summary>
    /// Gets a value indicating whether this handle is still valid.
    /// </summary>
    bool IsValid { get; }
}

/// <summary>
/// Contains statistics about a memory pool.
/// </summary>
public class MemoryPoolStatistics
{
    /// <summary>
    /// Gets or sets the total number of allocations.
    /// </summary>
    public long TotalAllocations { get; set; }

    /// <summary>
    /// Gets or sets the total number of deallocations.
    /// </summary>
    public long TotalDeallocations { get; set; }

    /// <summary>
    /// Gets or sets the current number of active allocations.
    /// </summary>
    public long ActiveAllocations { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the total bytes allocated.
    /// </summary>
    public long TotalBytesAllocated { get; set; }

    /// <summary>
    /// Gets or sets the total bytes deallocated.
    /// </summary>
    public long TotalBytesDeallocated { get; set; }

    /// <summary>
    /// Gets or sets the average allocation size in bytes.
    /// </summary>
    public double AverageAllocationSize { get; set; }

    /// <summary>
    /// Gets or sets the largest allocation size in bytes.
    /// </summary>
    public long LargestAllocation { get; set; }

    /// <summary>
    /// Gets or sets the smallest allocation size in bytes.
    /// </summary>
    public long SmallestAllocation { get; set; }

    /// <summary>
    /// Gets or sets the number of failed allocations.
    /// </summary>
    public long FailedAllocations { get; set; }

    /// <summary>
    /// Gets or sets the last cleanup timestamp.
    /// </summary>
    public DateTimeOffset? LastCleanup { get; set; }

    /// <summary>
    /// Gets or sets the last defragmentation timestamp.
    /// </summary>
    public DateTimeOffset? LastDefragmentation { get; set; }
}