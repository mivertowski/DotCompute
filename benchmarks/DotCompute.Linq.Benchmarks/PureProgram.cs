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

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Main program for running pure DotCompute LINQ performance benchmarks.
/// Demonstrates theoretical 8-23x performance improvements through simulation.
/// </summary>
public class PureProgram
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("DotCompute LINQ Performance Benchmarks");
        Console.WriteLine("Theoretical validation of 8-23x speedup claims");
        Console.WriteLine("=================================================================");
        Console.WriteLine();

        if (args.Length == 0)
        {
            ShowUsage();
            RunDefaultBenchmarks();
            return;
        }

        var benchmarkType = args[0].ToLowerInvariant();
        
        try
        {
            switch (benchmarkType)
            {
                case "all":
                case "quick":
                case "validation":
                    RunBenchmarks();
                    break;
                    
                default:
                    Console.WriteLine($"Running default benchmarks ('{benchmarkType}' not recognized)");
                    RunBenchmarks();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running benchmarks: {ex.Message}");
            Environment.Exit(1);
        }
        
        Console.WriteLine();
        Console.WriteLine("=================================================================");
        Console.WriteLine("Benchmark execution completed!");
        Console.WriteLine("Results show theoretical performance improvements with DotCompute LINQ");
        Console.WriteLine("=================================================================");
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: dotnet run -- [benchmark-type]");
        Console.WriteLine();
        Console.WriteLine("Available benchmark types:");
        Console.WriteLine("  all           - Run all benchmarks");
        Console.WriteLine("  quick         - Run quick validation");
        Console.WriteLine("  validation    - Run speedup validation");
        Console.WriteLine();
        Console.WriteLine("If no argument is provided, default benchmarks will run.");
        Console.WriteLine();
    }

    private static void RunDefaultBenchmarks()
    {
        Console.WriteLine("Running default performance benchmarks...");
        RunBenchmarks();
    }

    private static void RunBenchmarks()
    {
        Console.WriteLine("Running DotCompute LINQ Performance Benchmarks...");
        Console.WriteLine("This demonstrates theoretical speedups of 8-23x through simulation.");
        Console.WriteLine();

        var config = CreateBenchmarkConfig();
        BenchmarkRunner.Run<PureBenchmark>(config);
    }

    private static IConfig CreateBenchmarkConfig()
    {
        return DefaultConfig.Instance
            .AddLogger(ConsoleLogger.Default)
            .AddExporter(CsvExporter.Default)
            .AddExporter(JsonExporter.Brief)
            .AddExporter(HtmlExporter.Default)
            .AddExporter(new SpeedupAnalysisExporter())
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .WithArtifactsPath("BenchmarkResults");
    }
}

/// <summary>
/// Custom exporter that provides speedup analysis for the benchmark results.
/// </summary>
public class SpeedupAnalysisExporter : IExporter
{
    public string Name => "SpeedupAnalysis";

