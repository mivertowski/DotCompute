// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.LinearAlgebra.Operations;

/// <summary>
/// LU decomposition operations for solving linear systems.
/// Implements LU decomposition with partial pivoting for numerical stability.
/// </summary>
public static class LuDecomposition
{
    private const float NumericalTolerance = 1e-10f;

    /// <summary>
    /// Performs LU decomposition with partial pivoting.
    /// Decomposes matrix A into L (lower triangular) and U (upper triangular)
    /// such that P*A = L*U where P is a permutation matrix.
    /// </summary>
    /// <param name="matrix">Input matrix to decompose (must be square)</param>
    /// <param name="accelerator">Compute accelerator for GPU acceleration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing (L, U, P) where P is the permutation array</returns>
    /// <exception cref="ArgumentException">Thrown when matrix is not square</exception>
    /// <exception cref="InvalidOperationException">Thrown when matrix is singular</exception>
    public static async Task<(Matrix L, Matrix U, int[] P)> DecomposeAsync(
        Matrix matrix, 
        IAccelerator accelerator, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        if (!matrix.IsSquare)
        {
            throw new ArgumentException("Matrix must be square for LU decomposition.");
        }

        var n = matrix.Rows;
        var l = Matrix.Identity(n);
        var u = matrix.Clone();
        var p = new int[n];

        // Initialize permutation array
        for (var i = 0; i < n; i++)
        {
            p[i] = i;
        }

        return await Task.Run(() =>
        {
            for (var k = 0; k < n - 1; k++)
            {
                // Find pivot
                var pivotRow = FindPivotRow(u, k, n);

                // Swap rows if needed
                if (pivotRow != k)
                {
                    SwapRows(u, k, pivotRow, n);
                    SwapRowsInL(l, k, pivotRow);
                    (p[k], p[pivotRow]) = (p[pivotRow], p[k]);
                }

                // Check for singularity
                if (Math.Abs(u[k, k]) < NumericalTolerance)
                {
                    throw new InvalidOperationException("Matrix is singular or nearly singular.");
                }

                // Eliminate column
                EliminateColumn(l, u, k, n);
            }

            return (l, u, p);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Solves the linear system Ax = b using LU decomposition.
    /// First performs LU decomposition, then solves using forward and backward substitution.
    /// </summary>
    /// <param name="matrix">Coefficient matrix A</param>
    /// <param name="rhs">Right-hand side vector b</param>
    /// <param name="accelerator">Compute accelerator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Solution vector x</returns>
    public static async Task<Matrix> SolveAsync(
        Matrix matrix, 
        Matrix rhs, 
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(rhs);

        if (!matrix.IsSquare)
        {
            throw new ArgumentException("Coefficient matrix must be square.");
        }

        if (matrix.Rows != rhs.Rows)
        {
            throw new ArgumentException("Matrix and RHS dimensions must match.");
        }

        // Perform LU decomposition
        var (l, u, p) = await DecomposeAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

        return await Task.Run(() =>
        {
            var n = matrix.Rows;
            var permutedRhs = new Matrix(n, rhs.Columns);

            // Apply permutation to RHS
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < rhs.Columns; j++)
                {
                    permutedRhs[i, j] = rhs[p[i], j];
                }
            }

            var solution = new Matrix(n, rhs.Columns);

            // Solve for each column of RHS
            for (var col = 0; col < rhs.Columns; col++)
            {
                // Forward substitution: Ly = Pb
                var y = new float[n];
                for (var i = 0; i < n; i++)
                {
                    y[i] = permutedRhs[i, col];
                    for (var j = 0; j < i; j++)
                    {
                        y[i] -= l[i, j] * y[j];
                    }
                    y[i] /= l[i, i];
                }

                // Backward substitution: Ux = y
                var x = new float[n];
                for (var i = n - 1; i >= 0; i--)
                {
                    x[i] = y[i];
                    for (var j = i + 1; j < n; j++)
                    {
                        x[i] -= u[i, j] * x[j];
                    }
                    x[i] /= u[i, i];
                }

                // Store solution
                for (var i = 0; i < n; i++)
                {
                    solution[i, col] = x[i];
                }
            }

            return solution;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Computes the determinant of a matrix using LU decomposition.
    /// The determinant is computed as the product of diagonal elements of U,
    /// with sign adjustment for row swaps.
    /// </summary>
    /// <param name="matrix">Input square matrix</param>
    /// <param name="accelerator">Compute accelerator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Determinant of the matrix</returns>
    public static async Task<float> DeterminantAsync(
        Matrix matrix,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        if (!matrix.IsSquare)
        {
            throw new ArgumentException("Matrix must be square to compute determinant.");
        }

        try
        {
            var (l, u, p) = await DecomposeAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

            return await Task.Run(() =>
            {
                // Count row swaps to determine sign
                var swapCount = CountPermutationSwaps(p);
                var sign = (swapCount % 2 == 0) ? 1.0f : -1.0f;

                // Determinant is product of diagonal elements of U
                var det = sign;
                for (var i = 0; i < u.Rows; i++)
                {
                    det *= u[i, i];
                }

                return det;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Singular matrix has determinant 0
            return 0.0f;
        }
    }

    /// <summary>
    /// Computes the inverse of a matrix using LU decomposition.
    /// Solves AX = I where I is the identity matrix.
    /// </summary>
    /// <param name="matrix">Input square matrix</param>
    /// <param name="accelerator">Compute accelerator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Inverse matrix</returns>
    /// <exception cref="InvalidOperationException">Thrown when matrix is singular</exception>
    public static async Task<Matrix> InverseAsync(
        Matrix matrix,
        IAccelerator accelerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        if (!matrix.IsSquare)
        {
            throw new ArgumentException("Matrix must be square to compute inverse.");
        }

        var n = matrix.Rows;
        var identity = Matrix.Identity(n);

        // Solve AX = I
        return await SolveAsync(matrix, identity, accelerator, cancellationToken).ConfigureAwait(false);
    }

    #region Private Helper Methods

    private static int FindPivotRow(Matrix u, int k, int n)
    {
        var pivotRow = k;
        var maxVal = Math.Abs(u[k, k]);

        for (var i = k + 1; i < n; i++)
        {
            var val = Math.Abs(u[i, k]);
            if (val > maxVal)
            {
                maxVal = val;
                pivotRow = i;
            }
        }

        return pivotRow;
    }

    private static void SwapRows(Matrix matrix, int row1, int row2, int cols)
    {
        for (var j = 0; j < cols; j++)
        {
            (matrix[row1, j], matrix[row2, j]) = (matrix[row2, j], matrix[row1, j]);
        }
    }

    private static void SwapRowsInL(Matrix l, int row1, int row2)
    {
        // Only swap the computed part of L (below diagonal)
        for (var j = 0; j < Math.Min(row1, row2); j++)
        {
            (l[row1, j], l[row2, j]) = (l[row2, j], l[row1, j]);
        }
    }

    private static void EliminateColumn(Matrix l, Matrix u, int k, int n)
    {
        for (var i = k + 1; i < n; i++)
        {
            var multiplier = u[i, k] / u[k, k];
            l[i, k] = multiplier;

            // Eliminate
            for (var j = k + 1; j < n; j++)
            {
                u[i, j] -= multiplier * u[k, j];
            }
            u[i, k] = 0; // Set to zero explicitly
        }
    }

    private static int CountPermutationSwaps(int[] permutation)
    {
        var n = permutation.Length;
        var visited = new bool[n];
        var swapCount = 0;

        for (var i = 0; i < n; i++)
        {
            if (visited[i] || permutation[i] == i)
                continue;

            // Count cycle length
            var cycleLength = 0;
            var current = i;

            while (!visited[current])
            {
                visited[current] = true;
                current = permutation[current];
                cycleLength++;
            }

            // A cycle of length k contributes k-1 swaps
            swapCount += cycleLength - 1;
        }

        return swapCount;
    }

    #endregion
}