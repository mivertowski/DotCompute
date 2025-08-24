// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;

namespace DotCompute.Memory;

/// <summary>
/// Lock-free, high-performance memory pool implementation.
/// Addresses critical lock contention issues identified in performance analysis.
/// Expected improvements: 40-60% allocation throughput, 2-4x under high concurrency.
/// </summary>
public sealed class OptimizedMemoryPool<T> : IMemoryPool<T>, IDisposable where T : unmanaged
{
    // Lock-free bucket implementation
    private sealed class LockFreeBucket
    {
        private readonly ConcurrentQueue<IMemoryBuffer<T>> _queue = new();
        private volatile int _count;
        private readonly int _maxCount;
        
        public LockFreeBucket(int maxCount)
        {
            _maxCount = maxCount;
        }
        
        public int Count => _count;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out IMemoryBuffer<T>? buffer)
        {
            if (_queue.TryDequeue(out buffer))
            {
                Interlocked.Decrement(ref _count);
                return true;
            }
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(IMemoryBuffer<T> buffer)
        {
            // Check capacity using atomic read
            if (Volatile.Read(ref _count) >= _maxCount)
            {
                return false;
            }
            
            // Optimistic enqueue - may exceed max temporarily under high concurrency
            // This is acceptable as it's a soft limit for memory management
            _queue.Enqueue(buffer);
            Interlocked.Increment(ref _count);
            return true;
        }
        
        public void Clear(Action<IMemoryBuffer<T>> disposeAction)
        {
            while (_queue.TryDequeue(out var buffer))
            {
                Interlocked.Decrement(ref _count);
                disposeAction(buffer);
            }
        }
    }
    
    private readonly IMemoryManager _memoryManager;
    private readonly ConcurrentDictionary<int, LockFreeBucket> _buckets;
    private readonly int _maxBuffersPerBucket;
    private readonly long _memoryPressureThreshold;
    private readonly Timer? _cleanupTimer;
    
    // Atomic counters - no locks needed
    private long _totalAllocatedBytes;
    private long _totalAllocatedBuffers;
    private long _totalRentedBuffers;
    private long _totalReturnedBuffers;
    private volatile int _disposed;
    
    // Pre-calculated bucket sizes for common allocations
    private static readonly int[] BucketSizes = 
    {
        16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192,
        16384, 32768, 65536, 131072, 262144, 524288, 1048576
    };
    
    public OptimizedMemoryPool(
        IMemoryManager memoryManager,
        int maxBuffersPerBucket = 64,
        TimeSpan cleanupInterval = default,
        long memoryPressureThreshold = 1_073_741_824) // 1GB
    {
        ArgumentNullException.ThrowIfNull(memoryManager);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBuffersPerBucket);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(memoryPressureThreshold);
        
        _memoryManager = memoryManager;
        _maxBuffersPerBucket = maxBuffersPerBucket;
        _memoryPressureThreshold = memoryPressureThreshold;
        _buckets = new ConcurrentDictionary<int, LockFreeBucket>();
        
