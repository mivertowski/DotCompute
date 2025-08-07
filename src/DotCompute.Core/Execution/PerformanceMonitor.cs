// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Kernels;

namespace DotCompute.Core.Execution;

/// <summary>
/// Monitors and analyzes parallel execution performance with machine learning-based optimization.
/// </summary>
public sealed class PerformanceMonitor : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<ExecutionRecord> _executionHistory;
    private readonly ConcurrentDictionary<string, KernelPerformanceProfile> _kernelProfiles;
    private readonly ConcurrentDictionary<string, DevicePerformanceProfile> _deviceProfiles;
    private readonly PerformanceAnalyzer _analyzer;
    private readonly AdaptiveOptimizer _optimizer;
    private readonly object _metricsLock = new();
    private ParallelExecutionMetrics _currentMetrics;
    private bool _disposed;

    private const int MaxHistorySize = 10000;
    private const int AnalysisWindowSize = 100;

    public PerformanceMonitor(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executionHistory = new ConcurrentQueue<ExecutionRecord>();
        _kernelProfiles = new ConcurrentDictionary<string, KernelPerformanceProfile>();
        _deviceProfiles = new ConcurrentDictionary<string, DevicePerformanceProfile>();
        _analyzer = new PerformanceAnalyzer(logger);
        _optimizer = new AdaptiveOptimizer(logger);
        _currentMetrics = new ParallelExecutionMetrics();
    }

    /// <summary>
    /// Records a parallel execution result for analysis.
    /// </summary>
    public void RecordExecution(ParallelExecutionResult result)
    {
        var record = new ExecutionRecord
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Strategy = result.Strategy,
            Success = result.Success,
            TotalExecutionTimeMs = result.TotalExecutionTimeMs,
            ThroughputGFLOPS = result.ThroughputGFLOPS,
            MemoryBandwidthGBps = result.MemoryBandwidthGBps,
            EfficiencyPercentage = result.EfficiencyPercentage,
            DeviceResults = result.DeviceResults.ToArray(),
            ErrorMessage = result.ErrorMessage
        };

        // Add to history
        _executionHistory.Enqueue(record);

        // Limit history size
        while (_executionHistory.Count > MaxHistorySize)
        {
            _executionHistory.TryDequeue(out _);
        }

        // Update metrics
        UpdateMetrics(record);

        // Update device profiles
        UpdateDeviceProfiles(record);

        // Trigger analysis if we have enough data
        if (_executionHistory.Count % AnalysisWindowSize == 0)
        {
            _ = Task.Run(() => AnalyzePerformanceAsync());
        }

        _logger.LogDebug("Recorded execution: Strategy={Strategy}, Success={Success}, Time={ExecutionTimeMs:F2}ms, Efficiency={EfficiencyPercentage:F1}%",
            result.Strategy, result.Success, result.TotalExecutionTimeMs, result.EfficiencyPercentage);
    }

    /// <summary>
    /// Records kernel-specific performance data.
    /// </summary>
    public void RecordKernelExecution(string kernelName, string deviceId, double executionTimeMs, double throughputGFLOPS)
    {
        var profile = _kernelProfiles.GetOrAdd(kernelName, _ => new KernelPerformanceProfile { KernelName = kernelName });
        
        profile.AddExecution(deviceId, executionTimeMs, throughputGFLOPS);

        _logger.LogTrace("Recorded kernel execution: {KernelName} on {DeviceId}, Time={ExecutionTimeMs:F2}ms, Throughput={ThroughputGFLOPS:F2} GFLOPS",
            kernelName, deviceId, executionTimeMs, throughputGFLOPS);
    }

    /// <summary>
    /// Gets current performance metrics.
    /// </summary>
    public ParallelExecutionMetrics GetCurrentMetrics()
    {
        lock (_metricsLock)
        {
            return new ParallelExecutionMetrics
            {
                TotalExecutions = _currentMetrics.TotalExecutions,
                AverageExecutionTimeMs = _currentMetrics.AverageExecutionTimeMs,
                AverageEfficiencyPercentage = _currentMetrics.AverageEfficiencyPercentage,
                TotalGFLOPSHours = _currentMetrics.TotalGFLOPSHours,
                MetricsByStrategy = new Dictionary<ExecutionStrategyType, StrategyMetrics>(_currentMetrics.MetricsByStrategy),
                MetricsByDevice = new Dictionary<string, DeviceMetrics>(_currentMetrics.MetricsByDevice)
            };
        }
    }

    /// <summary>
    /// Gets comprehensive performance analysis with optimization recommendations.
    /// </summary>
    public ParallelExecutionAnalysis GetPerformanceAnalysis()
    {
        var recentExecutions = GetRecentExecutions(AnalysisWindowSize);
        
        if (recentExecutions.Length == 0)
        {
            return new ParallelExecutionAnalysis
            {
                OverallRating = 5.0,
                RecommendedStrategy = ExecutionStrategyType.Single,
                OptimizationRecommendations = new List<string> { "No execution data available for analysis." }
            };
        }

        return _analyzer.AnalyzePerformance(recentExecutions, _deviceProfiles.Values.ToArray());
    }

    /// <summary>
    /// Recommends optimal execution strategy based on historical performance and problem characteristics.
    /// </summary>
    public ExecutionStrategyRecommendation RecommendOptimalStrategy(
        string kernelName,
        int[] inputSizes,
        AcceleratorType[] availableAcceleratorTypes)
    {
        var recentExecutions = GetRecentExecutions(AnalysisWindowSize);
        var kernelProfile = _kernelProfiles.GetValueOrDefault(kernelName);
        
        return _optimizer.RecommendStrategy(
            kernelName, inputSizes, availableAcceleratorTypes, recentExecutions, kernelProfile);
    }

    /// <summary>
    /// Gets performance trends over time.
    /// </summary>
    public PerformanceTrends GetPerformanceTrends(TimeSpan timeWindow)
    {
        var cutoffTime = DateTimeOffset.UtcNow - timeWindow;
        var relevantExecutions = _executionHistory
            .Where(e => e.Timestamp >= cutoffTime)
            .OrderBy(e => e.Timestamp)
            .ToArray();

        return _analyzer.AnalyzeTrends(relevantExecutions);
    }

    /// <summary>
    /// Gets device utilization analysis.
    /// </summary>
    public Dictionary<string, DeviceUtilizationAnalysis> GetDeviceUtilizationAnalysis()
    {
        var analysis = new Dictionary<string, DeviceUtilizationAnalysis>();
        
        foreach (var profile in _deviceProfiles.Values)
        {
            analysis[profile.DeviceId] = new DeviceUtilizationAnalysis
            {
                DeviceId = profile.DeviceId,
                AverageUtilizationPercentage = profile.AverageUtilizationPercentage,
                PeakUtilizationPercentage = profile.PeakUtilizationPercentage,
                IdleTimePercentage = profile.IdleTimePercentage,
                BottleneckSeverity = profile.PrimaryBottleneck?.Severity ?? 0,
                RecommendedOptimizations = profile.GetOptimizationRecommendations()
            };
        }

        return analysis;
    }

    /// <summary>
    /// Clears all performance history and resets metrics.
    /// </summary>
    public void Reset()
    {
        while (_executionHistory.TryDequeue(out _)) { }
        
        _kernelProfiles.Clear();
        _deviceProfiles.Clear();

        lock (_metricsLock)
        {
            _currentMetrics = new ParallelExecutionMetrics();
        }

        _logger.LogInformation("Performance monitor reset");
    }

    public void Dispose()
    {
        if (_disposed) return;

        Reset();
        _disposed = true;
        
        _logger.LogInformation("Performance monitor disposed");
    }

    #region Private Methods

    private void UpdateMetrics(ExecutionRecord record)
    {
        lock (_metricsLock)
        {
            _currentMetrics.TotalExecutions++;
            
            // Update average execution time
            var totalTime = _currentMetrics.AverageExecutionTimeMs * (_currentMetrics.TotalExecutions - 1) + record.TotalExecutionTimeMs;
            _currentMetrics.AverageExecutionTimeMs = totalTime / _currentMetrics.TotalExecutions;

            // Update average efficiency
            var totalEfficiency = _currentMetrics.AverageEfficiencyPercentage * (_currentMetrics.TotalExecutions - 1) + record.EfficiencyPercentage;
            _currentMetrics.AverageEfficiencyPercentage = totalEfficiency / _currentMetrics.TotalExecutions;

            // Update GFLOPS-hours
            _currentMetrics.TotalGFLOPSHours += record.ThroughputGFLOPS * (record.TotalExecutionTimeMs / 3600000.0);

            // Update strategy metrics
            UpdateStrategyMetrics(record);

            // Update device metrics
            UpdateDeviceMetrics(record);
        }
    }

    private void UpdateStrategyMetrics(ExecutionRecord record)
    {
        if (!_currentMetrics.MetricsByStrategy.TryGetValue(record.Strategy, out var strategyMetrics))
        {
            strategyMetrics = new StrategyMetrics();
            _currentMetrics.MetricsByStrategy[record.Strategy] = strategyMetrics;
        }

        strategyMetrics.UsageCount++;
        
        // Update average execution time
        var totalTime = strategyMetrics.AverageExecutionTimeMs * (strategyMetrics.UsageCount - 1) + record.TotalExecutionTimeMs;
        strategyMetrics.AverageExecutionTimeMs = totalTime / strategyMetrics.UsageCount;

        // Update average efficiency
        var totalEfficiency = strategyMetrics.AverageEfficiencyPercentage * (strategyMetrics.UsageCount - 1) + record.EfficiencyPercentage;
        strategyMetrics.AverageEfficiencyPercentage = totalEfficiency / strategyMetrics.UsageCount;

        // Update success rate
        var successfulExecutions = record.Success ? 1 : 0;
        var totalSuccessful = (strategyMetrics.SuccessRatePercentage * (strategyMetrics.UsageCount - 1) / 100.0) + successfulExecutions;
        strategyMetrics.SuccessRatePercentage = (totalSuccessful / strategyMetrics.UsageCount) * 100;
    }

    private void UpdateDeviceMetrics(ExecutionRecord record)
    {
        foreach (var deviceResult in record.DeviceResults)
        {
            if (!_currentMetrics.MetricsByDevice.TryGetValue(deviceResult.DeviceId, out var deviceMetrics))
            {
                deviceMetrics = new DeviceMetrics { DeviceId = deviceResult.DeviceId };
                _currentMetrics.MetricsByDevice[deviceResult.DeviceId] = deviceMetrics;
            }

            deviceMetrics.TotalExecutions++;
            
            // Update averages
            var totalTime = deviceMetrics.AverageExecutionTimeMs * (deviceMetrics.TotalExecutions - 1) + deviceResult.ExecutionTimeMs;
            deviceMetrics.AverageExecutionTimeMs = totalTime / deviceMetrics.TotalExecutions;

            var totalThroughput = deviceMetrics.AverageThroughputGFLOPS * (deviceMetrics.TotalExecutions - 1) + deviceResult.ThroughputGFLOPS;
            deviceMetrics.AverageThroughputGFLOPS = totalThroughput / deviceMetrics.TotalExecutions;

            var totalBandwidth = deviceMetrics.AverageMemoryBandwidthGBps * (deviceMetrics.TotalExecutions - 1) + deviceResult.MemoryBandwidthGBps;
            deviceMetrics.AverageMemoryBandwidthGBps = totalBandwidth / deviceMetrics.TotalExecutions;
        }
    }

    private void UpdateDeviceProfiles(ExecutionRecord record)
    {
        foreach (var deviceResult in record.DeviceResults)
        {
            var profile = _deviceProfiles.GetOrAdd(deviceResult.DeviceId, _ => new DevicePerformanceProfile
            {
                DeviceId = deviceResult.DeviceId
            });

            profile.AddExecution(deviceResult);
        }
    }

    private ExecutionRecord[] GetRecentExecutions(int count)
    {
        return _executionHistory
            .TakeLast(count)
            .OrderByDescending(e => e.Timestamp)
            .ToArray();
    }

    private async Task AnalyzePerformanceAsync()
    {
        try
        {
            var recentExecutions = GetRecentExecutions(AnalysisWindowSize);
            if (recentExecutions.Length == 0) return;

            // Perform background analysis
            var analysis = _analyzer.AnalyzePerformance(recentExecutions, _deviceProfiles.Values.ToArray());
            
            // Log findings
            if (analysis.Bottlenecks.Any())
            {
                var primaryBottleneck = analysis.Bottlenecks.OrderByDescending(b => b.Severity).First();
                _logger.LogInformation("Performance analysis: Primary bottleneck is {BottleneckType} with severity {Severity:F2}",
                    primaryBottleneck.Type, primaryBottleneck.Severity);
            }

            if (analysis.OptimizationRecommendations.Any())
            {
                _logger.LogInformation("Performance recommendations: {Recommendations}",
                    string.Join("; ", analysis.OptimizationRecommendations));
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during performance analysis");
        }
    }

    #endregion
}

