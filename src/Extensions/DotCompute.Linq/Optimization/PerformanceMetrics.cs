// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Contains performance metrics for a compute operation execution.
/// </summary>
/// <remarks>
/// Phase 6: Query Optimization - Support class for adaptive optimization.
/// Used to record and analyze actual execution performance.
/// </remarks>
public sealed class PerformanceMetrics
{
    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets or sets the throughput in operations per second.
    /// </summary>
    public double ThroughputPerSecond { get; init; }

    /// <summary>
    /// Gets or sets the memory efficiency (0.0 to 1.0).
    /// </summary>
    public double MemoryEfficiency { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the execution was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// </summary>
    public long MemoryUsage { get; init; }

    /// <summary>
    /// Gets or sets the CPU utilization (0.0 to 1.0).
    /// </summary>
    public double CpuUtilization { get; init; }

    /// <summary>
    /// Gets or sets the GPU utilization (0.0 to 1.0).
    /// </summary>
    public double GpuUtilization { get; init; }
}

/// <summary>
/// Contains the result of an optimization operation.
/// </summary>
/// <remarks>
/// Phase 6: Query Optimization - Support class for adaptive optimization.
/// Used to return optimization results and decisions.
/// </remarks>
public sealed class OptimizationResult
{
    /// <summary>
    /// Gets or sets the unique identifier for this optimization strategy.
    /// </summary>
    public required string StrategyId { get; init; }

    /// <summary>
    /// Gets or sets the optimization strategy that was selected.
    /// </summary>
    public OptimizationStrategy Strategy { get; init; }

    /// <summary>
    /// Gets or sets the compute backend that was selected.
    /// </summary>
    public DotCompute.Linq.CodeGeneration.ComputeBackend Backend { get; init; }

    /// <summary>
    /// Gets or sets the optimized operation graph.
    /// </summary>
    public OperationGraph? OptimizedGraph { get; init; }

    /// <summary>
    /// Gets or sets the estimated execution time in milliseconds.
    /// </summary>
    public double EstimatedExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets or sets additional optimization metadata.
    /// </summary>
    public System.Collections.Generic.Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether optimization was successful.
    /// </summary>
    public bool Success { get; init; }
}
