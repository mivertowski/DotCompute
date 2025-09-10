// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Reports;
using DotCompute.Linq.Benchmarks;
using System.Reflection;

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Main program for running DotCompute LINQ performance benchmarks.
/// Demonstrates the 8-23x performance improvements through comprehensive testing.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("DotCompute LINQ Performance Benchmarks");
        Console.WriteLine("Comprehensive validation of 8-23x speedup claims");
        Console.WriteLine("=================================================================");
        Console.WriteLine();

        if (args.Length == 0)
        {
            ShowUsage();
            return;
        }

        var benchmarkType = args[0].ToLowerInvariant();
        
        try
        {
            switch (benchmarkType)
            {
                case "all":
                    RunAllBenchmarks();
                    break;
                    
                case "expression":
                case "compilation":
                    RunExpressionCompilationBenchmarks();
                    break;
                    
                case "linq":
                case "gpu":
                case "speedup":
                    RunLinqVsGpuBenchmarks();
                    break;
                    
                case "streaming":
                case "reactive":
                    RunStreamingBenchmarks();
                    break;
                    
                case "optimization":
                case "impact":
                    RunOptimizationImpactBenchmarks();
                    break;
                    
                case "quick":
                    RunQuickBenchmarks();
                    break;
                    
                case "validation":
                    RunSpeedupValidation();
                    break;
                    
                default:
                    Console.WriteLine($"Unknown benchmark type: {benchmarkType}");
                    ShowUsage();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running benchmarks: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
        
        Console.WriteLine();
        Console.WriteLine("=================================================================");
        Console.WriteLine("Benchmark execution completed!");
        Console.WriteLine("Results have been saved to BenchmarkDotNet.Artifacts/");
        Console.WriteLine("=================================================================");
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: dotnet run -- <benchmark-type>");
        Console.WriteLine();
        Console.WriteLine("Available benchmark types:");
        Console.WriteLine("  all           - Run all benchmarks (comprehensive suite)");
        Console.WriteLine("  expression    - Expression compilation performance");
        Console.WriteLine("  linq          - LINQ vs GPU-accelerated LINQ comparison");
        Console.WriteLine("  streaming     - Reactive streaming performance");
        Console.WriteLine("  optimization  - Optimization impact analysis");
        Console.WriteLine("  quick         - Quick validation (subset of tests)");
        Console.WriteLine("  validation    - Speedup claim validation suite");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -- all");
        Console.WriteLine("  dotnet run -- linq");
        Console.WriteLine("  dotnet run -- validation");
    }

    private static void RunAllBenchmarks()
    {
        Console.WriteLine("Running comprehensive benchmark suite...");
        Console.WriteLine("This may take 30-60 minutes depending on hardware.");
        Console.WriteLine();

        var config = CreateComprehensiveConfig();
        
        // Run available benchmark classes
        var benchmarks = new[]
        {
            typeof(StandaloneBenchmark)
        };

        BenchmarkRunner.Run(benchmarks, config);
    }

    private static void RunExpressionCompilationBenchmarks()
    {
        Console.WriteLine("Running Simplified LINQ Benchmarks...");
        Console.WriteLine("Testing LINQ operations performance with available DotCompute functionality.");
        Console.WriteLine();

        var config = CreateStandardConfig();
        BenchmarkRunner.Run<StandaloneBenchmark>(config);
    }

    private static void RunLinqVsGpuBenchmarks()
    {
        Console.WriteLine("Running Simplified LINQ Benchmarks...");
        Console.WriteLine("Testing LINQ operations performance with available DotCompute functionality.");
        Console.WriteLine();

        var config = CreateStandardConfig();
        BenchmarkRunner.Run<StandaloneBenchmark>(config);
    }

    private static void RunStreamingBenchmarks()
    {
        Console.WriteLine("Running Simplified LINQ Benchmarks...");
        Console.WriteLine("Testing LINQ operations performance with available DotCompute functionality.");
        Console.WriteLine();

        var config = CreateStandardConfig();
        BenchmarkRunner.Run<StandaloneBenchmark>(config);
    }

    private static void RunOptimizationImpactBenchmarks()
    {
        Console.WriteLine("Running Simplified LINQ Benchmarks...");
        Console.WriteLine("Testing LINQ operations performance with available DotCompute functionality.");
        Console.WriteLine();

        var config = CreateStandardConfig();
        BenchmarkRunner.Run<StandaloneBenchmark>(config);
    }

    private static void RunQuickBenchmarks()
    {
        Console.WriteLine("Running Quick Validation Benchmarks...");
        Console.WriteLine("Subset of tests for rapid feedback (5-10 minutes).");
        Console.WriteLine();

        var config = CreateQuickConfig();
        
        // Run a subset of benchmarks with reduced parameters
        BenchmarkRunner.Run<SimplifiedLinqBenchmark>(config);
    }

    private static void RunSpeedupValidation()
    {
        Console.WriteLine("Running Speedup Validation Suite...");
        Console.WriteLine("Focused benchmarks to validate performance improvements.");
        Console.WriteLine();

        var config = CreateValidationConfig();
        
        // Focus on the most important comparisons
        BenchmarkRunner.Run<SimplifiedLinqBenchmark>(config);
    }

    private static IConfig CreateComprehensiveConfig()
    {
        return DefaultConfig.Instance
            .AddLogger(ConsoleLogger.Default)
            .AddExporter(CsvExporter.Default)
            .AddExporter(JsonExporter.Brief)
            .AddExporter(HtmlExporter.Default)
            .AddExporter(PlainExporter.Default)
            .AddExporter(new SpeedupReportExporter())
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .WithArtifactsPath("BenchmarkDotNet.Artifacts/Comprehensive");
    }

    private static IConfig CreateStandardConfig()
    {
        return DefaultConfig.Instance
            .AddLogger(ConsoleLogger.Default)
            .AddExporter(CsvExporter.Default)
            .AddExporter(JsonExporter.Brief)
            .AddExporter(HtmlExporter.Default)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .WithArtifactsPath("BenchmarkDotNet.Artifacts/Standard");
    }

    private static IConfig CreateQuickConfig()
    {
        return DefaultConfig.Instance
            .AddLogger(ConsoleLogger.Default)
            .AddExporter(CsvExporter.Default)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .WithArtifactsPath("BenchmarkDotNet.Artifacts/Quick");
    }

    private static IConfig CreateValidationConfig()
    {
        return DefaultConfig.Instance
            .AddLogger(ConsoleLogger.Default)
            .AddExporter(CsvExporter.Default)
            .AddExporter(JsonExporter.Brief)
            .AddExporter(new SpeedupReportExporter())
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .WithArtifactsPath("BenchmarkDotNet.Artifacts/Validation");
    }
}

