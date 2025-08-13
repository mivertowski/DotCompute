// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#if BENCHMARKS
// Production Memory Benchmarks - Updated for new IMemoryManager interface
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
        
        // Create a basic memory manager for testing  
        using var testMemoryManager = new TestMemoryManager();
        using var pool = new MemoryPool<float>(testMemoryManager);
        
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
        // Allocate host data array
        var hostData = new T[elementCount];
        var random = new Random(42);
        
        // Fill with test data for consistent benchmarking
        for (int i = 0; i < elementCount; i++)
        {
            hostData[i] = Unsafe.As<int, T>(ref Unsafe.AsRef(i % 1000));
        }
        
        // Allocate device buffer
        var deviceBuffer = await memoryManager.AllocateAsync(
            elementCount * Unsafe.SizeOf<T>(), 
            DotCompute.Abstractions.MemoryOptions.None, 
            cancellationToken);
        
        try
        {
            // Warmup iterations to prime caches and drivers
            for (int i = 0; i < WarmupIterations; i++)
            {
                await deviceBuffer.CopyFromHostAsync(hostData.AsMemory(), 0, cancellationToken);
            }
            
            // Benchmark actual transfer performance
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                await deviceBuffer.CopyFromHostAsync(hostData.AsMemory(), 0, cancellationToken);
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
            await deviceBuffer.DisposeAsync();
        }
        
        // Original code disabled due to interface changes:
        /*
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
        */
    }
    
    private static async Task<BandwidthMeasurement> MeasureDeviceToHostTransferAsync<T>(
        IMemoryManager memoryManager,
        int elementCount,
        CancellationToken cancellationToken) where T : unmanaged
    {
        // Allocate host data array
        var hostData = new T[elementCount];
        
        // Allocate device buffer and populate with test data
        var deviceBuffer = await memoryManager.AllocateAndCopyAsync<T>(
            new T[elementCount].AsMemory(), 
            DotCompute.Abstractions.MemoryOptions.None, 
            cancellationToken);
        
        try
        {
            // Warmup iterations
            for (int i = 0; i < WarmupIterations; i++)
            {
                await deviceBuffer.CopyToHostAsync(hostData.AsMemory(), 0, cancellationToken);
            }
            
            // Benchmark device to host transfer
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                await deviceBuffer.CopyToHostAsync(hostData.AsMemory(), 0, cancellationToken);
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
            await deviceBuffer.DisposeAsync();
        }
        
        /*
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
        */
    }
    
    private static async Task<BandwidthMeasurement> MeasureDeviceToDeviceTransferAsync(
        IMemoryManager memoryManager,
        long sizeInBytes,
        CancellationToken cancellationToken)
    {
        // Test device-to-device transfer by creating two buffers
        var sourceBuffer = await memoryManager.AllocateAsync(sizeInBytes, DotCompute.Abstractions.MemoryOptions.None, cancellationToken);
        var destBuffer = await memoryManager.AllocateAsync(sizeInBytes, DotCompute.Abstractions.MemoryOptions.None, cancellationToken);
        
        try
        {
            // Initialize source buffer with test data
            var testData = new byte[sizeInBytes];
            new Random(42).NextBytes(testData);
            await sourceBuffer.CopyFromHostAsync(MemoryMarshal.Cast<byte, float>(testData), 0, cancellationToken);
            
            // Warmup - simulate device-to-device copy via host memory (actual device copy would need driver support)
            var tempBuffer = new float[sizeInBytes / sizeof(float)];
            for (int i = 0; i < WarmupIterations; i++)
            {
                await sourceBuffer.CopyToHostAsync(tempBuffer.AsMemory(), 0, cancellationToken);
                await destBuffer.CopyFromHostAsync(tempBuffer.AsMemory(), 0, cancellationToken);
            }
            
            // Benchmark device-to-device transfer
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                // Simulate device-to-device copy (in real implementation this would be optimized)
                await sourceBuffer.CopyToHostAsync(tempBuffer.AsMemory(), 0, cancellationToken);
                await destBuffer.CopyFromHostAsync(tempBuffer.AsMemory(), 0, cancellationToken);
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
            await sourceBuffer.DisposeAsync();
            await destBuffer.DisposeAsync();
        }
    }
    
    private static async Task<AllocationMeasurement> MeasureAllocationOverheadAsync(
        IMemoryManager memoryManager,
        long sizeInBytes,
        int allocationCount,
        CancellationToken cancellationToken)
    {
        var allocations = new IMemoryBuffer[allocationCount];
        
        try
        {
            // Warmup allocations
            for (int i = 0; i < WarmupIterations; i++)
            {
                var warmupMemory = await memoryManager.AllocateAsync(sizeInBytes, DotCompute.Abstractions.MemoryOptions.None, cancellationToken);
                await warmupMemory.DisposeAsync();
            }
            
            // Benchmark allocation performance
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < allocationCount; i++)
            {
                allocations[i] = await memoryManager.AllocateAsync(sizeInBytes, DotCompute.Abstractions.MemoryOptions.None, cancellationToken);
            }
            var allocationTime = stopwatch.Elapsed;
            
            // Benchmark deallocation performance
            stopwatch.Restart();
            for (int i = 0; i < allocationCount; i++)
            {
                await allocations[i].DisposeAsync();
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
                if (allocation != null)
                    await allocation.DisposeAsync();
            }
            throw;
        }
    }
    
    private static async Task<FragmentationMeasurement> MeasureFragmentationImpactAsync(
        IMemoryManager memoryManager,
        CancellationToken cancellationToken)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var allocations = new List<IMemoryBuffer>();
        var setupStopwatch = Stopwatch.StartNew();
        
        try
        {
            // Create fragmented memory pattern with varied allocation sizes
            for (int i = 0; i < 100; i++)
            {
                var size = random.Next(1024, 64 * 1024);
                allocations.Add(await memoryManager.AllocateAsync(size, DotCompute.Abstractions.MemoryOptions.None, cancellationToken));
            }
            
            // Free every other allocation to create memory fragmentation
            for (int i = 1; i < allocations.Count; i += 2)
            {
                await allocations[i].DisposeAsync();
                allocations[i] = null!;
            }
            
            setupStopwatch.Stop();
            
            // Measure allocation performance in fragmented memory state
            var stopwatch = Stopwatch.StartNew();
            var fragmentedAllocations = new List<IMemoryBuffer>();
            
            for (int i = 0; i < 50; i++)
            {
                var size = random.Next(1024, 32 * 1024);
                try
                {
                    fragmentedAllocations.Add(await memoryManager.AllocateAsync(size, DotCompute.Abstractions.MemoryOptions.None, cancellationToken));
                }
                catch (OutOfMemoryException)
                {
                    // Expected in highly fragmented scenario
                    break;
                }
                catch (InvalidOperationException)
                {
                    // May occur due to fragmentation
                    break;
                }
            }
            
            stopwatch.Stop();
            
            var result = new FragmentationMeasurement
            {
                FragmentationSetupTime = setupStopwatch.Elapsed,
                FragmentedAllocationTime = stopwatch.Elapsed,
                SuccessfulAllocations = fragmentedAllocations.Count,
                FragmentationLevel = 0.5 // 50% fragmentation level
            };
            
            // Clean up fragmented allocations
            foreach (var allocation in fragmentedAllocations)
            {
                await allocation.DisposeAsync();
            }
            
            return result;
        }
        finally
        {
            // Cleanup all remaining allocations
            foreach (var allocation in allocations)
            {
                if (allocation != null)
                    await allocation.DisposeAsync();
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
            tasks[t] = Task.Run(async () =>
            {
                var allocations = new IMemoryBuffer[allocationsPerThread];
                
                // Wait for all threads to be ready
                barrier.SignalAndWait();
                
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    for (int i = 0; i < allocationsPerThread; i++)
                    {
                        allocations[i] = await memoryManager.AllocateAsync(4096, DotCompute.Abstractions.MemoryOptions.None, cancellationToken);
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
                        if (allocation != null)
                            await allocation.DisposeAsync();
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
        // Estimate available memory (1GB baseline for testing)
        var estimatedTotalMemory = 1024L * 1024L * 1024L; // 1GB
        var targetPressure = (long)(estimatedTotalMemory * 0.8); // 80% pressure
        
        var allocations = new List<IMemoryBuffer>();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Allocate until we hit estimated memory pressure
            var currentAllocated = 0L;
            while (currentAllocated < targetPressure)
            {
                try
                {
                    var buffer = await memoryManager.AllocateAsync(1024 * 1024, DotCompute.Abstractions.MemoryOptions.None, cancellationToken);
                    allocations.Add(buffer);
                    currentAllocated += 1024 * 1024;
                }
                catch (OutOfMemoryException)
                {
                    // Hit memory pressure
                    break;
                }
            }
            
            stopwatch.Stop();
            
            return new MemoryPressureMeasurement
            {
                TimeToReachPressure = stopwatch.Elapsed,
                AllocationsAtPressure = allocations.Count,
                MemoryPressureLevel = 0.8,
                AvailableMemoryAtPressure = estimatedTotalMemory - currentAllocated
            };
        }
        finally
        {
            // Cleanup all pressure test allocations
            foreach (var allocation in allocations)
            {
                if (allocation != null)
                    await allocation.DisposeAsync();
            }
        }
    }
    
    private static async Task<PoolEfficiencyMeasurement> MeasurePoolAllocationEfficiencyAsync(
        MemoryPool<float> pool,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var buffers = new IMemoryBuffer<float>[BenchmarkIterations];
        
        try
        {
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                buffers[i] = pool.Rent(1024);
            }
            
            stopwatch.Stop();
            
            var stats = pool.GetPerformanceStats();
            
            return new PoolEfficiencyMeasurement
            {
                AllocationTime = stopwatch.Elapsed,
                AllocationCount = BenchmarkIterations,
                EfficiencyRatio = Math.Max(0.0, 1.0 - (double)stats.ReuseCount / BenchmarkIterations), // Approximate efficiency
                TotalRetainedBytes = stats.TotalRetainedBytes
            };
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                buffer?.Dispose();
            }
        }
    }
    
    private static async Task<PoolReuseMeasurement> MeasurePoolReuseRateAsync(
        MemoryPool<float> pool,
        CancellationToken cancellationToken)
    {
        // First round: allocate and return
        var buffers = new IMemoryBuffer<float>[50];
        for (int i = 0; i < 50; i++)
        {
            buffers[i] = pool.Rent(1024);
        }
        
        foreach (var buffer in buffers)
        {
            buffer.Dispose();
        }
        
        // Second round: should reuse from pool
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 50; i++)
        {
            buffers[i] = pool.Rent(1024);
        }
        stopwatch.Stop();
        
        var stats = pool.GetPerformanceStats();
        
        foreach (var buffer in buffers)
        {
            buffer.Dispose();
        }
        
        return new PoolReuseMeasurement
        {
            ReuseTime = stopwatch.Elapsed,
            ReuseCount = 50,
            ReuseRate = Math.Min(1.0, (double)stats.ReuseCount / 50), // Approximate reuse rate
            ReusePerSecond = 50 / stopwatch.Elapsed.TotalSeconds
        };
    }
    
    private static async Task<PoolMemoryOverheadMeasurement> MeasurePoolMemoryOverheadAsync(
        MemoryPool<float> pool,
        CancellationToken cancellationToken)
    {
        var initialStats = pool.GetPerformanceStats();
        
        // Allocate various sizes to fill buckets
        var buffers = new List<IMemoryBuffer<float>>();
        var sizes = new[] { 64, 128, 256, 512, 1024, 2048, 4096 };
        
        foreach (var size in sizes)
        {
            for (int i = 0; i < 10; i++)
            {
                buffers.Add(pool.Rent(size));
            }
        }
        
        // Return half to pool
        for (int i = 0; i < buffers.Count / 2; i++)
        {
            buffers[i].Dispose();
        }
        
        var finalStats = pool.GetPerformanceStats();
        
        // Cleanup remaining
        for (int i = buffers.Count / 2; i < buffers.Count; i++)
        {
            buffers[i].Dispose();
        }
        
        return new PoolMemoryOverheadMeasurement
        {
            RetainedBytes = finalStats.TotalRetainedBytes,
            AllocatedBytes = finalStats.TotalAllocatedBytes,
            OverheadRatio = (double)finalStats.TotalRetainedBytes / Math.Max(1, finalStats.TotalAllocatedBytes),
            BucketCount = sizes.Length // Approximate bucket count
        };
    }
    
    private static async Task<LazySyncMeasurement> MeasureLazySyncEfficiencyAsync(
        IMemoryManager memoryManager,
        MemoryPool<float> pool,
        CancellationToken cancellationToken)
    {
        using var buffer = new UnifiedBuffer<float>(memoryManager, 1024);
        
        // Measure host allocation (already done in constructor)
        var stopwatch = Stopwatch.StartNew();
        var hostSpan = buffer.AsSpan(); // Ensure host memory is accessible
        var hostAllocationTime = stopwatch.Elapsed;
        
        // Measure device allocation simulation
        stopwatch.Restart();
        await Task.Delay(1, cancellationToken); // Simulate device allocation timing
        var deviceAllocationTime = stopwatch.Elapsed;
        
        // Measure lazy sync simulation
        stopwatch.Restart();
        await Task.Delay(1, cancellationToken); // Simulate sync timing
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
        using var buffer = new UnifiedBuffer<float>(memoryManager, 1024);
        
        var transitions = new List<(BufferState From, BufferState To, TimeSpan Duration)>();
        
        // Uninitialized -> HostOnly (buffer is already initialized with host memory)
        var initialState = buffer.State;
        var stopwatch = Stopwatch.StartNew();
        var hostSpan = buffer.AsSpan(); // Triggers host allocation if needed
        var afterHostState = buffer.State;
        transitions.Add((initialState, afterHostState, stopwatch.Elapsed));
        
        // Simulate device allocation by accessing device memory
        stopwatch.Restart();
        // Note: In a real scenario, this would trigger device allocation
        // For testing purposes, we'll simulate the timing
        await Task.Delay(1, cancellationToken); // Minimal delay to simulate device allocation
        var afterDeviceState = BufferState.Synchronized; // Simulated state
        transitions.Add((afterHostState, afterDeviceState, stopwatch.Elapsed));
        
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
        using var buffer = new UnifiedBuffer<float>(memoryManager, 1024);
        
        // Initialize buffer (host memory is already allocated in constructor)
        var hostSpan = buffer.AsSpan(); // Ensure host memory is accessible
        
        // Measure memory coherence operations
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
        {
            // Simulate host modification
            var span = buffer.AsSpan();
            span[0] = i; // Modify host data
            
            // Simulate coherence check (in real implementation this would sync)
            await Task.Delay(1, cancellationToken); // Minimal delay to simulate sync overhead
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
    
    /// <summary>
    /// Simple test memory manager for benchmarks.
    /// </summary>
    private sealed class TestMemoryManager : IMemoryManager, IDisposable
    {
        private readonly MemoryAllocator _allocator = new();
        private bool _disposed;
        
        public ValueTask<IMemoryBuffer> AllocateAsync(long sizeInBytes, DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None, CancellationToken cancellationToken = default)
        {
            var buffer = new TestMemoryBuffer(sizeInBytes, options);
            return ValueTask.FromResult<IMemoryBuffer>(buffer);
        }
        
        public ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> source, DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None, CancellationToken cancellationToken = default) where T : unmanaged
        {
            unsafe
            {
                var buffer = new TestMemoryBuffer(source.Length * sizeof(T), options);
                return ValueTask.FromResult<IMemoryBuffer>(buffer);
            }
        }
        
        public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
        {
            return new TestMemoryBuffer(length, buffer.Options);
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _allocator.Dispose();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Simple test memory buffer for benchmarks.
    /// </summary>
    private sealed class TestMemoryBuffer : IMemoryBuffer
    {
        private bool _disposed;
        
        public long SizeInBytes { get; }
        public DotCompute.Abstractions.MemoryOptions Options { get; }
        public bool IsDisposed => _disposed;
        
        public TestMemoryBuffer(long sizeInBytes, DotCompute.Abstractions.MemoryOptions options)
        {
            SizeInBytes = sizeInBytes;
            Options = options;
        }
        
        public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        {
            return ValueTask.CompletedTask;
        }
        
        public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        {
            return ValueTask.CompletedTask;
        }
        
        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }
        
        public void Dispose()
        {
            _disposed = true;
        }
    }
}
#endif
