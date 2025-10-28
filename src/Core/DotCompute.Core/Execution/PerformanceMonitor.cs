// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

using DotCompute.Core.Execution.Metrics;
using ExecutionStrategyType = DotCompute.Abstractions.Types.ExecutionStrategyType;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Core.Execution.Models;

// Type alias to resolve ambiguity between different BottleneckType enums
using AbsTrendDirection = DotCompute.Abstractions.Types.TrendDirection;

namespace DotCompute.Core.Execution
{

    /// <summary>
    /// Monitors and analyzes parallel execution performance with machine learning-based optimization.
    /// </summary>
    public sealed partial class PerformanceMonitor(ILogger logger) : IDisposable
    {
        #region LoggerMessage Delegates

        [LoggerMessage(EventId = 10801, Level = MsLogLevel.Trace, Message = "Recorded kernel execution: {KernelName} on {DeviceId}, Time={ExecutionTimeMs:F2}ms, Throughput={ThroughputGFLOPS:F2} GFLOPS")]
        private static partial void LogKernelExecutionRecorded(ILogger logger, string kernelName, string deviceId, double executionTimeMs, double throughputGFLOPS);

        #endregion

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

            LogKernelExecutionRecorded(_logger, kernelName, deviceId, executionTimeMs, throughputGFLOPS);
        }


        /// <summary>
        /// Gets current performance metrics.
        /// </summary>
#pragma warning disable CA1024 // Use properties where appropriate - Method creates new object with dictionary copies
        public ParallelExecutionMetrics GetCurrentMetrics()
#pragma warning restore CA1024
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
                var emptyAnalysis = new ParallelExecutionAnalysis
                {
                    OverallRating = 5.0,
                    RecommendedStrategy = ExecutionStrategyType.Single
                };
                emptyAnalysis.OptimizationRecommendations.Add("No execution data available for analysis.");
                return emptyAnalysis;
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

            var executionTrend = PerformanceAnalyzer.AnalyzeTrends(relevantExecutions);

            // Convert ExecutionPerformanceTrend to PerformanceTrends
            return new PerformanceTrends
            {
                ExecutionTimeTrend = new TrendAnalysis { Direction = ConvertToDebugTrendDirection(ConvertTrendDirection(executionTrend.EfficiencyTrend)) },
                ThroughputTrend = new TrendAnalysis { Direction = ConvertToDebugTrendDirection(ConvertTrendDirection(executionTrend.ThroughputTrend)) },
                MemoryUsageTrend = new TrendAnalysis { Direction = ConvertToDebugTrendDirection(ConvertTrendDirection(executionTrend.EfficiencyTrend)) },
                OverallTrend = ConvertToDebugTrendDirection(ConvertTrendDirection(executionTrend.EfficiencyTrend)),
                DataPoints = relevantExecutions.Length,
                TimeWindow = timeWindow
            };
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
                    RecommendedOptimizations = (IList<string>)profile.GetOptimizationRecommendations()
                };
            }

            return analysis;
        }

        /// <summary>
        /// Clears all performance history and resets metrics.
        /// </summary>
        public void Reset()
        {
            while (_executionHistory.TryDequeue(out _))
            {
            }

            _kernelProfiles.Clear();
            _deviceProfiles.Clear();

            lock (_metricsLock)
            {
                _currentMetrics = new ParallelExecutionMetrics();
            }

            _logger.LogInfoMessage("Performance monitor reset");
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

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

        private static AbsTrendDirection ConvertTrendDirection(TrendDirection trend)
        {
            return trend switch
            {
                TrendDirection.Improving => AbsTrendDirection.Improving,
                TrendDirection.Stable => AbsTrendDirection.Stable,
                TrendDirection.Degrading => AbsTrendDirection.Degrading,
                _ => AbsTrendDirection.Unknown
            };
        }

        private static DotCompute.Abstractions.Debugging.TrendDirection ConvertToDebugTrendDirection(AbsTrendDirection trend)
        {
            return trend switch
            {
                AbsTrendDirection.Improving => DotCompute.Abstractions.Debugging.TrendDirection.Improving,
                AbsTrendDirection.Stable => DotCompute.Abstractions.Debugging.TrendDirection.Stable,
                AbsTrendDirection.Degrading => DotCompute.Abstractions.Debugging.TrendDirection.Degrading,
                _ => DotCompute.Abstractions.Debugging.TrendDirection.Unknown
            };
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
}
