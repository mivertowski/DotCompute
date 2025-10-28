// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.LinearAlgebra.Operations;

namespace DotCompute.Algorithms.LinearAlgebra
{
    /// <summary>
    /// Provides high-performance matrix operations with GPU acceleration.
    /// This class serves as a facade for the organized matrix operation modules.
    /// </summary>
    public static class MatrixMath
    {

        // Basic Matrix Operations
        /// <summary>
        /// Multiplies two matrices with GPU acceleration when beneficial.
        /// </summary>
        public static async Task<Matrix> MultiplyAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixOperations.MultiplyAsync(a, b, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Adds two matrices element-wise with GPU acceleration when beneficial.
        /// </summary>
        public static async Task<Matrix> AddAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixOperations.AddAsync(a, b, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Subtracts matrix b from matrix a element-wise with GPU acceleration when beneficial.
        /// </summary>
        public static async Task<Matrix> SubtractAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixOperations.SubtractAsync(a, b, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Transposes a matrix.
        /// </summary>
        public static async Task<Matrix> TransposeAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixOperations.TransposeAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Computes the inverse of a square matrix.
        /// </summary>
        public static async Task<Matrix> InverseAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixOperations.InverseAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Computes the determinant of a square matrix.
        /// </summary>
        public static async Task<float> DeterminantAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixOperations.DeterminantAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

        // Matrix Decomposition Operations

        /// <summary>
        /// Computes the QR decomposition of a matrix using Householder reflections.
        /// </summary>
        /// <param name="matrix">Input matrix to decompose.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A tuple containing Q (orthogonal) and R (upper triangular) matrices.</returns>
        public static async Task<(Matrix Q, Matrix R)> QRDecompositionAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixDecomposition.QRDecompositionAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Performs LU decomposition with partial pivoting.
        /// </summary>
        public static async Task<(Matrix L, Matrix U, int[] P)> LUDecompositionAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixDecomposition.LUDecompositionAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Computes the Singular Value Decomposition (SVD) of a matrix.
        /// </summary>
        /// <param name="matrix">Input matrix to decompose.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A tuple containing U, S (diagonal singular values), and VT matrices.</returns>
        public static async Task<(Matrix U, Matrix S, Matrix VT)> SVDAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixDecomposition.SVDAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Computes the Cholesky decomposition of a positive definite matrix.
        /// </summary>
        /// <param name="matrix">Input positive definite matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Lower triangular matrix L such that A = L * L^T.</returns>
        public static async Task<Matrix> CholeskyDecompositionAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixDecomposition.CholeskyDecompositionAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Computes eigenvalues and eigenvectors using the QR algorithm with shifts.
        /// </summary>
        /// <param name="matrix">Input square matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="maxIterations">Maximum number of iterations.</param>
        /// <param name="tolerance">Convergence tolerance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A tuple containing eigenvalues and eigenvectors.</returns>
        public static async Task<(Matrix Eigenvalues, Matrix Eigenvectors)> EigenDecompositionAsync(Matrix matrix, IAccelerator accelerator, int maxIterations = 1000, float tolerance = 1e-10f, CancellationToken cancellationToken = default)
            => await MatrixDecomposition.EigenDecompositionAsync(matrix, accelerator, maxIterations, tolerance, cancellationToken).ConfigureAwait(false);

        // Matrix Solver Operations
        /// <summary>
        /// Solves the linear system Ax = b.
        /// </summary>
        public static async Task<Matrix> SolveAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixSolvers.SolveAsync(a, b, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Solves the linear system Ax = b using iterative refinement for improved accuracy.
        /// </summary>
        /// <param name="a">Coefficient matrix.</param>
        /// <param name="b">Right-hand side vector.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="maxRefinements">Maximum number of refinement iterations.</param>
        /// <param name="tolerance">Convergence tolerance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Solution vector with improved accuracy.</returns>
        public static async Task<Matrix> SolveWithRefinementAsync(Matrix a, Matrix b, IAccelerator accelerator, int maxRefinements = 5, float tolerance = 1e-12f, CancellationToken cancellationToken = default)
            => await MatrixSolvers.SolveWithRefinementAsync(a, b, accelerator, maxRefinements, tolerance, cancellationToken).ConfigureAwait(false);

        // Matrix Statistics Operations
        /// <summary>
        /// Computes the condition number of a matrix using SVD.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The condition number (ratio of largest to smallest singular value).</returns>
        public static async Task<float> ConditionNumberAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixStatistics.ConditionNumberAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Computes the Frobenius norm of a matrix.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <returns>The Frobenius norm.</returns>
        public static float FrobeniusNorm(Matrix matrix)
            => MatrixStatistics.FrobeniusNorm(matrix);

        /// <summary>
        /// Computes the trace (sum of diagonal elements) of a square matrix.
        /// </summary>
        /// <param name="matrix">Input square matrix.</param>
        /// <returns>The trace.</returns>
        public static float Trace(Matrix matrix)
            => MatrixStatistics.Trace(matrix);

        /// <summary>
        /// Computes the rank of a matrix using SVD.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="tolerance">Tolerance for determining zero singular values.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The numerical rank of the matrix.</returns>
        public static async Task<int> RankAsync(Matrix matrix, IAccelerator accelerator, float tolerance = 1e-10f, CancellationToken cancellationToken = default)
            => await MatrixStatistics.RankAsync(matrix, accelerator, tolerance, cancellationToken).ConfigureAwait(false);

        // Matrix Transform Operations
        /// <summary>
        /// Creates a scaling transformation matrix.
        /// </summary>
        /// <param name="scaleX">Scale factor for X axis.</param>
        /// <param name="scaleY">Scale factor for Y axis.</param>
        /// <param name="scaleZ">Scale factor for Z axis (optional, for 3D).</param>
        /// <returns>Scaling transformation matrix.</returns>
        public static Matrix CreateScaling(float scaleX, float scaleY, float scaleZ = 1.0f)
            => MatrixTransforms.CreateScaling(scaleX, scaleY, scaleZ);

        /// <summary>
        /// Creates a rotation transformation matrix around the Z axis.
        /// </summary>
        /// <param name="angleRadians">Rotation angle in radians.</param>
        /// <returns>Rotation transformation matrix.</returns>
        public static Matrix CreateRotationZ(float angleRadians)
            => MatrixTransforms.CreateRotationZ(angleRadians);

        /// <summary>
        /// Applies a transformation to a point or vector.
        /// </summary>
        /// <param name="transformation">Transformation matrix.</param>
        /// <param name="point">Point or vector to transform.</param>
        /// <param name="isPoint">True if transforming a point, false for a vector.</param>
        /// <returns>Transformed point or vector.</returns>
        public static async Task<Matrix> ApplyTransformAsync(Matrix transformation, Matrix point, bool isPoint = true, IAccelerator? accelerator = null, CancellationToken cancellationToken = default)
            => await MatrixTransforms.ApplyTransformAsync(transformation, point, isPoint, accelerator, cancellationToken).ConfigureAwait(false);

        // Matrix Factorization Operations
        /// <summary>
        /// Computes the Moore-Penrose pseudoinverse of a matrix using SVD.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="tolerance">Tolerance for determining zero singular values.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The pseudoinverse matrix.</returns>
        public static async Task<Matrix> PseudoInverseAsync(Matrix matrix, IAccelerator accelerator, float tolerance = 1e-10f, CancellationToken cancellationToken = default)
            => await MatrixFactorization.PseudoInverseAsync(matrix, accelerator, tolerance, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Computes the matrix exponential using Padé approximation with scaling and squaring.
        /// </summary>
        /// <param name="matrix">Input square matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matrix exponential exp(A).</returns>
        public static async Task<Matrix> MatrixExponentialAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixFactorization.MatrixExponentialAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Computes a matrix power A^p using eigenvalue decomposition.
        /// </summary>
        /// <param name="matrix">Input square matrix.</param>
        /// <param name="power">Power to raise the matrix to.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matrix power A^p.</returns>
        public static async Task<Matrix> MatrixPowerAsync(Matrix matrix, float power, IAccelerator accelerator, CancellationToken cancellationToken = default)
            => await MatrixFactorization.MatrixPowerAsync(matrix, power, accelerator, cancellationToken).ConfigureAwait(false);

        #region Legacy Support - Public Methods for Backward Compatibility

        /// <summary>
        /// Legacy method for copying array data to matrix - maintained for backward compatibility.
        /// </summary>
        /// <param name="array">Source array.</param>
        /// <param name="matrix">Target matrix.</param>
        public static void CopyArrayToMatrix(float[] array, Matrix matrix)
            => MatrixOperations.CopyArrayToMatrix(array, matrix);

        #endregion
    }
}
