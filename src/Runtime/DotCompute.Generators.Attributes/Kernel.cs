// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Generators;

/// <summary>
/// Runtime helper providing GPU/SIMD thread intrinsics for kernel code.
/// These values represent the current work-item's position in the execution grid.
/// </summary>
/// <remarks>
/// <para>
/// In kernel code, use these properties to determine which data element the current
/// thread should process. The source generator replaces these placeholder values
/// with actual backend-specific intrinsics (e.g., CUDA's threadIdx, blockIdx).
/// </para>
/// <para>
/// <strong>Execution Model:</strong>
/// <list type="bullet">
/// <item><description><see cref="ThreadId"/>: Position within a thread block (0 to BlockDim-1)</description></item>
/// <item><description><see cref="BlockId"/>: Which block this thread belongs to (0 to GridDim-1)</description></item>
/// <item><description><see cref="BlockDim"/>: Number of threads per block in each dimension</description></item>
/// <item><description><see cref="GridDim"/>: Number of blocks in the grid in each dimension</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Global Thread Index Calculation:</strong>
/// <code>
/// int globalX = BlockId.X * BlockDim.X + ThreadId.X;
/// int globalY = BlockId.Y * BlockDim.Y + ThreadId.Y;
/// </code>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Kernel]
/// public static void VectorAdd(ReadOnlySpan&lt;float&gt; a, ReadOnlySpan&lt;float&gt; b, Span&lt;float&gt; result)
/// {
///     int idx = KernelContext.ThreadId.X;
///     if (idx &lt; result.Length)
///         result[idx] = a[idx] + b[idx];
/// }
/// </code>
/// </example>
public static class KernelContext
{
    /// <summary>
    /// Gets the thread index within the current block.
    /// </summary>
    public static ThreadIndex ThreadId => default;

    /// <summary>
    /// Gets the block index within the grid.
    /// </summary>
    public static BlockIndex BlockId => default;

    /// <summary>
    /// Gets the dimensions of each thread block.
    /// </summary>
    public static BlockDimension BlockDim => default;

    /// <summary>
    /// Gets the dimensions of the execution grid.
    /// </summary>
    public static GridDimension GridDim => default;

    /// <summary>
    /// Synchronizes all threads within the current thread block.
    /// </summary>
    public static void SyncThreads() { }

    /// <summary>
    /// Atomically adds a value to a memory location.
    /// </summary>
    /// <param name="location">Reference to the target location.</param>
    /// <param name="value">Value to add.</param>
    /// <returns>The original value before addition.</returns>
    public static int AtomicAdd(ref int location, int value) => System.Threading.Interlocked.Add(ref location, value) - value;

    /// <summary>
    /// Atomically adds a value to a memory location.
    /// </summary>
    /// <param name="location">Reference to the target location.</param>
    /// <param name="value">Value to add.</param>
    /// <returns>The original value before addition.</returns>
    public static float AtomicAdd(ref float location, float value)
    {
        float initial, computed;
        do
        {
            initial = location;
            computed = initial + value;
        }
        while (System.Threading.Interlocked.CompareExchange(ref location, computed, initial) != initial);
        return initial;
    }
}

/// <summary>
/// Represents a 3D thread index within a thread block.
/// </summary>
/// <remarks>
/// These are placeholder values replaced by the source generator with actual
/// backend intrinsics (threadIdx for CUDA, get_local_id for OpenCL, etc.).
/// </remarks>
#pragma warning disable CA1815 // Override equals and operator equals on value types
#pragma warning disable CS0649 // Field is never assigned to (placeholder values replaced by source generator)
public readonly struct ThreadIndex
#pragma warning restore CA1815
{
    private readonly int _x;
    private readonly int _y;
    private readonly int _z;
#pragma warning restore CS0649

    /// <summary>Gets the X component of the thread index.</summary>
    public int X => _x;

    /// <summary>Gets the Y component of the thread index.</summary>
    public int Y => _y;

    /// <summary>Gets the Z component of the thread index.</summary>
    public int Z => _z;
}

/// <summary>
/// Represents a 3D block index within the execution grid.
/// </summary>
#pragma warning disable CA1815
#pragma warning disable CS0649
public readonly struct BlockIndex
#pragma warning restore CA1815
{
    private readonly int _x;
    private readonly int _y;
    private readonly int _z;
#pragma warning restore CS0649

    /// <summary>Gets the X component of the block index.</summary>
    public int X => _x;

    /// <summary>Gets the Y component of the block index.</summary>
    public int Y => _y;

    /// <summary>Gets the Z component of the block index.</summary>
    public int Z => _z;
}

/// <summary>
/// Represents the 3D dimensions of a thread block.
/// </summary>
#pragma warning disable CA1815
public readonly struct BlockDimension
#pragma warning restore CA1815
{
    private readonly int _x;
    private readonly int _y;
    private readonly int _z;

    /// <summary>Initializes a new instance with default dimension of 1.</summary>
    public BlockDimension()
    {
        _x = 1;
        _y = 1;
        _z = 1;
    }

    /// <summary>Gets the X dimension (number of threads in X).</summary>
    public int X => _x;

    /// <summary>Gets the Y dimension (number of threads in Y).</summary>
    public int Y => _y;

    /// <summary>Gets the Z dimension (number of threads in Z).</summary>
    public int Z => _z;
}

/// <summary>
/// Represents the 3D dimensions of the execution grid.
/// </summary>
#pragma warning disable CA1815
public readonly struct GridDimension
#pragma warning restore CA1815
{
    private readonly int _x;
    private readonly int _y;
    private readonly int _z;

    /// <summary>Initializes a new instance with default dimension of 1.</summary>
    public GridDimension()
    {
        _x = 1;
        _y = 1;
        _z = 1;
    }

    /// <summary>Gets the X dimension (number of blocks in X).</summary>
    public int X => _x;

    /// <summary>Gets the Y dimension (number of blocks in Y).</summary>
    public int Y => _y;

    /// <summary>Gets the Z dimension (number of blocks in Z).</summary>
    public int Z => _z;
}
