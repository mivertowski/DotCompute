// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Running;
using DotCompute.Performance.Tests;

namespace DotCompute.Performance.Tests;

/// <summary>
/// Entry point for running DotCompute performance benchmarks
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("DotCompute Performance Benchmarks");
        Console.WriteLine("=================================");
        Console.WriteLine();
        
        if (args.Length > 0 && args[0] == "--benchmarkdotnet")
        {
            // Run with BenchmarkDotNet for detailed performance analysis
            Console.WriteLine("Running with BenchmarkDotNet...");
            // Uncomment the following lines when BenchmarkDotNet attributes are properly set up:
            // var summary = BenchmarkRunner.Run<KernelPerformanceTests>();
            // Console.WriteLine(summary);
        }
        else
        {
            Console.WriteLine("Performance tests are designed to run with xUnit.");
            Console.WriteLine("Use the following commands:");
            Console.WriteLine();
            Console.WriteLine("  dotnet test --logger trx --results-directory ./TestResults");
            Console.WriteLine("  dotnet test --filter \"Category=Performance\"");
            Console.WriteLine("  dotnet test --filter \"Category=Benchmark\"");
            Console.WriteLine("  dotnet test --filter \"Category=MemoryIntensive\"");
            Console.WriteLine();
            Console.WriteLine("Test Categories Available:");
            Console.WriteLine("  - Performance: General performance tests");
            Console.WriteLine("  - Benchmark: Comparative benchmarks");
            Console.WriteLine("  - MemoryIntensive: Memory performance tests");
            Console.WriteLine("  - LongRunning: Extended scalability tests");
            Console.WriteLine("  - Concurrency: Multi-threading tests");
            Console.WriteLine();
            Console.WriteLine("For BenchmarkDotNet integration, run with --benchmarkdotnet flag.");
        }
        
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
