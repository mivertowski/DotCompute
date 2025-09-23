// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Debugging.Analytics;

/// <summary>
/// Provides comprehensive performance profiling and analysis for kernel execution.
/// </summary>
public sealed partial class KernelProfiler : IDisposable
{
    private readonly ILogger<KernelProfiler> _logger;
    private readonly ConcurrentDictionary<string, ProfilingSession> _activeSessions;
    private readonly ConcurrentDictionary<string, List<ProfilingData>> _historicalData;
    private readonly Timer _performanceMonitor;
    private DebugServiceOptions _options;
    private bool _disposed;

    public KernelProfiler(ILogger<KernelProfiler> logger, DebugServiceOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeSessions = new ConcurrentDictionary<string, ProfilingSession>();
        _historicalData = new ConcurrentDictionary<string, List<ProfilingData>>();
        _options = options ?? new DebugServiceOptions();

        // Start performance monitoring
        _performanceMonitor = new Timer(MonitorPerformance, null,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Starts a profiling session for a kernel.
    /// </summary>
    /// <param name="sessionId">Unique session identifier.</param>
    /// <param name="kernel">The kernel to profile.</param>
    /// <param name="accelerator">The accelerator being used.</param>
    /// <returns>Profiling session information.</returns>
    public ProfilingSession StartProfiling(string sessionId, IKernel kernel, IAccelerator accelerator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(accelerator);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var session = new ProfilingSession
        {
            SessionId = sessionId,
            KernelName = kernel.Name,
            AcceleratorType = accelerator.Type,
            StartTime = DateTime.UtcNow,
            StartMemory = GC.GetTotalMemory(false),
            StartCpuTime = GetCurrentCpuTime().TotalMilliseconds,
            IsActive = true
        };

        if (_activeSessions.TryAdd(sessionId, session))
        {
            LogProfilingStarted(sessionId, kernel.Name, accelerator.Type.ToString());
        }
        else
        {
            LogSessionAlreadyExists(sessionId);
            throw new InvalidOperationException($"Profiling session '{sessionId}' already exists.");
        }

        return session;
    }

    /// <summary>
    /// Stops a profiling session and generates analysis.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="result">The execution result.</param>
    /// <param name="error">Optional error that occurred.</param>
    /// <returns>Comprehensive profiling data.</returns>
    public ProfilingData StopProfiling(string sessionId, object? result = null, Exception? error = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_activeSessions.TryRemove(sessionId, out var session))
        {
            LogSessionNotFound(sessionId);
            throw new InvalidOperationException($"Profiling session '{sessionId}' not found.");
        }

        var endTime = DateTime.UtcNow;
        var endMemory = GC.GetTotalMemory(false);
        var endCpuTime = GetCurrentCpuTime();

        var profilingData = new ProfilingData
        {
            SessionId = sessionId,
            KernelName = session.KernelName,
            AcceleratorType = session.AcceleratorType,
            StartTime = session.StartTime,
            EndTime = endTime,
            ExecutionTime = endTime - session.StartTime,
            MemoryUsage = new MemoryProfilingData
            {
                StartMemory = session.StartMemory,
                EndMemory = endMemory,
                PeakMemory = Math.Max(session.StartMemory, endMemory),
                AllocatedMemory = Math.Max(0, endMemory - session.StartMemory),
                GCCollections = GetGCCollectionCounts().Values.Sum()
            },
            CpuUsage = new CpuProfilingData
            {
                StartCpuTime = session.StartCpuTime,
                EndCpuTime = endCpuTime.TotalMilliseconds,
                CpuTime = endCpuTime - TimeSpan.FromMilliseconds(session.StartCpuTime),
                CpuUtilization = CalculateCpuUtilization(session.StartCpuTime, endCpuTime, session.StartTime, endTime)
            },
            Result = result,
            Error = error,
            Success = error == null,
            PerformanceMetrics = ConvertToPerformanceMetricsDictionary(CalculatePerformanceMetrics(session, endTime, endMemory, endCpuTime))
        };

        // Store historical data
        var kernelKey = $"{session.KernelName}_{session.AcceleratorType}";
        _historicalData.AddOrUpdate(kernelKey,
            [profilingData],
            (key, existing) =>
            {
                existing.Add(profilingData);
                // Keep only recent data to prevent memory bloat
                if (existing.Count > _options.MaxHistoricalEntries)
                {
                    existing.RemoveRange(0, existing.Count - _options.MaxHistoricalEntries);
                }
                return existing;
            });

        LogProfilingCompleted(sessionId, profilingData.ExecutionTime.TotalMilliseconds);

        return profilingData;
    }

    /// <summary>
    /// Gets comprehensive performance analysis for a kernel.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="acceleratorType">Optional accelerator type filter.</param>
    /// <returns>Performance analysis results.</returns>
    public PerformanceAnalysis GetPerformanceAnalysis(string kernelName, AcceleratorType? acceleratorType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var relevantData = new List<ProfilingData>();

        foreach (var (key, data) in _historicalData)
        {
            var filteredData = data.Where(d => d.KernelName.Equals(kernelName, StringComparison.OrdinalIgnoreCase));

            if (acceleratorType.HasValue)
            {
                filteredData = filteredData.Where(d => d.AcceleratorType == acceleratorType.Value);
            }

            relevantData.AddRange(filteredData);
        }

        if (!relevantData.Any())
        {
            LogNoDataAvailable(kernelName);
            return new PerformanceAnalysis
            {
                KernelName = kernelName,
                AcceleratorType = acceleratorType ?? AcceleratorType.CPU,
                DataPoints = 0,
                AnalysisTime = DateTime.UtcNow
            };
        }

        var rawAnalysis = AnalyzePerformanceData(relevantData);

        // Create new analysis with proper values due to init-only properties
        var analysis = new PerformanceAnalysis
        {
            KernelName = kernelName,
            AcceleratorType = acceleratorType ?? AcceleratorType.CPU,
            AnalysisTime = DateTime.UtcNow,
            AverageExecutionTimeMs = rawAnalysis.AverageExecutionTimeMs,
            MinExecutionTimeMs = rawAnalysis.MinExecutionTimeMs,
            MaxExecutionTimeMs = rawAnalysis.MaxExecutionTimeMs,
            ExecutionTimeStdDev = rawAnalysis.ExecutionTimeStdDev,
            AverageMemoryUsage = rawAnalysis.AverageMemoryUsage,
            PeakMemoryUsage = rawAnalysis.PeakMemoryUsage,
            AverageThroughput = rawAnalysis.AverageThroughput,
            DataPointCount = rawAnalysis.DataPointCount,
            AnalysisTimeRange = rawAnalysis.AnalysisTimeRange,
            Trends = rawAnalysis.Trends,
            Anomalies = rawAnalysis.Anomalies,
            DataPoints = rawAnalysis.DataPoints
        };

        LogAnalysisCompleted(kernelName, relevantData.Count);

        return analysis;
    }

    /// <summary>
    /// Compares performance between different accelerators for a kernel.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <returns>Comparison results.</returns>
    public AcceleratorComparisonResult CompareAcceleratorPerformance(string kernelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var allData = _historicalData.Values
            .SelectMany(list => list)
            .Where(d => d.KernelName.Equals(kernelName, StringComparison.OrdinalIgnoreCase))
            .GroupBy(d => d.AcceleratorType)
            .ToDictionary(g => g.Key, g => g.ToList());

        if (allData.Count < 2)
        {
            LogInsufficientDataForComparison(kernelName, allData.Count);
            return new AcceleratorComparisonResult
            {
                KernelName = kernelName,
                ComparisonTime = DateTime.UtcNow,
                HasSufficientData = false
            };
        }

        var comparisons = new Dictionary<AcceleratorType, AcceleratorPerformanceSummary>();

        foreach (var (acceleratorType, data) in allData)
        {
            var summary = new AcceleratorPerformanceSummary
            {
                AcceleratorType = acceleratorType,
                DataPoints = data.Count,
                AverageExecutionTime = data.Average(d => d.ExecutionTime.TotalMilliseconds),
                MedianExecutionTime = CalculateMedian(data.Select(d => d.ExecutionTime.TotalMilliseconds)),
                MinExecutionTime = data.Min(d => d.ExecutionTime.TotalMilliseconds),
                MaxExecutionTime = data.Max(d => d.ExecutionTime.TotalMilliseconds),
                StandardDeviation = CalculateStandardDeviation(data.Select(d => d.ExecutionTime.TotalMilliseconds)),
                AverageMemoryUsage = data.Average(d => d.MemoryUsage.AllocatedMemory),
                SuccessRate = data.Count(d => d.Success) / (double)data.Count * 100,
                ThroughputScore = CalculateThroughputScore(data)
            };

            comparisons[acceleratorType] = summary;
        }

        var result = new AcceleratorComparisonResult
        {
            KernelName = kernelName,
            ComparisonTime = DateTime.UtcNow,
            HasSufficientData = true,
            AcceleratorSummaries = comparisons,
            BestPerformingAccelerator = comparisons.OrderBy(kvp => kvp.Value.AverageExecutionTime).First().Key,
            MostReliableAccelerator = comparisons.OrderByDescending(kvp => kvp.Value.SuccessRate).First().Key,
            Recommendations = GenerateRecommendations(comparisons)
        };

        LogComparisonCompleted(kernelName, comparisons.Count);

        return result;
    }

    /// <summary>
    /// Gets real-time performance trends.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="timeRange">Time range for analysis.</param>
    /// <returns>Trend analysis results.</returns>
    public PerformanceTrend GetPerformanceTrend(string kernelName, TimeSpan timeRange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var cutoffTime = DateTime.UtcNow.Subtract(timeRange);
        var recentData = _historicalData.Values
            .SelectMany(list => list)
            .Where(d => d.KernelName.Equals(kernelName, StringComparison.OrdinalIgnoreCase) &&
                       d.EndTime >= cutoffTime)
            .OrderBy(d => d.EndTime)
            .ToList();

        if (!recentData.Any())
        {
            LogNoRecentData(kernelName, timeRange.ToString());
            return new PerformanceTrend
            {
                KernelName = kernelName,
                TimeRange = timeRange,
                DataPoints = 0,
                TrendDirection = TrendDirection.Unknown
            };
        }

        var trend = AnalyzeTrend(recentData);
        trend.KernelName = kernelName;
        trend.TimeRange = timeRange;
        trend.AnalysisTime = DateTime.UtcNow;

        LogTrendAnalysisCompleted(kernelName, recentData.Count, trend.TrendDirection.ToString());

        return trend;
    }

    /// <summary>
    /// Detects performance anomalies.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <returns>Collection of detected anomalies.</returns>
    public IEnumerable<PerformanceAnomaly> DetectAnomalies(string kernelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var allData = _historicalData.Values
            .SelectMany(list => list)
            .Where(d => d.KernelName.Equals(kernelName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.EndTime)
            .ToList();

        if (allData.Count < 10) // Need sufficient data for anomaly detection
        {
            LogInsufficientDataForAnomalies(kernelName, allData.Count);
            return Enumerable.Empty<PerformanceAnomaly>();
        }

        var anomalies = new List<PerformanceAnomaly>();

        // Detect execution time anomalies
        var executionTimes = allData.Select(d => d.ExecutionTime.TotalMilliseconds).ToList();
        var meanTime = executionTimes.Average();
        var stdDev = CalculateStandardDeviation(executionTimes);
        var threshold = meanTime + (2 * stdDev); // 2 standard deviations

        foreach (var data in allData.Where(d => d.ExecutionTime.TotalMilliseconds > threshold))
        {
            anomalies.Add(new PerformanceAnomaly
            {
                Type = AnomalyType.ExecutionTime,
                SessionId = data.SessionId,
                DetectedAt = data.EndTime,
                Value = data.ExecutionTime.TotalMilliseconds,
                ExpectedValue = meanTime,
                Severity = data.ExecutionTime.TotalMilliseconds > meanTime + (3 * stdDev) ? AnomalySeverity.High : AnomalySeverity.Medium,
                Description = $"Execution time ({data.ExecutionTime.TotalMilliseconds:F2}ms) significantly higher than average ({meanTime:F2}ms)"
            });
        }

        // Detect memory anomalies
        var memoryUsages = allData.Select(d => (double)d.MemoryUsage.AllocatedMemory).ToList();
        var meanMemory = memoryUsages.Average();
        var memoryStdDev = CalculateStandardDeviation(memoryUsages);
        var memoryThreshold = meanMemory + (2 * memoryStdDev);

        foreach (var data in allData.Where(d => d.MemoryUsage.AllocatedMemory > memoryThreshold))
        {
            anomalies.Add(new PerformanceAnomaly
            {
                Type = AnomalyType.MemoryUsage,
                SessionId = data.SessionId,
                DetectedAt = data.EndTime,
                Value = data.MemoryUsage.AllocatedMemory,
                ExpectedValue = meanMemory,
                Severity = data.MemoryUsage.AllocatedMemory > meanMemory + (3 * memoryStdDev) ? AnomalySeverity.High : AnomalySeverity.Medium,
                Description = $"Memory usage ({data.MemoryUsage.AllocatedMemory:N0} bytes) significantly higher than average ({meanMemory:F0} bytes)"
            });
        }

        LogAnomaliesDetected(kernelName, anomalies.Count);

        return anomalies.OrderByDescending(a => a.DetectedAt);
    }

    /// <summary>
    /// Clears historical profiling data.
    /// </summary>
    /// <param name="kernelName">Optional kernel name filter.</param>
    public void ClearProfilingData(string? kernelName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(kernelName))
        {
            _historicalData.Clear();
            LogAllDataCleared();
        }
        else
        {
            var keysToRemove = _historicalData.Keys
                .Where(k => k.StartsWith(kernelName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _historicalData.TryRemove(key, out _);
            }

            LogKernelDataCleared(kernelName);
        }
    }

    /// <summary>
    /// Monitors performance in background.
    /// </summary>
    private void MonitorPerformance(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            // Monitor active sessions for long-running operations
            var longRunningsessions = _activeSessions.Values
                .Where(s => DateTime.UtcNow - s.StartTime > _options.LongRunningThreshold)
                .ToList();

            foreach (var session in longRunningsessions)
            {
                LogLongRunningSession(session.SessionId, session.KernelName,
                    (DateTime.UtcNow - session.StartTime).TotalMinutes);
            }

            // Check for memory pressure
            var currentMemory = GC.GetTotalMemory(false);
            if (currentMemory > _options.MemoryPressureThreshold)
            {
                LogMemoryPressure(currentMemory);
            }
        }
        catch (Exception ex)
        {
            LogMonitoringError(ex.Message);
        }
    }

    #region Helper Methods

    private static TimeSpan GetCurrentCpuTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime;
    }

