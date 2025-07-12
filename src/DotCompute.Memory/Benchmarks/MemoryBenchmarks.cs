using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Memory.Benchmarks;

/// <summary>
/// Comprehensive benchmarks for memory system performance analysis.
/// Measures transfer bandwidth, allocation overhead, and memory usage patterns.
/// </summary>
public static class MemoryBenchmarks
{
    private const int WarmupIterations = 10;
    private const int BenchmarkIterations = 100;
    private const int SmallBufferSize = 1024;
    private const int MediumBufferSize = 64 * 1024;
    private const int LargeBufferSize = 16 * 1024 * 1024;
    
    /// <summary>
    /// Runs a comprehensive memory benchmark suite.
    /// </summary>
    /// <param name="memoryManager">The memory manager to benchmark.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete benchmark results.</returns>
    public static async Task<MemoryBenchmarkResults> RunComprehensiveBenchmarkAsync(
        IMemoryManager memoryManager,
        CancellationToken cancellationToken = default)
    {
        var results = new MemoryBenchmarkResults();
        
        // Transfer bandwidth benchmarks
        results.TransferBandwidth = await BenchmarkTransferBandwidthAsync(memoryManager, cancellationToken);
        
        // Allocation overhead benchmarks
        results.AllocationOverhead = await BenchmarkAllocationOverheadAsync(memoryManager, cancellationToken);
        
        // Memory usage pattern benchmarks
        results.MemoryUsagePatterns = await BenchmarkMemoryUsagePatternsAsync(memoryManager, cancellationToken);
        
        // Pool performance benchmarks
        results.PoolPerformance = await BenchmarkPoolPerformanceAsync(cancellationToken);
        
        // Unified buffer benchmarks
        results.UnifiedBufferPerformance = await BenchmarkUnifiedBufferAsync(memoryManager, cancellationToken);
        
        return results;
    }
    
    /// <summary>
    /// Benchmarks memory transfer bandwidth in various scenarios.
    /// </summary>
    public static async Task<TransferBandwidthResults> BenchmarkTransferBandwidthAsync(
        IMemoryManager memoryManager,
        CancellationToken cancellationToken = default)
    {
        var results = new TransferBandwidthResults();
        
        // Host to Device transfer
        results.HostToDeviceSmall = await MeasureHostToDeviceTransferAsync<float>(
            memoryManager, SmallBufferSize, cancellationToken);
        
        results.HostToDeviceMedium = await MeasureHostToDeviceTransferAsync<float>(
            memoryManager, MediumBufferSize, cancellationToken);
        
        results.HostToDeviceLarge = await MeasureHostToDeviceTransferAsync<float>(
            memoryManager, LargeBufferSize, cancellationToken);
        
        // Device to Host transfer
        results.DeviceToHostSmall = await MeasureDeviceToHostTransferAsync<float>(
            memoryManager, SmallBufferSize, cancellationToken);
        
        results.DeviceToHostMedium = await MeasureDeviceToHostTransferAsync<float>(
            memoryManager, MediumBufferSize, cancellationToken);
        
        results.DeviceToHostLarge = await MeasureDeviceToHostTransferAsync<float>(
            memoryManager, LargeBufferSize, cancellationToken);
        
        // Device to Device transfer
        results.DeviceToDeviceSmall = await MeasureDeviceToDeviceTransferAsync(
            memoryManager, SmallBufferSize * sizeof(float), cancellationToken);
        
        results.DeviceToDeviceMedium = await MeasureDeviceToDeviceTransferAsync(
            memoryManager, MediumBufferSize * sizeof(float), cancellationToken);
        
        results.DeviceToDeviceLarge = await MeasureDeviceToDeviceTransferAsync(
            memoryManager, LargeBufferSize * sizeof(float), cancellationToken);
        
        return results;
    }
    
