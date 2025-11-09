// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Optimization;

/// <summary>
/// Optimization strategies for pipeline performance tuning.
/// </summary>
/// <remarks>
/// <para>
/// Different optimization strategies represent different tradeoffs between
/// performance dimensions: execution time, memory usage, throughput, latency, and energy.
/// </para>
/// <para>
/// Select strategy based on workload characteristics and performance goals.
/// Adaptive strategy automatically adjusts based on runtime profiling.
/// </para>
/// </remarks>
public enum OptimizationStrategy
{
    /// <summary>Conservative optimization focusing on reliability.</summary>
    /// <remarks>Prioritizes correctness and stability over raw performance.</remarks>
    Conservative,

    /// <summary>Balanced optimization between performance and reliability.</summary>
    /// <remarks>Default strategy providing good performance with safety guarantees.</remarks>
    Balanced,

    /// <summary>Aggressive optimization maximizing performance.</summary>
    /// <remarks>May sacrifice safety checks for maximum throughput.</remarks>
    Aggressive,

    /// <summary>Adaptive optimization based on runtime characteristics.</summary>
    /// <remarks>Learns from execution patterns and adjusts automatically.</remarks>
    Adaptive,

    /// <summary>Memory-focused optimization minimizing allocations.</summary>
    /// <remarks>Best for memory-constrained environments or large datasets.</remarks>
    MemoryOptimal,

    /// <summary>Throughput-focused optimization maximizing data processing rate.</summary>
    /// <remarks>Optimizes for batch processing and high-volume scenarios.</remarks>
    ThroughputOptimal,

    /// <summary>Latency-focused optimization minimizing response time.</summary>
    /// <remarks>Optimizes for interactive and real-time applications.</remarks>
    LatencyOptimal,

    /// <summary>Energy-efficient optimization minimizing power consumption.</summary>
    /// <remarks>Balances performance with power constraints for mobile/edge devices.</remarks>
    EnergyEfficient
}

/// <summary>
/// Context information for optimization operations.
/// </summary>
/// <remarks>
/// <para>
/// Provides the optimizer with performance goals, constraints, execution history,
/// hardware characteristics, and input data patterns.
/// </para>
/// <para>
/// Richer context enables more informed optimization decisions and better
/// performance outcomes.
/// </para>
/// </remarks>
public class OptimizationContext
{
    /// <summary>Performance goals for the optimization.</summary>
    public PerformanceGoals Goals { get; set; } = new();

    /// <summary>Constraints that must be respected during optimization.</summary>
    public OptimizationConstraints Constraints { get; set; } = new();

    // TODO: Define missing type
    /* <summary>Historical execution data for informed optimization.</summary>
    public IExecutionHistory? ExecutionHistory { get; set; } */

    // TODO: Define missing type
    /* <summary>Target hardware characteristics.</summary>
    public IHardwareProfile? HardwareProfile { get; set; } */

    // TODO: Define missing type
    /* <summary>Input data characteristics for optimization decisions.</summary>
    public IInputCharacteristics? InputCharacteristics { get; set; } */
}

/// <summary>
/// Performance goals for pipeline optimization.
/// </summary>
/// <remarks>
/// <para>
/// Defines target metrics and relative importance weights for different
/// performance aspects. Optimizer attempts to meet these goals while
/// respecting constraints.
/// </para>
/// <para>
/// Goals are aspirational - actual performance may differ based on
/// workload and system capabilities.
/// </para>
/// </remarks>
public class PerformanceGoals
{
    /// <summary>Target execution time for the pipeline.</summary>
    public TimeSpan? TargetExecutionTime { get; set; }

    /// <summary>Target throughput in items per second.</summary>
    public double? TargetThroughput { get; set; }

    /// <summary>Maximum acceptable memory usage.</summary>
    public long? MaxMemoryUsage { get; set; }

    /// <summary>Target CPU utilization percentage.</summary>
    public double? TargetCpuUtilization { get; set; }

    /// <summary>Target energy efficiency in operations per watt.</summary>
    public double? TargetEnergyEfficiency { get; set; }

    /// <summary>Relative importance weights for different performance aspects.</summary>
    public PerformanceWeights Weights { get; set; } = new();
}

/// <summary>
/// Weights for balancing different performance aspects during optimization.
/// </summary>
/// <remarks>
/// <para>
/// Weights should sum to 1.0 for meaningful interpretation.
/// Higher weight indicates higher importance for that performance dimension.
/// </para>
/// <para>
/// Default weights (0.4 execution time, 0.3 throughput, 0.2 memory, 0.1 energy)
/// reflect common priorities for general-purpose compute workloads.
/// </para>
/// </remarks>
public class PerformanceWeights
{
    /// <summary>Weight for execution time optimization (0.0 to 1.0).</summary>
    public double ExecutionTime { get; set; } = 0.4;

    /// <summary>Weight for memory usage optimization (0.0 to 1.0).</summary>
    public double MemoryUsage { get; set; } = 0.2;

    /// <summary>Weight for throughput optimization (0.0 to 1.0).</summary>
    public double Throughput { get; set; } = 0.3;

    /// <summary>Weight for energy efficiency optimization (0.0 to 1.0).</summary>
    public double EnergyEfficiency { get; set; } = 0.1;
}

/// <summary>
/// Constraints that limit optimization decisions.
/// </summary>
/// <remarks>
/// <para>
/// Constraints are hard limits that optimizer must respect.
/// Unlike goals (which are targets), constraints cannot be violated.
/// </para>
/// <para>
/// Overly restrictive constraints may prevent effective optimization.
/// Balance safety requirements with optimization freedom.
/// </para>
/// </remarks>
public class OptimizationConstraints
{
    /// <summary>Maximum execution time allowed.</summary>
    public TimeSpan? MaxExecutionTime { get; set; }

    /// <summary>Maximum memory that can be allocated.</summary>
    public long? MaxMemoryAllocation { get; set; }

    /// <summary>Maximum CPU cores that can be used.</summary>
    public int? MaxCpuCores { get; set; }

    /// <summary>Backends that are not allowed for execution.</summary>
    public HashSet<string> DisallowedBackends { get; } = [];

    /// <summary>Backends that must be used if available.</summary>
    public HashSet<string> RequiredBackends { get; } = [];

    /// <summary>Whether optimization can modify pipeline structure.</summary>
    /// <remarks>
    /// Structural changes include stage reordering, fusion, and decomposition.
    /// </remarks>
    public bool AllowStructuralChanges { get; set; } = true;

    /// <summary>Whether optimization can reorder independent operations.</summary>
    /// <remarks>
    /// Reordering can improve data locality but may affect debugging.
    /// </remarks>
    public bool AllowReordering { get; set; } = true;
}
