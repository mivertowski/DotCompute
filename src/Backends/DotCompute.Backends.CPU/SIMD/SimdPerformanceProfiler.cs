// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CPU.Kernels.Simd;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DotCompute.Backends.CPU.SIMD;

/// <summary>
/// Performance profiler for SIMD operations with detailed metrics collection
/// </summary>
public sealed class SimdPerformanceProfiler : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<SimdExecutionStrategy, StrategyMetrics> _strategyMetrics;
    private readonly Timer _metricsReportTimer;
    private volatile bool _disposed;

    public SimdPerformanceProfiler(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _strategyMetrics = new ConcurrentDictionary<SimdExecutionStrategy, StrategyMetrics>();

        // Report metrics periodically
        _metricsReportTimer = new Timer(ReportMetrics, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Records execution metrics for a SIMD operation
    /// </summary>
    public void RecordExecution(
        SimdExecutionStrategy strategy,
        long elementCount,
        TimeSpan duration,
        long vectorizedElements,
        long scalarElements)
    {
        var metrics = _strategyMetrics.GetOrAdd(strategy, _ => new StrategyMetrics());

        lock (metrics)
        {
            metrics.TotalExecutions++;
            metrics.TotalElements += elementCount;
            metrics.TotalDuration += duration;
            metrics.VectorizedElements += vectorizedElements;
            metrics.ScalarElements += scalarElements;

            // Update performance statistics
            var throughput = elementCount / duration.TotalSeconds;
            metrics.UpdateThroughput(throughput);

            var vectorizationRatio = (double)vectorizedElements / elementCount;
            metrics.UpdateVectorizationRatio(vectorizationRatio);
        }
    }

    /// <summary>
    /// Calculates vectorization statistics for given strategy and element count
    /// </summary>
    public static (long vectorized, long scalar) CalculateVectorizationStats(
        SimdExecutionStrategy strategy,
        long elementCount)
    {
        var vectorSize = GetVectorSize(strategy);
        if (vectorSize == 1) // Scalar execution
        {
            return (0, elementCount);
        }

        var vectorizedElements = (elementCount / vectorSize) * vectorSize;
        var scalarElements = elementCount % vectorSize;

        return (vectorizedElements, scalarElements);
    }

    /// <summary>
    /// Gets comprehensive performance report
    /// </summary>
    public PerformanceReport GetPerformanceReport()
    {
        var report = new PerformanceReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            StrategyPerformance = []
        };

        foreach (var (strategy, metrics) in _strategyMetrics)
        {
            lock (metrics)
            {
                if (metrics.TotalExecutions > 0)
                {
                    report.StrategyPerformance[strategy] = new StrategyPerformance
                    {
                        Strategy = strategy,
                        TotalExecutions = metrics.TotalExecutions,
                        TotalElements = metrics.TotalElements,
                        AverageDuration = TimeSpan.FromTicks(metrics.TotalDuration.Ticks / metrics.TotalExecutions),
                        AverageThroughput = metrics.TotalElements / metrics.TotalDuration.TotalSeconds,
                        VectorizationRatio = (double)metrics.VectorizedElements / metrics.TotalElements,
                        PeakThroughput = metrics.PeakThroughput,
                        EfficiencyScore = CalculateEfficiencyScore(metrics, strategy)
                    };
                }
            }
        }

        // Calculate overall statistics
        report.OverallStatistics = CalculateOverallStatistics(report.StrategyPerformance.Values);

        return report;
    }

    /// <summary>
    /// Gets performance comparison between strategies
    /// </summary>
    public StrategyComparison CompareStrategies(
        SimdExecutionStrategy strategy1,
        SimdExecutionStrategy strategy2)
    {
        var metrics1 = _strategyMetrics.GetValueOrDefault(strategy1);
        var metrics2 = _strategyMetrics.GetValueOrDefault(strategy2);

        if (metrics1 == null || metrics2 == null)
        {
            return new StrategyComparison
            {
                Strategy1 = strategy1,
                Strategy2 = strategy2,
                IsValid = false,
                Reason = "Insufficient data for comparison"
            };
        }

        lock (metrics1)
        {
            lock (metrics2)
            {
                var throughput1 = metrics1.TotalElements / metrics1.TotalDuration.TotalSeconds;
                var throughput2 = metrics2.TotalElements / metrics2.TotalDuration.TotalSeconds;

                return new StrategyComparison
                {
                    Strategy1 = strategy1,
                    Strategy2 = strategy2,
                    IsValid = true,
                    ThroughputRatio = throughput1 / throughput2,
                    VectorizationDifference = (double)metrics1.VectorizedElements / metrics1.TotalElements -
                                            (double)metrics2.VectorizedElements / metrics2.TotalElements,
                    PerformanceGain = CalculatePerformanceGain(throughput1, throughput2),
                    Recommendation = GenerateRecommendation(strategy1, strategy2, throughput1, throughput2)
                };
            }
        }
    }

    /// <summary>
    /// Analyzes performance trends over time
    /// </summary>
    public PerformanceTrend AnalyzeTrends(SimdExecutionStrategy strategy, TimeSpan timeWindow)
    {
        var metrics = _strategyMetrics.GetValueOrDefault(strategy);
        if (metrics == null)
        {
            return new PerformanceTrend
            {
                Strategy = strategy,
                IsValid = false,
                Reason = "No metrics available for strategy"
            };
        }

        lock (metrics)
        {
            return new PerformanceTrend
            {
                Strategy = strategy,
                IsValid = true,
                ThroughputTrend = metrics.CalculateThroughputTrend(),
                VectorizationTrend = metrics.CalculateVectorizationTrend(),
                EfficiencyTrend = metrics.CalculateEfficiencyTrend(),
                Recommendations = GenerateTrendRecommendations(metrics)
            };
        }
    }

    /// <summary>
    /// Gets optimal strategy recommendation based on performance data
    /// </summary>
    public StrategyRecommendation GetOptimalStrategy(long elementCount, Type dataType)
    {
        var recommendations = new Dictionary<SimdExecutionStrategy, double>();

        foreach (var (strategy, metrics) in _strategyMetrics)
        {
            lock (metrics)
            {
                if (metrics.TotalExecutions > 10) // Require sufficient data
                {
                    var score = CalculateStrategyScore(metrics, strategy, elementCount, dataType);
                    recommendations[strategy] = score;
                }
            }
        }

        if (recommendations.Count == 0)
        {
            return new StrategyRecommendation
            {
                RecommendedStrategy = SimdExecutionStrategy.Scalar,
                Confidence = 0.5,
                Reason = "Insufficient performance data",
                AlternativeStrategies = []
            };
        }

        var optimal = recommendations.OrderByDescending(kvp => kvp.Value).First();
        var alternatives = recommendations
            .Where(kvp => kvp.Key != optimal.Key)
            .OrderByDescending(kvp => kvp.Value)
            .Take(2)
            .Select(kvp => kvp.Key)
            .ToList();

        return new StrategyRecommendation
        {
            RecommendedStrategy = optimal.Key,
            Confidence = Math.Min(1.0, optimal.Value / 100.0),
            Reason = $"Best performance for {elementCount} elements of type {dataType.Name}",
            AlternativeStrategies = alternatives
        };
    }

    private static int GetVectorSize(SimdExecutionStrategy strategy)
    {
        return strategy switch
        {
            SimdExecutionStrategy.Avx512 => 16, // 512 bits / 32 bits per float
            SimdExecutionStrategy.Avx2 => 8,   // 256 bits / 32 bits per float
            SimdExecutionStrategy.Sse => 4,    // 128 bits / 32 bits per float
            SimdExecutionStrategy.Neon => 4,   // 128 bits / 32 bits per float
            _ => 1
        };
    }

    private static double CalculateEfficiencyScore(StrategyMetrics metrics, SimdExecutionStrategy strategy)
    {
        var expectedSpeedup = GetVectorSize(strategy);
        var actualSpeedup = metrics.TotalElements / metrics.TotalDuration.TotalSeconds;
        var baselineSpeedup = 1.0; // Scalar baseline

        return Math.Min(1.0, actualSpeedup / (baselineSpeedup * expectedSpeedup));
    }

    private static OverallStatistics CalculateOverallStatistics(IEnumerable<StrategyPerformance> strategies)
    {
        var list = strategies.ToList();
        if (list.Count == 0)
        {
            return new OverallStatistics();
        }

        return new OverallStatistics
        {
            TotalExecutions = list.Sum(s => s.TotalExecutions),
            TotalElements = list.Sum(s => s.TotalElements),
            AverageVectorizationRatio = list.Average(s => s.VectorizationRatio),
            BestStrategy = list.OrderByDescending(s => s.AverageThroughput).First().Strategy,
            WorstStrategy = list.OrderBy(s => s.AverageThroughput).First().Strategy,
            PerformanceSpread = list.Max(s => s.AverageThroughput) / list.Min(s => s.AverageThroughput)
        };
    }

    private static double CalculatePerformanceGain(double throughput1, double throughput2)
    {
        return throughput2 > 0 ? (throughput1 / throughput2) - 1.0 : 0.0;
    }

    private static string GenerateRecommendation(
        SimdExecutionStrategy strategy1,
        SimdExecutionStrategy strategy2,
        double throughput1,
        double throughput2)
    {
        var better = throughput1 > throughput2 ? strategy1 : strategy2;
        var gain = Math.Abs(CalculatePerformanceGain(throughput1, throughput2));

        return gain > 0.1
            ? $"Use {better} for {gain:P1} better performance"
            : "Performance difference is minimal";
    }

    private static double CalculateStrategyScore(
        StrategyMetrics metrics,
        SimdExecutionStrategy strategy,
        long elementCount,
        Type dataType)
    {
        var baseScore = metrics.TotalElements / metrics.TotalDuration.TotalSeconds;
        var vectorizationBonus = (double)metrics.VectorizedElements / metrics.TotalElements * 20;
        var consistencyBonus = metrics.CalculateConsistencyScore() * 10;

        return baseScore + vectorizationBonus + consistencyBonus;
    }

    private static List<string> GenerateTrendRecommendations(StrategyMetrics metrics)
    {
        var recommendations = new List<string>();

        if (metrics.CalculateThroughputTrend() < -0.1)
        {
            recommendations.Add("Decreasing throughput trend detected - consider workload optimization");
        }

        if (metrics.CalculateVectorizationTrend() < 0.8)
        {
            recommendations.Add("Low vectorization ratio - consider data alignment optimization");
        }

        return recommendations;
    }

    private void ReportMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var report = GetPerformanceReport();
            _logger.LogInformation("SIMD Performance Summary: {TotalExecutions} executions, {Strategies} strategies used",
                report.OverallStatistics.TotalExecutions,
                report.StrategyPerformance.Count);

            foreach (var (strategy, performance) in report.StrategyPerformance)
            {
                _logger.LogDebug("Strategy {Strategy}: {Throughput:F2} elements/sec, {VectorizationRatio:P1} vectorized",
                    strategy, performance.AverageThroughput, performance.VectorizationRatio);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting SIMD performance metrics");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _metricsReportTimer?.Dispose();
            _disposed = true;
            _logger.LogDebug("SIMD Performance Profiler disposed");
        }
    }
}

