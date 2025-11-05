// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests.Performance;

/// <summary>
/// Comprehensive performance benchmark harness for Metal backend.
/// Measures throughput, latency, and comparative performance metrics.
/// </summary>
[Trait("Category", "Performance")]
public class MetalPerformanceBenchmarkHarness : MetalTestBase
{
    public MetalPerformanceBenchmarkHarness(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task VectorAdd_ThroughputBenchmark()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        var sizes = new[] { 1_000, 10_000, 100_000, 1_000_000, 10_000_000 };

        var kernelDef = new KernelDefinition
        {
            Name = "vector_add_bench",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void vector_add_bench(
                    device const float* a [[buffer(0)]],
                    device const float* b [[buffer(1)]],
                    device float* result [[buffer(2)]],
                    uint id [[thread_position_in_grid]])
                {
                    result[id] = a[id] + b[id];
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        Output.WriteLine("Vector Add Throughput Benchmark");
        Output.WriteLine("================================");

        foreach (var size in sizes)
        {
            var a = MetalTestDataGenerator.CreateRandomData(size);
            var b = MetalTestDataGenerator.CreateRandomData(size);

            var bufferA = await accelerator.Memory.AllocateAsync<float>(size);
            var bufferB = await accelerator.Memory.AllocateAsync<float>(size);
            var bufferResult = await accelerator.Memory.AllocateAsync<float>(size);

            await bufferA.CopyFromAsync(a.AsMemory());
            await bufferB.CopyFromAsync(b.AsMemory());

            // Warmup
            var args = new KernelArguments();
            args.AddBuffer(bufferA);
            args.AddBuffer(bufferB);
            args.AddBuffer(bufferResult);

            await kernel.ExecuteAsync(args);
            await accelerator.SynchronizeAsync();

            // Benchmark
            const int iterations = 100;
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                await kernel.ExecuteAsync(args);
            }

            await accelerator.SynchronizeAsync();
            sw.Stop();

            var avgTime = sw.Elapsed.TotalMilliseconds / iterations;
            var bytesTransferred = (long)size * sizeof(float) * 3; // Read A, Read B, Write Result
            var bandwidth = bytesTransferred / (avgTime / 1000.0) / (1024.0 * 1024.0 * 1024.0);

            Output.WriteLine($"Size: {size,12:N0} | Time: {avgTime,8:F3} ms | Bandwidth: {bandwidth,8:F2} GB/s");

            await bufferA.DisposeAsync();
            await bufferB.DisposeAsync();
            await bufferResult.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task MatrixMultiply_ScalabilityBenchmark()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        var sizes = new[] { 64, 128, 256, 512, 1024 };

        var kernelDef = new KernelDefinition
        {
            Name = "matmul_bench",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void matmul_bench(
                    device const float* a [[buffer(0)]],
                    device const float* b [[buffer(1)]],
                    device float* c [[buffer(2)]],
                    constant uint& N [[buffer(3)]],
                    uint2 id [[thread_position_in_grid]])
                {
                    uint row = id.y;
                    uint col = id.x;

                    if (row >= N || col >= N) return;

                    float sum = 0.0f;
                    for (uint k = 0; k < N; k++)
                    {
                        sum += a[row * N + k] * b[k * N + col];
                    }
                    c[row * N + col] = sum;
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        Output.WriteLine("Matrix Multiply Scalability Benchmark");
        Output.WriteLine("=====================================");

        foreach (var N in sizes)
        {
            var size = N * N;
            var a = MetalTestDataGenerator.CreateMatrix(N, N);
            var b = MetalTestDataGenerator.CreateMatrix(N, N);

            var bufferA = await accelerator.Memory.AllocateAsync<float>(size);
            var bufferB = await accelerator.Memory.AllocateAsync<float>(size);
            var bufferC = await accelerator.Memory.AllocateAsync<float>(size);

            await bufferA.CopyFromAsync(a.AsMemory());
            await bufferB.CopyFromAsync(b.AsMemory());

            var args = new KernelArguments();
            args.AddBuffer(bufferA);
            args.AddBuffer(bufferB);
            args.AddBuffer(bufferC);
            args.AddScalar((uint)N);

            // Warmup
            await kernel.ExecuteAsync(args);
            await accelerator.SynchronizeAsync();

            // Benchmark
            var sw = Stopwatch.StartNew();
            await kernel.ExecuteAsync(args);
            await accelerator.SynchronizeAsync();
            sw.Stop();

            var gflops = (2.0 * N * N * N) / (sw.Elapsed.TotalSeconds * 1e9);
            var time = sw.Elapsed.TotalMilliseconds;

            Output.WriteLine($"Size: {N,4}x{N,4} | Time: {time,8:F2} ms | Performance: {gflops,8:F2} GFLOPS");

            await bufferA.DisposeAsync();
            await bufferB.DisposeAsync();
            await bufferC.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Memory_AllocationLatencyBenchmark()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        var sizes = new[] { 1_000, 10_000, 100_000, 1_000_000, 10_000_000 };

        Output.WriteLine("Memory Allocation Latency Benchmark");
        Output.WriteLine("===================================");

        foreach (var size in sizes)
        {
            const int iterations = 100;
            var times = new List<double>();

            for (int i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                var buffer = await accelerator!.Memory.AllocateAsync<float>(size);
                sw.Stop();

                times.Add(sw.Elapsed.TotalMilliseconds);

                await buffer.DisposeAsync();
            }

            var avgTime = times.Average();
            var minTime = times.Min();
            var maxTime = times.Max();

            Output.WriteLine($"Size: {size,12:N0} | Avg: {avgTime,6:F3} ms | Min: {minTime,6:F3} ms | Max: {maxTime,6:F3} ms");
        }
    }

    [SkippableFact]
    public async Task Memory_TransferBandwidthBenchmark()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        var sizes = new[] { 1_000_000, 10_000_000, 100_000_000 }; // 1M to 100M floats

        Output.WriteLine("Memory Transfer Bandwidth Benchmark");
        Output.WriteLine("===================================");

        foreach (var size in sizes)
        {
            var data = MetalTestDataGenerator.CreateRandomData(size);
            var buffer = await accelerator!.Memory.AllocateAsync<float>(size);

            // Host to Device
            var sw = Stopwatch.StartNew();
            await buffer.CopyFromAsync(data.AsMemory());
            await accelerator.SynchronizeAsync();
            sw.Stop();

            var h2dTime = sw.Elapsed.TotalMilliseconds;
            var h2dBandwidth = (size * sizeof(float)) / (h2dTime / 1000.0) / (1024.0 * 1024.0 * 1024.0);

            // Device to Host
            var result = new float[size];
            sw.Restart();
            await buffer.CopyToAsync(result.AsMemory());
            await accelerator.SynchronizeAsync();
            sw.Stop();

            var d2hTime = sw.Elapsed.TotalMilliseconds;
            var d2hBandwidth = (size * sizeof(float)) / (d2hTime / 1000.0) / (1024.0 * 1024.0 * 1024.0);

            var sizeGB = (size * sizeof(float)) / (1024.0 * 1024.0 * 1024.0);

            Output.WriteLine($"Size: {sizeGB,6:F2} GB");
            Output.WriteLine($"  H2D: {h2dTime,8:F2} ms | {h2dBandwidth,6:F2} GB/s");
            Output.WriteLine($"  D2H: {d2hTime,8:F2} ms | {d2hBandwidth,6:F2} GB/s");

            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Kernel_CompilationTimeBenchmark()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();

        var kernels = new[]
        {
            ("Simple", @"
                #include <metal_stdlib>
                using namespace metal;
                kernel void simple(device float* d [[buffer(0)]], uint id [[thread_position_in_grid]]) {
                    d[id] *= 2.0f;
                }"),
            ("Medium", @"
                #include <metal_stdlib>
                using namespace metal;
                kernel void medium(device const float* a [[buffer(0)]], device const float* b [[buffer(1)]],
                    device float* c [[buffer(2)]], uint id [[thread_position_in_grid]]) {
                    c[id] = a[id] * b[id] + a[id] - b[id];
                }"),
            ("Complex", @"
                #include <metal_stdlib>
                using namespace metal;
                kernel void complex(device const float* input [[buffer(0)]], device float* output [[buffer(1)]],
                    constant uint& size [[buffer(2)]], uint id [[thread_position_in_grid]]) {
                    if (id >= size) return;
                    float x = input[id];
                    output[id] = sqrt(sin(x) * sin(x) + cos(x) * cos(x)) * pow(x, 2.0f) + log(abs(x) + 1.0f);
                }")
        };

        Output.WriteLine("Kernel Compilation Time Benchmark");
        Output.WriteLine("=================================");

        foreach (var (name, code) in kernels)
        {
            var kernelDef = new KernelDefinition
            {
                Name = name.ToLowerInvariant(),
                Language = KernelLanguage.Metal,
                Code = code
            };

            // First compilation (cold)
            var sw = Stopwatch.StartNew();
            var compiled1 = await accelerator!.CompileKernelAsync(kernelDef);
            sw.Stop();
            var coldTime = sw.Elapsed.TotalMilliseconds;

            // Second compilation (should be cached)
            sw.Restart();
            var compiled2 = await accelerator.CompileKernelAsync(kernelDef);
            sw.Stop();
            var cacheTime = sw.Elapsed.TotalMilliseconds;

            Output.WriteLine($"{name,-10} | Cold: {coldTime,6:F2} ms | Cached: {cacheTime,6:F2} ms | Speedup: {coldTime / Math.Max(0.001, cacheTime),4:F1}x");
        }
    }

    [SkippableFact]
    public async Task Concurrent_KernelExecutionBenchmark()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 1_000_000;
        const int concurrentCount = 4;

        var kernelDef = new KernelDefinition
        {
            Name = "concurrent_bench",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void concurrent_bench(
                    device float* data [[buffer(0)]],
                    constant float& multiplier [[buffer(1)]],
                    uint id [[thread_position_in_grid]])
                {
                    data[id] *= multiplier;
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);

        var buffers = new List<IUnifiedMemoryBuffer<float>>();
        var data = MetalTestDataGenerator.CreateConstantData(size, 1.0f);

        for (int i = 0; i < concurrentCount; i++)
        {
            var buffer = await accelerator.Memory.AllocateAsync<float>(size);
            await buffer.CopyFromAsync(data.AsMemory());
            buffers.Add(buffer);
        }

        Output.WriteLine("Concurrent Kernel Execution Benchmark");
        Output.WriteLine("=====================================");

        // Sequential execution
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < concurrentCount; i++)
        {
            var args = new KernelArguments();
            args.AddBuffer(buffers[i]);
            args.AddScalar(2.0f);
            await kernel.ExecuteAsync(args);
        }
        await accelerator.SynchronizeAsync();
        sw.Stop();
        var sequentialTime = sw.Elapsed.TotalMilliseconds;

        // Reset buffers
        foreach (var buffer in buffers)
        {
            await buffer.CopyFromAsync(data.AsMemory());
        }

        // Concurrent execution
        sw.Restart();
        var tasks = new List<Task>();
        for (int i = 0; i < concurrentCount; i++)
        {
            var args = new KernelArguments();
            args.AddBuffer(buffers[i]);
            args.AddScalar(2.0f);
            tasks.Add(kernel.ExecuteAsync(args).AsTask());
        }
        await Task.WhenAll(tasks);
        await accelerator.SynchronizeAsync();
        sw.Stop();
        var concurrentTime = sw.Elapsed.TotalMilliseconds;

        Output.WriteLine($"Kernels: {concurrentCount}");
        Output.WriteLine($"Sequential: {sequentialTime,8:F2} ms");
        Output.WriteLine($"Concurrent: {concurrentTime,8:F2} ms");
        Output.WriteLine($"Speedup:    {sequentialTime / concurrentTime,8:F2}x");

        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task End_To_End_WorkflowBenchmark()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal not available");

        // Arrange
        await using var accelerator = Factory.CreateProductionAccelerator();
        const int size = 10_000_000;

        Output.WriteLine("End-to-End Workflow Benchmark");
        Output.WriteLine("=============================");

        var sw = Stopwatch.StartNew();

        // Step 1: Kernel compilation
        var compilationStart = sw.Elapsed;
        var kernelDef = new KernelDefinition
        {
            Name = "e2e_workflow",
            Language = KernelLanguage.Metal,
            Code = @"
                #include <metal_stdlib>
                using namespace metal;

                kernel void e2e_workflow(
                    device const float* a [[buffer(0)]],
                    device const float* b [[buffer(1)]],
                    device float* result [[buffer(2)]],
                    uint id [[thread_position_in_grid]])
                {
                    result[id] = sqrt(a[id] * a[id] + b[id] * b[id]);
                }"
        };

        var kernel = await accelerator!.CompileKernelAsync(kernelDef);
        var compilationTime = (sw.Elapsed - compilationStart).TotalMilliseconds;

        // Step 2: Memory allocation
        var allocationStart = sw.Elapsed;
        var bufferA = await accelerator.Memory.AllocateAsync<float>(size);
        var bufferB = await accelerator.Memory.AllocateAsync<float>(size);
        var bufferResult = await accelerator.Memory.AllocateAsync<float>(size);
        var allocationTime = (sw.Elapsed - allocationStart).TotalMilliseconds;

        // Step 3: Host to Device transfer
        var transferH2DStart = sw.Elapsed;
        var a = MetalTestDataGenerator.CreateRandomData(size);
        var b = MetalTestDataGenerator.CreateRandomData(size);
        await bufferA.CopyFromAsync(a.AsMemory());
        await bufferB.CopyFromAsync(b.AsMemory());
        var transferH2DTime = (sw.Elapsed - transferH2DStart).TotalMilliseconds;

        // Step 4: Kernel execution
        var executionStart = sw.Elapsed;
        var args = new KernelArguments();
        args.AddBuffer(bufferA);
        args.AddBuffer(bufferB);
        args.AddBuffer(bufferResult);
        await kernel.ExecuteAsync(args);
        await accelerator.SynchronizeAsync();
        var executionTime = (sw.Elapsed - executionStart).TotalMilliseconds;

        // Step 5: Device to Host transfer
        var transferD2HStart = sw.Elapsed;
        var result = new float[size];
        await bufferResult.CopyToAsync(result.AsMemory());
        var transferD2HTime = (sw.Elapsed - transferD2HStart).TotalMilliseconds;

        // Step 6: Cleanup
        var cleanupStart = sw.Elapsed;
        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferResult.DisposeAsync();
        var cleanupTime = (sw.Elapsed - cleanupStart).TotalMilliseconds;

        sw.Stop();
        var totalTime = sw.Elapsed.TotalMilliseconds;

        Output.WriteLine($"Data size: {size:N0} floats ({size * sizeof(float) / (1024.0 * 1024.0):F2} MB)");
        Output.WriteLine($"1. Compilation:      {compilationTime,8:F2} ms ({compilationTime / totalTime * 100,5:F1}%)");
        Output.WriteLine($"2. Allocation:       {allocationTime,8:F2} ms ({allocationTime / totalTime * 100,5:F1}%)");
        Output.WriteLine($"3. Transfer H2D:     {transferH2DTime,8:F2} ms ({transferH2DTime / totalTime * 100,5:F1}%)");
        Output.WriteLine($"4. Execution:        {executionTime,8:F2} ms ({executionTime / totalTime * 100,5:F1}%)");
        Output.WriteLine($"5. Transfer D2H:     {transferD2HTime,8:F2} ms ({transferD2HTime / totalTime * 100,5:F1}%)");
        Output.WriteLine($"6. Cleanup:          {cleanupTime,8:F2} ms ({cleanupTime / totalTime * 100,5:F1}%)");
        Output.WriteLine($"Total:               {totalTime,8:F2} ms");
    }
}
