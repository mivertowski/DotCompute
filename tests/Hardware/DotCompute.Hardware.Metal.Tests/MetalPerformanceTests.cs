// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using FluentAssertions;

namespace DotCompute.Hardware.Metal.Tests;

/// <summary>
/// Performance benchmark tests for Metal operations.
/// Tests real-world performance scenarios and validates against expected performance metrics.
/// </summary>
[Trait("Category", "RequiresMetal")]
[Trait("Category", "Performance")]
[Trait("Category", "LongRunning")]
public class MetalPerformanceTests : MetalTestBase
{
    // Metal compute shaders for performance testing
    private const string MemoryBandwidthKernel = @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void memory_bandwidth_test(
            device float4* input [[buffer(0)]],
            device float4* output [[buffer(1)]],
            constant uint& n [[buffer(2)]],
            uint id [[thread_position_in_grid]]
        ) {
            if (id >= n) return;
            
            float4 data = input[id];
            // Simple computation to ensure memory access
            data.x = data.x * 1.1f + data.y;
            data.y = data.y * 1.1f + data.z;
            data.z = data.z * 1.1f + data.w;
            data.w = data.w * 1.1f + data.x;
            output[id] = data;
        }";

    private const string ComputeIntensiveKernel = @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void compute_intensive_test(
            device float* input [[buffer(0)]],
            device float* output [[buffer(1)]],
            constant uint& n [[buffer(2)]],
            uint id [[thread_position_in_grid]]
        ) {
            if (id >= n) return;
            
            float value = input[id];
            // Compute intensive operations
            for (int i = 0; i < 1000; i++) {
                value = sin(value) * cos(value) + sqrt(abs(value));
            }
            output[id] = value;
        }";

    private const string MatrixMultiplyOptimized = @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void matrix_multiply_optimized(
            device float* A [[buffer(0)]],
            device float* B [[buffer(1)]],
            device float* C [[buffer(2)]],
            constant uint& N [[buffer(3)]],
            uint2 gid [[thread_position_in_grid]],
            threadgroup float* shared_A [[threadgroup(0)]],
            threadgroup float* shared_B [[threadgroup(1)]]
        ) {
            const uint row = gid.y;
            const uint col = gid.x;
            const uint local_row = thread_position_in_threadgroup().y;
            const uint local_col = thread_position_in_threadgroup().x;
            const uint tile_size = 16;
            
            if (row >= N || col >= N) return;
            
            float sum = 0.0f;
            
            for (uint tile = 0; tile < (N + tile_size - 1) / tile_size; tile++) {
                // Load tile into shared memory
                if (row < N && tile * tile_size + local_col < N) {
                    shared_A[local_row * tile_size + local_col] = A[row * N + tile * tile_size + local_col];
                } else {
                    shared_A[local_row * tile_size + local_col] = 0.0f;
                }
                
                if (col < N && tile * tile_size + local_row < N) {
                    shared_B[local_row * tile_size + local_col] = B[(tile * tile_size + local_row) * N + col];
                } else {
                    shared_B[local_row * tile_size + local_col] = 0.0f;
                }
                
                threadgroup_barrier(mem_flags::mem_threadgroup);
                
                // Compute partial sum
                for (uint k = 0; k < tile_size; k++) {
                    sum += shared_A[local_row * tile_size + k] * shared_B[k * tile_size + local_col];
                }
                
                threadgroup_barrier(mem_flags::mem_threadgroup);
            }
            
            C[row * N + col] = sum;
        }";

    public MetalPerformanceTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Memory_Bandwidth_Benchmark_Should_Achieve_Expected_Performance()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        using var memoryTracker = new PerformanceMemoryTracker(Output);
        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        LogMetalDeviceCapabilities(accelerator);

