// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Interfaces;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Implements adaptive optimization that learns from execution history.
/// </summary>
/// <remarks>
/// Phase 8 Preview: Advanced Optimization - Basic implementation for test compatibility.
/// This is a simplified version that will be enhanced in Phase 8 with:
/// - ML.NET integration for backend selection
/// - Neural network-based performance prediction
/// - Advanced pattern recognition
/// - Cross-session learning
/// </remarks>
public sealed class AdaptiveOptimizer : IAdaptiveOptimizer
{
    private readonly ILogger<AdaptiveOptimizer> _logger;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _performanceHistory = new();
    private readonly ConcurrentDictionary<string, List<OptimizationResult>> _strategyResults = new();
    private readonly object _lockObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveOptimizer"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    public AdaptiveOptimizer(ILogger<AdaptiveOptimizer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Selects the optimal compute backend based on workload characteristics.
    /// </summary>
    /// <param name="workload">The workload characteristics to analyze.</param>
    /// <returns>The recommended compute backend.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workload is null.</exception>
    public ComputeBackend SelectBackend(WorkloadCharacteristics workload)
    {
        ArgumentNullException.ThrowIfNull(workload);

        _logger.LogDebug("Selecting optimal backend for workload: Size={Size}, Intensity={Intensity}",
            workload.DataSize, workload.ComputeIntensity);

        // Use historical data if available
        var historicalBackend = GetHistoricalBestBackend(workload);
        if (historicalBackend.HasValue)
        {
            _logger.LogInformation("Selected backend {Backend} based on historical performance",
                historicalBackend.Value);
            return historicalBackend.Value;
        }

        // Use heuristics for new workloads
        var backend = SelectBackendByHeuristic(workload);

        _logger.LogInformation("Selected backend {Backend} based on heuristics", backend);
        return backend;
    }

    /// <summary>
    /// Trains the optimizer using historical execution data.
    /// </summary>
    /// <param name="history">The execution history to learn from.</param>
    /// <exception cref="ArgumentNullException">Thrown when history is null.</exception>
    public void TrainFromHistory(ExecutionHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);

        _logger.LogInformation("Training adaptive optimizer from {RecordCount} execution records",
            history.Records.Count);

        lock (_lockObject)
        {
            foreach (var record in history.Records)
            {
                // Create a unique key for this workload pattern
                var workloadKey = CreateWorkloadKey(record.Workload);

                // Store the execution record for future reference
                var metrics = new PerformanceMetrics
                {
                    ExecutionTime = TimeSpan.FromMilliseconds(record.ExecutionTimeMs),
                    Success = record.WasSuccessful,
                    ThroughputPerSecond = record.Workload.DataSize / (record.ExecutionTimeMs / 1000.0)
                };

                _performanceHistory[workloadKey] = metrics;

                _logger.LogTrace("Recorded performance for workload {WorkloadKey}: {ExecutionTime}ms",
                    workloadKey, record.ExecutionTimeMs);
            }
        }

