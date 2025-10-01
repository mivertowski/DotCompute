// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Kernels;
using DotCompute.Algorithms.Types.Kernels;
using DotCompute.Core.Extensions;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;
using LinearAlgebraOp = DotCompute.Algorithms.LinearAlgebra.LinearAlgebraKernels.LinearAlgebraOperation;
using DotCompute.Abstractions.Memory;
using KernelArgument = DotCompute.Abstractions.Interfaces.Kernels.KernelArgument;

namespace DotCompute.Algorithms.LinearAlgebra.Operations
{
    /// <summary>
    /// Provides basic matrix operations (add, subtract, multiply, transpose).
    /// </summary>
    public static class MatrixOperations
    {
        private const int BlockSize = 64; // Cache-friendly block size
        private const int GPUThreshold = 10000; // Minimum matrix size for GPU
        
        private static KernelManager? s_kernelManager;
        private static readonly Lock s_kernelLock = new();

        /// <summary>
        /// Multiplies two matrices with GPU acceleration when beneficial.
        /// </summary>
        public static async Task<Matrix> MultiplyAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            ArgumentNullException.ThrowIfNull(accelerator);

            if (a.Columns != b.Rows)
            {
                throw new ArgumentException($"Matrix dimensions incompatible for multiplication: ({a.Rows}x{a.Columns}) * ({b.Rows}x{b.Columns})");
            }

            var result = new Matrix(a.Rows, b.Columns);

