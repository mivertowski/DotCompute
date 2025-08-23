// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Enums;

/// <summary>
/// Defines the different types of performance recommendations that can be made for pipeline optimization.
/// Each type represents a specific optimization strategy with proven effectiveness.
/// </summary>
public enum RecommendationType
{
    /// <summary>
    /// Enable kernel fusion to combine multiple kernels into fewer launches.
    /// Reduces kernel launch overhead and improves data locality.
    /// </summary>
    KernelFusion,

    /// <summary>
    /// Adjust work group size for better resource utilization.
    /// Optimizes occupancy and parallel execution efficiency.
    /// </summary>
    WorkGroupSize,

    /// <summary>
    /// Optimize memory layout for better cache performance and coalescing.
    /// Improves memory bandwidth utilization and reduces latency.
    /// </summary>
    MemoryLayout,

    /// <summary>
    /// Reduce synchronization points to minimize overhead.
    /// Enables more parallel execution and reduces waiting time.
    /// </summary>
    ReduceSynchronization,

    /// <summary>
    /// Use memory pooling to reduce allocation overhead.
    /// Avoids frequent allocation and deallocation of memory buffers.
    /// </summary>
    MemoryPooling,

    /// <summary>
    /// Parallelize sequential stages to improve throughput.
    /// Enables concurrent execution of independent pipeline stages.
    /// </summary>
    Parallelization,

    /// <summary>
    /// Cache intermediate results to avoid recomputation.
    /// Reduces redundant work and improves overall efficiency.
    /// </summary>
    Caching
}