// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Models.Device;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

using DotCompute.Core.Execution.Metrics;
using DotCompute.Core.Execution.Workload;
using DotCompute.Core.Execution.Pipeline;
using ExecutionStrategyType = DotCompute.Abstractions.Types.ExecutionStrategyType;
using DotCompute.Core.Execution.Plans;

// Type alias to resolve ambiguity between different BottleneckType enums
using AnalysisBottleneckType = DotCompute.Abstractions.Types.BottleneckType;

namespace DotCompute.Core.Execution
{

    /// <summary>
    /// Monitors and analyzes parallel execution performance with machine learning-based optimization.
    /// </summary>
    public sealed class PerformanceMonitor(ILogger logger) : IDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly ConcurrentQueue<ExecutionRecord> _executionHistory = new();
        private readonly ConcurrentDictionary<string, KernelPerformanceProfile> _kernelProfiles = new();
        private readonly ConcurrentDictionary<string, DevicePerformanceProfile> _deviceProfiles = new();
        private readonly Lock _metricsLock = new();
        private ParallelExecutionMetrics _currentMetrics = new();
        private bool _disposed;

        private const int MaxHistorySize = 10000;
        private const int AnalysisWindowSize = 100;

        /// <summary>
        /// Records a parallel execution result for analysis.
        /// </summary>
        public void RecordExecution(ParallelExecutionResult result)
        {
            var record = new ExecutionRecord
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Strategy = (ExecutionStrategyType)result.Strategy,
                Success = result.Success,
                TotalExecutionTimeMs = result.TotalExecutionTimeMs,
                ThroughputGFLOPS = result.ThroughputGFLOPS,
                MemoryBandwidthGBps = result.MemoryBandwidthGBps,
                EfficiencyPercentage = result.EfficiencyPercentage,
                DeviceResults = [.. result.DeviceResults],
                ErrorMessage = result.ErrorMessage
            };

            // Add to history
            _executionHistory.Enqueue(record);

            // Limit history size
            while (_executionHistory.Count > MaxHistorySize)
            {
                _ = _executionHistory.TryDequeue(out _);
            }

            // Update metrics
            UpdateMetrics(record);

            // Update device profiles
            UpdateDeviceProfiles(record);

            // Trigger analysis if we have enough data
            if (_executionHistory.Count % AnalysisWindowSize == 0)
            {
                _ = Task.Run(AnalyzePerformanceAsync);
            }

