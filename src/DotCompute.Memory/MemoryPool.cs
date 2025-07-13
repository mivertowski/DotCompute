// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using DotCompute.Abstractions;

namespace DotCompute.Memory;

/// <summary>
/// A high-performance memory pool that uses power-of-2 bucket allocation for efficient memory reuse.
/// Provides thread-safe concurrent operations and memory pressure monitoring.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class MemoryPool<T> : IDisposable where T : unmanaged
{
    private readonly IMemoryManager _memoryManager;
    private readonly ConcurrentDictionary<int, ConcurrentQueue<IMemoryBuffer<T>>> _buckets;
    private readonly object _lock = new();
    private readonly Timer _cleanupTimer;
    
    private volatile bool _disposed;
    private long _totalAllocatedBytes;
    private long _totalRentedBuffers;
    private long _totalReturnedBuffers;
    private long _totalAllocatedBuffers;
    
    // Configuration
    private readonly int _maxBuffersPerBucket;
    private readonly TimeSpan _cleanupInterval;
    private readonly long _memoryPressureThreshold;
    
    /// <summary>
    /// Gets the total number of bytes currently allocated by the pool.
    /// </summary>
    public long TotalAllocatedBytes => Interlocked.Read(ref _totalAllocatedBytes);
    
    /// <summary>
    /// Gets the total number of buffers currently rented from the pool.
    /// </summary>
    public long TotalRentedBuffers => Interlocked.Read(ref _totalRentedBuffers);
    
    /// <summary>
    /// Gets the total number of buffers returned to the pool.
    /// </summary>
    public long TotalReturnedBuffers => Interlocked.Read(ref _totalReturnedBuffers);
    
    /// <summary>
    /// Gets the total number of buffers allocated by the pool.
    /// </summary>
    public long TotalAllocatedBuffers => Interlocked.Read(ref _totalAllocatedBuffers);
    
    /// <summary>
    /// Gets the memory manager used by this pool.
    /// </summary>
    public IMemoryManager MemoryManager => _memoryManager;
    
    /// <summary>
    /// Initializes a new instance of the MemoryPool class.
    /// </summary>
    /// <param name="memoryManager">The memory manager to use for allocations.</param>
    /// <param name="maxBuffersPerBucket">The maximum number of buffers to keep in each bucket.</param>
    /// <param name="cleanupInterval">The interval at which to clean up unused buffers.</param>
    /// <param name="memoryPressureThreshold">The memory pressure threshold in bytes.</param>
    public MemoryPool(IMemoryManager memoryManager, 
                     int maxBuffersPerBucket = 64,
                     TimeSpan cleanupInterval = default,
                     long memoryPressureThreshold = 1024 * 1024 * 1024) // 1GB
    {
        ArgumentNullException.ThrowIfNull(memoryManager);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBuffersPerBucket);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(memoryPressureThreshold);
        
        _memoryManager = memoryManager;
        _maxBuffersPerBucket = maxBuffersPerBucket;
        _cleanupInterval = cleanupInterval == default ? TimeSpan.FromMinutes(5) : cleanupInterval;
        _memoryPressureThreshold = memoryPressureThreshold;
        
        _buckets = new ConcurrentDictionary<int, ConcurrentQueue<IMemoryBuffer<T>>>();
        
        // Start cleanup timer
        _cleanupTimer = new Timer(CleanupCallback, null, _cleanupInterval, _cleanupInterval);
    }
    
    /// <summary>
    /// Rents a buffer from the pool with at least the specified length.
    /// </summary>
    /// <param name="minimumLength">The minimum length required.</param>
    /// <returns>A rented buffer.</returns>
    public IMemoryBuffer<T> Rent(int minimumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumLength);
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        var bucketSize = GetBucketSize(minimumLength);
        var bucket = GetOrCreateBucket(bucketSize);
        
        // Try to get a buffer from the bucket
        if (bucket.TryDequeue(out var buffer))
        {
            Interlocked.Increment(ref _totalRentedBuffers);
            return new PooledMemoryBuffer<T>(this, buffer, bucketSize);
        }
        
        // Create a new buffer
        buffer = new UnifiedBuffer<T>(_memoryManager, bucketSize);
        Interlocked.Increment(ref _totalAllocatedBuffers);
        Interlocked.Increment(ref _totalRentedBuffers);
        Interlocked.Add(ref _totalAllocatedBytes, buffer.SizeInBytes);
        
        return new PooledMemoryBuffer<T>(this, buffer, bucketSize);
    }
    
    /// <summary>
    /// Rents a buffer from the pool with the specified length and initial data.
    /// </summary>
    /// <param name="data">The initial data to populate the buffer with.</param>
    /// <returns>A rented buffer.</returns>
    public IMemoryBuffer<T> Rent(ReadOnlySpan<T> data)
    {
        ArgumentOutOfRangeException.ThrowIfZero(data.Length);
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        var buffer = Rent(data.Length);
        data.CopyTo(buffer.AsSpan());
        return buffer;
    }
    
    /// <summary>
    /// Returns a buffer to the pool.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    /// <param name="bucketSize">The bucket size the buffer belongs to.</param>
    internal void Return(IMemoryBuffer<T> buffer, int bucketSize)
    {
        if (_disposed || buffer == null)
            return;
        
        var bucket = GetOrCreateBucket(bucketSize);
        
        // Check if bucket is not full
        if (bucket.Count < _maxBuffersPerBucket)
        {
            bucket.Enqueue(buffer);
            Interlocked.Decrement(ref _totalRentedBuffers);
            Interlocked.Increment(ref _totalReturnedBuffers);
        }
        else
        {
            // Bucket is full, dispose the buffer
            buffer.Dispose();
            Interlocked.Decrement(ref _totalRentedBuffers);
            Interlocked.Add(ref _totalAllocatedBytes, -buffer.SizeInBytes);
        }
    }
    
    /// <summary>
    /// Clears all buffers from the pool.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        lock (_lock)
        {
            foreach (var bucket in _buckets.Values)
            {
                while (bucket.TryDequeue(out var buffer))
                {
                    buffer.Dispose();
                    Interlocked.Add(ref _totalAllocatedBytes, -buffer.SizeInBytes);
                }
            }
            
            _buckets.Clear();
            Interlocked.Exchange(ref _totalAllocatedBytes, 0);
            Interlocked.Exchange(ref _totalAllocatedBuffers, 0);
            Interlocked.Exchange(ref _totalReturnedBuffers, 0);
        }
    }
    
    /// <summary>
    /// Gets statistics about the memory pool.
    /// </summary>
    /// <returns>Memory pool statistics.</returns>
    public MemoryPoolStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
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
            bucketStats
        );
    }
    
    /// <summary>
    /// Checks if the pool is under memory pressure.
    /// </summary>
    /// <returns>True if under memory pressure.</returns>
    public bool IsUnderMemoryPressure()
    {
        return TotalAllocatedBytes > _memoryPressureThreshold;
    }
    
    /// <summary>
    /// Gets performance statistics for the memory pool.
    /// </summary>
    /// <returns>Performance statistics.</returns>
    public MemoryPoolPerformanceStats GetPerformanceStats()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        return new MemoryPoolPerformanceStats(
            TotalAllocatedBytes,
            TotalAllocatedBytes, // TotalRetainedBytes - assuming all allocated bytes are retained
            TotalReturnedBuffers // ReuseCount
        );
    }
    
    /// <summary>
    /// Handles memory pressure by releasing unused buffers.
    /// </summary>
    /// <param name="pressure">Memory pressure level (0.0 to 1.0).</param>
    public void HandleMemoryPressure(double pressure)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pressure, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pressure, 1.0);
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (pressure > 0.5)
        {
            CleanupOldBuffers();
        }
    }
    
    /// <summary>
    /// Compacts the memory pool and releases unused memory.
    /// </summary>
    /// <param name="maxBytesToRelease">Maximum bytes to release.</param>
    /// <returns>Number of bytes actually released.</returns>
    public long Compact(long maxBytesToRelease = long.MaxValue)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        long bytesReleased = 0;
        
        lock (_lock)
        {
            if (_disposed)
                return 0;
            
            foreach (var bucket in _buckets.Values)
            {
                var itemsToRemove = Math.Min(bucket.Count / 2, 10); // Remove up to 50% or 10 items
                
                for (int i = 0; i < itemsToRemove && bytesReleased < maxBytesToRelease; i++)
                {
                    if (bucket.TryDequeue(out var buffer))
                    {
                        bytesReleased += buffer.SizeInBytes;
                        buffer.Dispose();
                        Interlocked.Add(ref _totalAllocatedBytes, -buffer.SizeInBytes);
                    }
                }
            }
        }
        
        return bytesReleased;
    }
    
    /// <summary>
    /// Releases all resources used by the MemoryPool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        
        lock (_lock)
        {
            if (_disposed)
                return;
            
            _disposed = true;
            
            _cleanupTimer?.Dispose();
            Clear();
        }
    }
    
    #region Private Methods
    
    private static int GetBucketSize(int minimumLength)
    {
        // Round up to the next power of 2
        if (minimumLength <= 0)
            return 1;
        
        var size = 1;
        while (size < minimumLength)
        {
            size <<= 1;
        }
        
        return size;
    }
    
    private ConcurrentQueue<IMemoryBuffer<T>> GetOrCreateBucket(int size)
    {
        return _buckets.GetOrAdd(size, _ => new ConcurrentQueue<IMemoryBuffer<T>>());
    }
    
    private void CleanupCallback(object? state)
    {
        if (_disposed)
            return;
        
        try
        {
            // Check memory pressure and clean up if needed
            if (IsUnderMemoryPressure())
            {
                CleanupOldBuffers();
            }
        }
        catch (Exception)
        {
            // Ignore cleanup errors
        }
    }
    
    private void CleanupOldBuffers()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            
            // Remove some buffers from each bucket to reduce memory pressure
            foreach (var bucket in _buckets.Values)
            {
                var itemsToRemove = bucket.Count / 4; // Remove 25% of buffers
                
                for (int i = 0; i < itemsToRemove; i++)
                {
                    if (bucket.TryDequeue(out var buffer))
                    {
                        buffer.Dispose();
                        Interlocked.Add(ref _totalAllocatedBytes, -buffer.SizeInBytes);
                    }
                }
            }
        }
    }
    
    #endregion
}

