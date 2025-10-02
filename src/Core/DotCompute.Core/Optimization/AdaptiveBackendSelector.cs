// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Options;
using DotCompute.Abstractions;
using DotCompute.Core.Telemetry;
using DotCompute.Core.Telemetry.System;
using DotCompute.Core.Pipelines;
using DotCompute.Core.Optimization.Configuration;
using DotCompute.Core.Optimization.Enums;
using DotCompute.Core.Optimization.Models;
using DotCompute.Core.Optimization.Performance;
using DotCompute.Core.Optimization.Selection;

namespace DotCompute.Core.Optimization;

/// <summary>
/// Intelligent backend selection system that adapts based on workload characteristics,
/// historical performance data, and real-time system metrics.
/// </summary>
public class AdaptiveBackendSelector : IDisposable
{
    private readonly ILogger<AdaptiveBackendSelector> _logger;
    private readonly PerformanceProfiler _performanceProfiler;
    private readonly AdaptiveSelectionOptions _options;

    // Historical performance data for different workload patterns

    private readonly ConcurrentDictionary<WorkloadSignature, PerformanceHistory> _performanceHistory;

    // Real-time backend performance monitoring

    private readonly ConcurrentDictionary<string, BackendPerformanceState> _backendStates;

    // Workload analysis cache

    private readonly ConcurrentDictionary<string, WorkloadAnalysis> _workloadAnalysisCache;


    private readonly Timer _performanceUpdateTimer;
    private bool _disposed;

    public AdaptiveBackendSelector(
        ILogger<AdaptiveBackendSelector> logger,
        PerformanceProfiler performanceProfiler,
        IOptions<AdaptiveSelectionOptions>? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _performanceProfiler = performanceProfiler ?? throw new ArgumentNullException(nameof(performanceProfiler));
        _options = options?.Value ?? new AdaptiveSelectionOptions();

        _performanceHistory = new ConcurrentDictionary<WorkloadSignature, PerformanceHistory>();
        _backendStates = new ConcurrentDictionary<string, BackendPerformanceState>();
        _workloadAnalysisCache = new ConcurrentDictionary<string, WorkloadAnalysis>();

        // Start performance monitoring timer
        _performanceUpdateTimer = new Timer(UpdateBackendPerformanceStates, null,
            TimeSpan.Zero, TimeSpan.FromSeconds(_options.PerformanceUpdateIntervalSeconds));

        _logger.LogInfoMessage($"Adaptive backend selector initialized with learning enabled: {_options.EnableLearning}");
    }

    /// <summary>
    /// Selects the optimal backend for a given workload based on comprehensive analysis.
    /// </summary>
    /// <param name="kernelName">Name of the kernel to execute</param>
    /// <param name="workloadCharacteristics">Characteristics of the workload</param>
    /// <param name="availableBackends">Available backends to choose from</param>
    /// <param name="constraints">Optional constraints for backend selection</param>
    /// <returns>Optimal backend selection with confidence score</returns>
    public async Task<BackendSelection> SelectOptimalBackendAsync(
        string kernelName,
        WorkloadCharacteristics workloadCharacteristics,
        IEnumerable<IAccelerator> availableBackends,
        SelectionConstraints? constraints = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(workloadCharacteristics);
        ArgumentNullException.ThrowIfNull(availableBackends);

        var backends = availableBackends.ToList();
        if (backends.Count == 0)
        {
            return new BackendSelection
            {
                SelectedBackend = null,
                BackendId = "None",
                ConfidenceScore = 0f,
                Reason = "No backends available",
                SelectionStrategy = SelectionStrategy.Fallback
            };
        }

        if (backends.Count == 1)
        {
            return new BackendSelection
            {
                SelectedBackend = backends[0],
                BackendId = GetBackendId(backends[0]),
                ConfidenceScore = 0.8f,
                Reason = "Only one backend available",
                SelectionStrategy = SelectionStrategy.OnlyOption
            };
        }

        var workloadSignature = CreateWorkloadSignature(kernelName, workloadCharacteristics);
        var workloadAnalysis = await AnalyzeWorkloadAsync(workloadSignature, workloadCharacteristics);

        // Apply different selection strategies based on configuration and available data

        var selection = await ApplyOptimalSelectionStrategyAsync(
            backends, workloadAnalysis, workloadSignature, constraints);

        _logger.LogDebugMessage($"Selected backend {selection.BackendId} for {kernelName} with {selection.ConfidenceScore} confidence using {selection.SelectionStrategy}");

        return selection;
    }

