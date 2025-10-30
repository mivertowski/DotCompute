// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel.Enums;

namespace DotCompute.Generators.Kernel.Attributes;

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
    /// <value>
    /// A combination of <see cref="KernelBackends"/> flags indicating
    /// which execution backends this kernel can target.
    /// </value>
    public KernelBackends Backends { get; set; } = KernelBackends.CPU;

    /// <summary>
    /// Gets or sets the preferred vector size for SIMD operations.
    /// Default is 8 (256-bit vectors).
    /// </summary>
    /// <value>
    /// The number of elements that should be processed together in SIMD operations.
    /// Common values are 4 (128-bit), 8 (256-bit), or 16 (512-bit vectors).
    /// </value>
    /// <remarks>
    /// This hint is used by the code generator to optimize vectorization.
    /// The actual vector size used may vary based on target hardware capabilities.
    /// </remarks>
    public int VectorSize { get; set; } = 8;

    /// <summary>
    /// Gets or sets whether this kernel should use parallel execution.
    /// Default is true.
    /// </summary>
    /// <value>
    /// <c>true</c> if the kernel should be executed in parallel across multiple threads;
    /// <c>false</c> for sequential execution.
    /// </value>
    /// <remarks>
    /// When enabled, the kernel will be divided into work items that can be
    /// executed concurrently. This is typically beneficial for data-parallel operations.
    /// </remarks>
    public bool IsParallel { get; set; } = true;

    /// <summary>
    /// Gets or sets the grid dimensions for GPU execution.
    /// </summary>
    /// <value>
    /// An array specifying the number of thread blocks in each dimension.
    /// Can be 1D, 2D, or 3D. Null indicates automatic grid size calculation.
    /// </value>
    /// <remarks>
    /// This property is only relevant for GPU backends (CUDA, OpenCL, Metal).
    /// The grid defines the overall problem size in terms of thread blocks.
    /// </remarks>
    public int[]? GridDimensions { get; set; }

    /// <summary>
    /// Gets or sets the block dimensions for GPU execution.
    /// </summary>
    /// <value>
    /// An array specifying the number of threads per block in each dimension.
    /// Can be 1D, 2D, or 3D. Null indicates automatic block size selection.
    /// </value>
    /// <remarks>
    /// This property is only relevant for GPU backends (CUDA, OpenCL, Metal).
    /// The block size affects memory access patterns and occupancy.
    /// </remarks>
    public int[]? BlockDimensions { get; set; }

    /// <summary>
    /// Gets or sets optimization hints for the compiler.
    /// </summary>
    /// <value>
    /// A combination of <see cref="OptimizationHints"/> flags that guide
    /// the code generator's optimization strategy.
    /// </value>
    /// <remarks>
    /// These hints help the compiler make informed decisions about optimizations
    /// such as loop unrolling, vectorization, and inlining.
    /// </remarks>
    public OptimizationHints Optimizations { get; set; } = OptimizationHints.None;

    /// <summary>
    /// Gets or sets memory access pattern hints.
    /// </summary>
    /// <value>
    /// The expected memory access pattern for this kernel, which helps
    /// optimize memory layout and prefetching strategies.
    /// </value>
    /// <remarks>
    /// Different access patterns benefit from different optimization strategies.
    /// For example, sequential access enables efficient prefetching, while
    /// random access may benefit from cache optimization.
    /// </remarks>
    public MemoryAccessPattern MemoryPattern { get; set; } = MemoryAccessPattern.Sequential;
}
