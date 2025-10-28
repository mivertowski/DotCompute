#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.LinearAlgebra.LinearAlgebraKernels;
/// <summary>
/// An linear algebra operation enumeration.
/// </summary>


/// <summary>
/// Linear algebra operation types.
/// </summary>
public enum LinearAlgebraOperation
{
    MatrixMultiply,
    MatrixAdd,
    MatrixSubtract,
    VectorAdd,
    VectorDotProduct,
    Transpose
}

/// <summary>
/// Hardware information for optimization.
/// </summary>
public sealed class HardwareInfo
{
    /// <summary>
    /// Gets or sets the compute units.
    /// </summary>
    /// <value>The compute units.</value>
    public int ComputeUnits { get; init; }
    /// <summary>
    /// Gets or sets the memory size.
    /// </summary>
    /// <value>The memory size.</value>
    public long MemorySize { get; init; }
    /// <summary>
    /// Gets or sets the architecture.
    /// </summary>
    /// <value>The architecture.</value>
    public string Architecture { get; init; } = "Unknown";

    /// <summary>
    /// Gets or sets the global memory size in bytes.
    /// </summary>
    public long GlobalMemorySize { get; init; }

    /// <summary>
    /// Gets or sets the shared memory size per block in bytes.
    /// </summary>
    public int SharedMemorySize { get; init; }

    /// <summary>
    /// Gets or sets the maximum work group size.
    /// </summary>
    public int MaxWorkGroupSize { get; init; }
}

/// <summary>
/// Parameters for kernel execution.
/// </summary>
public sealed class KernelExecutionParameters
{
    /// <summary>
    /// Gets or sets the block size.
    /// </summary>
    /// <value>The block size.</value>
    public int BlockSize { get; init; } = 256;
    /// <summary>
    /// Gets or sets the grid size.
    /// </summary>
    /// <value>The grid size.</value>
    public int GridSize { get; init; } = 1;
    /// <summary>
    /// Gets or sets the shared memory size.
    /// </summary>
    /// <value>The shared memory size.</value>
    public long SharedMemorySize { get; init; }

    /// <summary>
    /// Gets or sets the global work size dimensions.
    /// </summary>
    public IReadOnlyList<ulong> GlobalWorkSize { get; init; } = Array.Empty<ulong>();

    /// <summary>
    /// Gets or sets the local work size dimensions.
    /// </summary>
    public IReadOnlyList<ulong> LocalWorkSize { get; init; } = Array.Empty<ulong>();

    /// <summary>
    /// Gets or sets the work group size.
    /// </summary>
    public int WorkGroupSize { get; init; } = 256;
}
