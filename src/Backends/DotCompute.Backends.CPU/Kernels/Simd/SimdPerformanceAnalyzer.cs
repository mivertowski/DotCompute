// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Simd;

/// <summary>
/// Performance analysis and metrics collection for SIMD operations.
/// Tracks execution statistics, performance trends, and optimization effectiveness.
/// </summary>
public sealed partial class SimdPerformanceAnalyzer : IDisposable
{
    private readonly ILogger<SimdPerformanceAnalyzer> _logger;
    private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
    private readonly Timer _metricsReportTimer;

    // Atomic counters for thread-safe statistics
    private long _totalExecutions;
    private long _totalElements;
    private long _vectorizedElements;
    private long _scalarElements;
    private long _totalExecutionTime;
    private long _totalReductions;
    private volatile bool _disposed;

    // Strategy-specific counters
    private readonly ConcurrentDictionary<SimdExecutionStrategy, long> _strategyUsage;
    private readonly ConcurrentDictionary<ReductionOperation, long> _reductionUsage;

    // LoggerMessage delegates - Event IDs 7620-7639
    [LoggerMessage(EventId = 7620, Level = LogLevel.Debug, Message = "SIMD performance analyzer initialized")]
    private partial void LogAnalyzerInitialized();

    [LoggerMessage(EventId = 7621, Level = LogLevel.Information, Message = "Performance analyzer metrics reset")]
    private partial void LogMetricsReset();

    [LoggerMessage(EventId = 7622, Level = LogLevel.Information, Message = "SIMD Performance Metrics - Executions: {Executions}, Vectorization: {VectorizationRatio:P2}, Performance Gain: {PerformanceGain:F2}x, Avg Time: {AvgTime:F2}ms")]
    private partial void LogPerformanceMetrics(long executions, double vectorizationRatio, double performanceGain, double avgTime);

    [LoggerMessage(EventId = 7623, Level = LogLevel.Warning, Message = "Error reporting SIMD performance metrics")]
    private partial void LogMetricsReportError(Exception ex);

    [LoggerMessage(EventId = 7624, Level = LogLevel.Debug, Message = "SIMD performance analyzer disposed")]
    private partial void LogAnalyzerDisposed();

    /// <summary>
    /// Initializes a new instance of the SimdPerformanceAnalyzer class.
    /// </summary>
    /// <param name="logger">The logger.</param>

