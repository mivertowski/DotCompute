// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Algorithms.LinearAlgebra.Operations
{
    /// <summary>
    /// Provides advanced matrix factorization and specialized decomposition operations.
    /// </summary>
    public static class MatrixFactorization
    {
        /// <summary>
        /// Computes the Moore-Penrose pseudoinverse of a matrix using SVD.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="tolerance">Tolerance for determining zero singular values.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The pseudoinverse matrix.</returns>
        public static async Task<Matrix> PseudoInverseAsync(Matrix matrix, IAccelerator accelerator, float tolerance = 1e-10f, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);
            
            var (u, s, vt) = await MatrixDecomposition.SVDAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);
            
            // Create S+ (pseudoinverse of diagonal matrix S)
            var sPlus = new Matrix(s.Columns, s.Rows);
            for (var i = 0; i < Math.Min(s.Rows, s.Columns); i++)
            {
                var singularValue = s[i, i];
                if (singularValue > tolerance)
                {
                    sPlus[i, i] = 1.0f / singularValue;
                }
                // else leave as 0 (already initialized)
            }
            
            // Pseudoinverse = V * S+ * U^T
            var vTransposed = await MatrixOperations.TransposeAsync(vt, accelerator, cancellationToken).ConfigureAwait(false);
            var uTransposed = await MatrixOperations.TransposeAsync(u, accelerator, cancellationToken).ConfigureAwait(false);
            
            var temp = await MatrixOperations.MultiplyAsync(vTransposed, sPlus, accelerator, cancellationToken).ConfigureAwait(false);
            var pseudoInverse = await MatrixOperations.MultiplyAsync(temp, uTransposed, accelerator, cancellationToken).ConfigureAwait(false);
            
            return pseudoInverse;
        }

        /// <summary>
        /// Computes the polar decomposition A = UP where U is unitary and P is positive semidefinite.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A tuple containing U (unitary) and P (positive semidefinite) matrices.</returns>
        public static async Task<(Matrix U, Matrix P)> PolarDecompositionAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);
            
            var (u, s, vt) = await MatrixDecomposition.SVDAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);
            
            // U_polar = U * V^T
            var uPolar = await MatrixOperations.MultiplyAsync(u, vt, accelerator, cancellationToken).ConfigureAwait(false);
            
            // P = V * S * V^T
            var v = await MatrixOperations.TransposeAsync(vt, accelerator, cancellationToken).ConfigureAwait(false);
            var temp = await MatrixOperations.MultiplyAsync(v, s, accelerator, cancellationToken).ConfigureAwait(false);
            var p = await MatrixOperations.MultiplyAsync(temp, vt, accelerator, cancellationToken).ConfigureAwait(false);
            
            return (uPolar, p);
        }

        /// <summary>
        /// Computes the Schur decomposition A = QTQ^T where Q is orthogonal and T is quasi-upper triangular.
        /// </summary>
        /// <param name="matrix">Input square matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="maxIterations">Maximum number of iterations.</param>
        /// <param name="tolerance">Convergence tolerance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A tuple containing Q (orthogonal) and T (quasi-upper triangular) matrices.</returns>
        public static async Task<(Matrix Q, Matrix T)> SchurDecompositionAsync(Matrix matrix, IAccelerator accelerator, int maxIterations = 1000, float tolerance = 1e-10f, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);
            
            if (!matrix.IsSquare)
            {
                throw new ArgumentException("Matrix must be square for Schur decomposition.");
            }
            
            // Use QR algorithm with shifts (similar to eigenvalue computation)
            var (eigenvalues, eigenvectors) = await MatrixDecomposition.EigenDecompositionAsync(matrix, accelerator, maxIterations, tolerance, cancellationToken).ConfigureAwait(false);
            
            // For real matrices, the Schur form is the result of the QR algorithm
            // The eigenvectors matrix Q is already orthogonal from the QR algorithm
            var q = eigenvectors;
            
            // T = Q^T * A * Q
            var qTransposed = await MatrixOperations.TransposeAsync(q, accelerator, cancellationToken).ConfigureAwait(false);
            var temp = await MatrixOperations.MultiplyAsync(qTransposed, matrix, accelerator, cancellationToken).ConfigureAwait(false);
            var t = await MatrixOperations.MultiplyAsync(temp, q, accelerator, cancellationToken).ConfigureAwait(false);
            
            return (q, t);
        }

        /// <summary>
        /// Computes the Jordan normal form of a matrix.
        /// </summary>
        /// <param name="matrix">Input square matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="tolerance">Tolerance for determining eigenvalue multiplicities.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A tuple containing P (similarity transform) and J (Jordan form) matrices.</returns>
        public static async Task<(Matrix P, Matrix J)> JordanDecompositionAsync(Matrix matrix, IAccelerator accelerator, float tolerance = 1e-10f, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);
            
            if (!matrix.IsSquare)
            {
                throw new ArgumentException("Matrix must be square for Jordan decomposition.");
            }
            
            // This is a simplified implementation - full Jordan decomposition is complex
            // For most practical purposes, eigenvalue decomposition or Schur decomposition is sufficient
            var (eigenvalues, eigenvectors) = await MatrixDecomposition.EigenDecompositionAsync(matrix, accelerator, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            var n = matrix.Rows;
            var j = new Matrix(n, n);
            
            // Create Jordan blocks (simplified - assumes distinct eigenvalues)
            for (var i = 0; i < n; i++)
            {
                j[i, i] = eigenvalues[i, 0];
            }
            
            return (eigenvectors, j);
        }

        /// <summary>
        /// Computes the matrix exponential using Padé approximation with scaling and squaring.
        /// </summary>
        /// <param name="matrix">Input square matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matrix exponential exp(A).</returns>
        public static async Task<Matrix> MatrixExponentialAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);
            
            if (!matrix.IsSquare)
            {
                throw new ArgumentException("Matrix must be square for matrix exponential.");
            }
            
            var n = matrix.Rows;
            
            // Scale matrix by power of 2 to reduce norm
            var norm = MatrixStatistics.OneNorm(matrix);
            var s = Math.Max(0, (int)Math.Ceiling(Math.Log2(norm)));
            var scaledMatrix = new Matrix(n, n);
            var scaleFactor = (float)Math.Pow(2, -s);
            
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    scaledMatrix[i, j] = matrix[i, j] * scaleFactor;
                }
            }
            
            // Compute Padé approximation
            var result = await ComputePadeApproximation(scaledMatrix, accelerator, cancellationToken).ConfigureAwait(false);
            
            // Square the result s times to undo scaling
            for (var i = 0; i < s; i++)
            {
                result = await MatrixOperations.MultiplyAsync(result, result, accelerator, cancellationToken).ConfigureAwait(false);
            }
            
            return result;
        }

        /// <summary>
        /// Computes the matrix logarithm using inverse scaling and squaring with Padé approximation.
        /// </summary>
        /// <param name="matrix">Input square matrix (must be invertible with eigenvalues not on negative real axis).</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matrix logarithm log(A).</returns>
        public static async Task<Matrix> MatrixLogarithmAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);
            
            if (!matrix.IsSquare)
            {
                throw new ArgumentException("Matrix must be square for matrix logarithm.");
            }
            
            // Use eigenvalue decomposition for matrix logarithm
            var (eigenvalues, eigenvectors) = await MatrixDecomposition.EigenDecompositionAsync(matrix, accelerator, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            // Compute log of eigenvalues
            var logEigenvalues = new Matrix(eigenvalues.Rows, eigenvalues.Columns);
            for (var i = 0; i < eigenvalues.Rows; i++)
            {
                var eigenvalue = eigenvalues[i, 0];
                if (eigenvalue <= 0)
                {
                    throw new InvalidOperationException("Matrix logarithm is not defined for matrices with non-positive eigenvalues.");
                }
                logEigenvalues[i, 0] = (float)Math.Log(eigenvalue);
            }
            
            // Reconstruct matrix: P * diag(log(λ)) * P^(-1)
            var eigenvectorsInverse = await MatrixOperations.InverseAsync(eigenvectors, accelerator, cancellationToken).ConfigureAwait(false);
            var logEigenMatrix = new Matrix(matrix.Rows, matrix.Columns);
            
            // Create diagonal matrix of log eigenvalues
            for (var i = 0; i < logEigenvalues.Rows; i++)
            {
                logEigenMatrix[i, i] = logEigenvalues[i, 0];
            }
            
            var temp = await MatrixOperations.MultiplyAsync(eigenvectors, logEigenMatrix, accelerator, cancellationToken).ConfigureAwait(false);
            var result = await MatrixOperations.MultiplyAsync(temp, eigenvectorsInverse, accelerator, cancellationToken).ConfigureAwait(false);
            
            return result;
        }

        /// <summary>
        /// Computes a matrix power A^p using eigenvalue decomposition.
        /// </summary>
        /// <param name="matrix">Input square matrix.</param>
        /// <param name="power">Power to raise the matrix to.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matrix power A^p.</returns>
        public static async Task<Matrix> MatrixPowerAsync(Matrix matrix, float power, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);
            
            if (!matrix.IsSquare)
            {
                throw new ArgumentException("Matrix must be square for matrix power.");
            }
            
            // Special cases
            if (Math.Abs(power) < 1e-10f)
            {
                return Matrix.Identity(matrix.Rows);
            }
            
            if (Math.Abs(power - 1.0f) < 1e-10f)
            {
                return matrix.Clone();
            }
            
            // Use eigenvalue decomposition
            var (eigenvalues, eigenvectors) = await MatrixDecomposition.EigenDecompositionAsync(matrix, accelerator, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            // Compute eigenvalues^power
            var poweredEigenvalues = new Matrix(eigenvalues.Rows, eigenvalues.Columns);
            for (var i = 0; i < eigenvalues.Rows; i++)
            {
                var eigenvalue = eigenvalues[i, 0];
                if (eigenvalue < 0 && Math.Abs(power - Math.Round(power)) > 1e-10f)
                {
                    throw new InvalidOperationException("Non-integer powers of matrices with negative eigenvalues are not supported in real arithmetic.");
                }
                poweredEigenvalues[i, 0] = (float)Math.Pow(eigenvalue, power);
            }
            
            // Reconstruct matrix: P * diag(λ^p) * P^(-1)
            var eigenvectorsInverse = await MatrixOperations.InverseAsync(eigenvectors, accelerator, cancellationToken).ConfigureAwait(false);
            var poweredEigenMatrix = new Matrix(matrix.Rows, matrix.Columns);
            
            // Create diagonal matrix of powered eigenvalues
            for (var i = 0; i < poweredEigenvalues.Rows; i++)
            {
                poweredEigenMatrix[i, i] = poweredEigenvalues[i, 0];
            }
            
            var temp = await MatrixOperations.MultiplyAsync(eigenvectors, poweredEigenMatrix, accelerator, cancellationToken).ConfigureAwait(false);
            var result = await MatrixOperations.MultiplyAsync(temp, eigenvectorsInverse, accelerator, cancellationToken).ConfigureAwait(false);
            
            return result;
        }

        /// <summary>
        /// Computes the square root of a positive semidefinite matrix using eigenvalue decomposition.
        /// </summary>
        /// <param name="matrix">Input positive semidefinite matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matrix square root.</returns>
        public static async Task<Matrix> MatrixSquareRootAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            return await MatrixPowerAsync(matrix, 0.5f, accelerator, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Computes the matrix sign function using the Newton iteration.
        /// </summary>
        /// <param name="matrix">Input square matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="maxIterations">Maximum number of iterations.</param>
        /// <param name="tolerance">Convergence tolerance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matrix sign function.</returns>
        public static async Task<Matrix> MatrixSignAsync(Matrix matrix, IAccelerator accelerator, int maxIterations = 50, float tolerance = 1e-10f, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);
            
            if (!matrix.IsSquare)
            {
                throw new ArgumentException("Matrix must be square for matrix sign function.");
            }
            
            var x = matrix.Clone();
            
            for (var iter = 0; iter < maxIterations; iter++)
            {
                var xInverse = await MatrixOperations.InverseAsync(x, accelerator, cancellationToken).ConfigureAwait(false);
                var xNew = await MatrixOperations.AddAsync(x, xInverse, accelerator, cancellationToken).ConfigureAwait(false);
                
                // Scale by 0.5
                for (var i = 0; i < xNew.Rows; i++)
                {
                    for (var j = 0; j < xNew.Columns; j++)
                    {
                        xNew[i, j] *= 0.5f;
                    }
                }
                
                // Check convergence
                var diff = await MatrixOperations.SubtractAsync(xNew, x, accelerator, cancellationToken).ConfigureAwait(false);
                var diffNorm = MatrixStatistics.FrobeniusNorm(diff);
                
                x = xNew;
                
                if (diffNorm < tolerance)
                {
                    break;
                }

            }
            
            return x;
        }

        // Private helper methods
        private static async Task<Matrix> ComputePadeApproximation(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken)
        {
            var n = matrix.Rows;
            var identity = Matrix.Identity(n);
            
            // Use (6,6) Padé approximation
            var powers = new Matrix[7];
            powers[0] = identity;
            powers[1] = matrix;
            
            // Compute powers of matrix
            for (var i = 2; i <= 6; i++)
            {
                powers[i] = await MatrixOperations.MultiplyAsync(powers[i - 1], matrix, accelerator, cancellationToken).ConfigureAwait(false);
            }
            
            // Numerator coefficients for (6,6) Padé approximation
            float[] c = { 1.0f, 1.0f, 0.5f, 1.0f/6.0f, 1.0f/24.0f, 1.0f/120.0f, 1.0f/720.0f };
            
            // Compute numerator
            var numerator = new Matrix(n, n);
            for (var i = 0; i <= 6; i++)
            {
                for (var row = 0; row < n; row++)
                {
                    for (var col = 0; col < n; col++)
                    {
                        numerator[row, col] += c[i] * powers[i][row, col];
                    }
                }
            }
            
            // Compute denominator (same coefficients but alternating signs)
            var denominator = new Matrix(n, n);
            for (var i = 0; i <= 6; i++)
            {
                var sign = (i % 2 == 0) ? 1.0f : -1.0f;
                for (var row = 0; row < n; row++)
                {
                    for (var col = 0; col < n; col++)
                    {
                        denominator[row, col] += sign * c[i] * powers[i][row, col];
                    }
                }
            }
            
            // Solve denominator * result = numerator
            var result = await MatrixSolvers.SolveAsync(denominator, numerator, accelerator, cancellationToken).ConfigureAwait(false);
            
            return result;
        }
    }
}