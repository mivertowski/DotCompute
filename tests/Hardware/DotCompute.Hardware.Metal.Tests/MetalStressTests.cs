// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Kernels;
using FluentAssertions;

namespace DotCompute.Hardware.Metal.Tests;

/// <summary>
/// Stress tests for Metal backend to ensure stability under heavy load.
/// These tests are designed to run for extended periods and stress various subsystems.
/// </summary>
[Collection("Metal Stress Tests")]
[Trait("Category", "Stress")]
[Trait("Category", "RequiresMetal")]
[Trait("Category", "LongRunning")]
public class MetalStressTests : MetalTestBase
{
    public MetalStressTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task Memory_Pool_Concurrent_Allocations_Should_Handle_High_Load()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int threadCount = 20;
        const int allocationsPerThread = 100;
        const int minSize = 1024;
        const int maxSize = 1024 * 1024; // 1MB
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(async () =>
        {
            var allocations = new List<IDisposable>();
            var localRandom = new Random(threadId);
            var successCount = 0;

            try
            {
                for (var i = 0; i < allocationsPerThread && !cts.Token.IsCancellationRequested; i++)
                {
                    var size = localRandom.Next(minSize, maxSize);
                    var buffer = await accelerator.Memory.AllocateAsync<byte>(size);
                    allocations.Add(buffer);
                    successCount++;

                    // Simulate work
                    await Task.Delay(localRandom.Next(1, 10), cts.Token);

                    // Randomly free some allocations to create memory pressure
                    if (allocations.Count > 10 && localRandom.NextDouble() > 0.5)
                    {
                        var index = localRandom.Next(allocations.Count);
                        allocations[index].Dispose();
                        allocations.RemoveAt(index);
                    }
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Thread {threadId} error: {ex.Message}");
            }
            finally
            {
                // Clean up remaining allocations
                foreach (var buffer in allocations)
                {
                    buffer.Dispose();
                }
            }

            return successCount;
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        var totalSuccesses = results.Sum();
        var expectedTotal = threadCount * allocationsPerThread;
        var successRate = (double)totalSuccesses / expectedTotal;

        Output.WriteLine($"Concurrent Memory Allocation Stress Test:");
        Output.WriteLine($"  Thread count: {threadCount}");
        Output.WriteLine($"  Allocations per thread: {allocationsPerThread}");
        Output.WriteLine($"  Total successful allocations: {totalSuccesses:N0}");
        Output.WriteLine($"  Success rate: {successRate:P1}");

        // Should achieve at least 80% success rate under stress
        successRate.Should().BeGreaterThan(0.8, "Should handle most concurrent allocations successfully");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task Memory_Pressure_Testing_Should_Handle_Exhaustion_Gracefully()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        var totalMemory = accelerator.Info.TotalMemory;
        var chunkSize = Math.Min(256 * 1024 * 1024, totalMemory / 10); // 256MB chunks or 10% of total
        var buffers = new List<IDisposable>();
        var allocatedBytes = 0L;
        var maxAllocations = 0;
        Exception? lastException = null;

        try
        {
            // Allocate until we hit memory limits
            while (allocatedBytes < totalMemory * 0.9) // Stop at 90% to avoid system issues
            {
                try
                {
                    var buffer = await accelerator.Memory.AllocateAsync<byte>((int)chunkSize);
                    buffers.Add(buffer);
                    allocatedBytes += chunkSize;
                    maxAllocations++;

                    if (maxAllocations % 10 == 0)
                    {
                        Output.WriteLine($"Allocated {maxAllocations} chunks ({allocatedBytes / (1024 * 1024 * 1024):F2} GB)");
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Output.WriteLine($"Allocation failed at {allocatedBytes / (1024 * 1024 * 1024):F2} GB: {ex.Message}");
                    break;
                }
            }

            // Test that we can still allocate smaller buffers
            var smallBufferSize = 1024 * 1024; // 1MB
            var smallBufferCount = 0;
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    var smallBuffer = await accelerator.Memory.AllocateAsync<byte>(smallBufferSize);
                    buffers.Add(smallBuffer);
                    smallBufferCount++;
                }
                catch
                {
                    break;
                }
            }

            Output.WriteLine($"Memory Pressure Test Results:");
            Output.WriteLine($"  Total memory: {totalMemory / (1024 * 1024 * 1024):F2} GB");
            Output.WriteLine($"  Allocated: {allocatedBytes / (1024 * 1024 * 1024):F2} GB");
            Output.WriteLine($"  Large allocations: {maxAllocations}");
            Output.WriteLine($"  Small allocations after pressure: {smallBufferCount}/10");
            
            if (lastException != null)
            {
                Output.WriteLine($"  Final allocation error: {lastException.GetType().Name}");
            }

            // Should be able to allocate a reasonable amount of memory
            (allocatedBytes / (double)totalMemory).Should().BeGreaterThan(0.5, 
                "Should be able to allocate at least 50% of available memory");

            // Should still be able to allocate some small buffers even under pressure
            smallBufferCount.Should().BeGreaterThan(0, 
                "Should be able to allocate small buffers even under memory pressure");
        }
        finally
        {
            // Cleanup all allocations
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task Kernel_Execution_Under_Load_Should_Remain_Stable()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int operationCount = 1000;
        var successCount = 0;
        var failureCount = 0;

        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;
            
            kernel void stress_test_kernel(
                device float* data [[buffer(0)]],
                constant uint& n [[buffer(1)]],
                uint id [[thread_position_in_grid]]
            ) {
                if (id >= n) return;
                data[id] = float(id) * 0.5f + sin(float(id) * 0.01f);
            }";

        var kernelDef = new KernelDefinition
        {
            Name = "stress_test_kernel",
            Code = kernelCode,
            EntryPoint = "stress_test_kernel"
        };

        var kernel = await accelerator.CompileKernelAsync(kernelDef);
        
        const int dataSize = 1024;
        const int threadsPerThreadgroup = 256;
        var threadgroups = (dataSize + threadsPerThreadgroup - 1) / threadsPerThreadgroup;

        for (var i = 0; i < operationCount; i++)
        {
            try
            {
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(dataSize);

                var args = new KernelArguments { buffer, (uint)dataSize };
                await kernel.LaunchAsync((threadgroups, 1, 1), (threadsPerThreadgroup, 1, 1), args);
                await accelerator.SynchronizeAsync();

                // Verify result occasionally
                if (i % 100 == 0)
                {
                    var result = new float[dataSize];
                    await buffer.CopyToAsync(result.AsMemory());
                    result[0].Should().BeApproximately(0.0f, 0.1f); // First element should be close to 0
                }

                successCount++;

                // Add some pressure by not waiting between operations
                if (i % 50 == 0)
                {
                    await Task.Delay(1); // Brief pause every 50 operations
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Operation {i} failed: {ex.Message}");
                failureCount++;
            }
        }

        var successRate = (double)successCount / operationCount;

        Output.WriteLine($"Kernel Execution Under Load:");
        Output.WriteLine($"  Total operations: {operationCount}");
        Output.WriteLine($"  Successful: {successCount}");
        Output.WriteLine($"  Failed: {failureCount}");
        Output.WriteLine($"  Success rate: {successRate:P1}");

        successRate.Should().BeGreaterThan(0.95, "Should maintain high success rate under load");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task Long_Running_Computation_Should_Remain_Stable()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int iterations = 500; // Increased for stress testing
        const int dataSize = 1024 * 256; // 256K elements
        var hostData = new float[dataSize];

        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;
            
            kernel void iterative_kernel(
                device float* data [[buffer(0)]],
                constant uint& n [[buffer(1)]],
                uint id [[thread_position_in_grid]]
            ) {
                if (id >= n) return;
                
                float value = data[id];
                // Iterative computation to simulate long-running work
                for (int i = 0; i < 100; i++) {
                    value = value * 1.01f + 0.001f;
                    if (value > 1000.0f) value = value * 0.5f; // Prevent overflow
                }
                data[id] = value;
            }";

        var kernelDef = new KernelDefinition
        {
            Name = "iterative_kernel",
            Code = kernelCode,
            EntryPoint = "iterative_kernel"
        };

        var kernel = await accelerator.CompileKernelAsync(kernelDef);
        await using var buffer = await accelerator.Memory.AllocateAsync<float>(dataSize);

        // Initialize data
        for (var i = 0; i < dataSize; i++)
        {
            hostData[i] = i * 0.001f;
        }
        await buffer.CopyFromAsync(hostData.AsMemory());

        var stopwatch = Stopwatch.StartNew();
        const int threadsPerThreadgroup = 256;
        var threadgroups = (dataSize + threadsPerThreadgroup - 1) / threadsPerThreadgroup;

        var validationInterval = 50;
        var errorCount = 0;

        // Run iterations
        for (var iter = 0; iter < iterations; iter++)
        {
            try
            {
                var args = new KernelArguments { buffer, (uint)dataSize };
                await kernel.LaunchAsync((threadgroups, 1, 1), (threadsPerThreadgroup, 1, 1), args);

                // Periodic validation and synchronization
                if (iter % validationInterval == 0)
                {
                    await accelerator.SynchronizeAsync();
                    await buffer.CopyToAsync(hostData.AsMemory());
                    
                    // Validate data integrity
                    var validCount = 0;
                    for (var i = 0; i < Math.Min(1000, dataSize); i++)
                    {
                        if (!float.IsNaN(hostData[i]) && !float.IsInfinity(hostData[i]))
                        {
                            validCount++;
                        }
                    }

                    if (validCount < 900) // At least 90% should be valid
                    {
                        errorCount++;
                        Output.WriteLine($"Validation failed at iteration {iter}: only {validCount}/1000 values valid");
                    }

                    Output.WriteLine($"Iteration {iter}: First value = {hostData[0]:F6}");
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                Output.WriteLine($"Error at iteration {iter}: {ex.Message}");
            }
        }

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        // Final validation
        await buffer.CopyToAsync(hostData.AsMemory());
        var finalValidCount = hostData.Count(v => !float.IsNaN(v) && !float.IsInfinity(v));
        var throughputMBps = (iterations * dataSize * sizeof(float)) / stopwatch.Elapsed.TotalSeconds / (1024 * 1024);

        Output.WriteLine($"Long-running computation results:");
        Output.WriteLine($"  Total iterations: {iterations}");
        Output.WriteLine($"  Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Output.WriteLine($"  Throughput: {throughputMBps:F2} MB/s");
        Output.WriteLine($"  Error count: {errorCount}");
        Output.WriteLine($"  Final valid values: {finalValidCount}/{dataSize}");

        // Stability checks
        errorCount.Should().BeLessThan(iterations * 0.05, "Error rate should be less than 5%");
        finalValidCount.Should().BeGreaterThan((int)(dataSize * 0.95), "At least 95% of values should remain valid");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task Concurrent_Memory_Operations_Should_Handle_Contention()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int concurrentOperations = 20;
        const int operationsPerTask = 100;
        const int bufferSize = 1024 * 1024; // 1MB per buffer

        var tasks = new Task[concurrentOperations];
        var successCounts = new int[concurrentOperations];
        var errorCounts = new int[concurrentOperations];

        for (var taskId = 0; taskId < concurrentOperations; taskId++)
        {
            var id = taskId;
            tasks[id] = Task.Run(async () =>
            {
                var random = new Random(id);
                
                for (var op = 0; op < operationsPerTask; op++)
                {
                    try
                    {
                        // Create buffer
                        await using var buffer = await accelerator.Memory.AllocateAsync<float>(bufferSize / sizeof(float));
                        
                        // Generate test data
                        var data = new float[bufferSize / sizeof(float)];
                        for (var i = 0; i < data.Length; i++)
                        {
                            data[i] = random.NextSingle() * 100;
                        }

                        // Write data
                        await buffer.CopyFromAsync(data.AsMemory());

                        // Read data back
                        var result = new float[data.Length];
                        await buffer.CopyToAsync(result.AsMemory());

                        // Verify data integrity
                        var matches = 0;
                        for (var i = 0; i < Math.Min(100, data.Length); i++)
                        {
                            if (Math.Abs(data[i] - result[i]) < 0.001f)
                            {
                                matches++;
                            }
                        }

                        if (matches >= 95) // At least 95% should match
                        {
                            successCounts[id]++;
                        }
                        else
                        {
                            errorCounts[id]++;
                        }

                        // Brief delay to create contention
                        await Task.Delay(random.Next(1, 5));
                    }
                    catch (Exception)
                    {
                        errorCounts[id]++;
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        var totalSuccess = successCounts.Sum();
        var totalErrors = errorCounts.Sum();
        var totalOperations = concurrentOperations * operationsPerTask;
        var successRate = (double)totalSuccess / totalOperations;

        Output.WriteLine($"Concurrent Memory Operations Results:");
        Output.WriteLine($"  Concurrent tasks: {concurrentOperations}");
        Output.WriteLine($"  Operations per task: {operationsPerTask}");
        Output.WriteLine($"  Total operations: {totalOperations}");
        Output.WriteLine($"  Successful: {totalSuccess}");
        Output.WriteLine($"  Errors: {totalErrors}");
        Output.WriteLine($"  Success rate: {successRate:P1}");

        // Individual task statistics
        for (var i = 0; i < concurrentOperations; i++)
        {
            var taskSuccessRate = (double)successCounts[i] / operationsPerTask;
            Output.WriteLine($"  Task {i}: {taskSuccessRate:P1} ({successCounts[i]}/{operationsPerTask})");
        }

        successRate.Should().BeGreaterThan(0.9, "Should maintain high success rate with concurrent memory operations");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task Mixed_Workload_Stress_Test_Should_Remain_Stable()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1)); // 1 minute stress test
        var tasks = new List<Task>();
        var operationCounts = new Dictionary<string, int>();
        var errorCounts = new Dictionary<string, int>();
        var lockObject = new object();

        void IncrementCounter(string operation, bool success)
        {
            lock (lockObject)
            {
                if (success)
                {
                    operationCounts.TryGetValue(operation, out var count);
                    operationCounts[operation] = count + 1;
                }
                else
                {
                    errorCounts.TryGetValue(operation, out var count);
                    errorCounts[operation] = count + 1;
                }
            }
        }

        // Memory allocation stress
        tasks.Add(Task.Run(async () =>
        {
            var random = new Random(1);
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var size = random.Next(1024, 1024 * 64);
                    await using var buffer = await accelerator.Memory.AllocateAsync<byte>(size);
                    IncrementCounter("MemoryAllocation", true);
                    await Task.Delay(random.Next(5, 15), cts.Token);
                }
                catch
                {
                    IncrementCounter("MemoryAllocation", false);
                }
            }
        }));

        // Kernel execution stress
        tasks.Add(Task.Run(async () =>
        {
            var kernelCode = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void stress_kernel(
                    device float* data [[buffer(0)]],
                    constant uint& n [[buffer(1)]],
                    uint id [[thread_position_in_grid]]
                ) {
                    if (id >= n) return;
                    data[id] = data[id] * 1.1f + 0.01f;
                }";

            var kernelDef = new KernelDefinition
            {
                Name = "stress_kernel",
                Code = kernelCode,
                EntryPoint = "stress_kernel"
            };

            var kernel = await accelerator.CompileKernelAsync(kernelDef);
            var random = new Random(2);

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    const int dataSize = 1000;
                    await using var buffer = await accelerator.Memory.AllocateAsync<float>(dataSize);
                    
                    var args = new KernelArguments { buffer, (uint)dataSize };
                    await kernel.LaunchAsync((4, 1, 1), (256, 1, 1), args);
                    
                    IncrementCounter("KernelExecution", true);
                    await Task.Delay(random.Next(10, 25), cts.Token);
                }
                catch
                {
                    IncrementCounter("KernelExecution", false);
                }
            }
        }));

        // Memory transfer stress
        tasks.Add(Task.Run(async () =>
        {
            var random = new Random(3);
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    const int dataSize = 10000;
                    var data = new float[dataSize];
                    random.NextBytes(MemoryMarshal.AsBytes(data.AsSpan()));

                    await using var buffer = await accelerator.Memory.AllocateAsync<float>(dataSize);
                    await buffer.CopyFromAsync(data.AsMemory());
                    await buffer.CopyToAsync(data.AsMemory());

                    IncrementCounter("MemoryTransfer", true);
                    await Task.Delay(random.Next(8, 20), cts.Token);
                }
                catch
                {
                    IncrementCounter("MemoryTransfer", false);
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

        // Calculate results
        var totalOperations = operationCounts.Values.Sum();
        var totalErrors = errorCounts.Values.Sum();
        var overallSuccessRate = totalOperations > 0 ? (double)totalOperations / (totalOperations + totalErrors) : 0;

        Output.WriteLine($"Mixed Workload Stress Test Results:");
        Output.WriteLine($"  Duration: {cts.Token.IsCancellationRequested} (1 minute)");
        Output.WriteLine($"  Total successful operations: {totalOperations}");
        Output.WriteLine($"  Total errors: {totalErrors}");
        Output.WriteLine($"  Overall success rate: {overallSuccessRate:P1}");

        foreach (var kvp in operationCounts)
        {
            var errors = errorCounts.TryGetValue(kvp.Key, out var errorCount) ? errorCount : 0;
            var successRate = (double)kvp.Value / (kvp.Value + errors);
            Output.WriteLine($"  {kvp.Key}: {kvp.Value} successes, {errors} errors ({successRate:P1})");
        }

        totalOperations.Should().BeGreaterThan(50, "Should complete a reasonable number of operations");
        overallSuccessRate.Should().BeGreaterThan(0.85, "Overall success rate should be high under mixed load");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}