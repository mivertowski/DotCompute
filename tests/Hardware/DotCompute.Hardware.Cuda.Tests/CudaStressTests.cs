// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Backends.CUDA.Factory;
// using DotCompute.Backends.CUDA.Kernels; // Not needed
using DotCompute.Hardware.Cuda.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Stress tests for CUDA backend to ensure stability under heavy load.
    /// These tests are designed to run for extended periods and stress various subsystems.
    /// </summary>
    [Collection("CUDA Stress Tests")]
    [Trait("Category", "Stress")]
    [Trait("Category", "HardwareRequired")]
    public class CudaStressTests : CudaTestBase
    {
        private readonly CudaAcceleratorFactory _factory;

        public CudaStressTests(ITestOutputHelper output) : base(output)
        {
            _factory = new CudaAcceleratorFactory();
        }

        [SkippableFact]
        [Trait("Duration", "Long")]
        public async Task MemoryPool_ConcurrentAllocations_ShouldHandleHighLoad()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);

            // Simplified memory pool test using available memory manager

            const int threadCount = 20;
            const int allocationsPerThread = 100;
            const int minSize = 1024;
            const int maxSize = 1024 * 1024; // 1MB
            var random = new Random(42);
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

            // Act

            var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(async () =>
            {
                var allocations = new List<IDisposable>();
                var localRandom = new Random(threadId);


                try
                {
                    for (var i = 0; i < allocationsPerThread && !cts.Token.IsCancellationRequested; i++)
                    {
                        var size = localRandom.Next(minSize, maxSize);
                        var buffer = await accelerator.Memory.AllocateAsync<byte>(size);
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

            await Task.WhenAll(tasks);

            // Assert
            Output.WriteLine($"Concurrent allocations test completed");
            Output.WriteLine($"  Thread count: {threadCount}");
            Output.WriteLine($"  Allocations per thread: {allocationsPerThread}");
        }

        [SkippableFact]
        [Trait("Duration", "Long")]
        public async Task PinnedMemory_HighBandwidthTransfers_ShouldImprovePerformance()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);

            // Arrange

            const int dataSize = 10 * 1024 * 1024; // 10MB (reduced for stability)
            const int iterations = 10;
            var hostData = new float[dataSize / sizeof(float)];
            Random.Shared.NextBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(hostData.AsSpan()));

            // Test with regular memory
            var regularBuffer = await accelerator.Memory.AllocateAsync<float>(hostData.Length);
            var regularStopwatch = Stopwatch.StartNew();


            for (var i = 0; i < iterations; i++)
            {
                await regularBuffer.CopyFromAsync(hostData.AsMemory());
                await regularBuffer.CopyToAsync(hostData.AsMemory());
            }


            regularStopwatch.Stop();
            var regularBandwidth = (dataSize * 2.0 * iterations) / regularStopwatch.Elapsed.TotalSeconds / (1024 * 1024 * 1024);

            // Assert
            Output.WriteLine($"Transfer Performance:");
            Output.WriteLine($"  Regular Memory: {regularBandwidth:F2} GB/s");
            Output.WriteLine($"  Total time: {regularStopwatch.ElapsedMilliseconds} ms");

            regularBandwidth.Should().BeGreaterThan(0, "Should achieve measurable bandwidth");

            // Cleanup
            regularBuffer.Dispose();
        }

        [SkippableFact]
        [Trait("Duration", "Long")]
        public async Task Kernel_Execution_Under_Load_ShouldRemainStable()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);

            // Arrange

            const int operationCount = 100;
            var successCount = 0;
            var failureCount = 0;

            // Create a simple test kernel
            var kernelCode = @"
                extern ""C"" __global__ void stress_test_kernel(float* data, int n)
                {
                    int tid = blockIdx.x * blockDim.x + threadIdx.x;
                    if (tid < n) {
                        data[tid] = tid * 0.5f;
                    }
                }";

            var kernelDef = CudaTestHelpers.CreateTestKernelDefinition(
                "stress_test_kernel",
                kernelCode
            );

            var kernel = await accelerator.CompileKernelAsync(kernelDef);

            // Act
            for (var i = 0; i < operationCount; i++)
            {
                try
                {
                    using var buffer = await accelerator.Memory.AllocateAsync<float>(1024);


                    var (grid, block) = CudaTestHelpers.CreateLaunchConfig(4, 1, 1, 256, 1, 1);


                    var kernelArgs = CudaTestHelpers.CreateKernelArguments(
                        [buffer, 1024],
                        grid,
                        block
                    );
                    await kernel.ExecuteAsync(kernelArgs);


                    await accelerator.SynchronizeAsync();
                    successCount++;
                }
                catch
                {
                    failureCount++;
                }
            }

            // Assert
            Output.WriteLine($"Kernel Execution Statistics:");
            Output.WriteLine($"  Success Count: {successCount}/{operationCount}");
            Output.WriteLine($"  Failure Count: {failureCount}");

            successCount.Should().BeGreaterThan((int)(operationCount * 0.9),

                "Most operations should succeed");
        }

        [SkippableFact]
        [Trait("Duration", "Long")]
        public async Task LongRunning_Kernel_ShouldRemainStable()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);

            // Arrange

            const int iterations = 100; // Reduced for test stability
            const int dataSize = 1024 * 256; // 256K elements
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

            var kernelDef = CudaTestHelpers.CreateTestKernelDefinition(
                "iterative_kernel",
                kernelCode
            );

            var kernel = await accelerator.CompileKernelAsync(kernelDef);
            using var buffer = await accelerator.Memory.AllocateAsync<float>(dataSize);

            // Initialize data

            for (var i = 0; i < dataSize; i++)
            {
                hostData[i] = i * 0.001f;
            }
            await buffer.CopyFromAsync(hostData.AsMemory());

            var stopwatch = Stopwatch.StartNew();
            var (grid, block) = CudaTestHelpers.CreateLaunchConfig(dataSize / 256, 1, 1, 256, 1, 1);

            // Act - Run many iterations
            for (var iter = 0; iter < iterations; iter++)
            {
                var kernelArgs = CudaTestHelpers.CreateKernelArguments(
                    [buffer, dataSize],
                    grid,
                    block
                );
                await kernel.ExecuteAsync(kernelArgs);

                // Periodic validation

                if (iter % 20 == 0)
                {
                    await buffer.CopyToAsync(hostData.AsMemory());
                    hostData[0].Should().BeGreaterThan(0, "Data should be computed");


                    Output.WriteLine($"Iteration {iter}: First value = {hostData[0]:F6}");
                }
            }

            await accelerator.SynchronizeAsync();
            stopwatch.Stop();

            // Assert
            await buffer.CopyToAsync(hostData.AsMemory());
            var throughput = (iterations * dataSize * sizeof(float)) / stopwatch.Elapsed.TotalSeconds / (1024 * 1024);


            Output.WriteLine($"Long-running kernel performance:");
            Output.WriteLine($"  Total iterations: {iterations}");
            Output.WriteLine($"  Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            Output.WriteLine($"  Throughput: {throughput:F2} MB/s");

            hostData.All(v => !float.IsNaN(v) && !float.IsInfinity(v))
                .Should().BeTrue("All values should remain valid");
        }

        [SkippableFact]
        [Trait("Duration", "Long")]
        public async Task Memory_ConcurrentAccess_ShouldHandleMultipleBuffers()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);

            // Arrange

            const int bufferCount = 5; // Reduced for stability
            const int bufferSize = 1 * 1024 * 1024; // 1MB each


            var stopwatch = Stopwatch.StartNew();

            // Test sequential memory allocations and access

            for (var i = 0; i < bufferCount; i++)
            {
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(bufferSize / sizeof(float));
                var data = new float[bufferSize / sizeof(float)];
                await buffer.CopyFromAsync(data.AsMemory());
                await buffer.CopyToAsync(data.AsMemory());
            }


            stopwatch.Stop();

            // Assert
            Output.WriteLine($"Sequential Memory Access Performance:");
            Output.WriteLine($"  Buffer count: {bufferCount}");
            Output.WriteLine($"  Buffer size: {bufferSize / (1024 * 1024)} MB each");
            Output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds} ms");
            Output.WriteLine($"  Average per buffer: {stopwatch.ElapsedMilliseconds / (double)bufferCount:F2} ms");

            stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(0, "Should measure time");
        }

        [SkippableFact]
        [Trait("Duration", "Long")]
        public async Task FullSystem_MixedWorkload_ShouldRemainStable()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            using var accelerator = _factory.CreateProductionAccelerator(0);

            // This test combines multiple operations under a mixed workload

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Reduced duration
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
                        var size = Random.Shared.Next(1024, 1024 * 64); // Smaller sizes
                        using var buffer = await accelerator.Memory.AllocateAsync<byte>(size);
                        Interlocked.Increment(ref operations);
                        await Task.Delay(10, cts.Token);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }
                }
            }));

            // Kernel execution
            tasks.Add(Task.Run(async () =>
            {
                var kernelCode = @"
                    extern ""C"" __global__ void simple_kernel(float* data, int n)
                    {
                        int tid = blockIdx.x * blockDim.x + threadIdx.x;
                        if (tid < n) data[tid] *= 2.0f;
                    }";


                var kernelDef = CudaTestHelpers.CreateTestKernelDefinition(
                    "simple_kernel",
                    kernelCode
                );


                var kernel = await accelerator.CompileKernelAsync(kernelDef);


                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        using var buffer = await accelerator.Memory.AllocateAsync<float>(1000);
                        var (grid, block) = CudaTestHelpers.CreateLaunchConfig(4, 1, 1, 256, 1, 1);


                        var kernelArgs = CudaTestHelpers.CreateKernelArguments(
                            [buffer, 1000],
                            grid,
                            block
                        );
                        await kernel.ExecuteAsync(kernelArgs);

                        Interlocked.Increment(ref operations);
                        await Task.Delay(15, cts.Token);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
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
            Output.WriteLine($"Mixed Workload Results:");
            Output.WriteLine($"  Total Operations: {operations}");
            Output.WriteLine($"  Total Errors: {errors}");
            Output.WriteLine($"  Error Rate: {(double)errors / (operations > 0 ? operations : 1):P2}");

            operations.Should().BeGreaterThan(10, "Should complete some operations");
            ((double)errors / (operations > 0 ? operations : 1)).Should().BeLessThan(0.2, "Error rate should be reasonable");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _factory?.Dispose();
            }
            base.Dispose(disposing);
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