using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.MPS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Backends.Metal.Benchmarks;

/// <summary>
/// Tests MPS performance at various matrix sizes to find crossover point
/// where MPS becomes faster than custom kernel.
/// </summary>
public static class MPSSizePerformanceTest
{
    public static async Task Run()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  MPS Performance vs Matrix Size Analysis");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        var sizes = new[] { 256, 512, 1024, 2048 };
        var results = new List<(int Size, double CustomMs, double MpsMs, double Speedup)>();

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
        var device = accelerator.Device;
        using var orchestrator = new MetalMPSOrchestrator(device, mpsLogger);

        foreach (var size in sizes)
        {
            Console.WriteLine($"Testing {size}x{size} matrices...");
            Console.WriteLine($"  Memory: {size * size * 4 * 3 / 1024 / 1024} MB");
            Console.WriteLine($"  GFLOP: {size * size * (long)size * 2 / 1e9:F2}");

            const int warmupIterations = 3;
            const int measureIterations = 5;

            // Test 1: Custom kernel
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

            var definition = new KernelDefinition($"matmul_{size}", kernelCode)
            {
                EntryPoint = "matmul",
                Language = KernelLanguage.Metal
            };

            // Pre-compile and allocate
            using var kernel = await accelerator.CompileKernelAsync(definition);
            var a = await memoryManager.AllocateAsync<float>(size * size);
            var b = await memoryManager.AllocateAsync<float>(size * size);
            var c = await memoryManager.AllocateAsync<float>(size * size);

            // Warmup custom
            for (int i = 0; i < warmupIterations; i++)
            {
                await kernel.ExecuteAsync([a, b, c, size], CancellationToken.None);
            }

            // Measure custom
            var customTimes = new List<double>();
            for (int i = 0; i < measureIterations; i++)
            {
                var sw = Stopwatch.StartNew();
                await kernel.ExecuteAsync([a, b, c, size], CancellationToken.None);
                sw.Stop();
                customTimes.Add(sw.Elapsed.TotalMilliseconds);
            }

            var avgCustom = customTimes.Average();

            // Cleanup
            await memoryManager.FreeAsync(a, CancellationToken.None);
            await memoryManager.FreeAsync(b, CancellationToken.None);
            await memoryManager.FreeAsync(c, CancellationToken.None);

            // Test 2: MPS
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

            // Measure MPS
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

            var avgMPS = mpsTimes.Average();
            var speedup = avgCustom / avgMPS;

            results.Add((size, avgCustom, avgMPS, speedup));

            Console.WriteLine($"  Custom kernel: {avgCustom:F2} ms");
            Console.WriteLine($"  MPS:           {avgMPS:F2} ms");
            Console.WriteLine($"  Speedup:       {speedup:F2}x {(speedup >= 1.0 ? "✅ MPS faster" : "❌ Custom faster")}\n");
        }

        // Summary
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  SUMMARY - MPS vs Custom Kernel Performance");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        Console.WriteLine("Size     | Custom (ms) | MPS (ms) | Speedup | Winner");
        Console.WriteLine("---------|-------------|----------|---------|-------------");
        foreach (var (size, custom, mps, speedup) in results)
        {
            var winner = speedup >= 1.0 ? "MPS ✅" : "Custom";
            Console.WriteLine($"{size,8} | {custom,11:F2} | {mps,8:F2} | {speedup,7:F2}x | {winner}");
        }

        Console.WriteLine();

        // Find crossover point
        var firstMpsWin = results.FirstOrDefault(r => r.Speedup >= 1.0);
        if (firstMpsWin != default)
        {
            Console.WriteLine($"✅ MPS crossover point: {firstMpsWin.Size}x{firstMpsWin.Size}");
            Console.WriteLine($"   MPS becomes faster at matrices ≥{firstMpsWin.Size}x{firstMpsWin.Size}");
        }
        else
        {
            Console.WriteLine("❌ MPS did not outperform custom kernel at any tested size");
            Console.WriteLine("   Recommendation: Test larger sizes (4096, 8192) or different operations");
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════════════\n");
    }
}
