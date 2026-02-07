// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DotCompute.Memory;

/// <summary>
/// CONSOLIDATED memory usage and performance statistics.
/// This replaces all duplicate MemoryStatistics implementations across the codebase.
///
/// Provides comprehensive tracking for:
/// - Allocation and deallocation counts
/// - Memory usage (current, peak, total)
/// - Pool efficiency metrics
/// - Performance timing data
/// - Error and failure statistics
/// </summary>
public sealed class MemoryStatistics
{
    private long _totalAllocations;
    private long _totalDeallocations;
    private long _totalBytesAllocated;
    private long _totalBytesFreed;
    private long _currentlyAllocatedBytes;
    private long _peakAllocatedBytes;
    private long _poolHits;
    private long _poolMisses;
    private long _failedAllocations;
    private double _totalAllocationTimeMs;
    private double _totalDeallocationTimeMs;
    private double _totalCopyTimeMs;
    private long _copyOperations;
    private readonly object _syncLock = new();

    /// <summary>
    /// Gets the number of bytes currently allocated and not yet freed.
    /// </summary>
    public long CurrentlyAllocatedBytes => Interlocked.Read(ref _currentlyAllocatedBytes);

    /// <summary>
    /// Gets the peak number of bytes allocated at any point in time.
    /// </summary>
    public long PeakAllocatedBytes => Interlocked.Read(ref _peakAllocatedBytes);

    /// <summary>
    /// Gets the total number of allocation operations performed.
    /// </summary>
    public long TotalAllocations => Interlocked.Read(ref _totalAllocations);

    /// <summary>
    /// Gets the total number of deallocation operations performed.
    /// </summary>
    public long TotalDeallocations => Interlocked.Read(ref _totalDeallocations);

    /// <summary>
    /// Gets the total number of bytes that have been allocated (cumulative).
    /// </summary>
    public long TotalBytesAllocated => Interlocked.Read(ref _totalBytesAllocated);

    /// <summary>
    /// Gets the total number of bytes that have been freed (cumulative).
    /// </summary>
    public long TotalBytesFreed => Interlocked.Read(ref _totalBytesFreed);

    /// <summary>
    /// Gets the number of active allocations (allocations - deallocations).
    /// </summary>
    public long ActiveAllocations => TotalAllocations - TotalDeallocations;

    /// <summary>
    /// Gets the average time taken for allocation operations in milliseconds.
    /// </summary>
    public double AverageAllocationTime
    {
        get
        {
            var allocations = TotalAllocations;
            return allocations > 0 ? _totalAllocationTimeMs / allocations : 0.0;
        }
    }

    /// <summary>
    /// Gets the average time taken for deallocation operations in milliseconds.
    /// </summary>
    public double AverageDeallocationTime
    {
        get
        {
            var deallocations = TotalDeallocations;
            return deallocations > 0 ? _totalDeallocationTimeMs / deallocations : 0.0;
        }
    }

    /// <summary>
    /// Gets the average time taken for copy operations in milliseconds.
    /// </summary>
    public double AverageCopyTime
    {
        get
        {
            var copies = Interlocked.Read(ref _copyOperations);
            return copies > 0 ? _totalCopyTimeMs / copies : 0.0;
        }
    }

    /// <summary>
    /// Gets the memory pool hit rate (percentage of allocations satisfied by the pool).
    /// </summary>
    public double PoolHitRate
    {
        get
        {
            var hits = Interlocked.Read(ref _poolHits);
            var misses = Interlocked.Read(ref _poolMisses);
            var total = hits + misses;
            return total > 0 ? (double)hits / total : 0.0;
        }
    }

    /// <summary>
    /// Gets the number of allocations satisfied by the memory pool.
    /// </summary>
    public long PoolHits => Interlocked.Read(ref _poolHits);

    /// <summary>
    /// Gets the number of allocations that could not be satisfied by the memory pool.
    /// </summary>
    public long PoolMisses => Interlocked.Read(ref _poolMisses);

    /// <summary>
    /// Gets the number of failed allocation attempts.
    /// </summary>
    public long FailedAllocations => Interlocked.Read(ref _failedAllocations);

    /// <summary>
    /// Gets the total number of copy operations performed.
    /// </summary>
    public long CopyOperations => Interlocked.Read(ref _copyOperations);

    /// <summary>
    /// Gets the memory efficiency ratio (bytes freed / bytes allocated).
    /// A value of 1.0 indicates all allocated memory has been freed.
    /// </summary>
    public double MemoryEfficiency
    {
        get
        {
            var allocated = TotalBytesAllocated;
            return allocated > 0 ? (double)TotalBytesFreed / allocated : 0.0;
        }
    }