    private static double CalculateCpuUtilization(TimeSpan startCpu, TimeSpan endCpu, DateTime startTime, DateTime endTime)
    {
        var cpuTime = endCpu - startCpu;
        var wallTime = endTime - startTime;

        if (wallTime.TotalMilliseconds > 0)
        {
            return (cpuTime.TotalMilliseconds / wallTime.TotalMilliseconds) * 100;
        }
        return 0.0;
    }

    private static Dictionary<int, int> GetGCCollectionCounts()
    {
        return new Dictionary<int, int>
        {
            [0] = GC.CollectionCount(0),
            [1] = GC.CollectionCount(1),
            [2] = GC.CollectionCount(2)
        };
    }

    private static PerformanceMetrics CalculatePerformanceMetrics(ProfilingSession session, DateTime endTime, long endMemory, TimeSpan endCpuTime)
    {
        var executionTime = endTime - session.StartTime;
        var memoryAllocated = Math.Max(0, endMemory - session.StartMemory);

        return new PerformanceMetrics
        {
            ExecutionTimeMs = executionTime.TotalMilliseconds,
            MemoryAllocatedBytes = memoryAllocated,
            ThroughputOpsPerSec = executionTime.TotalSeconds > 0 ? 1.0 / executionTime.TotalSeconds : 0.0,
            EfficiencyScore = CalculateEfficiencyScore(executionTime, memoryAllocated)
        };
    }

