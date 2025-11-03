// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Defines strategies for parallelizing LINQ operations across different compute backends.
/// </summary>
/// <remarks>
/// The parallelization strategy determines how work is distributed and executed,
/// ranging from sequential execution to heterogeneous compute utilizing both CPU and GPU.
/// </remarks>
public enum ParallelizationStrategy
{
    /// <summary>
    /// Sequential execution with no parallelization.
    /// </summary>
    /// <remarks>
    /// Suitable for small datasets or operations where overhead of parallelization
    /// would exceed the benefits.
    /// </remarks>
    Sequential,

    /// <summary>
    /// Data-parallel execution using SIMD vectorization on CPU.
    /// </summary>
    /// <remarks>
    /// Utilizes AVX2/AVX-512 instructions for parallel processing of multiple data elements.
    /// Optimal for operations on contiguous memory with regular data access patterns.
    /// </remarks>
    DataParallel,

    /// <summary>
    /// Task-parallel execution using multiple CPU threads.
    /// </summary>
    /// <remarks>
    /// Distributes work across multiple CPU threads using Thread Pool.
    /// Suitable for operations with irregular workloads or non-uniform data access.
    /// </remarks>
    TaskParallel,

    /// <summary>
    /// GPU-parallel execution using CUDA or Metal compute shaders.
    /// </summary>
    /// <remarks>
    /// Offloads computation to GPU for massive parallelism.
    /// Optimal for large datasets with regular computation patterns
    /// and sufficient computational intensity to amortize transfer costs.
    /// </remarks>
    GpuParallel,

    /// <summary>
    /// Hybrid execution utilizing both CPU and GPU resources.
    /// </summary>
    /// <remarks>
    /// Dynamically partitions work between CPU and GPU based on
    /// workload characteristics and hardware capabilities.
    /// Suitable for complex pipelines with varied computational requirements.
    /// </remarks>
    Hybrid
}

/// <summary>
/// Provides extension methods for working with <see cref="ParallelizationStrategy"/>.
/// </summary>
public static class ParallelizationStrategyExtensions
{
    /// <summary>
    /// Determines whether the strategy requires GPU resources.
    /// </summary>
    /// <param name="strategy">The parallelization strategy to check.</param>
    /// <returns>
    /// <c>true</c> if the strategy requires GPU acceleration; otherwise, <c>false</c>.
    /// </returns>
    public static bool RequiresGpu(this ParallelizationStrategy strategy)
    {
        return strategy switch
        {
            ParallelizationStrategy.GpuParallel => true,
            ParallelizationStrategy.Hybrid => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines whether the strategy uses multiple CPU threads.
    /// </summary>
    /// <param name="strategy">The parallelization strategy to check.</param>
    /// <returns>
    /// <c>true</c> if the strategy uses multiple CPU threads; otherwise, <c>false</c>.
    /// </returns>
    public static bool UsesMultipleThreads(this ParallelizationStrategy strategy)
    {
        return strategy switch
        {
            ParallelizationStrategy.TaskParallel => true,
            ParallelizationStrategy.Hybrid => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines whether the strategy uses SIMD vectorization.
    /// </summary>
    /// <param name="strategy">The parallelization strategy to check.</param>
    /// <returns>
    /// <c>true</c> if the strategy uses SIMD vectorization; otherwise, <c>false</c>.
    /// </returns>
    public static bool UsesSimd(this ParallelizationStrategy strategy)
    {
        return strategy switch
        {
            ParallelizationStrategy.DataParallel => true,
            ParallelizationStrategy.Hybrid => true,
            _ => false
        };
    }
}