    /// <summary>
    /// Records the actual performance results for learning and adaptation.
    /// </summary>
    /// <param name="kernelName">Name of the executed kernel</param>
    /// <param name="workloadCharacteristics">Workload characteristics used for selection</param>
    /// <param name="selectedBackend">Backend that was selected</param>
    /// <param name="performanceResult">Actual performance results</param>
    public async Task RecordPerformanceResultAsync(
        string kernelName,
        WorkloadCharacteristics workloadCharacteristics,
        string selectedBackend,
        PerformanceResult performanceResult)
    {
        if (!_options.EnableLearning)
        {
            return;
        }


        ArgumentException.ThrowIfNullOrEmpty(kernelName);
        ArgumentNullException.ThrowIfNull(workloadCharacteristics);
        ArgumentException.ThrowIfNullOrEmpty(selectedBackend);
        ArgumentNullException.ThrowIfNull(performanceResult);

        var workloadSignature = CreateWorkloadSignature(kernelName, workloadCharacteristics);

        // Update performance history

        var history = _performanceHistory.GetOrAdd(workloadSignature,

            _ => new PerformanceHistory(workloadSignature, _options.MaxHistoryEntries));

        history.AddPerformanceResult(selectedBackend, performanceResult);

        // Update backend state
        if (_backendStates.TryGetValue(selectedBackend, out var backendState))
        {
            backendState.RecordExecution(performanceResult);
        }

        // Invalidate workload analysis cache for this signature
        _ = _workloadAnalysisCache.TryRemove(GetWorkloadCacheKey(workloadSignature), out _);

        _logger.LogTrace("Recorded performance result for {Kernel} on {Backend}: {ExecutionTime}ms, {Throughput} ops/sec",
            kernelName, selectedBackend, performanceResult.ExecutionTimeMs, performanceResult.ThroughputOpsPerSecond);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets current performance insights for debugging and monitoring.
    /// </summary>
    /// <returns>Performance insights across all tracked workloads and backends</returns>
    public PerformanceInsights GetPerformanceInsights()
    {
        var insights = new PerformanceInsights
        {
            Timestamp = DateTimeOffset.UtcNow,
            TotalWorkloadSignatures = _performanceHistory.Count,
            TotalBackends = _backendStates.Count,
            BackendStates = _backendStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetSummary()),
            TopPerformingPairs = GetTopPerformingWorkloadBackendPairs(10),
            LearningStatistics = GetLearningStatistics()
        };

        return insights;
    }

    private async Task<BackendSelection> ApplyOptimalSelectionStrategyAsync(
        List<IAccelerator> backends,
        WorkloadAnalysis workloadAnalysis,
        WorkloadSignature workloadSignature,
        SelectionConstraints? constraints)
    {
        // Strategy 1: Historical performance-based selection (highest priority)
        if (_options.EnableLearning && workloadAnalysis.HasSufficientHistory)
        {
            var historicalSelection = SelectBasedOnHistory(backends, workloadAnalysis, constraints);
            if (historicalSelection.ConfidenceScore >= _options.MinConfidenceThreshold)
            {
                return historicalSelection;
            }
        }

        // Strategy 2: Real-time performance-based selection
        var realtimeSelection = await SelectBasedOnRealtimePerformanceAsync(backends, workloadAnalysis, constraints);
        if (realtimeSelection.ConfidenceScore >= _options.MinConfidenceThreshold)
        {
            return realtimeSelection;
        }

        // Strategy 3: Workload characteristics-based selection
        var characteristicsSelection = SelectBasedOnCharacteristics(backends, workloadAnalysis, constraints);
        if (characteristicsSelection.ConfidenceScore >= _options.MinConfidenceThreshold)
        {
            return characteristicsSelection;
        }

        // Strategy 4: Fallback to priority-based selection
        return SelectBasedOnPriority(backends, constraints);
    }

