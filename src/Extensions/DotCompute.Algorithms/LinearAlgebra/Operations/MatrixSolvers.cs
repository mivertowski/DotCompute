// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;

namespace DotCompute.Algorithms.LinearAlgebra.Operations
{
    /// <summary>
    /// Provides matrix linear system solvers.
    /// </summary>
    public static class MatrixSolvers
    {
        /// <summary>
        /// Solves the linear system Ax = b.
        /// </summary>
        public static async Task<Matrix> SolveAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);

            if (!a.IsSquare)
            {
                throw new ArgumentException("Matrix A must be square.");
            }

            if (a.Rows != b.Rows)
            {
                throw new ArgumentException("Matrix dimensions incompatible for solving Ax = b.");
            }

            // Use LU decomposition
            var (l, u, p) = await MatrixDecomposition.LUDecompositionAsync(a, accelerator, cancellationToken).ConfigureAwait(false);

            return await Task.Run(() => SolveLU(l, u, p, b), cancellationToken).ConfigureAwait(false);
        }

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
        {
            var x = await SolveAsync(a, b, accelerator, cancellationToken).ConfigureAwait(false);
            var originalA = a.Clone();

            for (var iter = 0; iter < maxRefinements; iter++)
            {
                // Compute residual r = b - Ax
                var ax = await MatrixOperations.MultiplyAsync(originalA, x, accelerator, cancellationToken).ConfigureAwait(false);
                var residual = await MatrixOperations.SubtractAsync(b, ax, accelerator, cancellationToken).ConfigureAwait(false);

                // Check convergence
                var residualNorm = ComputeVector2Norm(residual);
                if (residualNorm < tolerance)
                {
                    break;
                }

                // Solve A * correction = residual

                var correction = await SolveAsync(a, residual, accelerator, cancellationToken).ConfigureAwait(false);

                // Update solution: x = x + correction
                x = await MatrixOperations.AddAsync(x, correction, accelerator, cancellationToken).ConfigureAwait(false);
            }

            return x;
        }

        /// <summary>
        /// Solves a linear system using LU decomposition with forward and back substitution.
        /// </summary>
        /// <param name="l">Lower triangular matrix from LU decomposition.</param>
        /// <param name="u">Upper triangular matrix from LU decomposition.</param>
        /// <param name="p">Permutation array from LU decomposition.</param>
        /// <param name="b">Right-hand side matrix.</param>
        /// <returns>Solution matrix.</returns>
        public static Matrix SolveLU(Matrix l, Matrix u, int[] p, Matrix b)
        {
            var n = l.Rows;
            var x = new Matrix(n, b.Columns);

            // Apply permutation to b
            var pb = new Matrix(n, b.Columns);
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < b.Columns; j++)
                {
                    pb[i, j] = b[p[i], j];
                }
            }

            // Forward substitution: Ly = Pb
            var y = new Matrix(n, b.Columns);
            for (var col = 0; col < b.Columns; col++)
            {
                for (var i = 0; i < n; i++)
                {
                    var sum = pb[i, col];
                    for (var j = 0; j < i; j++)
                    {
                        sum -= l[i, j] * y[j, col];
                    }
                    y[i, col] = sum;
                }
            }

            // Back substitution: Ux = y
            for (var col = 0; col < b.Columns; col++)
            {
                for (var i = n - 1; i >= 0; i--)
                {
                    var sum = y[i, col];
                    for (var j = i + 1; j < n; j++)
                    {
                        sum -= u[i, j] * x[j, col];
                    }
                    x[i, col] = sum / u[i, i];
                }
            }

            return x;
        }

        // Helper methods
        private static float ComputeVector2Norm(Matrix vector)
        {
            float sum = 0;
            var data = vector.AsSpan();
            for (var i = 0; i < data.Length; i++)
            {
                sum += data[i] * data[i];
            }
            return (float)Math.Sqrt(sum);
        }
    }
}