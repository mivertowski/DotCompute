// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Hardware.OpenCL.Tests.Helpers;
using Xunit.Abstractions;

namespace DotCompute.Hardware.OpenCL.Tests.Performance;

/// <summary>
/// Stress tests for OpenCL backend.
/// Tests system stability under heavy load, memory pressure, and extended runtime.
/// </summary>
[Trait("Category", "RequiresOpenCL")]
[Trait("Category", "Stress")]
public class OpenCLStressTests : OpenCLTestBase
{
    private const string StressKernel = @"
        __kernel void stress(__global const float* input, __global float* output, int n) {
            int idx = get_global_id(0);
            if (idx < n) {
                float x = input[idx];
                float result = x;
                for (int i = 0; i < 1000; i++) {
                    result = result * 1.01f - 0.001f;
                }
                output[idx] = result;
            }
        }";

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLStressTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public OpenCLStressTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Stress tests with many kernel launches.
    /// </summary>
    [SkippableFact]
    public async Task Stress_Many_Kernel_Launches()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 1024 * 1024;
        const int iterations = 1000;

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);

        var hostInput = new float[elementCount];
        Array.Fill(hostInput, 1.0f);
        await deviceInput.WriteAsync(hostInput.AsSpan(), 0);

        var kernelDef = new KernelDefinition("stress", StressKernel, "stress");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        const int workGroupSize = 256;
        var globalSize = ((elementCount + workGroupSize - 1) / workGroupSize) * workGroupSize;

        var launchConfig = new LaunchConfiguration
        {
            GlobalWorkSize = new Dim3(globalSize),
            LocalWorkSize = new Dim3(workGroupSize)
        };

        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            await kernel.LaunchAsync<float>(launchConfig, deviceInput, deviceOutput, elementCount);

            if (i % 100 == 0)
            {
                await accelerator.SynchronizeAsync();
                Output.WriteLine($"Completed {i} iterations...");
            }
        }

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        var avgTimePerLaunch = stopwatch.Elapsed.TotalMilliseconds / iterations;

        Output.WriteLine($"Stress Test - Many Launches:");
        Output.WriteLine($"  Total Iterations: {iterations:N0}");
        Output.WriteLine($"  Total Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Output.WriteLine($"  Average Time/Launch: {avgTimePerLaunch:F3} ms");

        avgTimePerLaunch.Should().BeLessThan(10, "Average launch time should remain reasonable");
    }

    /// <summary>
    /// Stress tests with many allocations and deallocations.
    /// </summary>
    [SkippableFact]
    public async Task Stress_Many_Allocations()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int iterations = 100;
        const int sizePerAllocation = 1024 * 1024;

        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            await using var buffer = await accelerator.Memory.AllocateAsync<float>(sizePerAllocation);

            var hostData = new float[100];
            await buffer.WriteAsync(hostData.AsSpan(), 0);

            if (i % 10 == 0)
            {
                Output.WriteLine($"Completed {i} allocations...");
            }
        }

        stopwatch.Stop();

        Output.WriteLine($"Stress Test - Many Allocations:");
        Output.WriteLine($"  Total Iterations: {iterations}");
        Output.WriteLine($"  Total Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Output.WriteLine($"  Average Time/Allocation: {stopwatch.Elapsed.TotalMilliseconds / iterations:F2} ms");

        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(30, "Allocations should complete in reasonable time");
    }

    /// <summary>
    /// Stress tests with large data transfers.
    /// </summary>
    [SkippableFact]
    public async Task Stress_Large_Data_Transfers()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 64 * 1024 * 1024; // 256 MB
        const int iterations = 20;

        var hostData = new float[elementCount];
        Array.Fill(hostData, 1.0f);

        await using var deviceBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);

        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            await deviceBuffer.WriteAsync(hostData.AsSpan(), 0);
            await deviceBuffer.ReadAsync(hostData.AsSpan(), 0);

            if (i % 5 == 0)
            {
                Output.WriteLine($"Completed {i} transfer cycles...");
            }
        }

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        var totalDataTransferred = elementCount * sizeof(float) * 2 * iterations / (1024.0 * 1024.0 * 1024.0);
        var bandwidth = totalDataTransferred / stopwatch.Elapsed.TotalSeconds;

        Output.WriteLine($"Stress Test - Large Data Transfers:");
        Output.WriteLine($"  Iterations: {iterations}");
        Output.WriteLine($"  Total Data: {totalDataTransferred:F2} GB");
        Output.WriteLine($"  Total Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Output.WriteLine($"  Average Bandwidth: {bandwidth:F2} GB/s");

        bandwidth.Should().BeGreaterThan(0.5, "Sustained bandwidth should be reasonable");
    }

    /// <summary>
    /// Stress tests with concurrent operations.
    /// </summary>
    [SkippableFact]
    public async Task Stress_Concurrent_Operations()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int numThreads = 4;
        const int operationsPerThread = 100;

        var kernelDef = new KernelDefinition("stress", StressKernel, "stress");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, numThreads).Select(async threadId =>
        {
            const int elementCount = 1024 * 1024;

            await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);

            var hostInput = new float[elementCount];
            Array.Fill(hostInput, threadId * 1.0f);
            await deviceInput.WriteAsync(hostInput.AsSpan(), 0);

            const int workGroupSize = 256;
            var globalSize = ((elementCount + workGroupSize - 1) / workGroupSize) * workGroupSize;

            var launchConfig = new LaunchConfiguration
            {
                GlobalWorkSize = new Dim3(globalSize),
                LocalWorkSize = new Dim3(workGroupSize)
            };

            for (var i = 0; i < operationsPerThread; i++)
            {
                await kernel.LaunchAsync<float>(launchConfig, deviceInput, deviceOutput, elementCount);

                if (i % 20 == 0)
                {
                    await accelerator.SynchronizeAsync();
                }
            }

            await accelerator.SynchronizeAsync();
            return threadId;
        });

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        results.Should().HaveCount(numThreads);

        Output.WriteLine($"Stress Test - Concurrent Operations:");
        Output.WriteLine($"  Threads: {numThreads}");
        Output.WriteLine($"  Operations/Thread: {operationsPerThread}");
        Output.WriteLine($"  Total Operations: {numThreads * operationsPerThread}");
        Output.WriteLine($"  Total Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(60, "Concurrent stress test should complete in reasonable time");
    }

    /// <summary>
    /// Stress tests memory pressure.
    /// </summary>
    [SkippableFact]
    public async Task Stress_Memory_Pressure()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var deviceInfo = accelerator.DeviceInfo;
        var availableMemory = deviceInfo?.GlobalMemorySize ?? (1024UL * 1024 * 1024);

        // Try to allocate 50% of available memory in chunks
        var targetMemory = (long)(availableMemory * 0.5);
        const int chunkSize = 16 * 1024 * 1024; // 64 MB chunks
        var numChunks = (int)(targetMemory / (chunkSize * sizeof(float)));

        var buffers = new List<IDisposable>();

        try
        {
            Output.WriteLine($"Allocating {numChunks} chunks of {chunkSize * sizeof(float) / (1024 * 1024):F0} MB...");

            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < numChunks; i++)
            {
                try
                {
                    var buffer = await accelerator.Memory.AllocateAsync<float>(chunkSize);
                    buffers.Add((IDisposable)buffer);

                    if ((i + 1) % 10 == 0)
                    {
                        var allocatedMB = (i + 1) * chunkSize * sizeof(float) / (1024 * 1024);
                        Output.WriteLine($"  Allocated {i + 1}/{numChunks} chunks ({allocatedMB} MB)...");
                    }
                }
                catch (OutOfMemoryException)
                {
                    Output.WriteLine($"  Ran out of memory after {i} chunks");
                    break;
                }
            }

            stopwatch.Stop();

            var totalAllocated = buffers.Count * chunkSize * sizeof(float) / (1024.0 * 1024.0 * 1024.0);

            Output.WriteLine($"Memory Pressure Stress Test:");
            Output.WriteLine($"  Available Memory: {availableMemory / (1024.0 * 1024.0 * 1024.0):F2} GB");
            Output.WriteLine($"  Allocated: {totalAllocated:F2} GB");
            Output.WriteLine($"  Chunks: {buffers.Count}");
            Output.WriteLine($"  Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

            buffers.Count.Should().BeGreaterThan(0, "Should be able to allocate at least some memory");
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                if (buffer is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else
                {
                    buffer.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Stress tests long-running computation.
    /// </summary>
    [SkippableFact]
    public async Task Stress_Long_Running_Computation()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 4 * 1024 * 1024;
        const int totalIterations = 500;

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);

        var hostInput = new float[elementCount];
        Array.Fill(hostInput, 1.0f);
        await deviceInput.WriteAsync(hostInput.AsSpan(), 0);

        var kernelDef = new KernelDefinition("stress", StressKernel, "stress");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        const int workGroupSize = 256;
        var globalSize = ((elementCount + workGroupSize - 1) / workGroupSize) * workGroupSize;

        var launchConfig = new LaunchConfiguration
        {
            GlobalWorkSize = new Dim3(globalSize),
            LocalWorkSize = new Dim3(workGroupSize)
        };

        var stopwatch = Stopwatch.StartNew();
        var checkpointTimes = new List<double>();

        for (var i = 0; i < totalIterations; i++)
        {
            await kernel.LaunchAsync<float>(launchConfig, deviceInput, deviceOutput, elementCount);

            if (i % 50 == 0)
            {
                await accelerator.SynchronizeAsync();
                checkpointTimes.Add(stopwatch.Elapsed.TotalSeconds);
                Output.WriteLine($"Checkpoint {i}/{totalIterations}: {stopwatch.Elapsed.TotalSeconds:F1}s");
            }
        }

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        Output.WriteLine($"Long Running Computation Stress Test:");
        Output.WriteLine($"  Total Iterations: {totalIterations}");
        Output.WriteLine($"  Total Time: {stopwatch.Elapsed.TotalMinutes:F2} minutes");
        Output.WriteLine($"  Average Time/Iteration: {stopwatch.Elapsed.TotalMilliseconds / totalIterations:F2} ms");

        // Verify results are still valid
        var resultData = new float[elementCount];
        await deviceOutput.ReadAsync(resultData.AsSpan(), 0);

        var validCount = resultData.Count(x => !float.IsNaN(x) && !float.IsInfinity(x));
        validCount.Should().Be(elementCount, "All results should be valid after long computation");

        Output.WriteLine($"  Valid Results: {validCount}/{elementCount}");
    }
}
