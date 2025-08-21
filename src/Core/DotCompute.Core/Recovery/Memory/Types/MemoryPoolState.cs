// <copyright file="MemoryPoolState.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Core.Recovery.Memory.Interfaces;

namespace DotCompute.Core.Recovery.Memory.Types;

/// <summary>
/// Tracks state and provides recovery operations for a memory pool.
/// Manages cleanup and defragmentation operations with statistics tracking.
/// </summary>
public class MemoryPoolState
{
    private readonly IMemoryPool _pool;
    private readonly object _lock = new();
    private DateTimeOffset _lastCleanup = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastDefragmentation = DateTimeOffset.UtcNow;
    private int _cleanupCount;
    private int _defragmentationCount;

    /// <summary>
    /// Gets the pool identifier.
    /// Unique name for the managed pool.
    /// </summary>
    public string PoolId { get; }

    /// <summary>
    /// Gets the last cleanup timestamp.
    /// When the pool was last cleaned up.
    /// </summary>
    public DateTimeOffset LastCleanup => _lastCleanup;

    /// <summary>
    /// Gets the last defragmentation timestamp.
    /// When the pool was last defragmented.
    /// </summary>
    public DateTimeOffset LastDefragmentation => _lastDefragmentation;

    /// <summary>
    /// Gets the cleanup count.
    /// Number of cleanup operations performed.
    /// </summary>
    public int CleanupCount => _cleanupCount;

    /// <summary>
    /// Gets the defragmentation count.
    /// Number of defragmentation operations performed.
    /// </summary>
    public int DefragmentationCount => _defragmentationCount;

    /// <summary>
    /// Initializes a new instance of the MemoryPoolState class.
    /// </summary>
    /// <param name="poolId">The pool identifier.</param>
    /// <param name="pool">The memory pool to manage.</param>
    public MemoryPoolState(string poolId, IMemoryPool pool)
    {
        PoolId = poolId ?? throw new ArgumentNullException(nameof(poolId));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
    }

    /// <summary>
    /// Performs cleanup on the memory pool.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task representing the cleanup operation.</returns>
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
    /// Performs defragmentation on the memory pool.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task representing the defragmentation operation.</returns>
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
    /// Performs emergency cleanup on the memory pool.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task representing the emergency cleanup operation.</returns>
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
    /// Gets current statistics for the memory pool.
    /// </summary>
    /// <returns>Memory pool statistics.</returns>
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