// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Backends.Metal.Benchmarks;

/// <summary>
/// Simple test to debug cache behavior without BenchmarkDotNet complexity.
/// </summary>
public static class CacheDebugTest
{
    public static async Task RunTest()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("  Metal Cache Debug Test");
        Console.WriteLine("═══════════════════════════════════════");

        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning)); // Show warnings to see debug logs

        var logger = loggerFactory.CreateLogger<MetalAccelerator>();

        var options = Options.Create(new MetalAcceleratorOptions
        {
            EnableMetalPerformanceShaders = true,
            CommandBufferCacheSize = 16
        });

        await using var accelerator = new MetalAccelerator(options, logger);

        // Test 1: First compilation (cache miss)
        Console.WriteLine("\n--- Test 1: First Compilation (Cache Miss) ---");
        const string kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void cached_test_kernel(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    uint id [[thread_position_in_grid]])
{
    output[id] = input[id] * 2.0f;
}";

        var definition = new KernelDefinition("cached_test_kernel", kernelCode)
        {
            EntryPoint = "cached_test_kernel",
            Language = KernelLanguage.Metal
        };

        Console.WriteLine("Compiling kernel for the first time...");
        var kernel1 = await accelerator.CompileKernelAsync(definition);
        Console.WriteLine("✓ First compilation complete");

        // Test 2: Second compilation (should be cache hit)
        Console.WriteLine("\n--- Test 2: Second Compilation (Cache Hit Expected) ---");
        Console.WriteLine("Compiling same kernel again...");
        var kernel2 = await accelerator.CompileKernelAsync(definition);
        Console.WriteLine("✓ Second compilation complete");

        // Test 3: Third compilation (should also be cache hit)
        Console.WriteLine("\n--- Test 3: Third Compilation (Cache Hit Expected) ---");
        var kernel3 = await accelerator.CompileKernelAsync(definition);
        Console.WriteLine("✓ Third compilation complete");

        // Cleanup
        kernel1.Dispose();
        kernel2.Dispose();
        kernel3.Dispose();

        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("  Test Complete - Check logs above");
        Console.WriteLine("═══════════════════════════════════════");
    }
}