    private BackendSelection SelectBasedOnHistory(
        List<IAccelerator> backends,
        WorkloadAnalysis workloadAnalysis,
        SelectionConstraints? constraints)
    {
        var availableBackendIds = backends.Select(GetBackendId).ToHashSet();
        var historicalPerformance = workloadAnalysis.HistoricalPerformance
            .Where(kvp => availableBackendIds.Contains(kvp.Key))
            .ToList();

        if (historicalPerformance.Count == 0)
        {
            return new BackendSelection { ConfidenceScore = 0f };
        }

        // Select based on composite score (execution time + throughput + reliability)
        var bestBackend = historicalPerformance
            .Where(kvp => constraints?.IsBackendAllowed(kvp.Key) != false)
            .OrderByDescending(kvp => CalculateCompositeScore(kvp.Value))
            .FirstOrDefault();

        if (bestBackend.Key == null)
        {
            return new BackendSelection { ConfidenceScore = 0f };
        }

        var selectedAccelerator = backends.First(b => GetBackendId(b) == bestBackend.Key);
        var confidence = CalculateHistoricalConfidence(bestBackend.Value, workloadAnalysis.TotalHistoryEntries);

        return new BackendSelection
        {
            SelectedBackend = selectedAccelerator,
            BackendId = bestBackend.Key,
            ConfidenceScore = confidence,
            Reason = $"Historical performance: avg {bestBackend.Value.AverageExecutionTimeMs:F1}ms, {bestBackend.Value.AverageThroughput:F0} ops/sec",
            SelectionStrategy = SelectionStrategy.Historical,
            Metadata = new Dictionary<string, object>
            {
                ["HistoricalSamples"] = bestBackend.Value.SampleCount,
                ["AverageExecutionTime"] = bestBackend.Value.AverageExecutionTimeMs,
                ["Reliability"] = bestBackend.Value.ReliabilityScore
            }
        };
    }

    private async Task<BackendSelection> SelectBasedOnRealtimePerformanceAsync(
        List<IAccelerator> backends,
        WorkloadAnalysis workloadAnalysis,
        SelectionConstraints? constraints)
    {
        var realtimeScores = new Dictionary<string, double>();

        foreach (var backend in backends)
        {
            var backendId = GetBackendId(backend);
            if (constraints?.IsBackendAllowed(backendId) == false)
            {
                continue;
            }


            if (_backendStates.TryGetValue(backendId, out var state))
            {
                var systemMetrics = PerformanceMonitor.GetSystemPerformanceSnapshot();
                var score = CalculateRealtimeScore(state, systemMetrics, workloadAnalysis.WorkloadPattern);
                realtimeScores[backendId] = score;
            }
        }

        if (realtimeScores.Count == 0)
        {
            return new BackendSelection { ConfidenceScore = 0f };
        }

        var bestBackendId = realtimeScores.OrderByDescending(kvp => kvp.Value).First().Key;
        var bestBackend = backends.First(b => GetBackendId(b) == bestBackendId);
        var bestState = _backendStates[bestBackendId];


        await Task.CompletedTask;

        return new BackendSelection
        {
            SelectedBackend = bestBackend,
            BackendId = bestBackendId,
            ConfidenceScore = Math.Min(0.8f, (float)realtimeScores[bestBackendId]),
            Reason = $"Real-time performance: {bestState.RecentAverageExecutionTimeMs:F1}ms avg, {bestState.CurrentUtilization:P1} utilization",
            SelectionStrategy = SelectionStrategy.RealTime,
            Metadata = new Dictionary<string, object>
            {
                ["RecentExecutions"] = bestState.RecentExecutionCount,
                ["CurrentUtilization"] = bestState.CurrentUtilization,
                ["Score"] = realtimeScores[bestBackendId]
            }
        };
    }

    private BackendSelection SelectBasedOnCharacteristics(
        List<IAccelerator> backends,
        WorkloadAnalysis workloadAnalysis,
        SelectionConstraints? constraints)
    {
        var workloadPattern = workloadAnalysis.WorkloadPattern;
        var characteristicScores = new Dictionary<string, double>();

        foreach (var backend in backends)
        {
            var backendId = GetBackendId(backend);
            if (constraints?.IsBackendAllowed(backendId) == false)
            {
                continue;
            }


            var score = CalculateCharacteristicScore(backendId, workloadPattern);
            characteristicScores[backendId] = score;
        }

        if (characteristicScores.Count == 0)
        {
            return new BackendSelection { ConfidenceScore = 0f };
        }

        var bestBackendId = characteristicScores.OrderByDescending(kvp => kvp.Value).First().Key;
        var bestBackend = backends.First(b => GetBackendId(b) == bestBackendId);

        return new BackendSelection
        {
            SelectedBackend = bestBackend,
            BackendId = bestBackendId,
            ConfidenceScore = 0.6f,
            Reason = $"Workload characteristics: {workloadPattern} pattern favors {bestBackendId}",
            SelectionStrategy = SelectionStrategy.Characteristics,
            Metadata = new Dictionary<string, object>
            {
                ["WorkloadPattern"] = workloadPattern.ToString(),
                ["CharacteristicScore"] = characteristicScores[bestBackendId]
            }
        };
    }