#region Supporting Types

/// <summary>
/// Metrics for a specific SIMD strategy
/// </summary>
internal sealed class StrategyMetrics
{
    public long TotalExecutions { get; set; }
    public long TotalElements { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public long VectorizedElements { get; set; }
    public long ScalarElements { get; set; }
    public double PeakThroughput { get; private set; }
    private readonly List<double> _throughputHistory = [];
    private readonly List<double> _vectorizationHistory = [];

    public void UpdateThroughput(double throughput)
    {
        PeakThroughput = Math.Max(PeakThroughput, throughput);
        _throughputHistory.Add(throughput);

        // Keep only recent history
        if (_throughputHistory.Count > 100)
        {
            _throughputHistory.RemoveAt(0);
        }
    }

    public void UpdateVectorizationRatio(double ratio)
    {
        _vectorizationHistory.Add(ratio);

        // Keep only recent history
        if (_vectorizationHistory.Count > 100)
        {
            _vectorizationHistory.RemoveAt(0);
        }
    }

    public double CalculateThroughputTrend()
    {
        return _throughputHistory.Count > 1
            ? _throughputHistory.TakeLast(10).Average() / _throughputHistory.Take(10).Average() - 1.0
            : 0.0;
    }

    public double CalculateVectorizationTrend()
    {
        return _vectorizationHistory.Count > 0
            ? _vectorizationHistory.Average()
            : 0.0;
    }

    public double CalculateEfficiencyTrend()
    {
        // Simplified efficiency calculation
        return CalculateThroughputTrend() * CalculateVectorizationTrend();
    }

    public double CalculateConsistencyScore()
    {
        if (_throughputHistory.Count < 5)
        {
            return 1.0;
        }


        var mean = _throughputHistory.Average();
        var variance = _throughputHistory.Select(x => Math.Pow(x - mean, 2)).Average();
        var stddev = Math.Sqrt(variance);

        // Return score from 0-1 where 1 is perfectly consistent
        return Math.Max(0.0, 1.0 - (stddev / mean));
    }
}

/// <summary>
/// Performance report for all strategies
/// </summary>
public sealed class PerformanceReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public Dictionary<SimdExecutionStrategy, StrategyPerformance> StrategyPerformance { get; init; } = [];
    public OverallStatistics OverallStatistics { get; set; } = new();
}

