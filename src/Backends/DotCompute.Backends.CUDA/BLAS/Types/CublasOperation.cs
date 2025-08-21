// <copyright file="CublasOperation.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.BLAS.Types;

/// <summary>
/// Specifies the operation to be performed on matrices in cuBLAS functions.
/// Controls whether matrices are transposed or conjugate transposed.
/// </summary>
public enum CublasOperation
{
    /// <summary>
    /// No transpose operation.
    /// The matrix is used as-is: op(A) = A.
    /// </summary>
    NonTranspose = 0,

    /// <summary>
    /// Transpose operation.
    /// The matrix is transposed: op(A) = A^T.
    /// </summary>
    Transpose = 1,

    /// <summary>
    /// Conjugate transpose operation (Hermitian transpose).
    /// The matrix is conjugate transposed: op(A) = A^H.
    /// Used for complex matrices.
    /// </summary>
    ConjugateTranspose = 2
}