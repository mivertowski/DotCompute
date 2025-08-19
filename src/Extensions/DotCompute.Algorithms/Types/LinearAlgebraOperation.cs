// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types;


/// <summary>
/// Represents different types of linear algebra operations for kernel optimization.
/// </summary>
public enum LinearAlgebraOperation
{
/// <summary>
/// Matrix multiplication operation.
/// </summary>
MatrixMultiply,

/// <summary>
/// QR decomposition operation.
/// </summary>
QRDecomposition,

/// <summary>
/// Singular Value Decomposition (SVD) operation.
/// </summary>
SVD,

/// <summary>
/// Cholesky decomposition operation.
/// </summary>
CholeskyDecomposition,

/// <summary>
/// LU decomposition operation.
/// </summary>
LUDecomposition,

/// <summary>
/// Eigenvalue computation operation.
/// </summary>
Eigenvalue,

/// <summary>
/// Householder vector computation.
/// </summary>
HouseholderVector,

/// <summary>
/// Householder transformation operation.
/// </summary>
HouseholderTransform,

/// <summary>
/// Matrix inversion operation.
/// </summary>
MatrixInversion,

/// <summary>
/// Linear system solving operation.
/// </summary>
LinearSystemSolve,

/// <summary>
/// Matrix transpose operation.
/// </summary>
Transpose,

/// <summary>
/// Matrix-vector multiplication operation.
/// </summary>
MatrixVectorMultiply,

/// <summary>
/// Vector dot product operation.
/// </summary>
VectorDotProduct,

/// <summary>
/// Vector cross product operation.
/// </summary>
VectorCrossProduct,

/// <summary>
/// Matrix norm computation.
/// </summary>
MatrixNorm,

/// <summary>
/// Vector norm computation.
/// </summary>
VectorNorm
}