        const int elementCount = 16 * 1024 * 1024; // 16M float4s = 256MB
        const int iterations = 20;
        const int warmupIterations = 5;

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount * 4); // float4
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount * 4);

        var kernelDef = new KernelDefinition
        {
            Name = "memory_bandwidth_test",
            Code = MemoryBandwidthKernel,
            EntryPoint = "memory_bandwidth_test"
        };
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        const int threadsPerThreadgroup = 256;
        var threadgroups = (elementCount + threadsPerThreadgroup - 1) / threadsPerThreadgroup;

        var measure = new PerformanceMeasurement("Memory Bandwidth", Output);

        // Warmup
        for (var i = 0; i < warmupIterations; i++)
        {
            var args = new KernelArguments { deviceInput, deviceOutput, elementCount };
            await kernel.LaunchAsync((threadgroups, 1, 1), (threadsPerThreadgroup, 1, 1), args);
        }

        var times = new double[iterations];

        for (var i = 0; i < iterations; i++)
        {
            measure.Start();
            var args = new KernelArguments { deviceInput, deviceOutput, elementCount };
            await kernel.LaunchAsync((threadgroups, 1, 1), (threadsPerThreadgroup, 1, 1), args);
            await accelerator.SynchronizeAsync();
            measure.Stop();
            times[i] = measure.ElapsedTime.TotalSeconds;
        }

        var avgTime = times.Average();
        var minTime = times.Min();
        var maxTime = times.Max();

        // Calculate bandwidth (read + write)
        var bytesPerIteration = elementCount * 4 * sizeof(float) * 2; // read input + write output
        var avgBandwidthGBps = bytesPerIteration / (avgTime * 1024 * 1024 * 1024);
        var peakBandwidthGBps = bytesPerIteration / (minTime * 1024 * 1024 * 1024);

        // Expected bandwidth varies by system
        var expectedBandwidth = IsAppleSilicon() ? 400.0 : 200.0; // GB/s
        var achievedRatio = avgBandwidthGBps / expectedBandwidth;

        Output.WriteLine($"Memory Bandwidth Benchmark Results:");
        Output.WriteLine($"  Data Size: {bytesPerIteration / (1024 * 1024):F1} MB per iteration");
        Output.WriteLine($"  Iterations: {iterations} (after {warmupIterations} warmup)");
        Output.WriteLine($"  Average Time: {avgTime * 1000:F2} ms");
        Output.WriteLine($"  Min/Max Time: {minTime * 1000:F2} / {maxTime * 1000:F2} ms");
        Output.WriteLine($"  Average Bandwidth: {avgBandwidthGBps:F1} GB/s");
        Output.WriteLine($"  Peak Bandwidth: {peakBandwidthGBps:F1} GB/s");
        Output.WriteLine($"  Expected Max: {expectedBandwidth:F1} GB/s");
        Output.WriteLine($"  Efficiency: {achievedRatio:P1}");

        // Performance validations
        avgBandwidthGBps.Should().BeGreaterThan(50, "Should achieve reasonable memory bandwidth");
        achievedRatio.Should().BeGreaterThan(0.2, "Should achieve at least 20% of theoretical bandwidth");
        peakBandwidthGBps.Should().BeGreaterThan(avgBandwidthGBps, "Peak should exceed average");

        memoryTracker.LogCurrentUsage("After benchmark");
    }

    [SkippableFact]
    public async Task Compute_Performance_Benchmark_Should_Meet_Expectations()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int elementCount = 1024 * 1024; // 1M elements
        const int iterations = 10;
        const int operationsPerElement = 1000;

        var hostInput = MetalTestDataGenerator.CreateRandomData(elementCount, seed: 42, min: 0.1f, max: 10.0f);

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);

        await deviceInput.CopyFromAsync(hostInput.AsMemory());

        var kernelDef = new KernelDefinition
        {
            Name = "compute_intensive_test",
            Code = ComputeIntensiveKernel,
            EntryPoint = "compute_intensive_test"
        };
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        const int threadsPerThreadgroup = 256;
        var threadgroups = (elementCount + threadsPerThreadgroup - 1) / threadsPerThreadgroup;

        // Warmup
        var args = new KernelArguments { deviceInput, deviceOutput, elementCount };
        await kernel.LaunchAsync((threadgroups, 1, 1), (threadsPerThreadgroup, 1, 1), args);

        var times = new double[iterations];

        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            args = new KernelArguments { deviceInput, deviceOutput, elementCount };
            await kernel.LaunchAsync((threadgroups, 1, 1), (threadsPerThreadgroup, 1, 1), args);
            await accelerator.SynchronizeAsync();
            stopwatch.Stop();
            times[i] = stopwatch.Elapsed.TotalSeconds;
        }

        var avgTime = times.Average();
        var totalOperations = (long)elementCount * operationsPerElement * iterations;
        var gflops = totalOperations / (avgTime * iterations * 1e9);

        // Verify computation completed (results should be different from input)
        var hostOutput = new float[elementCount];
        await deviceOutput.CopyToAsync(hostOutput.AsMemory());

        var significantDifferences = 0;
        for (var i = 0; i < Math.Min(1000, elementCount); i++)
        {
            if (Math.Abs(hostOutput[i] - hostInput[i]) > 0.1f)
            {
                significantDifferences++;
            }
        }

        Output.WriteLine($"Compute Performance Benchmark:");
        Output.WriteLine($"  Elements: {elementCount:N0}");
        Output.WriteLine($"  Operations per Element: {operationsPerElement:N0}");
        Output.WriteLine($"  Total Operations: {totalOperations:N0}");
        Output.WriteLine($"  Average Time: {avgTime * 1000:F2} ms");
        Output.WriteLine($"  Performance: {gflops:F2} GFLOPS");
        Output.WriteLine($"  Verification: {significantDifferences}/1000 elements changed significantly");

        gflops.Should().BeGreaterThan(1.0, "Should achieve reasonable compute performance");
        significantDifferences.Should().BeGreaterThan(900, "Most elements should be modified by computation");
    }

    [SkippableFact]
    public async Task Matrix_Multiply_Performance_Should_Be_Optimized()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        var matrixSizes = new[] { 512, 1024, 2048 };

        foreach (var matrixSize in matrixSizes)
        {
            var requiredMemory = (long)matrixSize * matrixSize * sizeof(float) * 3 * 2;
            if (accelerator.Info.TotalMemory < requiredMemory)
            {
                Output.WriteLine($"Skipping matrix size {matrixSize} due to insufficient memory");
                continue;
            }

            await BenchmarkMatrixMultiply(accelerator, matrixSize);
        }
    }

    private async Task BenchmarkMatrixMultiply(IAccelerator accelerator, int matrixSize)
    {
        const int iterations = 5;
        var elementCount = matrixSize * matrixSize;

        var hostA = MetalTestDataGenerator.CreateRandomData(elementCount, seed: 42);
        var hostB = MetalTestDataGenerator.CreateRandomData(elementCount, seed: 43);

        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);

        await deviceA.CopyFromAsync(hostA.AsMemory());
        await deviceB.CopyFromAsync(hostB.AsMemory());

        var kernelDef = new KernelDefinition
        {
            Name = "matrix_multiply_optimized",
            Code = MatrixMultiplyOptimized,
            EntryPoint = "matrix_multiply_optimized"
        };
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        var threadgroupSize = 16;
        var threadgroups = ((matrixSize + threadgroupSize - 1) / threadgroupSize,
                           (matrixSize + threadgroupSize - 1) / threadgroupSize, 1);

        // Warmup
        var args = new KernelArguments { deviceA, deviceB, deviceC, (uint)matrixSize };
        await kernel.LaunchAsync(threadgroups, (threadgroupSize, threadgroupSize, 1), args);

        var times = new double[iterations];

        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            args = new KernelArguments { deviceA, deviceB, deviceC, (uint)matrixSize };
            await kernel.LaunchAsync(threadgroups, (threadgroupSize, threadgroupSize, 1), args);
            await accelerator.SynchronizeAsync();
            stopwatch.Stop();
            times[i] = stopwatch.Elapsed.TotalSeconds;
        }

        var avgTime = times.Average();
        var minTime = times.Min();

        // Calculate GFLOPS (2 * N^3 operations for matrix multiply)
        var totalOps = 2.0 * matrixSize * matrixSize * matrixSize;
        var avgGflops = totalOps / (avgTime * 1e9);
        var peakGflops = totalOps / (minTime * 1e9);

        // Verify a few results
        var hostResult = new float[elementCount];
        await deviceC.CopyToAsync(hostResult.AsMemory());

        // Quick verification - compute first few elements on CPU
        for (var row = 0; row < Math.Min(3, matrixSize); row++)
        {
            for (var col = 0; col < Math.Min(3, matrixSize); col++)
            {
                var expected = 0.0f;
                for (var k = 0; k < matrixSize; k++)
                {
                    expected += hostA[row * matrixSize + k] * hostB[k * matrixSize + col];
                }

                var actual = hostResult[row * matrixSize + col];
                Math.Abs(actual - expected).Should().BeLessThan(0.01f,
                    $"Matrix multiply result incorrect at ({row},{col})");
            }
        }

        Output.WriteLine($"Matrix Multiply ({matrixSize}x{matrixSize}):");
        Output.WriteLine($"  Average Time: {avgTime * 1000:F2} ms");
        Output.WriteLine($"  Min Time: {minTime * 1000:F2} ms");
        Output.WriteLine($"  Average Performance: {avgGflops:F1} GFLOPS");
        Output.WriteLine($"  Peak Performance: {peakGflops:F1} GFLOPS");
        Output.WriteLine($"  Memory Usage: {elementCount * 3 * sizeof(float) / (1024 * 1024):F1} MB");

        // Performance expectations based on matrix size and system
        var expectedMinGflops = matrixSize switch
        {
            512 => IsAppleSilicon() ? 200.0 : 50.0,
            1024 => IsAppleSilicon() ? 800.0 : 200.0,
            2048 => IsAppleSilicon() ? 2000.0 : 500.0,
            _ => 25.0
        };

        avgGflops.Should().BeGreaterThan(expectedMinGflops,
            $"Matrix multiply should achieve reasonable performance for {matrixSize}x{matrixSize}");
    }

    [SkippableFact]
    public async Task Transfer_Performance_Should_Meet_Expectations()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        var transferSizes = new[] { 1, 4, 16, 64, 256 }; // MB
        const int iterations = 10;

        Output.WriteLine("Transfer Performance Benchmark:");
        Output.WriteLine("Size (MB)\tH2D (GB/s)\tD2H (GB/s)\tBidirectional (GB/s)");

        foreach (var sizeMB in transferSizes)
        {
            var sizeBytes = sizeMB * 1024 * 1024;
            var elementCount = (int)(sizeBytes / sizeof(float));

            var hostData = MetalTestDataGenerator.CreateRandomData(elementCount);
            await using var deviceBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);

            // Host to Device
            var h2dTimes = new double[iterations];
            for (var i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await deviceBuffer.CopyFromAsync(hostData.AsMemory());
                await accelerator.SynchronizeAsync();
                stopwatch.Stop();
                h2dTimes[i] = stopwatch.Elapsed.TotalSeconds;
            }
            var h2dBandwidth = sizeBytes / (h2dTimes.Average() * 1024 * 1024 * 1024);

            // Device to Host
            var d2hTimes = new double[iterations];
            var resultData = new float[elementCount];
            for (var i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await deviceBuffer.CopyToAsync(resultData.AsMemory());
                await accelerator.SynchronizeAsync();
                stopwatch.Stop();
                d2hTimes[i] = stopwatch.Elapsed.TotalSeconds;
            }
            var d2hBandwidth = sizeBytes / (d2hTimes.Average() * 1024 * 1024 * 1024);

            // Bidirectional (simulate concurrent access)
            var biTimes = new double[iterations];
            for (var i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                var uploadTask = deviceBuffer.CopyFromAsync(hostData.AsMemory());
                var downloadTask = deviceBuffer.CopyToAsync(resultData.AsMemory());
                await Task.WhenAll(uploadTask.AsTask(), downloadTask.AsTask());
                await accelerator.SynchronizeAsync();
                stopwatch.Stop();
                biTimes[i] = stopwatch.Elapsed.TotalSeconds;
            }
            var biBandwidth = (sizeBytes * 2) / (biTimes.Average() * 1024 * 1024 * 1024);

            Output.WriteLine($"{sizeMB}\t\t{h2dBandwidth:F2}\t\t{d2hBandwidth:F2}\t\t{biBandwidth:F2}");

            // Performance validations for larger transfers
            if (sizeMB >= 16)
            {
                var minExpectedBandwidth = IsAppleSilicon() ? 10.0 : 5.0; // Unified vs discrete memory
                h2dBandwidth.Should().BeGreaterThan(minExpectedBandwidth, $"H2D bandwidth should be reasonable for {sizeMB}MB");
                d2hBandwidth.Should().BeGreaterThan(minExpectedBandwidth, $"D2H bandwidth should be reasonable for {sizeMB}MB");
            }
        }
    }

    [SkippableFact]
    public async Task Concurrent_Kernel_Performance_Should_Show_Benefit()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int elementCount = 1024 * 1024;
        const int numKernels = 4;
        const int iterations = 5;

        var kernelCode = @"
            #include <metal_stdlib>
            using namespace metal;
            
            kernel void simple_add(
                device float* a [[buffer(0)]],
                device float* b [[buffer(1)]],
                device float* c [[buffer(2)]],
                constant uint& n [[buffer(3)]],
                uint id [[thread_position_in_grid]]
            ) {
                if (id >= n) return;
                c[id] = a[id] + b[id];
            }";

        var kernelDef = new KernelDefinition
        {
            Name = "simple_add",
            Code = kernelCode,
            EntryPoint = "simple_add"
        };
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        const int threadsPerThreadgroup = 256;
        var threadgroups = (elementCount + threadsPerThreadgroup - 1) / threadsPerThreadgroup;

        // Prepare data
        var hostDataA = new float[numKernels][];
        var hostDataB = new float[numKernels][];
        var deviceBuffersA = new IUnifiedMemoryBuffer<float>[numKernels];
        var deviceBuffersB = new IUnifiedMemoryBuffer<float>[numKernels];
        var deviceBuffersC = new IUnifiedMemoryBuffer<float>[numKernels];

        for (var i = 0; i < numKernels; i++)
        {
            hostDataA[i] = MetalTestUtilities.TestDataGenerator.CreateLinearSequence(elementCount, i * 1000);
            hostDataB[i] = MetalTestUtilities.TestDataGenerator.CreateLinearSequence(elementCount, i * 2000);
            deviceBuffersA[i] = await accelerator.Memory.AllocateAsync<float>(elementCount);
            deviceBuffersB[i] = await accelerator.Memory.AllocateAsync<float>(elementCount);
            deviceBuffersC[i] = await accelerator.Memory.AllocateAsync<float>(elementCount);
        }

        try
        {
            // Sequential execution
            var sequentialTimes = new double[iterations];

            for (var iter = 0; iter < iterations; iter++)
            {
                var stopwatch = Stopwatch.StartNew();

                for (var i = 0; i < numKernels; i++)
                {
                    await deviceBuffersA[i].CopyFromAsync(hostDataA[i].AsMemory());
                    await deviceBuffersB[i].CopyFromAsync(hostDataB[i].AsMemory());
                    var args = new KernelArguments { deviceBuffersA[i], deviceBuffersB[i], deviceBuffersC[i], (uint)elementCount };
                    await kernel.LaunchAsync((threadgroups, 1, 1), (threadsPerThreadgroup, 1, 1), args);
                }
                await accelerator.SynchronizeAsync();

                stopwatch.Stop();
                sequentialTimes[iter] = stopwatch.Elapsed.TotalSeconds;
            }

            // Concurrent execution (simulated through task parallelism)
            var concurrentTimes = new double[iterations];

            for (var iter = 0; iter < iterations; iter++)
            {
                var stopwatch = Stopwatch.StartNew();

                var tasks = new Task[numKernels];
                for (var i = 0; i < numKernels; i++)
                {
                    var kernelIndex = i;
                    tasks[i] = Task.Run(async () =>
                    {
                        await deviceBuffersA[kernelIndex].CopyFromAsync(hostDataA[kernelIndex].AsMemory());
                        await deviceBuffersB[kernelIndex].CopyFromAsync(hostDataB[kernelIndex].AsMemory());
                        var args = new KernelArguments { deviceBuffersA[kernelIndex], deviceBuffersB[kernelIndex], deviceBuffersC[kernelIndex], (uint)elementCount };
                        await kernel.LaunchAsync((threadgroups, 1, 1), (threadsPerThreadgroup, 1, 1), args);
                    });
                }

                await Task.WhenAll(tasks);
                await accelerator.SynchronizeAsync();
                stopwatch.Stop();
                concurrentTimes[iter] = stopwatch.Elapsed.TotalSeconds;
            }

            var avgSequential = sequentialTimes.Average();
            var avgConcurrent = concurrentTimes.Average();
            var speedup = avgSequential / avgConcurrent;

            Output.WriteLine($"Concurrent Kernel Performance:");
            Output.WriteLine($"  Kernels: {numKernels}");
            Output.WriteLine($"  Elements per kernel: {elementCount:N0}");
            Output.WriteLine($"  Sequential time: {avgSequential * 1000:F2} ms");
            Output.WriteLine($"  Concurrent time: {avgConcurrent * 1000:F2} ms");
            Output.WriteLine($"  Speedup: {speedup:F2}x");

            // Should see some benefit from concurrency (even if limited by Metal's command queue serialization)
            speedup.Should().BeGreaterThan(1.0, "Concurrent execution should provide some performance benefit");
        }
        finally
        {
            // Cleanup
            for (var i = 0; i < numKernels; i++)
            {
                await deviceBuffersA[i].DisposeAsync();
                await deviceBuffersB[i].DisposeAsync();
                await deviceBuffersC[i].DisposeAsync();
            }
        }
    }

    private void LogMetalDeviceCapabilities(IAccelerator accelerator)
    {
        var info = accelerator.Info;
        var capabilities = info.Capabilities ?? new Dictionary<string, object>();

        Output.WriteLine("Metal Device Capabilities:");
        Output.WriteLine($"  Name: {info.Name}");
        Output.WriteLine($"  Type: {info.DeviceType}");
        Output.WriteLine($"  Compute Capability: {info.ComputeCapability}");
        Output.WriteLine($"  Total Memory: {info.TotalMemory / (1024.0 * 1024.0 * 1024.0):F2} GB");
        Output.WriteLine($"  Compute Units: {info.ComputeUnits}");
        Output.WriteLine($"  Max Clock: {info.MaxClockFrequency} MHz");
        Output.WriteLine($"  Unified Memory: {info.IsUnifiedMemory}");

        if (capabilities.TryGetValue("MaxThreadgroupSize", out var maxThreadgroup))
        {
            Output.WriteLine($"  Max Threadgroup Size: {maxThreadgroup}");
        }

        if (capabilities.TryGetValue("SupportsFamily", out var families))
        {
            Output.WriteLine($"  GPU Families: {families}");
        }

        if (capabilities.TryGetValue("Location", out var location))
        {
            Output.WriteLine($"  Location: {location}");
        }

        Output.WriteLine($"  Apple Silicon: {IsAppleSilicon()}");
        Output.WriteLine($"  macOS Version: {GetMacOSVersion()}");
    }
}
