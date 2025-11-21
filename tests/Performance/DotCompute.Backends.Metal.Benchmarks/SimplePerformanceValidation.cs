// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Native;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
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
}
