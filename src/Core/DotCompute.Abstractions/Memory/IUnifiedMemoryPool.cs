// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Unified memory pool interface that replaces all duplicate memory pool interfaces.
/// This is the ONLY memory pool interface in the entire solution.
/// </summary>
public interface IUnifiedMemoryPool : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the unique pool identifier.
    /// </summary>
    public string PoolId { get; }

    /// <summary>
    /// Gets the accelerator this pool belongs to (if any).
    /// </summary>
    public IAccelerator? Accelerator { get; }

    /// <summary>
    /// Gets the total pool size in bytes.
    /// </summary>
    public long TotalSize { get; }

    /// <summary>
    /// Gets the currently allocated size in bytes.
    /// </summary>
    public long AllocatedSize { get; }

    /// <summary>
    /// Gets the available size in bytes.
    /// </summary>
    public long AvailableSize { get; }

    /// <summary>
    /// Gets the number of active allocations.
    /// </summary>
    public int ActiveAllocationCount { get; }

    /// <summary>
    /// Gets pool statistics.
    /// </summary>
    public MemoryPoolStatistics Statistics { get; }

    /// <summary>
    /// Allocates memory from the pool.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="count">The number of elements to allocate.</param>
    /// <param name="options">Memory allocation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The allocated memory buffer.</returns>
    public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Allocates raw memory from the pool.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes to allocate.</param>
    /// <param name="options">Memory allocation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The allocated memory buffer.</returns>
    public ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns memory to the pool.
    /// </summary>
    /// <param name="buffer">The memory buffer to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the return operation.</returns>
    public ValueTask ReturnAsync(
        IUnifiedMemoryBuffer buffer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to allocate memory from the pool without waiting.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="count">The number of elements to allocate.</param>
    /// <param name="buffer">The allocated buffer if successful.</param>
    /// <param name="options">Memory allocation options.</param>
    /// <returns>True if allocation succeeded, false otherwise.</returns>
    public bool TryAllocate<T>(
        int count,
        out IUnifiedMemoryBuffer<T>? buffer,
        MemoryOptions options = MemoryOptions.None) where T : unmanaged;

    /// <summary>
    /// Defragments the memory pool to reduce fragmentation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the defragmentation operation.</returns>
    public ValueTask DefragmentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs cleanup operations on the pool.
    /// </summary>
    /// <param name="aggressive">Whether to perform aggressive cleanup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the cleanup operation.</returns>
    public ValueTask CleanupAsync(
        bool aggressive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes the pool by resizing, defragmenting, and cleaning up.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the optimization operation.</returns>
    public ValueTask OptimizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resizes the pool to the specified size.
    /// </summary>
    /// <param name="newSizeInBytes">The new size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the resize operation.</returns>
    public ValueTask ResizeAsync(
        long newSizeInBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the pool, releasing all allocations.
    /// </summary>
    public void Reset();
}

/// <summary>
/// Memory pool statistics.
/// </summary>
public sealed class MemoryPoolStatistics
{
    /// <summary>
    /// Gets the pool identifier.
    /// </summary>
    public string PoolId { get; init; } = string.Empty;


    /// <summary>
    /// Gets the total pool size in bytes.
    /// </summary>
    public long TotalSize { get; init; }


    /// <summary>
    /// Gets the allocated size in bytes.
    /// </summary>
    public long AllocatedSize { get; init; }


    /// <summary>
    /// Gets the available size in bytes.
    /// </summary>
    public long AvailableSize { get; init; }


    /// <summary>
    /// Gets the peak allocated size in bytes.
    /// </summary>
    public long PeakAllocatedSize { get; init; }


    /// <summary>
    /// Gets the number of active allocations.
    /// </summary>
    public int ActiveAllocations { get; init; }


    /// <summary>
    /// Gets the total number of allocations made.
    /// </summary>
    public long TotalAllocations { get; init; }


    /// <summary>
    /// Gets the total number of deallocations made.
    /// </summary>
    public long TotalDeallocations { get; init; }


    /// <summary>
    /// Gets the cache hit rate (0.0 to 1.0).
    /// </summary>
    public double HitRate { get; init; }


    /// <summary>
    /// Gets the fragmentation percentage (0.0 to 100.0).
    /// </summary>
    public double FragmentationPercentage { get; init; }


    /// <summary>
    /// Gets the number of defragmentation operations performed.
    /// </summary>
    public long DefragmentationCount { get; init; }


    /// <summary>
    /// Gets the number of cleanup operations performed.
    /// </summary>
    public long CleanupCount { get; init; }


    /// <summary>
    /// Gets the number of resize operations performed.
    /// </summary>
    public long ResizeCount { get; init; }


    /// <summary>
    /// Gets the last cleanup time.
    /// </summary>
    public DateTimeOffset? LastCleanup { get; init; }


    /// <summary>
    /// Gets the last defragmentation time.
    /// </summary>
    public DateTimeOffset? LastDefragmentation { get; init; }


    /// <summary>
    /// Gets allocation size distribution.
    /// </summary>
    public IReadOnlyDictionary<string, long> AllocationSizeDistribution { get; init; } =

        new Dictionary<string, long>();
}