    public SimdPerformanceAnalyzer(ILogger<SimdPerformanceAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = new ConcurrentDictionary<string, PerformanceMetric>();
        _strategyUsage = new ConcurrentDictionary<SimdExecutionStrategy, long>();
        _reductionUsage = new ConcurrentDictionary<ReductionOperation, long>();

        // Initialize strategy counters
        foreach (var strategy in Enum.GetValues<SimdExecutionStrategy>())
        {
            _strategyUsage[strategy] = 0;
        }

        // Initialize reduction counters
        foreach (var operation in Enum.GetValues<ReductionOperation>())
        {
            _reductionUsage[operation] = 0;
        }

        // Set up periodic metrics reporting
        _metricsReportTimer = new Timer(ReportMetrics, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        LogAnalyzerInitialized();
    }

    /// <summary>
    /// Records the start of a kernel execution.
    /// </summary>
    /// <param name="elementCount">Number of elements being processed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordExecutionStart(long elementCount)
    {
        _ = Interlocked.Increment(ref _totalExecutions);
        _ = Interlocked.Add(ref _totalElements, elementCount);
    }

    /// <summary>
    /// Records the completion of a kernel execution.
    /// </summary>
    /// <param name="elementCount">Number of elements processed.</param>
    /// <param name="executionTime">Time taken for execution.</param>
    /// <param name="strategy">Strategy used for execution.</param>
    public void RecordExecutionComplete(long elementCount, TimeSpan executionTime, SimdExecutionStrategy strategy)
    {
        _ = Interlocked.Add(ref _totalExecutionTime, executionTime.Ticks);
        _ = _strategyUsage.AddOrUpdate(strategy, 1, (key, old) => old + 1);

        // Record detailed metrics for analysis
        var metricKey = $"execution_{strategy}_{GetElementCountCategory(elementCount)}";
        var metric = _metrics.GetOrAdd(metricKey, k => new PerformanceMetric(k));
        metric.RecordExecution(executionTime, elementCount);
    }

    /// <summary>
    /// Records the number of elements processed using vectorized instructions.
    /// </summary>
    /// <param name="elements">Number of vectorized elements.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordVectorizedElements(long elements) => _ = Interlocked.Add(ref _vectorizedElements, elements);

    /// <summary>
    /// Records the number of elements processed using scalar instructions.
    /// </summary>
    /// <param name="elements">Number of scalar elements.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordScalarElements(long elements) => _ = Interlocked.Add(ref _scalarElements, elements);

    /// <summary>
    /// Records the start of a reduction operation.
    /// </summary>
    /// <param name="elementCount">Number of elements in reduction.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordReductionStart(int elementCount) => _ = Interlocked.Increment(ref _totalReductions);

    /// <summary>
    /// Records the completion of a reduction operation.
    /// </summary>
    /// <param name="elementCount">Number of elements reduced.</param>
    /// <param name="executionTime">Time taken for reduction.</param>
    /// <param name="operation">Type of reduction operation.</param>
    public void RecordReductionComplete(int elementCount, TimeSpan executionTime, ReductionOperation operation)
    {
        _ = _reductionUsage.AddOrUpdate(operation, 1, (key, old) => old + 1);

        var metricKey = $"reduction_{operation}_{GetElementCountCategory(elementCount)}";
        var metric = _metrics.GetOrAdd(metricKey, k => new PerformanceMetric(k));
        metric.RecordExecution(executionTime, elementCount);
    }

    /// <summary>
    /// Gets comprehensive executor performance statistics.
    /// </summary>
    public ExecutorStatistics GetStatistics()
    {
        return new ExecutorStatistics
        {
            TotalExecutions = Interlocked.Read(ref _totalExecutions),
            TotalElements = Interlocked.Read(ref _totalElements),
            VectorizedElements = Interlocked.Read(ref _vectorizedElements),
            ScalarElements = Interlocked.Read(ref _scalarElements),
            AverageExecutionTime = CalculateAverageExecutionTime(),
            VectorizationRatio = CalculateVectorizationRatio(),
            PerformanceGain = CalculatePerformanceGain(),
            TotalReductions = Interlocked.Read(ref _totalReductions),
            StrategyDistribution = GetStrategyDistribution(),
            ReductionDistribution = GetReductionDistribution()
        };
    }

    /// <summary>
    /// Gets detailed performance metrics for a specific operation type.
    /// </summary>
    /// <param name="operationType">Type of operation (e.g., "execution_Avx512_large").</param>
    /// <returns>Performance metrics or null if not found.</returns>
    public PerformanceMetricSnapshot? GetMetrics(string operationType)
    {
        if (_metrics.TryGetValue(operationType, out var metric))
        {
            return metric.GetSnapshot();
        }
        return null;
    }

    /// <summary>
    /// Analyzes performance trends over time.
    /// </summary>
    /// <returns>Performance trend analysis.</returns>
    public PerformanceTrendAnalysis AnalyzeTrends()
    {
        var analysis = new PerformanceTrendAnalysis
        {
            AnalysisTime = DateTimeOffset.UtcNow,
            TotalOperations = Interlocked.Read(ref _totalExecutions) + Interlocked.Read(ref _totalReductions),
            VectorizationEffectiveness = CalculateVectorizationRatio(),
            AveragePerformance = CalculateAverageExecutionTime(),
            PreferredStrategy = GetMostUsedStrategy(),
            PerformanceGainTrend = AnalyzePerformanceGainTrend()
        };

        return analysis;
    }

    /// <summary>
    /// Resets all performance counters and metrics.
    /// </summary>
    public void Reset()
    {
        _ = Interlocked.Exchange(ref _totalExecutions, 0);
        _ = Interlocked.Exchange(ref _totalElements, 0);
        _ = Interlocked.Exchange(ref _vectorizedElements, 0);
        _ = Interlocked.Exchange(ref _scalarElements, 0);
        _ = Interlocked.Exchange(ref _totalExecutionTime, 0);
        _ = Interlocked.Exchange(ref _totalReductions, 0);

        _metrics.Clear();

        foreach (var strategy in _strategyUsage.Keys.ToArray())
        {
            _strategyUsage[strategy] = 0;
        }

        foreach (var operation in _reductionUsage.Keys.ToArray())
        {
            _reductionUsage[operation] = 0;
        }

        LogMetricsReset();
    }

    #region Private Helper Methods

    private TimeSpan CalculateAverageExecutionTime()
    {
        var totalTime = Interlocked.Read(ref _totalExecutionTime);
        var totalExecs = Interlocked.Read(ref _totalExecutions);
        return totalExecs > 0 ? TimeSpan.FromTicks(totalTime / totalExecs) : TimeSpan.Zero;
    }

    private double CalculateVectorizationRatio()
    {
        var vectorized = Interlocked.Read(ref _vectorizedElements);
        var total = Interlocked.Read(ref _totalElements);
        return total > 0 ? (double)vectorized / total : 0.0;
    }

    private double CalculatePerformanceGain()
    {
        // Estimate performance gain based on vectorization ratio and typical gains
        var vectorizationRatio = CalculateVectorizationRatio();
        var baseGain = GetEstimatedVectorGain();
        return 1.0 + (baseGain - 1.0) * vectorizationRatio;
    }

    private double GetEstimatedVectorGain()
    {
        // Estimate based on most used strategy
        var mostUsed = GetMostUsedStrategy();
        return mostUsed switch
        {
            SimdExecutionStrategy.Avx512 => 16.0,
            SimdExecutionStrategy.Avx2 => 8.0,
            SimdExecutionStrategy.Sse => 4.0,
            SimdExecutionStrategy.Neon => 4.0,
            _ => 1.0
        };
    }

    private SimdExecutionStrategy GetMostUsedStrategy() => _strategyUsage.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;

    private Dictionary<SimdExecutionStrategy, long> GetStrategyDistribution() => _strategyUsage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    private Dictionary<ReductionOperation, long> GetReductionDistribution() => _reductionUsage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    private static string GetElementCountCategory(long elementCount)
    {
        return elementCount switch
        {
            < 100 => "small",
            < 1000 => "medium",
            < 10000 => "large",
            _ => "xlarge"
        };
    }

    private PerformanceGainTrend AnalyzePerformanceGainTrend()
    {
        var vectorizationRatio = CalculateVectorizationRatio();

        if (vectorizationRatio > 0.8)
        {
            return PerformanceGainTrend.Excellent;
        }
        else if (vectorizationRatio > 0.6)
        {
            return PerformanceGainTrend.Good;
        }
        else if (vectorizationRatio > 0.4)
        {
            return PerformanceGainTrend.Moderate;
        }
        else
        {
            return PerformanceGainTrend.Poor;
        }
    }

    private void ReportMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var stats = GetStatistics();
            LogPerformanceMetrics(
                stats.TotalExecutions,
                stats.VectorizationRatio,
                stats.PerformanceGain,
                stats.AverageExecutionTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            LogMetricsReportError(ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _metricsReportTimer?.Dispose();
            LogAnalyzerDisposed();
        }
    }
}

#region Supporting Types

/// <summary>
/// Thread-safe performance metric for tracking individual operation types.
/// </summary>
public sealed class PerformanceMetric(string name)
{
    private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));
    private long _executionCount;
    private long _totalTime;
    private long _totalElements;
    private long _minTime = long.MaxValue;
    private long _maxTime = long.MinValue;
    /// <summary>
    /// Performs record execution.
    /// </summary>
    /// <param name="executionTime">The execution time.</param>
    /// <param name="elementCount">The element count.</param>

    public void RecordExecution(TimeSpan executionTime, long elementCount)
    {
        var ticks = executionTime.Ticks;

        _ = Interlocked.Increment(ref _executionCount);
        _ = Interlocked.Add(ref _totalTime, ticks);
        _ = Interlocked.Add(ref _totalElements, elementCount);

        // Update min/max with thread-safe compare-exchange
        var currentMin = Interlocked.Read(ref _minTime);
        while (ticks < currentMin && Interlocked.CompareExchange(ref _minTime, ticks, currentMin) != currentMin)
        {
            currentMin = Interlocked.Read(ref _minTime);
        }

        var currentMax = Interlocked.Read(ref _maxTime);
        while (ticks > currentMax && Interlocked.CompareExchange(ref _maxTime, ticks, currentMax) != currentMax)
        {
            currentMax = Interlocked.Read(ref _maxTime);
        }
    }
    /// <summary>
    /// Gets the snapshot.
    /// </summary>
    /// <returns>The snapshot.</returns>

    public PerformanceMetricSnapshot GetSnapshot()
    {
        var execCount = Interlocked.Read(ref _executionCount);
        var totalTime = Interlocked.Read(ref _totalTime);
        var totalElements = Interlocked.Read(ref _totalElements);
        var minTime = Interlocked.Read(ref _minTime);
        var maxTime = Interlocked.Read(ref _maxTime);

        return new PerformanceMetricSnapshot
        {
            Name = _name,
            ExecutionCount = execCount,
            TotalElements = totalElements,
            AverageTime = execCount > 0 ? TimeSpan.FromTicks(totalTime / execCount) : TimeSpan.Zero,
            MinTime = minTime != long.MaxValue ? TimeSpan.FromTicks(minTime) : TimeSpan.Zero,
            MaxTime = maxTime != long.MinValue ? TimeSpan.FromTicks(maxTime) : TimeSpan.Zero,
            ElementsPerSecond = totalTime > 0 ? (double)totalElements * TimeSpan.TicksPerSecond / totalTime : 0.0
        };
    }
}

