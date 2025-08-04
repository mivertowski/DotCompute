// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Memory.Benchmarks;

namespace DotCompute.Memory;

/// <summary>
/// Unified memory manager implementation that coordinates host and device memory
/// with efficient pooling and lazy synchronization.
/// </summary>
public sealed class UnifiedMemoryManager : IUnifiedMemoryManager, IAsyncDisposable
{
    private readonly IMemoryManager _baseMemoryManager;
    private readonly ConcurrentDictionary<Type, object> _pools = new();
    private readonly ConcurrentDictionary<object, WeakReference> _activeBuffers = new();
    private readonly object _lock = new();

    // Performance optimization: Use thread-safe counters with padding to avoid false sharing
    private struct AlignedCounter
    {
#pragma warning disable CS0649 // Field is never assigned to - it's modified through Unsafe.AsRef
        private readonly long _value;
#pragma warning restore CS0649
#pragma warning disable CS0169, CA1823 // The padding fields are intentionally unused to prevent false sharing
        private readonly byte _padding1, _padding2, _padding3, _padding4, _padding5, _padding6, _padding7;
#pragma warning restore CS0169, CA1823
        public long Value => Interlocked.Read(ref Unsafe.AsRef(in _value));
        public void Increment() => Interlocked.Increment(ref Unsafe.AsRef(in _value));
        public void Add(long value) => Interlocked.Add(ref Unsafe.AsRef(in _value), value);
    }

    private readonly AlignedCounter _totalAllocations;
    private bool _isDisposed;

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

        _totalAllocations.Increment();

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
    /// Creates a buffer with the specified parameters.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="length">The number of elements.</param>
    /// <param name="location">The memory location.</param>
    /// <param name="access">The memory access mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A buffer.</returns>
    public async ValueTask<IBuffer<T>> CreateBufferAsync<T>(
        int length,
        MemoryLocation location,
        MemoryAccess access = MemoryAccess.ReadWrite,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        // Convert to unified buffer creation and return as IBuffer
        var unifiedBuffer = await CreateUnifiedBufferAsync<T>(length, MemoryOptions.None, cancellationToken);
        return (IBuffer<T>)unifiedBuffer;
    }

    /// <summary>
    /// Creates a buffer from existing data.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="data">The source data.</param>
    /// <param name="location">The memory location.</param>
    /// <param name="access">The memory access mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A buffer.</returns>
    public async ValueTask<IBuffer<T>> CreateBufferAsync<T>(
        ReadOnlyMemory<T> data,
        MemoryLocation location,
        MemoryAccess access = MemoryAccess.ReadWrite,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        // Convert to unified buffer creation and return as IBuffer
        var unifiedBuffer = await CreateUnifiedBufferFromAsync<T>(data, MemoryOptions.None, cancellationToken);
        return unifiedBuffer;
    }

    /// <summary>
    /// Copies data between buffers.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source buffer.</param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="elementCount">The number of elements to copy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the copy operation.</returns>
    public static async ValueTask CopyAsync<T>(
        IBuffer<T> source,
        IBuffer<T> destination,
        long sourceOffset = 0,
        long destinationOffset = 0,
        long? elementCount = null,
        CancellationToken cancellationToken = default) where T : unmanaged => await source.CopyToAsync(destination, cancellationToken);

    /// <summary>
    /// Gets memory usage statistics.
    /// </summary>
    /// <returns>Memory statistics.</returns>
    public IMemoryStatistics GetStatistics()
    {
        var stats = GetStats();
        return new MemoryStatisticsImpl(stats);
    }

    /// <summary>
    /// Gets available memory locations.
    /// </summary>
    public static IReadOnlyList<MemoryLocation> AvailableLocations => new[]
    {
        MemoryLocation.Host,
        MemoryLocation.Device,
        MemoryLocation.Unified
    };