/// <summary>
/// Analyzes performance data and identifies bottlenecks.
/// </summary>
public class PerformanceAnalyzer
{
    private readonly ILogger _logger;

    public PerformanceAnalyzer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ParallelExecutionAnalysis AnalyzePerformance(ExecutionRecord[] executions, DevicePerformanceProfile[] deviceProfiles)
    {
        var analysis = new ParallelExecutionAnalysis();

        // Calculate overall rating
        analysis.OverallRating = CalculateOverallRating(executions);

        // Identify bottlenecks
        analysis.Bottlenecks = IdentifyBottlenecks(executions, deviceProfiles).ToList();

        // Generate optimization recommendations
        analysis.OptimizationRecommendations = GenerateOptimizationRecommendations(executions, analysis.Bottlenecks).ToList();

        // Recommend optimal strategy
        analysis.RecommendedStrategy = RecommendStrategy(executions);

        // Analyze device utilization
        analysis.DeviceUtilizationAnalysis = AnalyzeDeviceUtilization(deviceProfiles);

        return analysis;
    }

    public PerformanceTrends AnalyzeTrends(ExecutionRecord[] executions)
    {
        if (executions.Length == 0)
        {
            return new PerformanceTrends();
        }

        var trends = new PerformanceTrends
        {
            TimeRange = new TimeRange
            {
                Start = executions.First().Timestamp,
                End = executions.Last().Timestamp
            }
        };

        // Calculate throughput trend
        trends.ThroughputTrend = CalculateTrend(executions.Select(e => e.ThroughputGFLOPS).ToArray());

        // Calculate efficiency trend
        trends.EfficiencyTrend = CalculateTrend(executions.Select(e => e.EfficiencyPercentage).ToArray());

        // Calculate execution time trend
        trends.ExecutionTimeTrend = CalculateTrend(executions.Select(e => e.TotalExecutionTimeMs).ToArray());

        return trends;
    }

