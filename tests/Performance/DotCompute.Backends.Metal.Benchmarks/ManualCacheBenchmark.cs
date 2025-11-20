// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Backends.Metal.Benchmarks;

/// <summary>
/// Manual cache benchmark that bypasses BenchmarkDotNet to avoid process isolation issues.
/// </summary>
public static class ManualCacheBenchmark
{
    public static async Task RunBenchmark()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  Metal Kernel Compilation Cache Performance Benchmark");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("This benchmark measures:");
        Console.WriteLine("  1. Cache MISS: First compilation of a kernel");
        Console.WriteLine("  2. Cache HIT:  Subsequent compilations of the same kernel");
        Console.WriteLine();
        Console.WriteLine("Target: Cache hits should be <1ms (>10x faster than miss)");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();

        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));

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

kernel void test_kernel(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    uint id [[thread_position_in_grid]])
{
    output[id] = input[id] * 2.0f;
}";

        var definition = new KernelDefinition("test_kernel", kernelCode)
        {
            EntryPoint = "test_kernel",
            Language = KernelLanguage.Metal
        };

        Console.WriteLine("Running warmup iterations...");
        Console.WriteLine();

        // Warmup: Compile 3 times to stabilize timings
        for (int i = 0; i < 3; i++)
        {
            var warmupKernel = await accelerator.CompileKernelAsync(definition);
            warmupKernel.Dispose();
        }

        Console.WriteLine("Starting benchmark measurements...");
        Console.WriteLine();

        // Measure cache misses (compile different kernels each time)
        var missTimes = new List<double>();
        Console.WriteLine("--- CACHE MISS Measurements ---");
        for (int i = 0; i < 10; i++)
        {
            // Create unique kernel each time to force cache miss
            var missKernelCode = @$"
#include <metal_stdlib>
using namespace metal;

kernel void test_kernel_{i}(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    uint id [[thread_position_in_grid]])
{{
    output[id] = input[id] * {i + 1}.0f;
}}";

            var missDefinition = new KernelDefinition($"test_kernel_{i}", missKernelCode)
            {
                EntryPoint = $"test_kernel_{i}",
                Language = KernelLanguage.Metal
            };

            var sw = Stopwatch.StartNew();
            var missKernel = await accelerator.CompileKernelAsync(missDefinition);
            sw.Stop();

            missTimes.Add(sw.Elapsed.TotalMilliseconds);
            Console.WriteLine($"  Iteration {i + 1}: {sw.Elapsed.TotalMilliseconds:F3} ms");

            missKernel.Dispose();
        }

        Console.WriteLine();
        Console.WriteLine("--- CACHE HIT Measurements ---");

        // Measure cache hits (compile same kernel repeatedly)
        var hitTimes = new List<double>();
        for (int i = 0; i < 10; i++)
        {
            var sw = Stopwatch.StartNew();
            var hitKernel = await accelerator.CompileKernelAsync(definition);
            sw.Stop();

            hitTimes.Add(sw.Elapsed.TotalMilliseconds);
            Console.WriteLine($"  Iteration {i + 1}: {sw.Elapsed.TotalMilliseconds:F3} ms");

            hitKernel.Dispose();
        }

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  RESULTS");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();

        var avgMiss = missTimes.Average();
        var avgHit = hitTimes.Average();
        var minMiss = missTimes.Min();
        var minHit = hitTimes.Min();
        var speedup = avgMiss / avgHit;

        Console.WriteLine($"Cache MISS (new kernel compilation):");
        Console.WriteLine($"  Average: {avgMiss:F3} ms");
        Console.WriteLine($"  Minimum: {minMiss:F3} ms");
        Console.WriteLine($"  StdDev:  {CalculateStdDev(missTimes):F3} ms");
        Console.WriteLine();

        Console.WriteLine($"Cache HIT (retrieve from cache):");
        Console.WriteLine($"  Average: {avgHit:F3} ms");
        Console.WriteLine($"  Minimum: {minHit:F3} ms");
        Console.WriteLine($"  StdDev:  {CalculateStdDev(hitTimes):F3} ms");
        Console.WriteLine();

        Console.WriteLine($"Speedup: {speedup:F2}x faster");
        Console.WriteLine();

        // Verify against target
        var target = 1.0; // <1ms target
        var targetSpeedup = 10.0; // >10x speedup target

        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  VALIDATION");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();

        var hitTargetMet = avgHit < target;
        var speedupTargetMet = speedup > targetSpeedup;

        Console.WriteLine($"Cache Hit Time < {target} ms:    {(hitTargetMet ? "✓ PASS" : "✗ FAIL")} ({avgHit:F3} ms)");
        Console.WriteLine($"Speedup > {targetSpeedup}x:            {(speedupTargetMet ? "✓ PASS" : "✗ FAIL")} ({speedup:F2}x)");
        Console.WriteLine();

        if (hitTargetMet && speedupTargetMet)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ All performance targets MET!");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ Some performance targets NOT met");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
    }

    private static double CalculateStdDev(List<double> values)
    {
        var avg = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }
}