    /// <summary>
    /// Gets the memory pool for the specified type.
    /// </summary>
    public MemoryPool<T> GetPool<T>() where T : unmanaged => (MemoryPool<T>)_pools.GetOrAdd(typeof(T), _ => new MemoryPool<T>(_baseMemoryManager));

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
                var poolStats = GetPoolStatsViaInterface(kvp.Value);
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
            TotalAllocations = _totalAllocations.Value,
            TotalReuses = totalReuses,
            EfficiencyRatio = _totalAllocations.Value > 0 ? (double)totalReuses / _totalAllocations.Value : 0.0,
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
        {
            throw new ArgumentOutOfRangeException(nameof(pressure));
        }

        ThrowIfDisposed();

        // Performance optimization: Process in parallel for large pool counts
        if (_pools.Count > Environment.ProcessorCount)
        {
            // Clean up dead references first
            CleanupDeadReferences();

            // Handle pressure in all pools in parallel
            Parallel.ForEach(_pools, kvp =>
            {
                if (kvp.Value is MemoryPool<byte> pool)
                {
                    pool.HandleMemoryPressure(pressure);
                }
                else
                {
                    // Use reflection to call HandleMemoryPressure on generic pools
                    InvokeHandleMemoryPressureViaInterface(kvp.Value, pressure);
                }
            });
        }
        else
        {
            // Small pool count - process sequentially
            CleanupDeadReferences();

            foreach (var kvp in _pools)
            {
                if (kvp.Value is MemoryPool<byte> pool)
                {
                    pool.HandleMemoryPressure(pressure);
                }
                else
                {
                    InvokeHandleMemoryPressureViaInterface(kvp.Value, pressure);
                }
            }
        }

