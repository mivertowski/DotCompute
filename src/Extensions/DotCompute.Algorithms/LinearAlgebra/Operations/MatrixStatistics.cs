
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Algorithms.LinearAlgebra.Operations
{
    /// <summary>
    /// Provides matrix statistical operations (condition number, norms, etc.).
    /// </summary>
    public static class MatrixStatistics
    {
        /// <summary>
        /// Computes the condition number of a matrix using SVD.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The condition number (ratio of largest to smallest singular value).</returns>
        public static async Task<float> ConditionNumberAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            var (_, s, _) = await MatrixDecomposition.SVDAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

            var maxSingularValue = float.MinValue;
            var minSingularValue = float.MaxValue;

            for (var i = 0; i < s.Rows; i++)
            {
                var value = s[i, i];
                if (value > maxSingularValue)
                {
                    maxSingularValue = value;
                }

                if (value < minSingularValue && value > 1e-15f)
                {
                    minSingularValue = value;
                }
            }

            return minSingularValue > 0 ? maxSingularValue / minSingularValue : float.PositiveInfinity;
        }

        /// <summary>
        /// Computes the Frobenius norm of a matrix.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <returns>The Frobenius norm.</returns>
        public static float FrobeniusNorm(Matrix matrix)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            float sum = 0;
            for (var i = 0; i < matrix.Rows; i++)
            {
                for (var j = 0; j < matrix.Columns; j++)
                {
                    var value = matrix[i, j];
                    sum += value * value;
                }
            }

            return (float)Math.Sqrt(sum);
        }

        /// <summary>
        /// Computes the 1-norm (maximum absolute column sum) of a matrix.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <returns>The 1-norm.</returns>
        public static float OneNorm(Matrix matrix)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            float maxColumnSum = 0;
            for (var j = 0; j < matrix.Columns; j++)
            {
                float columnSum = 0;
                for (var i = 0; i < matrix.Rows; i++)
                {
                    columnSum += Math.Abs(matrix[i, j]);
                }
                if (columnSum > maxColumnSum)
                {
                    maxColumnSum = columnSum;
                }
            }

            return maxColumnSum;
        }

        /// <summary>
        /// Computes the infinity norm (maximum absolute row sum) of a matrix.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <returns>The infinity norm.</returns>
        public static float InfinityNorm(Matrix matrix)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            float maxRowSum = 0;
            for (var i = 0; i < matrix.Rows; i++)
            {
                float rowSum = 0;
                for (var j = 0; j < matrix.Columns; j++)
                {
                    rowSum += Math.Abs(matrix[i, j]);
                }
                if (rowSum > maxRowSum)
                {
                    maxRowSum = rowSum;
                }
            }

            return maxRowSum;
        }

        /// <summary>
        /// Computes the 2-norm (spectral norm) of a matrix using SVD.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The 2-norm (largest singular value).</returns>
        public static async Task<float> TwoNormAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            var (_, s, _) = await MatrixDecomposition.SVDAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

            float maxSingularValue = 0;
            for (var i = 0; i < s.Rows; i++)
            {
                var value = s[i, i];
                if (value > maxSingularValue)
                {
                    maxSingularValue = value;
                }
            }

            return maxSingularValue;
        }

        /// <summary>
        /// Computes the trace (sum of diagonal elements) of a square matrix.
        /// </summary>
        /// <param name="matrix">Input square matrix.</param>
        /// <returns>The trace.</returns>
        public static float Trace(Matrix matrix)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            if (!matrix.IsSquare)
            {
                throw new ArgumentException("Matrix must be square to compute trace.");
            }

            float trace = 0;
            for (var i = 0; i < matrix.Rows; i++)
            {
                trace += matrix[i, i];
            }

            return trace;
        }

        /// <summary>
        /// Computes the rank of a matrix using SVD.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="tolerance">Tolerance for determining zero singular values.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The numerical rank of the matrix.</returns>
        public static async Task<int> RankAsync(Matrix matrix, IAccelerator accelerator, float tolerance = 1e-10f, CancellationToken cancellationToken = default)
        {
            var (_, s, _) = await MatrixDecomposition.SVDAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

            var rank = 0;
            for (var i = 0; i < s.Rows; i++)
            {
                if (s[i, i] > tolerance)
                {
                    rank++;
                }
            }

            return rank;
        }

        /// <summary>
        /// Checks if a matrix is symmetric within a tolerance.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="tolerance">Tolerance for comparison.</param>
        /// <returns>True if the matrix is symmetric, false otherwise.</returns>
        public static bool IsSymmetric(Matrix matrix, float tolerance = 1e-10f)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            if (!matrix.IsSquare)
            {

                return false;
            }


            for (var i = 0; i < matrix.Rows; i++)
            {
                for (var j = 0; j < matrix.Columns; j++)
                {
                    if (Math.Abs(matrix[i, j] - matrix[j, i]) > tolerance)
                    {

                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if a matrix is positive definite by attempting Cholesky decomposition.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the matrix is positive definite, false otherwise.</returns>
        public static async Task<bool> IsPositiveDefiniteAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            if (!matrix.IsSquare)
            {

                return false;
            }


            try
            {
                _ = await MatrixDecomposition.CholeskyDecompositionAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Computes the matrix norm of a vector (treated as a single-column matrix).
        /// </summary>
        /// <param name="vector">Input vector as a matrix.</param>
        /// <param name="p">The p-norm to compute (1, 2, or infinity).</param>
        /// <returns>The p-norm of the vector.</returns>
        public static float VectorNorm(Matrix vector, float p = 2.0f)
        {
            ArgumentNullException.ThrowIfNull(vector);

            if (vector.Columns != 1)
            {
                throw new ArgumentException("Input must be a column vector (single column matrix).");
            }

            if (p == 1.0f)
            {
                float sum = 0;
                for (var i = 0; i < vector.Rows; i++)
                {
                    sum += Math.Abs(vector[i, 0]);
                }
                return sum;
            }
            else if (p == 2.0f)
            {
                float sum = 0;
                for (var i = 0; i < vector.Rows; i++)
                {
                    var value = vector[i, 0];
                    sum += value * value;
                }
                return (float)Math.Sqrt(sum);
            }
            else if (float.IsPositiveInfinity(p))
            {
                float max = 0;
                for (var i = 0; i < vector.Rows; i++)
                {
                    var absValue = Math.Abs(vector[i, 0]);
                    if (absValue > max)
                    {
                        max = absValue;
                    }
                }
                return max;
            }
            else
            {
                float sum = 0;
                for (var i = 0; i < vector.Rows; i++)
                {
                    sum += (float)Math.Pow(Math.Abs(vector[i, 0]), p);
                }
                return (float)Math.Pow(sum, 1.0f / p);
            }
        }

        /// <summary>
        /// Computes basic statistics about the matrix elements.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <returns>A tuple containing (min, max, mean, standard deviation).</returns>
        public static (float Min, float Max, float Mean, float StandardDeviation) ComputeStatistics(Matrix matrix)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            var min = float.MaxValue;
            var max = float.MinValue;
            float sum = 0;
            float sumSquares = 0;
            var count = matrix.Rows * matrix.Columns;

            for (var i = 0; i < matrix.Rows; i++)
            {
                for (var j = 0; j < matrix.Columns; j++)
                {
                    var value = matrix[i, j];
                    if (value < min)
                    {
                        min = value;
                    }


                    if (value > max)
                    {
                        max = value;
                    }


                    sum += value;
                    sumSquares += value * value;
                }
            }

            var mean = sum / count;
            var variance = (sumSquares / count) - (mean * mean);
            var standardDeviation = (float)Math.Sqrt(Math.Max(0, variance)); // Ensure non-negative

            return (min, max, mean, standardDeviation);
        }
    }
}