    private BackendSelection SelectBasedOnPriority(
        List<IAccelerator> backends,
        SelectionConstraints? constraints)
    {
        var priorityOrder = new[] { "CUDA", "Metal", "OpenCL", "CPU" };


        foreach (var preferredBackend in priorityOrder)
        {
            var backend = backends.FirstOrDefault(b =>

                GetBackendId(b).Equals(preferredBackend, StringComparison.OrdinalIgnoreCase));


            if (backend != null && constraints?.IsBackendAllowed(GetBackendId(backend)) != false)
            {
                return new BackendSelection
                {
                    SelectedBackend = backend,
                    BackendId = GetBackendId(backend),
                    ConfidenceScore = 0.4f,
                    Reason = $"Priority-based fallback selection",
                    SelectionStrategy = SelectionStrategy.Priority,
                    Metadata = new Dictionary<string, object>
                    {
                        ["FallbackReason"] = "Insufficient data for other strategies"
                    }
                };
            }
        }

        // Last resort - first available backend
        var firstBackend = backends.First();
        return new BackendSelection
        {
            SelectedBackend = firstBackend,
            BackendId = GetBackendId(firstBackend),
            ConfidenceScore = 0.2f,
            Reason = "Last resort - first available backend",
            SelectionStrategy = SelectionStrategy.Fallback
        };
    }

    private static WorkloadSignature CreateWorkloadSignature(string kernelName, WorkloadCharacteristics characteristics)
    {
        return new WorkloadSignature
        {
            KernelName = kernelName,
            DataSize = characteristics.DataSize,
            ComputeIntensity = characteristics.ComputeIntensity,
            MemoryIntensity = characteristics.MemoryIntensity,
            ParallelismLevel = characteristics.ParallelismLevel,
            WorkloadPattern = DetermineWorkloadPattern(characteristics)
        };
    }

    private async Task<WorkloadAnalysis> AnalyzeWorkloadAsync(
        WorkloadSignature signature,
        WorkloadCharacteristics characteristics)
    {
        var cacheKey = GetWorkloadCacheKey(signature);


        if (_workloadAnalysisCache.TryGetValue(cacheKey, out var cachedAnalysis))
        {
            return cachedAnalysis;
        }

        var analysis = new WorkloadAnalysis
        {
            WorkloadSignature = signature,
            WorkloadPattern = signature.WorkloadPattern,
            EstimatedExecutionTimeMs = EstimateExecutionTime(characteristics),
            EstimatedMemoryUsageMB = EstimateMemoryUsage(characteristics),
            HistoricalPerformance = [],
            TotalHistoryEntries = 0
        };

        // Get historical performance data
        if (_performanceHistory.TryGetValue(signature, out var history))
        {
            analysis.HistoricalPerformance = history.GetPerformanceStats();
            analysis.TotalHistoryEntries = history.TotalEntries;
            analysis.HasSufficientHistory = analysis.TotalHistoryEntries >= _options.MinHistoryForLearning;
        }

        _ = _workloadAnalysisCache.TryAdd(cacheKey, analysis);
        await Task.CompletedTask;

        return analysis;
    }

    private static WorkloadPattern DetermineWorkloadPattern(WorkloadCharacteristics characteristics)
    {
        // Determine pattern based on compute vs memory intensity
        var computeRatio = characteristics.ComputeIntensity;
        var memoryRatio = characteristics.MemoryIntensity;


        if (computeRatio > 0.7 && memoryRatio < 0.3)
        {

            return WorkloadPattern.ComputeIntensive;
        }


        if (memoryRatio > 0.7 && computeRatio < 0.3)
        {

            return WorkloadPattern.MemoryIntensive;
        }


        if (computeRatio > 0.5 && memoryRatio > 0.5)
        {

            return WorkloadPattern.Balanced;
        }


        if (characteristics.ParallelismLevel > 0.8)
        {

            return WorkloadPattern.HighlyParallel;
        }


        return WorkloadPattern.Sequential;
    }