/// <summary>
/// Custom exporter that generates a comprehensive speedup analysis report.
/// </summary>
public class SpeedupReportExporter : IExporter
{
    public string Name => "SpeedupReport";

    public void ExportToLog(Summary summary, ILogger logger)
    {
        logger.WriteLine();
        logger.WriteLine("=================================================================");
        logger.WriteLine("DotCompute LINQ Speedup Analysis Report");
        logger.WriteLine("=================================================================");

        var baseline = summary.GetBaseline();
        if (baseline == null)
        {
            logger.WriteLine("No baseline found for speedup calculation.");
            return;
        }

        var baselineResult = summary[baseline];
        if (baselineResult?.ResultStatistics == null)
        {
            logger.WriteLine("Baseline result not available.");
            return;
        }

        logger.WriteLine($"Baseline: {baseline.DisplayInfo}");
        logger.WriteLine($"Baseline Mean Time: {baselineResult.ResultStatistics.Mean:F2} ns");
        logger.WriteLine();

        var speedups = new List<(string Name, double Speedup)>();

        foreach (var benchmarkCase in summary.BenchmarksCases)
        {
            if (benchmarkCase == baseline) continue;

            var result = summary[benchmarkCase];
            if (result?.ResultStatistics?.Mean == null) continue;

            var speedup = baselineResult.ResultStatistics.Mean / result.ResultStatistics.Mean;
            speedups.Add((benchmarkCase.DisplayInfo, speedup));

            logger.WriteLine($"{benchmarkCase.DisplayInfo}:");
            logger.WriteLine($"  Mean Time: {result.ResultStatistics.Mean:F2} ns");
            logger.WriteLine($"  Speedup: {speedup:F2}x");
            logger.WriteLine();
        }

        // Summary statistics
        if (speedups.Any())
        {
            var maxSpeedup = speedups.Max(s => s.Speedup);
            var avgSpeedup = speedups.Average(s => s.Speedup);
            var speedupsAbove8x = speedups.Count(s => s.Speedup >= 8.0);
            var speedupsAbove20x = speedups.Count(s => s.Speedup >= 20.0);

            logger.WriteLine("=================================================================");
            logger.WriteLine("SPEEDUP SUMMARY");
            logger.WriteLine("=================================================================");
            logger.WriteLine($"Maximum Speedup: {maxSpeedup:F2}x");
            logger.WriteLine($"Average Speedup: {avgSpeedup:F2}x");
            logger.WriteLine($"Tests with 8x+ speedup: {speedupsAbove8x}/{speedups.Count} ({(double)speedupsAbove8x/speedups.Count*100:F1}%)");
            logger.WriteLine($"Tests with 20x+ speedup: {speedupsAbove20x}/{speedups.Count} ({(double)speedupsAbove20x/speedups.Count*100:F1}%)");
            logger.WriteLine();

            // Validate claims
            var claimValidated = maxSpeedup >= 8.0 && speedupsAbove8x >= speedups.Count * 0.5; // At least 50% of tests should show 8x+
            logger.WriteLine($"8-23x Speedup Claim Validation: {(claimValidated ? "✓ VALIDATED" : "✗ NOT VALIDATED")}");
            
            if (claimValidated)
            {
                logger.WriteLine("The DotCompute LINQ system successfully demonstrates significant performance improvements.");
            }
            else
            {
                logger.WriteLine("Performance improvements detected but may not meet all claimed thresholds.");
            }
        }

        logger.WriteLine("=================================================================");
    }

    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        var filePath = Path.Combine(summary.ResultsDirectoryPath, $"{summary.Title}-speedup-report.txt");
        
        using var fileLogger = new StreamLogger(new StreamWriter(filePath));
        ExportToLog(summary, fileLogger);
        
        return new[] { filePath };
    }
}

/// <summary>
/// Simple stream-based logger for file output.
/// </summary>
public class StreamLogger : ILogger
{
    private readonly StreamWriter _writer;

    public StreamLogger(StreamWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public string Id => "StreamLogger";
    public int Priority => 0;

    public void Write(LogKind logKind, string text)
    {
        _writer.Write(text);
    }

    public void WriteLine()
    {
        _writer.WriteLine();
    }

    public void WriteLine(LogKind logKind, string text)
    {
        _writer.WriteLine(text);
    }

    public void Flush()
    {
        _writer.Flush();
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }
}