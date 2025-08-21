// <copyright file="CublasDiagType.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.BLAS.Types;

/// <summary>
/// Specifies the type of the diagonal elements in triangular matrices.
/// Used in triangular solve and triangular matrix-vector operations.
/// </summary>
public enum CublasDiagType
{
    /// <summary>
    /// Non-unit diagonal.
    /// The diagonal elements are read from the matrix.
    /// </summary>
    NonUnit = 0,

    /// <summary>
    /// Unit diagonal.
    /// The diagonal elements are assumed to be 1 and are not read from memory.
    /// Improves performance when the diagonal is known to be unit.
    /// </summary>
    Unit = 1
}