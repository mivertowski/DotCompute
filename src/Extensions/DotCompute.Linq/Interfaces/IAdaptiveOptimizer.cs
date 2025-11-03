using System;
using System.Collections.ObjectModel;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.Interfaces;

/// <summary>
/// Interface for adaptive optimization that learns from execution history.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// Uses machine learning to predict optimal backend and strategy.
/// </remarks>
public interface IAdaptiveOptimizer
{
    /// <summary>
    /// Selects the optimal compute backend based on workload characteristics.
    /// </summary>
    /// <param name="workload">The workload characteristics to analyze.</param>
    /// <returns>The recommended compute backend.</returns>
    public ComputeBackend SelectBackend(WorkloadCharacteristics workload);

    /// <summary>
    /// Trains the optimizer using historical execution data.
    /// </summary>
    /// <param name="history">The execution history to learn from.</param>
    public void TrainFromHistory(ExecutionHistory history);

    /// <summary>
    /// Predicts the optimal optimization strategy for an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to analyze.</param>
    /// <returns>The recommended optimization strategy.</returns>
    public OptimizationStrategy PredictOptimalStrategy(OperationGraph graph);
}

/// <summary>
/// Contains historical execution data for training adaptive optimizers.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// </remarks>
public class ExecutionHistory
{
    /// <summary>
    /// Gets the collection of execution records.
    /// </summary>
    public Collection<ExecutionRecord> Records { get; init; } = new();

    /// <summary>
    /// Gets the timestamp when this history was last updated.
    /// </summary>
    public DateTime LastUpdated { get; init; }
}

/// <summary>
/// Represents a single execution record in the execution history.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// </remarks>
public class ExecutionRecord
{
    /// <summary>
    /// Gets the workload characteristics for this execution.
    /// </summary>
    public WorkloadCharacteristics Workload { get; init; } = new();

    /// <summary>
    /// Gets the compute backend that was used.
    /// </summary>
    public ComputeBackend UsedBackend { get; init; }

    /// <summary>
    /// Gets the actual execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets a value indicating whether the execution was successful.
    /// </summary>
    public bool WasSuccessful { get; init; }

    /// <summary>
    /// Gets the timestamp when this execution occurred.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the optimization strategy that was used.
    /// </summary>
    public OptimizationStrategy Strategy { get; init; }
}
