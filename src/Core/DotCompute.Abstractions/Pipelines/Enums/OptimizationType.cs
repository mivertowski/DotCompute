// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Specifies the types of optimizations that can be applied to pipelines.
/// This is a flags enum allowing combination of multiple optimization types.
/// </summary>
[Flags]
public enum OptimizationType
{
    /// <summary>
    /// No optimizations applied.
    /// </summary>
    None = 0,

    /// <summary>
    /// Kernel fusion to combine multiple kernels into a single execution unit.
    /// Reduces memory transfers and improves cache locality.
    /// </summary>
    KernelFusion = 1 << 0,

    /// <summary>
    /// Memory access pattern optimization for better cache performance.
    /// Includes data layout transformations and memory coalescing.
    /// </summary>
    MemoryAccess = 1 << 1,

    /// <summary>
    /// Loop optimizations including unrolling, vectorization, and blocking.
    /// Improves instruction-level parallelism and memory locality.
    /// </summary>
    LoopOptimization = 1 << 2,

    /// <summary>
    /// Parallelization and concurrency optimizations.
    /// Identifies opportunities for parallel execution and load balancing.
    /// </summary>
    Parallelization = 1 << 3,

    /// <summary>
    /// Backend-specific optimizations for target compute devices.
    /// Leverages specific features of CUDA, CPU SIMD, or Metal backends.
    /// </summary>
    BackendSpecific = 1 << 4,

    /// <summary>
    /// Data layout optimization for better memory access patterns.
    /// Includes structure-of-arrays vs array-of-structures transformations.
    /// </summary>
    DataLayout = 1 << 5,

    /// <summary>
    /// Instruction scheduling and register allocation optimizations.
    /// Improves instruction throughput and reduces register pressure.
    /// </summary>
    InstructionScheduling = 1 << 6,

    /// <summary>
    /// Dead code elimination and unused variable removal.
    /// Reduces memory usage and improves cache efficiency.
    /// </summary>
    DeadCodeElimination = 1 << 7,

    /// <summary>
    /// Constant folding and propagation optimizations.
    /// Evaluates constant expressions at compile time.
    /// </summary>
    ConstantFolding = 1 << 8,

    /// <summary>
    /// Function inlining for reduced call overhead.
    /// Eliminates function call costs for small, frequently used functions.
    /// </summary>
    Inlining = 1 << 9,

    /// <summary>
    /// Mathematical expression simplification and strength reduction.
    /// Replaces expensive operations with cheaper equivalents.
    /// </summary>
    MathOptimization = 1 << 10,

    /// <summary>
    /// Memory pooling and allocation optimization.
    /// Reduces allocation overhead and memory fragmentation.
    /// </summary>
    MemoryPooling = 1 << 11,

    /// <summary>
    /// Pipeline stage reordering for better resource utilization.
    /// Minimizes idle time and maximizes throughput.
    /// </summary>
    StageReordering = 1 << 12,

    /// <summary>
    /// Branch prediction and conditional optimization.
    /// Improves performance for code with conditional branches.
    /// </summary>
    BranchOptimization = 1 << 13,

    /// <summary>
    /// Cache optimization for better data locality.
    /// Includes cache-aware tiling and prefetching strategies.
    /// </summary>
    CacheOptimization = 1 << 14,

    /// <summary>
    /// Vectorization for SIMD instruction utilization.
    /// Leverages vector processing units for data-parallel operations.
    /// </summary>
    Vectorization = 1 << 15,

    /// <summary>
    /// Parallel merging optimization for combining parallel execution stages.
    /// Merges independent parallel operations to improve resource utilization.
    /// </summary>
    ParallelMerging = 1 << 16,

    /// <summary>
    /// Comprehensive optimization applying all applicable optimizations.
    /// Includes all optimization types that are safe and beneficial.
    /// </summary>
    Comprehensive = KernelFusion | MemoryAccess | LoopOptimization | Parallelization |
                    BackendSpecific | DataLayout | InstructionScheduling | DeadCodeElimination |
                    ConstantFolding | Inlining | MathOptimization | MemoryPooling |
                    StageReordering | BranchOptimization | CacheOptimization | Vectorization | ParallelMerging,

    /// <summary>
    /// Conservative optimizations that are safe and unlikely to cause issues.
    /// Includes only optimizations with minimal risk of correctness problems.
    /// </summary>
    Conservative = DeadCodeElimination | ConstantFolding | MemoryPooling | CacheOptimization,

    /// <summary>
    /// Aggressive optimizations that may require careful validation.
    /// Includes optimizations that could potentially affect correctness.
    /// </summary>
    Aggressive = KernelFusion | LoopOptimization | Inlining | MathOptimization | StageReordering,

    /// <summary>
    /// Memory-focused optimizations for reducing memory usage and improving access patterns.
    /// </summary>
    MemoryFocused = MemoryAccess | DataLayout | MemoryPooling | CacheOptimization,

    /// <summary>
    /// Performance-focused optimizations for maximum execution speed.
    /// </summary>
    PerformanceFocused = Parallelization | Vectorization | BackendSpecific | InstructionScheduling,

    /// <summary>
    /// Size-focused optimizations for reducing memory footprint.
    /// </summary>
    SizeFocused = DeadCodeElimination | ConstantFolding | MemoryPooling
}
