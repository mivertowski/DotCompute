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

    /// <summary>
    /// Gets or sets whether this kernel uses GPU thread barriers for synchronization.
    /// Default is false.
    /// </summary>
    /// <value>
    /// <c>true</c> if the kernel requires explicit thread synchronization barriers;
    /// <c>false</c> if threads operate independently.
    /// </value>
    /// <remarks>
    /// <para>
    /// When enabled, the kernel can use barriers to synchronize threads within a scope
    /// (thread block, warp, or grid). Barriers ensure all threads reach a synchronization
    /// point before any thread proceeds.
    /// </para>
    /// <para>
    /// <strong>Backend Support:</strong>
    /// <list type="bullet">
    /// <item><description>CUDA: Full support (ThreadBlock, Warp, Grid with cooperative launch)</description></item>
    /// <item><description>Metal: ThreadBlock and Warp (no Grid barriers)</description></item>
    /// <item><description>OpenCL: ThreadBlock barriers via barrier() function</description></item>
    /// <item><description>CPU: Emulated via threading primitives</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>Shared memory synchronization</description></item>
    /// <item><description>Reduction operations</description></item>
    /// <item><description>Stencil computations requiring neighbor communication</description></item>
    /// <item><description>Multi-phase algorithms with dependencies</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance Impact:</strong>
    /// Barriers add ~10-20ns latency per synchronization point. Use judiciously
    /// and prefer narrower scopes (Warp &lt; ThreadBlock &lt; Grid) when possible.
    /// </para>
    /// </remarks>
    public bool UseBarriers { get; set; }

    /// <summary>
    /// Gets or sets the synchronization scope for barriers used in this kernel.
    /// Default is ThreadBlock.
    /// </summary>
    /// <value>
    /// The barrier scope determining which threads synchronize together.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property is only relevant when <see cref="UseBarriers"/> is true.
    /// Different scopes have different performance characteristics and availability:
    /// </para>
    /// <para>
    /// <strong>Scope Characteristics:</strong>
    /// <list type="table">
    /// <item><term>ThreadBlock</term><description>~10ns, all backends, most common</description></item>
    /// <item><term>Warp</term><description>~1ns, CUDA/Metal, 32 threads</description></item>
    /// <item><term>Grid</term><description>~1-10μs, CUDA only (not Metal), requires cooperative launch</description></item>
    /// <item><term>Tile</term><description>~20ns, CUDA 7.0+, flexible subset</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Backend Compatibility:</strong>
    /// If Grid barriers are specified but not supported (e.g., Metal), the runtime
    /// will either throw NotSupportedException or fall back to multiple kernel dispatches
    /// with CPU-side synchronization.
    /// </para>
    /// </remarks>
    public BarrierScope BarrierScope { get; set; } = BarrierScope.ThreadBlock;

    /// <summary>
    /// Gets or sets the expected number of threads participating in barrier synchronization.
    /// Default is 0 (automatic based on block/warp size).
    /// </summary>
    /// <value>
    /// The barrier capacity, typically equal to the number of threads per block or warp.
    /// Set to 0 for automatic calculation.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property provides a hint to the runtime about barrier capacity requirements:
    /// <list type="bullet">
    /// <item><description><strong>0 (Automatic):</strong> Runtime calculates based on BlockDimensions</description></item>
    /// <item><description><strong>ThreadBlock scope:</strong> Should match total threads per block (e.g., 256, 512)</description></item>
    /// <item><description><strong>Warp scope:</strong> Should be 32 (CUDA/Metal warp size)</description></item>
    /// <item><description><strong>Grid scope:</strong> Should match total threads in grid</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Validation:</strong> Backends may validate that capacity doesn't exceed
    /// hardware limits (e.g., 1024 threads per block on many GPUs).
    /// </para>
    /// </remarks>
    public int BarrierCapacity { get; set; }

    /// <summary>
    /// Gets or sets the memory consistency model for this kernel's memory operations.
    /// Default is Relaxed (no ordering guarantees).
    /// </summary>
    /// <value>
    /// The memory consistency model controlling memory operation ordering and visibility.
    /// </value>
    /// <remarks>
    /// <para>
    /// Memory consistency models determine how memory operations are ordered and made
    /// visible across threads:
    /// <list type="table">
    /// <item><term>Relaxed</term><description>1.0× performance, no ordering (GPU default)</description></item>
    /// <item><term>ReleaseAcquire</term><description>0.85× performance, causal ordering (recommended)</description></item>
    /// <item><term>Sequential</term><description>0.60× performance, total order (strongest)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>When to Use:</strong>
    /// <list type="bullet">
    /// <item><description><strong>Relaxed:</strong> Data-parallel kernels with no inter-thread communication</description></item>
    /// <item><description><strong>ReleaseAcquire:</strong> Producer-consumer patterns, message passing, distributed data structures</description></item>
    /// <item><description><strong>Sequential:</strong> Complex algorithms requiring total order, or debugging race conditions</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Backend Support:</strong>
    /// <list type="bullet">
    /// <item><description>CUDA: Full support via __threadfence_* and atomics</description></item>
    /// <item><description>Metal: Full support via threadgroup_barrier() with fence flags</description></item>
    /// <item><description>OpenCL: Supported via mem_fence() and atomic operations</description></item>
    /// <item><description>CPU: Volatile + Interlocked operations</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Recommendation:</strong> Start with Relaxed and upgrade to ReleaseAcquire only
    /// if profiling reveals race conditions. Sequential should be used sparingly due to
    /// significant performance overhead.
    /// </para>
    /// </remarks>
    public MemoryConsistencyModel MemoryConsistency { get; set; } = MemoryConsistencyModel.Relaxed;

    /// <summary>
    /// Gets or sets whether to enable causal memory ordering (release-acquire semantics).
    /// Default is false.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable release-acquire memory ordering; <c>false</c> for relaxed ordering.
    /// </value>
    /// <remarks>
    /// <para>
    /// This is a convenience property that automatically sets <see cref="MemoryConsistency"/>
    /// to <see cref="MemoryConsistencyModel.ReleaseAcquire"/> when true. Enables proper
    /// causality for producer-consumer patterns without the overhead of sequential consistency.
    /// </para>
    /// <para>
    /// <strong>Effect:</strong> When enabled (15% overhead):
    /// <list type="bullet">
    /// <item><description>Memory writes are visible in order to other threads</description></item>
    /// <item><description>Prevents read-before-write hazards in message passing</description></item>
    /// <item><description>Ensures causality: if A writes then B reads, B sees A's prior writes</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Typical Usage:</strong>
    /// <code>
    /// [Kernel(UseBarriers = true, EnableCausalOrdering = true)]
    /// public static void ProducerConsumer(Span&lt;int&gt; data, Span&lt;int&gt; flags)
    /// {
    ///     // Producer writes data, then sets flag
    ///     data[tid] = ComputeValue();
    ///     flags[tid] = READY;  // Release
    ///
    ///     // Consumer waits for flag, then reads data
    ///     while (flags[neighbor] != READY);  // Acquire
    ///     Process(data[neighbor]);  // Guaranteed to see producer's write
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public bool EnableCausalOrdering { get; set; }
}
