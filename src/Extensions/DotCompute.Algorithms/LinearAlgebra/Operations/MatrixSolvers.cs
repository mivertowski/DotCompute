
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

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

        /// <summary>
        /// Solves the upper triangular system Ux = b using back substitution.
        /// </summary>
        /// <param name="u">Upper triangular matrix.</param>
        /// <param name="b">Right-hand side vector.</param>
        /// <param name="accelerator">Compute accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Solution vector x.</returns>
        public static async Task<Matrix> SolveUpperTriangularAsync(Matrix u, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(u);
            ArgumentNullException.ThrowIfNull(b);
            cancellationToken.ThrowIfCancellationRequested();

            if (!u.IsSquare)
            {
                throw new ArgumentException("Matrix U must be square.");
            }

            if (u.Rows != b.Rows)
            {
                throw new ArgumentException("Matrix dimensions incompatible for solving Ux = b.");
            }

            return await Task.Run(() =>
            {
                var n = u.Rows;
                var x = new Matrix(n, b.Columns);

                // Back substitution for each column of b
                for (var col = 0; col < b.Columns; col++)
                {
                    for (var i = n - 1; i >= 0; i--)
                    {
                        // Check for zero diagonal element
                        if (Math.Abs(u[i, i]) < 1e-10f)
                        {
                            throw new ArgumentException($"Upper triangular matrix has zero diagonal element at position ({i},{i}).");
                        }

                        var sum = b[i, col];
                        for (var j = i + 1; j < n; j++)
                        {
                            sum -= u[i, j] * x[j, col];
                        }
                        x[i, col] = sum / u[i, i];
                    }
                }

                return x;
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Solves the lower triangular system Lx = b using forward substitution.
        /// </summary>
        /// <param name="l">Lower triangular matrix.</param>
        /// <param name="b">Right-hand side vector.</param>
        /// <param name="accelerator">Compute accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Solution vector x.</returns>
        public static async Task<Matrix> SolveLowerTriangularAsync(Matrix l, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(l);
            ArgumentNullException.ThrowIfNull(b);
            cancellationToken.ThrowIfCancellationRequested();

            if (!l.IsSquare)
            {
                throw new ArgumentException("Matrix L must be square.");
            }

            if (l.Rows != b.Rows)
            {
                throw new ArgumentException("Matrix dimensions incompatible for solving Lx = b.");
            }

            return await Task.Run(() =>
            {
                var n = l.Rows;
                var x = new Matrix(n, b.Columns);

                // Forward substitution for each column of b
                for (var col = 0; col < b.Columns; col++)
                {
                    for (var i = 0; i < n; i++)
                    {
                        // Check for zero diagonal element
                        if (Math.Abs(l[i, i]) < 1e-10f)
                        {
                            throw new ArgumentException($"Lower triangular matrix has zero diagonal element at position ({i},{i}).");
                        }

                        var sum = b[i, col];
                        for (var j = 0; j < i; j++)
                        {
                            sum -= l[i, j] * x[j, col];
                        }
                        x[i, col] = sum / l[i, i];
                    }
                }

                return x;
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Solves a positive definite system Ax = b using Cholesky decomposition.
        /// </summary>
        /// <param name="a">Positive definite coefficient matrix.</param>
        /// <param name="b">Right-hand side vector.</param>
        /// <param name="accelerator">Compute accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Solution vector x.</returns>
        public static async Task<Matrix> SolveCholeskyAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            cancellationToken.ThrowIfCancellationRequested();

            if (!a.IsSquare)
            {
                throw new ArgumentException("Matrix A must be square.");
            }

            if (a.Rows != b.Rows)
            {
                throw new ArgumentException("Matrix dimensions incompatible for solving Ax = b.");
            }

            // Perform Cholesky decomposition: A = L * L^T
            var l = await MatrixDecomposition.CholeskyDecompositionAsync(a, accelerator, cancellationToken).ConfigureAwait(false);

            // Solve L * y = b
            var y = await SolveLowerTriangularAsync(l, b, accelerator, cancellationToken).ConfigureAwait(false);

            // Solve L^T * x = y (transpose L to get upper triangular)
            var lt = TransposeMatrix(l);
            return await SolveUpperTriangularAsync(lt, y, accelerator, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Solves a tridiagonal system using the Thomas algorithm.
        /// </summary>
        /// <param name="lower">Lower diagonal (size n-1).</param>
        /// <param name="main">Main diagonal (size n).</param>
        /// <param name="upper">Upper diagonal (size n-1).</param>
        /// <param name="b">Right-hand side vector.</param>
        /// <param name="accelerator">Compute accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Solution vector x.</returns>
        public static async Task<Matrix> SolveTridiagonalAsync(float[] lower, float[] main, float[] upper, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(lower);
            ArgumentNullException.ThrowIfNull(main);
            ArgumentNullException.ThrowIfNull(upper);
            ArgumentNullException.ThrowIfNull(b);
            cancellationToken.ThrowIfCancellationRequested();

            var n = main.Length;

            if (lower.Length != n - 1)
            {
                throw new ArgumentException("Lower diagonal must have size n-1.");
            }

            if (upper.Length != n - 1)
            {
                throw new ArgumentException("Upper diagonal must have size n-1.");
            }

            if (b.Rows != n)
            {
                throw new ArgumentException("Right-hand side vector must have size n.");
            }

            return await Task.Run(() =>
            {
                var x = new Matrix(n, b.Columns);

                // Thomas algorithm (tridiagonal matrix algorithm)
                for (var col = 0; col < b.Columns; col++)
                {
                    // Forward elimination
                    var c = new float[n];
                    var d = new float[n];

                    c[0] = upper[0] / main[0];
                    d[0] = b[0, col] / main[0];

                    for (var i = 1; i < n; i++)
                    {
                        var denom = main[i] - (i > 0 ? lower[i - 1] * c[i - 1] : 0);

                        if (Math.Abs(denom) < 1e-10f)
                        {
                            throw new InvalidOperationException($"Tridiagonal system is singular at row {i}.");
                        }

                        if (i < n - 1)
                        {
                            c[i] = upper[i] / denom;
                        }

                        d[i] = (b[i, col] - (i > 0 ? lower[i - 1] * d[i - 1] : 0)) / denom;
                    }

                    // Back substitution
                    x[n - 1, col] = d[n - 1];
                    for (var i = n - 2; i >= 0; i--)
                    {
                        x[i, col] = d[i] - c[i] * x[i + 1, col];
                    }
                }

                return x;
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Solves a banded matrix system using LU decomposition optimized for band structure.
        /// </summary>
        /// <param name="a">Banded coefficient matrix.</param>
        /// <param name="b">Right-hand side vector.</param>
        /// <param name="bandwidth">Half-bandwidth of the matrix.</param>
        /// <param name="accelerator">Compute accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Solution vector x.</returns>
        public static async Task<Matrix> SolveBandedAsync(Matrix a, Matrix b, int bandwidth, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            cancellationToken.ThrowIfCancellationRequested();

            if (!a.IsSquare)
            {
                throw new ArgumentException("Matrix A must be square.");
            }

            if (a.Rows != b.Rows)
            {
                throw new ArgumentException("Matrix dimensions incompatible for solving Ax = b.");
            }

            if (bandwidth < 0 || bandwidth >= a.Rows)
            {
                throw new ArgumentException($"Bandwidth must be between 0 and {a.Rows - 1}.");
            }

            return await Task.Run(() =>
            {
                var n = a.Rows;
                var lu = a.Clone();

                // Banded LU decomposition (only process elements within the band)
                for (var k = 0; k < n - 1; k++)
                {
                    if (Math.Abs(lu[k, k]) < 1e-10f)
                    {
                        throw new InvalidOperationException($"Banded matrix is singular at position ({k},{k}).");
                    }

                    // Only process rows within lower bandwidth
                    var rowEnd = Math.Min(k + bandwidth + 1, n);
                    for (var i = k + 1; i < rowEnd; i++)
                    {
                        var multiplier = lu[i, k] / lu[k, k];
                        lu[i, k] = multiplier;

                        // Only process columns within upper bandwidth
                        var colEnd = Math.Min(k + bandwidth + 1, n);
                        for (var j = k + 1; j < colEnd; j++)
                        {
                            lu[i, j] -= multiplier * lu[k, j];
                        }
                    }
                }

                // Forward substitution (Ly = b)
                var y = new Matrix(n, b.Columns);
                for (var col = 0; col < b.Columns; col++)
                {
                    for (var i = 0; i < n; i++)
                    {
                        var sum = b[i, col];
                        var startJ = Math.Max(0, i - bandwidth);
                        for (var j = startJ; j < i; j++)
                        {
                            sum -= lu[i, j] * y[j, col];
                        }
                        y[i, col] = sum;
                    }
                }

                // Back substitution (Ux = y)
                var x = new Matrix(n, b.Columns);
                for (var col = 0; col < b.Columns; col++)
                {
                    for (var i = n - 1; i >= 0; i--)
                    {
                        if (Math.Abs(lu[i, i]) < 1e-10f)
                        {
                            throw new InvalidOperationException($"Banded matrix U is singular at position ({i},{i}).");
                        }

                        var sum = y[i, col];
                        var endJ = Math.Min(n, i + bandwidth + 1);
                        for (var j = i + 1; j < endJ; j++)
                        {
                            sum -= lu[i, j] * x[j, col];
                        }
                        x[i, col] = sum / lu[i, i];
                    }
                }

                return x;
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Solves an overdetermined system Ax = b in the least squares sense using QR decomposition.
        /// </summary>
        /// <param name="a">Coefficient matrix (m x n, m >= n).</param>
        /// <param name="b">Right-hand side vector (m x 1).</param>
        /// <param name="accelerator">Compute accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Least squares solution vector x (n x 1).</returns>
        /// <remarks>
        /// This method handles rank-deficient matrices by using a tolerance-based approach.
        /// Diagonal elements of R below the tolerance are treated as zero and corresponding
        /// solution elements are set to zero.
        /// </remarks>
        public static async Task<Matrix> SolveLeastSquaresAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            cancellationToken.ThrowIfCancellationRequested();

            if (a.Rows < a.Columns)
            {
                throw new ArgumentException("Matrix A must have at least as many rows as columns for least squares.");
            }

            if (a.Rows != b.Rows)
            {
                throw new ArgumentException("Matrix dimensions incompatible for least squares solution.");
            }

            // Use QR decomposition: A = Q * R
            var (q, r) = await MatrixDecomposition.QRDecompositionAsync(a, accelerator, cancellationToken).ConfigureAwait(false);

            // Compute Q^T * b
            var qtb = await Task.Run(() =>
            {
                var result = new Matrix(q.Columns, b.Columns);
                for (var i = 0; i < q.Columns; i++)
                {
                    for (var j = 0; j < b.Columns; j++)
                    {
                        float sum = 0;
                        for (var k = 0; k < q.Rows; k++)
                        {
                            sum += q[k, i] * b[k, j];
                        }
                        result[i, j] = sum;
                    }
                }
                return result;
            }, cancellationToken).ConfigureAwait(false);

            // Solve R * x = Q^T * b using back substitution with tolerance for rank deficiency
            var n = a.Columns;
            var x = new Matrix(n, b.Columns);
            const float tolerance = 1e-10f;

            await Task.Run(() =>
            {
                for (var col = 0; col < b.Columns; col++)
                {
                    // Back substitution with rank-deficiency handling
                    for (var i = n - 1; i >= 0; i--)
                    {
                        // Check if diagonal element is effectively zero (rank deficient)
                        if (Math.Abs(r[i, i]) < tolerance)
                        {
                            // Set corresponding solution element to zero for rank-deficient case
                            x[i, col] = 0;
                            continue;
                        }

                        var sum = qtb[i, col];
                        for (var j = i + 1; j < n; j++)
                        {
                            sum -= r[i, j] * x[j, col];
                        }
                        x[i, col] = sum / r[i, i];
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return x;
        }

        /// <summary>
        /// Solves the linear system Ax = b using Jacobi iterative method.
        /// </summary>
        /// <param name="a">Coefficient matrix (must be strictly diagonally dominant for convergence).</param>
        /// <param name="b">Right-hand side vector.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="maxIterations">Maximum number of iterations (default: 1000).</param>
        /// <param name="tolerance">Convergence tolerance for residual norm (default: 1e-6).</param>
        /// <param name="initialGuess">Optional initial guess for solution.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Solution vector.</returns>
        /// <remarks>
        /// The Jacobi method converges if the matrix A is strictly diagonally dominant,
        /// i.e., |a_ii| > sum(|a_ij|) for all i != j.
        /// The method uses the decomposition A = D + R where D is diagonal and R is the remainder.
        /// Each iteration computes: x^(k+1) = D^(-1)(b - R*x^(k))
        /// </remarks>
        public static async Task<Matrix> SolveJacobiAsync(
            Matrix a,
            Matrix b,
            IAccelerator accelerator,
            int maxIterations = 1000,
            float tolerance = 1e-6f,
            Matrix? initialGuess = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            ArgumentNullException.ThrowIfNull(accelerator);

            if (!a.IsSquare)
            {
                throw new ArgumentException("Matrix A must be square.");
            }

            if (a.Rows != b.Rows)
            {
                throw new ArgumentException("Matrix dimensions incompatible for solving Ax = b.");
            }

            if (b.Columns != 1)
            {
                throw new ArgumentException("Right-hand side must be a column vector.");
            }

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIterations);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tolerance);

            var n = a.Rows;

            // Check diagonal elements are non-zero
            for (var i = 0; i < n; i++)
            {
                if (Math.Abs(a[i, i]) < 1e-10f)
                {
                    throw new ArgumentException($"Matrix has zero or near-zero diagonal element at index {i}. Jacobi method requires non-zero diagonal.");
                }
            }

            // Initialize solution vector
            var x = initialGuess?.Clone() ?? new Matrix(n, 1);
            var xNew = new Matrix(n, 1);

            for (var iter = 0; iter < maxIterations; iter++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Compute x^(k+1) = D^(-1)(b - R*x^(k))
                await Task.Run(() =>
                {
                    for (var i = 0; i < n; i++)
                    {
                        var sum = b[i, 0];

                        // Subtract off-diagonal terms: sum = b_i - sum(a_ij * x_j) for j != i
                        for (var j = 0; j < n; j++)
                        {
                            if (j != i)
                            {
                                sum -= a[i, j] * x[j, 0];
                            }
                        }

                        // Divide by diagonal element: x_i = sum / a_ii
                        xNew[i, 0] = sum / a[i, i];
                    }
                }, cancellationToken).ConfigureAwait(false);

                // Check convergence by computing residual norm ||b - Ax||
                var ax = await MatrixOperations.MultiplyAsync(a, xNew, accelerator, cancellationToken).ConfigureAwait(false);
                var residual = await MatrixOperations.SubtractAsync(b, ax, accelerator, cancellationToken).ConfigureAwait(false);
                var residualNorm = ComputeVector2Norm(residual);

                // Update solution
                x = xNew.Clone();

                if (residualNorm < tolerance)
                {
                    return x;
                }

                // Prepare for next iteration
                xNew = new Matrix(n, 1);
            }

            // Check if we achieved acceptable accuracy
            var finalAx = await MatrixOperations.MultiplyAsync(a, x, accelerator, cancellationToken).ConfigureAwait(false);
            var finalResidual = await MatrixOperations.SubtractAsync(b, finalAx, accelerator, cancellationToken).ConfigureAwait(false);
            var finalNorm = ComputeVector2Norm(finalResidual);

            if (finalNorm > tolerance * 10) // Allow some leeway
            {
                throw new InvalidOperationException(
                    $"Jacobi method did not converge after {maxIterations} iterations. " +
                    $"Final residual norm: {finalNorm:E3}. " +
                    $"Matrix may not be diagonally dominant or tolerance is too strict.");
            }

            return x;
        }

        /// <summary>
        /// Solves the linear system Ax = b using Gauss-Seidel iterative method.
        /// </summary>
        /// <param name="a">Coefficient matrix (must be strictly diagonally dominant for convergence).</param>
        /// <param name="b">Right-hand side vector.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="maxIterations">Maximum number of iterations (default: 1000).</param>
        /// <param name="tolerance">Convergence tolerance for residual norm (default: 1e-6).</param>
        /// <param name="initialGuess">Optional initial guess for solution.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Solution vector.</returns>
        /// <remarks>
        /// The Gauss-Seidel method is similar to Jacobi but uses updated values immediately.
        /// It typically converges faster than Jacobi for the same system.
        /// The method uses the decomposition A = L + D + U where L is lower, D is diagonal, U is upper.
        /// Each iteration computes: x_i^(k+1) = (b_i - sum(a_ij * x_j^(k+1)) for j &lt; i - sum(a_ij * x_j^(k)) for j &gt; i) / a_ii
        /// Convergence is guaranteed for strictly diagonally dominant or symmetric positive definite matrices.
        /// </remarks>
        public static async Task<Matrix> SolveGaussSeidelAsync(
            Matrix a,
            Matrix b,
            IAccelerator accelerator,
            int maxIterations = 1000,
            float tolerance = 1e-6f,
            Matrix? initialGuess = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            ArgumentNullException.ThrowIfNull(accelerator);

            if (!a.IsSquare)
            {
                throw new ArgumentException("Matrix A must be square.");
            }

            if (a.Rows != b.Rows)
            {
                throw new ArgumentException("Matrix dimensions incompatible for solving Ax = b.");
            }

            if (b.Columns != 1)
            {
                throw new ArgumentException("Right-hand side must be a column vector.");
            }

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIterations);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tolerance);

            var n = a.Rows;

            // Check diagonal elements are non-zero
            for (var i = 0; i < n; i++)
            {
                if (Math.Abs(a[i, i]) < 1e-10f)
                {
                    throw new ArgumentException($"Matrix has zero or near-zero diagonal element at index {i}. Gauss-Seidel method requires non-zero diagonal.");
                }
            }

            // Initialize solution vector
            var x = initialGuess?.Clone() ?? new Matrix(n, 1);

            for (var iter = 0; iter < maxIterations; iter++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Compute updated solution using most recent values
                await Task.Run(() =>
                {
                    for (var i = 0; i < n; i++)
                    {
                        var sum = b[i, 0];

                        // Use updated values for j < i (already computed in this iteration)
                        for (var j = 0; j < i; j++)
                        {
                            sum -= a[i, j] * x[j, 0];
                        }

                        // Use old values for j > i (not yet computed in this iteration)
                        for (var j = i + 1; j < n; j++)
                        {
                            sum -= a[i, j] * x[j, 0];
                        }

                        // Update x_i immediately
                        x[i, 0] = sum / a[i, i];
                    }
                }, cancellationToken).ConfigureAwait(false);

                // Check convergence every few iterations to avoid overhead
                if (iter % 5 == 0 || iter == maxIterations - 1)
                {
                    var ax = await MatrixOperations.MultiplyAsync(a, x, accelerator, cancellationToken).ConfigureAwait(false);
                    var residual = await MatrixOperations.SubtractAsync(b, ax, accelerator, cancellationToken).ConfigureAwait(false);
                    var residualNorm = ComputeVector2Norm(residual);

                    if (residualNorm < tolerance)
                    {
                        return x;
                    }
                }
            }

            // Final convergence check
            var finalAx = await MatrixOperations.MultiplyAsync(a, x, accelerator, cancellationToken).ConfigureAwait(false);
            var finalResidual = await MatrixOperations.SubtractAsync(b, finalAx, accelerator, cancellationToken).ConfigureAwait(false);
            var finalNorm = ComputeVector2Norm(finalResidual);

            if (finalNorm > tolerance * 10)
            {
                throw new InvalidOperationException(
                    $"Gauss-Seidel method did not converge after {maxIterations} iterations. " +
                    $"Final residual norm: {finalNorm:E3}. " +
                    $"Matrix may not be diagonally dominant or tolerance is too strict.");
            }

            return x;
        }

        /// <summary>
        /// Solves the linear system Ax = b using Conjugate Gradient iterative method.
        /// </summary>
        /// <param name="a">Coefficient matrix (must be symmetric positive definite for convergence).</param>
        /// <param name="b">Right-hand side vector.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="maxIterations">Maximum number of iterations (default: 1000).</param>
        /// <param name="tolerance">Convergence tolerance for residual norm (default: 1e-6).</param>
        /// <param name="initialGuess">Optional initial guess for solution.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Solution vector.</returns>
        /// <remarks>
        /// The Conjugate Gradient (CG) method is the most efficient iterative method for symmetric positive definite matrices.
        /// It minimizes the quadratic form f(x) = 0.5 * x^T A x - b^T x using conjugate directions.
        /// Theoretically converges in at most n iterations, but practical convergence typically occurs much faster.
        /// The method is highly effective for large sparse systems arising from discretized PDEs.
        ///
        /// Algorithm:
        /// 1. r_0 = b - A*x_0 (initial residual)
        /// 2. p_0 = r_0 (initial search direction)
        /// 3. For k = 0, 1, 2, ...:
        ///    - alpha_k = (r_k^T * r_k) / (p_k^T * A * p_k)
        ///    - x_(k+1) = x_k + alpha_k * p_k
        ///    - r_(k+1) = r_k - alpha_k * A * p_k
        ///    - if ||r_(k+1)|| &lt; tolerance, stop
        ///    - beta_k = (r_(k+1)^T * r_(k+1)) / (r_k^T * r_k)
        ///    - p_(k+1) = r_(k+1) + beta_k * p_k
        /// </remarks>
        public static async Task<Matrix> SolveConjugateGradientAsync(
            Matrix a,
            Matrix b,
            IAccelerator accelerator,
            int maxIterations = 1000,
            float tolerance = 1e-6f,
            Matrix? initialGuess = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            ArgumentNullException.ThrowIfNull(accelerator);

            if (!a.IsSquare)
            {
                throw new ArgumentException("Matrix A must be square.");
            }

            if (a.Rows != b.Rows)
            {
                throw new ArgumentException("Matrix dimensions incompatible for solving Ax = b.");
            }

            if (b.Columns != 1)
            {
                throw new ArgumentException("Right-hand side must be a column vector.");
            }

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIterations);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tolerance);

            var n = a.Rows;

            // Initialize solution vector
            var x = initialGuess?.Clone() ?? new Matrix(n, 1);

            // Compute initial residual: r = b - A*x
            var ax = await MatrixOperations.MultiplyAsync(a, x, accelerator, cancellationToken).ConfigureAwait(false);
            var r = await MatrixOperations.SubtractAsync(b, ax, accelerator, cancellationToken).ConfigureAwait(false);

            // Check if initial guess is already good enough
            var residualNorm = ComputeVector2Norm(r);
            if (residualNorm < tolerance)
            {
                return x;
            }

            // Initialize search direction: p = r
            var p = r.Clone();

            // Store r^T * r for later use
            var rsold = DotProduct(r, r);

            for (var iter = 0; iter < maxIterations; iter++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Compute A*p
                var ap = await MatrixOperations.MultiplyAsync(a, p, accelerator, cancellationToken).ConfigureAwait(false);

                // Compute step size: alpha = (r^T * r) / (p^T * A * p)
                var pAp = DotProduct(p, ap);

                if (Math.Abs(pAp) < 1e-14f)
                {
                    throw new InvalidOperationException(
                        "Conjugate Gradient method failed: p^T*A*p is nearly zero. " +
                        "Matrix A may not be positive definite.");
                }

                var alpha = rsold / pAp;

                // Update solution: x = x + alpha * p
                await Task.Run(() =>
                {
                    for (var i = 0; i < n; i++)
                    {
                        x[i, 0] += alpha * p[i, 0];
                    }
                }, cancellationToken).ConfigureAwait(false);

                // Update residual: r = r - alpha * A*p
                await Task.Run(() =>
                {
                    for (var i = 0; i < n; i++)
                    {
                        r[i, 0] -= alpha * ap[i, 0];
                    }
                }, cancellationToken).ConfigureAwait(false);

                // Check convergence
                residualNorm = ComputeVector2Norm(r);
                if (residualNorm < tolerance)
                {
                    return x;
                }

                // Compute new r^T * r
                var rsnew = DotProduct(r, r);

                // Compute improvement factor: beta = (r_new^T * r_new) / (r_old^T * r_old)
                var beta = rsnew / rsold;

                // Update search direction: p = r + beta * p
                await Task.Run(() =>
                {
                    for (var i = 0; i < n; i++)
                    {
                        p[i, 0] = r[i, 0] + beta * p[i, 0];
                    }
                }, cancellationToken).ConfigureAwait(false);

                // Update rsold for next iteration
                rsold = rsnew;
            }

            // Final convergence check
            var finalAx = await MatrixOperations.MultiplyAsync(a, x, accelerator, cancellationToken).ConfigureAwait(false);
            var finalResidual = await MatrixOperations.SubtractAsync(b, finalAx, accelerator, cancellationToken).ConfigureAwait(false);
            var finalNorm = ComputeVector2Norm(finalResidual);

            if (finalNorm > tolerance * 10)
            {
                throw new InvalidOperationException(
                    $"Conjugate Gradient method did not converge after {maxIterations} iterations. " +
                    $"Final residual norm: {finalNorm:E3}. " +
                    $"Matrix may not be symmetric positive definite or tolerance is too strict.");
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

        /// <summary>
        /// Computes the dot product of two column vectors.
        /// </summary>
        private static float DotProduct(Matrix a, Matrix b)
        {
            if (a.Rows != b.Rows || a.Columns != 1 || b.Columns != 1)
            {
                throw new ArgumentException("Both matrices must be column vectors of the same size.");
            }

            float sum = 0;
            for (var i = 0; i < a.Rows; i++)
            {
                sum += a[i, 0] * b[i, 0];
            }
            return sum;
        }

        private static Matrix TransposeMatrix(Matrix matrix)
        {
            var result = new Matrix(matrix.Columns, matrix.Rows);
            for (var i = 0; i < matrix.Rows; i++)
            {
                for (var j = 0; j < matrix.Columns; j++)
                {
                    result[j, i] = matrix[i, j];
                }
            }
            return result;
        }
    }
}
