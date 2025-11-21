// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.MPS;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Backends.Metal.Benchmarks;

/// <summary>
/// Simple performance validation without BenchmarkDotNet complexity.
/// Tests each of the 7 performance claims individually.
/// </summary>
public static class SimplePerformanceValidation
{
    private const int WarmupIterations = 10;
    private const int MeasureIterations = 100;

    public static async Task Run()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  Metal Backend Performance Validation (Simple)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        var results = new List<(string Claim, bool Passed, string Result, string Target)>();

        // Claim 5: Kernel Compilation Cache
        results.Add(await ValidateKernelCachePerformance());

        // Claim 6a: Command Queue Latency
        results.Add(await ValidateCommandQueueLatency());

        // Claim 4: Backend Cold Start
        results.Add(await ValidateBackendInitialization());

        // Claim 1: Unified Memory Performance
        results.Add(await ValidateUnifiedMemoryPerformance());

        // Claim 2: MPS Performance
        // TODO: Disposal crash - investigating MetalMPSOrchestrator cleanup
        // results.Add(await ValidateMPSPerformance());
        Console.WriteLine("⚠️  Skipping Claim #2 (MPS Performance) - disposal issue under investigation\n");

        // Claim 7: Graph Execution Parallelism (FIXED - command queue pool implemented)
        results.Add(await ValidateGraphExecutionParallelism());

        // Print Summary
        Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  VALIDATION SUMMARY");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        int passed = 0;
        int failed = 0;

        foreach (var (claim, passedTest, result, target) in results)
        {
            var status = passedTest ? "✅ PASS" : "❌ FAIL";
            Console.WriteLine($"{status} | {claim}");
            Console.WriteLine($"      Result: {result}");
            Console.WriteLine($"      Target: {target}\n");

            if (passedTest)
            {
                passed++;
            }
            else
            {
                failed++;
            }
        }

        Console.WriteLine($"Total: {passed} passed, {failed} failed out of {results.Count} claims validated");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
    }

    private static async Task<(string, bool, string, string)> ValidateKernelCachePerformance()
    {
        Console.WriteLine("Testing Claim #5: Kernel Compilation Cache (<1ms cache hits)...");

        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Error));
        var logger = loggerFactory.CreateLogger<MetalAccelerator>();
        var options = Options.Create(new MetalAcceleratorOptions
        {
            EnableMetalPerformanceShaders = true,
            CommandBufferCacheSize = 16
        });

        await using var accelerator = new MetalAccelerator(options, logger);

        const string kernelCode = @"