/// <summary>
/// Performance data for a specific strategy
/// </summary>
public sealed class StrategyPerformance
{
    public SimdExecutionStrategy Strategy { get; init; }
    public long TotalExecutions { get; init; }
    public long TotalElements { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public double AverageThroughput { get; init; }
    public double VectorizationRatio { get; init; }
    public double PeakThroughput { get; init; }
    public double EfficiencyScore { get; init; }
}

/// <summary>
/// Overall performance statistics
/// </summary>
public sealed class OverallStatistics
{
    public long TotalExecutions { get; init; }
    public long TotalElements { get; init; }
    public double AverageVectorizationRatio { get; init; }
    public SimdExecutionStrategy BestStrategy { get; init; }
    public SimdExecutionStrategy WorstStrategy { get; init; }
    public double PerformanceSpread { get; init; }
}

/// <summary>
/// Comparison between two strategies
/// </summary>
public sealed class StrategyComparison
{
    public SimdExecutionStrategy Strategy1 { get; init; }
    public SimdExecutionStrategy Strategy2 { get; init; }
    public bool IsValid { get; init; }
    public string? Reason { get; init; }
    public double ThroughputRatio { get; init; }
    public double VectorizationDifference { get; init; }
    public double PerformanceGain { get; init; }
    public string? Recommendation { get; init; }
}

/// <summary>
/// Performance trend analysis
/// </summary>
public sealed class PerformanceTrend
{
    public SimdExecutionStrategy Strategy { get; init; }
    public bool IsValid { get; init; }
    public string? Reason { get; init; }
    public double ThroughputTrend { get; init; }
    public double VectorizationTrend { get; init; }
    public double EfficiencyTrend { get; init; }
    public List<string> Recommendations { get; init; } = [];
}

/// <summary>
/// Strategy recommendation based on performance analysis
/// </summary>
public sealed class StrategyRecommendation
{
    public SimdExecutionStrategy RecommendedStrategy { get; init; }
    public double Confidence { get; init; }
    public string? Reason { get; init; }
    public List<SimdExecutionStrategy> AlternativeStrategies { get; init; } = [];
}

#endregion