// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Models;
using DotCompute.Core.Execution.Metrics;
using Microsoft.Extensions.Logging;
using ExecutionStrategyType = DotCompute.Abstractions.Types.ExecutionStrategyType;
using AnalysisBottleneckType = DotCompute.Abstractions.Types.BottleneckType;

namespace DotCompute.Core.Execution;

/// <summary>
/// Analyzes performance data and identifies bottlenecks.
/// </summary>
public class PerformanceAnalyzer
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the PerformanceAnalyzer class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PerformanceAnalyzer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    /// <summary>
    /// Gets analyze performance.
    /// </summary>
    /// <param name="executions">The executions.</param>
    /// <param name="deviceProfiles">The device profiles.</param>
    /// <returns>The result of the operation.</returns>

    public static ParallelExecutionAnalysis AnalyzePerformance(ExecutionRecord[] executions, DevicePerformanceProfile[] deviceProfiles)
    {
        var analysis = new ParallelExecutionAnalysis();

        // Calculate overall rating
        analysis.OverallRating = CalculateOverallRating(executions);

        // Identify bottlenecks
        analysis.Bottlenecks = [.. IdentifyBottlenecks(executions, deviceProfiles)];

        // Generate optimization recommendations
        // Convert execution bottlenecks to kernel bottlenecks for recommendations
        var kernelBottlenecks = new List<Analysis.BottleneckAnalysis>();
        foreach (var b in analysis.Bottlenecks)
        {
            kernelBottlenecks.Add(new Analysis.BottleneckAnalysis
            {
                Type = b.Type switch
                {
                    AnalysisBottleneckType.MemoryBandwidth => AnalysisBottleneckType.MemoryBandwidth,
                    AnalysisBottleneckType.Compute => AnalysisBottleneckType.Compute,
                    _ => AnalysisBottleneckType.MemoryLatency
                },
                Severity = b.Severity,
                Details = b.Details,
                ResourceUtilization = []
            });
        }

        analysis.OptimizationRecommendations = [.. GenerateOptimizationRecommendations(executions, kernelBottlenecks)];

        // Recommend optimal strategy
        analysis.RecommendedStrategy = (ExecutionStrategyType)RecommendStrategy(executions);

        // Analyze device utilization
        analysis.DeviceUtilizationAnalysis = AnalyzeDeviceUtilization(deviceProfiles);

        return analysis;
    }
    /// <summary>
    /// Gets analyze trends.
    /// </summary>
    /// <param name="executions">The executions.</param>
    /// <returns>The result of the operation.</returns>

    public static ExecutionPerformanceTrend AnalyzeTrends(ExecutionRecord[] executions)
    {
        if (executions.Length == 0)
        {
            return new ExecutionPerformanceTrend();
        }

        var trends = new ExecutionPerformanceTrend
        {
            TimeRange = new TimeRange
            {
                Start = executions.First().Timestamp,
                End = executions.Last().Timestamp
            }
        };

        // Calculate throughput trend
        trends.ThroughputTrend = CalculateTrend([.. executions.Select(e => e.ThroughputGFLOPS)]);

        // Calculate efficiency trend
        trends.EfficiencyTrend = CalculateTrend([.. executions.Select(e => e.EfficiencyPercentage)]);

        // Calculate execution time trend
        trends.ExecutionTimeTrend = CalculateTrend([.. executions.Select(e => e.TotalExecutionTimeMs)]);

        // Calculate memory bandwidth trend
        trends.MemoryBandwidthTrend = CalculateTrend([.. executions.Select(e => e.MemoryBandwidthGBps)]);

        return trends;
    }

    private static double CalculateOverallRating(ExecutionRecord[] executions)
    {
        if (executions.Length == 0)
        {
            return 5.0;
        }

        var avgEfficiency = executions.Average(e => e.EfficiencyPercentage);
        var successRate = executions.Count(e => e.Success) / (double)executions.Length;

        // Rating from 1-10 based on efficiency and success rate
        var efficiencyRating = Math.Min(10, avgEfficiency / 10.0);
        var successRating = successRate * 10.0;

        return (efficiencyRating + successRating) / 2.0;
    }

    private static IEnumerable<Analysis.BottleneckAnalysis> IdentifyBottlenecks(ExecutionRecord[] executions, DevicePerformanceProfile[] deviceProfiles)
    {
        var bottlenecks = new List<Analysis.BottleneckAnalysis>();

        // Memory bandwidth bottleneck
        var avgMemoryEfficiency = executions.Average(e =>
            e.DeviceResults.Where(d => d.Success).Average(d => d.MemoryBandwidthGBps));

        if (avgMemoryEfficiency < 100) // Assuming 100 GB/s as baseline
        {
            bottlenecks.Add(new Analysis.BottleneckAnalysis
            {
                Type = AnalysisBottleneckType.MemoryBandwidth,
                Severity = 1.0 - (avgMemoryEfficiency / 100),
                Details = $"Average memory bandwidth utilization is low: {avgMemoryEfficiency:F1} GB/s"
            });
        }

        // Parallel efficiency bottleneck
        var avgParallelEfficiency = executions.Average(e => e.EfficiencyPercentage);
        if (avgParallelEfficiency < 60)
        {
            bottlenecks.Add(new Analysis.BottleneckAnalysis
            {
                Type = AnalysisBottleneckType.Synchronization,
                Severity = (60 - avgParallelEfficiency) / 60,
                Details = $"Low parallel efficiency: {avgParallelEfficiency:F1}%"
            });
        }

        return bottlenecks.OrderByDescending(b => b.Severity);
    }

    private static IEnumerable<string> GenerateOptimizationRecommendations(ExecutionRecord[] executions, IReadOnlyList<Analysis.BottleneckAnalysis> bottlenecks)
    {
        var recommendations = new List<string>();

        foreach (var bottleneck in bottlenecks.Take(3)) // Top 3 bottlenecks
        {
            switch (bottleneck.Type)
            {
                case AnalysisBottleneckType.MemoryBandwidth:
                    recommendations.Add("Consider using larger batch sizes or optimizing memory access patterns");
                    break;
                case AnalysisBottleneckType.MemoryLatency:
                    recommendations.Add("Reduce synchronization overhead by using asynchronous operations");
                    break;
                case AnalysisBottleneckType.Compute:
                    recommendations.Add("Optimize computational kernels or use higher compute capability devices");
                    break;
                default:
                    recommendations.Add($"Address {bottleneck.Type} bottleneck with severity {bottleneck.Severity:F2}");
                    break;
            }
        }

        // Strategy-specific recommendations
        var strategyGroups = executions.GroupBy(e => e.Strategy);
        foreach (var group in strategyGroups)
        {
            var avgEfficiency = group.Average(e => e.EfficiencyPercentage);
            if (avgEfficiency < 50)
            {
                recommendations.Add($"Consider alternatives to {group.Key} strategy due to low efficiency ({avgEfficiency:F1}%)");
            }
        }

        return recommendations;
    }

    private static ExecutionStrategyType RecommendStrategy(ExecutionRecord[] executions)
    {
        if (executions.Length == 0)
        {
            return ExecutionStrategyType.Single;
        }

        // Find strategy with best average efficiency
        var strategyPerformance = executions
            .Where(e => e.Success)
            .GroupBy(e => e.Strategy)
            .Select(g => new
            {
                Strategy = g.Key,
                AvgEfficiency = g.Average(e => e.EfficiencyPercentage),
                Count = g.Count()
            })
            .Where(s => s.Count >= 3) // Need at least 3 samples
            .OrderByDescending(s => s.AvgEfficiency)
            .FirstOrDefault();

        return strategyPerformance?.Strategy ?? ExecutionStrategyType.DataParallel;
    }

    private static Dictionary<string, double> AnalyzeDeviceUtilization(DevicePerformanceProfile[] deviceProfiles)
    {
        return deviceProfiles.ToDictionary(
            p => p.DeviceId,
            p => p.AverageUtilizationPercentage
        );
    }

    private static TrendDirection CalculateTrend(double[] values)
    {
        if (values.Length < 2)
        {
            return TrendDirection.Stable;
        }

        var correlation = CalculateCorrelation(values);

        return correlation switch
        {
            > 0.1 => TrendDirection.Improving,
            < -0.1 => TrendDirection.Degrading,
            _ => TrendDirection.Stable
        };
    }

    private static double CalculateCorrelation(double[] values)
    {
        var n = values.Length;
        var xSum = n * (n - 1) / 2.0; // Sum of indices
        var ySum = values.Sum();
        var xySum = values.Select((y, x) => x * y).Sum();
        var xSquareSum = n * (n - 1) * (2 * n - 1) / 6.0; // Sum of squared indices
        var ySquareSum = values.Sum(y => y * y);

        var numerator = n * xySum - xSum * ySum;
        var denominator = Math.Sqrt((n * xSquareSum - xSum * xSum) * (n * ySquareSum - ySum * ySum));

        return denominator != 0 ? numerator / denominator : 0;
    }
}

