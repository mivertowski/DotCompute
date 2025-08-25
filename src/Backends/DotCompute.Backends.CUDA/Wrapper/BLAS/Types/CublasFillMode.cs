// <copyright file="CublasFillMode.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Wrapper.BLAS.Types;

/// <summary>
/// Specifies which part of a matrix is filled in cuBLAS functions.
/// Used for triangular and symmetric matrices.
/// </summary>
public enum CublasFillMode
{
    /// <summary>
    /// Lower triangular or symmetric matrix.
    /// Only the lower triangle of the matrix is referenced.
    /// </summary>
    Lower = 0,

    /// <summary>
    /// Upper triangular or symmetric matrix.
    /// Only the upper triangle of the matrix is referenced.
    /// </summary>
    Upper = 1
}