/// <summary>
/// Immutable snapshot of performance metrics.
/// </summary>
public readonly record struct PerformanceMetricSnapshot
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; init; }
    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    /// <value>The execution count.</value>
    public long ExecutionCount { get; init; }
    /// <summary>
    /// Gets or sets the total elements.
    /// </summary>
    /// <value>The total elements.</value>
    public long TotalElements { get; init; }
    /// <summary>
    /// Gets or sets the average time.
    /// </summary>
    /// <value>The average time.</value>
    public TimeSpan AverageTime { get; init; }
    /// <summary>
    /// Gets or sets the min time.
    /// </summary>
    /// <value>The min time.</value>
    public TimeSpan MinTime { get; init; }
    /// <summary>
    /// Gets or sets the max time.
    /// </summary>
    /// <value>The max time.</value>
    public TimeSpan MaxTime { get; init; }
    /// <summary>
    /// Gets or sets the elements per second.
    /// </summary>
    /// <value>The elements per second.</value>
    public double ElementsPerSecond { get; init; }
}

/// <summary>
/// Enhanced performance statistics with detailed breakdowns.
/// </summary>
public readonly record struct ExecutorStatistics
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
    /// Gets or sets the vectorized elements.
    /// </summary>
    /// <value>The vectorized elements.</value>
    public long VectorizedElements { get; init; }
    /// <summary>
    /// Gets or sets the scalar elements.
    /// </summary>
    /// <value>The scalar elements.</value>
    public long ScalarElements { get; init; }
    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    /// <value>The average execution time.</value>
    public TimeSpan AverageExecutionTime { get; init; }
    /// <summary>
    /// Gets or sets the vectorization ratio.
    /// </summary>
    /// <value>The vectorization ratio.</value>
    public double VectorizationRatio { get; init; }
    /// <summary>
    /// Gets or sets the performance gain.
    /// </summary>
    /// <value>The performance gain.</value>
    public double PerformanceGain { get; init; }
    /// <summary>
    /// Gets or sets the total reductions.
    /// </summary>
    /// <value>The total reductions.</value>
    public long TotalReductions { get; init; }
    /// <summary>
    /// Gets or sets the strategy distribution.
    /// </summary>
    /// <value>The strategy distribution.</value>
    public Dictionary<SimdExecutionStrategy, long> StrategyDistribution { get; init; }
    /// <summary>
    /// Gets or sets the reduction distribution.
    /// </summary>
    /// <value>The reduction distribution.</value>
    public Dictionary<ReductionOperation, long> ReductionDistribution { get; init; }
}

