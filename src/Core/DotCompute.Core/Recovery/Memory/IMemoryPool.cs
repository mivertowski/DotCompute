// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Memory;

/// <summary>
/// Defines the contract for memory pools that support recovery operations and provide
/// comprehensive memory management capabilities.
/// </summary>
/// <remarks>
/// This interface extends <see cref="IDisposable"/> to ensure proper resource cleanup.
/// Implementations should provide thread-safe access to memory statistics and
/// support asynchronous recovery operations with cancellation support.
/// Memory pools implementing this interface can participate in system-wide
/// memory recovery strategies.
/// </remarks>
public interface IMemoryPool : IDisposable
{
    /// <summary>
    /// Gets the unique identifier for this memory pool.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies this memory pool instance.
    /// </value>
    /// <remarks>
    /// This identifier is used for tracking, logging, and coordination
    /// purposes across the memory management system. It should remain
    /// constant for the lifetime of the pool instance.
    /// </remarks>
    public string PoolId { get; }

    /// <summary>
    /// Gets the total amount of memory currently allocated from this pool, in bytes.
    /// </summary>
    /// <value>
    /// The number of bytes currently allocated and in use.
    /// </value>
    /// <remarks>
    /// This value represents memory that has been allocated from the pool
    /// and is currently being used by the application. It should be updated
    /// in real-time as allocations and deallocations occur.
    /// </remarks>
    public long TotalAllocated { get; }

    /// <summary>
    /// Gets the total amount of memory available for allocation in this pool, in bytes.
    /// </summary>
    /// <value>
    /// The number of bytes available for immediate allocation.
    /// </value>
    /// <remarks>
    /// This represents memory that is ready for allocation without requiring
    /// additional system memory requests or garbage collection. The value
    /// may change as memory is allocated, freed, or reclaimed.
    /// </remarks>
    public long TotalAvailable { get; }

    /// <summary>
    /// Gets the current number of active memory allocations in this pool.
    /// </summary>
    /// <value>
    /// The count of currently active allocation handles or references.
    /// </value>
    /// <remarks>
    /// This metric helps track memory pool usage patterns and can be used
    /// to identify potential memory leaks or excessive allocation patterns.
    /// Each successful allocation should increment this counter, and each
    /// deallocation should decrement it.
    /// </remarks>
    public int ActiveAllocations { get; }

    /// <summary>
    /// Performs a standard cleanup operation on the memory pool.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the cleanup operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous cleanup operation.
    /// </returns>
    /// <remarks>
    /// This method should perform routine maintenance such as releasing
    /// unused memory blocks, updating internal data structures, and
    /// performing light defragmentation. It should be safe to call
    /// during normal operation with minimal performance impact.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the <paramref name="cancellationToken"/>.
    /// </exception>
    public Task CleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a comprehensive defragmentation operation on the memory pool.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the defragmentation operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous defragmentation operation.
    /// </returns>
    /// <remarks>
    /// This method should perform more intensive memory reorganization to
    /// reduce fragmentation and improve allocation efficiency. It may have
    /// a noticeable performance impact and should typically be performed
    /// during low-activity periods or when fragmentation becomes significant.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the <paramref name="cancellationToken"/>.
    /// </exception>
    public Task DefragmentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs emergency cleanup operations to free as much memory as possible.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the emergency cleanup operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous emergency cleanup operation.
    /// </returns>
    /// <remarks>
    /// This method should perform aggressive memory recovery operations,
    /// potentially including forceful deallocation of cached resources,
    /// aggressive garbage collection, and other emergency measures.
    /// It should be called only when the system is under severe memory pressure.
    /// This operation may significantly impact performance and should be
    /// considered a last resort.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the <paramref name="cancellationToken"/>.
    /// </exception>
    public Task EmergencyCleanupAsync(CancellationToken cancellationToken = default);
}