    private static double CalculateEfficiencyScore(TimeSpan executionTime, long memoryAllocated)
    {
        // Simplified efficiency scoring
        var timeScore = Math.Max(0, 100 - executionTime.TotalMilliseconds / 10);
        var memoryScore = Math.Max(0, 100 - memoryAllocated / (1024 * 1024)); // MB
        return (timeScore + memoryScore) / 2;
    }

    private static Dictionary<string, object> ConvertToPerformanceMetricsDictionary(PerformanceMetrics metrics)
    {
        return new Dictionary<string, object>
        {
            ["ExecutionTimeMs"] = metrics.ExecutionTimeMs,
            ["MemoryAllocatedBytes"] = metrics.MemoryAllocatedBytes,
            ["ThroughputOpsPerSec"] = metrics.ThroughputOpsPerSec,
            ["EfficiencyScore"] = metrics.EfficiencyScore
        };
    }

    private static PerformanceAnalysis AnalyzePerformanceData(List<ProfilingData> data)
    {
        var executionTimes = data.Select(d => d.ExecutionTime.TotalMilliseconds).ToList();
        var memoryUsages = data.Select(d => (double)d.MemoryUsage.AllocatedMemory).ToList();

        return new PerformanceAnalysis
        {
            DataPoints = data.Count,
            AverageExecutionTime = executionTimes.Average(),
            MedianExecutionTime = CalculateMedian(executionTimes),
            MinExecutionTime = executionTimes.Min(),
            MaxExecutionTime = executionTimes.Max(),
            ExecutionTimeStdDev = CalculateStandardDeviation(executionTimes),
            AverageMemoryUsage = memoryUsages.Average(),
            PeakMemoryUsage = memoryUsages.Max(),
            SuccessRate = data.Count(d => d.Success) / (double)data.Count * 100,
            TotalExecutions = data.Count
        };
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

    private static double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valueList = values.ToList();
        if (!valueList.Any())
        {
            return 0;
        }


        var mean = valueList.Average();
        var sumOfSquaredDifferences = valueList.Sum(val => Math.Pow(val - mean, 2));
        return Math.Sqrt(sumOfSquaredDifferences / valueList.Count);
    }