            _logger.LogDebugMessage($"Recorded execution: Strategy={result.Strategy}, Success={result.Success}, Time={result.TotalExecutionTimeMs}ms, Efficiency={result.EfficiencyPercentage}%");
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
                    RecommendedStrategy = (Types.ExecutionStrategyType)ExecutionStrategyType.Single,
                    OptimizationRecommendations = ["No execution data available for analysis."]
                };
            }

            return PerformanceAnalyzer.AnalyzePerformance(recentExecutions, [.. _deviceProfiles.Values]);
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

            return AdaptiveOptimizer.RecommendStrategy(
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

            return PerformanceAnalyzer.AnalyzeTrends(relevantExecutions);
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

            _logger.LogInfoMessage("Performance monitor reset");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Reset();
            _disposed = true;

            _logger.LogInfoMessage("Performance monitor disposed");
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
            return [.. _executionHistory
            .TakeLast(count)
            .OrderByDescending(e => e.Timestamp)];
        }

        private async Task AnalyzePerformanceAsync()
        {
            try
            {
                var recentExecutions = GetRecentExecutions(AnalysisWindowSize);
                if (recentExecutions.Length == 0)
                {
                    return;
                }

                // Perform background analysis
                var analysis = PerformanceAnalyzer.AnalyzePerformance(recentExecutions, [.. _deviceProfiles.Values]);

                // Log findings
                if (analysis.Bottlenecks.Count != 0)
                {
                    var primaryBottleneck = analysis.Bottlenecks.OrderByDescending(b => b.Severity).First();
                    _logger.LogInfoMessage($"Performance analysis: Primary bottleneck is {primaryBottleneck.Type} with severity {primaryBottleneck.Severity}");
                }

                if (analysis.OptimizationRecommendations.Count != 0)
                {
                    _logger.LogInfoMessage($"Performance recommendations: {string.Join("; ", analysis.OptimizationRecommendations)}");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during performance analysis");
            }
        }

        #endregion
    }

    /// <summary>
    /// Analyzes performance data and identifies bottlenecks.
    /// </summary>
    public class PerformanceAnalyzer
    {
        public PerformanceAnalyzer(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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
            analysis.RecommendedStrategy = (Types.ExecutionStrategyType)RecommendStrategy(executions);

            // Analyze device utilization
            analysis.DeviceUtilizationAnalysis = AnalyzeDeviceUtilization(deviceProfiles);

            return analysis;
        }

        public static PerformanceTrends AnalyzeTrends(ExecutionRecord[] executions)
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
            trends.ThroughputTrend = CalculateTrend([.. executions.Select(e => e.ThroughputGFLOPS)]);

            // Calculate efficiency trend
            trends.EfficiencyTrend = CalculateTrend([.. executions.Select(e => e.EfficiencyPercentage)]);

            // Calculate execution time trend
            trends.ExecutionTimeTrend = CalculateTrend([.. executions.Select(e => e.TotalExecutionTimeMs)]);

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

        private static IEnumerable<string> GenerateOptimizationRecommendations(ExecutionRecord[] executions, List<Analysis.BottleneckAnalysis> bottlenecks)
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

    /// <summary>
    /// Provides adaptive optimization recommendations based on machine learning.
    /// </summary>
    public class AdaptiveOptimizer
    {
        public AdaptiveOptimizer(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public static ExecutionStrategyRecommendation RecommendStrategy(
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
                Strategy = (Types.ExecutionStrategyType)recommendedStrategy,
                ConfidenceScore = confidenceScore,
                Reasoning = reasoning,
                ExpectedImprovementPercentage = EstimateImprovement(recommendedStrategy, recentExecutions)
            };
        }

        private static double GetStrategyPerformance(ExecutionStrategyType strategy, ExecutionRecord[] executions)
        {
            var strategyExecutions = executions.Where(e => e.Strategy == strategy && e.Success).ToArray();
            return strategyExecutions.Length > 0 ? strategyExecutions.Average(e => e.EfficiencyPercentage) : 0;
        }

        private static KernelCharacteristics AnalyzeKernelCharacteristics(KernelPerformanceProfile profile)
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

        private static double EstimateImprovement(ExecutionStrategyType strategy, ExecutionRecord[] executions)
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

        /// <summary>
        /// Estimates execution time for data parallel kernels.
        /// </summary>
        public static double EstimateExecutionTime(string kernelName, ComputeDeviceType[] deviceTypes, int dataSize)
        {
            // Simple estimation based on historical data or defaults
            const double baseTimeMs = 10.0; // Base execution time
            const double dataSizeMultiplier = 0.001; // Time per data element


            var deviceMultiplier = deviceTypes.Length > 0 ? 1.0 / deviceTypes.Length : 1.0;
            return (baseTimeMs + (dataSize * dataSizeMultiplier)) * deviceMultiplier;
        }

        /// <summary>
        /// Estimates execution time for model parallel execution.
        /// </summary>
        public static double EstimateModelParallelExecutionTime<T>(ModelParallelWorkload<T> workload, Dictionary<int, IAccelerator> layerAssignments) where T : unmanaged
        {
            // Estimate based on layer count and device distribution
            const double baseLayerTimeMs = 5.0;
            var totalLayers = workload.ModelLayers.Count;
            var deviceCount = layerAssignments.Values.Distinct().Count();


            return totalLayers * baseLayerTimeMs / Math.Max(1, deviceCount);
        }

        /// <summary>
        /// Estimates execution time for pipeline execution.
        /// </summary>
        public static double EstimatePipelineExecutionTime(List<PipelineStageDefinition> pipelineStages, MicrobatchConfiguration microbatchConfig)
        {
            // Estimate based on stage count and microbatch configuration
            const double baseStageTimeMs = 8.0;
            var stageCount = pipelineStages.Count;
            var microbatchOverhead = microbatchConfig.Count * 0.5; // Small overhead per microbatch


            return (stageCount * baseStageTimeMs) + microbatchOverhead;
        }

        /// <summary>
        /// Estimates processing time for a pipeline stage.
        /// </summary>
        public static double EstimateStageProcessingTime(PipelineStageDefinition stage, MicrobatchConfiguration microbatchConfig)
        {
            // Estimate based on stage complexity and microbatch size
            const double baseProcessingTimeMs = 5.0;
            var microbatchMultiplier = microbatchConfig.Count * 0.3;


            return baseProcessingTimeMs + microbatchMultiplier;
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
        public Dictionary<string, List<KernelExecution>> DeviceExecutions { get; set; } = [];

        public void AddExecution(string deviceId, double executionTimeMs, double throughputGFLOPS)
        {
            if (!DeviceExecutions.TryGetValue(deviceId, out var executions))
            {
                executions = [];
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
        public List<DeviceExecutionResult> Executions { get; set; } = [];

        public double AverageUtilizationPercentage { get; private set; }
        public double PeakUtilizationPercentage { get; private set; }
        public double IdleTimePercentage { get; private set; }
        public Analysis.BottleneckAnalysis? PrimaryBottleneck { get; private set; }

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
        public List<string> RecommendedOptimizations { get; set; } = [];
    }

    public class KernelCharacteristics
    {
        public bool IsMemoryBound { get; set; }
        public bool IsComputeBound { get; set; }
        public double AverageThroughput { get; set; }
        public double AverageMemoryBandwidth { get; set; }
    }
}