    /// <summary>
    /// Gets the average allocation size in bytes.
    /// </summary>
    public double AverageAllocationSize
    {
        get
        {
            var allocations = TotalAllocations;
            return allocations > 0 ? (double)TotalBytesAllocated / allocations : 0.0;
        }
    }

    /// <summary>
    /// Records a successful allocation operation.
    /// </summary>
    /// <param name="bytes">The number of bytes allocated.</param>
    /// <param name="timeMs">The time taken for the allocation in milliseconds.</param>
    /// <param name="fromPool">Whether the allocation was satisfied by the memory pool.</param>
    public void RecordAllocation(long bytes, double timeMs, bool fromPool)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(timeMs);

        _ = Interlocked.Increment(ref _totalAllocations);
        _ = Interlocked.Add(ref _totalBytesAllocated, bytes);

        var newCurrentBytes = Interlocked.Add(ref _currentlyAllocatedBytes, bytes);

        // Update peak if necessary
        var currentPeak = Interlocked.Read(ref _peakAllocatedBytes);
        while (newCurrentBytes > currentPeak)
        {
            var original = Interlocked.CompareExchange(ref _peakAllocatedBytes, newCurrentBytes, currentPeak);
            if (original == currentPeak)
            {
                break;
            }


            currentPeak = original;
        }

        if (fromPool)
        {
            _ = Interlocked.Increment(ref _poolHits);
        }
        else
        {
            _ = Interlocked.Increment(ref _poolMisses);
        }