    private static double CalculateThroughputScore(List<ProfilingData> data)
    {
        if (!data.Any())
        {
            return 0;
        }


        var totalOps = data.Count;
        var totalTime = data.Sum(d => d.ExecutionTime.TotalSeconds);

        return totalTime > 0 ? totalOps / totalTime : 0;
    }

    private static List<string> GenerateRecommendations(Dictionary<AcceleratorType, AcceleratorPerformanceSummary> comparisons)
    {
        var recommendations = new List<string>();

        if (!comparisons.Any())
        {
            return recommendations;
        }


        var fastest = comparisons.OrderBy(kvp => kvp.Value.AverageExecutionTime).First();
        var mostReliable = comparisons.OrderByDescending(kvp => kvp.Value.SuccessRate).First();
        var leastMemoryUsage = comparisons.OrderBy(kvp => kvp.Value.AverageMemoryUsage).First();

        recommendations.Add($"Fastest execution: {fastest.Key} ({fastest.Value.AverageExecutionTime:F2}ms average)");
        recommendations.Add($"Most reliable: {mostReliable.Key} ({mostReliable.Value.SuccessRate:F1}% success rate)");
        recommendations.Add($"Lowest memory usage: {leastMemoryUsage.Key} ({leastMemoryUsage.Value.AverageMemoryUsage:F0} bytes average)");

        if (fastest.Key == mostReliable.Key)
        {
            recommendations.Add($"Recommendation: Use {fastest.Key} for optimal performance and reliability");
        }
        else
        {
            recommendations.Add($"Consider {fastest.Key} for performance or {mostReliable.Key} for reliability");
        }

        return recommendations;
    }