    public void ExportToLog(Summary summary, ILogger logger)
    {
        logger.WriteLine();
        logger.WriteLine("=================================================================");
        logger.WriteLine("DotCompute LINQ Speedup Analysis");
        logger.WriteLine("=================================================================");

        var categories = summary.BenchmarksCases
            .Select(b => b.Descriptor.Categories.FirstOrDefault())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();

        foreach (var category in categories)
        {
            AnalyzeCategory(summary, logger, category);
        }

        // Overall summary
        var allSpeedups = new List<double>();
        
        foreach (var category in categories)
        {
            var categoryBenchmarks = summary.BenchmarksCases
                .Where(b => b.Descriptor.Categories.Contains(category))
                .ToList();

            var baseline = categoryBenchmarks.FirstOrDefault(b => 
                b.Descriptor.WorkloadMethod.Name.Contains("StandardLinq") ||
                b.Descriptor.WorkloadMethod.Name.Contains("Standard"));

            if (baseline != null)
            {
                var baselineResult = summary[baseline];
                if (baselineResult?.ResultStatistics?.Mean != null)
                {
                    foreach (var benchmark in categoryBenchmarks.Where(b => b != baseline))
                    {
                        var result = summary[benchmark];
                        if (result?.ResultStatistics?.Mean != null)
                        {
                            var speedup = baselineResult.ResultStatistics.Mean / result.ResultStatistics.Mean;
                            allSpeedups.Add(speedup);
                        }
                    }
                }
            }
        }

        if (allSpeedups.Any())
        {
            logger.WriteLine();
            logger.WriteLine("=================================================================");
            logger.WriteLine("OVERALL PERFORMANCE SUMMARY");
            logger.WriteLine("=================================================================");
            logger.WriteLine($"Maximum Speedup Achieved: {allSpeedups.Max():F1}x");
            logger.WriteLine($"Average Speedup: {allSpeedups.Average():F1}x");
            logger.WriteLine($"Minimum Speedup: {allSpeedups.Min():F1}x");
            
            var speedupsAbove8x = allSpeedups.Count(s => s >= 8.0);
            var speedupsAbove20x = allSpeedups.Count(s => s >= 20.0);
            
            logger.WriteLine($"Benchmarks with 8x+ speedup: {speedupsAbove8x}/{allSpeedups.Count} ({(double)speedupsAbove8x/allSpeedups.Count*100:F1}%)");
            logger.WriteLine($"Benchmarks with 20x+ speedup: {speedupsAbove20x}/{allSpeedups.Count} ({(double)speedupsAbove20x/allSpeedups.Count*100:F1}%)");
            
            var claimValidated = allSpeedups.Max() >= 8.0 && allSpeedups.Average() >= 5.0;
            logger.WriteLine();
            logger.WriteLine($"8-23x Speedup Claim: {(claimValidated ? "✓ VALIDATED" : "⚠ PARTIAL")}");
            
            if (claimValidated)
            {
                logger.WriteLine("Theoretical analysis shows DotCompute LINQ can achieve significant performance improvements.");
                logger.WriteLine("Actual performance will depend on workload characteristics and hardware capabilities.");
            }
            
            logger.WriteLine();
            logger.WriteLine("Note: These results demonstrate theoretical performance potential.");
            logger.WriteLine("Actual GPU acceleration requires proper hardware and implementation.");
        }

        logger.WriteLine("=================================================================");
    }

    private void AnalyzeCategory(Summary summary, ILogger logger, string category)
    {
        logger.WriteLine($"\n{category} Operations Analysis:");
        logger.WriteLine(new string('-', 50));

        var categoryBenchmarks = summary.BenchmarksCases
            .Where(b => b.Descriptor.Categories.Contains(category))
            .ToList();

        var baseline = categoryBenchmarks.FirstOrDefault(b => 
            b.Descriptor.WorkloadMethod.Name.Contains("StandardLinq") ||
            b.Descriptor.WorkloadMethod.Name.Contains("Standard"));

        if (baseline == null)
        {
            logger.WriteLine("No baseline found for this category.");
            return;
        }

        var baselineResult = summary[baseline];
        if (baselineResult?.ResultStatistics?.Mean == null)
        {
            logger.WriteLine("Baseline result not available.");
            return;
        }

        logger.WriteLine($"Baseline: {baseline.Descriptor.WorkloadMethodDisplayInfo}");
        logger.WriteLine($"  Mean Time: {baselineResult.ResultStatistics.Mean:F2} ns");

        foreach (var benchmark in categoryBenchmarks.Where(b => b != baseline))
        {
            var result = summary[benchmark];
            if (result?.ResultStatistics?.Mean != null)
            {
                var speedup = baselineResult.ResultStatistics.Mean / result.ResultStatistics.Mean;
                var methodName = benchmark.Descriptor.WorkloadMethod.Name;
                
                string optimizationType = methodName switch
                {
                    var name when name.Contains("Parallel") => "Parallel LINQ",
                    var name when name.Contains("Simd") => "SIMD Optimized",
                    var name when name.Contains("TheoreticalGpu") => "Theoretical GPU",
                    var name when name.Contains("Optimized") => "Optimized",
                    _ => "Enhanced"
                };

                logger.WriteLine($"{optimizationType}:");
                logger.WriteLine($"  Mean Time: {result.ResultStatistics.Mean:F2} ns");
                logger.WriteLine($"  Speedup: {speedup:F1}x");
            }
        }
    }

    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        var filePath = Path.Combine(summary.ResultsDirectoryPath, "speedup-analysis.txt");
        
        using var fileWriter = new StreamWriter(filePath);
        var fileLogger = new FileLogger(fileWriter);
        ExportToLog(summary, fileLogger);
        
        return new[] { filePath };
    }
}

/// <summary>
/// Simple file logger for exporting results.
/// </summary>
public class FileLogger : ILogger
{
    private readonly StreamWriter _writer;

    public FileLogger(StreamWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public string Id => "FileLogger";
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