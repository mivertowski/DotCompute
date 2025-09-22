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


    public long CurrentlyAllocatedBytes => _totalBytesAllocated - _totalBytesFreed;
    public long TotalAllocations => _totalAllocations;
    public long TotalDeallocations => _totalDeallocations;
    public long TotalBytesAllocated => _totalBytesAllocated;
    public double AverageAllocationTime => _totalAllocations > 0 ? _totalAllocationTimeMs / _totalAllocations : 0.0;
    public double PoolHitRate => (_poolHits + _poolMisses) > 0 ? (double)_poolHits / (_poolHits + _poolMisses) : 0.0;

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

    public static void RecordFailedAllocation(long bytes)
    {
        // Track failed allocations for monitoring
    }

    public static void RecordBufferCreation(long bytes)
    {
        // Track buffer creation events
    }

    public void RecordBufferDestruction(long bytes)
    {
        _ = Interlocked.Increment(ref _totalDeallocations);
        _ = Interlocked.Add(ref _totalBytesFreed, bytes);
    }

    public void RecordCopyOperation(long bytes, double timeMs, bool isHostToDevice)
    {
        lock (this)
        {
            _totalCopyTimeMs += timeMs;
        }
    }

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