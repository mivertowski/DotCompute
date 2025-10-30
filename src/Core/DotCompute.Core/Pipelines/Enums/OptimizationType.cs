// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Enums;

/// <summary>
/// Defines the different types of optimizations that can be applied to pipeline execution.
/// Each type represents a specific optimization technique with measurable performance impact.
/// </summary>
public enum OptimizationType
{
    /// <summary>
    /// Kernel fusion optimization - combines multiple kernels into a single kernel.
    /// Reduces kernel launch overhead and improves data locality.
    /// </summary>
    KernelFusion,

    /// <summary>
    /// Memory pooling optimization - reuses memory allocations to reduce allocation overhead.
    /// Improves performance by avoiding frequent allocation and deallocation operations.
    /// </summary>
    MemoryPooling,

    /// <summary>
    /// Data layout optimization - reorganizes data structures for better memory access patterns.
    /// Improves cache utilization and memory bandwidth efficiency.
    /// </summary>
    DataLayoutOptimization,

    /// <summary>
    /// Loop unrolling optimization - reduces loop overhead by expanding loop bodies.
    /// Improves instruction-level parallelism and reduces branch overhead.
    /// </summary>
    LoopUnrolling,

    /// <summary>
    /// Vectorization optimization - uses SIMD instructions to process multiple data elements simultaneously.
    /// Significantly improves performance for data-parallel operations.
    /// </summary>
    Vectorization,

    /// <summary>
    /// Caching optimization - stores frequently accessed data in fast memory.
    /// Reduces memory latency and bandwidth requirements.
    /// </summary>
    Caching,

    /// <summary>
    /// Synchronization reduction - minimizes the need for thread synchronization.
    /// Improves parallel execution efficiency and reduces overhead.
    /// </summary>
    SynchronizationReduction,

    /// <summary>
    /// Work group size optimization - tunes the parallel execution configuration.
    /// Optimizes resource utilization and occupancy on parallel devices.
    /// </summary>
    WorkGroupOptimization,

    /// <summary>
    /// Parallel merging optimization - combines parallel execution paths efficiently.
    /// Improves performance by optimizing parallel task coordination and data merging.
    /// </summary>
    ParallelMerging
}
