// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Statistics;

/// <summary>
/// Memory usage and performance statistics.
/// </summary>
public sealed class MemoryStatistics
{
    private long _totalAllocations;
    private long _totalDeallocations;
    private long _totalBytesAllocated;
    private long _totalBytesFreed;
    private long _poolHits;
    private long _poolMisses;
    private double _totalAllocationTimeMs;
    private double _totalCopyTimeMs;
    /// <summary>
    /// Gets or sets the currently allocated bytes.
    /// </summary>
    /// <value>The currently allocated bytes.</value>


    public long CurrentlyAllocatedBytes => _totalBytesAllocated - _totalBytesFreed;
    /// <summary>
    /// Gets or sets the total allocations.
    /// </summary>
    /// <value>The total allocations.</value>
    public long TotalAllocations => _totalAllocations;
    /// <summary>
    /// Gets or sets the total deallocations.
    /// </summary>
    /// <value>The total deallocations.</value>
    public long TotalDeallocations => _totalDeallocations;
    /// <summary>
    /// Gets or sets the total bytes allocated.
    /// </summary>
    /// <value>The total bytes allocated.</value>
    public long TotalBytesAllocated => _totalBytesAllocated;
    /// <summary>
    /// Gets or sets the average allocation time.
    /// </summary>
    /// <value>The average allocation time.</value>
    public double AverageAllocationTime => _totalAllocations > 0 ? _totalAllocationTimeMs / _totalAllocations : 0.0;
    /// <summary>
    /// Gets or sets the pool hit rate.
    /// </summary>
    /// <value>The pool hit rate.</value>
    public double PoolHitRate => (_poolHits + _poolMisses) > 0 ? (double)_poolHits / (_poolHits + _poolMisses) : 0.0;
    /// <summary>
    /// Performs record allocation.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="timeMs">The time ms.</param>
    /// <param name="fromPool">The from pool.</param>

    public void RecordAllocation(long bytes, double timeMs, bool fromPool)
    {
        _ = Interlocked.Increment(ref _totalAllocations);
        _ = Interlocked.Add(ref _totalBytesAllocated, bytes);

        if (fromPool)
        {
            _ = Interlocked.Increment(ref _poolHits);
        }
        else
        {
            _ = Interlocked.Increment(ref _poolMisses);
        }

        lock (this)
        {
            _totalAllocationTimeMs += timeMs;
        }
    }
    /// <summary>
    /// Performs record failed allocation.
    /// </summary>
    /// <param name="bytes">The bytes.</param>

    public static void RecordFailedAllocation(long bytes)
    {
        // Track failed allocations for monitoring
    }
    /// <summary>
    /// Performs record buffer creation.
    /// </summary>
    /// <param name="bytes">The bytes.</param>

    public static void RecordBufferCreation(long bytes)
    {
        // Track buffer creation events
    }
    /// <summary>
    /// Performs record buffer destruction.
    /// </summary>
    /// <param name="bytes">The bytes.</param>

    public void RecordBufferDestruction(long bytes)
    {
        _ = Interlocked.Increment(ref _totalDeallocations);
        _ = Interlocked.Add(ref _totalBytesFreed, bytes);
    }
    /// <summary>
    /// Performs record copy operation.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="timeMs">The time ms.</param>
    /// <param name="isHostToDevice">A value indicating whether hostToDevice.</param>

    public void RecordCopyOperation(long bytes, double timeMs, bool isHostToDevice)
    {
        lock (this)
        {
            _totalCopyTimeMs += timeMs;
        }
    }
    /// <summary>
    /// Creates a new snapshot.
    /// </summary>
    /// <returns>The created snapshot.</returns>

    public MemoryStatistics CreateSnapshot()
    {
        lock (this)
        {
            return new MemoryStatistics
            {
                _totalAllocations = _totalAllocations,
                _totalDeallocations = _totalDeallocations,
                _totalBytesAllocated = _totalBytesAllocated,
                _totalBytesFreed = _totalBytesFreed,
                _poolHits = _poolHits,
                _poolMisses = _poolMisses,
                _totalAllocationTimeMs = _totalAllocationTimeMs,
                _totalCopyTimeMs = _totalCopyTimeMs
            };
        }
    }
}
