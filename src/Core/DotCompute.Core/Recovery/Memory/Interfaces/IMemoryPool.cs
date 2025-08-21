// <copyright file="IMemoryPool.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Recovery.Memory.Interfaces;

/// <summary>
/// Interface for memory pools that support recovery operations.
/// Defines contract for managed memory pools with recovery capabilities.
/// </summary>
public interface IMemoryPool : IDisposable
{
    /// <summary>
    /// Gets the pool identifier.
    /// Unique name for this memory pool.
    /// </summary>
    string PoolId { get; }

    /// <summary>
    /// Gets the total allocated memory in bytes.
    /// Amount of memory currently allocated from this pool.
    /// </summary>
    long TotalAllocated { get; }

    /// <summary>
    /// Gets the total available memory in bytes.
    /// Amount of memory available for allocation.
    /// </summary>
    long TotalAvailable { get; }

    /// <summary>
    /// Gets the number of active allocations.
    /// Count of currently outstanding allocations.
    /// </summary>
    int ActiveAllocations { get; }

    /// <summary>
    /// Performs cleanup operations on the pool.
    /// Releases unused memory and consolidates allocations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task representing the cleanup operation.</returns>
    Task CleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Defragments the memory pool.
    /// Consolidates free memory blocks to reduce fragmentation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task representing the defragmentation operation.</returns>
    Task DefragmentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs emergency cleanup when memory is critical.
    /// Aggressively releases memory at the cost of performance.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task representing the emergency cleanup operation.</returns>
    Task EmergencyCleanupAsync(CancellationToken cancellationToken = default);
}