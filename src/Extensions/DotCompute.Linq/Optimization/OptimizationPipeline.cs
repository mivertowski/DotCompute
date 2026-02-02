// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using DotCompute.Core.Telemetry;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Implements a pipeline of optimization passes that transform operation graphs.
/// </summary>
/// <remarks>
/// Phase 6: Query Optimization - Production implementation.
/// The pipeline chains multiple optimizers in sequence:
/// 1. Query Optimizer (cost-based optimization)
/// 2. Kernel Fusion Optimizer (reduce kernel launches)
/// 3. Memory Optimizer (cache and coalescing)
/// Each pass can inspect and transform the operation graph.
/// </remarks>
public sealed class OptimizationPipeline : IOptimizationPipeline
{
    private readonly ILogger<OptimizationPipeline> _logger;
    private readonly List<IOptimizer> _optimizers = new();
    private readonly Dictionary<string, TimeSpan> _optimizerTimes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OptimizationPipeline"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    public OptimizationPipeline(ILogger<OptimizationPipeline> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes an operation graph through all registered optimizers.
    /// </summary>
    /// <param name="graph">The operation graph to process.</param>
    /// <returns>The optimized operation graph.</returns>
    /// <exception cref="ArgumentNullException">Thrown when graph is null.</exception>
    public OperationGraph Process(OperationGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        // Record experimental feature usage
        ExperimentalFeatureTelemetry.RecordUsage(
            "DOTCOMPUTE0004",
            "LINQ Optimization Interfaces",
            context: $"OptimizationPipeline.Process with {_optimizers.Count} optimizers");

        if (_optimizers.Count == 0)
        {
            _logger.LogWarning("No optimizers registered in pipeline, returning graph unchanged");
            return graph;
        }

        _logger.LogInformation("Starting optimization pipeline with {OptimizerCount} optimizers on {OperationCount} operations",
            _optimizers.Count, graph.Operations.Count);

        var currentGraph = graph;
        var overallStopwatch = Stopwatch.StartNew();
        var optimizationResults = new List<OptimizationPassResult>();

        for (var i = 0; i < _optimizers.Count; i++)
        {
            var optimizer = _optimizers[i];
            var optimizerName = optimizer.GetType().Name;

            _logger.LogDebug("Running optimizer {Index}/{Total}: {OptimizerName}",
                i + 1, _optimizers.Count, optimizerName);

            var stopwatch = Stopwatch.StartNew();
            var beforeOperationCount = currentGraph.Operations.Count;

            try
            {
                // Run the optimizer
                var optimizedGraph = optimizer.Optimize(currentGraph);

                stopwatch.Stop();
                var elapsed = stopwatch.Elapsed;

                // Record timing
                _optimizerTimes[optimizerName] = elapsed;

                // Validate the result
                if (optimizedGraph == null)
                {
                    _logger.LogWarning("Optimizer {OptimizerName} returned null, using previous graph",
                        optimizerName);
                    optimizedGraph = currentGraph;
                }

                var afterOperationCount = optimizedGraph.Operations.Count;
                var reduction = beforeOperationCount - afterOperationCount;

                var result = new OptimizationPassResult
                {
                    OptimizerName = optimizerName,
                    ExecutionTime = elapsed,
                    OperationsBefore = beforeOperationCount,
                    OperationsAfter = afterOperationCount,
                    OperationsReduced = reduction,
                    Success = true
                };

                optimizationResults.Add(result);

                _logger.LogInformation("Optimizer {OptimizerName} completed in {Time:F2}ms: " +
                    "operations {Before} → {After} ({Reduction} reduced)",
                    optimizerName, elapsed.TotalMilliseconds, beforeOperationCount, afterOperationCount, reduction);

                currentGraph = optimizedGraph;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "Optimizer {OptimizerName} failed after {Time:F2}ms, skipping",
                    optimizerName, stopwatch.Elapsed.TotalMilliseconds);

                var result = new OptimizationPassResult
                {
                    OptimizerName = optimizerName,
                    ExecutionTime = stopwatch.Elapsed,
                    OperationsBefore = beforeOperationCount,
                    OperationsAfter = beforeOperationCount,
                    OperationsReduced = 0,
                    Success = false,
                    Error = ex
                };

                optimizationResults.Add(result);

                // Continue with the graph from before this optimizer
                // Don't let one optimizer failure break the entire pipeline
            }
        }

        overallStopwatch.Stop();

        // Log summary
        var totalOperationsReduced = graph.Operations.Count - currentGraph.Operations.Count;
        var reductionPercentage = totalOperationsReduced / (double)graph.Operations.Count;

        _logger.LogInformation("Optimization pipeline completed in {TotalTime:F2}ms: " +
            "operations {Before} → {After} ({Reduction} reduced, {Percentage:P1})",
            overallStopwatch.Elapsed.TotalMilliseconds,
            graph.Operations.Count,
            currentGraph.Operations.Count,
            totalOperationsReduced,
            reductionPercentage);

        // Log individual optimizer timings
        foreach (var result in optimizationResults)
        {
            _logger.LogDebug("  {OptimizerName}: {Time:F2}ms, reduced {Count} operations",
                result.OptimizerName, result.ExecutionTime.TotalMilliseconds, result.OperationsReduced);
        }

        return currentGraph;
    }

    /// <summary>
    /// Adds an optimizer to the pipeline.
    /// </summary>
    /// <param name="optimizer">The optimizer to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when optimizer is null.</exception>
    public void AddOptimizer(IOptimizer optimizer)
    {
        ArgumentNullException.ThrowIfNull(optimizer);

        _optimizers.Add(optimizer);

        _logger.LogDebug("Added optimizer {OptimizerName} to pipeline (total: {Count})",
            optimizer.GetType().Name, _optimizers.Count);
    }

    /// <summary>
    /// Gets the current number of registered optimizers.
    /// </summary>
    public int OptimizerCount => _optimizers.Count;

    /// <summary>
    /// Gets the execution times for each optimizer.
    /// </summary>
    public IReadOnlyDictionary<string, TimeSpan> GetOptimizerTimes()
    {
        return _optimizerTimes;
    }

    /// <summary>
    /// Clears all registered optimizers from the pipeline.
    /// </summary>
    public void Clear()
    {
        _optimizers.Clear();
        _optimizerTimes.Clear();
        _logger.LogDebug("Cleared all optimizers from pipeline");
    }

    /// <summary>
    /// Removes a specific optimizer from the pipeline.
    /// </summary>
    /// <param name="optimizer">The optimizer to remove.</param>
    /// <returns>True if the optimizer was removed; false if it was not found.</returns>
    public bool RemoveOptimizer(IOptimizer optimizer)
    {
        ArgumentNullException.ThrowIfNull(optimizer);

        var removed = _optimizers.Remove(optimizer);

        if (removed)
        {
            _logger.LogDebug("Removed optimizer {OptimizerName} from pipeline",
                optimizer.GetType().Name);
        }

        return removed;
    }
}

/// <summary>
/// Represents the result of a single optimization pass in the pipeline.
/// </summary>
internal sealed class OptimizationPassResult
{
    public required string OptimizerName { get; init; }
    public required TimeSpan ExecutionTime { get; init; }
    public required int OperationsBefore { get; init; }
    public required int OperationsAfter { get; init; }
    public required int OperationsReduced { get; init; }
    public required bool Success { get; init; }
    public Exception? Error { get; init; }
}
