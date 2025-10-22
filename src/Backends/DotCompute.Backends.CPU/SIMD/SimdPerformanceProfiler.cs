// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CPU.Kernels.Simd;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DotCompute.Backends.CPU.SIMD;

/// <summary>
/// Performance profiler for SIMD operations with detailed metrics collection
/// </summary>
public sealed partial class SimdPerformanceProfiler : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<SimdExecutionStrategy, StrategyMetrics> _strategyMetrics;
    private readonly Timer _metricsReportTimer;
    private volatile bool _disposed;

    #region LoggerMessage Delegates (Event IDs 7560-7579)

    [LoggerMessage(EventId = 7560, Level = LogLevel.Information, Message = "SIMD Performance Summary: {TotalExecutions} executions, {Strategies} strategies used")]
    private static partial void LogPerformanceSummary(ILogger logger, long totalExecutions, int strategies);

    [LoggerMessage(EventId = 7561, Level = LogLevel.Debug, Message = "Strategy {Strategy}: {Throughput:F2} elements/sec, {VectorizationRatio:P1} vectorized")]
    private static partial void LogStrategyPerformance(ILogger logger, SimdExecutionStrategy strategy, double throughput, double vectorizationRatio);

    [LoggerMessage(EventId = 7562, Level = LogLevel.Error, Message = "Error reporting SIMD performance metrics")]
    private static partial void LogMetricsReportError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 7563, Level = LogLevel.Debug, Message = "SIMD Performance Profiler disposed")]
    private static partial void LogProfilerDisposed(ILogger logger);

    #endregion

    /// <summary>
    /// Initializes a new instance of the SimdPerformanceProfiler class.
    /// </summary>
    /// <param name="logger">The logger.</param>

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
    public SimdPerformanceTrend AnalyzeTrends(SimdExecutionStrategy strategy, TimeSpan timeWindow)
    {
        var metrics = _strategyMetrics.GetValueOrDefault(strategy);
        if (metrics == null)
        {
            return new SimdPerformanceTrend
            {
                Strategy = strategy,
                IsValid = false,
                Reason = "No metrics available for strategy"
            };
        }

        lock (metrics)
        {
            return new SimdPerformanceTrend
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

    private static double CalculatePerformanceGain(double throughput1, double throughput2) => throughput2 > 0 ? (throughput1 / throughput2) - 1.0 : 0.0;

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
            LogPerformanceSummary(_logger, report.OverallStatistics.TotalExecutions, report.StrategyPerformance.Count);

            foreach (var (strategy, performance) in report.StrategyPerformance)
            {
                LogStrategyPerformance(_logger, strategy, performance.AverageThroughput, performance.VectorizationRatio);
            }
        }
        catch (Exception ex)
        {
            LogMetricsReportError(_logger, ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _metricsReportTimer?.Dispose();
            _disposed = true;
            LogProfilerDisposed(_logger);
        }
    }
}

#region Supporting Types

/// <summary>
/// Metrics for a specific SIMD strategy
/// </summary>
internal sealed class StrategyMetrics
{
    /// <summary>
    /// Gets or sets the total executions.
    /// </summary>
    /// <value>The total executions.</value>
    public long TotalExecutions { get; set; }
    /// <summary>
    /// Gets or sets the total elements.
    /// </summary>
    /// <value>The total elements.</value>
    public long TotalElements { get; set; }
    /// <summary>
    /// Gets or sets the total duration.
    /// </summary>
    /// <value>The total duration.</value>
    public TimeSpan TotalDuration { get; set; }
    /// <summary>
    /// Gets or sets the vectorized elements.
    /// </summary>
    /// <value>The vectorized elements.</value>
    public long VectorizedElements { get; set; }
    /// <summary>
    /// Gets or sets the scalar elements.
    /// </summary>
    /// <value>The scalar elements.</value>
    public long ScalarElements { get; set; }
    /// <summary>
    /// Gets or sets the peak throughput.
    /// </summary>
    /// <value>The peak throughput.</value>
    public double PeakThroughput { get; private set; }
    private readonly List<double> _throughputHistory = [];
    private readonly List<double> _vectorizationHistory = [];
    /// <summary>
    /// Updates the throughput.
    /// </summary>
    /// <param name="throughput">The throughput.</param>

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
    /// <summary>
    /// Updates the vectorization ratio.
    /// </summary>
    /// <param name="ratio">The ratio.</param>

