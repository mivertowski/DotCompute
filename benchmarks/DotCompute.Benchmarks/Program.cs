using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using System.Reflection;

namespace DotCompute.Benchmarks;

/// <summary>
/// Production-quality BenchmarkDotNet runner for DotCompute performance benchmarks.
/// Provides comprehensive benchmarking capabilities with multiple configuration profiles.
/// </summary>
public class Program
{
    private static readonly Dictionary<string, Type> BenchmarkSuites = new()
    {
        ["memory"] = typeof(MemoryBenchmarks),
        ["cuda"] = typeof(CudaBenchmarks),
        ["cpu"] = typeof(CpuBenchmarks),
        ["algorithms"] = typeof(AlgorithmBenchmarks),
        ["all"] = typeof(AllBenchmarks)
    };

    public static int Main(string[] args)
    {
        try
        {
            Console.WriteLine("🚀 DotCompute Benchmark Suite");
            Console.WriteLine("================================");
            
            if (args.Length == 0)
            {
                return RunInteractiveMode();
            }
            
            return RunCommandLineMode(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Benchmark execution failed: {ex.Message}");
            return 1;
        }
    }

    private static int RunInteractiveMode()
    {
        Console.WriteLine("Available benchmark suites:");
        foreach (var suite in BenchmarkSuites.Keys.Where(k => k != "all"))
        {
            Console.WriteLine($"  • {suite}");
        }
        Console.WriteLine("  • all (runs all suites)");
        Console.WriteLine();

        Console.Write("Select benchmark suite (or 'q' to quit): ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        
        if (string.IsNullOrEmpty(input) || input == "q")
        {
            return 0;
        }

        if (!BenchmarkSuites.TryGetValue(input, out var benchmarkType))
        {
            Console.WriteLine("❌ Invalid suite selection");
            return 1;
        }

        return RunBenchmarks(benchmarkType, GetDefaultConfig());
    }

    private static int RunCommandLineMode(string[] args)
    {
        var suite = args[0].ToLowerInvariant();
        var profile = args.Length > 1 ? args[1].ToLowerInvariant() : "default";
        
        if (!BenchmarkSuites.TryGetValue(suite, out var benchmarkType))
        {
            Console.WriteLine($"❌ Unknown benchmark suite: {suite}");
            ShowUsage();
            return 1;
        }

        var config = GetConfigurationProfile(profile);
        return RunBenchmarks(benchmarkType, config);
    }

    private static int RunBenchmarks(Type benchmarkType, IConfig config)
    {
        try
        {
            if (benchmarkType == typeof(AllBenchmarks))
            {
                return RunAllBenchmarks(config);
            }

            var summary = BenchmarkRunner.Run(benchmarkType, config);
            
            Console.WriteLine();
            Console.WriteLine("📊 Benchmark Summary");
            Console.WriteLine($"Total benchmarks: {summary.BenchmarksCases.Length}");
            Console.WriteLine($"Successful runs: {summary.Reports.Count(r => r.Success)}");
            Console.WriteLine($"Failed runs: {summary.Reports.Count(r => !r.Success)}");

            return summary.HasCriticalValidationErrors ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Benchmark execution failed: {ex.Message}");
            return 1;
        }
    }

    private static int RunAllBenchmarks(IConfig config)
    {
        var allResults = new List<bool>();
        
        foreach (var suite in BenchmarkSuites.Where(kv => kv.Key != "all"))
        {
            Console.WriteLine($"\n🏃 Running {suite.Key} benchmarks...");
            try
            {
                var summary = BenchmarkRunner.Run(suite.Value, config);
                allResults.Add(!summary.HasCriticalValidationErrors);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to run {suite.Key} benchmarks: {ex.Message}");
                allResults.Add(false);
            }
        }

        var successCount = allResults.Count(r => r);
        var totalCount = allResults.Count;
        
        Console.WriteLine($"\n📊 Overall Results: {successCount}/{totalCount} suites completed successfully");
        
        return allResults.All(r => r) ? 0 : 1;
    }

    private static IConfig GetDefaultConfig()
    {
        return DefaultConfig.Instance
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(ThreadingDiagnoser.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(HtmlExporter.Default)
            .AddLogger(ConsoleLogger.Default)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }

    private static IConfig GetConfigurationProfile(string profile) => profile switch
    {
        "fast" => GetFastConfig(),
        "detailed" => GetDetailedConfig(),
        "production" => GetProductionConfig(),
        "ci" => GetContinuousIntegrationConfig(),
        _ => GetDefaultConfig()
    };

    private static IConfig GetFastConfig()
    {
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default.WithStrategy(RunStrategy.ColdStart).WithLaunchCount(1).WithIterationCount(3))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddExporter(MarkdownExporter.Console)
            .AddLogger(ConsoleLogger.Default)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }

    private static IConfig GetDetailedConfig()
    {
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default.WithRuntime(CoreRuntime.Core90))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(ThreadingDiagnoser.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(HtmlExporter.Default)
            .AddExporter(MarkdownExporter.Console)
            .AddLogger(ConsoleLogger.Default)
            .AddColumn(StatisticColumn.Mean, StatisticColumn.Error, StatisticColumn.StdDev)
            .AddColumn(RankColumn.Arabic)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }

    private static IConfig GetProductionConfig()
    {
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .WithRuntime(CoreRuntime.Core90)
                .WithStrategy(RunStrategy.Throughput)
                .WithLaunchCount(3)
                .WithIterationCount(10))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(ThreadingDiagnoser.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(HtmlExporter.Default)
            .AddExporter(MarkdownExporter.Console)
            .AddLogger(ConsoleLogger.Default)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }

    private static IConfig GetContinuousIntegrationConfig()
    {
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default.WithStrategy(RunStrategy.ColdStart).WithLaunchCount(1))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddExporter(MarkdownExporter.Console)
            .AddExporter(MarkdownExporter.Console)
            .AddLogger(ConsoleLogger.Default)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator | ConfigOptions.StopOnFirstError);
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: DotCompute.Benchmarks <suite> [profile]");
        Console.WriteLine();
        Console.WriteLine("Suites:");
        foreach (var suite in BenchmarkSuites.Keys)
        {
            Console.WriteLine($"  {suite}");
        }
        Console.WriteLine();
        Console.WriteLine("Profiles:");
        Console.WriteLine("  default  - Standard benchmarking configuration");
        Console.WriteLine("  fast     - Quick benchmarks for development");
        Console.WriteLine("  detailed - Comprehensive analysis with multiple exporters");
        Console.WriteLine("  production - High-precision benchmarks for release");
        Console.WriteLine("  ci       - Optimized for continuous integration");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  DotCompute.Benchmarks memory");
        Console.WriteLine("  DotCompute.Benchmarks cuda production");
        Console.WriteLine("  DotCompute.Benchmarks all detailed");
    }
}

/// <summary>
/// Placeholder for running all benchmark suites
/// </summary>
public class AllBenchmarks
{
    // This class is used as a marker for running all benchmarks
}