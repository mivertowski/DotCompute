using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Memory.Benchmarks;

namespace DotCompute.Memory;

/// <summary>
/// Unified memory manager implementation that coordinates host and device memory
/// with efficient pooling and lazy synchronization.
/// </summary>
public sealed class UnifiedMemoryManager : IUnifiedMemoryManager
{
    private readonly IMemoryManager _baseMemoryManager;
    private readonly ConcurrentDictionary<Type, object> _pools = new();
    private readonly ConcurrentDictionary<object, WeakReference> _activeBuffers = new();
    private readonly object _lock = new();
    
    private long _totalAllocations;
    private long _totalReuses;
    private bool _isDisposed;
    
    /// <summary>
    /// Gets the underlying accelerator.
    /// </summary>
    public IAccelerator Accelerator => _baseMemoryManager.Accelerator;
    
    /// <summary>
    /// Initializes a new instance of the UnifiedMemoryManager.
    /// </summary>
    /// <param name="baseMemoryManager">The base memory manager to wrap.</param>
    public UnifiedMemoryManager(IMemoryManager baseMemoryManager)
    {
        _baseMemoryManager = baseMemoryManager ?? throw new ArgumentNullException(nameof(baseMemoryManager));
    }
    
    /// <summary>
    /// Creates a unified buffer with both host and device memory coordination.
    /// </summary>
    public async ValueTask<UnifiedBuffer<T>> CreateUnifiedBufferAsync<T>(
        int length,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        
        var buffer = new UnifiedBuffer<T>(_baseMemoryManager, length);
        
        // Track the buffer
        _activeBuffers.TryAdd(buffer, new WeakReference(buffer));
        
        Interlocked.Increment(ref _totalAllocations);
        
        return buffer;
    }
    
