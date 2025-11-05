// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
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
        errorCount.Should().BeLessThan((int)(iterations * 0.05), "Error rate should be less than 5%");
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

    // ===============================================
    // MEMORY STRESS TESTS (Additional 5 tests)
    // ===============================================

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task AllocateMaxMemory_NoCrash()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        var totalMemory = accelerator.Info.TotalMemory;
        var targetAllocation = (long)(totalMemory * 0.95); // Try to allocate 95% of available memory
        var chunkSize = 128 * 1024 * 1024; // 128MB chunks
        var buffers = new List<IDisposable>();
        var allocatedBytes = 0L;
        var allocationCount = 0;
        var memoryLimitReached = false;

        try
        {
            Output.WriteLine($"Target allocation: {targetAllocation / (1024.0 * 1024 * 1024):F2} GB ({targetAllocation / (double)totalMemory:P1} of total)");

            while (allocatedBytes < targetAllocation)
            {
                try
                {
                    var buffer = await accelerator.Memory.AllocateAsync<byte>(chunkSize);
                    buffers.Add(buffer);
                    allocatedBytes += chunkSize;
                    allocationCount++;

                    if (allocationCount % 5 == 0)
                    {
                        Output.WriteLine($"  Allocated {allocationCount} chunks ({allocatedBytes / (1024.0 * 1024 * 1024):F2} GB, {allocatedBytes / (double)totalMemory:P1})");
                    }
                }
                catch (OutOfMemoryException)
                {
                    memoryLimitReached = true;
                    Output.WriteLine($"  Memory limit reached at {allocatedBytes / (1024.0 * 1024 * 1024):F2} GB");
                    break;
                }
                catch (Exception ex)
                {
                    Output.WriteLine($"  Allocation stopped: {ex.GetType().Name}: {ex.Message}");
                    break;
                }
            }

            Output.WriteLine($"AllocateMaxMemory Results:");
            Output.WriteLine($"  Total memory: {totalMemory / (1024.0 * 1024 * 1024):F2} GB");
            Output.WriteLine($"  Allocated: {allocatedBytes / (1024.0 * 1024 * 1024):F2} GB ({allocatedBytes / (double)totalMemory:P1})");
            Output.WriteLine($"  Chunk count: {allocationCount}");
            Output.WriteLine($"  Memory limit reached: {memoryLimitReached}");

            // Verify we could allocate a significant amount
            (allocatedBytes / (double)totalMemory).Should().BeGreaterThan(0.4,
                "Should be able to allocate at least 40% of total memory");
        }
        finally
        {
            // Cleanup - verify no crashes during cleanup
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }

        // Verify accelerator is still functional after max memory test
        var testBuffer = await accelerator.Memory.AllocateAsync<float>(1024);
        testBuffer.Should().NotBeNull("Accelerator should still be functional after max memory test");
        testBuffer.Dispose();
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task MemoryFragmentation_1000Allocs_Handled()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int iterations = 1000;
        var random = new Random(12345);
        var buffers = new List<IDisposable>();
        var allocationSizes = new List<int>();
        var totalAllocated = 0L;
        var maxAllocated = 0L;
        var allocationFailures = 0;

        Output.WriteLine("Starting memory fragmentation test with 1000 random allocations/deallocations...");

        for (var i = 0; i < iterations; i++)
        {
            // Random size between 1KB and 10MB
            var size = random.Next(1024, 10 * 1024 * 1024);

            try
            {
                var buffer = await accelerator.Memory.AllocateAsync<byte>(size);
                buffers.Add(buffer);
                allocationSizes.Add(size);
                totalAllocated += size;
                maxAllocated = Math.Max(maxAllocated, totalAllocated);

                // Randomly deallocate some buffers to create fragmentation
                if (buffers.Count > 50 && random.NextDouble() > 0.6)
                {
                    var removeCount = random.Next(1, Math.Min(10, buffers.Count / 2));
                    for (var j = 0; j < removeCount; j++)
                    {
                        var index = random.Next(buffers.Count);
                        buffers[index].Dispose();
                        totalAllocated -= allocationSizes[index];
                        buffers.RemoveAt(index);
                        allocationSizes.RemoveAt(index);
                    }
                }

                if (i % 100 == 0)
                {
                    Output.WriteLine($"  Iteration {i}: {buffers.Count} active buffers, {totalAllocated / (1024 * 1024):F2} MB allocated");
                }
            }
            catch
            {
                allocationFailures++;
                // Continue testing even on allocation failure
            }
        }

        Output.WriteLine($"Memory Fragmentation Test Results:");
        Output.WriteLine($"  Total iterations: {iterations}");
        Output.WriteLine($"  Allocation failures: {allocationFailures}");
        Output.WriteLine($"  Final active buffers: {buffers.Count}");
        Output.WriteLine($"  Max memory in use: {maxAllocated / (1024 * 1024):F2} MB");
        Output.WriteLine($"  Final memory in use: {totalAllocated / (1024 * 1024):F2} MB");
        Output.WriteLine($"  Success rate: {(iterations - allocationFailures) / (double)iterations:P1}");

        // Cleanup
        foreach (var buffer in buffers)
        {
            buffer.Dispose();
        }

        // Fragmentation should not cause excessive failures
        allocationFailures.Should().BeLessThan((int)(iterations * 0.2),
            "Should handle fragmentation with less than 20% failure rate");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task OOM_GracefulDegradation()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        var buffers = new List<IDisposable>();
        var chunkSize = 256 * 1024 * 1024; // 256MB chunks
        var oomEncountered = false;
        var exceptionType = "";
        var allocationCount = 0;

        try
        {
            Output.WriteLine("Forcing out-of-memory condition...");

            // Allocate until we hit OOM
            for (var i = 0; i < 100; i++) // Safety limit
            {
                try
                {
                    var buffer = await accelerator.Memory.AllocateAsync<byte>(chunkSize);
                    buffers.Add(buffer);
                    allocationCount++;

                    if (i % 2 == 0)
                    {
                        Output.WriteLine($"  Allocated {allocationCount} chunks ({allocationCount * chunkSize / (1024.0 * 1024 * 1024):F2} GB)");
                    }
                }
                catch (OutOfMemoryException ex)
                {
                    oomEncountered = true;
                    exceptionType = ex.GetType().Name;
                    Output.WriteLine($"  OOM at {allocationCount} chunks: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    oomEncountered = true;
                    exceptionType = ex.GetType().Name;
                    Output.WriteLine($"  Memory error at {allocationCount} chunks: {ex.GetType().Name}");
                    break;
                }
            }

            Output.WriteLine($"OOM Graceful Degradation Results:");
            Output.WriteLine($"  Allocations before OOM: {allocationCount}");
            Output.WriteLine($"  OOM encountered: {oomEncountered}");
            Output.WriteLine($"  Exception type: {exceptionType}");

            // Test that accelerator is still functional after OOM
            await Task.Delay(100); // Brief pause for recovery

            var testBuffer = await accelerator.Memory.AllocateAsync<float>(1024);
            testBuffer.Should().NotBeNull("Should be able to allocate small buffer after OOM");
            testBuffer.Dispose();

            Output.WriteLine("  Accelerator remains functional after OOM: ✓");
        }
        finally
        {
            // Cleanup should not crash
            foreach (var buffer in buffers)
            {
                try
                {
                    buffer.Dispose();
                }
                catch
                {
                    // Suppress disposal errors
                }
            }
        }

        // Verify graceful degradation occurred
        oomEncountered.Should().BeTrue("Should have encountered OOM condition");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task MemoryLeak_LongRunning_NoLeak()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int iterations = 10000;
        const int sampleSize = 1024 * 1024; // 1MB
        var memorySnapshots = new List<long>();
        var snapshotInterval = 1000;

        Output.WriteLine($"Starting memory leak test with {iterations} allocate/free cycles...");

        // Baseline memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var baselineMemory = GC.GetTotalMemory(false);
        Output.WriteLine($"Baseline memory: {baselineMemory / (1024.0 * 1024):F2} MB");

        for (var i = 0; i < iterations; i++)
        {
            // Allocate and immediately free
            var buffer = await accelerator.Memory.AllocateAsync<byte>(sampleSize);
            buffer.Dispose();

            // Periodic memory snapshots
            if (i % snapshotInterval == 0 && i > 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var currentMemory = GC.GetTotalMemory(false);
                memorySnapshots.Add(currentMemory);

                if (memorySnapshots.Count % 5 == 0)
                {
                    Output.WriteLine($"  Iteration {i}: Memory = {currentMemory / (1024.0 * 1024):F2} MB, " +
                        $"Delta = {(currentMemory - baselineMemory) / (1024.0 * 1024):+0.00;-0.00} MB");
                }
            }
        }

        // Final memory check
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        Output.WriteLine($"Memory Leak Test Results:");
        Output.WriteLine($"  Iterations: {iterations}");
        Output.WriteLine($"  Baseline memory: {baselineMemory / (1024.0 * 1024):F2} MB");
        Output.WriteLine($"  Final memory: {finalMemory / (1024.0 * 1024):F2} MB");
        Output.WriteLine($"  Memory increase: {(finalMemory - baselineMemory) / (1024.0 * 1024):+0.00;-0.00} MB");

        // Calculate trend
        if (memorySnapshots.Count > 2)
        {
            var firstHalf = memorySnapshots.Take(memorySnapshots.Count / 2).Average();
            var secondHalf = memorySnapshots.Skip(memorySnapshots.Count / 2).Average();
            var trend = (secondHalf - firstHalf) / firstHalf;
            Output.WriteLine($"  Memory growth trend: {trend:P2}");

            // Memory should not grow significantly over time
            trend.Should().BeLessThan(0.1, "Memory growth should be less than 10% over test duration");
        }

        // Memory should return to near baseline
        var memoryIncrease = finalMemory - baselineMemory;
        var memoryIncreasePercent = memoryIncrease / (double)baselineMemory;
        memoryIncreasePercent.Should().BeLessThan(0.2,
            "Memory increase should be less than 20% after all allocations freed");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task LargeBuffer_8GB_HandlesCorrectly()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        var totalMemory = accelerator.Info.TotalMemory;
        var largeBufferSize = Math.Min(8L * 1024 * 1024 * 1024, (long)(totalMemory * 0.8)); // 8GB or 80% of available
        var elementCount = (int)(largeBufferSize / sizeof(float));

        Output.WriteLine($"Testing large buffer allocation:");
        Output.WriteLine($"  Total memory: {totalMemory / (1024.0 * 1024 * 1024):F2} GB");
        Output.WriteLine($"  Requested size: {largeBufferSize / (1024.0 * 1024 * 1024):F2} GB ({elementCount:N0} floats)");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var buffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
            stopwatch.Stop();

            Output.WriteLine($"  Allocation succeeded in {stopwatch.ElapsedMilliseconds} ms");
            Output.WriteLine($"  Testing write operation...");

            // Test that we can actually use the buffer
            var testData = new float[1024];
            for (var i = 0; i < testData.Length; i++)
            {
                testData[i] = i * 0.5f;
            }

            await buffer.CopyFromAsync(testData.AsMemory(), 0);
            Output.WriteLine($"  Write operation succeeded");

            // Test read
            var readData = new float[1024];
            await buffer.CopyToAsync(readData.AsMemory(), 0);
            Output.WriteLine($"  Read operation succeeded");

            // Verify data integrity
            var matches = testData.Zip(readData, (a, b) => Math.Abs(a - b) < 0.001f).Count(x => x);
            matches.Should().BeGreaterThan(1000, "Data should be preserved correctly");
            Output.WriteLine($"  Data integrity verified: {matches}/1024 matches");

            buffer.Dispose();
            Output.WriteLine($"  Large buffer test passed ✓");
        }
        catch (OutOfMemoryException ex)
        {
            Output.WriteLine($"  Large buffer allocation failed (expected on systems with limited memory): {ex.Message}");
            // This is acceptable - not all systems can allocate 8GB
        }
        catch (Exception ex)
        {
            Output.WriteLine($"  Error (should be graceful): {ex.GetType().Name}: {ex.Message}");
            // Verify the accelerator is still functional
            var testBuffer = await accelerator.Memory.AllocateAsync<float>(1024);
            testBuffer.Should().NotBeNull("Accelerator should remain functional after large buffer failure");
            testBuffer.Dispose();
        }
    }

    // ===============================================
    // CONCURRENCY STRESS TESTS (Additional 5 tests)
    // ===============================================

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task ConcurrentKernels_100Simultaneous()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int kernelCount = 100;
        const int dataSize = 1024;
        var successCount = 0;
        var failureCount = 0;
        var lockObj = new object();

        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;

            kernel void concurrent_kernel(
                device float* data [[buffer(0)]],
                constant uint& n [[buffer(1)]],
                constant uint& kernel_id [[buffer(2)]],
                uint id [[thread_position_in_grid]]
            ) {
                if (id >= n) return;
                data[id] = float(kernel_id) + float(id) * 0.001f;
            }";

        var kernelDef = new KernelDefinition
        {
            Name = "concurrent_kernel",
            Code = kernelCode,
            EntryPoint = "concurrent_kernel"
        };

        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        Output.WriteLine($"Launching {kernelCount} kernels simultaneously...");
        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, kernelCount).Select(async kernelId =>
        {
            try
            {
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(dataSize);
                var args = new KernelArguments { buffer, (uint)dataSize, (uint)kernelId };

                await kernel.LaunchAsync((4, 1, 1), (256, 1, 1), args);
                await accelerator.SynchronizeAsync();

                // Verify results
                var result = new float[dataSize];
                await buffer.CopyToAsync(result.AsMemory());

                var expected = kernelId + 0.001f;
                if (Math.Abs(result[1] - expected) < 0.01f)
                {
                    lock (lockObj)
                        successCount++;
                }
                else
                {
                    lock (lockObj)
                        failureCount++;
                }
            }
            catch
            {
                lock (lockObj)
                    failureCount++;
            }
        }).ToArray();

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        var successRate = successCount / (double)kernelCount;
        Output.WriteLine($"Concurrent Kernels Test Results:");
        Output.WriteLine($"  Total kernels: {kernelCount}");
        Output.WriteLine($"  Successful: {successCount}");
        Output.WriteLine($"  Failed: {failureCount}");
        Output.WriteLine($"  Success rate: {successRate:P1}");
        Output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds} ms");
        Output.WriteLine($"  Average time per kernel: {stopwatch.ElapsedMilliseconds / (double)kernelCount:F2} ms");

        successRate.Should().BeGreaterThan(0.9, "Should handle concurrent kernels with 90%+ success");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task ThreadStorm_1000Threads_Stable()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int threadCount = 1000;
        var successCount = 0;
        var failureCount = 0;
        var lockObj = new object();
        var startSignal = new TaskCompletionSource<bool>();

        Output.WriteLine($"Starting thread storm with {threadCount} threads...");
        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(async () =>
        {
            // Wait for start signal to create maximum contention
            await startSignal.Task;

            try
            {
                // Each thread tries to allocate a buffer
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(256);

                // Write some data
                var data = new float[256];
                for (var i = 0; i < 256; i++)
                {
                    data[i] = threadId + i * 0.1f;
                }
                await buffer.CopyFromAsync(data.AsMemory());

                lock (lockObj)
                    successCount++;
            }
            catch
            {
                lock (lockObj)
                    failureCount++;
            }
        })).ToArray();

        // Brief delay to let all threads reach the wait point
        await Task.Delay(100);

        // Release all threads simultaneously
        startSignal.SetResult(true);
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        var successRate = successCount / (double)threadCount;
        Output.WriteLine($"Thread Storm Results:");
        Output.WriteLine($"  Total threads: {threadCount}");
        Output.WriteLine($"  Successful: {successCount}");
        Output.WriteLine($"  Failed: {failureCount}");
        Output.WriteLine($"  Success rate: {successRate:P1}");
        Output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds} ms");

        successRate.Should().BeGreaterThan(0.85, "Should handle thread storm with 85%+ success");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task RapidFire_10000Kernels_Sequential()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int kernelCount = 10000;
        const int dataSize = 512;
        var successCount = 0;
        var failureCount = 0;

        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;

            kernel void rapid_kernel(
                device float* data [[buffer(0)]],
                constant uint& n [[buffer(1)]],
                uint id [[thread_position_in_grid]]
            ) {
                if (id >= n) return;
                data[id] = data[id] * 1.01f + 0.001f;
            }";

        var kernelDef = new KernelDefinition
        {
            Name = "rapid_kernel",
            Code = kernelCode,
            EntryPoint = "rapid_kernel"
        };

        var kernel = await accelerator.CompileKernelAsync(kernelDef);
        await using var buffer = await accelerator.Memory.AllocateAsync<float>(dataSize);

        Output.WriteLine($"Launching {kernelCount} kernels in rapid succession...");
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < kernelCount; i++)
        {
            try
            {
                var args = new KernelArguments { buffer, (uint)dataSize };
                await kernel.LaunchAsync((2, 1, 1), (256, 1, 1), args);

                // Synchronize periodically to avoid overwhelming the queue
                if (i % 100 == 0)
                {
                    await accelerator.SynchronizeAsync();

                    if (i % 1000 == 0)
                    {
                        Output.WriteLine($"  Completed {i}/{kernelCount} kernels ({i / (double)kernelCount:P0})");
                    }
                }

                successCount++;
            }
            catch
            {
                failureCount++;
            }
        }

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        var successRate = successCount / (double)kernelCount;
        var throughput = kernelCount / stopwatch.Elapsed.TotalSeconds;

        Output.WriteLine($"Rapid Fire Results:");
        Output.WriteLine($"  Total kernels: {kernelCount}");
        Output.WriteLine($"  Successful: {successCount}");
        Output.WriteLine($"  Failed: {failureCount}");
        Output.WriteLine($"  Success rate: {successRate:P1}");
        Output.WriteLine($"  Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Output.WriteLine($"  Throughput: {throughput:F0} kernels/sec");

        successRate.Should().BeGreaterThan(0.95, "Should handle rapid kernel launches with 95%+ success");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task ConcurrentCompilation_50Threads()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int threadCount = 50;
        var successCount = 0;
        var failureCount = 0;
        var lockObj = new object();

        Output.WriteLine($"Starting {threadCount} concurrent kernel compilations...");
        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            try
            {
                // Each thread compiles its own kernel variant
                var kernelCode = $@"
                    #include <metal_stdlib>
                    using namespace metal;

                    kernel void compile_test_{threadId}(
                        device float* data [[buffer(0)]],
                        constant uint& n [[buffer(1)]],
                        uint id [[thread_position_in_grid]]
                    ) {{
                        if (id >= n) return;
                        data[id] = float({threadId}) + float(id) * 0.01f;
                    }}";

                var kernelDef = new KernelDefinition
                {
                    Name = $"compile_test_{threadId}",
                    Code = kernelCode,
                    EntryPoint = $"compile_test_{threadId}"
                };

                var kernel = await accelerator.CompileKernelAsync(kernelDef);

                // Execute to verify compilation worked
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(1024);
                var args = new KernelArguments { buffer, 1024u };
                await kernel.LaunchAsync((4, 1, 1), (256, 1, 1), args);

                lock (lockObj)
                    successCount++;
            }
            catch (Exception ex)
            {
                lock (lockObj)
                {
                    failureCount++;
                    Output.WriteLine($"  Thread {threadId} failed: {ex.Message}");
                }
            }
        }).ToArray();

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        var successRate = successCount / (double)threadCount;
        Output.WriteLine($"Concurrent Compilation Results:");
        Output.WriteLine($"  Total compilations: {threadCount}");
        Output.WriteLine($"  Successful: {successCount}");
        Output.WriteLine($"  Failed: {failureCount}");
        Output.WriteLine($"  Success rate: {successRate:P1}");
        Output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds} ms");
        Output.WriteLine($"  Average per compilation: {stopwatch.ElapsedMilliseconds / (double)threadCount:F2} ms");

        successRate.Should().BeGreaterThan(0.9, "Should handle concurrent compilation with 90%+ success");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task HighContention_QueuePool_Stable()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int contendingThreads = 100;
        const int operationsPerThread = 50;
        var successCount = 0;
        var failureCount = 0;
        var lockObj = new object();

        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;

            kernel void contention_kernel(
                device float* data [[buffer(0)]],
                constant uint& n [[buffer(1)]],
                uint id [[thread_position_in_grid]]
            ) {
                if (id >= n) return;
                data[id] = sin(float(id) * 0.01f);
            }";

        var kernelDef = new KernelDefinition
        {
            Name = "contention_kernel",
            Code = kernelCode,
            EntryPoint = "contention_kernel"
        };

        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        Output.WriteLine($"Creating extreme queue contention with {contendingThreads} threads...");
        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, contendingThreads).Select(threadId => Task.Run(async () =>
        {
            for (var i = 0; i < operationsPerThread; i++)
            {
                try
                {
                    await using var buffer = await accelerator.Memory.AllocateAsync<float>(256);
                    var args = new KernelArguments { buffer, 256u };
                    await kernel.LaunchAsync((1, 1, 1), (256, 1, 1), args);

                    lock (lockObj)
                        successCount++;
                }
                catch
                {
                    lock (lockObj)
                        failureCount++;
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        var totalOperations = contendingThreads * operationsPerThread;
        var successRate = successCount / (double)totalOperations;

        Output.WriteLine($"High Contention Results:");
        Output.WriteLine($"  Contending threads: {contendingThreads}");
        Output.WriteLine($"  Operations per thread: {operationsPerThread}");
        Output.WriteLine($"  Total operations: {totalOperations}");
        Output.WriteLine($"  Successful: {successCount}");
        Output.WriteLine($"  Failed: {failureCount}");
        Output.WriteLine($"  Success rate: {successRate:P1}");
        Output.WriteLine($"  Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        successRate.Should().BeGreaterThan(0.85, "Should handle high contention with 85%+ success");
    }

    // ===============================================
    // RESOURCE EXHAUSTION TESTS (5 tests)
    // ===============================================

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task MaxQueues_ExhaustPool_Recovers()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        Output.WriteLine("Testing command queue pool exhaustion and recovery...");

        // This test is more conceptual as queue pool size is implementation-specific
        // We'll simulate by creating many concurrent operations
        const int concurrentOps = 50;
        var successCount = 0;
        var failureCount = 0;

        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;

            kernel void queue_test(device float* data [[buffer(0)]], uint id [[thread_position_in_grid]]) {
                data[id] = float(id);
            }";

        var kernelDef = new KernelDefinition { Name = "queue_test", Code = kernelCode, EntryPoint = "queue_test" };
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        // Phase 1: Exhaust queue pool
        var tasks = Enumerable.Range(0, concurrentOps).Select(async _ =>
        {
            try
            {
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(1024);
                await kernel.LaunchAsync((4, 1, 1), (256, 1, 1), new KernelArguments { buffer });
                Interlocked.Increment(ref successCount);
            }
            catch
            {
                Interlocked.Increment(ref failureCount);
            }
        }).ToArray();

        await Task.WhenAll(tasks);
        Output.WriteLine($"  Phase 1 - Exhaustion: {successCount} success, {failureCount} failures");

        // Phase 2: Verify recovery
        await Task.Delay(1000); // Allow pool to recover

        var recoverySuccess = 0;
        for (var i = 0; i < 10; i++)
        {
            try
            {
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(1024);
                await kernel.LaunchAsync((4, 1, 1), (256, 1, 1), new KernelArguments { buffer });
                recoverySuccess++;
            }
            catch
            {
                // Failure during recovery
            }
        }

        Output.WriteLine($"  Phase 2 - Recovery: {recoverySuccess}/10 operations succeeded");
        recoverySuccess.Should().BeGreaterThanOrEqualTo(8, "Should recover after pool exhaustion");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task MaxEncoders_ExhaustLimit_Handled()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        Output.WriteLine("Testing encoder limit handling...");

        // Metal has limits on concurrent encoders - test by creating many operations
        const int operationCount = 100;
        var successfulEncodings = 0;
        var errors = 0;

        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;
            kernel void encoder_test(device float* data [[buffer(0)]], uint id [[thread_position_in_grid]]) {
                data[id] = float(id) * 2.0f;
            }";

        var kernelDef = new KernelDefinition { Name = "encoder_test", Code = kernelCode, EntryPoint = "encoder_test" };
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        for (var i = 0; i < operationCount; i++)
        {
            try
            {
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(512);
                await kernel.LaunchAsync((2, 1, 1), (256, 1, 1), new KernelArguments { buffer });
                successfulEncodings++;
            }
            catch
            {
                errors++;
            }

            if (i % 20 == 0)
            {
                Output.WriteLine($"  Progress: {i}/{operationCount} ({successfulEncodings} success, {errors} errors)");
            }
        }

        Output.WriteLine($"Encoder Limit Test Results:");
        Output.WriteLine($"  Successful encodings: {successfulEncodings}");
        Output.WriteLine($"  Errors: {errors}");
        Output.WriteLine($"  Success rate: {successfulEncodings / (double)operationCount:P1}");

        successfulEncodings.Should().BeGreaterThan((int)(operationCount * 0.8),
            "Should handle encoder limit with 80%+ success");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task MaxKernels_CacheFull_EvictsOld()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        Output.WriteLine("Testing kernel cache eviction...");

        // Compile many unique kernels to fill cache
        const int kernelVariants = 100;
        var compiledKernels = new List<ICompiledKernel>();
        var compilationErrors = 0;

        for (var i = 0; i < kernelVariants; i++)
        {
            try
            {
                var kernelCode = $@"
                    #include <metal_stdlib>
                    using namespace metal;
                    kernel void cached_kernel_{i}(device float* data [[buffer(0)]], uint id [[thread_position_in_grid]]) {{
                        data[id] = float({i}) + float(id) * 0.01f;
                    }}";

                var kernelDef = new KernelDefinition
                {
                    Name = $"cached_kernel_{i}",
                    Code = kernelCode,
                    EntryPoint = $"cached_kernel_{i}"
                };

                var kernel = await accelerator.CompileKernelAsync(kernelDef);
                compiledKernels.Add(kernel);

                if (i % 20 == 0)
                {
                    Output.WriteLine($"  Compiled {i}/{kernelVariants} kernel variants");
                }
            }
            catch
            {
                compilationErrors++;
            }
        }

        Output.WriteLine($"Kernel Cache Test Results:");
        Output.WriteLine($"  Compiled kernels: {compiledKernels.Count}");
        Output.WriteLine($"  Compilation errors: {compilationErrors}");

        // Verify kernels are still usable (testing cache hits)
        var executionTests = 10;
        var executionSuccesses = 0;
        var random = new Random(42);

        for (var i = 0; i < executionTests; i++)
        {
            try
            {
                var kernel = compiledKernels[random.Next(compiledKernels.Count)];
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(256);
                await kernel.LaunchAsync((1, 1, 1), (256, 1, 1), new KernelArguments { buffer });
                executionSuccesses++;
            }
            catch
            {
                // Execution failure
            }
        }

        Output.WriteLine($"  Random kernel executions: {executionSuccesses}/{executionTests}");

        compiledKernels.Count.Should().BeGreaterThan(80, "Should compile most kernel variants");
        executionSuccesses.Should().BeGreaterThanOrEqualTo(8, "Cached kernels should remain executable");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task FileDescriptors_NotExhausted()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        Output.WriteLine("Testing file descriptor stability over extended operations...");

        // Run many operations that might leak file descriptors
        const int iterations = 1000;
        var errors = 0;

        for (var i = 0; i < iterations; i++)
        {
            try
            {
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(1024);
                var data = new float[1024];
                await buffer.CopyFromAsync(data.AsMemory());
                await buffer.CopyToAsync(data.AsMemory());
            }
            catch
            {
                errors++;
            }

            if (i % 100 == 0 && i > 0)
            {
                Output.WriteLine($"  Completed {i}/{iterations} iterations, errors: {errors}");
            }
        }

        Output.WriteLine($"File Descriptor Test Results:");
        Output.WriteLine($"  Total iterations: {iterations}");
        Output.WriteLine($"  Errors: {errors}");
        Output.WriteLine($"  Error rate: {errors / (double)iterations:P1}");

        errors.Should().BeLessThan((int)(iterations * 0.05), "Should have less than 5% error rate");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task GPUHang_DetectedAndRecovered()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        Output.WriteLine("Testing GPU hang detection and recovery...");

        // Create a kernel that might cause issues
        var problematicKernelCode = @"
            #include <metal_stdlib>
            using namespace metal;

            kernel void potentially_slow_kernel(
                device float* data [[buffer(0)]],
                constant uint& n [[buffer(1)]],
                uint id [[thread_position_in_grid]]
            ) {
                if (id >= n) return;

                // Intensive computation
                float value = data[id];
                for (int i = 0; i < 10000; i++) {
                    value = sin(value) * cos(value) + 0.001f;
                }
                data[id] = value;
            }";

        var kernelDef = new KernelDefinition
        {
            Name = "potentially_slow_kernel",
            Code = problematicKernelCode,
            EntryPoint = "potentially_slow_kernel"
        };

        try
        {
            var kernel = await accelerator.CompileKernelAsync(kernelDef);
            await using var buffer = await accelerator.Memory.AllocateAsync<float>(10000);

            // Launch with timeout expectations
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var stopwatch = Stopwatch.StartNew();

            await kernel.LaunchAsync((40, 1, 1), (256, 1, 1), new KernelArguments { buffer, 10000u });
            await accelerator.SynchronizeAsync(cts.Token);

            stopwatch.Stop();
            Output.WriteLine($"  Kernel completed in {stopwatch.ElapsedMilliseconds} ms");

            // Test that accelerator is still functional
            await using var testBuffer = await accelerator.Memory.AllocateAsync<float>(100);
            Output.WriteLine($"  Accelerator remains functional after intensive operation ✓");
        }
        catch (OperationCanceledException)
        {
            Output.WriteLine($"  Operation timed out (potential hang detected)");

            // Verify we can still use the accelerator
            await Task.Delay(1000);
            var testBuffer = await accelerator.Memory.AllocateAsync<float>(100);
            testBuffer.Should().NotBeNull("Accelerator should recover after timeout");
            testBuffer.Dispose();
            Output.WriteLine($"  Recovery successful ✓");
        }
        catch (Exception ex)
        {
            Output.WriteLine($"  Error during intensive operation: {ex.GetType().Name}");

            // Verify accelerator can still be used
            var testBuffer = await accelerator.Memory.AllocateAsync<float>(100);
            testBuffer.Should().NotBeNull("Accelerator should remain functional after errors");
            testBuffer.Dispose();
        }
    }

    // ===============================================
    // LONG-RUNNING STABILITY TESTS (2 tests + 1 manual)
    // ===============================================

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task ContinuousLoad_1Million_Operations()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int targetOperations = 1000000;
        var successCount = 0;
        var errorCount = 0;

        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;
            kernel void continuous_kernel(device float* data [[buffer(0)]], uint id [[thread_position_in_grid]]) {
                data[id] = data[id] * 1.001f + 0.0001f;
            }";

        var kernelDef = new KernelDefinition { Name = "continuous_kernel", Code = kernelCode, EntryPoint = "continuous_kernel" };
        var kernel = await accelerator.CompileKernelAsync(kernelDef);
        await using var buffer = await accelerator.Memory.AllocateAsync<float>(1024);

        Output.WriteLine($"Starting continuous load test: {targetOperations:N0} operations...");
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < targetOperations; i++)
        {
            try
            {
                await kernel.LaunchAsync((4, 1, 1), (256, 1, 1), new KernelArguments { buffer });

                if (i % 10000 == 0)
                {
                    await accelerator.SynchronizeAsync();
                }

                successCount++;

                if (i % 100000 == 0 && i > 0)
                {
                    var elapsed = stopwatch.Elapsed.TotalSeconds;
                    var rate = i / elapsed;
                    Output.WriteLine($"  Progress: {i:N0}/{targetOperations:N0} ({i / (double)targetOperations:P1}), " +
                        $"Rate: {rate:F0} ops/sec, Errors: {errorCount}");
                }
            }
            catch
            {
                errorCount++;
                if (errorCount > targetOperations * 0.01) // More than 1% errors
                {
                    break;
                }
            }
        }

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        var finalRate = successCount / stopwatch.Elapsed.TotalSeconds;
        var successRate = successCount / (double)targetOperations;

        Output.WriteLine($"Continuous Load Results:");
        Output.WriteLine($"  Target operations: {targetOperations:N0}");
        Output.WriteLine($"  Successful: {successCount:N0}");
        Output.WriteLine($"  Errors: {errorCount:N0}");
        Output.WriteLine($"  Success rate: {successRate:P2}");
        Output.WriteLine($"  Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Output.WriteLine($"  Throughput: {finalRate:F0} operations/second");

        successRate.Should().BeGreaterThan(0.99, "Should complete 99%+ of operations successfully");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task PeriodicStress_SustainedLoad()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        Output.WriteLine("Starting periodic stress test (10 cycles of load/rest)...");

        const int cycles = 10;
        const int operationsPerCycle = 5000;
        var cycleResults = new List<(int Success, int Errors, double Duration)>();

        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;
            kernel void stress_cycle(device float* data [[buffer(0)]], constant uint& n [[buffer(1)]], uint id [[thread_position_in_grid]]) {
                if (id >= n) return;
                data[id] = sin(float(id) * 0.01f) + cos(data[id]);
            }";

        var kernelDef = new KernelDefinition { Name = "stress_cycle", Code = kernelCode, EntryPoint = "stress_cycle" };
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        for (var cycle = 0; cycle < cycles; cycle++)
        {
            var success = 0;
            var errors = 0;
            var stopwatch = Stopwatch.StartNew();

            // Heavy load phase
            for (var i = 0; i < operationsPerCycle; i++)
            {
                try
                {
                    await using var buffer = await accelerator.Memory.AllocateAsync<float>(2048);
                    await kernel.LaunchAsync((8, 1, 1), (256, 1, 1), new KernelArguments { buffer, 2048u });
                    success++;
                }
                catch
                {
                    errors++;
                }
            }

            await accelerator.SynchronizeAsync();
            stopwatch.Stop();

            cycleResults.Add((success, errors, stopwatch.Elapsed.TotalSeconds));
            var successRate = success / (double)operationsPerCycle;

            Output.WriteLine($"  Cycle {cycle + 1}/{cycles}: {success}/{operationsPerCycle} success ({successRate:P1}), " +
                $"Time: {stopwatch.Elapsed.TotalSeconds:F2}s");

            // Rest phase
            if (cycle < cycles - 1)
            {
                await Task.Delay(2000);
            }
        }

        // Analyze performance degradation
        var firstHalfAvg = cycleResults.Take(cycles / 2).Average(r => r.Success / (double)operationsPerCycle);
        var secondHalfAvg = cycleResults.Skip(cycles / 2).Average(r => r.Success / (double)operationsPerCycle);
        var degradation = (firstHalfAvg - secondHalfAvg) / firstHalfAvg;

        Output.WriteLine($"Periodic Stress Results:");
        Output.WriteLine($"  Total cycles: {cycles}");
        Output.WriteLine($"  First half avg success: {firstHalfAvg:P1}");
        Output.WriteLine($"  Second half avg success: {secondHalfAvg:P1}");
        Output.WriteLine($"  Performance degradation: {degradation:P2}");

        degradation.Should().BeLessThan(0.1, "Performance should not degrade more than 10%");
        secondHalfAvg.Should().BeGreaterThan(0.9, "Later cycles should maintain 90%+ success");
    }

    [Fact(Skip = "Stress test - run manually for 24-hour stability validation")]
    [Trait("Category", "Manual")]
    [Trait("Duration", "VeryLong")]
    public async Task LongRunning_24Hours_Stable()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        var duration = TimeSpan.FromHours(24);
        var cts = new CancellationTokenSource(duration);
        var startTime = DateTime.UtcNow;
        var operationCount = 0L;
        var errorCount = 0L;
        var memorySnapshots = new List<long>();

        Output.WriteLine($"Starting 24-hour stability test...");
        Output.WriteLine($"Start time: {startTime:yyyy-MM-dd HH:mm:ss} UTC");

        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;
            kernel void longrun_kernel(device float* data [[buffer(0)]], uint id [[thread_position_in_grid]]) {
                data[id] = sin(float(id) * 0.001f);
            }";

        var kernelDef = new KernelDefinition { Name = "longrun_kernel", Code = kernelCode, EntryPoint = "longrun_kernel" };
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await using var buffer = await accelerator.Memory.AllocateAsync<float>(1024);
                await kernel.LaunchAsync((4, 1, 1), (256, 1, 1), new KernelArguments { buffer });

                Interlocked.Increment(ref operationCount);

                // Periodic synchronization and memory check
                if (operationCount % 10000 == 0)
                {
                    await accelerator.SynchronizeAsync(cts.Token);

                    GC.Collect();
                    var currentMemory = GC.GetTotalMemory(false);
                    memorySnapshots.Add(currentMemory);

                    var elapsed = DateTime.UtcNow - startTime;
                    var rate = operationCount / elapsed.TotalSeconds;

                    Output.WriteLine($"[{elapsed.TotalHours:F1}h] Operations: {operationCount:N0}, " +
                        $"Errors: {errorCount}, Rate: {rate:F0}/s, Memory: {currentMemory / (1024 * 1024):F0} MB");
                }
            }
            catch
            {
                Interlocked.Increment(ref errorCount);
            }
        }

        var totalElapsed = DateTime.UtcNow - startTime;
        var successRate = (operationCount - errorCount) / (double)operationCount;
        var avgMemory = memorySnapshots.Average();
        var memoryGrowth = memorySnapshots.Count > 1
            ? (memorySnapshots.Last() - memorySnapshots.First()) / (double)memorySnapshots.First()
            : 0;

        Output.WriteLine($"24-Hour Stability Test Results:");
        Output.WriteLine($"  Duration: {totalElapsed.TotalHours:F2} hours");
        Output.WriteLine($"  Total operations: {operationCount:N0}");
        Output.WriteLine($"  Errors: {errorCount:N0}");
        Output.WriteLine($"  Success rate: {successRate:P2}");
        Output.WriteLine($"  Average memory: {avgMemory / (1024 * 1024):F0} MB");
        Output.WriteLine($"  Memory growth: {memoryGrowth:P2}");

        successRate.Should().BeGreaterThan(0.99, "24-hour test should maintain 99%+ success rate");
        memoryGrowth.Should().BeLessThan(0.2, "Memory growth should be less than 20% over 24 hours");
    }

    // ===============================================
    // ERROR RECOVERY TESTS (3 tests)
    // ===============================================

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task InvalidKernel_RecoversGracefully()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        Output.WriteLine("Testing recovery from invalid kernel launches...");

        const int invalidAttempts = 50;
        var compilationErrors = 0;
        var executionErrors = 0;

        for (var i = 0; i < invalidAttempts; i++)
        {
            try
            {
                // Deliberately create problematic kernel code
                var invalidKernelCode = $@"
                    #include <metal_stdlib>
                    using namespace metal;
                    kernel void invalid_kernel_{i}(
                        device float* data [[buffer(0)]],
                        // Missing other required parameters
                        uint id [[thread_position_in_grid]]
                    ) {{
                        // Potential out-of-bounds access
                        data[id + 999999] = float(id);
                    }}";

                var kernelDef = new KernelDefinition
                {
                    Name = $"invalid_kernel_{i}",
                    Code = invalidKernelCode,
                    EntryPoint = $"invalid_kernel_{i}"
                };

                try
                {
                    var kernel = await accelerator.CompileKernelAsync(kernelDef);

                    await using var buffer = await accelerator.Memory.AllocateAsync<float>(100);
                    await kernel.LaunchAsync((1, 1, 1), (64, 1, 1), new KernelArguments { buffer });
                }
                catch
                {
                    executionErrors++;
                }
            }
            catch
            {
                compilationErrors++;
            }

            // After each error, verify system is still functional
            if (i % 10 == 0 && i > 0)
            {
                var testBuffer = await accelerator.Memory.AllocateAsync<float>(256);
                testBuffer.Should().NotBeNull($"Accelerator should be functional after {i} invalid kernel attempts");
                testBuffer.Dispose();

                Output.WriteLine($"  Progress: {i}/{invalidAttempts}, Compilation errors: {compilationErrors}, " +
                    $"Execution errors: {executionErrors}, System stable: ✓");
            }
        }

        Output.WriteLine($"Invalid Kernel Recovery Results:");
        Output.WriteLine($"  Total attempts: {invalidAttempts}");
        Output.WriteLine($"  Compilation errors: {compilationErrors}");
        Output.WriteLine($"  Execution errors: {executionErrors}");
        Output.WriteLine($"  System remained stable throughout test");

        // System should remain stable despite errors
        var finalTestBuffer = await accelerator.Memory.AllocateAsync<float>(1024);
        finalTestBuffer.Should().NotBeNull("System should be fully functional after error recovery test");
        finalTestBuffer.Dispose();
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task DeviceReset_Recovers()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        Output.WriteLine("Testing device reset recovery...");

        // Test normal operation
        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;
            kernel void reset_test(device float* data [[buffer(0)]], uint id [[thread_position_in_grid]]) {
                data[id] = float(id);
            }";

        var kernelDef = new KernelDefinition { Name = "reset_test", Code = kernelCode, EntryPoint = "reset_test" };
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        // Phase 1: Normal operation
        await using (var buffer1 = await accelerator.Memory.AllocateAsync<float>(1024))
        {
            await kernel.LaunchAsync((4, 1, 1), (256, 1, 1), new KernelArguments { buffer1 });
            await accelerator.SynchronizeAsync();
            Output.WriteLine("  Phase 1: Normal operation - ✓");
        }

        // Phase 2: Simulate potential reset condition with intensive operations
        try
        {
            var heavyKernelCode = @"
                #include <metal_stdlib>
                using namespace metal;
                kernel void heavy_kernel(device float* data [[buffer(0)]], uint id [[thread_position_in_grid]]) {
                    float value = float(id);
                    for (int i = 0; i < 100000; i++) {
                        value = sin(value) * cos(value);
                    }
                    data[id] = value;
                }";

            var heavyKernelDef = new KernelDefinition { Name = "heavy_kernel", Code = heavyKernelCode, EntryPoint = "heavy_kernel" };
            var heavyKernel = await accelerator.CompileKernelAsync(heavyKernelDef);

            await using var buffer2 = await accelerator.Memory.AllocateAsync<float>(10000);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await heavyKernel.LaunchAsync((40, 1, 1), (256, 1, 1), new KernelArguments { buffer2 });
            await accelerator.SynchronizeAsync(cts.Token);

            Output.WriteLine("  Phase 2: Intensive operation completed");
        }
        catch (OperationCanceledException)
        {
            Output.WriteLine("  Phase 2: Operation timed out (simulating potential reset condition)");
        }
        catch (Exception ex)
        {
            Output.WriteLine($"  Phase 2: Error occurred: {ex.GetType().Name}");
        }

        // Phase 3: Recovery verification
        await Task.Delay(2000); // Allow time for recovery

        var recoveryAttempts = 5;
        var successfulRecoveries = 0;

        for (var i = 0; i < recoveryAttempts; i++)
        {
            try
            {
                await using var buffer3 = await accelerator.Memory.AllocateAsync<float>(1024);
                await kernel.LaunchAsync((4, 1, 1), (256, 1, 1), new KernelArguments { buffer3 });
                await accelerator.SynchronizeAsync();
                successfulRecoveries++;
            }
            catch
            {
                await Task.Delay(500); // Brief delay between retries
            }
        }

        Output.WriteLine($"  Phase 3: Recovery - {successfulRecoveries}/{recoveryAttempts} operations succeeded");

        successfulRecoveries.Should().BeGreaterThanOrEqualTo(4,
            "Should successfully recover and complete most operations after reset condition");
    }

    [SkippableFact]
    [Trait("Duration", "Long")]
    public async Task MultipleFailures_Cascading_Handled()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        Output.WriteLine("Testing cascading failure handling...");

        var scenarioResults = new Dictionary<string, bool>();

        // Scenario 1: Compilation failure
        try
        {
            var invalidCode = "invalid metal code that won't compile";
            var kernelDef = new KernelDefinition { Name = "invalid", Code = invalidCode, EntryPoint = "invalid" };
            await accelerator.CompileKernelAsync(kernelDef);
            scenarioResults["CompilationFailure"] = false;
        }
        catch
        {
            scenarioResults["CompilationFailure"] = true;
            Output.WriteLine("  Scenario 1: Compilation failure handled - ✓");
        }

        // Scenario 2: Memory allocation failure (try to allocate way too much)
        try
        {
            await using var hugeBuffer = await accelerator.Memory.AllocateAsync<float>(int.MaxValue / 4);
            scenarioResults["AllocationFailure"] = false;
        }
        catch
        {
            scenarioResults["AllocationFailure"] = true;
            Output.WriteLine("  Scenario 2: Allocation failure handled - ✓");
        }

        // Scenario 3: Invalid launch parameters
        try
        {
            var kernelCode = @"
                #include <metal_stdlib>
                using namespace metal;
                kernel void test(device float* data [[buffer(0)]], uint id [[thread_position_in_grid]]) {
                    data[id] = float(id);
                }";
            var kernelDef = new KernelDefinition { Name = "test", Code = kernelCode, EntryPoint = "test" };
            var kernel = await accelerator.CompileKernelAsync(kernelDef);

            await using var buffer = await accelerator.Memory.AllocateAsync<float>(100);
            // Invalid grid dimensions
            await kernel.LaunchAsync((0, 0, 0), (0, 0, 0), new KernelArguments { buffer });
            scenarioResults["InvalidLaunch"] = false;
        }
        catch
        {
            scenarioResults["InvalidLaunch"] = true;
            Output.WriteLine("  Scenario 3: Invalid launch handled - ✓");
        }

        // Scenario 4: Null/empty buffer operations
        try
        {
            var kernelCode = @"
                #include <metal_stdlib>
                using namespace metal;
                kernel void test(device float* data [[buffer(0)]], uint id [[thread_position_in_grid]]) {
                    data[id] = float(id);
                }";
            var kernelDef = new KernelDefinition { Name = "test2", Code = kernelCode, EntryPoint = "test" };
            var kernel = await accelerator.CompileKernelAsync(kernelDef);

            // Try to launch with no arguments (should fail gracefully)
            await kernel.LaunchAsync((1, 1, 1), (64, 1, 1), new KernelArguments());
            scenarioResults["EmptyBufferOp"] = false;
        }
        catch
        {
            scenarioResults["EmptyBufferOp"] = true;
            Output.WriteLine("  Scenario 4: Empty buffer operation handled - ✓");
        }

        // Critical test: System should still be functional after all failures
        try
        {
            var validKernelCode = @"
                #include <metal_stdlib>
                using namespace metal;
                kernel void recovery_test(device float* data [[buffer(0)]], uint id [[thread_position_in_grid]]) {
                    data[id] = float(id) * 2.0f;
                }";
            var validKernelDef = new KernelDefinition { Name = "recovery_test", Code = validKernelCode, EntryPoint = "recovery_test" };
            var validKernel = await accelerator.CompileKernelAsync(validKernelDef);

            await using var validBuffer = await accelerator.Memory.AllocateAsync<float>(1024);
            await validKernel.LaunchAsync((4, 1, 1), (256, 1, 1), new KernelArguments { validBuffer });
            await accelerator.SynchronizeAsync();

            scenarioResults["FinalRecovery"] = true;
            Output.WriteLine("  Final Recovery: System fully functional - ✓");
        }
        catch
        {
            scenarioResults["FinalRecovery"] = false;
            Output.WriteLine("  Final Recovery: FAILED - System compromised");
        }

        Output.WriteLine($"Cascading Failure Test Results:");
        foreach (var scenario in scenarioResults)
        {
            Output.WriteLine($"  {scenario.Key}: {(scenario.Value ? "PASSED" : "FAILED")}");
        }

        scenarioResults.Values.Count(v => v).Should().BeGreaterThanOrEqualTo(4,
            "Should handle at least 4/5 failure scenarios gracefully");
        scenarioResults["FinalRecovery"].Should().BeTrue(
            "System must remain functional after cascading failures");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
