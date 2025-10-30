// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Reports;

namespace DotCompute.Benchmarks.OpenCL;

/// <summary>
/// Generates comprehensive performance analysis reports comparing OpenCL backend
/// against baseline implementations and other accelerators.
/// </summary>
public sealed class PerformanceAnalysisReport
{
    private readonly Summary _summary;
    private readonly string _outputDirectory;

    public PerformanceAnalysisReport(Summary summary, string outputDirectory)
    {
        _summary = summary ?? throw new ArgumentNullException(nameof(summary));
        _outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));

        Directory.CreateDirectory(_outputDirectory);
    }

    /// <summary>
    /// Generates a comprehensive performance analysis report with recommendations
    /// </summary>
    public async Task GenerateReportAsync()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var reportPath = Path.Combine(_outputDirectory, $"OpenCL_Performance_Report_{timestamp}.md");
        var jsonPath = Path.Combine(_outputDirectory, $"OpenCL_Performance_Data_{timestamp}.json");

        // Extract performance metrics
        var metrics = ExtractPerformanceMetrics();

        // Generate markdown report
        await GenerateMarkdownReportAsync(reportPath, metrics);

        // Generate JSON data for further analysis
        await GenerateJsonDataAsync(jsonPath, metrics);

        Console.WriteLine($"\n📊 Performance Analysis Report Generated:");
        Console.WriteLine($"  Markdown: {reportPath}");
        Console.WriteLine($"  JSON Data: {jsonPath}");
    }

    private PerformanceMetrics ExtractPerformanceMetrics()
    {
        var metrics = new PerformanceMetrics
        {
            Timestamp = DateTime.UtcNow,
            Benchmarks = new List<BenchmarkMetric>()
        };

        foreach (var benchmarkCase in _summary.BenchmarksCases)
        {
            var report = _summary.Reports.FirstOrDefault(r => r.BenchmarkCase == benchmarkCase);
            if (report?.ResultStatistics == null) continue;

            var metric = new BenchmarkMetric
            {
                Name = benchmarkCase.DisplayInfo,
                Category = GetCategory(benchmarkCase.DisplayInfo),
                Backend = GetBackend(benchmarkCase.DisplayInfo),
                MeanNanoseconds = report.ResultStatistics.Mean,
                StdDevNanoseconds = report.ResultStatistics.StandardDeviation,
                MedianNanoseconds = report.ResultStatistics.Median,
                MinNanoseconds = report.ResultStatistics.Min,
                MaxNanoseconds = report.ResultStatistics.Max,
                AllocatedBytes = report.GcStats.GetTotalAllocatedBytes(false),
                Gen0Collections = report.GcStats.Gen0Collections,
                Gen1Collections = report.GcStats.Gen1Collections,
                Gen2Collections = report.GcStats.Gen2Collections
            };

            metrics.Benchmarks.Add(metric);
        }

        // Calculate speedups
        CalculateSpeedups(metrics);

        return metrics;
    }

    private void CalculateSpeedups(PerformanceMetrics metrics)
    {
        var categories = metrics.Benchmarks.Select(b => b.Category).Distinct();

        foreach (var category in categories)
        {
            var categoryBenchmarks = metrics.Benchmarks.Where(b => b.Category == category).ToList();
            var baseline = categoryBenchmarks.FirstOrDefault(b => b.Backend == "CPU_Scalar");

            if (baseline == null) continue;

            foreach (var benchmark in categoryBenchmarks)
            {
                if (benchmark.Backend == "CPU_Scalar") continue;

                benchmark.SpeedupVsBaseline = baseline.MeanNanoseconds / benchmark.MeanNanoseconds;
            }
        }
    }

    private async Task GenerateMarkdownReportAsync(string filePath, PerformanceMetrics metrics)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# OpenCL Backend Performance Analysis Report");
        sb.AppendLine($"**Generated**: {metrics.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine();

        // Executive Summary
        sb.AppendLine("## Executive Summary");
        sb.AppendLine();

        var openclBenchmarks = metrics.Benchmarks.Where(b => b.Backend == "OpenCL").ToList();
        var avgSpeedup = openclBenchmarks.Average(b => b.SpeedupVsBaseline);
        var maxSpeedup = openclBenchmarks.Max(b => b.SpeedupVsBaseline);

        sb.AppendLine($"- **Average Speedup (OpenCL vs Scalar)**: {avgSpeedup:F2}x");
        sb.AppendLine($"- **Maximum Speedup**: {maxSpeedup:F2}x");
        sb.AppendLine($"- **Total Benchmarks Run**: {metrics.Benchmarks.Count}");
        sb.AppendLine();

        // Performance by Category
        sb.AppendLine("## Performance by Category");
        sb.AppendLine();

        var categories = metrics.Benchmarks.GroupBy(b => b.Category);
        foreach (var category in categories)
        {
            sb.AppendLine($"### {category.Key}");
            sb.AppendLine();
            sb.AppendLine("| Backend | Mean (ms) | Std Dev (ms) | Speedup | Allocated (KB) |");
            sb.AppendLine("|---------|-----------|--------------|---------|----------------|");

            foreach (var benchmark in category.OrderBy(b => b.MeanNanoseconds))
            {
                var meanMs = benchmark.MeanNanoseconds / 1_000_000.0;
                var stdDevMs = benchmark.StdDevNanoseconds / 1_000_000.0;
                var allocatedKB = benchmark.AllocatedBytes / 1024.0;
                var speedup = benchmark.SpeedupVsBaseline > 0 ? $"{benchmark.SpeedupVsBaseline:F2}x" : "baseline";

                sb.AppendLine($"| {benchmark.Backend,-15} | {meanMs,9:F3} | {stdDevMs,12:F3} | {speedup,7} | {allocatedKB,14:F2} |");
            }

            sb.AppendLine();
        }

        // Bottleneck Analysis
        sb.AppendLine("## Bottleneck Analysis");
        sb.AppendLine();

        var slowest = metrics.Benchmarks.OrderByDescending(b => b.MeanNanoseconds).Take(5);
        sb.AppendLine("### Top 5 Slowest Operations:");
        sb.AppendLine();

        int rank = 1;
        foreach (var benchmark in slowest)
        {
            var meanMs = benchmark.MeanNanoseconds / 1_000_000.0;
            sb.AppendLine($"{rank}. **{benchmark.Name}** ({benchmark.Backend})");
            sb.AppendLine($"   - Duration: {meanMs:F3}ms");
            sb.AppendLine($"   - Speedup potential: {(benchmark.SpeedupVsBaseline > 0 ? $"{benchmark.SpeedupVsBaseline:F2}x" : "baseline")}");
            sb.AppendLine();
            rank++;
        }

        // Memory Analysis
        sb.AppendLine("## Memory Usage Analysis");
        sb.AppendLine();

        var highAllocation = metrics.Benchmarks.OrderByDescending(b => b.AllocatedBytes).Take(5);
        sb.AppendLine("### Top 5 Memory-Intensive Operations:");
        sb.AppendLine();

        rank = 1;
        foreach (var benchmark in highAllocation)
        {
            var allocatedMB = benchmark.AllocatedBytes / (1024.0 * 1024.0);
            sb.AppendLine($"{rank}. **{benchmark.Name}** ({benchmark.Backend})");
            sb.AppendLine($"   - Allocated: {allocatedMB:F2}MB");
            sb.AppendLine($"   - GC Collections: Gen0={benchmark.Gen0Collections}, Gen1={benchmark.Gen1Collections}, Gen2={benchmark.Gen2Collections}");
            sb.AppendLine();
            rank++;
        }

        // Optimization Recommendations
        sb.AppendLine("## Optimization Recommendations");
        sb.AppendLine();

        sb.AppendLine("### Priority 1: High-Impact Optimizations");
        sb.AppendLine();

        // Identify opportunities
        var slowOpenCL = openclBenchmarks.Where(b => b.SpeedupVsBaseline < 2.0).ToList();
        if (slowOpenCL.Any())
        {
            sb.AppendLine("**OpenCL Performance Below 2x Speedup:**");
            sb.AppendLine("- Investigate kernel compilation overhead");
            sb.AppendLine("- Optimize work-group size selection");
            sb.AppendLine("- Consider kernel fusion for small operations");
            sb.AppendLine();
        }

        var highMemoryOps = metrics.Benchmarks.Where(b => b.AllocatedBytes > 100 * 1024 * 1024).ToList();
        if (highMemoryOps.Any())
        {
            sb.AppendLine("**High Memory Allocation Detected:**");
            sb.AppendLine("- Enable memory pooling (90% reduction possible)");
            sb.AppendLine("- Implement buffer reuse strategies");
            sb.AppendLine("- Consider zero-copy memory where applicable");
            sb.AppendLine();
        }

        sb.AppendLine("### Priority 2: Compilation & Caching");
        sb.AppendLine();
        sb.AppendLine("- **Compilation Cache Hit Rate**: Monitor via profiler");
        sb.AppendLine("- **JIT Compilation Overhead**: Measure first-run vs cached");
        sb.AppendLine("- **Binary Caching**: Enable disk-based cache for production");
        sb.AppendLine();

        sb.AppendLine("### Priority 3: Device Utilization");
        sb.AppendLine();
        sb.AppendLine("- **Occupancy Analysis**: Ensure >50% compute unit utilization");
        sb.AppendLine("- **Async Execution**: Overlap memory transfers with computation");
        sb.AppendLine("- **Multi-Device**: Consider load balancing across devices");
        sb.AppendLine();

        // Validation Status
        sb.AppendLine("## Performance Claims Validation");
        sb.AppendLine();

        if (avgSpeedup >= 8.0)
        {
            sb.AppendLine("✅ **VALIDATED**: OpenCL backend achieves 8-23x speedup target");
            sb.AppendLine($"   - Average speedup: {avgSpeedup:F2}x");
            sb.AppendLine($"   - Maximum speedup: {maxSpeedup:F2}x");
        }
        else if (avgSpeedup >= 4.0)
        {
            sb.AppendLine("⚠️ **PARTIAL**: OpenCL backend shows significant performance improvement");
            sb.AppendLine($"   - Average speedup: {avgSpeedup:F2}x");
            sb.AppendLine($"   - Target: 8.0x+");
            sb.AppendLine("   - Recommendation: Apply optimization strategies above");
        }
        else
        {
            sb.AppendLine("❌ **BELOW TARGET**: OpenCL backend performance needs optimization");
            sb.AppendLine($"   - Current average: {avgSpeedup:F2}x");
            sb.AppendLine($"   - Target: 8.0x+");
            sb.AppendLine("   - Action: Prioritize all optimization recommendations");
        }

        sb.AppendLine();

        // Conclusion
        sb.AppendLine("## Conclusion");
        sb.AppendLine();
        sb.AppendLine("This report provides a comprehensive analysis of OpenCL backend performance.");
        sb.AppendLine("Follow the optimization recommendations to achieve maximum performance gains.");
        sb.AppendLine();

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    private async Task GenerateJsonDataAsync(string filePath, PerformanceMetrics metrics)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(metrics, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    private static string GetCategory(string displayInfo)
    {
        if (displayInfo.Contains("VectorAdd", StringComparison.OrdinalIgnoreCase))
            return "Vector Addition";
        if (displayInfo.Contains("DotProduct", StringComparison.OrdinalIgnoreCase) ||
            displayInfo.Contains("Reduction", StringComparison.OrdinalIgnoreCase))
            return "Reduction";
        if (displayInfo.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            return "Memory Transfer";
        if (displayInfo.Contains("Compilation", StringComparison.OrdinalIgnoreCase))
            return "Compilation";

        return "Other";
    }

    private static string GetBackend(string displayInfo)
    {
        if (displayInfo.Contains("OpenCL", StringComparison.OrdinalIgnoreCase))
            return "OpenCL";
        if (displayInfo.Contains("CUDA", StringComparison.OrdinalIgnoreCase))
            return "CUDA";
        if (displayInfo.Contains("Metal", StringComparison.OrdinalIgnoreCase))
            return "Metal";
        if (displayInfo.Contains("SIMD", StringComparison.OrdinalIgnoreCase))
            return "CPU_SIMD";
        if (displayInfo.Contains("Parallel", StringComparison.OrdinalIgnoreCase))
            return "CPU_Parallel";
        if (displayInfo.Contains("Scalar", StringComparison.OrdinalIgnoreCase))
            return "CPU_Scalar";

        return "Unknown";
    }
}

/// <summary>
/// Performance metrics data structure for analysis
/// </summary>
public sealed class PerformanceMetrics
{
    public DateTime Timestamp { get; init; }
    public List<BenchmarkMetric> Benchmarks { get; init; } = new();
}

/// <summary>
/// Individual benchmark metric
/// </summary>
public sealed class BenchmarkMetric
{
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Backend { get; init; } = string.Empty;
    public double MeanNanoseconds { get; init; }
    public double StdDevNanoseconds { get; init; }
    public double MedianNanoseconds { get; init; }
    public double MinNanoseconds { get; init; }
    public double MaxNanoseconds { get; init; }
    public long AllocatedBytes { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
    public double SpeedupVsBaseline { get; set; }
}
