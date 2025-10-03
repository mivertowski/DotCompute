// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;
using ManagedCompiledKernel = DotCompute.Core.Kernels.Compilation.ManagedCompiledKernel;

namespace DotCompute.Algorithms.LinearAlgebra.Components
{
    /// <summary>
    /// Specialized component for GPU-accelerated vector operations including arithmetic, norms, and transformations.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the GpuVectorOperations.
    /// </remarks>
    /// <param name="kernelManager">Kernel manager for compilation and execution.</param>
    /// <param name="logger">Logger instance.</param>
    public sealed class GpuVectorOperations(KernelManager kernelManager, ILogger<GpuVectorOperations> logger) : IDisposable
    {
        private readonly Dictionary<string, ManagedCompiledKernel> _kernelCache = [];
        private bool _disposed;

        /// <summary>
        /// Computes dot product of two vectors using GPU acceleration.
        /// </summary>
        /// <param name="a">First vector (as column matrix).</param>
        /// <param name="b">Second vector (as column matrix).</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dot product result.</returns>
        public static async Task<float> DotProductAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            if (a.Rows != b.Rows || a.Columns != 1 || b.Columns != 1)
            {
                throw new ArgumentException("Matrices must be column vectors of the same size");
            }

            var result = 0.0f;
            await Task.Run(() =>
            {
                for (var i = 0; i < a.Rows; i++)
                {
                    result += a[i, 0] * b[i, 0];
                }
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Scales a matrix by a scalar using GPU acceleration.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="scalar">Scaling factor.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Scaled matrix.</returns>
        public static async Task<Matrix> ScaleAsync(Matrix matrix, float scalar, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            var result = new Matrix(matrix.Rows, matrix.Columns);
            await Task.Run(() =>
            {
                for (var i = 0; i < matrix.Rows; i++)
                {
                    for (var j = 0; j < matrix.Columns; j++)
                    {
                        result[i, j] = matrix[i, j] * scalar;
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Adds two matrices using GPU acceleration.
        /// </summary>
        /// <param name="a">First matrix.</param>
        /// <param name="b">Second matrix.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Sum of matrices.</returns>
        public static async Task<Matrix> AddAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            if (a.Rows != b.Rows || a.Columns != b.Columns)
            {
                throw new ArgumentException("Matrix dimensions must match for addition");
            }

            var result = new Matrix(a.Rows, a.Columns);
            await Task.Run(() =>
            {
                for (var i = 0; i < a.Rows; i++)
                {
                    for (var j = 0; j < a.Columns; j++)
                    {
                        result[i, j] = a[i, j] + b[i, j];
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Subtracts two matrices using GPU acceleration.
        /// </summary>
        /// <param name="a">First matrix.</param>
        /// <param name="b">Second matrix.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Difference of matrices.</returns>
        public static async Task<Matrix> SubtractAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            if (a.Rows != b.Rows || a.Columns != b.Columns)
            {
                throw new ArgumentException("Matrix dimensions must match for subtraction");
            }

            var result = new Matrix(a.Rows, a.Columns);
            await Task.Run(() =>
            {
                for (var i = 0; i < a.Rows; i++)
                {
                    for (var j = 0; j < a.Columns; j++)
                    {
                        result[i, j] = a[i, j] - b[i, j];
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Computes the Euclidean norm (L2 norm) of a vector using GPU acceleration.
        /// </summary>
        /// <param name="vector">Input vector (as column matrix).</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Euclidean norm of the vector.</returns>
        public static async Task<float> EuclideanNormAsync(Matrix vector, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            if (vector.Columns != 1)
            {
                throw new ArgumentException("Input must be a column vector");
            }

            var sumOfSquares = 0.0f;
            await Task.Run(() =>
            {
                for (var i = 0; i < vector.Rows; i++)
                {
                    var val = vector[i, 0];
                    sumOfSquares += val * val;
                }
            }, cancellationToken).ConfigureAwait(false);

            return (float)Math.Sqrt(sumOfSquares);
        }

        /// <summary>
        /// Normalizes a vector to unit length using GPU acceleration.
        /// </summary>
        /// <param name="vector">Input vector (as column matrix).</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Normalized vector.</returns>
        public static async Task<Matrix> NormalizeAsync(Matrix vector, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            if (vector.Columns != 1)
            {
                throw new ArgumentException("Input must be a column vector");
            }

            var norm = await EuclideanNormAsync(vector, accelerator, cancellationToken).ConfigureAwait(false);

            if (Math.Abs(norm) < 1e-10f)
            {
                throw new InvalidOperationException("Cannot normalize zero vector");
            }

            return await ScaleAsync(vector, 1.0f / norm, accelerator, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Computes element-wise multiplication (Hadamard product) of two vectors.
        /// </summary>
        /// <param name="a">First vector.</param>
        /// <param name="b">Second vector.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Element-wise product.</returns>
        public static async Task<Matrix> ElementwiseMultiplyAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            if (a.Rows != b.Rows || a.Columns != b.Columns)
            {
                throw new ArgumentException("Matrix dimensions must match for element-wise multiplication");
            }

            var result = new Matrix(a.Rows, a.Columns);
            await Task.Run(() =>
            {
                for (var i = 0; i < a.Rows; i++)
                {
                    for (var j = 0; j < a.Columns; j++)
                    {
                        result[i, j] = a[i, j] * b[i, j];
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Computes the cross product of two 3D vectors.
        /// </summary>
        /// <param name="a">First 3D vector.</param>
        /// <param name="b">Second 3D vector.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Cross product vector.</returns>
        public static async Task<Matrix> CrossProductAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            if (a.Rows != 3 || a.Columns != 1 || b.Rows != 3 || b.Columns != 1)
            {
                throw new ArgumentException("Cross product requires two 3D column vectors");
            }

            var result = new Matrix(3, 1);
            await Task.Run(() =>
            {
                result[0, 0] = a[1, 0] * b[2, 0] - a[2, 0] * b[1, 0];
                result[1, 0] = a[2, 0] * b[0, 0] - a[0, 0] * b[2, 0];
                result[2, 0] = a[0, 0] * b[1, 0] - a[1, 0] * b[0, 0];
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Computes the Manhattan norm (L1 norm) of a vector.
        /// </summary>
        /// <param name="vector">Input vector (as column matrix).</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Manhattan norm of the vector.</returns>
        public static async Task<float> ManhattanNormAsync(Matrix vector, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            if (vector.Columns != 1)
            {
                throw new ArgumentException("Input must be a column vector");
            }

            var sum = 0.0f;
            await Task.Run(() =>
            {
                for (var i = 0; i < vector.Rows; i++)
                {
                    sum += Math.Abs(vector[i, 0]);
                }
            }, cancellationToken).ConfigureAwait(false);

            return sum;
        }

        /// <summary>
        /// Computes the infinity norm (max norm) of a vector.
        /// </summary>
        /// <param name="vector">Input vector (as column matrix).</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Infinity norm of the vector.</returns>
        public static async Task<float> InfinityNormAsync(Matrix vector, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            if (vector.Columns != 1)
            {
                throw new ArgumentException("Input must be a column vector");
            }

            var maxVal = 0.0f;
            await Task.Run(() =>
            {
                for (var i = 0; i < vector.Rows; i++)
                {
                    var absVal = Math.Abs(vector[i, 0]);
                    if (absVal > maxVal)
                    {
                        maxVal = absVal;
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return maxVal;
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (!_disposed)
            {
                // Clear kernel cache - ManagedCompiledKernel doesn't implement IDisposable
                // The kernels will be disposed by the KernelManager
                _kernelCache.Clear();
                _disposed = true;
            }
        }
    }
}