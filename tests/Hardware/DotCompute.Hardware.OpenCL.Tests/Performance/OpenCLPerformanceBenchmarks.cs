// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Hardware.OpenCL.Tests.Helpers;
using Xunit.Abstractions;

namespace DotCompute.Hardware.OpenCL.Tests.Performance;

/// <summary>
/// Performance benchmarks for OpenCL operations.
/// Measures throughput, latency, and scalability.
/// </summary>
[Trait("Category", "RequiresOpenCL")]
[Trait("Category", "Performance")]
public class OpenCLPerformanceBenchmarks : OpenCLTestBase
{
    private const string BenchmarkKernel = @"
        __kernel void benchmark(__global const float* input, __global float* output, int n) {
            int idx = get_global_id(0);
            if (idx < n) {
                float x = input[idx];
                output[idx] = x * x + 2.0f * x + 1.0f;
            }
        }";

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLPerformanceBenchmarks"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public OpenCLPerformanceBenchmarks(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Benchmarks small kernel launches (latency focused).
    /// </summary>
    [SkippableFact]
    public async Task Benchmark_Small_Kernel_Latency()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 1024;
        const int iterations = 100;

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);

        var hostInput = new float[elementCount];
        Array.Fill(hostInput, 1.0f);
        await deviceInput.WriteAsync(hostInput.AsSpan(), 0);

        var kernelDef = new KernelDefinition("benchmark", BenchmarkKernel, "benchmark");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        var launchConfig = new LaunchConfiguration
        {
            GlobalWorkSize = new Dim3(elementCount),
            LocalWorkSize = new Dim3(256)
        };

        // Warmup
        await kernel.LaunchAsync<float>(launchConfig, deviceInput, deviceOutput, elementCount);
        await accelerator.SynchronizeAsync();

        var times = new double[iterations];

        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await kernel.LaunchAsync<float>(launchConfig, deviceInput, deviceOutput, elementCount);
            await accelerator.SynchronizeAsync();
            stopwatch.Stop();
            times[i] = stopwatch.Elapsed.TotalMicroseconds;
        }

        var avgLatency = times.Average();
        var minLatency = times.Min();
        var maxLatency = times.Max();
        var stdDev = Math.Sqrt(times.Average(t => Math.Pow(t - avgLatency, 2)));

        Output.WriteLine($"Small Kernel Latency Benchmark ({iterations} iterations):");
        Output.WriteLine($"  Average: {avgLatency:F2} μs");
        Output.WriteLine($"  Min: {minLatency:F2} μs");
        Output.WriteLine($"  Max: {maxLatency:F2} μs");
        Output.WriteLine($"  Std Dev: {stdDev:F2} μs");

        avgLatency.Should().BeLessThan(1000, "Small kernel latency should be reasonable");
    }

    /// <summary>
    /// Benchmarks large kernel launches (throughput focused).
    /// </summary>
    [SkippableFact]
    public async Task Benchmark_Large_Kernel_Throughput()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 16 * 1024 * 1024;
        const int iterations = 10;

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);

        var hostInput = new float[elementCount];
        Array.Fill(hostInput, 1.0f);
        await deviceInput.WriteAsync(hostInput.AsSpan(), 0);

        var kernelDef = new KernelDefinition("benchmark", BenchmarkKernel, "benchmark");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        const int workGroupSize = 256;
        var globalSize = ((elementCount + workGroupSize - 1) / workGroupSize) * workGroupSize;

        var launchConfig = new LaunchConfiguration
        {
            GlobalWorkSize = new Dim3(globalSize),
            LocalWorkSize = new Dim3(workGroupSize)
        };

        // Warmup
        await kernel.LaunchAsync<float>(launchConfig, deviceInput, deviceOutput, elementCount);
        await accelerator.SynchronizeAsync();

        var times = new double[iterations];

        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await kernel.LaunchAsync<float>(launchConfig, deviceInput, deviceOutput, elementCount);
            await accelerator.SynchronizeAsync();
            stopwatch.Stop();
            times[i] = stopwatch.Elapsed.TotalMilliseconds;
        }

        var avgTime = times.Average();
        var throughput = (elementCount * 2 * sizeof(float)) / (avgTime / 1000.0 * 1024 * 1024 * 1024);

        Output.WriteLine($"Large Kernel Throughput Benchmark ({iterations} iterations):");
        Output.WriteLine($"  Elements: {elementCount:N0}");
        Output.WriteLine($"  Average Time: {avgTime:F2} ms");
        Output.WriteLine($"  Throughput: {throughput:F2} GB/s");

        throughput.Should().BeGreaterThan(5, "Large kernel throughput should be substantial");
    }

    /// <summary>
    /// Benchmarks memory transfer bandwidth.
    /// </summary>
    [SkippableFact]
    public async Task Benchmark_Memory_Transfer_Bandwidth()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var sizes = new[] { 1024 * 1024, 4 * 1024 * 1024, 16 * 1024 * 1024, 64 * 1024 * 1024 };

        Output.WriteLine("Memory Transfer Bandwidth Benchmark:");
        Output.WriteLine($"{"Size (MB)",10} | {"H2D (GB/s)",12} | {"D2H (GB/s)",12}");
        Output.WriteLine(new string('-', 40));

        foreach (var size in sizes)
        {
            var hostData = new float[size];
            Array.Fill(hostData, 1.0f);

            await using var deviceBuffer = await accelerator.Memory.AllocateAsync<float>(size);

            // Measure H2D
            var h2dWatch = Stopwatch.StartNew();
            await deviceBuffer.WriteAsync(hostData.AsSpan(), 0);
            await accelerator.SynchronizeAsync();
            h2dWatch.Stop();

            var h2dBandwidth = (size * sizeof(float)) / (h2dWatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);

            // Measure D2H
            var d2hWatch = Stopwatch.StartNew();
            await deviceBuffer.ReadAsync(hostData.AsSpan(), 0);
            await accelerator.SynchronizeAsync();
            d2hWatch.Stop();

            var d2hBandwidth = (size * sizeof(float)) / (d2hWatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);

            var sizeMB = (size * sizeof(float)) / (1024 * 1024);
            Output.WriteLine($"{sizeMB,10:F1} | {h2dBandwidth,12:F2} | {d2hBandwidth,12:F2}");
        }
    }

    /// <summary>
    /// Benchmarks scaling with problem size.
    /// </summary>
    [SkippableFact]
    public async Task Benchmark_Scalability()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        var sizes = new[] { 1024, 4096, 16384, 65536, 262144, 1048576 };

        var kernelDef = new KernelDefinition("benchmark", BenchmarkKernel, "benchmark");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        Output.WriteLine("Scalability Benchmark:");
        Output.WriteLine($"{"Elements",10} | {"Time (ms)",12} | {"Throughput (M elem/s)",25}");
        Output.WriteLine(new string('-', 50));

        foreach (var size in sizes)
        {
            await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(size);
            await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(size);

            var hostInput = new float[size];
            Array.Fill(hostInput, 1.0f);
            await deviceInput.WriteAsync(hostInput.AsSpan(), 0);

            const int workGroupSize = 256;
            var globalSize = ((size + workGroupSize - 1) / workGroupSize) * workGroupSize;

            var launchConfig = new LaunchConfiguration
            {
                GlobalWorkSize = new Dim3(globalSize),
                LocalWorkSize = new Dim3(workGroupSize)
            };

            // Warmup
            await kernel.LaunchAsync<float>(launchConfig, deviceInput, deviceOutput, size);
            await accelerator.SynchronizeAsync();

            // Measure
            const int iterations = 10;
            var times = new double[iterations];

            for (var i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await kernel.LaunchAsync<float>(launchConfig, deviceInput, deviceOutput, size);
                await accelerator.SynchronizeAsync();
                stopwatch.Stop();
                times[i] = stopwatch.Elapsed.TotalMilliseconds;
            }

            var avgTime = times.Average();
            var throughput = size / (avgTime / 1000.0) / 1e6;

            Output.WriteLine($"{size,10:N0} | {avgTime,12:F3} | {throughput,25:F2}");
        }
    }

    /// <summary>
    /// Benchmarks concurrent kernel execution.
    /// </summary>
    [SkippableFact]
    public async Task Benchmark_Concurrent_Execution()
    {
        Skip.IfNot(IsOpenCLAvailable, "OpenCL hardware not available");

        await using var accelerator = CreateAccelerator();

        const int elementCount = 1024 * 1024;
        const int numKernels = 4;

        var kernelDef = new KernelDefinition("benchmark", BenchmarkKernel, "benchmark");
        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        var buffers = new List<(IDisposable input, IDisposable output)>();

        try
        {
            // Allocate buffers
            for (var i = 0; i < numKernels; i++)
            {
                var input = await accelerator.Memory.AllocateAsync<float>(elementCount);
                var output = await accelerator.Memory.AllocateAsync<float>(elementCount);
                buffers.Add((input, output));

                var hostData = new float[elementCount];
                Array.Fill(hostData, 1.0f);
                await ((dynamic)input).WriteAsync(hostData.AsSpan(), 0);
            }

            const int workGroupSize = 256;
            var globalSize = ((elementCount + workGroupSize - 1) / workGroupSize) * workGroupSize;

            var launchConfig = new LaunchConfiguration
            {
                GlobalWorkSize = new Dim3(globalSize),
                LocalWorkSize = new Dim3(workGroupSize)
            };

            // Sequential execution
            var sequentialWatch = Stopwatch.StartNew();
            foreach (var (input, output) in buffers)
            {
                await kernel.LaunchAsync<float>(launchConfig, (dynamic)input, (dynamic)output, elementCount);
                await accelerator.SynchronizeAsync();
            }
            sequentialWatch.Stop();

            // Concurrent execution
            var concurrentWatch = Stopwatch.StartNew();
            var tasks = buffers.Select(async b =>
            {
                await kernel.LaunchAsync<float>(launchConfig, (dynamic)b.input, (dynamic)b.output, elementCount);
            }).ToArray();
            await Task.WhenAll(tasks);
            await accelerator.SynchronizeAsync();
            concurrentWatch.Stop();

            Output.WriteLine("Concurrent Execution Benchmark:");
            Output.WriteLine($"  Sequential Time: {sequentialWatch.Elapsed.TotalMilliseconds:F2} ms");
            Output.WriteLine($"  Concurrent Time: {concurrentWatch.Elapsed.TotalMilliseconds:F2} ms");
            Output.WriteLine($"  Speedup: {sequentialWatch.Elapsed.TotalMilliseconds / concurrentWatch.Elapsed.TotalMilliseconds:F2}x");
        }
        finally
        {
            foreach (var (input, output) in buffers)
            {
                if (input is IAsyncDisposable asyncInput)
                {
                    await asyncInput.DisposeAsync();
                }
                if (output is IAsyncDisposable asyncOutput)
                {
                    await asyncOutput.DisposeAsync();
                }
            }
        }
    }
}