        lock (_syncLock)
        {
            _totalAllocationTimeMs += timeMs;
        }
    }

    /// <summary>
    /// Records a successful deallocation operation.
    /// </summary>
    /// <param name="bytes">The number of bytes deallocated.</param>
    /// <param name="timeMs">The time taken for the deallocation in milliseconds (optional).</param>
    public void RecordDeallocation(long bytes, double timeMs = 0.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(timeMs);

        _ = Interlocked.Increment(ref _totalDeallocations);
        _ = Interlocked.Add(ref _totalBytesFreed, bytes);
        _ = Interlocked.Add(ref _currentlyAllocatedBytes, -bytes);

        if (timeMs > 0)
        {
            lock (_syncLock)
            {
                _totalDeallocationTimeMs += timeMs;
            }
        }
    }

    /// <summary>
    /// Records a failed allocation attempt.
    /// </summary>
    /// <param name="requestedBytes">The number of bytes that were requested but could not be allocated.</param>
    public void RecordFailedAllocation(long requestedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requestedBytes);
        _ = Interlocked.Increment(ref _failedAllocations);
    }

    /// <summary>
    /// Records a memory copy operation.
    /// </summary>
    /// <param name="bytes">The number of bytes copied.</param>
    /// <param name="timeMs">The time taken for the copy operation in milliseconds.</param>
    /// <param name="isHostToDevice">Whether this was a host-to-device transfer.</param>
    public void RecordCopyOperation(long bytes, double timeMs, bool isHostToDevice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(timeMs);

        _ = Interlocked.Increment(ref _copyOperations);

        lock (_syncLock)
        {
            _totalCopyTimeMs += timeMs;
        }
    }

    /// <summary>
    /// Records buffer creation (for tracking buffer lifecycle).
    /// </summary>
    /// <param name="bytes">The size of the buffer created.</param>
    public static void RecordBufferCreation(long bytes)
    {
        // Static method for compatibility with existing code
        // In practice, this could be integrated into the instance methods
    }

    /// <summary>
    /// Creates a snapshot of the current statistics.
    /// This is useful for reporting without affecting the ongoing statistics collection.
    /// </summary>
    /// <returns>A new MemoryStatistics instance containing the current values.</returns>
    public MemoryStatistics CreateSnapshot()
    {
        lock (_syncLock)
        {
            return new MemoryStatistics
            {
                _totalAllocations = TotalAllocations,
                _totalDeallocations = TotalDeallocations,
                _totalBytesAllocated = TotalBytesAllocated,
                _totalBytesFreed = TotalBytesFreed,
                _currentlyAllocatedBytes = CurrentlyAllocatedBytes,
                _peakAllocatedBytes = PeakAllocatedBytes,
                _poolHits = PoolHits,
                _poolMisses = PoolMisses,
                _failedAllocations = FailedAllocations,
                _totalAllocationTimeMs = _totalAllocationTimeMs,
                _totalDeallocationTimeMs = _totalDeallocationTimeMs,
                _totalCopyTimeMs = _totalCopyTimeMs,
                _copyOperations = CopyOperations
            };
        }
    }

    /// <summary>
    /// Resets all statistics to zero.
    /// Use with caution as this will clear all historical data.
    /// </summary>
    public void Reset()
    {
        lock (_syncLock)
        {
            _ = Interlocked.Exchange(ref _totalAllocations, 0);
            _ = Interlocked.Exchange(ref _totalDeallocations, 0);
            _ = Interlocked.Exchange(ref _totalBytesAllocated, 0);
            _ = Interlocked.Exchange(ref _totalBytesFreed, 0);
            _ = Interlocked.Exchange(ref _currentlyAllocatedBytes, 0);
            _ = Interlocked.Exchange(ref _peakAllocatedBytes, 0);
            _ = Interlocked.Exchange(ref _poolHits, 0);
            _ = Interlocked.Exchange(ref _poolMisses, 0);
            _ = Interlocked.Exchange(ref _failedAllocations, 0);
            _ = Interlocked.Exchange(ref _copyOperations, 0);
            _totalAllocationTimeMs = 0.0;
            _totalDeallocationTimeMs = 0.0;
            _totalCopyTimeMs = 0.0;
        }
    }

    /// <summary>
    /// Gets a summary of the current memory statistics.
    /// </summary>
    /// <returns>A formatted string containing key statistics.</returns>
    public override string ToString()
    {
        return $"MemoryStatistics {{ " +
               $"CurrentlyAllocated: {CurrentlyAllocatedBytes:N0} bytes, " +
               $"Peak: {PeakAllocatedBytes:N0} bytes, " +
               $"TotalAllocations: {TotalAllocations:N0}, " +
               $"ActiveAllocations: {ActiveAllocations:N0}, " +
               $"PoolHitRate: {PoolHitRate:P2}, " +
               $"AvgAllocationTime: {AverageAllocationTime:F2}ms, " +
               $"MemoryEfficiency: {MemoryEfficiency:P2} " +
               $"}}";
    }

    /// <summary>
    /// Gets detailed statistics for diagnostic purposes.
    /// </summary>
    /// <returns>A detailed string representation of all statistics.</returns>
    public string ToDetailedString()
    {
        var snapshot = CreateSnapshot();
        return $"Detailed Memory Statistics:\n" +
               $"  Memory Usage:\n" +
               $"    Currently Allocated: {snapshot.CurrentlyAllocatedBytes:N0} bytes\n" +
               $"    Peak Allocated: {snapshot.PeakAllocatedBytes:N0} bytes\n" +
               $"    Total Allocated: {snapshot.TotalBytesAllocated:N0} bytes\n" +
               $"    Total Freed: {snapshot.TotalBytesFreed:N0} bytes\n" +
               $"    Memory Efficiency: {snapshot.MemoryEfficiency:P2}\n" +
               $"  Allocation Statistics:\n" +
               $"    Total Allocations: {snapshot.TotalAllocations:N0}\n" +
               $"    Total Deallocations: {snapshot.TotalDeallocations:N0}\n" +
               $"    Active Allocations: {snapshot.ActiveAllocations:N0}\n" +
               $"    Failed Allocations: {snapshot.FailedAllocations:N0}\n" +
               $"    Average Allocation Size: {snapshot.AverageAllocationSize:N0} bytes\n" +
               $"  Pool Statistics:\n" +
               $"    Pool Hits: {snapshot.PoolHits:N0}\n" +
               $"    Pool Misses: {snapshot.PoolMisses:N0}\n" +
               $"    Pool Hit Rate: {snapshot.PoolHitRate:P2}\n" +
               $"  Performance Statistics:\n" +
               $"    Average Allocation Time: {snapshot.AverageAllocationTime:F2} ms\n" +
               $"    Average Deallocation Time: {snapshot.AverageDeallocationTime:F2} ms\n" +
               $"    Average Copy Time: {snapshot.AverageCopyTime:F2} ms\n" +
               $"    Total Copy Operations: {snapshot.CopyOperations:N0}";
    }
}

/// <summary>
/// Memory pool for efficient buffer reuse with comprehensive statistics.
/// This consolidates memory pool functionality from multiple implementations.
/// </summary>
public sealed class MemoryPool : IDisposable
{
    private const int CleanupTimerIntervalMinutes = 10;
    private const int MaxBuffersPerPoolSize = 10;

    private readonly ILogger _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, Queue<nint>> _pools = new();
    private readonly Timer _cleanupTimer;
    private readonly MemoryStatistics _poolStatistics = new();
    private bool _disposed;

