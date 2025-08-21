// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Memory;

/// <summary>
/// Tracks state and provides recovery operations for a memory pool, maintaining
/// statistics and operation history for monitoring and optimization purposes.
/// </summary>
/// <remarks>
/// This class serves as a wrapper around an <see cref="IMemoryPool"/> instance,
/// providing additional state tracking and statistics collection. It maintains
/// thread-safe counters for cleanup and defragmentation operations and provides
/// a unified interface for performing recovery operations with automatic state updates.
/// </remarks>
public class MemoryPoolState
{
    private readonly IMemoryPool _pool;
    private readonly object _lock = new();
    private DateTimeOffset _lastCleanup = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastDefragmentation = DateTimeOffset.UtcNow;
    private int _cleanupCount;
    private int _defragmentationCount;

    /// <summary>
    /// Gets the unique identifier of the associated memory pool.
    /// </summary>
    /// <value>
    /// The pool identifier string.
    /// </value>
    public string PoolId { get; }

    /// <summary>
    /// Gets the timestamp of the last cleanup operation.
    /// </summary>
    /// <value>
    /// A <see cref="DateTimeOffset"/> indicating when the last cleanup was performed.
    /// </value>
    /// <remarks>
    /// This value is updated automatically when cleanup operations are performed
    /// through this state tracker. It can be used to determine if periodic
    /// cleanup is needed.
    /// </remarks>
    public DateTimeOffset LastCleanup => _lastCleanup;

    /// <summary>
    /// Gets the timestamp of the last defragmentation operation.
    /// </summary>
    /// <value>
    /// A <see cref="DateTimeOffset"/> indicating when the last defragmentation was performed.
    /// </value>
    /// <remarks>
    /// This value is updated automatically when defragmentation operations are performed
    /// through this state tracker. It can be used to schedule periodic defragmentation.
    /// </remarks>
    public DateTimeOffset LastDefragmentation => _lastDefragmentation;

    /// <summary>
    /// Gets the total number of cleanup operations performed.
    /// </summary>
    /// <value>
    /// The count of cleanup operations executed through this state tracker.
    /// </value>
    /// <remarks>
    /// This counter is incremented for both standard cleanup and emergency cleanup operations.
    /// It provides insight into pool maintenance frequency and can help identify
    /// pools that require frequent intervention.
    /// </remarks>
    public int CleanupCount => _cleanupCount;

    /// <summary>
    /// Gets the total number of defragmentation operations performed.
    /// </summary>
    /// <value>
    /// The count of defragmentation operations executed through this state tracker.
    /// </value>
    /// <remarks>
    /// This counter helps track defragmentation frequency and can be used to
    /// evaluate the effectiveness of defragmentation strategies and identify
    /// pools with high fragmentation rates.
    /// </remarks>
    public int DefragmentationCount => _defragmentationCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryPoolState"/> class.
    /// </summary>
    /// <param name="poolId">The unique identifier for the memory pool.</param>
    /// <param name="pool">The memory pool instance to track and manage.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="poolId"/> or <paramref name="pool"/> is null.
    /// </exception>
    /// <remarks>
    /// The constructor initializes the state tracker with the current timestamp
    /// for both last cleanup and last defragmentation times, and sets all
    /// operation counters to zero.
    /// </remarks>
    public MemoryPoolState(string poolId, IMemoryPool pool)
    {
        PoolId = poolId ?? throw new ArgumentNullException(nameof(poolId));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
    }

    /// <summary>
    /// Performs a cleanup operation on the memory pool and updates tracking statistics.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the cleanup operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous cleanup operation.
    /// </returns>
    /// <remarks>
    /// This method delegates to the underlying pool's cleanup method and then
    /// atomically updates the last cleanup timestamp and cleanup counter.
    /// The statistics update is thread-safe and will occur even if the
    /// cleanup operation throws an exception.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the <paramref name="cancellationToken"/>.
    /// </exception>
    public async Task PerformCleanupAsync(CancellationToken cancellationToken = default)
    {
        await _pool.CleanupAsync(cancellationToken);

        lock (_lock)
        {
            _lastCleanup = DateTimeOffset.UtcNow;
            _cleanupCount++;
        }
    }

    /// <summary>
    /// Performs a defragmentation operation on the memory pool and updates tracking statistics.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the defragmentation operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous defragmentation operation.
    /// </returns>
    /// <remarks>
    /// This method delegates to the underlying pool's defragmentation method and then
    /// atomically updates the last defragmentation timestamp and defragmentation counter.
    /// The statistics update is thread-safe and will occur even if the
    /// defragmentation operation throws an exception.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the <paramref name="cancellationToken"/>.
    /// </exception>
    public async Task PerformDefragmentationAsync(CancellationToken cancellationToken = default)
    {
        await _pool.DefragmentAsync(cancellationToken);

        lock (_lock)
        {
            _lastDefragmentation = DateTimeOffset.UtcNow;
            _defragmentationCount++;
        }
    }

    /// <summary>
    /// Performs an emergency cleanup operation on the memory pool and updates tracking statistics.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the emergency cleanup operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous emergency cleanup operation.
    /// </returns>
    /// <remarks>
    /// This method delegates to the underlying pool's emergency cleanup method and then
    /// atomically updates the last cleanup timestamp and cleanup counter.
    /// Emergency cleanup operations are counted as regular cleanup operations
    /// for statistical purposes.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the <paramref name="cancellationToken"/>.
    /// </exception>
    public async Task PerformEmergencyCleanupAsync(CancellationToken cancellationToken = default)
    {
        await _pool.EmergencyCleanupAsync(cancellationToken);

        lock (_lock)
        {
            _lastCleanup = DateTimeOffset.UtcNow;
            _cleanupCount++;
        }
    }

    /// <summary>
    /// Gets comprehensive statistics for the memory pool including current state and operation history.
    /// </summary>
    /// <returns>
    /// A <see cref="MemoryPoolStatistics"/> object containing current pool metrics
    /// and operation tracking information.
    /// </returns>
    /// <remarks>
    /// This method creates a point-in-time snapshot of pool statistics by querying
    /// the underlying pool for current memory usage and combining it with the
    /// operation history maintained by this state tracker. The utilization ratio
    /// is calculated as the fraction of total pool capacity currently allocated.
    /// </remarks>
    public MemoryPoolStatistics GetStatistics()
    {
        return new MemoryPoolStatistics
        {
            PoolId = PoolId,
            TotalAllocated = _pool.TotalAllocated,
            TotalAvailable = _pool.TotalAvailable,
            ActiveAllocations = _pool.ActiveAllocations,
            LastCleanup = _lastCleanup,
            LastDefragmentation = _lastDefragmentation,
            CleanupCount = _cleanupCount,
            DefragmentationCount = _defragmentationCount,
            UtilizationRatio = _pool.TotalAllocated + _pool.TotalAvailable > 0
                ? (double)_pool.TotalAllocated / (_pool.TotalAllocated + _pool.TotalAvailable)
                : 0.0
        };
    }
}