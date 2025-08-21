// <copyright file="CublasSideMode.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.BLAS.Types;

/// <summary>
/// Specifies whether a matrix appears on the left or right side in matrix equations.
/// Used in triangular solve operations and symmetric matrix multiplication.
/// </summary>
public enum CublasSideMode
{
    /// <summary>
    /// Matrix is on the left side.
    /// For solving: op(A) * X = alpha * B.
    /// </summary>
    Left = 0,

    /// <summary>
    /// Matrix is on the right side.
    /// For solving: X * op(A) = alpha * B.
    /// </summary>
    Right = 1
}