    /// <summary>
    /// Gets the statistics for this memory pool.
    /// </summary>
    public MemoryStatistics Statistics => _poolStatistics.CreateSnapshot();

    /// <summary>
    /// Creates a new memory pool with periodic cleanup.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    public MemoryPool(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(CleanupTimerIntervalMinutes), TimeSpan.FromMinutes(CleanupTimerIntervalMinutes));

        _logger.LogDebug("Memory pool initialized with periodic cleanup every 10 minutes");
    }

    /// <summary>
    /// Attempts to get a buffer from the pool.
    /// </summary>
    /// <param name="size">The requested buffer size.</param>
    /// <param name="options">Memory options (currently unused).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A pooled buffer handle, or null if none available.</returns>
    public ValueTask<IntPtr?> TryGetBufferAsync(long size, Abstractions.Memory.MemoryOptions options, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return ValueTask.FromResult<IntPtr?>(null);
        }

        // Round up to nearest power of 2 for better pooling efficiency
        var poolSize = RoundToPowerOfTwo(size);

        if (_pools.TryGetValue(poolSize, out var queue))
        {
            lock (queue)
            {
                if (queue.Count > 0)
                {
                    var buffer = queue.Dequeue();
                    _poolStatistics.RecordAllocation(size, 0.0, fromPool: true);
                    _logger.LogTrace("Retrieved buffer of size {Size} from pool (pooled size: {PoolSize})", size, poolSize);
                    return ValueTask.FromResult<IntPtr?>(buffer);
                }
            }
        }

        _poolStatistics.RecordFailedAllocation(size);
        return ValueTask.FromResult<IntPtr?>(null);
    }

    /// <summary>
    /// Returns a buffer to the pool for reuse.
    /// </summary>
    /// <param name="handle">The buffer handle to return.</param>
    /// <param name="size">The original size of the buffer.</param>
    /// <returns>A task representing the async operation.</returns>
    public ValueTask ReturnBufferAsync(IntPtr handle, long size)
    {
        if (_disposed || handle == IntPtr.Zero)
        {
            return ValueTask.CompletedTask;
        }

        var poolSize = RoundToPowerOfTwo(size);
        var queue = _pools.GetOrAdd(poolSize, _ => new Queue<nint>());

        lock (queue)
        {
            // Limit pool size to prevent excessive memory usage
            if (queue.Count < MaxBuffersPerPoolSize)
            {
                queue.Enqueue(handle);
                _poolStatistics.RecordDeallocation(size);
                _logger.LogTrace("Returned buffer of size {Size} to pool (pooled size: {PoolSize})", size, poolSize);
            }
            else
            {
                // Pool is full, free the buffer
                System.Runtime.InteropServices.Marshal.FreeHGlobal(handle);
                _poolStatistics.RecordDeallocation(size);
                _logger.LogTrace("Pool full, freed buffer of size {Size} directly", size);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Performs maintenance on the memory pool, freeing unused buffers.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public async ValueTask PerformMaintenanceAsync()
    {
        if (_disposed)
        {
            return;
        }

        await Task.Run(() =>
        {
            var totalFreed = 0;
            foreach (var kvp in _pools)
            {
                var queue = kvp.Value;
                lock (queue)
                {
                    // Keep only recent buffers, free the rest
                    var keepCount = Math.Min(queue.Count, 5); // Keep max 5 buffers per size
                    var freeCount = queue.Count - keepCount;

                    for (var i = 0; i < freeCount; i++)
                    {
                        if (queue.TryDequeue(out var buffer))
                        {
                            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
                            totalFreed++;
                        }
                    }
                }
            }

            if (totalFreed > 0)
            {
                _logger.LogDebug("Memory pool maintenance freed {Count} unused buffers", totalFreed);
            }
        });
    }

    private void PerformCleanup(object? state) => _ = Task.Run(async () =>
    {
        try
        {
            await PerformMaintenanceAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Memory maintenance failed: {ex.Message}");
        }
    });

    private static long RoundToPowerOfTwo(long value)
    {
        if (value <= 0)
        {
            return 1;
        }

        var result = 1L;
        while (result < value)
        {
            result <<= 1;
        }
        return result;
    }

    /// <summary>
    /// Disposes the memory pool and frees all pooled buffers.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cleanupTimer?.Dispose();

            var totalBuffers = 0;
            foreach (var queue in _pools.Values)
            {
                lock (queue)
                {
                    while (queue.TryDequeue(out var buffer))
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
                        totalBuffers++;
                    }
                }
            }

            _pools.Clear();
            _logger.LogDebug("Memory pool disposed, freed {Count} pooled buffers", totalBuffers);
        }
    }
}
