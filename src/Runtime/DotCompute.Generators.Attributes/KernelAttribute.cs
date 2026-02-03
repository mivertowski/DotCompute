// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Generators;

/// <summary>
/// Marks a method as a compute kernel that will be compiled for multiple backends.
/// </summary>
/// <remarks>
/// This attribute provides configuration options for kernel compilation, including
/// backend selection, SIMD vectorization settings, parallelization control,
/// GPU execution parameters, and optimization hints.
/// </remarks>
/// <example>
/// <code>
/// [Kernel(Backends = KernelBackends.CPU | KernelBackends.CUDA, VectorSize = 8)]
/// public static void MyKernel(Span&lt;float&gt; input, Span&lt;float&gt; output)
/// {
///     // Kernel implementation
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class KernelAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the backends this kernel supports.
    /// Default is CPU only.
    /// </summary>
    public KernelBackends Backends { get; set; } = KernelBackends.CPU;

    /// <summary>
    /// Gets or sets the preferred vector size for SIMD operations.
    /// Default is 8 (256-bit vectors).
    /// </summary>
    public int VectorSize { get; set; } = 8;

    /// <summary>
    /// Gets or sets whether this kernel should use parallel execution.
    /// Default is true.
    /// </summary>
    public bool IsParallel { get; set; } = true;

    /// <summary>
    /// Gets or sets the grid dimensions for GPU execution.
    /// Null indicates automatic grid size calculation.
    /// </summary>
    public int[]? GridDimensions { get; set; }

    /// <summary>
    /// Gets or sets the block dimensions for GPU execution.
    /// Null indicates automatic block size selection.
    /// </summary>
    public int[]? BlockDimensions { get; set; }

    /// <summary>
    /// Gets or sets optimization hints for the compiler.
    /// </summary>
    public OptimizationHints Optimizations { get; set; } = OptimizationHints.None;

    /// <summary>
    /// Gets or sets memory access pattern hints.
    /// </summary>
    public MemoryAccessPattern MemoryPattern { get; set; } = MemoryAccessPattern.Sequential;

    /// <summary>
    /// Gets or sets whether this kernel uses GPU thread barriers for synchronization.
    /// Default is false.
    /// </summary>
    public bool UseBarriers { get; set; }

    /// <summary>
    /// Gets or sets the synchronization scope for barriers used in this kernel.
    /// Default is ThreadBlock.
    /// </summary>
    public BarrierScope BarrierScope { get; set; } = BarrierScope.ThreadBlock;

    /// <summary>
    /// Gets or sets the expected number of threads participating in barrier synchronization.
    /// Default is 0 (automatic based on block/warp size).
    /// </summary>
    public int BarrierCapacity { get; set; }

    /// <summary>
    /// Gets or sets the memory consistency model for this kernel's memory operations.
    /// Default is Relaxed (no ordering guarantees).
    /// </summary>
    public MemoryConsistencyModel MemoryConsistency { get; set; } = MemoryConsistencyModel.Relaxed;

    /// <summary>
    /// Gets or sets whether to enable causal memory ordering (release-acquire semantics).
    /// Default is false.
    /// </summary>
    public bool EnableCausalOrdering { get; set; }
}