        _logger.LogInformation("Training completed. Performance history now contains {Count} records",
            _performanceHistory.Count);
    }

    /// <summary>
    /// Predicts the optimal optimization strategy for an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to analyze.</param>
    /// <returns>The recommended optimization strategy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when graph is null.</exception>
    public OptimizationStrategy PredictOptimalStrategy(OperationGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        _logger.LogDebug("Predicting optimal strategy for graph with {OperationCount} operations",
            graph.Operations.Count);

        // Analyze graph characteristics
        var operationCount = graph.Operations.Count;
        var hasComplexOps = graph.Operations.Any(op =>
            op.Type == OperationType.Join ||
            op.Type == OperationType.GroupBy ||
            op.Type == OperationType.OrderBy);

        // Select strategy based on complexity
        var strategy = (operationCount, hasComplexOps) switch
        {
            ( <= 5, false) => OptimizationStrategy.Conservative,
            ( > 10, true) => OptimizationStrategy.Aggressive,
            _ => OptimizationStrategy.Balanced
        };

        _logger.LogInformation("Predicted strategy {Strategy} for graph", strategy);
        return strategy;
    }

    /// <summary>
    /// Asynchronously optimizes a workload using the specified strategy.
    /// </summary>
    /// <param name="workload">The workload characteristics.</param>
    /// <param name="initialStrategy">The initial optimization strategy.</param>
    /// <returns>An optimization result.</returns>
    public Task<OptimizationResult> OptimizeAsync(
        WorkloadCharacteristics workload,
        OptimizationStrategy initialStrategy)
    {
        ArgumentNullException.ThrowIfNull(workload);

        _logger.LogInformation("Optimizing workload asynchronously with {Strategy} strategy",
            initialStrategy);

        // Select optimal backend
        var backend = SelectBackend(workload);

        // Adjust strategy based on historical performance
        var strategy = AdjustStrategy(initialStrategy, workload);

        var result = new OptimizationResult
        {
            StrategyId = Guid.NewGuid().ToString(),
            Strategy = strategy,
            Backend = backend,
            EstimatedExecutionTimeMs = EstimateExecutionTime(workload, backend),
            Success = true
        };

        // Store result for learning
        lock (_lockObject)
        {
            var key = strategy.ToString();
            if (!_strategyResults.TryGetValue(key, out var results))
            {
                results = new List<OptimizationResult>();
                _strategyResults[key] = results;
            }
            results.Add(result);
        }

        _logger.LogInformation("Optimization completed: Strategy={Strategy}, Backend={Backend}, " +
            "EstimatedTime={Time}ms",
            strategy, backend, result.EstimatedExecutionTimeMs);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Records performance feedback for a strategy.
    /// </summary>
    /// <param name="strategyId">The strategy identifier.</param>
    /// <param name="metrics">The performance metrics.</param>
    /// <returns>A completed task.</returns>
    public Task RecordPerformanceAsync(string strategyId, PerformanceMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(metrics);

        _logger.LogDebug("Recording performance for strategy {StrategyId}: {ExecutionTime}ms",
            strategyId, metrics.ExecutionTime.TotalMilliseconds);

        // Store performance metrics
        _performanceHistory[strategyId] = metrics;

        _logger.LogInformation("Performance recorded: ExecutionTime={Time}ms, Throughput={Throughput}/s, " +
            "Success={Success}",
            metrics.ExecutionTime.TotalMilliseconds, metrics.ThroughputPerSecond, metrics.Success);

        return Task.CompletedTask;
    }

    #region Private Helper Methods

    /// <summary>
    /// Gets the historically best backend for a workload pattern.
    /// </summary>
    private ComputeBackend? GetHistoricalBestBackend(WorkloadCharacteristics workload)
    {
        var workloadKey = CreateWorkloadKey(workload);

        if (_performanceHistory.TryGetValue(workloadKey, out var metrics))
        {
            // In a full implementation, this would look at backend-specific performance
            // For now, use heuristic based on success rate
            return metrics.Success ? ComputeBackend.Cuda : ComputeBackend.CpuSimd;
        }

        return null;
    }

    /// <summary>
    /// Selects a backend using heuristic rules.
    /// </summary>
    private ComputeBackend SelectBackendByHeuristic(WorkloadCharacteristics workload)
    {
        // GPU is better for large datasets with high compute intensity
        if (workload.DataSize > 100_000 && workload.ComputeIntensity == ComputeIntensity.High)
        {
            return ComputeBackend.Cuda;
        }

        // CPU is better for small datasets or low compute intensity
        if (workload.DataSize < 10_000 || workload.ComputeIntensity == ComputeIntensity.Low)
        {
            return ComputeBackend.CpuSimd;
        }

        // Balanced choice for medium workloads
        return workload.DataSize > 50_000 ? ComputeBackend.Cuda : ComputeBackend.CpuSimd;
    }

    /// <summary>
    /// Creates a unique key for a workload pattern.
    /// </summary>
    private string CreateWorkloadKey(WorkloadCharacteristics workload)
    {
        return $"{workload.DataSize / 10_000}_{workload.ComputeIntensity}_{workload.MemoryPattern}";
    }

    /// <summary>
    /// Adjusts the optimization strategy based on historical performance.
    /// </summary>
    private OptimizationStrategy AdjustStrategy(
        OptimizationStrategy initialStrategy,
        WorkloadCharacteristics workload)
    {
        // Check if we have historical data for this strategy
        if (_strategyResults.TryGetValue(initialStrategy.ToString(), out var results))
        {
            // If the strategy has consistently failed, try a different one
            var successRate = results.Count(r => r.Success) / (double)results.Count;
            if (successRate < 0.5)
            {
                _logger.LogWarning("Strategy {Strategy} has low success rate ({Rate:P1}), adjusting",
                    initialStrategy, successRate);

                return initialStrategy switch
                {
                    OptimizationStrategy.Aggressive => OptimizationStrategy.Balanced,
                    OptimizationStrategy.Balanced => OptimizationStrategy.Conservative,
                    _ => initialStrategy
                };
            }
        }

        return initialStrategy;
    }

    /// <summary>
    /// Estimates execution time for a workload on a given backend.
    /// </summary>
    private double EstimateExecutionTime(WorkloadCharacteristics workload, ComputeBackend backend)
    {
        // Base time in milliseconds
        var baseTime = workload.DataSize / 10000.0; // 1ms per 10K elements

        // Adjust for compute intensity
        var intensityMultiplier = workload.ComputeIntensity switch
        {
            ComputeIntensity.Low => 0.5,
            ComputeIntensity.Medium => 1.0,
            ComputeIntensity.High => 2.0,
            _ => 1.0
        };

        // Adjust for backend
        var backendMultiplier = backend switch
        {
            ComputeBackend.Cuda => 0.1,  // GPU is ~10x faster
            ComputeBackend.CpuSimd => 1.0,
            _ => 1.0
        };

        return baseTime * intensityMultiplier * backendMultiplier;
    }

    /// <summary>
    /// Gets optimization statistics for monitoring.
    /// </summary>
    public IReadOnlyDictionary<string, List<OptimizationResult>> GetStrategyResults()
    {
        return _strategyResults;
    }

    /// <summary>
    /// Gets performance history for monitoring.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetPerformanceHistory()
    {
        return _performanceHistory;
    }

    #endregion
}
