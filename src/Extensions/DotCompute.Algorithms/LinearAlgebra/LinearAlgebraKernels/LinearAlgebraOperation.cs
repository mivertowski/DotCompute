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
}

/// <summary>
/// Parameters for kernel execution.
/// </summary>
public sealed class KernelExecutionParameters
{
public int BlockSize { get; init; } = 256;
public int GridSize { get; init; } = 1;
public long SharedMemorySize { get; init; }
}
