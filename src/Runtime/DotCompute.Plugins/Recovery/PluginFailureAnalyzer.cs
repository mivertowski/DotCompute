// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Recovery;

/// <summary>
/// Analyzes plugin failures to identify patterns, root causes, and provide recovery recommendations
/// </summary>
public sealed class PluginFailureAnalyzer : IDisposable
{
    private readonly ILogger<PluginFailureAnalyzer> _logger;
    private readonly PluginRecoveryConfiguration _config;
    private readonly ConcurrentDictionary<string, List<FailureInstance>> _failureHistory;
    private readonly ConcurrentDictionary<string, FailurePattern> _identifiedPatterns;
    private readonly Timer _analysisTimer;
    private readonly SemaphoreSlim _analysisLock;
    private bool _disposed;

    public PluginFailureAnalyzer(ILogger<PluginFailureAnalyzer> logger, PluginRecoveryConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? PluginRecoveryConfiguration.Default;
        _failureHistory = new ConcurrentDictionary<string, List<FailureInstance>>();
        _identifiedPatterns = new ConcurrentDictionary<string, FailurePattern>();
        _analysisLock = new SemaphoreSlim(1, 1);

        // Start periodic pattern analysis
        _analysisTimer = new Timer(PerformPatternAnalysis, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));

        _logger.LogInformation("Plugin Failure Analyzer initialized with pattern analysis enabled");
    }

    /// <summary>
    /// Records a failure instance for analysis
    /// </summary>
    public void RecordFailure(string pluginId, Exception error, PluginRecoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(pluginId);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(context);

        var failureInstance = new FailureInstance
        {
            PluginId = pluginId,
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionType = error.GetType().Name,
            ErrorMessage = error.Message,
            StackTrace = error.StackTrace,
            AcceleratorType = context.AcceleratorType?.ToString(),
            OperationType = context.OperationType,
            MemoryUsage = GC.GetTotalMemory(false),
            ThreadId = Environment.CurrentManagedThreadId,
            ProcessId = Environment.ProcessId
        };

        var failures = _failureHistory.GetOrAdd(pluginId, _ => []);
        lock (failures)
        {
            failures.Add(failureInstance);

            // Keep only recent failures to prevent memory bloat
            if (failures.Count > _config.MaxFailureHistorySize)
            {
                failures.RemoveRange(0, failures.Count - _config.MaxFailureHistorySize);
            }
        }

        _logger.LogDebug("Recorded failure for plugin {PluginId}: {ErrorType} - {Message}",
            pluginId, error.GetType().Name, error.Message);

        // Trigger immediate analysis if this is a critical failure
        if (IsCriticalFailure(error))
        {
            _ = Task.Run(async () => await AnalyzePluginFailuresAsync(pluginId));
        }
    }

    /// <summary>
    /// Analyzes failures for a specific plugin and returns recommendations
    /// </summary>
    public async Task<FailureAnalysisResult> AnalyzePluginFailuresAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginId);

        await _analysisLock.WaitAsync(cancellationToken);
        try
        {
            if (!_failureHistory.TryGetValue(pluginId, out var failures) || failures.Count == 0)
            {
                return new FailureAnalysisResult
                {
                    PluginId = pluginId,
                    AnalysisTimestamp = DateTimeOffset.UtcNow,
                    TotalFailures = 0,
                    Severity = FailureSeverity.None,
                    Recommendations = ["No failures recorded for analysis"],
                    Confidence = 1.0
                };
            }

            List<FailureInstance> failuresCopy;
            lock (failures)
            {
                failuresCopy = new List<FailureInstance>(failures);
            }

            return await PerformDetailedAnalysisAsync(pluginId, failuresCopy, cancellationToken);
        }
        finally
        {
            _ = _analysisLock.Release();
        }
    }

    /// <summary>
    /// Gets identified failure patterns for a plugin
    /// </summary>
    public FailurePattern? GetFailurePattern(string pluginId)
    {
        return _identifiedPatterns.TryGetValue(pluginId, out var pattern) ? pattern : null;
    }

    /// <summary>
    /// Gets failure statistics for a plugin over a specified time window
    /// </summary>
    public FailureStatistics GetFailureStatistics(string pluginId, TimeSpan timeWindow)
    {
        ArgumentNullException.ThrowIfNull(pluginId);

        if (!_failureHistory.TryGetValue(pluginId, out var failures))
        {
            return new FailureStatistics { PluginId = pluginId };
        }

        var cutoff = DateTimeOffset.UtcNow - timeWindow;
        List<FailureInstance> recentFailures;

        lock (failures)
        {
            recentFailures = failures.Where(f => f.Timestamp >= cutoff).ToList();
        }

        var statistics = new FailureStatistics
        {
            PluginId = pluginId,
            TimeWindow = timeWindow,
            TotalFailures = recentFailures.Count,
            FailureRate = recentFailures.Count / timeWindow.TotalHours,
            MostCommonExceptionType = recentFailures.GroupBy(f => f.ExceptionType)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? "None",
            AverageTimeBetweenFailures = CalculateAverageTimeBetweenFailures(recentFailures),
            FailuresPerHour = recentFailures.GroupBy(f => f.Timestamp.Hour)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return statistics;
    }

    /// <summary>
    /// Predicts likelihood of future failures based on historical patterns
    /// </summary>
    public FailurePrediction PredictFailureRisk(string pluginId, TimeSpan lookAhead)
    {
        ArgumentNullException.ThrowIfNull(pluginId);

        var pattern = GetFailurePattern(pluginId);
        var statistics = GetFailureStatistics(pluginId, TimeSpan.FromDays(7));

        var prediction = new FailurePrediction
        {
            PluginId = pluginId,
            PredictionTimestamp = DateTimeOffset.UtcNow,
            LookAheadPeriod = lookAhead,
            RiskLevel = CalculateRiskLevel(statistics, pattern),
            ConfidenceScore = CalculateConfidenceScore(statistics, pattern),
            EstimatedFailuresInPeriod = EstimateFailureCount(statistics, lookAhead)
        };

        if (pattern != null)
        {
            prediction.NextLikelyFailureTime = EstimateNextFailureTime(pattern, statistics);
            prediction.PrimaryRiskFactors = IdentifyRiskFactors(pattern, statistics);
        }

        return prediction;
    }

    private async Task<FailureAnalysisResult> PerformDetailedAnalysisAsync(
        string pluginId,
        List<FailureInstance> failures,
        CancellationToken cancellationToken)
    {
        var result = new FailureAnalysisResult
        {
            PluginId = pluginId,
            AnalysisTimestamp = DateTimeOffset.UtcNow,
            TotalFailures = failures.Count,
            Severity = FailureSeverity.None,
            Confidence = 0.0
        };

        // Analyze failure patterns
        var exceptionGroups = failures.GroupBy(f => f.ExceptionType).ToList();
        var mostCommonException = exceptionGroups.OrderByDescending(g => g.Count()).First();

        result.MostCommonExceptionType = mostCommonException.Key;
        result.ExceptionTypeDistribution = exceptionGroups.ToDictionary(g => g.Key, g => g.Count());

        // Analyze temporal patterns
        result.FailureFrequency = AnalyzeTemporalPatterns(failures);

        // Analyze memory patterns
        result.MemoryPatterns = AnalyzeMemoryPatterns(failures);

        // Determine severity
        result.Severity = DetermineSeverity(failures);

        // Generate recommendations
        result.Recommendations = await GenerateRecommendationsAsync(failures, cancellationToken);

        // Calculate confidence
        result.Confidence = CalculateAnalysisConfidence(failures);

        // Update pattern cache
        UpdatePatternCache(pluginId, result);

        return result;
    }

    private static bool IsCriticalFailure(Exception error) => error switch
    {
        OutOfMemoryException => true,
        StackOverflowException => true,
        AccessViolationException => true,
        AppDomainUnloadedException => true,
        _ => false
    };

    private static FailureSeverity DetermineSeverity(List<FailureInstance> failures)
    {
        if (failures.Count == 0)
        {
            return FailureSeverity.None;
        }


        var recentFailures = failures.Where(f => f.Timestamp >= DateTimeOffset.UtcNow.AddHours(-1)).Count();
        var criticalFailures = failures.Count(f => IsCriticalFailure(new Exception(f.ExceptionType)));

        return (recentFailures, criticalFailures) switch
        {
            ( > 10, _) => FailureSeverity.Critical,
            ( > 5, _) => FailureSeverity.High,
            (_, > 0) => FailureSeverity.High,
            ( > 2, _) => FailureSeverity.Medium,
            ( > 0, _) => FailureSeverity.Low,
            _ => FailureSeverity.None
        };
    }

    private static Dictionary<string, double> AnalyzeTemporalPatterns(List<FailureInstance> failures)
    {
        if (failures.Count < 2)
        {
            return new Dictionary<string, double>();
        }


        var intervals = failures.OrderBy(f => f.Timestamp)
            .Zip(failures.Skip(1).OrderBy(f => f.Timestamp))
            .Select(pair => (pair.Second.Timestamp - pair.First.Timestamp).TotalMinutes)
            .ToList();

        return new Dictionary<string, double>
        {
            ["AverageIntervalMinutes"] = intervals.Count > 0 ? intervals.Average() : 0,
            ["MinIntervalMinutes"] = intervals.Count > 0 ? intervals.Min() : 0,
            ["MaxIntervalMinutes"] = intervals.Count > 0 ? intervals.Max() : 0,
            ["StandardDeviation"] = intervals.Count > 1 ? CalculateStandardDeviation(intervals) : 0
        };
    }

    private static Dictionary<string, long> AnalyzeMemoryPatterns(List<FailureInstance> failures)
    {
        if (failures.Count == 0)
        {
            return new Dictionary<string, long>();
        }


        var memoryUsages = failures.Select(f => f.MemoryUsage).ToList();

        return new Dictionary<string, long>
        {
            ["AverageMemoryUsage"] = (long)memoryUsages.Average(),
            ["MinMemoryUsage"] = memoryUsages.Min(),
            ["MaxMemoryUsage"] = memoryUsages.Max(),
            ["MemoryGrowthTrend"] = CalculateMemoryGrowthTrend(failures)
        };
    }

    private static Task<List<string>> GenerateRecommendationsAsync(
        List<FailureInstance> failures,
        CancellationToken cancellationToken)
    {
        var recommendations = new List<string>();

        // Memory-based recommendations
        if (failures.Any(f => f.ExceptionType.Contains("OutOfMemory")))
        {
            recommendations.Add("Consider increasing memory limits or implementing memory pooling");
            recommendations.Add("Review memory usage patterns and optimize allocation strategies");
        }

        // Threading-based recommendations
        var threadingIssues = failures.Where(f =>
            f.ExceptionType.Contains("Thread") ||
            f.ExceptionType.Contains("Deadlock") ||
            f.ExceptionType.Contains("Race")).ToList();

        if (threadingIssues.Count > 0)
        {
            recommendations.Add("Review thread synchronization and consider using async/await patterns");
            recommendations.Add("Implement proper thread-safe patterns for shared resources");
        }

        // High frequency recommendations
        if (failures.Count > 10)
        {
            recommendations.Add("Enable plugin isolation to prevent cascade failures");
            recommendations.Add("Consider implementing circuit breaker pattern");
            recommendations.Add("Review plugin initialization and resource allocation");
        }

        // Pattern-based recommendations
        var commonException = failures.GroupBy(f => f.ExceptionType)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (commonException != null && commonException.Count() > failures.Count * 0.6)
        {
            recommendations.Add($"Focus on resolving {commonException.Key} as it represents majority of failures");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Monitor plugin behavior and consider implementing health checks");
        }

        return Task.FromResult(recommendations);
    }

    private static double CalculateAnalysisConfidence(List<FailureInstance> failures)
    {
        if (failures.Count == 0)
        {
            return 0.0;
        }


        if (failures.Count == 1)
        {
            return 0.3;
        }


        if (failures.Count < 5)
        {
            return 0.6;
        }


        if (failures.Count < 10)
        {
            return 0.8;
        }


        return 0.95;
    }

    private void UpdatePatternCache(string pluginId, FailureAnalysisResult analysis)
    {
        var pattern = new FailurePattern
        {
            PluginId = pluginId,
            LastUpdated = DateTimeOffset.UtcNow,
            DominantExceptionType = analysis.MostCommonExceptionType,
            AverageFailureInterval = analysis.FailureFrequency.GetValueOrDefault("AverageIntervalMinutes", 0),
            Severity = analysis.Severity,
            TrendDirection = DetermineTrendDirection(analysis),
            Confidence = analysis.Confidence
        };

        _ = _identifiedPatterns.AddOrUpdate(pluginId, pattern, (_, _) => pattern);
    }

    private static FailureTrendDirection DetermineTrendDirection(FailureAnalysisResult analysis)
    {
        // Simple heuristic based on recent vs older failures
        // In a real implementation, this would use time series analysis
        return analysis.Severity switch
        {
            FailureSeverity.Critical => FailureTrendDirection.Increasing,
            FailureSeverity.High => FailureTrendDirection.Increasing,
            FailureSeverity.Medium => FailureTrendDirection.Stable,
            _ => FailureTrendDirection.Decreasing
        };
    }

    private void PerformPatternAnalysis(object? state)
    {
        if (_disposed)
        {
            return;
        }


        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var pluginId in _failureHistory.Keys)
                {
                    _ = await AnalyzePluginFailuresAsync(pluginId);
                    await Task.Delay(100); // Prevent overwhelming the system
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic pattern analysis");
            }
        });
    }

    // Helper methods
    private static double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }


        var average = values.Average();
        var sumOfSquaredDifferences = values.Sum(v => Math.Pow(v - average, 2));
        return Math.Sqrt(sumOfSquaredDifferences / values.Count);
    }

    private static TimeSpan CalculateAverageTimeBetweenFailures(List<FailureInstance> failures)
    {
        if (failures.Count < 2)
        {
            return TimeSpan.Zero;
        }


        var sortedFailures = failures.OrderBy(f => f.Timestamp).ToList();
        var intervals = new List<TimeSpan>();

        for (var i = 1; i < sortedFailures.Count; i++)
        {
            intervals.Add(sortedFailures[i].Timestamp - sortedFailures[i - 1].Timestamp);
        }

        var averageTicks = (long)intervals.Average(i => i.Ticks);
        return new TimeSpan(averageTicks);
    }

    private static long CalculateMemoryGrowthTrend(List<FailureInstance> failures)
    {
        if (failures.Count < 2)
        {
            return 0;
        }


        var sortedFailures = failures.OrderBy(f => f.Timestamp).ToList();
        var firstHalf = sortedFailures.Take(sortedFailures.Count / 2).Average(f => f.MemoryUsage);
        var secondHalf = sortedFailures.Skip(sortedFailures.Count / 2).Average(f => f.MemoryUsage);

        return (long)(secondHalf - firstHalf);
    }

    private static FailureRiskLevel CalculateRiskLevel(FailureStatistics statistics, FailurePattern? pattern)
    {
        var riskScore = 0;

        // Frequency-based risk
        if (statistics.FailureRate > 5)
        {
            riskScore += 3; // > 5 failures per hour
        }
        else if (statistics.FailureRate > 2)
        {
            riskScore += 2;
        }
        else if (statistics.FailureRate > 0.5)
        {
            riskScore += 1;
        }

        // Pattern-based risk
        if (pattern?.Severity == FailureSeverity.Critical)
        {
            riskScore += 3;
        }
        else if (pattern?.Severity == FailureSeverity.High)
        {
            riskScore += 2;
        }
        else if (pattern?.Severity == FailureSeverity.Medium)
        {
            riskScore += 1;
        }


        return riskScore switch
        {
            >= 5 => FailureRiskLevel.Critical,
            >= 3 => FailureRiskLevel.High,
            >= 2 => FailureRiskLevel.Medium,
            >= 1 => FailureRiskLevel.Low,
            _ => FailureRiskLevel.None
        };
    }

    private static double CalculateConfidenceScore(FailureStatistics statistics, FailurePattern? pattern)
    {
        var confidence = 0.5; // Base confidence

        // More data = higher confidence
        if (statistics.TotalFailures > 20)
        {
            confidence += 0.3;
        }
        else if (statistics.TotalFailures > 10)
        {
            confidence += 0.2;
        }
        else if (statistics.TotalFailures > 5)
        {
            confidence += 0.1;
        }

        // Pattern confidence
        if (pattern?.Confidence > 0.8)
        {
            confidence += 0.2;
        }
        else if (pattern?.Confidence > 0.6)
        {
            confidence += 0.1;
        }


        return Math.Min(1.0, confidence);
    }

    private static int EstimateFailureCount(FailureStatistics statistics, TimeSpan lookAhead)
    {
        if (statistics.FailureRate <= 0)
        {
            return 0;
        }


        var estimatedFailures = statistics.FailureRate * lookAhead.TotalHours;
        return Math.Max(0, (int)Math.Round(estimatedFailures));
    }

    private static DateTimeOffset? EstimateNextFailureTime(FailurePattern pattern, FailureStatistics statistics)
    {
        if (pattern.AverageFailureInterval <= 0)
        {
            return null;
        }


        var averageInterval = TimeSpan.FromMinutes(pattern.AverageFailureInterval);
        return DateTimeOffset.UtcNow.Add(averageInterval);
    }

    private static List<string> IdentifyRiskFactors(FailurePattern pattern, FailureStatistics statistics)
    {
        var factors = new List<string>();

        if (statistics.FailureRate > 2)
        {
            factors.Add("High failure frequency");
        }

        if (pattern.Severity >= FailureSeverity.High)
        {
            factors.Add("High severity failures");
        }

        if (pattern.TrendDirection == FailureTrendDirection.Increasing)
        {
            factors.Add("Increasing failure trend");
        }

        if (statistics.MostCommonExceptionType.Contains("OutOfMemory"))
        {
            factors.Add("Memory management issues");
        }


        return factors;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _analysisTimer?.Dispose();
            _analysisLock?.Dispose();
            _disposed = true;

            _logger.LogInformation("Plugin Failure Analyzer disposed");
        }
    }
}

