// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Attributes;

/// <summary>
/// Declares a shared memory allocation for a kernel method.
/// Shared memory (threadgroup memory in Metal, __shared__ in CUDA, __local in OpenCL)
/// is fast on-chip memory shared between threads in a thread block/workgroup.
/// </summary>
/// <remarks>
/// <para>
/// Shared memory is significantly faster than global memory (typically 10-100x lower latency)
/// and is essential for many GPU optimization patterns:
/// </para>
/// <list type="bullet">
/// <item><description>Reduction operations (sum, min, max across threads)</description></item>
/// <item><description>Tiled matrix multiplication (loading tiles into shared memory)</description></item>
/// <item><description>Stencil computations (caching neighbor values)</description></item>
/// <item><description>Inter-thread communication within a block</description></item>
/// </list>
/// <para>
/// <strong>Backend Translation:</strong>
/// </para>
/// <list type="table">
/// <listheader><term>Backend</term><description>Translation</description></listheader>
/// <item><term>CUDA</term><description><c>__shared__ T name[Size];</c></description></item>
/// <item><term>Metal</term><description><c>threadgroup T* name [[threadgroup(N)]];</c></description></item>
/// <item><term>OpenCL</term><description><c>__local T name[Size];</c></description></item>
/// <item><term>CPU</term><description>Thread-local storage or stack allocation</description></item>
/// </list>
/// <para>
/// <strong>Limitations:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Size is limited per thread block (typically 32-48KB on modern GPUs)</description></item>
/// <item><description>Only accessible within a single thread block/workgroup</description></item>
/// <item><description>Must use barriers for synchronization when sharing data</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Declare 256 floats of shared memory for a reduction
/// [Kernel(Backends = KernelBackends.CUDA | KernelBackends.Metal)]
/// [SharedMemory(typeof(float), "shared_data", Size = 256)]
/// public static void Reduction(ReadOnlySpan&lt;float&gt; input, Span&lt;float&gt; output)
/// {
///     int tid = Kernel.ThreadId.X;
///     int gid = Kernel.GlobalId.X;
///
///     // Load into shared memory
///     Kernel.SharedMemory&lt;float&gt;("shared_data")[tid] = input[gid];
///     Kernel.Barrier();
///
///     // Parallel reduction in shared memory
///     for (int stride = 128; stride &gt; 0; stride &gt;&gt;= 1)
///     {
///         if (tid &lt; stride)
///             Kernel.SharedMemory&lt;float&gt;("shared_data")[tid] +=
///                 Kernel.SharedMemory&lt;float&gt;("shared_data")[tid + stride];
///         Kernel.Barrier();
///     }
///
///     if (tid == 0)
///         output[Kernel.BlockId.X] = Kernel.SharedMemory&lt;float&gt;("shared_data")[0];
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class SharedMemoryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SharedMemoryAttribute"/> class.
    /// </summary>
    /// <param name="elementType">The type of elements in the shared memory array.</param>
    /// <param name="name">The name used to reference this shared memory in the kernel.</param>
    public SharedMemoryAttribute(Type elementType, string name)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Gets the element type for the shared memory array.
    /// </summary>
    /// <value>
    /// The type of each element in the shared memory. Must be an unmanaged type
    /// (e.g., int, float, double, or an unmanaged struct).
    /// </value>
    public Type ElementType { get; }

    /// <summary>
    /// Gets the name used to reference this shared memory allocation in the kernel code.
    /// </summary>
    /// <value>
    /// A unique identifier for this shared memory block within the kernel.
    /// Used with <c>Kernel.SharedMemory&lt;T&gt;(name)</c> to access the memory.
    /// </value>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the number of elements in the shared memory array.
    /// </summary>
    /// <value>
    /// The size of the shared memory array in elements (not bytes).
    /// Default is 0, which indicates dynamic sizing based on thread block size.
    /// </value>
    /// <remarks>
    /// <para>
    /// When Size is 0 (dynamic), the shared memory is typically sized to match
    /// the thread block size or can be specified at kernel launch time.
    /// </para>
    /// <para>
    /// <strong>Memory Calculation:</strong> Total bytes = Size * sizeof(ElementType)
    /// </para>
    /// <para>
    /// <strong>Common Sizes:</strong>
    /// <list type="bullet">
    /// <item><description>256 - Standard block size</description></item>
    /// <item><description>512/1024 - Larger blocks for higher occupancy</description></item>
    /// <item><description>32 - Warp-sized for warp-level operations</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets the memory alignment requirement in bytes.
    /// </summary>
    /// <value>
    /// The required memory alignment in bytes. Default is 0 (use natural alignment).
    /// </value>
    /// <remarks>
    /// <para>
    /// Proper alignment can improve memory access performance:
    /// <list type="bullet">
    /// <item><description>16 bytes - Optimal for float4/int4 vector types</description></item>
    /// <item><description>32 bytes - Cache line alignment on some GPUs</description></item>
    /// <item><description>128 bytes - Memory transaction alignment on NVIDIA GPUs</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Backend Translation:</strong>
    /// <list type="bullet">
    /// <item><description>CUDA: <c>__align__(N)</c></description></item>
    /// <item><description>Metal: <c>alignas(N)</c></description></item>
    /// <item><description>OpenCL: Uses aligned allocation</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public int Alignment { get; set; }

    /// <summary>
    /// Gets or sets whether to zero-initialize the shared memory.
    /// </summary>
    /// <value>
    /// <c>true</c> to zero-initialize the shared memory before use;
    /// <c>false</c> for uninitialized memory (faster).
    /// </value>
    /// <remarks>
    /// <para>
    /// Zero initialization adds overhead but ensures predictable initial values.
    /// Only enable when the algorithm requires known initial state.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Disabling saves ~1-2 clock cycles per element.
    /// </para>
    /// </remarks>
    public bool ZeroInitialize { get; set; }

    /// <summary>
    /// Gets or sets the threadgroup binding index for Metal shaders.
    /// </summary>
    /// <value>
    /// The threadgroup buffer binding index for Metal (0-based).
    /// Default is -1 (auto-assign based on declaration order).
    /// </value>
    /// <remarks>
    /// <para>
    /// Metal requires explicit binding indices for threadgroup memory:
    /// <code>
    /// threadgroup float* shared_data [[threadgroup(0)]];
    /// threadgroup int* shared_indices [[threadgroup(1)]];
    /// </code>
    /// </para>
    /// <para>
    /// When set to -1, binding indices are auto-assigned in declaration order.
    /// Set explicitly when multiple kernels share consistent binding layouts.
    /// </para>
    /// </remarks>
    public int MetalBindingIndex { get; set; } = -1;
}
