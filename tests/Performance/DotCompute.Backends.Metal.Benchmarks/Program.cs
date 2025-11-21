// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Running;
using DotCompute.Backends.Metal.Benchmarks;

namespace DotCompute.Backends.Metal.Benchmarks;

/// <summary>
/// BenchmarkDotNet runner for Metal backend performance validation.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Check for simple validation flag (no BenchmarkDotNet)
        if (args.Length > 0 && args[0] == "--simple")
        {
            await SimplePerformanceValidation.Run();
            return;
        }

        // Check for debug cache test flag
        if (args.Length > 0 && args[0] == "--debug-cache")
        {
            await CacheDebugTest.RunTest();
            return;
        }

        // Check for manual cache benchmark flag
        if (args.Length > 0 && args[0] == "--manual-cache")
        {
            await ManualCacheBenchmark.RunBenchmark();
            return;
        }

        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  DotCompute Metal Backend Performance Validation Benchmarks");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("Validating Performance Claims:");
        Console.WriteLine("  1. Unified Memory: 2-3x speedup vs discrete memory");
        Console.WriteLine("  2. MPS Operations: 3-4x speedup vs custom kernels");
        Console.WriteLine("  3. Memory Pooling: 90% allocation reduction");
        Console.WriteLine("  4. Backend Init: Sub-10ms cold start");
        Console.WriteLine("  5. Kernel Compilation: <1ms cache hits");
        Console.WriteLine("  6. Command Queue: <100μs latency, >80% reuse");
        Console.WriteLine("  7. Graph Execution: >1.5x parallel speedup");
        Console.WriteLine();
        Console.WriteLine("Requirements:");
        Console.WriteLine("  - macOS with Metal support");
        Console.WriteLine("  - Apple Silicon (M1/M2/M3) for unified memory");
        Console.WriteLine("  - .NET 9.0 SDK");
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();

        // Verify platform
        if (!OperatingSystem.IsMacOS())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: Metal benchmarks require macOS.");
            Console.ResetColor();
            return;
        }

        _ = BenchmarkRunner.Run<MetalPerformanceBenchmarks>(args: args);

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  Benchmark Results Summary");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("Results saved to: BenchmarkDotNet.Artifacts/results/");
        Console.WriteLine();
        Console.WriteLine("Analysis:");
        Console.WriteLine("  - Compare baseline vs optimized benchmarks");
        Console.WriteLine("  - Verify speedup ratios match claimed values");
        Console.WriteLine("  - Check Debug.Assert statements in benchmark code");
        Console.WriteLine("  - Review BenchmarkDotNet detailed reports");
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }
}