    private static PerformanceTrend AnalyzeTrend(List<ProfilingData> data)
    {
        if (data.Count < 3)
        {
            return new PerformanceTrend
            {
                DataPoints = data.Count,
                TrendDirection = TrendDirection.Unknown
            };
        }

        // Simple linear regression to determine trend
        var times = data.Select((d, i) => (double)i).ToList();
        var executionTimes = data.Select(d => d.ExecutionTime.TotalMilliseconds).ToList();

        var slope = CalculateSlope(times, executionTimes);

        var trendDirection = slope switch
        {
            > 1.0 => TrendDirection.Increasing,
            < -1.0 => TrendDirection.Decreasing,
            _ => TrendDirection.Stable
        };

        return new PerformanceTrend
        {
            DataPoints = data.Count,
            TrendDirection = trendDirection,
            Slope = slope,
            FirstDataPoint = data.First().ExecutionTime,
            LastDataPoint = data.Last().ExecutionTime,
            AverageChange = (data.Last().ExecutionTime.TotalMilliseconds - data.First().ExecutionTime.TotalMilliseconds) / data.Count
        };
    }

    private static double CalculateSlope(List<double> x, List<double> y)
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

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _performanceMonitor?.Dispose();
            _activeSessions.Clear();
            _historicalData.Clear();
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Profiling started for session {SessionId}, kernel {KernelName} on {AcceleratorType}")]
    private partial void LogProfilingStarted(string sessionId, string kernelName, string acceleratorType);