    private static double CalculateCompositeScore(BackendPerformanceStats stats)
    {
        // Composite score based on execution time (lower is better), throughput (higher is better), and reliability
        var timeScore = stats.AverageExecutionTimeMs > 0 ? 1000.0 / stats.AverageExecutionTimeMs : 0;
        var throughputScore = stats.AverageThroughput / 10000.0; // Normalize to 0-1 range
        var reliabilityScore = stats.ReliabilityScore;

        // Weighted combination

        return (timeScore * 0.4) + (throughputScore * 0.4) + (reliabilityScore * 0.2);
    }

    private float CalculateHistoricalConfidence(BackendPerformanceStats stats, int totalSamples)
    {
        // Confidence based on sample size and consistency
        var sampleConfidence = Math.Min(1.0f, stats.SampleCount / (float)_options.MinSamplesForHighConfidence);
        var consistencyConfidence = 1.0f - (stats.ExecutionTimeStdDev / Math.Max(1.0f, stats.AverageExecutionTimeMs));
        var reliabilityConfidence = stats.ReliabilityScore;


        return (float)((sampleConfidence + consistencyConfidence + reliabilityConfidence) / 3.0f);
    }

    private static double CalculateRealtimeScore(
        BackendPerformanceState state,
        SystemPerformanceSnapshot systemSnapshot,
        WorkloadPattern workloadPattern)
    {
        // Real-time score based on current system state and backend performance
        var utilizationScore = 1.0 - state.CurrentUtilization; // Lower utilization is better
        var recentPerformanceScore = state.RecentAverageExecutionTimeMs > 0

            ? 1000.0 / state.RecentAverageExecutionTimeMs

            : 0.1;

        // Adjust for system load

        var systemLoadScore = 1.0 - (systemSnapshot.CpuUsage + systemSnapshot.MemoryUsage) / 2.0;

        // Pattern-specific adjustments

        var patternMultiplier = workloadPattern switch
        {
            WorkloadPattern.ComputeIntensive => GetBackendComputeAffinity(state.BackendId),
            WorkloadPattern.MemoryIntensive => GetBackendMemoryAffinity(state.BackendId),
            WorkloadPattern.HighlyParallel => GetBackendParallelismAffinity(state.BackendId),
            _ => 1.0
        };


        return (utilizationScore * 0.3 + recentPerformanceScore * 0.4 + systemLoadScore * 0.3) * patternMultiplier;
    }

    private static double CalculateCharacteristicScore(string backendId, WorkloadPattern pattern)
    {
        // Score backend based on its suitability for the workload pattern
        return pattern switch
        {
            WorkloadPattern.ComputeIntensive => GetBackendComputeAffinity(backendId),
            WorkloadPattern.MemoryIntensive => GetBackendMemoryAffinity(backendId),
            WorkloadPattern.HighlyParallel => GetBackendParallelismAffinity(backendId),
            WorkloadPattern.Balanced => GetBackendBalancedAffinity(backendId),
            WorkloadPattern.Sequential => GetBackendSequentialAffinity(backendId),
            _ => 0.5
        };
    }

    private static double GetBackendComputeAffinity(string backendId) => backendId.ToUpperInvariant() switch
    {
        "CUDA" => 0.95,
        "METAL" => 0.9,
        "OPENCL" => 0.85,
        "CPU" => 0.6,
        _ => 0.5
    };

    private static double GetBackendMemoryAffinity(string backendId) => backendId.ToUpperInvariant() switch
    {
        "CPU" => 0.9,
        "CUDA" => 0.7,
        "METAL" => 0.75,
        "OPENCL" => 0.7,
        _ => 0.5
    };

    private static double GetBackendParallelismAffinity(string backendId) => backendId.ToUpperInvariant() switch
    {
        "CUDA" => 0.95,
        "OPENCL" => 0.9,
        "METAL" => 0.85,
        "CPU" => 0.4,
        _ => 0.5
    };