    /// <summary>
    /// Benchmarks memory allocation overhead.
    /// </summary>
    public static async Task<AllocationOverheadResults> BenchmarkAllocationOverheadAsync(
        IMemoryManager memoryManager,
        CancellationToken cancellationToken = default)
    {
        var results = new AllocationOverheadResults();
        
        // Single allocation benchmarks
        results.SingleAllocationSmall = await MeasureAllocationOverheadAsync(
            memoryManager, SmallBufferSize * sizeof(float), 1, cancellationToken);
        
        results.SingleAllocationMedium = await MeasureAllocationOverheadAsync(
            memoryManager, MediumBufferSize * sizeof(float), 1, cancellationToken);
        
        results.SingleAllocationLarge = await MeasureAllocationOverheadAsync(
            memoryManager, LargeBufferSize * sizeof(float), 1, cancellationToken);
        
        // Bulk allocation benchmarks
        results.BulkAllocationSmall = await MeasureAllocationOverheadAsync(
            memoryManager, SmallBufferSize * sizeof(float), BenchmarkIterations, cancellationToken);
        
        results.BulkAllocationMedium = await MeasureAllocationOverheadAsync(
            memoryManager, MediumBufferSize * sizeof(float), BenchmarkIterations, cancellationToken);
        
        results.BulkAllocationLarge = await MeasureAllocationOverheadAsync(
            memoryManager, LargeBufferSize * sizeof(float), BenchmarkIterations, cancellationToken);
        
        return results;
    }
    
    /// <summary>
    /// Benchmarks memory usage patterns.
    /// </summary>
    public static async Task<MemoryUsagePatternResults> BenchmarkMemoryUsagePatternsAsync(
        IMemoryManager memoryManager,
        CancellationToken cancellationToken = default)
    {
        var results = new MemoryUsagePatternResults();
        
        // Fragmentation test
        results.FragmentationImpact = await MeasureFragmentationImpactAsync(memoryManager, cancellationToken);
        
        // Concurrent allocation test
        results.ConcurrentAllocation = await MeasureConcurrentAllocationAsync(memoryManager, cancellationToken);
        
        // Memory pressure test
        results.MemoryPressureHandling = await MeasureMemoryPressureHandlingAsync(memoryManager, cancellationToken);
        
        return results;
    }
    
    /// <summary>
    /// Benchmarks memory pool performance.
    /// </summary>
    public static async Task<PoolPerformanceResults> BenchmarkPoolPerformanceAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new PoolPerformanceResults();
        
        using var pool = new MemoryPool<float>();
        
        // Pool allocation efficiency
        results.AllocationEfficiency = await MeasurePoolAllocationEfficiencyAsync(pool, cancellationToken);
        
        // Pool reuse rate
        results.ReuseRate = await MeasurePoolReuseRateAsync(pool, cancellationToken);
        
        // Pool memory overhead
        results.MemoryOverhead = await MeasurePoolMemoryOverheadAsync(pool, cancellationToken);
        
