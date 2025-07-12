// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Engines;

namespace DotCompute.Performance.Benchmarks.Config;

public static class BenchmarkConfig
{
    public static IConfig GetDefaultConfig()
    {
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(GetDefaultJob())
            .AddJob(GetOptimizedJob())
            .AddJob(GetDebugJob())
            .AddDiagnoser(GetDiagnosers())
            .AddExporter(GetExporters())
            .AddLogger(GetLoggers())
            .AddColumn(GetColumns())
            .WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));
    }

    public static IConfig GetMemoryIntensiveConfig()
    {
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .WithPlatform(Platform.X64)
                .WithRuntime(CoreRuntime.Core80)
                .WithGcServer(true)
                .WithGcConcurrent(true)
                .WithGcForce(false)
                .WithStrategy(RunStrategy.Throughput))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(ThreadingDiagnoser.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(HtmlExporter.Default)
            .AddExporter(JsonExporter.Full)
            .AddColumn(StatisticColumn.Mean)
            .AddColumn(StatisticColumn.Median)
            .AddColumn(StatisticColumn.StdDev)
            .AddColumn(BaselineRatioColumn.RatioMean)
            .AddColumn(RankColumn.Arabic);
    }

    public static IConfig GetVectorizationConfig()
    {
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .WithPlatform(Platform.X64)
                .WithRuntime(CoreRuntime.Core80)
                .WithGcServer(true)
                .WithEnvironmentVariable("DOTNET_EnableAVX2", "1")
                .WithEnvironmentVariable("DOTNET_EnableAES", "1")
                .WithEnvironmentVariable("DOTNET_EnableBMI1", "1")
                .WithEnvironmentVariable("DOTNET_EnableBMI2", "1")
                .WithEnvironmentVariable("DOTNET_EnableFMA", "1")
                .WithEnvironmentVariable("DOTNET_EnableLZCNT", "1")
                .WithEnvironmentVariable("DOTNET_EnablePCLMULQDQ", "1")
                .WithEnvironmentVariable("DOTNET_EnablePOPCNT", "1")
                .WithEnvironmentVariable("DOTNET_EnableSSE3", "1")
                .WithEnvironmentVariable("DOTNET_EnableSSE41", "1")
                .WithEnvironmentVariable("DOTNET_EnableSSE42", "1")
                .WithEnvironmentVariable("DOTNET_EnableSSSE3", "1"))
            .AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(
                printSource: true,
                printInstructionAddresses: true,
                exportGithubMarkdown: true,
                exportHtml: true,
                exportCombinedDisassemblyReport: true,
                exportDiff: true)))
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(HtmlExporter.Default)
            .AddColumn(StatisticColumn.Mean)
            .AddColumn(BaselineRatioColumn.RatioMean)
            .AddColumn(RankColumn.Arabic);
    }

    public static IConfig GetThroughputConfig()
    {
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .WithPlatform(Platform.X64)
                .WithRuntime(CoreRuntime.Core80)
                .WithGcServer(true)
                .WithGcConcurrent(true)
                .WithStrategy(RunStrategy.Throughput)
                .WithLaunchCount(1)
                .WithWarmupCount(5)
                .WithIterationCount(15))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(ThreadingDiagnoser.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(JsonExporter.Full)
            .AddColumn(StatisticColumn.Mean)
            .AddColumn(StatisticColumn.Throughput)
            .AddColumn(BaselineRatioColumn.RatioMean);
    }

    public static IConfig GetStressTestConfig()
    {
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .WithPlatform(Platform.X64)
                .WithRuntime(CoreRuntime.Core80)
                .WithGcServer(true)
                .WithGcConcurrent(true)
                .WithStrategy(RunStrategy.Monitoring)
                .WithLaunchCount(1)
                .WithWarmupCount(1)
                .WithIterationCount(1)
                .WithIterationTime(TimeInterval.FromMinutes(5)))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(ThreadingDiagnoser.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(JsonExporter.Full)
            .AddColumn(StatisticColumn.Mean)
            .AddColumn(StatisticColumn.Max)
            .AddColumn(StatisticColumn.Min);
    }

    private static Job GetDefaultJob()
    {
        return Job.Default
            .WithPlatform(Platform.X64)
            .WithRuntime(CoreRuntime.Core80)
            .WithGcServer(true)
            .WithGcConcurrent(true)
            .WithGcForce(false)
            .WithId("Default");
    }

    private static Job GetOptimizedJob()
    {
        return Job.Default
            .WithPlatform(Platform.X64)
            .WithRuntime(CoreRuntime.Core80)
            .WithGcServer(true)
            .WithGcConcurrent(true)
            .WithGcForce(false)
            .WithStrategy(RunStrategy.Throughput)
            .WithEnvironmentVariable("DOTNET_TieredCompilation", "1")
            .WithEnvironmentVariable("DOTNET_TieredPGO", "1")
            .WithEnvironmentVariable("DOTNET_TC_QuickJitForLoops", "1")
            .WithEnvironmentVariable("DOTNET_ReadyToRun", "0")
            .WithId("Optimized");
    }

    private static Job GetDebugJob()
    {
        return Job.Default
            .WithPlatform(Platform.X64)
            .WithRuntime(CoreRuntime.Core80)
            .WithGcServer(false)
            .WithGcConcurrent(false)
            .WithGcForce(true)
            .WithStrategy(RunStrategy.ColdStart)
            .WithEnvironmentVariable("DOTNET_TieredCompilation", "0")
            .WithEnvironmentVariable("DOTNET_TieredPGO", "0")
            .WithId("Debug");
    }

    private static IDiagnoser[] GetDiagnosers()
    {
        return new IDiagnoser[]
        {
            MemoryDiagnoser.Default,
            ThreadingDiagnoser.Default,
            new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(
                printSource: true,
                printInstructionAddresses: false,
                exportGithubMarkdown: true,
                exportHtml: false))
        };
    }

    private static IExporter[] GetExporters()
    {
        return new IExporter[]
        {
            MarkdownExporter.GitHub,
            HtmlExporter.Default,
            CsvExporter.Default,
            JsonExporter.Full,
            XmlExporter.Full
        };
    }

    private static ILogger[] GetLoggers()
    {
        return new ILogger[]
        {
            ConsoleLogger.Default,
            new FileLogger("benchmark-log.txt")
        };
    }

    private static IColumn[] GetColumns()
    {
        return new IColumn[]
        {
            StatisticColumn.Mean,
            StatisticColumn.Median,
            StatisticColumn.StdDev,
            StatisticColumn.Min,
            StatisticColumn.Max,
            StatisticColumn.P95,
            StatisticColumn.Throughput,
            BaselineRatioColumn.RatioMean,
            RankColumn.Arabic,
            CategoryColumn.Default
        };
    }
}

public class FileLogger : ILogger
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public FileLogger(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? ".");
    }

    public string Id => nameof(FileLogger);

    public int Priority => 0;

    public void Write(LogKind logKind, string text)
    {
        lock (_lock)
        {
            File.AppendAllText(_filePath, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {logKind}: {text}");
        }
    }

    public void WriteLine()
    {
        lock (_lock)
        {
            File.AppendAllText(_filePath, Environment.NewLine);
        }
    }

    public void WriteLine(LogKind logKind, string text)
    {
        lock (_lock)
        {
            File.AppendAllText(_filePath, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {logKind}: {text}{Environment.NewLine}");
        }
    }

    public void Flush()
    {
        // File operations are already flushed
    }
}