    [LoggerMessage(Level = MsLogLevel.Warning, Message = "Profiling session {SessionId} already exists")]
    private partial void LogSessionAlreadyExists(string sessionId);

    [LoggerMessage(Level = MsLogLevel.Warning, Message = "Profiling session {SessionId} not found")]
    private partial void LogSessionNotFound(string sessionId);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Profiling completed for session {SessionId} in {ElapsedMs}ms")]
    private partial void LogProfilingCompleted(string sessionId, double elapsedMs);

    [LoggerMessage(Level = MsLogLevel.Warning, Message = "No profiling data available for kernel {KernelName}")]
    private partial void LogNoDataAvailable(string kernelName);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Performance analysis completed for kernel {KernelName} with {DataPoints} data points")]
    private partial void LogAnalysisCompleted(string kernelName, int dataPoints);

    [LoggerMessage(Level = MsLogLevel.Warning, Message = "Insufficient data for accelerator comparison of kernel {KernelName}: {AcceleratorCount} accelerators")]
    private partial void LogInsufficientDataForComparison(string kernelName, int acceleratorCount);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Accelerator comparison completed for kernel {KernelName} with {AcceleratorCount} accelerators")]
    private partial void LogComparisonCompleted(string kernelName, int acceleratorCount);

    [LoggerMessage(Level = MsLogLevel.Warning, Message = "No recent data available for kernel {KernelName} in time range {TimeRange}")]
    private partial void LogNoRecentData(string kernelName, string timeRange);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Trend analysis completed for kernel {KernelName}: {DataPoints} points, trend: {TrendDirection}")]
    private partial void LogTrendAnalysisCompleted(string kernelName, int dataPoints, string trendDirection);

    [LoggerMessage(Level = MsLogLevel.Warning, Message = "Insufficient data for anomaly detection of kernel {KernelName}: {DataPoints} points")]
    private partial void LogInsufficientDataForAnomalies(string kernelName, int dataPoints);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Anomaly detection completed for kernel {KernelName}: {AnomalyCount} anomalies found")]
    private partial void LogAnomaliesDetected(string kernelName, int anomalyCount);

    [LoggerMessage(Level = MsLogLevel.Information, Message = "All profiling data cleared")]
    private partial void LogAllDataCleared();

    [LoggerMessage(Level = MsLogLevel.Information, Message = "Profiling data cleared for kernel {KernelName}")]
    private partial void LogKernelDataCleared(string kernelName);

    [LoggerMessage(Level = MsLogLevel.Warning, Message = "Long-running session detected: {SessionId} for kernel {KernelName}, running for {Minutes:F1} minutes")]
    private partial void LogLongRunningSession(string sessionId, string kernelName, double minutes);

    [LoggerMessage(Level = MsLogLevel.Warning, Message = "Memory pressure detected: {CurrentMemory:N0} bytes")]
    private partial void LogMemoryPressure(long currentMemory);

    [LoggerMessage(Level = MsLogLevel.Error, Message = "Performance monitoring error: {Error}")]
    private partial void LogMonitoringError(string error);

    #endregion
}