            // Use GPU if available and matrices are large enough
            if (accelerator.Info.DeviceType != "CPU" && a.Size > GPUThreshold)
            {
                try
                {
                    return await MultiplyOnGPUAsync(a, b, accelerator, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Fall back to CPU on error
                }
            }

            // CPU implementation with blocking for cache efficiency
            await Task.Run(() => MultiplyBlocked(a, b, result), cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Adds two matrices element-wise with GPU acceleration when beneficial.
        /// </summary>
        public static async Task<Matrix> AddAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);

            if (a.Rows != b.Rows || a.Columns != b.Columns)
            {
                throw new ArgumentException($"Matrix dimensions must match for addition: ({a.Rows}x{a.Columns}) vs ({b.Rows}x{b.Columns})");
            }

            // Use GPU for large matrices
            if (accelerator.Info.DeviceType != "CPU" && a.Size > GPUThreshold)
            {
                try
                {
                    return await ElementwiseOperationOnGPUAsync(a, b, "Add", accelerator, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Fall back to CPU
                }
            }

            var result = new Matrix(a.Rows, a.Columns);

            await Task.Run(() =>
            {
                var aSpan = a.AsSpan();
                var bSpan = b.AsSpan();
                var resultData = result.ToArray();

                if (Vector.IsHardwareAccelerated && Vector<float>.Count <= aSpan.Length)
                {
                    VectorizedAdd(aSpan, bSpan, resultData);
                }
                else
                {
                    for (var i = 0; i < aSpan.Length; i++)
                    {
                        resultData[i] = aSpan[i] + bSpan[i];
                    }
                }

                // Copy result back
                CopyArrayToMatrix(resultData, result);
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Subtracts matrix b from matrix a element-wise with GPU acceleration when beneficial.
        /// </summary>
        public static async Task<Matrix> SubtractAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);

            if (a.Rows != b.Rows || a.Columns != b.Columns)
            {
                throw new ArgumentException($"Matrix dimensions must match for subtraction: ({a.Rows}x{a.Columns}) vs ({b.Rows}x{b.Columns})");
            }

            // Use GPU for large matrices
            if (accelerator.Info.DeviceType != "CPU" && a.Size > GPUThreshold)
            {
                try
                {
                    return await ElementwiseOperationOnGPUAsync(a, b, "Subtract", accelerator, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Fall back to CPU
                }
            }

            var result = new Matrix(a.Rows, a.Columns);

            await Task.Run(() =>
            {
                var aSpan = a.AsSpan();
                var bSpan = b.AsSpan();
                var resultData = result.ToArray();

                if (Vector.IsHardwareAccelerated && Vector<float>.Count <= aSpan.Length)
                {
                    VectorizedSubtract(aSpan, bSpan, resultData);
                }
                else
                {
                    for (var i = 0; i < aSpan.Length; i++)
                    {
                        resultData[i] = aSpan[i] - bSpan[i];
                    }
                }

                // Copy result back
                CopyArrayToMatrix(resultData, result);
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Transposes a matrix.
        /// </summary>
        public static async Task<Matrix> TransposeAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            var result = new Matrix(matrix.Columns, matrix.Rows);

            await Task.Run(() =>
            {
                // Block transpose for cache efficiency
                for (var i = 0; i < matrix.Rows; i += BlockSize)
                {
                    for (var j = 0; j < matrix.Columns; j += BlockSize)
                    {
                        var blockRows = Math.Min(BlockSize, matrix.Rows - i);
                        var blockCols = Math.Min(BlockSize, matrix.Columns - j);

                        for (var bi = 0; bi < blockRows; bi++)
                        {
                            for (var bj = 0; bj < blockCols; bj++)
                            {
                                result[j + bj, i + bi] = matrix[i + bi, j + bj];
                            }
                        }
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Computes the inverse of a square matrix.
        /// </summary>
        public static async Task<Matrix> InverseAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            if (!matrix.IsSquare)
            {
                throw new ArgumentException("Matrix must be square to compute inverse.");
            }

            // Use LU decomposition to compute inverse
            var (l, u, p) = await MatrixDecomposition.LUDecompositionAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);
            
            var n = matrix.Rows;
            var inverse = new Matrix(n, n);

            // Solve AX = I by solving n systems Ax_i = e_i
            await Task.Run(() =>
            {
                for (var i = 0; i < n; i++)
                {
                    // Create unit vector e_i
                    var e = new Matrix(n, 1);
                    e[i, 0] = 1.0f;

                    // Solve using forward and back substitution
                    var x = MatrixSolvers.SolveLU(l, u, p, e);

                    // Copy solution to inverse matrix column
                    for (var j = 0; j < n; j++)
                    {
                        inverse[j, i] = x[j, 0];
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return inverse;
        }

        /// <summary>
        /// Computes the determinant of a square matrix.
        /// </summary>
        public static async Task<float> DeterminantAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            if (!matrix.IsSquare)
            {
                throw new ArgumentException("Matrix must be square to compute determinant.");
            }

            if (matrix.Rows == 1)
            {
                return matrix[0, 0];
            }

            if (matrix.Rows == 2)
            {
                return matrix[0, 0] * matrix[1, 1] - matrix[0, 1] * matrix[1, 0];
            }

            // Use LU decomposition for larger matrices
            var (_, u, p) = await MatrixDecomposition.LUDecompositionAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

            // Determinant is product of diagonal elements of U times sign of permutation
            var det = 1.0f;
            for (var i = 0; i < u.Rows; i++)
            {
                det *= u[i, i];
            }

            // Count permutation swaps
            var swaps = 0;
            for (var i = 0; i < p.Length; i++)
            {
                if (p[i] != i)
                {
                    swaps++;
                }
            }

            return (swaps % 2 == 0) ? det : -det;
        }

        // Private helper methods
        private static async Task<Matrix> MultiplyOnGPUAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken)
        {
            var kernelManager = GetKernelManager();

            // Create kernel generation context
            var context = new KernelGenerationContext
            {
                DeviceInfo = accelerator.Info,
                UseSharedMemory = true,
                UseVectorTypes = false, // Matrix multiplication benefits from shared memory more than vectors
                Precision = PrecisionMode.Single,
                WorkGroupDimensions = [16, 16] // Tile size for matrix multiplication
            };

            // Get or compile kernel
            var kernel = await kernelManager.GetOrCompileOperationKernelAsync(
                "MatrixMultiply",
                [typeof(float[]), typeof(float[])],
                typeof(float[]),
                accelerator,
                context,
                null,
                cancellationToken).ConfigureAwait(false);

            // Allocate device memory
            var aData = a.ToArray();
            var bData = b.ToArray();
            var resultData = new float[a.Rows * b.Columns];

            var sizeA = aData.Length * sizeof(float);
            var sizeB = bData.Length * sizeof(float);
            var sizeResult = resultData.Length * sizeof(float);

            var bufferA = await accelerator.Memory.AllocateAsync<float>(aData.Length, DotCompute.Abstractions.Memory.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            var bufferB = await accelerator.Memory.AllocateAsync<float>(bData.Length, DotCompute.Abstractions.Memory.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            var bufferC = await accelerator.Memory.AllocateAsync<float>(resultData.Length, DotCompute.Abstractions.Memory.MemoryOptions.None, cancellationToken).ConfigureAwait(false);

            try
            {
                // Copy data to device
                await bufferA.WriteAsync(aData, 0, cancellationToken).ConfigureAwait(false);
                await bufferB.WriteAsync(bData, 0, cancellationToken).ConfigureAwait(false);

                // Prepare kernel arguments
                var arguments = new[]
                {
                    new KernelArgument { Name = "A", Value = bufferA, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = bufferA },
                    new KernelArgument { Name = "B", Value = bufferB, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = bufferB },
                    new KernelArgument { Name = "C", Value = bufferC, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = bufferC },
                    new KernelArgument { Name = "M", Value = a.Rows, Type = typeof(int), IsDeviceMemory = false },
                    new KernelArgument { Name = "N", Value = b.Columns, Type = typeof(int), IsDeviceMemory = false },
                    new KernelArgument { Name = "K", Value = a.Columns, Type = typeof(int), IsDeviceMemory = false }
                };

                // Configure execution
                var tileSize = 16;
                var globalRows = ((a.Rows + tileSize - 1) / tileSize) * tileSize;
                var globalCols = ((b.Columns + tileSize - 1) / tileSize) * tileSize;

                var config = new KernelExecutionConfig
                {
                    GlobalWorkSize = [globalCols, globalRows],
                    LocalWorkSize = [tileSize, tileSize],
                    CaptureTimings = true
                };

                // Execute kernel
                var result = await kernelManager.ExecuteKernelAsync(
                    kernel,
                    arguments,
                    accelerator,
                    config,
                    cancellationToken).ConfigureAwait(false);

                if (!result.Success)
                {
                    throw new InvalidOperationException($"GPU execution failed: {result.ErrorMessage}");
                }

                // Read results
                await bufferC.ReadAsync(resultData, 0, cancellationToken).ConfigureAwait(false);

                // Create result matrix
                var resultMatrix = new Matrix(a.Rows, b.Columns);
                CopyArrayToMatrix(resultData, resultMatrix);

                return resultMatrix;
            }
            finally
            {
                await bufferA.DisposeAsync().ConfigureAwait(false);
                await bufferB.DisposeAsync().ConfigureAwait(false);
                await bufferC.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static async Task<Matrix> ElementwiseOperationOnGPUAsync(Matrix a, Matrix b, string operation, IAccelerator accelerator, CancellationToken cancellationToken)
        {
            var kernelManager = GetKernelManager();

            // For element-wise operations, we can reuse the vector addition kernel structure
            var context = new KernelGenerationContext
            {
                DeviceInfo = accelerator.Info,
                UseSharedMemory = false,
                UseVectorTypes = true,
                Precision = PrecisionMode.Single
            };

            // Map operation names
            var kernelOperation = operation switch
            {
                "Add" => "VectorAdd",
                "Subtract" => "VectorSubtract",
                _ => throw new NotSupportedException($"Operation {operation} not supported")
            };

            var kernel = await kernelManager.GetOrCompileOperationKernelAsync(
                kernelOperation,
                [typeof(float[]), typeof(float[])],
                typeof(float[]),
                accelerator,
                context,
                null,
                cancellationToken).ConfigureAwait(false);

            // Execute similar to vector operations
            var aData = a.ToArray();
            var bData = b.ToArray();
            var resultData = new float[aData.Length];

            var size = aData.Length * sizeof(float);
            var bufferA = await accelerator.Memory.AllocateAsync<float>(aData.Length, DotCompute.Abstractions.Memory.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            var bufferB = await accelerator.Memory.AllocateAsync<float>(aData.Length, DotCompute.Abstractions.Memory.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            var bufferResult = await accelerator.Memory.AllocateAsync<float>(aData.Length, DotCompute.Abstractions.Memory.MemoryOptions.None, cancellationToken).ConfigureAwait(false);

            try
            {
                await bufferA.WriteAsync(aData, 0, cancellationToken).ConfigureAwait(false);
                await bufferB.WriteAsync(bData, 0, cancellationToken).ConfigureAwait(false);

                var arguments = new[]
                {
                    new KernelArgument { Name = "a", Value = bufferA, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = bufferA },
                    new KernelArgument { Name = "b", Value = bufferB, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = bufferB },
                    new KernelArgument { Name = "result", Value = bufferResult, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = bufferResult },
                    new KernelArgument { Name = "n", Value = aData.Length, Type = typeof(int), IsDeviceMemory = false }
                };

                var workGroupSize = Math.Min(256, accelerator.Info.MaxThreadsPerBlock);
                var globalSize = ((aData.Length + workGroupSize - 1) / workGroupSize) * workGroupSize;

                var config = new KernelExecutionConfig
                {
                    GlobalWorkSize = [globalSize],
                    LocalWorkSize = [workGroupSize],
                    CaptureTimings = true
                };

                var result = await kernelManager.ExecuteKernelAsync(kernel, arguments, accelerator, config, cancellationToken).ConfigureAwait(false);

                if (!result.Success)
                {
                    throw new InvalidOperationException($"GPU execution failed: {result.ErrorMessage}");
                }

                await bufferResult.ReadAsync(resultData, 0, cancellationToken).ConfigureAwait(false);

                var resultMatrix = new Matrix(a.Rows, a.Columns);
                CopyArrayToMatrix(resultData, resultMatrix);

                return resultMatrix;
            }
            finally
            {
                await bufferA.DisposeAsync().ConfigureAwait(false);
                await bufferB.DisposeAsync().ConfigureAwait(false);
                await bufferResult.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static KernelManager GetKernelManager()
        {
#pragma warning disable CA1508 // Avoid dead conditional code
            if (s_kernelManager == null)
            {
                lock (s_kernelLock)
                {
                    s_kernelManager ??= new KernelManager(Microsoft.Extensions.Logging.Abstractions.NullLogger<KernelManager>.Instance);
                }
            }
#pragma warning restore CA1508
            return s_kernelManager;
        }

        public static void CopyArrayToMatrix(float[] array, Matrix matrix)
        {
            for (var i = 0; i < matrix.Rows; i++)
            {
                for (var j = 0; j < matrix.Columns; j++)
                {
                    matrix[i, j] = array[i * matrix.Columns + j];
                }
            }
        }

        private static void MultiplyBlocked(Matrix a, Matrix b, Matrix result)
        {
            var m = a.Rows;
            var n = b.Columns;
            var k = a.Columns;

            // Initialize result to zero
            for (var i = 0; i < m; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    result[i, j] = 0;
                }
            }

            // Blocked matrix multiplication
            for (var i0 = 0; i0 < m; i0 += BlockSize)
            {
                for (var j0 = 0; j0 < n; j0 += BlockSize)
                {
                    for (var k0 = 0; k0 < k; k0 += BlockSize)
                    {
                        var iMax = Math.Min(i0 + BlockSize, m);
                        var jMax = Math.Min(j0 + BlockSize, n);
                        var kMax = Math.Min(k0 + BlockSize, k);

                        // Multiply block
                        for (var i = i0; i < iMax; i++)
                        {
                            for (var j = j0; j < jMax; j++)
                            {
                                var sum = result[i, j];
                                for (var l = k0; l < kMax; l++)
                                {
                                    sum += a[i, l] * b[l, j];
                                }
                                result[i, j] = sum;
                            }
                        }
                    }
                }
            }
        }

        private static void VectorizedAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, float[] result)
        {
            var vectorSize = Vector<float>.Count;
            var i = 0;

            // Process vectorized portion
            for (; i <= a.Length - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Slice(i, vectorSize));
                var vb = new Vector<float>(b.Slice(i, vectorSize));
                var vr = va + vb;
                vr.CopyTo(result, i);
            }

            // Process remaining elements
            for (; i < a.Length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }

        private static void VectorizedSubtract(ReadOnlySpan<float> a, ReadOnlySpan<float> b, float[] result)
        {
            var vectorSize = Vector<float>.Count;
            var i = 0;

            // Process vectorized portion
            for (; i <= a.Length - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Slice(i, vectorSize));
                var vb = new Vector<float>(b.Slice(i, vectorSize));
                var vr = va - vb;
                vr.CopyTo(result, i);
            }

            // Process remaining elements
            for (; i < a.Length; i++)
            {
                result[i] = a[i] - b[i];
            }
        }
    }
}