    /// <summary>
    /// Creates a unified buffer from existing data.
    /// </summary>
    public async ValueTask<UnifiedBuffer<T>> CreateUnifiedBufferFromAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var buffer = await CreateUnifiedBufferAsync<T>(source.Length, options, cancellationToken);
        await buffer.CopyFromAsync(source, cancellationToken);
        return buffer;
    }
    
    /// <summary>
    /// Gets the memory pool for the specified type.
    /// </summary>
    public MemoryPool<T> GetPool<T>() where T : unmanaged
    {
        return (MemoryPool<T>)_pools.GetOrAdd(typeof(T), _ => new MemoryPool<T>(_baseMemoryManager));
    }
    
    /// <summary>
    /// Gets memory statistics and performance metrics.
    /// </summary>
    public MemoryManagerStats GetStats()
    {
        CleanupDeadReferences();
        
        long totalAllocatedBytes = 0;
        long totalRetainedBytes = 0;
        long totalReuses = 0;
        int activePoolCount = 0;
        
        foreach (var kvp in _pools)
        {
            if (kvp.Value is MemoryPool<byte> bytePool)
            {
                var stats = bytePool.GetPerformanceStats();
                totalAllocatedBytes += stats.TotalAllocatedBytes;
                totalRetainedBytes += stats.TotalRetainedBytes;
                totalReuses += stats.ReuseCount;
                activePoolCount++;
            }
            else
            {
                // Use reflection to get stats from generic pools
                var poolType = kvp.Value.GetType();
                var getStatsMethod = poolType.GetMethod("GetPerformanceStats");
                if (getStatsMethod != null)
                {
                    var stats = getStatsMethod.Invoke(kvp.Value, null);
                    if (stats != null)
                    {
                        var statsType = stats.GetType();
                        var allocatedBytesProperty = statsType.GetProperty("TotalAllocatedBytes");
                        var retainedBytesProperty = statsType.GetProperty("TotalRetainedBytes");
                        var reuseCountProperty = statsType.GetProperty("ReuseCount");
                        
                        if (allocatedBytesProperty != null)
                            totalAllocatedBytes += (long)allocatedBytesProperty.GetValue(stats)!;
                        if (retainedBytesProperty != null)
                            totalRetainedBytes += (long)retainedBytesProperty.GetValue(stats)!;
                        if (reuseCountProperty != null)
                            totalReuses += (long)reuseCountProperty.GetValue(stats)!;
                        
                        activePoolCount++;
                    }
                }
            }
        }
        
        return new MemoryManagerStats
        {
            TotalAllocatedBytes = totalAllocatedBytes,
            TotalRetainedBytes = totalRetainedBytes,
            TotalAllocations = Volatile.Read(ref _totalAllocations),
            TotalReuses = totalReuses,
            EfficiencyRatio = _totalAllocations > 0 ? (double)totalReuses / _totalAllocations : 0.0,
            AvailableDeviceMemory = _baseMemoryManager.GetAvailableMemory(),
            TotalDeviceMemory = _baseMemoryManager.GetTotalMemory(),
            ActiveUnifiedBuffers = _activeBuffers.Count,
            ActiveMemoryPools = activePoolCount
        };
    }
    
    /// <summary>
    /// Handles memory pressure by releasing unused resources.
    /// </summary>
    public async ValueTask HandleMemoryPressureAsync(double pressure)
    {
        if (pressure < 0.0 || pressure > 1.0)
            throw new ArgumentOutOfRangeException(nameof(pressure));
        
        ThrowIfDisposed();
        
        // Clean up dead references first
        CleanupDeadReferences();
        
        // Handle pressure in all pools
        foreach (var kvp in _pools)
        {
            if (kvp.Value is MemoryPool<byte> pool)
            {
                pool.HandleMemoryPressure(pressure);
            }
            else
            {
                // Use reflection to call HandleMemoryPressure on generic pools
                var poolType = kvp.Value.GetType();
                var handlePressureMethod = poolType.GetMethod("HandleMemoryPressure");
                handlePressureMethod?.Invoke(kvp.Value, new object[] { pressure });
            }
        }
        
        // Force garbage collection if pressure is high
        if (pressure > 0.8)
        {
            GC.Collect(1, GCCollectionMode.Optimized);
            GC.WaitForPendingFinalizers();
        }
    }
    
    /// <summary>
    /// Compacts all memory pools and releases unused memory.
    /// </summary>
    public async ValueTask<long> CompactAsync()
    {
        ThrowIfDisposed();
        
        long totalReleased = 0;
        
        foreach (var kvp in _pools)
        {
            if (kvp.Value is MemoryPool<byte> pool)
            {
                totalReleased += pool.Compact();
            }
            else
            {
                // Use reflection to call Compact on generic pools
                var poolType = kvp.Value.GetType();
                var compactMethod = poolType.GetMethod("Compact", new[] { typeof(long) });
                if (compactMethod != null)
                {
                    var result = compactMethod.Invoke(kvp.Value, new object[] { long.MaxValue });
                    if (result is long released)
                        totalReleased += released;
                }
            }
        }
        
        CleanupDeadReferences();
        
        return totalReleased;
    }
    
    /// <summary>
    /// Runs performance benchmarks.
    /// </summary>
    public async ValueTask<MemoryBenchmarkResults> RunBenchmarksAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        return await MemoryBenchmarks.RunComprehensiveBenchmarkAsync(_baseMemoryManager, cancellationToken);
    }
    
    #region IMemoryManager Implementation (Abstractions)
    
    public DeviceMemory Allocate(long sizeInBytes)
    {
        ThrowIfDisposed();
        return _baseMemoryManager.Allocate(sizeInBytes);
    }
    
    public DeviceMemory AllocateAligned(long sizeInBytes, int alignment)
    {
        ThrowIfDisposed();
        return _baseMemoryManager.AllocateAligned(sizeInBytes, alignment);
    }
    
    public void Free(DeviceMemory memory)
    {
        if (!_isDisposed)
        {
            _baseMemoryManager.Free(memory);
        }
    }
    
    public void CopyToDevice<T>(ReadOnlySpan<T> source, DeviceMemory destination) where T : unmanaged
    {
        ThrowIfDisposed();
        _baseMemoryManager.CopyToDevice(source, destination);
    }
    
    public void CopyToHost<T>(DeviceMemory source, Span<T> destination) where T : unmanaged
    {
        ThrowIfDisposed();
        _baseMemoryManager.CopyToHost(source, destination);
    }
    
    public void CopyDeviceToDevice(DeviceMemory source, DeviceMemory destination, long sizeInBytes)
    {
        ThrowIfDisposed();
        _baseMemoryManager.CopyDeviceToDevice(source, destination, sizeInBytes);
    }
    
    public void CopyToDeviceWithContext<T>(ReadOnlyMemory<T> source, DeviceMemory destination, AcceleratorContext context) where T : unmanaged
    {
        ThrowIfDisposed();
        _baseMemoryManager.CopyToDeviceWithContext(source, destination, context);
    }
    
    public void CopyToHostWithContext<T>(DeviceMemory source, Memory<T> destination, AcceleratorContext context) where T : unmanaged
    {
        ThrowIfDisposed();
        _baseMemoryManager.CopyToHostWithContext(source, destination, context);
    }
    
    public long GetAvailableMemory()
    {
        return _isDisposed ? 0 : _baseMemoryManager.GetAvailableMemory();
    }
    
    public long GetTotalMemory()
    {
        return _isDisposed ? 0 : _baseMemoryManager.GetTotalMemory();
    }
    
    public IMemoryOwner<T> AllocatePinnedHost<T>(int length) where T : unmanaged
    {
        ThrowIfDisposed();
        return _baseMemoryManager.AllocatePinnedHost<T>(length);
    }
    
    #endregion
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CleanupDeadReferences()
    {
        var deadKeys = new List<object>();
        
        foreach (var kvp in _activeBuffers)
        {
            if (!kvp.Value.IsAlive)
            {
                deadKeys.Add(kvp.Key);
            }
        }
        
        foreach (var key in deadKeys)
        {
            _activeBuffers.TryRemove(key, out _);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(UnifiedMemoryManager));
    }
    
    public void Dispose()
    {
        if (_isDisposed)
            return;
        
        lock (_lock)
        {
            if (_isDisposed)
                return;
            
            // Dispose all pools
            foreach (var kvp in _pools)
            {
                if (kvp.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            _pools.Clear();
            _activeBuffers.Clear();
            
            // Dispose base memory manager
            _baseMemoryManager?.Dispose();
            
            _isDisposed = true;
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;
        
        // Dispose all pools asynchronously if they support it
        foreach (var kvp in _pools)
        {
            if (kvp.Value is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (kvp.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        
        Dispose();
    }
}

/// <summary>
/// Memory allocation options for unified memory operations.
/// </summary>
[Flags]
public enum MemoryOptions
{
    /// <summary>
    /// No special options.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Memory is read-only.
    /// </summary>
    ReadOnly = 1,
    
    /// <summary>
    /// Memory is write-only.
    /// </summary>
    WriteOnly = 2,
    
    /// <summary>
    /// Memory should be allocated in host-visible memory if possible.
    /// </summary>
    HostVisible = 4,
    
    /// <summary>
    /// Memory should be cached if possible.
    /// </summary>
    Cached = 8,
    
    /// <summary>
    /// Memory will be used for atomic operations.
    /// </summary>
    Atomic = 16,
    
    /// <summary>
    /// Use lazy synchronization between host and device.
    /// </summary>
    LazySync = 32,
    
    /// <summary>
    /// Prefer pooled allocation for better performance.
    /// </summary>
    PreferPooled = 64
}