// Supporting data structures
public sealed record FailureInstance
{
    public required string PluginId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string ExceptionType { get; init; }
    public required string ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
    public string? AcceleratorType { get; init; }
    public string? OperationType { get; init; }
    public long MemoryUsage { get; init; }
    public int ThreadId { get; init; }
    public int ProcessId { get; init; }
}

public sealed class FailureAnalysisResult
{
    public required string PluginId { get; init; }
    public required DateTimeOffset AnalysisTimestamp { get; init; }
    public required int TotalFailures { get; init; }
    public required FailureSeverity Severity { get; set; }
    public string? MostCommonExceptionType { get; set; }
    public Dictionary<string, int> ExceptionTypeDistribution { get; set; } = [];
    public Dictionary<string, double> FailureFrequency { get; set; } = [];
    public Dictionary<string, long> MemoryPatterns { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public required double Confidence { get; set; }
}

public sealed class FailurePattern
{
    public required string PluginId { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }
    public string? DominantExceptionType { get; init; }
    public double AverageFailureInterval { get; init; }
    public FailureSeverity Severity { get; init; }
    public FailureTrendDirection TrendDirection { get; init; }
    public double Confidence { get; init; }
}

public sealed class FailureStatistics
{
    public required string PluginId { get; init; }
    public TimeSpan TimeWindow { get; init; }
    public int TotalFailures { get; init; }
    public double FailureRate { get; init; }
    public string MostCommonExceptionType { get; init; } = "None";
    public TimeSpan AverageTimeBetweenFailures { get; init; }
    public Dictionary<int, int> FailuresPerHour { get; init; } = [];
}

public sealed class FailurePrediction
{
    public required string PluginId { get; init; }
    public required DateTimeOffset PredictionTimestamp { get; init; }
    public required TimeSpan LookAheadPeriod { get; init; }
    public required FailureRiskLevel RiskLevel { get; set; }
    public required double ConfidenceScore { get; set; }
    public required int EstimatedFailuresInPeriod { get; set; }
    public DateTimeOffset? NextLikelyFailureTime { get; set; }
    public List<string> PrimaryRiskFactors { get; set; } = [];
}

public enum FailureSeverity
{
    None,
    Low,
    Medium,
    High,
    Critical
}

public enum FailureTrendDirection
{
    Decreasing,
    Stable,
    Increasing
}

public enum FailureRiskLevel
{
    None,
    Low,
    Medium,
    High,
    Critical
}