    public void UpdateVectorizationRatio(double ratio)
    {
        _vectorizationHistory.Add(ratio);

        // Keep only recent history
        if (_vectorizationHistory.Count > 100)
        {
            _vectorizationHistory.RemoveAt(0);
        }
    }
    /// <summary>
    /// Calculates the throughput trend.
    /// </summary>
    /// <returns>The calculated throughput trend.</returns>

    public double CalculateThroughputTrend()
    {
        return _throughputHistory.Count > 1
            ? _throughputHistory.TakeLast(10).Average() / _throughputHistory.Take(10).Average() - 1.0
            : 0.0;
    }
    /// <summary>
    /// Calculates the vectorization trend.
    /// </summary>
    /// <returns>The calculated vectorization trend.</returns>

    public double CalculateVectorizationTrend()
    {
        return _vectorizationHistory.Count > 0
            ? _vectorizationHistory.Average()
            : 0.0;
    }
    /// <summary>
    /// Calculates the efficiency trend.
    /// </summary>
    /// <returns>The calculated efficiency trend.</returns>

    public double CalculateEfficiencyTrend()
        // Simplified efficiency calculation

        => CalculateThroughputTrend() * CalculateVectorizationTrend();
    /// <summary>
    /// Calculates the consistency score.
    /// </summary>
    /// <returns>The calculated consistency score.</returns>

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
    /// <summary>
    /// Gets or sets the generated at.
    /// </summary>
    /// <value>The generated at.</value>
    public DateTimeOffset GeneratedAt { get; init; }
    /// <summary>
    /// Gets or sets the strategy performance.
    /// </summary>
    /// <value>The strategy performance.</value>
    public Dictionary<SimdExecutionStrategy, StrategyPerformance> StrategyPerformance { get; init; } = [];
    /// <summary>
    /// Gets or sets the overall statistics.
    /// </summary>
    /// <value>The overall statistics.</value>
    public OverallStatistics OverallStatistics { get; set; } = new();
}

/// <summary>
/// Performance data for a specific strategy
/// </summary>
public sealed class StrategyPerformance
{
    /// <summary>
    /// Gets or sets the strategy.
    /// </summary>
    /// <value>The strategy.</value>
    public SimdExecutionStrategy Strategy { get; init; }
    /// <summary>
    /// Gets or sets the total executions.
    /// </summary>
    /// <value>The total executions.</value>
    public long TotalExecutions { get; init; }
    /// <summary>
    /// Gets or sets the total elements.
    /// </summary>
    /// <value>The total elements.</value>
    public long TotalElements { get; init; }
    /// <summary>
    /// Gets or sets the average duration.
    /// </summary>
    /// <value>The average duration.</value>
    public TimeSpan AverageDuration { get; init; }
    /// <summary>
    /// Gets or sets the average throughput.
    /// </summary>
    /// <value>The average throughput.</value>
    public double AverageThroughput { get; init; }
    /// <summary>
    /// Gets or sets the vectorization ratio.
    /// </summary>
    /// <value>The vectorization ratio.</value>
    public double VectorizationRatio { get; init; }
    /// <summary>
    /// Gets or sets the peak throughput.
    /// </summary>
    /// <value>The peak throughput.</value>
    public double PeakThroughput { get; init; }
    /// <summary>
    /// Gets or sets the efficiency score.
    /// </summary>
    /// <value>The efficiency score.</value>
    public double EfficiencyScore { get; init; }
}

