using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    private bool _isDisposed;
    
    /// <summary>
    /// Gets the underlying accelerator - not available in unified manager.
    /// </summary>
    // Note: Removed accelerator property as it's not available in the new interface
    
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
    public ValueTask<UnifiedBuffer<T>> CreateUnifiedBufferAsync<T>(
        int length,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        
        var buffer = new UnifiedBuffer<T>(_baseMemoryManager, length);
        
        // Track the buffer
        _activeBuffers.TryAdd(buffer, new WeakReference(buffer));
        
        Interlocked.Increment(ref _totalAllocations);
        
        return new ValueTask<UnifiedBuffer<T>>(buffer);
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
                var poolStats = GetPoolStatsViaReflection(kvp.Value);
                if (poolStats.HasValue)
                {
                    totalAllocatedBytes += poolStats.Value.totalAllocatedBytes;
                    totalRetainedBytes += poolStats.Value.totalRetainedBytes;
                    totalReuses += poolStats.Value.totalReuses;
                    activePoolCount++;
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
            AvailableDeviceMemory = GetAvailableDeviceMemory(),
            TotalDeviceMemory = GetTotalDeviceMemory(),
            ActiveUnifiedBuffers = _activeBuffers.Count,
            ActiveMemoryPools = activePoolCount
        };
    }

    private static long GetAvailableDeviceMemory()
    {
        try
        {
            // Use GC.GetTotalMemory to estimate available memory
            var currentMemory = GC.GetTotalMemory(false);
            var maxMemory = Environment.WorkingSet;
            
            // Conservative estimate: return 80% of working set minus current allocation
            var availableMemory = (long)(maxMemory * 0.8) - currentMemory;
            return Math.Max(0, availableMemory);
        }
        catch
        {
            // Fallback to conservative estimate
            return 1024L * 1024L * 1024L; // 1GB fallback
        }
    }

    private static long GetTotalDeviceMemory()
    {
        try
        {
            // For CPU backend, total device memory is system memory
            // Use working set as proxy for available system memory
            return Environment.WorkingSet;
        }
        catch
        {
            // Fallback to conservative estimate
            return 8L * 1024L * 1024L * 1024L; // 8GB fallback
        }
    }
    
    /// <summary>
    /// Handles memory pressure by releasing unused resources.
    /// </summary>
    public ValueTask HandleMemoryPressureAsync(double pressure)
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
                InvokeHandleMemoryPressureViaReflection(kvp.Value, pressure);
            }
        }
        
        // Force garbage collection if pressure is high
        if (pressure > 0.8)
        {
            GC.Collect(1, GCCollectionMode.Optimized);
            GC.WaitForPendingFinalizers();
        }
        
        return ValueTask.CompletedTask;
    }
    
    /// <summary>
    /// Compacts all memory pools and releases unused memory.
    /// </summary>
    public ValueTask<long> CompactAsync()
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
                var released = InvokeCompactViaReflection(kvp.Value);
                if (released.HasValue)
                    totalReleased += released.Value;
            }
        }
        
        CleanupDeadReferences();
        
        return new ValueTask<long>(totalReleased);
    }
    
    /// <summary>
    /// Runs performance benchmarks.
    /// </summary>
    public ValueTask<MemoryBenchmarkResults> RunBenchmarksAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        // TODO: Implement comprehensive benchmark when MemoryBenchmarks class is available
        var results = new MemoryBenchmarkResults
        {
            TransferBandwidth = new(),
            AllocationOverhead = new(),
            MemoryUsagePatterns = new(),
            PoolPerformance = new(),
            UnifiedBufferPerformance = new()
        };
        
        return new ValueTask<MemoryBenchmarkResults>(results);
    }
    
    #region IMemoryManager Implementation (Abstractions)
    
    // Async interface implementation
    public ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _baseMemoryManager.AllocateAsync(sizeInBytes, options, cancellationToken);
    }

    public ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        return _baseMemoryManager.AllocateAndCopyAsync(source, options, cancellationToken);
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        ThrowIfDisposed();
        return _baseMemoryManager.CreateView(buffer, offset, length);
    }

    // Note: Legacy sync methods removed as they are not part of the new IMemoryManager interface
    
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
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }
    
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", 
        Justification = "The generic MemoryPool<T> types are preserved via other code paths")]
    private static (long totalAllocatedBytes, long totalRetainedBytes, long totalReuses)? GetPoolStatsViaReflection(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] object pool)
    {
        var poolType = pool.GetType();
        var getStatsMethod = poolType.GetMethod("GetPerformanceStats");
        if (getStatsMethod != null)
        {
            var stats = getStatsMethod.Invoke(pool, null);
            if (stats != null)
            {
                return ExtractStatsFromObject(stats);
            }
        }
        return null;
    }
    
    private static (long totalAllocatedBytes, long totalRetainedBytes, long totalReuses) ExtractStatsFromObject(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] object stats)
    {
        var statsType = stats.GetType();
        var allocatedBytesProperty = statsType.GetProperty("TotalAllocatedBytes");
        var retainedBytesProperty = statsType.GetProperty("TotalRetainedBytes");
        var reuseCountProperty = statsType.GetProperty("ReuseCount");
        
        long totalAllocatedBytes = 0;
        long totalRetainedBytes = 0;
        long totalReuses = 0;
        
        if (allocatedBytesProperty != null)
            totalAllocatedBytes = (long)allocatedBytesProperty.GetValue(stats)!;
        if (retainedBytesProperty != null)
            totalRetainedBytes = (long)retainedBytesProperty.GetValue(stats)!;
        if (reuseCountProperty != null)
            totalReuses = (long)reuseCountProperty.GetValue(stats)!;
            
        return (totalAllocatedBytes, totalRetainedBytes, totalReuses);
    }
    
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", 
        Justification = "The generic MemoryPool<T> types are preserved via other code paths")]
    private static void InvokeHandleMemoryPressureViaReflection(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] object pool, 
        double pressure)
    {
        var poolType = pool.GetType();
        var handlePressureMethod = poolType.GetMethod("HandleMemoryPressure");
        handlePressureMethod?.Invoke(pool, new object[] { pressure });
    }
    
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", 
        Justification = "The generic MemoryPool<T> types are preserved via other code paths")]
    private static long? InvokeCompactViaReflection(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] object pool)
    {
        var poolType = pool.GetType();
        var compactMethod = poolType.GetMethod("Compact", Type.EmptyTypes);
        if (compactMethod != null)
        {
            var result = compactMethod.Invoke(pool, null);
            if (result is long released)
                return released;
        }
        return null;
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
            
            // Note: base memory manager disposal handled by DI container or caller
            
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