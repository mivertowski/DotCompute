
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Selection.Enums;

/// <summary>
/// BLAS operation types for algorithm selection.
/// Represents the fundamental linear algebra operations supported by BLAS libraries.
/// </summary>
public enum BLASOperation
{
    /// <summary>
    /// Dot product: result = x · y
    /// </summary>
    DOT,

    /// <summary>
    /// Scaled vector addition: y = αx + y
    /// </summary>
    AXPY,

    /// <summary>
    /// General matrix-vector multiply: y = αAx + βy
    /// </summary>
    GEMV,

    /// <summary>
    /// General matrix-matrix multiply: C = αAB + βC
    /// </summary>
    GEMM
}