/// <summary>
/// Overall performance statistics
/// </summary>
public sealed class OverallStatistics
{
    /// <summary>
    /// Gets or sets the total executions.
    /// </summary>
    /// <value>The total executions.</value>
    public long TotalExecutions { get; init; }
    /// <summary>
    /// Gets or sets the total elements.
    /// </summary>
    /// <value>The total elements.</value>
    public long TotalElements { get; init; }
    /// <summary>
    /// Gets or sets the average vectorization ratio.
    /// </summary>
    /// <value>The average vectorization ratio.</value>
    public double AverageVectorizationRatio { get; init; }
    /// <summary>
    /// Gets or sets the best strategy.
    /// </summary>
    /// <value>The best strategy.</value>
    public SimdExecutionStrategy BestStrategy { get; init; }
    /// <summary>
    /// Gets or sets the worst strategy.
    /// </summary>
    /// <value>The worst strategy.</value>
    public SimdExecutionStrategy WorstStrategy { get; init; }
    /// <summary>
    /// Gets or sets the performance spread.
    /// </summary>
    /// <value>The performance spread.</value>
    public double PerformanceSpread { get; init; }
}

/// <summary>
/// Comparison between two strategies
/// </summary>
public sealed class StrategyComparison
{
    /// <summary>
    /// Gets or sets the strategy1.
    /// </summary>
    /// <value>The strategy1.</value>
    public SimdExecutionStrategy Strategy1 { get; init; }
    /// <summary>
    /// Gets or sets the strategy2.
    /// </summary>
    /// <value>The strategy2.</value>
    public SimdExecutionStrategy Strategy2 { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; init; }
    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    /// <value>The reason.</value>
    public string? Reason { get; init; }
    /// <summary>
    /// Gets or sets the throughput ratio.
    /// </summary>
    /// <value>The throughput ratio.</value>
    public double ThroughputRatio { get; init; }
    /// <summary>
    /// Gets or sets the vectorization difference.
    /// </summary>
    /// <value>The vectorization difference.</value>
    public double VectorizationDifference { get; init; }
    /// <summary>
    /// Gets or sets the performance gain.
    /// </summary>
    /// <value>The performance gain.</value>
    public double PerformanceGain { get; init; }
    /// <summary>
    /// Gets or sets the recommendation.
    /// </summary>
    /// <value>The recommendation.</value>
    public string? Recommendation { get; init; }
}

/// <summary>
/// SIMD-specific performance trend analysis
/// </summary>
public sealed class SimdPerformanceTrend
{
    /// <summary>
    /// Gets or sets the strategy.
    /// </summary>
    /// <value>The strategy.</value>
    public SimdExecutionStrategy Strategy { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; init; }
    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    /// <value>The reason.</value>
    public string? Reason { get; init; }
    /// <summary>
    /// Gets or sets the throughput trend.
    /// </summary>
    /// <value>The throughput trend.</value>
    public double ThroughputTrend { get; init; }
    /// <summary>
    /// Gets or sets the vectorization trend.
    /// </summary>
    /// <value>The vectorization trend.</value>
    public double VectorizationTrend { get; init; }
    /// <summary>
    /// Gets or sets the efficiency trend.
    /// </summary>
    /// <value>The efficiency trend.</value>
    public double EfficiencyTrend { get; init; }
    /// <summary>
    /// Gets or sets the recommendations.
    /// </summary>
    /// <value>The recommendations.</value>
    public IReadOnlyList<string> Recommendations { get; init; } = [];
}

/// <summary>
/// Strategy recommendation based on performance analysis
/// </summary>
public sealed class StrategyRecommendation
{
    /// <summary>
    /// Gets or sets the recommended strategy.
    /// </summary>
    /// <value>The recommended strategy.</value>
    public SimdExecutionStrategy RecommendedStrategy { get; init; }
    /// <summary>
    /// Gets or sets the confidence.
    /// </summary>
    /// <value>The confidence.</value>
    public double Confidence { get; init; }
    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    /// <value>The reason.</value>
    public string? Reason { get; init; }
    /// <summary>
    /// Gets or sets the alternative strategies.
    /// </summary>
    /// <value>The alternative strategies.</value>
    public IReadOnlyList<SimdExecutionStrategy> AlternativeStrategies { get; init; } = [];
}



#endregion