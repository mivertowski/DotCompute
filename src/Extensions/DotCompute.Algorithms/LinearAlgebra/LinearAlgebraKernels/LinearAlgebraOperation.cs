// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.LinearAlgebra.LinearAlgebraKernels;


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
    public int ComputeUnits { get; init; }
    public long MemorySize { get; init; }
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
    public int BlockSize { get; init; } = 256;
    public int GridSize { get; init; } = 1;
    public long SharedMemorySize { get; init; }

    /// <summary>
    /// Gets or sets the global work size dimensions.
    /// </summary>
    public ulong[] GlobalWorkSize { get; init; } = [];

    /// <summary>
    /// Gets or sets the local work size dimensions.
    /// </summary>
    public ulong[] LocalWorkSize { get; init; } = [];

    /// <summary>
    /// Gets or sets the work group size.
    /// </summary>
    public int WorkGroupSize { get; init; } = 256;
}