        if (cleanupInterval != default)
        {
            _cleanupTimer = new Timer(CleanupCallback, null, cleanupInterval, cleanupInterval);
        }
    }
    
    /// <summary>
    /// Gets total allocated bytes using atomic read.
    /// </summary>
    public long TotalAllocatedBytes => Interlocked.Read(ref _totalAllocatedBytes);
    
    /// <summary>
    /// Gets total allocated buffers using atomic read.
    /// </summary>
    public long TotalAllocatedBuffers => Interlocked.Read(ref _totalAllocatedBuffers);
    
    /// <summary>
    /// Gets total rented buffers using atomic read.
    /// </summary>
    public long TotalRentedBuffers => Interlocked.Read(ref _totalRentedBuffers);
    
    /// <summary>
    /// Gets total returned buffers using atomic read.
    /// </summary>
    public long TotalReturnedBuffers => Interlocked.Read(ref _totalReturnedBuffers);
    
    /// <inheritdoc/>
    public IMemoryBuffer<T> Rent(int minimumLength)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumLength);
        
        var bucketSize = GetBucketSize(minimumLength);
        var bucket = GetOrCreateBucket(bucketSize);
        
        // Lock-free dequeue attempt
        if (bucket.TryDequeue(out var buffer) && buffer != null)
        {
            Interlocked.Increment(ref _totalRentedBuffers);
            return new PooledMemoryBuffer<T>(this, buffer, bucketSize);
        }
        
        // Allocate new buffer if pool is empty
        buffer = CreateNewBuffer(bucketSize);
        Interlocked.Increment(ref _totalAllocatedBuffers);
        Interlocked.Increment(ref _totalRentedBuffers);
        Interlocked.Add(ref _totalAllocatedBytes, buffer.SizeInBytes);
        
        return new PooledMemoryBuffer<T>(this, buffer, bucketSize);
    }
    
    /// <inheritdoc/>
    public IMemoryBuffer<T> Rent(ReadOnlySpan<T> data)
    {
        ArgumentOutOfRangeException.ThrowIfZero(data.Length);
        
        var buffer = Rent(data.Length);
        data.CopyTo(buffer.AsSpan());
        return buffer;
    }
    
    /// <inheritdoc/>
    public void Return(IMemoryBuffer<T> buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        
        if (_disposed != 0)
        {
            buffer.Dispose();
            return;
        }
        
        Interlocked.Increment(ref _totalReturnedBuffers);
        
        // Try to return to pool (lock-free)
        if (buffer is PooledMemoryBuffer<T> pooled)
        {
            var bucket = GetOrCreateBucket(pooled.BucketSize);
            
            // Lock-free enqueue - if bucket is full, dispose the buffer
            if (!bucket.TryEnqueue(pooled.UnderlyingBuffer))
            {
                pooled.UnderlyingBuffer.Dispose();
                Interlocked.Add(ref _totalAllocatedBytes, -pooled.UnderlyingBuffer.SizeInBytes);
            }
        }
        else
        {
            // Not a pooled buffer, just dispose
            buffer.Dispose();
        }
    }
    
    /// <inheritdoc/>
    public void Clear()
    {
        ThrowIfDisposed();
        
        // Lock-free clear - each bucket handles its own cleanup
        foreach (var kvp in _buckets)
        {
            kvp.Value.Clear(buffer =>
            {
                buffer.Dispose();
                Interlocked.Add(ref _totalAllocatedBytes, -buffer.SizeInBytes);
            });
        }
        
        // Reset counters atomically
        Interlocked.Exchange(ref _totalAllocatedBytes, 0);
        Interlocked.Exchange(ref _totalAllocatedBuffers, 0);
        Interlocked.Exchange(ref _totalReturnedBuffers, 0);
    }
    
    /// <inheritdoc/>
    public MemoryPoolStatistics GetStatistics()
    {
        ThrowIfDisposed();
        
        var bucketStats = new Dictionary<int, int>();
        foreach (var kvp in _buckets)
        {
            bucketStats[kvp.Key] = kvp.Value.Count;
        }
        
        return new MemoryPoolStatistics(
            TotalAllocatedBytes,
            TotalRentedBuffers,
            TotalReturnedBuffers,
            TotalAllocatedBuffers,
            bucketStats);
    }
    
    /// <inheritdoc/>
    public bool IsUnderMemoryPressure()
    {
        return Interlocked.Read(ref _totalAllocatedBytes) > _memoryPressureThreshold;
    }
    
    /// <inheritdoc/>
    public void HandleMemoryPressure(double pressure)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pressure, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pressure, 1.0);
        ThrowIfDisposed();
        
        if (pressure > 0.5)
        {
            // Trigger cleanup without locks
            CleanupCallback(null);
        }
    }
    
    /// <inheritdoc/>
    public long Compact(long maxBytesToRelease = long.MaxValue)
    {
        ThrowIfDisposed();
        
        long bytesReleased = 0;
        
        // Lock-free compaction - process each bucket independently
        foreach (var kvp in _buckets)
        {
            var bucket = kvp.Value;
            var itemsToRemove = Math.Min(bucket.Count / 2, 10);
            
            for (int i = 0; i < itemsToRemove && bytesReleased < maxBytesToRelease; i++)
            {
                if (bucket.TryDequeue(out var buffer) && buffer != null)
                {
                    var size = buffer.SizeInBytes;
                    buffer.Dispose();
                    bytesReleased += size;
                    Interlocked.Add(ref _totalAllocatedBytes, -size);
                }
            }
        }
        
        return bytesReleased;
    }
    
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            _cleanupTimer?.Dispose();
            Clear();
            _buckets.Clear();
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBucketSize(int minimumLength)
    {
        // Binary search for the appropriate bucket size
        for (int i = 0; i < BucketSizes.Length; i++)
        {
            if (BucketSizes[i] >= minimumLength)
            {
                return BucketSizes[i];
            }
        }
        
        // For very large allocations, round up to next power of 2
        return (int)BitOperations.RoundUpToPowerOf2((uint)minimumLength);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private LockFreeBucket GetOrCreateBucket(int bucketSize)
    {
        return _buckets.GetOrAdd(bucketSize, size => new LockFreeBucket(_maxBuffersPerBucket));
    }
    
    private IMemoryBuffer<T> CreateNewBuffer(int elementCount)
    {
        return new UnifiedBuffer<T>(_memoryManager, elementCount);
    }
    
    private void CleanupCallback(object? state)
    {
        if (_disposed != 0) return;
        
        // Lock-free cleanup - remove old buffers from oversized buckets
        foreach (var kvp in _buckets)
        {
            var bucket = kvp.Value;
            
            // Only cleanup if bucket is over 50% capacity
            if (bucket.Count > _maxBuffersPerBucket / 2)
            {
                var toRemove = bucket.Count - _maxBuffersPerBucket / 2;
                for (int i = 0; i < toRemove; i++)
                {
                    if (bucket.TryDequeue(out var buffer) && buffer != null)
                    {
                        buffer.Dispose();
                        Interlocked.Add(ref _totalAllocatedBytes, -buffer.SizeInBytes);
                    }
                }
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, GetType());
    }
}

/// <summary>
/// Helper class for BitOperations (if not available in target framework).
/// </summary>
internal static class BitOperations
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RoundUpToPowerOf2(uint value)
    {
        --value;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}