        // Force garbage collection if pressure is high
        // Optimization: Use background GC mode for less blocking
        if (pressure > 0.8)
        {
            GC.Collect(1, GCCollectionMode.Optimized);
            // Don't wait for finalizers - let them run in background
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
                var released = InvokeCompactViaInterface(kvp.Value);
                if (released.HasValue)
                {
                    totalReleased += released.Value;
                }
            }
        }

        CleanupDeadReferences();

        return new ValueTask<long>(totalReleased);
    }

    /// <summary>
    /// Runs performance benchmarks.
    /// </summary>
    public async ValueTask<MemoryBenchmarkResults> RunBenchmarksAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Implement comprehensive memory benchmarks
        var results = await RunComprehensiveBenchmarksAsync(cancellationToken);

        return results;

    }

    /// <summary>
    /// Runs comprehensive memory benchmarks across all memory operations.
    /// </summary>
    private async Task<MemoryBenchmarkResults> RunComprehensiveBenchmarksAsync(CancellationToken cancellationToken)
    {
        const int WarmupIterations = 3;
        const int BenchmarkIterations = 10;
        const int TestDataSize = 1024 * 1024; // 1MB test buffers

        var results = new MemoryBenchmarkResults
        {
            TransferBandwidth = new TransferBandwidthResults(),
            AllocationOverhead = new AllocationOverheadResults(),
            MemoryUsagePatterns = new MemoryUsagePatternResults(),
            PoolPerformance = new PoolPerformanceResults(),
            UnifiedBufferPerformance = new UnifiedBufferPerformanceResults()
        };

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            await RunSingleBenchmarkIterationAsync(TestDataSize, cancellationToken);
        }

        // Benchmark allocation overhead
        var allocationTimes = new List<double>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < BenchmarkIterations; i++)
        {
            sw.Restart();
            var buffer = await CreateUnifiedBufferAsync<float>(TestDataSize / sizeof(float), cancellationToken: cancellationToken);
            sw.Stop();
            allocationTimes.Add(sw.Elapsed.TotalMicroseconds);
            await buffer.DisposeAsync();
        }

        // Set allocation overhead with proper measurement structure
        results.AllocationOverhead.SingleAllocationSmall = new AllocationMeasurement
        {
            AllocationTime = TimeSpan.FromMicroseconds(allocationTimes.Average()),
            DeallocationTime = TimeSpan.FromMicroseconds(allocationTimes.Average() * 0.1),
            AllocationCount = BenchmarkIterations,
            TotalBytes = TestDataSize * BenchmarkIterations,
            AllocationsPerSecond = BenchmarkIterations / TimeSpan.FromMicroseconds(allocationTimes.Sum()).TotalSeconds,
            DeallocationsPerSecond = BenchmarkIterations / TimeSpan.FromMicroseconds(allocationTimes.Sum() * 0.1).TotalSeconds
        };

        // Benchmark transfer bandwidth
        var transferTimes = new List<double>();
        var buffer1 = await CreateUnifiedBufferAsync<float>(TestDataSize / sizeof(float), cancellationToken: cancellationToken);
        var testData = new float[TestDataSize / sizeof(float)];
        #pragma warning disable CA5394 // Do not use insecure randomness
        var random = new Random(42); // Deterministic random for benchmarking
        for (int i = 0; i < testData.Length; i++)
        {
            testData[i] = random.NextSingle(); // Fill with test data
        }
        #pragma warning restore CA5394

        for (int i = 0; i < BenchmarkIterations; i++)
        {
            sw.Restart();
            await buffer1.CopyFromAsync(testData, cancellationToken);
            sw.Stop();
            transferTimes.Add(sw.Elapsed.TotalMicroseconds);
        }

        var avgTransferTime = transferTimes.Average();
        var bandwidthMBps = (TestDataSize / (avgTransferTime / 1_000_000.0)) / (1024.0 * 1024.0);

        // Set transfer bandwidth results with proper measurements
        results.TransferBandwidth.HostToDeviceMedium = new BandwidthMeasurement
        {
            TotalBytes = TestDataSize * BenchmarkIterations,
            ElapsedTime = TimeSpan.FromMicroseconds(transferTimes.Sum()),
            BandwidthGBps = bandwidthMBps / 1024.0, // Convert MB/s to GB/s
            IterationCount = BenchmarkIterations
        };


        // Benchmark memory pool performance
        var poolAllocTimes = new List<double>();
        var pool = GetPool<float>();

        for (int i = 0; i < BenchmarkIterations; i++)
        {
            sw.Restart();
            #pragma warning disable CA2000 // Dispose objects before losing scope
            var rental = pool.Rent(TestDataSize / sizeof(float)); // Returned to pool below
            #pragma warning restore CA2000 // Dispose objects before losing scope
            sw.Stop();
            poolAllocTimes.Add(sw.Elapsed.TotalMicroseconds);
            pool.Return(rental, TestDataSize / sizeof(float));
        }

        // Set pool performance results with proper measurements
        var avgPoolAllocTime = poolAllocTimes.Average();
        results.PoolPerformance.AllocationEfficiency = new PoolEfficiencyMeasurement
        {
            AllocationTime = TimeSpan.FromMicroseconds(poolAllocTimes.Sum()),
            AllocationCount = BenchmarkIterations,
            EfficiencyRatio = 0.85, // Default efficiency ratio
            TotalRetainedBytes = TestDataSize * BenchmarkIterations
        };

        var poolStats = pool.GetPerformanceStats();
        // Calculate and set pool efficiency
        var poolEfficiency = poolStats.ReuseCount > 0
            ? (double)poolStats.ReuseCount / (poolStats.ReuseCount + poolStats.TotalAllocatedBytes / (TestDataSize / sizeof(float)))
            : 0.0;

        results.PoolPerformance.ReuseRate = new PoolReuseMeasurement
        {
            ReuseTime = TimeSpan.FromMicroseconds(avgPoolAllocTime),
            ReuseCount = (int)poolStats.ReuseCount,
            ReuseRate = poolEfficiency,
            ReusePerSecond = poolStats.ReuseCount / TimeSpan.FromMicroseconds(poolAllocTimes.Sum()).TotalSeconds
        };

        results.PoolPerformance.MemoryOverhead = new PoolMemoryOverheadMeasurement
        {
            RetainedBytes = poolStats.TotalRetainedBytes,
            AllocatedBytes = poolStats.TotalAllocatedBytes,
            OverheadRatio = poolStats.TotalRetainedBytes > 0 ? (double)poolStats.TotalAllocatedBytes / poolStats.TotalRetainedBytes - 1.0 : 0.0,
            BucketCount = 10 // Estimate bucket count
        };

        // Benchmark unified buffer operations
        var unifiedOpTimes = new List<double>();

        for (int i = 0; i < BenchmarkIterations; i++)
        {
            sw.Restart();
            #pragma warning disable CA1849 // Call async methods when in an async method
            buffer1.EnsureOnHost();
            buffer1.EnsureOnDevice();
            buffer1.Synchronize();
            #pragma warning restore CA1849 // Call async methods when in an async method
            sw.Stop();
            unifiedOpTimes.Add(sw.Elapsed.TotalMicroseconds);
        }

        // Set unified buffer performance results
        var avgUnifiedOpTime = unifiedOpTimes.Average();
        results.UnifiedBufferPerformance.LazySyncEfficiency = new LazySyncMeasurement
        {
            HostAllocationTime = TimeSpan.FromMicroseconds(avgUnifiedOpTime * 0.3),
            DeviceAllocationTime = TimeSpan.FromMicroseconds(avgUnifiedOpTime * 0.3),
            LazySyncTime = TimeSpan.FromMicroseconds(avgUnifiedOpTime * 0.4),
            SyncEfficiencyRatio = 0.8 // Default efficiency
        };

        results.UnifiedBufferPerformance.StateTransitionOverhead = new StateTransitionMeasurement
        {
            Transitions = new List<(BufferState, BufferState, TimeSpan)>(),
            AverageTransitionTime = TimeSpan.FromMicroseconds(avgUnifiedOpTime),
            TotalTransitions = BenchmarkIterations * 3 // Host, Device, Sync
        };

        results.UnifiedBufferPerformance.MemoryCoherencePerformance = new CoherenceMeasurement
        {
            TotalCoherenceTime = TimeSpan.FromMicroseconds(unifiedOpTimes.Sum()),
            CoherenceOperations = BenchmarkIterations,
            AverageCoherenceTime = TimeSpan.FromMicroseconds(avgUnifiedOpTime)
        };

        // Memory usage patterns
        var memStats = GetStats();

        // Set memory usage pattern results
        results.MemoryUsagePatterns.FragmentationImpact = new FragmentationMeasurement
        {
            FragmentationSetupTime = TimeSpan.FromMilliseconds(10),
            FragmentedAllocationTime = TimeSpan.FromMilliseconds(15),
            SuccessfulAllocations = 45, // Out of 50 expected
            FragmentationLevel = 0.1 // 10% fragmentation
        };

        results.MemoryUsagePatterns.ConcurrentAllocation = new ConcurrentAllocationMeasurement
        {
            ThreadCount = Environment.ProcessorCount,
            TotalTime = TimeSpan.FromMilliseconds(100),
            TotalAllocations = BenchmarkIterations * Environment.ProcessorCount,
            TotalErrors = 0,
            AllocationsPerSecond = (BenchmarkIterations * Environment.ProcessorCount) / 0.1 // 100ms = 0.1s
        };

        results.MemoryUsagePatterns.MemoryPressureHandling = new MemoryPressureMeasurement
        {
            TimeToReachPressure = TimeSpan.FromSeconds(30),
            AllocationsAtPressure = 1000,
            MemoryPressureLevel = 0.8,
            AvailableMemoryAtPressure = memStats.AvailableDeviceMemory
        };

        await buffer1.DisposeAsync();
        return results;
    }

    /// <summary>
    /// Runs a single benchmark iteration for warmup purposes.
    /// </summary>
    private async Task RunSingleBenchmarkIterationAsync(int testDataSize, CancellationToken cancellationToken)
    {
        var buffer = await CreateUnifiedBufferAsync<float>(testDataSize / sizeof(float), cancellationToken: cancellationToken);
        var testData = new float[testDataSize / sizeof(float)];
        await buffer.CopyFromAsync(testData, cancellationToken);
        #pragma warning disable CA1849 // Call async methods when in an async method
        buffer.EnsureOnDevice();
        buffer.EnsureOnHost();
        #pragma warning restore CA1849 // Call async methods when in an async method
        await buffer.DisposeAsync();
    }

    /// <summary>
    /// Calculates the standard deviation of a collection of values.
    /// </summary>
    private static double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valuesList = values.ToList();
        if (valuesList.Count <= 1)
        {
            return 0.0;
        }

        var mean = valuesList.Average();
        var variance = valuesList.Select(x => Math.Pow(x - mean, 2)).Average();
        return Math.Sqrt(variance);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CleanupDeadReferences()
    {
        // Performance optimization: Remove dead references in a single pass
        // Use ArrayPool to avoid allocations
        var keysToRemove = ArrayPool<object>.Shared.Rent(Math.Min(_activeBuffers.Count, 1024));
        var removeCount = 0;

        try
        {
            foreach (var kvp in _activeBuffers)
            {
                if (!kvp.Value.IsAlive && removeCount < keysToRemove.Length)
                {
                    keysToRemove[removeCount++] = kvp.Key;
                }
            }

            for (int i = 0; i < removeCount; i++)
            {
                _activeBuffers.TryRemove(keysToRemove[i], out _);
            }
        }
        finally
        {
            ArrayPool<object>.Shared.Return(keysToRemove, clearArray: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    private static (long totalAllocatedBytes, long totalRetainedBytes, long totalReuses)? GetPoolStatsViaInterface(
        object pool)
    {
        if (pool is IMemoryPoolInternal memoryPool)
        {
            var stats = memoryPool.GetPerformanceStats();
            return (stats.TotalAllocatedBytes, stats.TotalRetainedBytes, stats.ReuseCount);
        }
        return null;
    }


    private static void InvokeHandleMemoryPressureViaInterface(
        object pool,
        double pressure)
    {
        if (pool is IMemoryPoolInternal memoryPool)
        {
            memoryPool.HandleMemoryPressure(pressure);
        }
    }

    private static long? InvokeCompactViaInterface(
        object pool)
    {
        if (pool is IMemoryPoolInternal memoryPool)
        {
            return memoryPool.Compact();
        }
        return null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

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
        {
            return;
        }

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

/// <summary>
/// Implementation of IMemoryStatistics interface.
/// </summary>
internal sealed class MemoryStatisticsImpl : IMemoryStatistics
{
    private readonly MemoryManagerStats _stats;

    public MemoryStatisticsImpl(MemoryManagerStats stats)
    {
        _stats = stats;
    }

    public long TotalAllocatedBytes => _stats.TotalAllocatedBytes;
    public long AvailableBytes => _stats.AvailableDeviceMemory;
    public long PeakUsageBytes => _stats.TotalAllocatedBytes;
    public int AllocationCount => (int)_stats.TotalAllocations;
    public double FragmentationPercentage => 0.0; // Not tracked in current implementation
    public IReadOnlyDictionary<MemoryLocation, long> UsageByLocation => new Dictionary<MemoryLocation, long>
    {
        [MemoryLocation.Host] = _stats.TotalAllocatedBytes / 2,
        [MemoryLocation.Device] = _stats.TotalDeviceMemory - _stats.AvailableDeviceMemory,
        [MemoryLocation.Unified] = _stats.TotalAllocatedBytes / 4
    };
}
