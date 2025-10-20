// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using PerformanceTrend = DotCompute.Abstractions.Types.PerformanceTrend;
using TrendDirection = DotCompute.Abstractions.Types.TrendDirection;
using AnomalyType = DotCompute.Abstractions.Debugging.AnomalyType;
using AnomalySeverity = DotCompute.Abstractions.Debugging.AnomalySeverity;

namespace DotCompute.Core.Debugging.Infrastructure;

/// <summary>
/// Collects and aggregates debugging metrics and performance data.
/// </summary>
public sealed partial class DebugMetricsCollector : IDisposable
{
    private readonly ILogger<DebugMetricsCollector> _logger;
    private readonly ConcurrentDictionary<string, MetricsSeries> _metricsData;
    private readonly Timer _metricsCollectionTimer;
    private readonly DebugServiceOptions _options;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the DebugMetricsCollector class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>

    public DebugMetricsCollector(ILogger<DebugMetricsCollector> logger, DebugServiceOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? DebugServiceOptions.Development;
        _metricsData = new ConcurrentDictionary<string, MetricsSeries>();

        // Start periodic metrics collection
        _metricsCollectionTimer = new Timer(CollectSystemMetrics, null,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Records a debug metric.
    /// </summary>
    /// <param name="name">The metric name.</param>
    /// <param name="value">The metric value.</param>
    /// <param name="tags">Optional tags for categorization.</param>
    public void RecordMetric(string name, double value, Dictionary<string, string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var timestamp = DateTime.UtcNow;
        var metricPoint = new MetricPoint
        {
            Timestamp = timestamp,
            Value = value,
            Tags = tags ?? []
        };

        _ = _metricsData.AddOrUpdate(name,
            _ =>
            {
                var points = new Collection<MetricPoint> { metricPoint };
                return new MetricsSeries { Name = name, Points = points };
            },
            (key, existing) =>
            {
                existing.Points.Add(metricPoint);
                // Keep only recent points to prevent memory bloat
                if (existing.Points.Count > _options.MaxMetricPoints)
                {
                    // Remove old points - Collection doesn't have RemoveRange, so we'll keep the last N points
                    var pointsToKeep = existing.Points.Skip(existing.Points.Count - _options.MaxMetricPoints).ToList();
                    existing.Points.Clear();
                    foreach (var point in pointsToKeep)
                    {
                        existing.Points.Add(point);
                    }
                }
                return existing;
            });

        LogMetricRecorded(name, value);
    }

    /// <summary>
    /// Records execution timing metrics.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <param name="executionTime">The execution time.</param>
    /// <param name="success">Whether execution was successful.</param>
    public void RecordExecutionMetrics(string kernelName, AcceleratorType acceleratorType, TimeSpan executionTime, bool success)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tags = new Dictionary<string, string>
        {
            ["kernel"] = kernelName,
            ["accelerator"] = acceleratorType.ToString(),
            ["success"] = success.ToString()
        };

        // Record execution time
        RecordMetric("kernel.execution_time_ms", executionTime.TotalMilliseconds, tags);

        // Record success/failure count
        RecordMetric("kernel.execution_count", 1.0, tags);

        // Record separate success and failure metrics
        if (success)
        {
            RecordMetric("kernel.success_count", 1.0, tags);
        }
        else
        {
            RecordMetric("kernel.failure_count", 1.0, tags);
        }

        LogExecutionMetricsRecorded(kernelName, acceleratorType.ToString(), executionTime.TotalMilliseconds, success);
    }

    /// <summary>
    /// Records memory usage metrics.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="beforeMemory">Memory before execution.</param>
    /// <param name="afterMemory">Memory after execution.</param>
    /// <param name="peakMemory">Peak memory during execution.</param>
    public void RecordMemoryMetrics(string kernelName, long beforeMemory, long afterMemory, long? peakMemory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tags = new Dictionary<string, string> { ["kernel"] = kernelName };

        RecordMetric("kernel.memory_before_bytes", beforeMemory, tags);
        RecordMetric("kernel.memory_after_bytes", afterMemory, tags);
        RecordMetric("kernel.memory_allocated_bytes", Math.Max(0, afterMemory - beforeMemory), tags);

        if (peakMemory.HasValue)
        {
            RecordMetric("kernel.memory_peak_bytes", peakMemory.Value, tags);
        }

        LogMemoryMetricsRecorded(kernelName, beforeMemory, afterMemory);
    }

    /// <summary>
    /// Gets metrics summary for a specific metric name.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="timeRange">Optional time range filter.</param>
    /// <returns>Metrics summary or null if not found.</returns>
    public MetricsSummary? GetMetricsSummary(string metricName, TimeSpan? timeRange = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_metricsData.TryGetValue(metricName, out var series))
        {
            return null;
        }

        var points = series.Points.AsEnumerable();

        // Apply time filter if specified
        if (timeRange.HasValue)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeRange.Value);
            points = points.Where(p => p.Timestamp >= cutoffTime);
        }

        var pointsList = points.ToList();
        if (pointsList.Count == 0)
        {
            return null;
        }

        var values = pointsList.Select(p => p.Value).ToList();

        return new MetricsSummary
        {
            MetricName = metricName,
            Count = pointsList.Count,
            Sum = values.Sum(),
            Average = values.Average(),
            Minimum = values.Min(),
            Maximum = values.Max(),
            StandardDeviation = CalculateStandardDeviation(values),
            Median = CalculateMedian(values),
            Percentile95 = CalculatePercentile(values, 95),
            Percentile99 = CalculatePercentile(values, 99),
            FirstRecorded = pointsList.Min(p => p.Timestamp),
            LastRecorded = pointsList.Max(p => p.Timestamp)
        };
    }

    /// <summary>
    /// Gets all available metric names.
    /// </summary>
    /// <returns>Collection of metric names.</returns>
    public IEnumerable<string> GetAvailableMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _metricsData.Keys.ToList();
    }

    /// <summary>
    /// Gets comprehensive metrics report.
    /// </summary>
    /// <param name="timeRange">Optional time range filter.</param>
    /// <returns>Comprehensive metrics report.</returns>
    public MetricsReport GetMetricsReport(TimeSpan? timeRange = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var report = new MetricsReport
        {
            GeneratedAt = DateTime.UtcNow,
            TimeRange = timeRange,
            MetricsSummaries = [],
            SystemMetrics = CollectCurrentSystemMetrics()
        };

        foreach (var metricName in GetAvailableMetrics())
        {
            var summary = GetMetricsSummary(metricName, timeRange);
            if (summary != null)
            {
                report.MetricsSummaries[metricName] = summary;
            }
        }

        LogMetricsReportGenerated(report.MetricsSummaries.Count);

        return report;
    }

    /// <summary>
    /// Gets performance trends for a specific metric.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="timeRange">Time range for trend analysis.</param>
    /// <returns>Performance trend analysis.</returns>
    public PerformanceTrend GetPerformanceTrend(string metricName, TimeSpan timeRange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_metricsData.TryGetValue(metricName, out var series))
        {
            return new PerformanceTrend
            {
                MetricName = metricName,
                TimeRange = timeRange,
                DataPoints = 0,
                TrendDirection = TrendDirection.Unknown
            };
        }

        var cutoffTime = DateTime.UtcNow.Subtract(timeRange);
        var recentPoints = series.Points
            .Where(p => p.Timestamp >= cutoffTime)
            .OrderBy(p => p.Timestamp)
            .ToList();

        if (recentPoints.Count < 3)
        {
            return new PerformanceTrend
            {
                MetricName = metricName,
                TimeRange = timeRange,
                DataPoints = recentPoints.Count,
                TrendDirection = TrendDirection.Unknown,
                AnalysisTime = DateTime.UtcNow
            };
        }

        var trend = AnalyzeTrend(recentPoints, metricName, timeRange);

        LogTrendAnalysisCompleted(metricName, recentPoints.Count, trend.TrendDirection.ToString());

        return trend;
    }

    /// <summary>
    /// Detects anomalies in metrics data.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="sensitivityFactor">Sensitivity factor for anomaly detection (default: 2.0).</param>
    /// <returns>Collection of detected anomalies.</returns>
    public IEnumerable<MetricAnomaly> DetectAnomalies(string metricName, double sensitivityFactor = 2.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricName);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sensitivityFactor, 0);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_metricsData.TryGetValue(metricName, out var series) || series.Points.Count < 10)
        {
            return Enumerable.Empty<MetricAnomaly>();
        }

        var anomalies = new List<MetricAnomaly>();
        var values = series.Points.Select(p => p.Value).ToList();
        var mean = values.Average();
        var stdDev = CalculateStandardDeviation(values);
        var threshold = mean + (sensitivityFactor * stdDev);
        var lowerThreshold = mean - (sensitivityFactor * stdDev);

        foreach (var point in series.Points)
        {
            if (point.Value > threshold || point.Value < lowerThreshold)
            {
                anomalies.Add(new MetricAnomaly
                {
                    MetricName = metricName,
                    Timestamp = point.Timestamp,
                    Value = point.Value,
                    ExpectedValue = mean,
                    Deviation = Math.Abs(point.Value - mean),
                    Severity = Math.Abs(point.Value - mean) > (3 * stdDev) ? AnomalySeverity.High : AnomalySeverity.Medium,
                    Type = point.Value > threshold ? AnomalyType.ExecutionTime : AnomalyType.MemoryUsage
                });
            }
        }

        LogAnomaliesDetected(metricName, anomalies.Count);

        return anomalies.OrderByDescending(a => a.Timestamp);
    }

    /// <summary>
    /// Clears metrics data.
    /// </summary>
    /// <param name="metricName">Optional metric name to clear specific metric.</param>
    public void ClearMetrics(string? metricName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(metricName))
        {
            _metricsData.Clear();
            LogAllMetricsCleared();
        }
        else
        {
            _ = _metricsData.TryRemove(metricName, out _);
            LogMetricCleared(metricName);
        }
    }

    /// <summary>
    /// Collects system metrics periodically.
    /// </summary>
    private void CollectSystemMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var systemMetrics = CollectCurrentSystemMetrics();

            RecordMetric("system.memory_total_bytes", systemMetrics.TotalMemory);
            RecordMetric("system.memory_used_bytes", systemMetrics.UsedMemory);
            RecordMetric("system.cpu_usage_percent", systemMetrics.CpuUsage);
            RecordMetric("system.thread_count", systemMetrics.ThreadCount);
            RecordMetric("system.handle_count", systemMetrics.HandleCount);
        }
        catch (Exception ex)
        {
            LogSystemMetricsCollectionFailed(ex.Message);
        }
    }

    /// <summary>
    /// Collects current system metrics.
    /// </summary>
    private static SystemMetrics CollectCurrentSystemMetrics()
    {
        var process = Process.GetCurrentProcess();

        return new SystemMetrics
        {
            TotalMemory = GC.GetTotalMemory(false),
            UsedMemory = process.WorkingSet64,
            CpuUsage = GetCpuUsage(),
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            GCGen0Collections = GC.CollectionCount(0),
            GCGen1Collections = GC.CollectionCount(1),
            GCGen2Collections = GC.CollectionCount(2)
        };
    }

    /// <summary>
    /// Gets current CPU usage percentage.
    /// </summary>
    private static double GetCpuUsage()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount;
        }
        catch
        {
            return 0.0; // Return 0 if unable to get CPU usage
        }
    }

    #region Statistical Calculations

    private static double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valuesList = values.ToList();
        if (valuesList.Count == 0)
        {
            return 0;
        }


        var mean = valuesList.Average();
        var sumOfSquaredDifferences = valuesList.Sum(val => Math.Pow(val - mean, 2));
        return Math.Sqrt(sumOfSquaredDifferences / valuesList.Count);
    }

    private static double CalculateMedian(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var count = sorted.Count;

        if (count == 0)
        {
            return 0;
        }


        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
        return sorted[count / 2];
    }

    private static double CalculatePercentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.OrderBy(x => x).ToList();
        if (sorted.Count == 0)
        {
            return 0;
        }


        var index = (percentile / 100.0) * (sorted.Count - 1);
        var lowerIndex = (int)Math.Floor(index);
        var upperIndex = (int)Math.Ceiling(index);

        if (lowerIndex == upperIndex)
        {
            return sorted[lowerIndex];
        }

        var weight = index - lowerIndex;
        return sorted[lowerIndex] * (1 - weight) + sorted[upperIndex] * weight;
    }

    private static PerformanceTrend AnalyzeTrend(IReadOnlyList<MetricPoint> points, string metricName, TimeSpan timeRange)
    {
        if (points.Count < 3)
        {
            return new PerformanceTrend
            {
                MetricName = metricName,
                TimeRange = timeRange,
                DataPoints = points.Count,
                TrendDirection = TrendDirection.Unknown,
                AnalysisTime = DateTime.UtcNow
            };
        }

        // Simple linear regression to determine trend
        var times = points.Select((p, i) => (double)i).ToList();
        var values = points.Select(p => p.Value).ToList();

        var slope = CalculateSlope(times, values);

        var trendDirection = slope switch
        {
            > 0.1 => TrendDirection.Degrading,
            < -0.1 => TrendDirection.Improving,
            _ => TrendDirection.Stable
        };

        var firstValue = points[0].Value;
        var lastValue = points[^1].Value;
        var averageChange = (lastValue - firstValue) / points.Count;
        var percentChange = firstValue != 0 ? (lastValue - firstValue) / firstValue : 0;

        return new PerformanceTrend
        {
            MetricName = metricName,
            TimeRange = timeRange,
            DataPoints = points.Count,
            TrendDirection = trendDirection,
            RateOfChange = slope,
            Magnitude = Math.Abs(slope),
            PercentChange = percentChange,
            PerformanceChange = averageChange,
            AnalysisTime = DateTime.UtcNow,
            Confidence = points.Count >= 10 ? 0.9 : points.Count / 10.0
        };
    }

    private static double CalculateSlope(List<double> x, IReadOnlyList<double> y)
    {
        if (x.Count != y.Count || x.Count < 2)
        {
            return 0;
        }


        var n = x.Count;
        var sumX = x.Sum();
        var sumY = y.Sum();
        var sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
        var sumX2 = x.Sum(xi => xi * xi);

        var denominator = n * sumX2 - sumX * sumX;
        return denominator != 0 ? (n * sumXY - sumX * sumY) / denominator : 0;
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
            _metricsCollectionTimer?.Dispose();
            _metricsData.Clear();
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = MsLogLevel.Debug, Message = "Metric recorded: {MetricName} = {Value}")]
    private partial void LogMetricRecorded(string metricName, double value);

    [LoggerMessage(Level = MsLogLevel.Debug, Message = "Execution metrics recorded for {KernelName} on {AcceleratorType}: {ExecutionTimeMs}ms, Success={Success}")]
    private partial void LogExecutionMetricsRecorded(string kernelName, string acceleratorType, double executionTimeMs, bool success);

    [LoggerMessage(Level = MsLogLevel.Debug, Message = "Memory metrics recorded for {KernelName}: Before={BeforeMemory}, After={AfterMemory}")]
    private partial void LogMemoryMetricsRecorded(string kernelName, long beforeMemory, long afterMemory);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Metrics report generated with {MetricCount} metrics")]
    private partial void LogMetricsReportGenerated(int metricCount);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Trend analysis completed for {MetricName}: {DataPoints} points, trend: {TrendDirection}")]
    private partial void LogTrendAnalysisCompleted(string metricName, int dataPoints, string trendDirection);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Anomalies detected for {MetricName}: {AnomalyCount} anomalies")]
    private partial void LogAnomaliesDetected(string metricName, int anomalyCount);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "All metrics cleared")]
    private partial void LogAllMetricsCleared();

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Metric cleared: {MetricName}")]
    private partial void LogMetricCleared(string metricName);

    [LoggerMessage(Level = MsLogLevel.Error, Message = "System metrics collection failed: {Error}")]
    private partial void LogSystemMetricsCollectionFailed(string error);

    #endregion
}