/// <summary>
/// Performance trend analysis over time.
/// </summary>
public readonly record struct PerformanceTrendAnalysis
{
    /// <summary>
    /// Gets or sets the analysis time.
    /// </summary>
    /// <value>The analysis time.</value>
    public DateTimeOffset AnalysisTime { get; init; }
    /// <summary>
    /// Gets or sets the total operations.
    /// </summary>
    /// <value>The total operations.</value>
    public long TotalOperations { get; init; }
    /// <summary>
    /// Gets or sets the vectorization effectiveness.
    /// </summary>
    /// <value>The vectorization effectiveness.</value>
    public double VectorizationEffectiveness { get; init; }
    /// <summary>
    /// Gets or sets the average performance.
    /// </summary>
    /// <value>The average performance.</value>
    public TimeSpan AveragePerformance { get; init; }
    /// <summary>
    /// Gets or sets the preferred strategy.
    /// </summary>
    /// <value>The preferred strategy.</value>
    public SimdExecutionStrategy PreferredStrategy { get; init; }
    /// <summary>
    /// Gets or sets the performance gain trend.
    /// </summary>
    /// <value>The performance gain trend.</value>
    public PerformanceGainTrend PerformanceGainTrend { get; init; }
}
/// <summary>
/// An performance gain trend enumeration.
/// </summary>

/// <summary>
/// Performance gain trend indicators.
/// </summary>
public enum PerformanceGainTrend
{
    Poor,
    Moderate,
    Good,
    Excellent
}



#endregion