/// <summary>
/// A pooled memory buffer that automatically returns itself to the pool when disposed.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class PooledMemoryBuffer<T> : IMemoryBuffer<T> where T : unmanaged
{
    private readonly MemoryPool<T> _pool;
    private readonly IMemoryBuffer<T> _buffer;
    private readonly int _bucketSize;
    private volatile bool _disposed;
    
    public int Length => _buffer.Length;
    public long SizeInBytes => _buffer.SizeInBytes;
    public bool IsOnHost => _buffer.IsOnHost;
    public bool IsOnDevice => _buffer.IsOnDevice;
    public bool IsDirty => _buffer.IsDirty;
    public BufferState State => _buffer.State;
    
    public PooledMemoryBuffer(MemoryPool<T> pool, IMemoryBuffer<T> buffer, int bucketSize)
    {
        _pool = pool;
        _buffer = buffer;
        _bucketSize = bucketSize;
    }
    
    public Span<T> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.AsSpan();
    }
    
    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.AsReadOnlySpan();
    }
    
    public Memory<T> AsMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.AsMemory();
    }
    
    public ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.AsReadOnlyMemory();
    }
    
    public DeviceMemory GetDeviceMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.GetDeviceMemory();
    }
    
    public void EnsureOnHost()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.EnsureOnHost();
    }
    
    public void EnsureOnDevice()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.EnsureOnDevice();
    }
    
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.EnsureOnHostAsync(context, cancellationToken);
    }
    
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.EnsureOnDeviceAsync(context, cancellationToken);
    }
    
    public void MarkHostDirty()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.MarkHostDirty();
    }
    
    public void MarkDeviceDirty()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.MarkDeviceDirty();
    }
    
    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.Synchronize();
    }
    
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.SynchronizeAsync(context, cancellationToken);
    }
    
    public Memory<T> GetMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.GetMemory();
    }
    
    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.CopyFromAsync(source, cancellationToken);
    }
    
    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.CopyToAsync(destination, cancellationToken);
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        _pool.Return(_buffer, _bucketSize);
    }
}

/// <summary>
/// Statistics about a memory pool.
/// </summary>
/// <param name="TotalAllocatedBytes">The total number of bytes allocated.</param>
/// <param name="TotalRentedBuffers">The total number of buffers currently rented.</param>
/// <param name="TotalReturnedBuffers">The total number of buffers returned.</param>
/// <param name="TotalAllocatedBuffers">The total number of buffers allocated.</param>
/// <param name="BucketStatistics">Statistics per bucket size.</param>
public record MemoryPoolStatistics(
    long TotalAllocatedBytes,
    long TotalRentedBuffers,
    long TotalReturnedBuffers,
    long TotalAllocatedBuffers,
    IReadOnlyDictionary<int, int> BucketStatistics
);

/// <summary>
/// Performance statistics for a memory pool.
/// </summary>
/// <param name="TotalAllocatedBytes">The total number of bytes allocated.</param>
/// <param name="TotalRetainedBytes">The total number of bytes retained in the pool.</param>
/// <param name="ReuseCount">The number of times buffers were reused.</param>
public record MemoryPoolPerformanceStats(
    long TotalAllocatedBytes,
    long TotalRetainedBytes,
    long ReuseCount
);