public class DevicePerformanceProfile
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public required string DeviceId { get; set; }
    /// <summary>
    /// Gets or sets the executions.
    /// </summary>
    /// <value>The executions.</value>
    public IList<DeviceExecutionResult> Executions { get; init; } = [];
    /// <summary>
    /// Gets or sets the average utilization percentage.
    /// </summary>
    /// <value>The average utilization percentage.</value>

    public double AverageUtilizationPercentage { get; private set; }
    /// <summary>
    /// Gets or sets the peak utilization percentage.
    /// </summary>
    /// <value>The peak utilization percentage.</value>
    public double PeakUtilizationPercentage { get; private set; }
    /// <summary>
    /// Gets or sets the idle time percentage.
    /// </summary>
    /// <value>The idle time percentage.</value>
    public double IdleTimePercentage { get; private set; }
    /// <summary>
    /// Gets or sets the primary bottleneck.
    /// </summary>
    /// <value>The primary bottleneck.</value>
    public Analysis.BottleneckAnalysis? PrimaryBottleneck { get; private set; }
    /// <summary>
    /// Performs add execution.
    /// </summary>
    /// <param name="result">The result.</param>

    public void AddExecution(DeviceExecutionResult result)
    {
        Executions.Add(result);

        // Update utilization metrics
        UpdateUtilizationMetrics();

        // Limit history
        if (Executions.Count > 1000)
        {
            Executions.RemoveAt(0);
        }
    }
    /// <summary>
    /// Gets the optimization recommendations.
    /// </summary>
    /// <returns>The optimization recommendations.</returns>

    public List<string> GetOptimizationRecommendations()
    {
        var recommendations = new List<string>();

        if (AverageUtilizationPercentage < 50)
        {
            recommendations.Add("Increase workload size or improve kernel efficiency");
        }

        if (PrimaryBottleneck is not null && PrimaryBottleneck.Type == AnalysisBottleneckType.MemoryBandwidth)
        {
            recommendations.Add("Optimize memory access patterns or use memory coalescing");
        }

        return recommendations;
    }

    private void UpdateUtilizationMetrics()
    {
        if (Executions.Count == 0)
        {
            return;
        }

        // Simple utilization calculation based on throughput
        var recentExecutions = Executions.TakeLast(100).Where(e => e.Success).ToArray();
        if (recentExecutions.Length == 0)
        {
            return;
        }

        AverageUtilizationPercentage = recentExecutions.Average(e => Math.Min(100, e.ThroughputGFLOPS / 10)); // Assuming 10 GFLOPS = 100% utilization
        PeakUtilizationPercentage = recentExecutions.Max(e => Math.Min(100, e.ThroughputGFLOPS / 10));
        IdleTimePercentage = 100 - AverageUtilizationPercentage;
    }
}

public class DeviceUtilizationAnalysis
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public required string DeviceId { get; set; }
    /// <summary>
    /// Gets or sets the average utilization percentage.
    /// </summary>
    /// <value>The average utilization percentage.</value>
    public double AverageUtilizationPercentage { get; set; }
    /// <summary>
    /// Gets or sets the peak utilization percentage.
    /// </summary>
    /// <value>The peak utilization percentage.</value>
    public double PeakUtilizationPercentage { get; set; }
    /// <summary>
    /// Gets or sets the idle time percentage.
    /// </summary>
    /// <value>The idle time percentage.</value>
    public double IdleTimePercentage { get; set; }
    /// <summary>
    /// Gets or sets the bottleneck severity.
    /// </summary>
    /// <value>The bottleneck severity.</value>
    public double BottleneckSeverity { get; set; }
    /// <summary>
    /// Gets or sets the recommended optimizations.
    /// </summary>
    /// <value>The recommended optimizations.</value>
    public IList<string> RecommendedOptimizations { get; init; } = [];
}