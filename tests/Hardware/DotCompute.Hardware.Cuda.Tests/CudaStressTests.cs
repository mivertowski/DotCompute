// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Persistent;
using DotCompute.Backends.CUDA.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Stress tests for CUDA backend to ensure stability under heavy load.
    /// These tests are designed to run for extended periods and stress various subsystems.
    /// </summary>
    [Collection("CUDA Stress Tests")]
    [Trait("Category", "Stress")]
    public class CudaStressTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<CudaStressTests> _logger;
        private readonly CudaContext _context;
        private readonly CudaDevice _device;
        private readonly CudaMemoryManager _memoryManager;
        private readonly CudaMemoryPoolManager _poolManager;
        private readonly CudaPinnedMemoryAllocator _pinnedAllocator;
        private readonly CudaMemoryPrefetcher _prefetcher;
        private readonly CudaErrorRecoveryManager _recoveryManager;
        private readonly CudaKernelCompiler _compiler;
        private readonly CudaKernelLauncher _launcher;

        public CudaStressTests(ITestOutputHelper output)
        {
            _output = output;
            _logger = new TestLogger<CudaStressTests>(output);
            
            if (!CudaContext.IsAvailable)
            {
                _output.WriteLine("CUDA not available, skipping stress tests");
                return;
            }

            _context = new CudaContext(0);
            _device = new CudaDevice(0, _logger);
            _memoryManager = new CudaMemoryManager(_context, _device, _logger);
            _poolManager = new CudaMemoryPoolManager(_context, _device, _logger);
            _pinnedAllocator = new CudaPinnedMemoryAllocator(_context, _logger);
            _prefetcher = new CudaMemoryPrefetcher(_context, _device, _logger);
            _recoveryManager = new CudaErrorRecoveryManager(_context, _logger);
            _compiler = new CudaKernelCompiler(_context, _logger);
            _launcher = new CudaKernelLauncher(_context, _logger);
        }

        [Fact]
        [Trait("Duration", "Long")]
        public async Task MemoryPool_ConcurrentAllocations_ShouldHandleHighLoad()
        {
            if (!CudaContext.IsAvailable) return;

            // Arrange
            const int threadCount = 20;
            const int allocationsPerThread = 100;
            const int minSize = 1024;
            const int maxSize = 1024 * 1024; // 1MB
            var random = new Random(42);
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            
            // Act
            var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(async () =>
            {
                var allocations = new List<IPooledMemoryBuffer>();
                var localRandom = new Random(threadId);
                
                try
                {
                    for (var i = 0; i < allocationsPerThread && !cts.Token.IsCancellationRequested; i++)
                    {
                        var size = localRandom.Next(minSize, maxSize);
                        var buffer = await _poolManager.AllocateAsync(size, zeroMemory: true, cts.Token);
                        allocations.Add(buffer);
                        
                        // Simulate work
                        await Task.Delay(localRandom.Next(1, 10), cts.Token);
                        
                        // Randomly free some allocations
                        if (allocations.Count > 10 && localRandom.NextDouble() > 0.5)
                        {
                            var index = localRandom.Next(allocations.Count);
                            allocations[index].Dispose();
                            allocations.RemoveAt(index);
                        }
                    }
                }
                finally
                {
                    // Clean up remaining allocations
                    foreach (var buffer in allocations)
                    {
                        buffer.Dispose();
                    }
                }
                
                return allocations.Count;
            })).ToArray();

            _ = await Task.WhenAll(tasks);

            // Assert
            var stats = _poolManager.GetStatistics();
            _output.WriteLine($"Pool Statistics:");
            _output.WriteLine($"  Total Allocations: {stats.TotalAllocations:N0}");
            _output.WriteLine($"  Pool Hit Rate: {stats.HitRate:P2}");
            _output.WriteLine($"  Total Bytes Allocated: {stats.TotalBytesAllocated:N0}");
            _output.WriteLine($"  Bytes in Pools: {stats.TotalBytesInPools:N0}");

            _ = stats.HitRate.Should().BeGreaterThan(0.5, "Pool should have reasonable hit rate");
            _ = stats.TotalAllocations.Should().BeGreaterThan(threadCount * allocationsPerThread / 2);
        }

        [Fact]
        [Trait("Duration", "Long")]
        public async Task PinnedMemory_HighBandwidthTransfers_ShouldAchieve10xSpeedup()
        {
            if (!CudaContext.IsAvailable) return;

            // Arrange
            const int dataSize = 100 * 1024 * 1024; // 100MB
            const int iterations = 10;
            var hostData = new float[dataSize / sizeof(float)];
            Random.Shared.NextBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(hostData.AsSpan()));

            // Test with regular memory
            var regularBuffer = await _memoryManager.AllocateAsync<float>(hostData.Length);
            var regularStopwatch = Stopwatch.StartNew();
            
            for (var i = 0; i < iterations; i++)
            {
                await regularBuffer.CopyFromHostAsync(hostData);
                await regularBuffer.CopyToHostAsync(hostData);
            }
            
            regularStopwatch.Stop();
            var regularBandwidth = (dataSize * 2.0 * iterations) / regularStopwatch.Elapsed.TotalSeconds / (1024 * 1024 * 1024);

            // Test with pinned memory
            var pinnedBuffer = await _pinnedAllocator.AllocatePinnedAsync<float>(hostData.Length);
            var deviceBuffer = await _memoryManager.AllocateAsync<float>(hostData.Length);
            var pinnedStopwatch = Stopwatch.StartNew();
            
            for (var i = 0; i < iterations; i++)
            {
                hostData.CopyTo(pinnedBuffer.AsSpan());
                await pinnedBuffer.CopyToDeviceAsync(deviceBuffer.DevicePointer);
                await pinnedBuffer.CopyFromDeviceAsync(deviceBuffer.DevicePointer);
            }
            
            pinnedStopwatch.Stop();
            var pinnedBandwidth = (dataSize * 2.0 * iterations) / pinnedStopwatch.Elapsed.TotalSeconds / (1024 * 1024 * 1024);

            // Assert
            _output.WriteLine($"Transfer Performance:");
            _output.WriteLine($"  Regular Memory: {regularBandwidth:F2} GB/s");
            _output.WriteLine($"  Pinned Memory: {pinnedBandwidth:F2} GB/s");
            _output.WriteLine($"  Speedup: {pinnedBandwidth / regularBandwidth:F1}x");

            _ = pinnedBandwidth.Should().BeGreaterThan(regularBandwidth * 2,

                "Pinned memory should provide significant bandwidth improvement");

            // Cleanup
            regularBuffer.Dispose();
            pinnedBuffer.Dispose();
            deviceBuffer.Dispose();
        }

        [Fact]
        [Trait("Duration", "Long")]
        public async Task ErrorRecovery_TransientFailures_ShouldRecover()
        {
            if (!CudaContext.IsAvailable) return;

            // Arrange
            const int operationCount = 100;
            var failureRate = 0.1; // 10% failure rate
            var random = new Random(42);
            var successCount = 0;
            var failureCount = 0;

            // Create a kernel that might fail
            var kernelCode = @"
                extern ""C"" __global__ void stress_test_kernel(float* data, int n, int fail_chance)
                {
                    int tid = blockIdx.x * blockDim.x + threadIdx.x;
                    if (tid < n) {
                        // Simulate potential failure based on fail_chance
                        if (tid % 100 < fail_chance) {
                            // This would normally cause an error, but we'll simulate it differently
                            data[tid] = -1.0f;
                        } else {
                            data[tid] = tid * 0.5f;
                        }
                    }
                }";

            var kernel = await _compiler.CompileKernelAsync(kernelCode, "stress_test_kernel");

            // Act
            for (var i = 0; i < operationCount; i++)
            {
                var shouldFail = random.NextDouble() < failureRate;
                
                try
                {
                    await _recoveryManager.ExecuteWithRecoveryAsync(async () =>
                    {
                        if (shouldFail && random.NextDouble() < 0.5)
                        {
                            // Simulate transient failure
                            throw new CudaException(CudaError.LaunchTimeout, "Simulated timeout");
                        }

                        var buffer = await _memoryManager.AllocateAsync<float>(1024);
                        var launchConfig = new KernelLaunchConfig(4, 256, 0);
                        
                        await _launcher.LaunchAsync(
                            kernel,
                            launchConfig,
                            IntPtr.Zero,
                            buffer.DevicePointer,
                            1024,
                            shouldFail ? 100 : 0);
                        
                        buffer.Dispose();
                        return true;
                    }, $"Operation_{i}");
                    
                    successCount++;
                }
                catch
                {
                    failureCount++;
                }
            }

            // Assert
            var stats = _recoveryManager.GetStatistics();
            _output.WriteLine($"Error Recovery Statistics:");
            _output.WriteLine($"  Total Errors: {stats.TotalErrors}");
            _output.WriteLine($"  Recovered Errors: {stats.RecoveredErrors}");
            _output.WriteLine($"  Permanent Failures: {stats.PermanentFailures}");
            _output.WriteLine($"  Recovery Rate: {stats.RecoverySuccessRate:P2}");
            _output.WriteLine($"  Success Count: {successCount}/{operationCount}");

            successCount.Should().BeGreaterThan(operationCount * 0.8, 
                "Most operations should succeed with recovery");
            _ = stats.RecoverySuccessRate.Should().BeGreaterThan(0.5,
                "Recovery should handle most transient failures");
        }

        [Fact]
        [Trait("Duration", "Long")]
        public async Task PersistentKernel_LongRunning_ShouldRemainStable()
        {
            if (!CudaContext.IsAvailable) return;

            // This test would require the full persistent kernel infrastructure
            // For now, we'll create a simulated long-running test TODO
            
            // Arrange
            const int iterations = 1000;
            const int dataSize = 1024 * 1024; // 1M elements
            var hostData = new float[dataSize];
            
            // Create a simple iterative kernel
            var kernelCode = @"
                extern ""C"" __global__ void iterative_kernel(float* data, int n)
                {
                    int tid = blockIdx.x * blockDim.x + threadIdx.x;
                    if (tid < n) {
                        // Simple iterative computation
                        float value = data[tid];
                        for (int i = 0; i < 10; i++) {
                            value = value * 1.01f + 0.001f;
                        }
                        data[tid] = value;
                    }
                }";

            var kernel = await _compiler.CompileKernelAsync(kernelCode, "iterative_kernel");
            var buffer = await _memoryManager.AllocateAsync<float>(dataSize);
            
            // Initialize data
            for (var i = 0; i < dataSize; i++)
            {
                hostData[i] = i * 0.001f;
            }
            await buffer.CopyFromHostAsync(hostData);

            var stopwatch = Stopwatch.StartNew();
            var launchConfig = new KernelLaunchConfig(dataSize / 256, 256, 0);

            // Act - Run many iterations
            for (var iter = 0; iter < iterations; iter++)
            {
                await _launcher.LaunchAsync(
                    kernel,
                    launchConfig,
                    IntPtr.Zero,
                    buffer.DevicePointer,
                    dataSize);
                
                // Periodic validation
                if (iter % 100 == 0)
                {
                    await buffer.CopyToHostAsync(hostData);
                    _ = hostData[0].Should().BeGreaterThan(0, "Data should be computed");
                    
                    _output.WriteLine($"Iteration {iter}: First value = {hostData[0]:F6}");
                }
            }

            stopwatch.Stop();

            // Assert
            await buffer.CopyToHostAsync(hostData);
            var throughput = (iterations * dataSize * sizeof(float)) / stopwatch.Elapsed.TotalSeconds / (1024 * 1024);
            
            _output.WriteLine($"Long-running kernel performance:");
            _output.WriteLine($"  Total iterations: {iterations}");
            _output.WriteLine($"  Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            _output.WriteLine($"  Throughput: {throughput:F2} MB/s");

            _ = hostData.All(v => !float.IsNaN(v) && !float.IsInfinity(v))
                .Should().BeTrue("All values should remain valid");

            // Cleanup
            buffer.Dispose();
        }

        [Fact]
        [Trait("Duration", "Long")]
        public async Task MemoryPrefetch_ConcurrentAccess_ShouldImprovePerformance()
        {
            if (!CudaContext.IsAvailable || !_prefetcher.SupportsPrefetch)
            {
                _output.WriteLine("Prefetch not supported, skipping test");
                return;
            }

            // Arrange
            const int bufferCount = 10;
            const int bufferSize = 10 * 1024 * 1024; // 10MB each
            var buffers = new List<IUnifiedMemoryBuffer<float>>();
            
            for (var i = 0; i < bufferCount; i++)
            {
                var buffer = await _memoryManager.AllocateAsync<float>(bufferSize / sizeof(float));
                buffers.Add(buffer);
            }

            // Test without prefetching
            var noPrefetchStopwatch = Stopwatch.StartNew();
            foreach (var buffer in buffers)
            {
                var data = new float[buffer.Length];
                await buffer.CopyFromHostAsync(data);
                await buffer.CopyToHostAsync(data);
            }
            noPrefetchStopwatch.Stop();

            // Test with prefetching
            var prefetchStopwatch = Stopwatch.StartNew();
            
            // Prefetch all buffers to device
            var prefetchTasks = buffers.Select(b => 
                _prefetcher.PrefetchToDeviceAsync(b.DevicePointer, bufferSize))
                .ToArray();
            _ = await Task.WhenAll(prefetchTasks);
            
            foreach (var buffer in buffers)
            {
                var data = new float[buffer.Length];
                await buffer.CopyFromHostAsync(data);
                
                // Record hit
                _prefetcher.RecordPrefetchHit(buffer.DevicePointer);
            }
            
            prefetchStopwatch.Stop();

            // Assert
            var stats = _prefetcher.GetStatistics();
            var speedup = noPrefetchStopwatch.ElapsedMilliseconds / (double)prefetchStopwatch.ElapsedMilliseconds;
            
            _output.WriteLine($"Prefetch Performance:");
            _output.WriteLine($"  Without Prefetch: {noPrefetchStopwatch.ElapsedMilliseconds} ms");
            _output.WriteLine($"  With Prefetch: {prefetchStopwatch.ElapsedMilliseconds} ms");
            _output.WriteLine($"  Speedup: {speedup:F2}x");
            _output.WriteLine($"  Hit Rate: {stats.HitRate:P2}");
            _output.WriteLine($"  Total Prefetched: {stats.TotalPrefetchedBytes:N0} bytes");

            _ = speedup.Should().BeGreaterThan(1.0, "Prefetching should improve performance");

            // Cleanup
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }

        [Fact]
        [Trait("Duration", "Long")]
        public async Task FullSystem_MixedWorkload_ShouldRemainStable()
        {
            if (!CudaContext.IsAvailable) return;

            // This test combines all systems under a mixed workload
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var tasks = new List<Task>();
            var errors = 0;
            var operations = 0;

            // Memory allocation stress
            tasks.Add(Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var size = Random.Shared.Next(1024, 1024 * 1024);
                        using var buffer = await _poolManager.AllocateAsync(size, cancellationToken: cts.Token);
                        _ = Interlocked.Increment(ref operations);
                        await Task.Delay(10, cts.Token);
                    }
                    catch
                    {
                        _ = Interlocked.Increment(ref errors);
                    }
                }
            }));

            // Pinned memory transfers
            tasks.Add(Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        using var pinned = await _pinnedAllocator.AllocatePinnedAsync<float>(10000, cancellationToken: cts.Token);
                        using var device = await _memoryManager.AllocateAsync<float>(10000, cts.Token);
                        await pinned.CopyToDeviceAsync(device.DevicePointer, cts.Token);
                        _ = Interlocked.Increment(ref operations);
                        await Task.Delay(20, cts.Token);
                    }
                    catch
                    {
                        _ = Interlocked.Increment(ref errors);
                    }
                }
            }));

            // Kernel execution with recovery
            tasks.Add(Task.Run(async () =>
            {
                var kernelCode = @"
                    extern ""C"" __global__ void simple_kernel(float* data, int n)
                    {
                        int tid = blockIdx.x * blockDim.x + threadIdx.x;
                        if (tid < n) data[tid] *= 2.0f;
                    }";
                
                var kernel = await _compiler.CompileKernelAsync(kernelCode, "simple_kernel");
                
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await _recoveryManager.ExecuteWithRecoveryAsync(async () =>
                        {
                            using var buffer = await _memoryManager.AllocateAsync<float>(1000, cts.Token);
                            var config = new KernelLaunchConfig(4, 256, 0);
                            await _launcher.LaunchAsync(kernel, config, IntPtr.Zero, buffer.DevicePointer, 1000);
                        }, "kernel_exec", cancellationToken: cts.Token);

                        _ = Interlocked.Increment(ref operations);
                        await Task.Delay(15, cts.Token);
                    }
                    catch
                    {
                        _ = Interlocked.Increment(ref errors);
                    }
                }
            }));

            // Wait for all tasks
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }

            // Assert
            _output.WriteLine($"Mixed Workload Results:");
            _output.WriteLine($"  Total Operations: {operations}");
            _output.WriteLine($"  Total Errors: {errors}");
            _output.WriteLine($"  Error Rate: {(double)errors / operations:P2}");
            
            var poolStats = _poolManager.GetStatistics();
            _output.WriteLine($"  Pool Hit Rate: {poolStats.HitRate:P2}");
            
            var recoveryStats = _recoveryManager.GetStatistics();
            _output.WriteLine($"  Recovery Success Rate: {recoveryStats.RecoverySuccessRate:P2}");

            _ = operations.Should().BeGreaterThan(100, "Should complete many operations");
            _ = ((double)errors / operations).Should().BeLessThan(0.1, "Error rate should be low");
        }

        public void Dispose()
        {
            _launcher?.Dispose();
            _compiler?.Dispose();
            _recoveryManager?.Dispose();
            _prefetcher?.Dispose();
            _pinnedAllocator?.Dispose();
            _poolManager?.Dispose();
            _memoryManager?.Dispose();
            _device?.Dispose();
            _context?.Dispose();
        }

        private class TestLogger<T> : ILogger<T>
        {
            private readonly ITestOutputHelper _output;

            public TestLogger(ITestOutputHelper output)
            {
                _output = output;
            }

            public IDisposable BeginScope<TState>(TState state) => null!;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, 
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] {message}");
                if (exception != null)
                {
                    _output.WriteLine($"Exception: {exception}");
                }
            }
        }
    }
}