// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Core.Recovery.Types;

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Represents the state and management information for a memory pool in the recovery system.
/// </summary>
public class MemoryPoolState
{
    private readonly IMemoryPool _pool;
    private long _lastCleanupTime;
    private long _lastDefragmentationTime;
    private int _cleanupCount;
    private int _defragmentationCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryPoolState"/> class.
    /// </summary>
    /// <param name="poolId">The unique identifier for the pool.</param>
    /// <param name="pool">The memory pool instance.</param>
    public MemoryPoolState(string poolId, IMemoryPool pool)
    {
        PoolId = poolId ?? throw new ArgumentNullException(nameof(poolId));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _lastCleanupTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _lastDefragmentationTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Gets the unique identifier for this memory pool.
    /// </summary>
    public string PoolId { get; }

    /// <summary>
    /// Gets the last cleanup timestamp.
    /// </summary>
    public DateTimeOffset LastCleanup => DateTimeOffset.FromUnixTimeSeconds(_lastCleanupTime);

    /// <summary>
    /// Gets the last defragmentation timestamp.
    /// </summary>
    public DateTimeOffset LastDefragmentation => DateTimeOffset.FromUnixTimeSeconds(_lastDefragmentationTime);

    /// <summary>
    /// Gets the number of cleanup operations performed.
    /// </summary>
    public int CleanupCount => _cleanupCount;

    /// <summary>
    /// Gets the number of defragmentation operations performed.
    /// </summary>
    public int DefragmentationCount => _defragmentationCount;

    /// <summary>
    /// Gets the current memory pressure level for this pool.
    /// </summary>
    public MemoryPressureLevel PressureLevel
    {
        get
        {
            var ratio = (double)_pool.AllocatedMemory / _pool.TotalCapacity;
            return ratio switch
            {
                < 0.5 => MemoryPressureLevel.Low,
                < 0.75 => MemoryPressureLevel.Medium,
                < 0.9 => MemoryPressureLevel.High,
                _ => MemoryPressureLevel.Critical
            };
        }
    }

    /// <summary>
    /// Performs a cleanup operation on the memory pool.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the cleanup operation.</returns>
    public async Task PerformCleanupAsync(CancellationToken cancellationToken = default)
    {
        await _pool.CleanupAsync(cancellationToken);
        Interlocked.Exchange(ref _lastCleanupTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Interlocked.Increment(ref _cleanupCount);
    }

    /// <summary>
    /// Performs a defragmentation operation on the memory pool.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the defragmentation operation.</returns>
    public async Task PerformDefragmentationAsync(CancellationToken cancellationToken = default)
    {
        await _pool.DefragmentAsync(cancellationToken);
        Interlocked.Exchange(ref _lastDefragmentationTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Interlocked.Increment(ref _defragmentationCount);
    }

    /// <summary>
    /// Performs an emergency cleanup operation on the memory pool.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the emergency cleanup operation.</returns>
    public async Task PerformEmergencyCleanupAsync(CancellationToken cancellationToken = default)
    {
        // Perform both cleanup and defragmentation in emergency situations
        await _pool.CleanupAsync(cancellationToken);
        await _pool.DefragmentAsync(cancellationToken);
        
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Interlocked.Exchange(ref _lastCleanupTime, now);
        Interlocked.Exchange(ref _lastDefragmentationTime, now);
        Interlocked.Increment(ref _cleanupCount);
        Interlocked.Increment(ref _defragmentationCount);
    }

    /// <summary>
    /// Gets the current statistics for the memory pool.
    /// </summary>
    /// <returns>The memory pool statistics.</returns>
    public MemoryPoolStatistics GetStatistics()
    {
        var stats = _pool.GetStatistics();
        stats.LastCleanup = LastCleanup;
        stats.LastDefragmentation = LastDefragmentation;
        return stats;
    }

    /// <summary>
    /// Determines if the pool needs cleanup based on pressure and time.
    /// </summary>
    /// <param name="maxTimeSinceCleanup">The maximum time allowed since last cleanup.</param>
    /// <returns>True if cleanup is needed; otherwise, false.</returns>
    public bool NeedsCleanup(TimeSpan maxTimeSinceCleanup)
    {
        var timeSinceCleanup = DateTimeOffset.UtcNow - LastCleanup;
        return PressureLevel >= MemoryPressureLevel.High || timeSinceCleanup > maxTimeSinceCleanup;
    }

    /// <summary>
    /// Determines if the pool needs defragmentation based on fragmentation level and time.
    /// </summary>
    /// <param name="maxTimeSinceDefrag">The maximum time allowed since last defragmentation.</param>
    /// <param name="fragmentationThreshold">The fragmentation threshold (0.0 to 1.0).</param>
    /// <returns>True if defragmentation is needed; otherwise, false.</returns>
    public bool NeedsDefragmentation(TimeSpan maxTimeSinceDefrag, double fragmentationThreshold = 0.3)
    {
        var timeSinceDefrag = DateTimeOffset.UtcNow - LastDefragmentation;
        return _pool.FragmentationLevel > fragmentationThreshold || timeSinceDefrag > maxTimeSinceDefrag;
    }
}