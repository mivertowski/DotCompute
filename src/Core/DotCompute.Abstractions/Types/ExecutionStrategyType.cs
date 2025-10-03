// Copyright (c) 2025 DotCompute
// File created as part of build error elimination
// Defines execution strategy types for execution planning

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Defines the type of execution strategy for kernel and pipeline execution.
/// </summary>
public enum ExecutionStrategyType
{
    /// <summary>
    /// Sequential execution - tasks run one after another.
    /// </summary>
    Sequential = 0,

    /// <summary>
    /// Parallel execution - tasks run concurrently where possible.
    /// </summary>
    Parallel = 1,

    /// <summary>
    /// Data parallel execution - data is split across multiple execution units.
    /// </summary>
    DataParallel = 2,

    /// <summary>
    /// Model parallel execution - model is split across multiple devices.
    /// </summary>
    ModelParallel = 3,

    /// <summary>
    /// Pipeline parallel execution - different stages run on different devices.
    /// </summary>
    PipelineParallel = 4,

    /// <summary>
    /// Hybrid execution - combines multiple strategies adaptively.
    /// </summary>
    Hybrid = 5,

    /// <summary>
    /// Adaptive execution - strategy chosen based on workload characteristics.
    /// </summary>
    Adaptive = 6,

    /// <summary>
    /// Streaming execution - continuous processing of data streams.
    /// </summary>
    Streaming = 7,

    /// <summary>
    /// Batch execution - processes data in batches for efficiency.
    /// </summary>
    Batch = 8,

    /// <summary>
    /// Single device execution strategy.
    /// Executes the entire workload on a single computing device without parallelization.
    /// Best for small workloads or when device resources are limited.
    /// </summary>
#pragma warning disable CA1720 // Identifier contains type name - "Single" refers to single-device strategy, not the type
    Single = 9,
#pragma warning restore CA1720

    /// <summary>
    /// Dynamic work stealing for load balancing.
    /// Allows idle devices to steal work from busy devices to maintain optimal load distribution.
    /// Best for workloads with irregular or unpredictable execution times.
    /// </summary>
    WorkStealing = 10,

    /// <summary>
    /// Heterogeneous CPU+GPU execution.
    /// Coordinates execution across different types of computing devices (CPUs and GPUs).
    /// Maximizes utilization of all available computing resources.
    /// </summary>
    Heterogeneous = 11
}