    private double CalculateOverallRating(ExecutionRecord[] executions)
    {
        if (executions.Length == 0) return 5.0;

        var avgEfficiency = executions.Average(e => e.EfficiencyPercentage);
        var successRate = executions.Count(e => e.Success) / (double)executions.Length;
        
        // Rating from 1-10 based on efficiency and success rate
        var efficiencyRating = Math.Min(10, avgEfficiency / 10.0);
        var successRating = successRate * 10.0;
        
        return (efficiencyRating + successRating) / 2.0;
    }

    private IEnumerable<BottleneckAnalysis> IdentifyBottlenecks(ExecutionRecord[] executions, DevicePerformanceProfile[] deviceProfiles)
    {
        var bottlenecks = new List<BottleneckAnalysis>();

        // Memory bandwidth bottleneck
        var avgMemoryEfficiency = executions.Average(e => 
            e.DeviceResults.Where(d => d.Success).Average(d => d.MemoryBandwidthGBps));
        
        if (avgMemoryEfficiency < 100) // Assuming 100 GB/s as baseline
        {
            bottlenecks.Add(new BottleneckAnalysis
            {
                Type = BottleneckType.MemoryBandwidth,
                Severity = 1.0 - (avgMemoryEfficiency / 100),
                Details = $"Average memory bandwidth utilization is low: {avgMemoryEfficiency:F1} GB/s"
            });
        }

        // Parallel efficiency bottleneck
        var avgParallelEfficiency = executions.Average(e => e.EfficiencyPercentage);
        if (avgParallelEfficiency < 60)
        {
            bottlenecks.Add(new BottleneckAnalysis
            {
                Type = BottleneckType.Synchronization,
                Severity = (60 - avgParallelEfficiency) / 60,
                Details = $"Low parallel efficiency: {avgParallelEfficiency:F1}%"
            });
        }

        return bottlenecks.OrderByDescending(b => b.Severity);
    }

