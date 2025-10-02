// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels.Models;

/// <summary>
/// Execution plan for a compiled kernel.
/// </summary>
public sealed class KernelExecutionPlan
{
    /// <summary>
    /// Gets or sets the analysis.
    /// </summary>
    /// <value>The analysis.</value>
    public required KernelAnalysis Analysis { get; init; }
    /// <summary>
    /// Gets or sets the use vectorization.
    /// </summary>
    /// <value>The use vectorization.</value>
    public required bool UseVectorization { get; init; }
    /// <summary>
    /// Gets or sets the use parallelization.
    /// </summary>
    /// <value>The use parallelization.</value>
    public bool UseParallelization { get; init; }
    /// <summary>
    /// Gets or sets the vector width.
    /// </summary>
    /// <value>The vector width.</value>
    public required int VectorWidth { get; init; }
    /// <summary>
    /// Gets or sets the vectorization factor.
    /// </summary>
    /// <value>The vectorization factor.</value>
    public required int VectorizationFactor { get; init; }
    /// <summary>
    /// Gets or sets the work group size.
    /// </summary>
    /// <value>The work group size.</value>
    public required int WorkGroupSize { get; init; }
    /// <summary>
    /// Gets or sets the optimal thread count.
    /// </summary>
    /// <value>The optimal thread count.</value>
    public int OptimalThreadCount { get; init; }
    /// <summary>
    /// Gets or sets the memory prefetch distance.
    /// </summary>
    /// <value>The memory prefetch distance.</value>
    public required int MemoryPrefetchDistance { get; init; }
    /// <summary>
    /// Gets or sets the enable loop unrolling.
    /// </summary>
    /// <value>The enable loop unrolling.</value>
    public required bool EnableLoopUnrolling { get; init; }
    /// <summary>
    /// Gets or sets the instruction sets.
    /// </summary>
    /// <value>The instruction sets.</value>
    public required IReadOnlySet<string> InstructionSets { get; init; }
    /// <summary>
    /// Gets or sets the memory optimizations.
    /// </summary>
    /// <value>The memory optimizations.</value>
    public IReadOnlyList<string> MemoryOptimizations { get; init; } = [];
    /// <summary>
    /// Gets or sets the cache optimizations.
    /// </summary>
    /// <value>The cache optimizations.</value>
    public IReadOnlyList<string> CacheOptimizations { get; init; } = [];
}