    private static double GetBackendBalancedAffinity(string backendId) => backendId.ToUpperInvariant() switch
    {
        "CUDA" => 0.85,
        "METAL" => 0.8,
        "CPU" => 0.75,
        "OPENCL" => 0.7,
        _ => 0.5
    };

    private static double GetBackendSequentialAffinity(string backendId) => backendId.ToUpperInvariant() switch
    {
        "CPU" => 0.9,
        "CUDA" => 0.3,
        "METAL" => 0.4,
        "OPENCL" => 0.3,
        _ => 0.5
    };

    private static double EstimateExecutionTime(WorkloadCharacteristics characteristics)
    {
        // Simplified execution time estimation based on workload characteristics
        var baseTime = characteristics.DataSize / 1000000.0; // Base on data size (MB)
        var computeMultiplier = 1.0 + characteristics.ComputeIntensity * 2.0;
        var memoryMultiplier = 1.0 + characteristics.MemoryIntensity * 1.5;
        var parallelismDivisor = Math.Max(1.0, characteristics.ParallelismLevel * Environment.ProcessorCount);


        return (baseTime * computeMultiplier * memoryMultiplier) / parallelismDivisor;
    }

    private static long EstimateMemoryUsage(WorkloadCharacteristics characteristics)
    {
        // Simplified memory usage estimation
        var baseMemory = characteristics.DataSize / 1024 / 1024; // Convert to MB
        var memoryMultiplier = 1.0 + characteristics.MemoryIntensity * 3.0;


        return (long)(baseMemory * memoryMultiplier);
    }

    private static string GetWorkloadCacheKey(WorkloadSignature signature)
        => $"{signature.KernelName}_{signature.DataSize}_{signature.ComputeIntensity:F2}_{signature.MemoryIntensity:F2}_{signature.ParallelismLevel:F2}";

    private string GetBackendId(IAccelerator accelerator)
    {
        // This would need to be implemented based on the actual IAccelerator interface
        return accelerator.GetType().Name.Replace("Accelerator", "");
    }

    private List<(WorkloadSignature Workload, string Backend, double PerformanceScore)> GetTopPerformingWorkloadBackendPairs(int count)
    {
        var topPairs = new List<(WorkloadSignature Workload, string Backend, double PerformanceScore)>();

        foreach (var historyEntry in _performanceHistory)
        {
            var workload = historyEntry.Key;
            var performanceStats = historyEntry.Value.GetPerformanceStats();

            foreach (var backendStats in performanceStats)
            {
                var score = CalculateCompositeScore(backendStats.Value);
                topPairs.Add((workload, backendStats.Key, score));
            }
        }

        return topPairs.OrderByDescending(p => p.PerformanceScore).Take(count).ToList();
    }

    private LearningStatistics GetLearningStatistics()
    {
        var totalSamples = _performanceHistory.Values.Sum(h => h.TotalEntries);
        var averageSamplesPerWorkload = _performanceHistory.Count > 0

            ? totalSamples / _performanceHistory.Count

            : 0;

        return new LearningStatistics
        {
            TotalPerformanceSamples = totalSamples,
            AverageSamplesPerWorkload = averageSamplesPerWorkload,
            WorkloadsWithSufficientHistory = _performanceHistory.Values.Count(h => h.TotalEntries >= _options.MinHistoryForLearning),
            LearningEffectiveness = CalculateLearningEffectiveness()
        };
    }

    private float CalculateLearningEffectiveness()
    {
        // Simplified learning effectiveness calculation
        var totalWorkloads = _performanceHistory.Count;
        if (totalWorkloads == 0)
        {
            return 0f;
        }


        var workloadsWithGoodHistory = _performanceHistory.Values.Count(h => h.TotalEntries >= _options.MinHistoryForLearning);
        return (float)workloadsWithGoodHistory / totalWorkloads;
    }

    private void UpdateBackendPerformanceStates(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var systemSnapshot = PerformanceMonitor.GetSystemPerformanceSnapshot();


            foreach (var backendState in _backendStates.Values)
            {
                backendState.UpdateState(systemSnapshot);
            }

            _logger.LogTrace("Updated performance states for {BackendCount} backends", _backendStates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating backend performance states");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _performanceUpdateTimer?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}


// Supporting data structures and enums will be defined in separate files...