    private IEnumerable<string> GenerateOptimizationRecommendations(ExecutionRecord[] executions, List<BottleneckAnalysis> bottlenecks)
    {
        var recommendations = new List<string>();

        foreach (var bottleneck in bottlenecks.Take(3)) // Top 3 bottlenecks
        {
            switch (bottleneck.Type)
            {
                case BottleneckType.MemoryBandwidth:
                    recommendations.Add("Consider using larger batch sizes or optimizing memory access patterns");
                    break;
                case BottleneckType.Synchronization:
                    recommendations.Add("Reduce synchronization overhead by using asynchronous operations");
                    break;
                case BottleneckType.Compute:
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

    private ExecutionStrategyType RecommendStrategy(ExecutionRecord[] executions)
    {
        if (executions.Length == 0) return ExecutionStrategyType.Single;

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

    private Dictionary<string, double> AnalyzeDeviceUtilization(DevicePerformanceProfile[] deviceProfiles)
    {
        return deviceProfiles.ToDictionary(
            p => p.DeviceId,
            p => p.AverageUtilizationPercentage
        );
    }

    private TrendDirection CalculateTrend(double[] values)
    {
        if (values.Length < 2) return TrendDirection.Stable;

        var correlation = CalculateCorrelation(values);
        
        return correlation switch
        {
            > 0.1 => TrendDirection.Improving,
            < -0.1 => TrendDirection.Degrading,
            _ => TrendDirection.Stable
        };
    }

    private double CalculateCorrelation(double[] values)
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

/// <summary>
/// Provides adaptive optimization recommendations based on machine learning.
/// </summary>
public class AdaptiveOptimizer
{
    private readonly ILogger _logger;

    public AdaptiveOptimizer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ExecutionStrategyRecommendation RecommendStrategy(
        string kernelName,
        int[] inputSizes,
        AcceleratorType[] availableAcceleratorTypes,
        ExecutionRecord[] recentExecutions,
        KernelPerformanceProfile? kernelProfile)
    {
        // Simple heuristic-based recommendation (could be replaced with ML model)
        var totalElements = inputSizes.Aggregate(1L, (a, b) => a * b);
        var hasMultipleGpus = availableAcceleratorTypes.Count(t => t != AcceleratorType.CPU) > 1;

        ExecutionStrategyType recommendedStrategy;
        double confidenceScore;
        string reasoning;

        if (totalElements > 1_000_000 && hasMultipleGpus)
        {
            // Large problem, multiple GPUs available
            var dataParallelPerformance = GetStrategyPerformance(ExecutionStrategyType.DataParallel, recentExecutions);
            var workStealingPerformance = GetStrategyPerformance(ExecutionStrategyType.WorkStealing, recentExecutions);

            if (workStealingPerformance > dataParallelPerformance * 1.1)
            {
                recommendedStrategy = ExecutionStrategyType.WorkStealing;
                reasoning = "Work stealing shows better performance for irregular workloads";
                confidenceScore = 0.8;
            }
            else
            {
                recommendedStrategy = ExecutionStrategyType.DataParallel;
                reasoning = "Large problem size benefits from data parallelism across multiple GPUs";
                confidenceScore = 0.9;
            }
        }
        else if (totalElements > 100_000)
        {
            // Medium problem size
            recommendedStrategy = hasMultipleGpus ? ExecutionStrategyType.DataParallel : ExecutionStrategyType.Single;
            reasoning = hasMultipleGpus ? "Medium problem size can benefit from multi-GPU execution" : "Single GPU sufficient for medium problem size";
            confidenceScore = 0.7;
        }
        else
        {
            // Small problem size
            recommendedStrategy = ExecutionStrategyType.Single;
            reasoning = "Small problem size - parallel overhead likely exceeds benefits";
            confidenceScore = 0.95;
        }

        // Adjust based on kernel profile
        if (kernelProfile != null)
        {
            var kernelCharacteristics = AnalyzeKernelCharacteristics(kernelProfile);
            if (kernelCharacteristics.IsMemoryBound && recommendedStrategy != ExecutionStrategyType.PipelineParallel)
            {
                reasoning += "; Consider pipeline parallelism for memory-bound kernels";
            }
        }

        return new ExecutionStrategyRecommendation
        {
            Strategy = recommendedStrategy,
            ConfidenceScore = confidenceScore,
            Reasoning = reasoning,
            ExpectedImprovementPercentage = EstimateImprovement(recommendedStrategy, recentExecutions)
        };
    }

    private double GetStrategyPerformance(ExecutionStrategyType strategy, ExecutionRecord[] executions)
    {
        var strategyExecutions = executions.Where(e => e.Strategy == strategy && e.Success).ToArray();
        return strategyExecutions.Length > 0 ? strategyExecutions.Average(e => e.EfficiencyPercentage) : 0;
    }

    private KernelCharacteristics AnalyzeKernelCharacteristics(KernelPerformanceProfile profile)
    {
        // Analyze kernel to determine if it's compute-bound or memory-bound
        var avgThroughput = profile.DeviceExecutions.SelectMany(kvp => kvp.Value).Average(e => e.ThroughputGFLOPS);
        var avgBandwidth = profile.DeviceExecutions.SelectMany(kvp => kvp.Value)
            .Where(e => e.MemoryBandwidthGBps > 0)
            .DefaultIfEmpty(new KernelExecution())
            .Average(e => e.MemoryBandwidthGBps);

        return new KernelCharacteristics
        {
            IsMemoryBound = avgBandwidth > avgThroughput * 4, // Simple heuristic
            IsComputeBound = avgThroughput > avgBandwidth,
            AverageThroughput = avgThroughput,
            AverageMemoryBandwidth = avgBandwidth
        };
    }

    private double EstimateImprovement(ExecutionStrategyType strategy, ExecutionRecord[] executions)
    {
        // Estimate expected improvement based on historical data
        var currentBestPerformance = executions.Where(e => e.Success).DefaultIfEmpty().Max(e => e?.EfficiencyPercentage ?? 0);
        
        return strategy switch
        {
            ExecutionStrategyType.DataParallel => Math.Max(0, 80 - currentBestPerformance),
            ExecutionStrategyType.WorkStealing => Math.Max(0, 85 - currentBestPerformance),
            ExecutionStrategyType.PipelineParallel => Math.Max(0, 75 - currentBestPerformance),
            _ => 0
        };
    }
}

// Supporting data structures
public class ExecutionRecord
{
    public required Guid Id { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
    public required ExecutionStrategyType Strategy { get; set; }
    public required bool Success { get; set; }
    public required double TotalExecutionTimeMs { get; set; }
    public required double ThroughputGFLOPS { get; set; }
    public required double MemoryBandwidthGBps { get; set; }
    public required double EfficiencyPercentage { get; set; }
    public required DeviceExecutionResult[] DeviceResults { get; set; }
    public string? ErrorMessage { get; set; }
}

public class KernelPerformanceProfile
{
    public required string KernelName { get; set; }
    public Dictionary<string, List<KernelExecution>> DeviceExecutions { get; set; } = new();

    public void AddExecution(string deviceId, double executionTimeMs, double throughputGFLOPS)
    {
        if (!DeviceExecutions.TryGetValue(deviceId, out var executions))
        {
            executions = new List<KernelExecution>();
            DeviceExecutions[deviceId] = executions;
        }

        executions.Add(new KernelExecution
        {
            Timestamp = DateTimeOffset.UtcNow,
            ExecutionTimeMs = executionTimeMs,
            ThroughputGFLOPS = throughputGFLOPS
        });

        // Limit history per device
        if (executions.Count > 1000)
        {
            executions.RemoveAt(0);
        }
    }
}

public class KernelExecution
{
    public DateTimeOffset Timestamp { get; set; }
    public double ExecutionTimeMs { get; set; }
    public double ThroughputGFLOPS { get; set; }
    public double MemoryBandwidthGBps { get; set; }
}

public class DevicePerformanceProfile
{
    public required string DeviceId { get; set; }
    public List<DeviceExecutionResult> Executions { get; set; } = new();
    
    public double AverageUtilizationPercentage { get; private set; }
    public double PeakUtilizationPercentage { get; private set; }
    public double IdleTimePercentage { get; private set; }
    public BottleneckAnalysis? PrimaryBottleneck { get; private set; }

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

    public List<string> GetOptimizationRecommendations()
    {
        var recommendations = new List<string>();
        
        if (AverageUtilizationPercentage < 50)
        {
            recommendations.Add("Increase workload size or improve kernel efficiency");
        }
        
        if (PrimaryBottleneck?.Type == BottleneckType.MemoryBandwidth)
        {
            recommendations.Add("Optimize memory access patterns or use memory coalescing");
        }
        
        return recommendations;
    }

    private void UpdateUtilizationMetrics()
    {
        if (Executions.Count == 0) return;

        // Simple utilization calculation based on throughput
        var recentExecutions = Executions.TakeLast(100).Where(e => e.Success).ToArray();
        if (recentExecutions.Length == 0) return;

        AverageUtilizationPercentage = recentExecutions.Average(e => Math.Min(100, e.ThroughputGFLOPS / 10)); // Assuming 10 GFLOPS = 100% utilization
        PeakUtilizationPercentage = recentExecutions.Max(e => Math.Min(100, e.ThroughputGFLOPS / 10));
        IdleTimePercentage = 100 - AverageUtilizationPercentage;
    }
}

public class PerformanceTrends
{
    public TimeRange TimeRange { get; set; } = new();
    public TrendDirection ThroughputTrend { get; set; }
    public TrendDirection EfficiencyTrend { get; set; }
    public TrendDirection ExecutionTimeTrend { get; set; }
}

public class TimeRange
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
}

public enum TrendDirection
{
    Improving,
    Stable,
    Degrading
}

public class DeviceUtilizationAnalysis
{
    public required string DeviceId { get; set; }
    public double AverageUtilizationPercentage { get; set; }
    public double PeakUtilizationPercentage { get; set; }
    public double IdleTimePercentage { get; set; }
    public double BottleneckSeverity { get; set; }
    public List<string> RecommendedOptimizations { get; set; } = new();
}

public class KernelCharacteristics
{
    public bool IsMemoryBound { get; set; }
    public bool IsComputeBound { get; set; }
    public double AverageThroughput { get; set; }
    public double AverageMemoryBandwidth { get; set; }
}