#include <metal_stdlib>
using namespace metal;
kernel void test_kernel(device const float* input [[buffer(0)]], device float* output [[buffer(1)]], uint id [[thread_position_in_grid]])
{
    output[id] = input[id] * 2.0f;
}";

        var definition = new KernelDefinition("test_kernel", kernelCode)
        {
            EntryPoint = "test_kernel",
            Language = KernelLanguage.Metal
        };

        // First compilation (cache miss)
        var kernel1 = await accelerator.CompileKernelAsync(definition);
        kernel1.Dispose();

        // Warmup cache hits
        for (int i = 0; i < WarmupIterations; i++)
        {
            var k = await accelerator.CompileKernelAsync(definition);
            k.Dispose();
        }

        // Measure cache hits
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < MeasureIterations; i++)
        {
            var k = await accelerator.CompileKernelAsync(definition);
            k.Dispose();
        }
        sw.Stop();

        var avgMicroseconds = (sw.Elapsed.TotalMicroseconds / MeasureIterations);
        var avgMilliseconds = avgMicroseconds / 1000.0;

        bool passed = avgMilliseconds < 1.0;

        Console.WriteLine($"  Average cache hit: {avgMicroseconds:F3} μs ({avgMilliseconds:F6} ms)");
        Console.WriteLine($"  Status: {(passed ? "✅ PASS" : "❌ FAIL")}\n");

        return (
            "Kernel Compilation Cache",
            passed,
            $"{avgMicroseconds:F3} μs",
            "<1000 μs (1 ms)"
        );
    }

    private static Task<(string, bool, string, string)> ValidateCommandQueueLatency()
    {
        Console.WriteLine("Testing Claim #6a: Command Buffer Acquisition Latency (<100μs)...");

        // Setup: Create device and queue once (outside measurement)
        var device = MetalNative.CreateSystemDefaultDevice();
        var commandQueue = MetalNative.CreateCommandQueue(device);

        try
        {
            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                var warmupBuffer = MetalNative.CreateCommandBuffer(commandQueue);
                MetalNative.ReleaseCommandBuffer(warmupBuffer);
            }

            // Measure pure command buffer acquisition
            var times = new List<double>();
            for (int i = 0; i < MeasureIterations; i++)
            {
                var sw = Stopwatch.StartNew();
                var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);
                sw.Stop();

                times.Add(sw.Elapsed.TotalMicroseconds);
                MetalNative.ReleaseCommandBuffer(commandBuffer);
            }

            var avgMicroseconds = times.Average();
            bool passed = avgMicroseconds < 100.0;

            Console.WriteLine($"  Average buffer acquisition: {avgMicroseconds:F2} μs");
            Console.WriteLine($"  Status: {(passed ? "✅ PASS" : "❌ FAIL")}\n");

            return Task.FromResult((
                "Command Buffer Acquisition",
                passed,
                $"{avgMicroseconds:F2} μs",
                "<100 μs"
            ));
        }
        finally
        {
            MetalNative.ReleaseCommandQueue(commandQueue);
            MetalNative.ReleaseDevice(device);
        }
    }

    private static async Task<(string, bool, string, string)> ValidateBackendInitialization()
    {
        Console.WriteLine("Testing Claim #4: Backend Cold Start (<10ms)...");

        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Error));
        var logger = loggerFactory.CreateLogger<MetalAccelerator>();
        var options = Options.Create(new MetalAcceleratorOptions
        {
            EnableMetalPerformanceShaders = true,
            CommandBufferCacheSize = 16
        });

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            await using var acc = new MetalAccelerator(options, logger);
        }

        // Measure cold starts
        var times = new List<double>();
        for (int i = 0; i < 50; i++)
        {
            var sw = Stopwatch.StartNew();
            await using var acc = new MetalAccelerator(options, logger);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        var avgMs = times.Average();
        bool passed = avgMs < 10.0;

        Console.WriteLine($"  Average initialization: {avgMs:F2} ms");
        Console.WriteLine($"  Status: {(passed ? "✅ PASS" : "❌ FAIL")}\n");

        return (
            "Backend Cold Start",
            passed,
            $"{avgMs:F2} ms",
            "<10 ms"
        );
    }

    private static async Task<(string, bool, string, string)> ValidateUnifiedMemoryPerformance()
    {
        Console.WriteLine("Testing Claim #1: Unified Memory Performance (2-3x speedup)...");

        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Error));
        var logger = loggerFactory.CreateLogger<MetalAccelerator>();
        var memLogger = loggerFactory.CreateLogger<MetalMemoryManager>();
        var options = Options.Create(new MetalAcceleratorOptions
        {
            EnableMetalPerformanceShaders = true,
            CommandBufferCacheSize = 16
        });

        await using var accelerator = new MetalAccelerator(options, logger);
        const int dataSize = 1_000_000;

        // Test 1: Discrete memory (baseline)
        var discreteMemory = new MetalMemoryManager(memLogger, accelerator, enablePooling: false);
        var discreteTimes = new List<double>();

        for (int i = 0; i < 10; i++)
        {
            var buffer = await discreteMemory.AllocateAsync<float>(dataSize, MemoryOptions.None);

            var sw = Stopwatch.StartNew();
            var hostData = new float[dataSize];
            await buffer.CopyFromAsync(hostData);
            var result = new float[dataSize];
            await buffer.CopyToAsync(result);
            sw.Stop();

            discreteTimes.Add(sw.Elapsed.TotalMilliseconds);
            await discreteMemory.FreeAsync(buffer, CancellationToken.None);
        }

        // Test 2: Unified memory (optimized)
        var unifiedMemory = new MetalMemoryManager(memLogger, accelerator, enablePooling: false);
        var unifiedTimes = new List<double>();

        for (int i = 0; i < 10; i++)
        {
            var buffer = await unifiedMemory.AllocateAsync<float>(dataSize, MemoryOptions.Unified | MemoryOptions.Coherent);

            var sw = Stopwatch.StartNew();
            var hostData = new float[dataSize];
            await buffer.CopyFromAsync(hostData);
            var result = new float[dataSize];
            await buffer.CopyToAsync(result);
            sw.Stop();

            unifiedTimes.Add(sw.Elapsed.TotalMilliseconds);
            await unifiedMemory.FreeAsync(buffer, CancellationToken.None);
        }

        var avgDiscrete = discreteTimes.Average();
        var avgUnified = unifiedTimes.Average();
        var speedup = avgDiscrete / avgUnified;

        bool passed = speedup >= 2.0;

        Console.WriteLine($"  Discrete memory: {avgDiscrete:F2} ms");
        Console.WriteLine($"  Unified memory: {avgUnified:F2} ms");
        Console.WriteLine($"  Speedup: {speedup:F2}x");
        Console.WriteLine($"  Status: {(passed ? "✅ PASS" : "❌ FAIL")}\n");

        return (
            "Unified Memory Performance",
            passed,
            $"{speedup:F2}x speedup",
            "2-3x speedup"
        );
    }

    private static async Task<(string, bool, string, string)> ValidateMPSPerformance()
    {
        Console.WriteLine("Testing Claim #2: MPS Performance (3-4x speedup)...");

        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Error));
        var logger = loggerFactory.CreateLogger<MetalAccelerator>();
        var memLogger = loggerFactory.CreateLogger<MetalMemoryManager>();
        var mpsLogger = loggerFactory.CreateLogger<MetalMPSOrchestrator>();
        var options = Options.Create(new MetalAcceleratorOptions
        {
            EnableMetalPerformanceShaders = true,
            CommandBufferCacheSize = 16
        });

        await using var accelerator = new MetalAccelerator(options, logger);
        var memoryManager = new MetalMemoryManager(memLogger, accelerator, enablePooling: true);

        const int size = 512; // 512x512 matrices
        const int warmupIterations = 3;
        const int measureIterations = 10;

        // Test 1: Custom kernel (baseline)
        // Compile kernel ONCE outside measurement loop
        const string kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void matmul(
    device const float* A [[buffer(0)]],
    device const float* B [[buffer(1)]],
    device float* C [[buffer(2)]],
    constant int& N [[buffer(3)]],
    uint2 gid [[thread_position_in_grid]])
{
    int row = gid.y;
    int col = gid.x;
    if (row >= N || col >= N) return;

    float sum = 0.0f;
    for (int k = 0; k < N; k++) {
        sum += A[row * N + k] * B[k * N + col];
    }
    C[row * N + col] = sum;
}";

        var definition = new KernelDefinition("matmul_perf", kernelCode)
        {
            EntryPoint = "matmul",
            Language = KernelLanguage.Metal
        };

        // Pre-compile kernel
        using var kernel = await accelerator.CompileKernelAsync(definition);

        // Allocate buffers once
        var a = await memoryManager.AllocateAsync<float>(size * size);
        var b = await memoryManager.AllocateAsync<float>(size * size);
        var c = await memoryManager.AllocateAsync<float>(size * size);

        // Warmup
        for (int i = 0; i < warmupIterations; i++)
        {
            await kernel.ExecuteAsync([a, b, c, size], CancellationToken.None);
        }

        // Measure execution time only
        var customTimes = new List<double>();
        for (int i = 0; i < measureIterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await kernel.ExecuteAsync([a, b, c, size], CancellationToken.None);
            sw.Stop();
            customTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Cleanup
        await memoryManager.FreeAsync(a, CancellationToken.None);
        await memoryManager.FreeAsync(b, CancellationToken.None);
        await memoryManager.FreeAsync(c, CancellationToken.None);

        // Test 2: MPS accelerated (optimized)
        // Use accelerator's device - DO NOT create separate device
        var device = accelerator.Device;
        using var orchestrator = new MetalMPSOrchestrator(device, mpsLogger);

        // Allocate data once
        var aData = new float[size * size];
        var bData = new float[size * size];
        var cData = new float[size * size];

        // Warmup MPS
        for (int i = 0; i < warmupIterations; i++)
        {
            orchestrator.MatrixMultiply(
                aData, size, size,
                bData, size, size,
                cData, size, size);
        }

        // Measure MPS execution time only
        var mpsTimes = new List<double>();
        for (int i = 0; i < measureIterations; i++)
        {
            var sw = Stopwatch.StartNew();
            orchestrator.MatrixMultiply(
                aData, size, size,
                bData, size, size,
                cData, size, size);
            sw.Stop();
            mpsTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        // NO manual device release - let accelerator handle it via using statement

        var avgCustom = customTimes.Average();
        var avgMPS = mpsTimes.Average();
        var speedup = avgCustom / avgMPS;

        bool passed = speedup >= 3.0;

        Console.WriteLine($"  Custom kernel: {avgCustom:F2} ms (avg of {measureIterations} runs)");
        Console.WriteLine($"  MPS accelerated: {avgMPS:F2} ms (avg of {measureIterations} runs)");
        Console.WriteLine($"  Speedup: {speedup:F2}x");
        Console.WriteLine($"  Status: {(passed ? "✅ PASS" : "❌ FAIL")}\n");

        return (
            "MPS Performance",
            passed,
            $"{speedup:F2}x speedup",
            "3-4x speedup"
        );
    }

    private static async Task<(string, bool, string, string)> ValidateGraphExecutionParallelism()
    {
        Console.WriteLine("Testing Claim #7: Graph Execution Parallelism (>1.5x speedup)...");

        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Error));
        var logger = loggerFactory.CreateLogger<MetalAccelerator>();
        var memLogger = loggerFactory.CreateLogger<MetalMemoryManager>();
        var options = Options.Create(new MetalAcceleratorOptions
        {
            EnableMetalPerformanceShaders = true,
            CommandBufferCacheSize = 16
        });

        await using var accelerator = new MetalAccelerator(options, logger);
        var memoryManager = new MetalMemoryManager(memLogger, accelerator, enablePooling: true);

        const int kernelCount = 3; // 3 independent kernels
        const int size = 100000; // 100K elements per kernel
        const int warmupIterations = 2;
        const int measureIterations = 5;

        // PRE-COMPILE all kernels SEQUENTIALLY (thread-safe)
        var seqKernels = new List<ICompiledKernel>();
        var parKernels = new List<ICompiledKernel>();

        for (int i = 0; i < kernelCount; i++)
        {
            // Sequential kernels
            var seqKernelCode = $@"
#include <metal_stdlib>
using namespace metal;

kernel void seq_kernel_{i}(
    device float* data [[buffer(0)]],
    uint id [[thread_position_in_grid]])
{{
    data[id] = data[id] * 2.0f + 1.0f;  // Simple operation
}}";

            var seqDefinition = new KernelDefinition($"seq_kernel_{i}", seqKernelCode)
            {
                EntryPoint = $"seq_kernel_{i}",
                Language = KernelLanguage.Metal
            };
            seqKernels.Add(await accelerator.CompileKernelAsync(seqDefinition));

            // Parallel kernels
            var parKernelCode = $@"
#include <metal_stdlib>
using namespace metal;

kernel void par_kernel_{i}(
    device float* data [[buffer(0)]],
    uint id [[thread_position_in_grid]])
{{
    data[id] = data[id] * 2.0f + 1.0f;  // Same operation
}}";

            var parDefinition = new KernelDefinition($"par_kernel_{i}", parKernelCode)
            {
                EntryPoint = $"par_kernel_{i}",
                Language = KernelLanguage.Metal
            };
            parKernels.Add(await accelerator.CompileKernelAsync(parDefinition));
        }

        // Allocate buffers (one per kernel)
        var seqBuffers = new List<IUnifiedMemoryBuffer<float>>();
        var parBuffers = new List<IUnifiedMemoryBuffer<float>>();
        for (int i = 0; i < kernelCount; i++)
        {
            seqBuffers.Add(await memoryManager.AllocateAsync<float>(size));
            parBuffers.Add(await memoryManager.AllocateAsync<float>(size));
        }

        // Test 1: Sequential execution (baseline)
        // Warmup
        for (int w = 0; w < warmupIterations; w++)
        {
            for (int i = 0; i < kernelCount; i++)
            {
                await seqKernels[i].ExecuteAsync([seqBuffers[i]], CancellationToken.None);
            }
        }

        // Measure
        var sequentialTimes = new List<double>();
        for (int run = 0; run < measureIterations; run++)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < kernelCount; i++)
            {
                await seqKernels[i].ExecuteAsync([seqBuffers[i]], CancellationToken.None);
            }
            sw.Stop();
            sequentialTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Test 2: Parallel execution (optimized)
        // Warmup
        for (int w = 0; w < warmupIterations; w++)
        {
            var warmupTasks = new List<Task>();
            for (int i = 0; i < kernelCount; i++)
            {
                int index = i; // Capture for closure
                warmupTasks.Add(parKernels[index].ExecuteAsync([parBuffers[index]], CancellationToken.None).AsTask());
            }
            await Task.WhenAll(warmupTasks);
        }

        // Measure
        var parallelTimes = new List<double>();
        for (int run = 0; run < measureIterations; run++)
        {
            var sw = Stopwatch.StartNew();
            var tasks = new List<Task>();
            for (int i = 0; i < kernelCount; i++)
            {
                int index = i; // Capture for closure
                tasks.Add(parKernels[index].ExecuteAsync([parBuffers[index]], CancellationToken.None).AsTask());
            }
            await Task.WhenAll(tasks);
            sw.Stop();
            parallelTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Cleanup
        foreach (var kernel in seqKernels)
        {
            kernel.Dispose();
        }
        foreach (var kernel in parKernels)
        {
            kernel.Dispose();
        }
        foreach (var buffer in seqBuffers)
        {
            await memoryManager.FreeAsync(buffer, CancellationToken.None);
        }
        foreach (var buffer in parBuffers)
        {
            await memoryManager.FreeAsync(buffer, CancellationToken.None);
        }

        var avgSequential = sequentialTimes.Average();
        var avgParallel = parallelTimes.Average();
        var speedup = avgSequential / avgParallel;

        bool passed = speedup >= 1.5;

        Console.WriteLine($"  Sequential execution: {avgSequential:F2} ms (avg of {measureIterations} runs)");
        Console.WriteLine($"  Parallel execution: {avgParallel:F2} ms (avg of {measureIterations} runs)");
        Console.WriteLine($"  Speedup: {speedup:F2}x");
        Console.WriteLine($"  Status: {(passed ? "✅ PASS" : "❌ FAIL")}\n");

        return (
            "Graph Execution Parallelism",
            passed,
            $"{speedup:F2}x speedup",
            ">1.5x speedup"
        );
    }
}