        return results;
    }
    
    /// <summary>
    /// Benchmarks unified buffer performance.
    /// </summary>
    public static async Task<UnifiedBufferPerformanceResults> BenchmarkUnifiedBufferAsync(
        IMemoryManager memoryManager,
        CancellationToken cancellationToken = default)
    {
        var results = new UnifiedBufferPerformanceResults();
        
        using var pool = new MemoryPool<float>(memoryManager);
        
        // Lazy synchronization efficiency
        results.LazySyncEfficiency = await MeasureLazySyncEfficiencyAsync(memoryManager, pool, cancellationToken);
        
        // State transition overhead
        results.StateTransitionOverhead = await MeasureStateTransitionOverheadAsync(memoryManager, pool, cancellationToken);
        
        // Memory coherence performance
        results.MemoryCoherencePerformance = await MeasureMemoryCoherenceAsync(memoryManager, pool, cancellationToken);
        
        return results;
    }
    
    private static async Task<BandwidthMeasurement> MeasureHostToDeviceTransferAsync<T>(
        IMemoryManager memoryManager,
        int elementCount,
        CancellationToken cancellationToken) where T : unmanaged
    {
        var hostData = new T[elementCount];
        var deviceMemory = memoryManager.Allocate(elementCount * Unsafe.SizeOf<T>());
        
        try
        {
            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                memoryManager.CopyToDevice(hostData.AsSpan(), deviceMemory);
            }
            
            // Benchmark
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                memoryManager.CopyToDevice(hostData.AsSpan(), deviceMemory);
            }
            stopwatch.Stop();
            
            var totalBytes = (long)elementCount * Unsafe.SizeOf<T>() * BenchmarkIterations;
            var bandwidthGBps = totalBytes / (1024.0 * 1024.0 * 1024.0) / stopwatch.Elapsed.TotalSeconds;
            
            return new BandwidthMeasurement
            {
                TotalBytes = totalBytes,
                ElapsedTime = stopwatch.Elapsed,
                BandwidthGBps = bandwidthGBps,
                IterationCount = BenchmarkIterations
            };
        }
        finally
        {
            memoryManager.Free(deviceMemory);
        }
    }
    
    private static async Task<BandwidthMeasurement> MeasureDeviceToHostTransferAsync<T>(
        IMemoryManager memoryManager,
        int elementCount,
        CancellationToken cancellationToken) where T : unmanaged
    {
        var hostData = new T[elementCount];
        var deviceMemory = memoryManager.Allocate(elementCount * Unsafe.SizeOf<T>());
        
        try
        {
            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                memoryManager.CopyToHost(deviceMemory, hostData.AsSpan());
            }
            
            // Benchmark
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                memoryManager.CopyToHost(deviceMemory, hostData.AsSpan());
            }
            stopwatch.Stop();
            
            var totalBytes = (long)elementCount * Unsafe.SizeOf<T>() * BenchmarkIterations;
            var bandwidthGBps = totalBytes / (1024.0 * 1024.0 * 1024.0) / stopwatch.Elapsed.TotalSeconds;
            
            return new BandwidthMeasurement
            {
                TotalBytes = totalBytes,
                ElapsedTime = stopwatch.Elapsed,
                BandwidthGBps = bandwidthGBps,
                IterationCount = BenchmarkIterations
            };
        }
        finally
        {
            memoryManager.Free(deviceMemory);
        }
    }
    
    private static async Task<BandwidthMeasurement> MeasureDeviceToDeviceTransferAsync(
        IMemoryManager memoryManager,
        long sizeInBytes,
        CancellationToken cancellationToken)
    {
        var sourceMemory = memoryManager.Allocate(sizeInBytes);
        var destMemory = memoryManager.Allocate(sizeInBytes);
        
        try
        {
            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                memoryManager.CopyDeviceToDevice(sourceMemory, destMemory, sizeInBytes);
            }
            
            // Benchmark
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                memoryManager.CopyDeviceToDevice(sourceMemory, destMemory, sizeInBytes);
            }
            stopwatch.Stop();
            
            var totalBytes = sizeInBytes * BenchmarkIterations;
            var bandwidthGBps = totalBytes / (1024.0 * 1024.0 * 1024.0) / stopwatch.Elapsed.TotalSeconds;
            
            return new BandwidthMeasurement
            {
                TotalBytes = totalBytes,
                ElapsedTime = stopwatch.Elapsed,
                BandwidthGBps = bandwidthGBps,
                IterationCount = BenchmarkIterations
            };
        }
        finally
        {
            memoryManager.Free(sourceMemory);
            memoryManager.Free(destMemory);
        }
    }
    
    private static async Task<AllocationMeasurement> MeasureAllocationOverheadAsync(
        IMemoryManager memoryManager,
        long sizeInBytes,
        int allocationCount,
        CancellationToken cancellationToken)
    {
        var allocations = new DeviceMemory[allocationCount];
        
        try
        {
            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                var warmupMemory = memoryManager.Allocate(sizeInBytes);
                memoryManager.Free(warmupMemory);
            }
            
            // Benchmark allocation
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < allocationCount; i++)
            {
                allocations[i] = memoryManager.Allocate(sizeInBytes);
            }
            var allocationTime = stopwatch.Elapsed;
            
            // Benchmark deallocation
            stopwatch.Restart();
            for (int i = 0; i < allocationCount; i++)
            {
                memoryManager.Free(allocations[i]);
            }
            var deallocationTime = stopwatch.Elapsed;
            
            return new AllocationMeasurement
            {
                AllocationTime = allocationTime,
                DeallocationTime = deallocationTime,
                AllocationCount = allocationCount,
                TotalBytes = sizeInBytes * allocationCount,
                AllocationsPerSecond = allocationCount / allocationTime.TotalSeconds,
                DeallocationsPerSecond = allocationCount / deallocationTime.TotalSeconds
            };
        }
        catch
        {
            // Cleanup on error
            foreach (var allocation in allocations)
            {
                if (allocation.IsValid)
                    memoryManager.Free(allocation);
            }
            throw;
        }
    }
    
    private static async Task<FragmentationMeasurement> MeasureFragmentationImpactAsync(
        IMemoryManager memoryManager,
        CancellationToken cancellationToken)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var allocations = new List<DeviceMemory>();
        
        try
        {
            // Create fragmented memory pattern
            for (int i = 0; i < 100; i++)
            {
                var size = random.Next(1024, 64 * 1024);
                allocations.Add(memoryManager.Allocate(size));
            }
            
            // Free every other allocation to create fragmentation
            for (int i = 1; i < allocations.Count; i += 2)
            {
                memoryManager.Free(allocations[i]);
                allocations[i] = DeviceMemory.Invalid;
            }
            
            // Measure allocation performance in fragmented state
            var stopwatch = Stopwatch.StartNew();
            var fragmentedAllocations = new List<DeviceMemory>();
            
            for (int i = 0; i < 50; i++)
            {
                var size = random.Next(1024, 32 * 1024);
                try
                {
                    fragmentedAllocations.Add(memoryManager.Allocate(size));
                }
                catch (OutOfMemoryException)
                {
                    // Expected in fragmented scenario
                    break;
                }
            }
            
            stopwatch.Stop();
            
            return new FragmentationMeasurement
            {
                FragmentationSetupTime = TimeSpan.FromMilliseconds(100), // Placeholder
                FragmentedAllocationTime = stopwatch.Elapsed,
                SuccessfulAllocations = fragmentedAllocations.Count,
                FragmentationLevel = 0.5 // 50% fragmentation
            };
        }
        finally
        {
            // Cleanup
            foreach (var allocation in allocations)
            {
                if (allocation.IsValid)
                    memoryManager.Free(allocation);
            }
        }
    }
    
    private static async Task<ConcurrentAllocationMeasurement> MeasureConcurrentAllocationAsync(
        IMemoryManager memoryManager,
        CancellationToken cancellationToken)
    {
        const int threadCount = 4;
        const int allocationsPerThread = 25;
        
        var barrier = new Barrier(threadCount);
        var tasks = new Task[threadCount];
        var results = new ConcurrentTaskResult[threadCount];
        
        for (int t = 0; t < threadCount; t++)
        {
            var threadIndex = t;
            tasks[t] = Task.Run(() =>
            {
                var allocations = new DeviceMemory[allocationsPerThread];
                
                // Wait for all threads to be ready
                barrier.SignalAndWait();
                
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    for (int i = 0; i < allocationsPerThread; i++)
                    {
                        allocations[i] = memoryManager.Allocate(4096);
                    }
                    
                    stopwatch.Stop();
                    
                    results[threadIndex] = new ConcurrentTaskResult
                    {
                        ElapsedTime = stopwatch.Elapsed,
                        SuccessfulAllocations = allocationsPerThread,
                        Errors = 0
                    };
                }
                catch (Exception)
                {
                    results[threadIndex] = new ConcurrentTaskResult
                    {
                        ElapsedTime = stopwatch.Elapsed,
                        SuccessfulAllocations = 0,
                        Errors = 1
                    };
                }
                finally
                {
                    // Cleanup
                    foreach (var allocation in allocations)
                    {
                        if (allocation.IsValid)
                            memoryManager.Free(allocation);
                    }
                }
            });
        }
        
        await Task.WhenAll(tasks);
        
        var totalTime = results.Max(r => r.ElapsedTime);
        var totalAllocations = results.Sum(r => r.SuccessfulAllocations);
        var totalErrors = results.Sum(r => r.Errors);
        
        return new ConcurrentAllocationMeasurement
        {
            ThreadCount = threadCount,
            TotalTime = totalTime,
            TotalAllocations = totalAllocations,
            TotalErrors = totalErrors,
            AllocationsPerSecond = totalAllocations / totalTime.TotalSeconds
        };
    }
    
    private static async Task<MemoryPressureMeasurement> MeasureMemoryPressureHandlingAsync(
        IMemoryManager memoryManager,
        CancellationToken cancellationToken)
    {
        var totalMemory = memoryManager.GetTotalMemory();
        var targetPressure = (long)(totalMemory * 0.8); // 80% pressure
        
        var allocations = new List<DeviceMemory>();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Allocate until we hit memory pressure
            while (memoryManager.GetAvailableMemory() > totalMemory * 0.2)
            {
                allocations.Add(memoryManager.Allocate(1024 * 1024));
            }
            
            stopwatch.Stop();
            
            return new MemoryPressureMeasurement
            {
                TimeToReachPressure = stopwatch.Elapsed,
                AllocationsAtPressure = allocations.Count,
                MemoryPressureLevel = 0.8,
                AvailableMemoryAtPressure = memoryManager.GetAvailableMemory()
            };
        }
        finally
        {
            // Cleanup
            foreach (var allocation in allocations)
            {
                if (allocation.IsValid)
                    memoryManager.Free(allocation);
            }
        }
    }
    
    private static async Task<PoolEfficiencyMeasurement> MeasurePoolAllocationEfficiencyAsync(
        MemoryPool<float> pool,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var owners = new IMemoryOwner<float>[BenchmarkIterations];
        
        try
        {
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                owners[i] = pool.Rent(1024);
            }
            
            stopwatch.Stop();
            
            var stats = pool.GetPerformanceStats();
            
            return new PoolEfficiencyMeasurement
            {
                AllocationTime = stopwatch.Elapsed,
                AllocationCount = BenchmarkIterations,
                EfficiencyRatio = stats.EfficiencyRatio,
                TotalRetainedBytes = stats.TotalRetainedBytes
            };
        }
        finally
        {
            foreach (var owner in owners)
            {
                owner?.Dispose();
            }
        }
    }
    
    private static async Task<PoolReuseMeasurement> MeasurePoolReuseRateAsync(
        MemoryPool<float> pool,
        CancellationToken cancellationToken)
    {
        // First round: allocate and return
        var owners = new IMemoryOwner<float>[50];
        for (int i = 0; i < 50; i++)
        {
            owners[i] = pool.Rent(1024);
        }
        
        foreach (var owner in owners)
        {
            owner.Dispose();
        }
        
        // Second round: should reuse from pool
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 50; i++)
        {
            owners[i] = pool.Rent(1024);
        }
        stopwatch.Stop();
        
        var stats = pool.GetPerformanceStats();
        
        foreach (var owner in owners)
        {
            owner.Dispose();
        }
        
        return new PoolReuseMeasurement
        {
            ReuseTime = stopwatch.Elapsed,
            ReuseCount = 50,
            ReuseRate = stats.EfficiencyRatio,
            ReusePerSecond = 50 / stopwatch.Elapsed.TotalSeconds
        };
    }
    
    private static async Task<PoolMemoryOverheadMeasurement> MeasurePoolMemoryOverheadAsync(
        MemoryPool<float> pool,
        CancellationToken cancellationToken)
    {
        var initialStats = pool.GetPerformanceStats();
        
        // Allocate various sizes to fill buckets
        var owners = new List<IMemoryOwner<float>>();
        var sizes = new[] { 64, 128, 256, 512, 1024, 2048, 4096 };
        
        foreach (var size in sizes)
        {
            for (int i = 0; i < 10; i++)
            {
                owners.Add(pool.Rent(size));
            }
        }
        
        // Return half to pool
        for (int i = 0; i < owners.Count / 2; i++)
        {
            owners[i].Dispose();
        }
        
        var finalStats = pool.GetPerformanceStats();
        
        // Cleanup remaining
        for (int i = owners.Count / 2; i < owners.Count; i++)
        {
            owners[i].Dispose();
        }
        
        return new PoolMemoryOverheadMeasurement
        {
            RetainedBytes = finalStats.TotalRetainedBytes,
            AllocatedBytes = finalStats.TotalAllocatedBytes,
            OverheadRatio = (double)finalStats.TotalRetainedBytes / finalStats.TotalAllocatedBytes,
            BucketCount = finalStats.BucketStats.Length
        };
    }
    
    private static async Task<LazySyncMeasurement> MeasureLazySyncEfficiencyAsync(
        IMemoryManager memoryManager,
        MemoryPool<float> pool,
        CancellationToken cancellationToken)
    {
        using var buffer = new UnifiedBuffer<float>(memoryManager, pool, 1024);
        
        // Measure host allocation
        var stopwatch = Stopwatch.StartNew();
        await buffer.AllocateHostMemoryAsync();
        var hostAllocationTime = stopwatch.Elapsed;
        
        // Measure device allocation
        stopwatch.Restart();
        await buffer.AllocateDeviceMemoryAsync();
        var deviceAllocationTime = stopwatch.Elapsed;
        
        // Measure lazy sync
        buffer.MarkHostDirty();
        stopwatch.Restart();
        await buffer.GetDeviceMemoryAsync();
        var lazySyncTime = stopwatch.Elapsed;
        
        return new LazySyncMeasurement
        {
            HostAllocationTime = hostAllocationTime,
            DeviceAllocationTime = deviceAllocationTime,
            LazySyncTime = lazySyncTime,
            SyncEfficiencyRatio = lazySyncTime.TotalMilliseconds / (hostAllocationTime.TotalMilliseconds + deviceAllocationTime.TotalMilliseconds)
        };
    }
    
    private static async Task<StateTransitionMeasurement> MeasureStateTransitionOverheadAsync(
        IMemoryManager memoryManager,
        MemoryPool<float> pool,
        CancellationToken cancellationToken)
    {
        using var buffer = new UnifiedBuffer<float>(memoryManager, pool, 1024);
        
        var transitions = new List<(MemoryState From, MemoryState To, TimeSpan Duration)>();
        
        // Uninitialized -> HostOnly
        var stopwatch = Stopwatch.StartNew();
        await buffer.AllocateHostMemoryAsync();
        transitions.Add((MemoryState.Uninitialized, MemoryState.HostOnly, stopwatch.Elapsed));
        
        // HostOnly -> HostAndDevice
        stopwatch.Restart();
        await buffer.AllocateDeviceMemoryAsync();
        transitions.Add((MemoryState.HostOnly, MemoryState.HostAndDevice, stopwatch.Elapsed));
        
        var averageTransitionTime = transitions.Average(t => t.Duration.TotalMilliseconds);
        
        return new StateTransitionMeasurement
        {
            Transitions = transitions.ToArray(),
            AverageTransitionTime = TimeSpan.FromMilliseconds(averageTransitionTime),
            TotalTransitions = transitions.Count
        };
    }
    
    private static async Task<CoherenceMeasurement> MeasureMemoryCoherenceAsync(
        IMemoryManager memoryManager,
        MemoryPool<float> pool,
        CancellationToken cancellationToken)
    {
        using var buffer = new UnifiedBuffer<float>(memoryManager, pool, 1024);
        
        await buffer.AllocateHostMemoryAsync();
        await buffer.AllocateDeviceMemoryAsync();
        
        // Measure coherence operations
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
        {
            buffer.MarkHostDirty();
            await buffer.GetDeviceMemoryAsync();
            buffer.MarkDeviceDirty();
            await buffer.GetHostSpanAsync();
        }
        stopwatch.Stop();
        
        return new CoherenceMeasurement
        {
            TotalCoherenceTime = stopwatch.Elapsed,
            CoherenceOperations = 20,
            AverageCoherenceTime = TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds / 20.0)
        };
    }
    
    private struct ConcurrentTaskResult
    {
        public TimeSpan ElapsedTime;
        public int SuccessfulAllocations;
        public int Errors;
    }
}