using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Columns;
using DotCompute.Performance.Benchmarks.Benchmarks;
using DotCompute.Performance.Benchmarks.Config;

namespace DotCompute.Performance.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .WithPlatform(Platform.X64)
                .WithRuntime(CoreRuntime.Core80)
                .WithGcServer(true)
                .WithGcConcurrent(true)
                .WithGcForce(false))
            .AddJob(Job.Default
                .WithPlatform(Platform.X64)
                .WithRuntime(CoreRuntime.Core80)
                .WithGcServer(true)
                .WithGcConcurrent(true)
                .WithGcForce(false)
                .WithStrategy(RunStrategy.Throughput))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(
                printSource: true,
                printInstructionAddresses: true,
                exportGithubMarkdown: true,
                exportHtml: true)))
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(HtmlExporter.Default)
            .AddExporter(CsvExporter.Default)
            .AddExporter(JsonExporter.Full)
            .AddColumn(StatisticColumn.Mean)
            .AddColumn(StatisticColumn.Median)
            .AddColumn(StatisticColumn.StdDev)
            .AddColumn(StatisticColumn.Min)
            .AddColumn(StatisticColumn.Max)
            .AddColumn(StatisticColumn.P95)
            .AddColumn(BaselineRatioColumn.RatioMean)
            .AddColumn(RankColumn.Arabic);

        if (args.Length == 0)
        {
            // Run all benchmarks
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                .RunAllJoined(config);
        }
        else
        {
            // Run specific benchmark
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                .Run(args, config);
        }
    }
}