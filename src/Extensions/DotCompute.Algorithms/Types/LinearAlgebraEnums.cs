// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms;

/// <summary>
/// Linear algebra operations supported by DotCompute kernel library.
/// </summary>
/// <remarks>
/// <para>
/// Enumerates GPU-accelerated linear algebra operations with optimized kernels
/// for CUDA, OpenCL, and Metal backends. Each operation has specialized
/// implementations for different matrix types and sizes.
/// </para>
/// <para>
/// Used by <see cref="LinearAlgebraKernelLibrary"/> to select appropriate
/// kernel implementations based on operation type and hardware capabilities.
/// </para>
/// </remarks>
public enum LinearAlgebraOperation
{
    /// <summary>
    /// Matrix-matrix multiplication (GEMM - General Matrix Multiply).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computes C = αAB + βC where A is M×K, B is K×N, C is M×N.
    /// Optimized implementations use tiled algorithms with shared memory.
    /// </para>
    /// <para><b>Performance</b>: Up to 4 TFLOPS on modern GPUs</para>
    /// <para><b>Kernels</b>: Tiled, Strassen's algorithm for large matrices</para>
    /// </remarks>
    MatrixMultiply,

    /// <summary>
    /// Householder reflection vector computation for QR decomposition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computes Householder vector v where H = I - 2vv^T reflects
    /// input vector x onto a coordinate axis. Used as building block
    /// for QR decomposition.
    /// </para>
    /// <para><b>Numerical Stability</b>: Designed to avoid catastrophic cancellation</para>
    /// </remarks>
    HouseholderVector,

    /// <summary>
    /// Householder transformation application to matrices.
    /// </summary>
    /// <remarks>
    /// Applies Householder reflector H = I - 2vv^T to matrix A.
    /// Critical operation in QR decomposition and eigenvalue algorithms.
    /// </remarks>
    HouseholderTransform,

    /// <summary>
    /// Jacobi Singular Value Decomposition (SVD).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computes SVD A = UΣV^T using Jacobi iterations. Provides high
    /// accuracy singular values and vectors through iterative refinement.
    /// </para>
    /// <para><b>Convergence</b>: Quadratic convergence with proper pivoting</para>
    /// <para><b>Accuracy</b>: Machine precision singular values</para>
    /// <para><b>Use Cases</b>: Numerical analysis, principal component analysis, signal processing</para>
    /// </remarks>
    JacobiSVD,

    /// <summary>
    /// Matrix-vector multiplication (GEMV).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computes y = αAx + βy where A is M×N matrix, x is N-vector, y is M-vector.
    /// Optimized for different matrix layouts (row-major, column-major).
    /// </para>
    /// <para><b>Performance</b>: Memory-bandwidth bound, benefits from coalesced access</para>
    /// </remarks>
    MatrixVector,

    /// <summary>
    /// Parallel reduction operations (sum, max, min, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Efficient parallel reduction using tree-based algorithms in shared memory.
    /// Used as primitive for norms, dot products, and statistical operations.
    /// </para>
    /// <para><b>Algorithm</b>: Binary tree reduction with warp-level primitives</para>
    /// <para><b>Performance</b>: O(log N) depth, minimal synchronization overhead</para>
    /// </remarks>
    ParallelReduction,

    /// <summary>
    /// QR algorithm for eigenvalue computation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Iterative QR factorization algorithm for computing eigenvalues and eigenvectors
    /// of general matrices. Uses Householder or Givens rotations.
    /// </para>
    /// <para><b>Convergence</b>: Linear convergence, accelerated with shifts</para>
    /// <para><b>Preprocessing</b>: Hessenberg reduction for efficiency</para>
    /// </remarks>
    QRAlgorithm,

    /// <summary>
    /// Cholesky decomposition for symmetric positive-definite matrices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computes L where A = LL^T for symmetric positive-definite A.
    /// Twice as efficient as LU decomposition for this matrix class.
    /// </para>
    /// <para><b>Requirements</b>: Matrix must be symmetric and positive-definite</para>
    /// <para><b>Applications</b>: Linear least squares, optimization, Monte Carlo</para>
    /// <para><b>Numerical Stability</b>: Highly stable for well-conditioned matrices</para>
    /// </remarks>
    CholeskyDecomposition,

    /// <summary>
    /// LU decomposition with partial pivoting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computes PA = LU where P is permutation matrix, L is lower triangular,
    /// U is upper triangular. General-purpose factorization for linear systems.
    /// </para>
    /// <para><b>Pivoting</b>: Partial pivoting for numerical stability</para>
    /// <para><b>Use Cases</b>: Solving linear systems, matrix inversion, determinants</para>
    /// </remarks>
    LUDecomposition,

    /// <summary>
    /// Eigenvalue decomposition for diagonalizable matrices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computes A = VΛV^(-1) where Λ is diagonal matrix of eigenvalues,
    /// V contains corresponding eigenvectors. Critical for spectral analysis.
    /// </para>
    /// <para><b>Algorithm</b>: QR algorithm with Hessenberg reduction</para>
    /// <para><b>Complexity</b>: O(N³) with iterative refinement</para>
    /// <para><b>Applications</b>: Stability analysis, quantum mechanics, vibration modes</para>
    /// </remarks>
    EigenDecomposition
}
