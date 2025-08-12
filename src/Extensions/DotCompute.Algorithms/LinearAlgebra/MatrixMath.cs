// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Types.Kernels;
using DotCompute.Core.Extensions;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;
using LinearAlgebraOp = DotCompute.Algorithms.LinearAlgebra.LinearAlgebraKernels.LinearAlgebraOperation;

namespace DotCompute.Algorithms.LinearAlgebra;

/// <summary>
/// Provides high-performance matrix operations with GPU acceleration.
/// </summary>
public static class MatrixMath
{
    private const int BlockSize = 64; // Cache-friendly block size
    private const int GPUThreshold = 10000; // Minimum matrix size for GPU
    private const float NumericalTolerance = 1e-10f; // Default numerical tolerance
    
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
        var (l, u, p) = await LUDecompositionAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);
        
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
                var x = SolveLU(l, u, p, e);

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
        var (_, u, p) = await LUDecompositionAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);

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

    /// <summary>
    /// Computes the QR decomposition of a matrix using Householder reflections.
    /// </summary>
    /// <param name="matrix">Input matrix to decompose.</param>
    /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing Q (orthogonal) and R (upper triangular) matrices.</returns>
    public static async Task<(Matrix Q, Matrix R)> QRDecompositionAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        var m = matrix.Rows;
        var n = matrix.Columns;
        
        var a = matrix.Clone();
        var q = Matrix.Identity(m);

        // Use GPU for large matrices
        if (accelerator.Info.DeviceType != "CPU" && matrix.Size > GPUThreshold)
        {
            try
            {
                return await QRDecompositionOnGPUAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Fall back to CPU on error
            }
        }

        return await Task.Run(() =>
        {
            // Householder QR decomposition
            for (var k = 0; k < Math.Min(m - 1, n); k++)
            {
                // Compute Householder vector
                var x = new Matrix(m - k, 1);
                for (var i = k; i < m; i++)
                {
                    x[i - k, 0] = a[i, k];
                }

                var norm = ComputeVector2Norm(x);
                if (Math.Abs(norm) < NumericalTolerance)
                    continue;

                var sign = x[0, 0] >= 0 ? 1.0f : -1.0f;
                x[0, 0] += sign * norm;
                var vnorm = ComputeVector2Norm(x);
                
                if (Math.Abs(vnorm) < NumericalTolerance)
                    continue;

                // Normalize Householder vector
                for (var i = 0; i < x.Rows; i++)
                {
                    x[i, 0] /= vnorm;
                }

                // Apply Householder transformation to A
                ApplyHouseholderLeft(a, x, k);

                // Apply Householder transformation to Q
                ApplyHouseholderRight(q, x, k);
            }

            // Extract R (upper triangular part of A)
            var r = new Matrix(Math.Min(m, n), n);
            for (var i = 0; i < r.Rows; i++)
            {
                for (var j = i; j < n; j++)
                {
                    r[i, j] = a[i, j];
                }
            }

            return (q, r);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs LU decomposition with partial pivoting.
    /// </summary>
    public static async Task<(Matrix L, Matrix U, int[] P)> LUDecompositionAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
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

        await Task.Run(() =>
        {
            for (var k = 0; k < n - 1; k++)
            {
                // Find pivot
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

                // Swap rows if needed
                if (pivotRow != k)
                {
                    // Swap in U
                    for (var j = 0; j < n; j++)
                    {
                        (u[k, j], u[pivotRow, j]) = (u[pivotRow, j], u[k, j]);
                    }

                    // Swap in L (only the computed part)
                    for (var j = 0; j < k; j++)
                    {
                        (l[k, j], l[pivotRow, j]) = (l[pivotRow, j], l[k, j]);
                    }

                    // Update permutation
                    (p[k], p[pivotRow]) = (p[pivotRow], p[k]);
                }

                // Check for singularity
                if (Math.Abs(u[k, k]) < 1e-10f)
                {
                    throw new InvalidOperationException("Matrix is singular or nearly singular.");
                }

                // Compute multipliers and eliminate
                for (var i = k + 1; i < n; i++)
                {
                    l[i, k] = u[i, k] / u[k, k];
                    for (var j = k + 1; j < n; j++)
                    {
                        u[i, j] -= l[i, k] * u[k, j];
                    }
                    u[i, k] = 0;
                }
            }
        }, cancellationToken).ConfigureAwait(false);

        return (l, u, p);
    }

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
        var (l, u, p) = await LUDecompositionAsync(a, accelerator, cancellationToken).ConfigureAwait(false);

        return await Task.Run(() => SolveLU(l, u, p, b), cancellationToken).ConfigureAwait(false);
    }

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

        var bufferA = await accelerator.Memory.AllocateAsync(sizeA, DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
        var bufferB = await accelerator.Memory.AllocateAsync(sizeB, DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
        var bufferC = await accelerator.Memory.AllocateAsync(sizeResult, DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);

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
        var bufferA = await accelerator.Memory.AllocateAsync(size, DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
        var bufferB = await accelerator.Memory.AllocateAsync(size, DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
        var bufferResult = await accelerator.Memory.AllocateAsync(size, DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);

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

    private static void CopyArrayToMatrix(float[] array, Matrix matrix)
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

    private static Matrix SolveLU(Matrix l, Matrix u, int[] p, Matrix b)
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
    /// Computes the Singular Value Decomposition (SVD) of a matrix.
    /// </summary>
    /// <param name="matrix">Input matrix to decompose.</param>
    /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing U, S (diagonal singular values), and VT matrices.</returns>
    public static async Task<(Matrix U, Matrix S, Matrix VT)> SVDAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        // Use GPU for large matrices
        if (accelerator.Info.DeviceType != "CPU" && matrix.Size > GPUThreshold)
        {
            try
            {
                return await SVDOnGPUAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Fall back to CPU on error
            }
        }

        return await Task.Run(() =>
        {
            // Use Jacobi SVD for numerical stability
            return ComputeJacobiSVD(matrix);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Computes the Cholesky decomposition of a positive definite matrix.
    /// </summary>
    /// <param name="matrix">Input positive definite matrix.</param>
    /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lower triangular matrix L such that A = L * L^T.</returns>
    public static async Task<Matrix> CholeskyDecompositionAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        if (!matrix.IsSquare)
        {
            throw new ArgumentException("Matrix must be square for Cholesky decomposition.");
        }

        return await Task.Run(() =>
        {
            var n = matrix.Rows;
            var l = new Matrix(n, n);

            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j <= i; j++)
                {
                    if (i == j) // Diagonal elements
                    {
                        float sum = 0;
                        for (var k = 0; k < j; k++)
                        {
                            sum += l[j, k] * l[j, k];
                        }
                        var value = matrix[j, j] - sum;
                        if (value <= 0)
                        {
                            throw new InvalidOperationException("Matrix is not positive definite.");
                        }
                        l[j, j] = (float)Math.Sqrt(value);
                    }
                    else // Lower triangular elements
                    {
                        float sum = 0;
                        for (var k = 0; k < j; k++)
                        {
                            sum += l[i, k] * l[j, k];
                        }
                        l[i, j] = (matrix[i, j] - sum) / l[j, j];
                    }
                }
            }

            return l;
        }, cancellationToken).ConfigureAwait(false);
    }

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
    {
        ArgumentNullException.ThrowIfNull(matrix);

        if (!matrix.IsSquare)
        {
            throw new ArgumentException("Matrix must be square for eigenvalue decomposition.");
        }

        return await Task.Run(() =>
        {
            var n = matrix.Rows;
            var a = matrix.Clone();
            var v = Matrix.Identity(n);

            // Reduce to Hessenberg form first
            ReduceToHessenberg(a, v);

            // Apply QR algorithm with shifts
            for (var iter = 0; iter < maxIterations; iter++)
            {
                // Check for convergence
                if (IsConverged(a, tolerance))
                    break;

                // Wilkinson shift
                var shift = ComputeWilkinsonShift(a);

                // Shift matrix
                for (var i = 0; i < n; i++)
                {
                    a[i, i] -= shift;
                }

                // QR decomposition
                var (q, r) = QRDecompositionHessenberg(a);

                // Update A = RQ + shift*I
                MultiplyMatrices(r, q, a);
                for (var i = 0; i < n; i++)
                {
                    a[i, i] += shift;
                }

                // Update eigenvectors
                var temp = new Matrix(n, n);
                MultiplyMatrices(v, q, temp);
                v = temp;
            }

            // Extract eigenvalues from diagonal
            var eigenvalues = new Matrix(n, 1);
            for (var i = 0; i < n; i++)
            {
                eigenvalues[i, 0] = a[i, i];
            }

            return (eigenvalues, v);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Computes the condition number of a matrix using SVD.
    /// </summary>
    /// <param name="matrix">Input matrix.</param>
    /// <param name="accelerator">Compute accelerator for GPU acceleration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The condition number (ratio of largest to smallest singular value).</returns>
    public static async Task<float> ConditionNumberAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
    {
        var (_, s, _) = await SVDAsync(matrix, accelerator, cancellationToken).ConfigureAwait(false);
        
        var maxSingularValue = float.MinValue;
        var minSingularValue = float.MaxValue;
        
        for (var i = 0; i < s.Rows; i++)
        {
            var value = s[i, i];
            if (value > maxSingularValue)
                maxSingularValue = value;
            if (value < minSingularValue && value > 1e-15f)
                minSingularValue = value;
        }
        
        return minSingularValue > 0 ? maxSingularValue / minSingularValue : float.PositiveInfinity;
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
            var ax = await MultiplyAsync(originalA, x, accelerator, cancellationToken).ConfigureAwait(false);
            var residual = await SubtractAsync(b, ax, accelerator, cancellationToken).ConfigureAwait(false);

            // Check convergence
            var residualNorm = ComputeVector2Norm(residual);
            if (residualNorm < tolerance)
                break;

            // Solve A * correction = residual
            var correction = await SolveAsync(a, residual, accelerator, cancellationToken).ConfigureAwait(false);

            // Update solution: x = x + correction
            x = await AddAsync(x, correction, accelerator, cancellationToken).ConfigureAwait(false);
        }

        return x;
    }

    // Helper methods for advanced matrix algorithms
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

    private static void ApplyHouseholderLeft(Matrix matrix, Matrix v, int startRow)
    {
        var m = matrix.Rows;
        var n = matrix.Columns;
        
        // Compute H * A where H = I - 2 * v * v^T
        for (var j = 0; j < n; j++)
        {
            float dot = 0;
            for (var i = startRow; i < m; i++)
            {
                dot += v[i - startRow, 0] * matrix[i, j];
            }
            
            dot *= 2.0f;
            
            for (var i = startRow; i < m; i++)
            {
                matrix[i, j] -= dot * v[i - startRow, 0];
            }
        }
    }

    private static void ApplyHouseholderRight(Matrix matrix, Matrix v, int startCol)
    {
        var m = matrix.Rows;
        var n = matrix.Columns;
        
        // Compute A * H where H = I - 2 * v * v^T
        for (var i = 0; i < m; i++)
        {
            float dot = 0;
            for (var j = startCol; j < Math.Min(n, startCol + v.Rows); j++)
            {
                dot += matrix[i, j] * v[j - startCol, 0];
            }
            
            dot *= 2.0f;
            
            for (var j = startCol; j < Math.Min(n, startCol + v.Rows); j++)
            {
                matrix[i, j] -= dot * v[j - startCol, 0];
            }
        }
    }

    private static (Matrix U, Matrix S, Matrix VT) ComputeJacobiSVD(Matrix matrix)
    {
        var m = matrix.Rows;
        var n = matrix.Columns;
        
        // Use two-sided Jacobi SVD for small to medium matrices
        var u = Matrix.Identity(m);
        var a = matrix.Clone();
        var v = Matrix.Identity(n);
        
        const int maxIterations = 1000;
        const float tolerance = 1e-10f;
        
        for (var iter = 0; iter < maxIterations; iter++)
        {
            var converged = true;
            
            // Iterate over all off-diagonal elements
            for (var i = 0; i < Math.Min(m, n); i++)
            {
                for (var j = i + 1; j < Math.Min(m, n); j++)
                {
                    // Check if off-diagonal element is small enough
                    var offDiag = Math.Abs(a[i, j]) + Math.Abs(a[j, i]);
                    if (offDiag > tolerance)
                    {
                        converged = false;
                        
                        // Compute Jacobi rotation to zero out A[i,j] and A[j,i]
                        ApplyJacobiRotation(a, u, v, i, j);
                    }
                }
            }
            
            if (converged)
                break;
        }
        
        // Extract singular values and ensure they are positive
        var s = new Matrix(Math.Min(m, n), Math.Min(m, n));
        for (var i = 0; i < Math.Min(m, n); i++)
        {
            var value = Math.Abs(a[i, i]);
            s[i, i] = value;
            
            // If singular value is negative, flip sign of corresponding column in U or V
            if (a[i, i] < 0 && i < m)
            {
                for (var j = 0; j < m; j++)
                {
                    u[j, i] = -u[j, i];
                }
            }
        }
        
        return (u, s, TransposeMatrix(v));
    }

    private static void ApplyJacobiRotation(Matrix a, Matrix u, Matrix v, int i, int j)
    {
        // Compute the 2x2 submatrix
        var aii = a[i, i];
        var aij = a[i, j];
        var aji = a[j, i];
        var ajj = a[j, j];
        
        // Compute SVD of 2x2 matrix
        ComputeJacobi2x2SVD(aii, aij, aji, ajj, out var c, out var s);
        
        // Apply rotations to A, U, and V
        ApplyGivensRotationColumns(a, c, s, i, j);
        ApplyGivensRotationColumns(u, c, s, i, j);
        ApplyGivensRotationRows(a, c, s, i, j);
        ApplyGivensRotationColumns(v, c, s, i, j);
    }

    private static void ComputeJacobi2x2SVD(float a11, float a12, float a21, float a22, out float c, out float s)
    {
        // Simplified Jacobi rotation computation
        var tau = (a22 - a11) / (2.0f * (a12 + a21));
        var t = Math.Sign(tau) / (Math.Abs(tau) + (float)Math.Sqrt(1 + tau * tau));
        c = 1.0f / (float)Math.Sqrt(1 + t * t);
        s = c * t;
    }

    private static void ApplyGivensRotationColumns(Matrix matrix, float c, float s, int i, int j)
    {
        for (var k = 0; k < matrix.Rows; k++)
        {
            var temp1 = c * matrix[k, i] - s * matrix[k, j];
            var temp2 = s * matrix[k, i] + c * matrix[k, j];
            matrix[k, i] = temp1;
            matrix[k, j] = temp2;
        }
    }

    private static void ApplyGivensRotationRows(Matrix matrix, float c, float s, int i, int j)
    {
        for (var k = 0; k < matrix.Columns; k++)
        {
            var temp1 = c * matrix[i, k] - s * matrix[j, k];
            var temp2 = s * matrix[i, k] + c * matrix[j, k];
            matrix[i, k] = temp1;
            matrix[j, k] = temp2;
        }
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

    private static void ReduceToHessenberg(Matrix a, Matrix v)
    {
        var n = a.Rows;
        
        for (var k = 0; k < n - 2; k++)
        {
            // Find Householder vector to zero out column k below the subdiagonal
            var x = new Matrix(n - k - 1, 1);
            for (var i = k + 1; i < n; i++)
            {
                x[i - k - 1, 0] = a[i, k];
            }
            
            var norm = ComputeVector2Norm(x);
            if (Math.Abs(norm) < NumericalTolerance)
                continue;
                
            var sign = x[0, 0] >= 0 ? 1.0f : -1.0f;
            x[0, 0] += sign * norm;
            var vnorm = ComputeVector2Norm(x);
            
            if (Math.Abs(vnorm) < NumericalTolerance)
                continue;
                
            for (var i = 0; i < x.Rows; i++)
            {
                x[i, 0] /= vnorm;
            }
            
            // Apply Householder transformation
            ApplyHouseholderToHessenberg(a, v, x, k + 1);
        }
    }

    private static void ApplyHouseholderToHessenberg(Matrix a, Matrix v, Matrix householder, int startIdx)
    {
        var n = a.Rows;
        
        // Apply to A from left and right
        for (var j = 0; j < n; j++)
        {
            float dot = 0;
            for (var i = startIdx; i < n; i++)
            {
                dot += householder[i - startIdx, 0] * a[i, j];
            }
            dot *= 2.0f;
            
            for (var i = startIdx; i < n; i++)
            {
                a[i, j] -= dot * householder[i - startIdx, 0];
            }
        }
        
        for (var i = 0; i < n; i++)
        {
            float dot = 0;
            for (var j = startIdx; j < n; j++)
            {
                dot += a[i, j] * householder[j - startIdx, 0];
            }
            dot *= 2.0f;
            
            for (var j = startIdx; j < n; j++)
            {
                a[i, j] -= dot * householder[j - startIdx, 0];
            }
        }
        
        // Update eigenvector matrix V
        for (var i = 0; i < n; i++)
        {
            float dot = 0;
            for (var j = startIdx; j < n; j++)
            {
                dot += v[i, j] * householder[j - startIdx, 0];
            }
            dot *= 2.0f;
            
            for (var j = startIdx; j < n; j++)
            {
                v[i, j] -= dot * householder[j - startIdx, 0];
            }
        }
    }

    private static bool IsConverged(Matrix matrix, float tolerance)
    {
        var n = matrix.Rows;
        for (var i = 0; i < n - 1; i++)
        {
            if (Math.Abs(matrix[i + 1, i]) > tolerance)
                return false;
        }
        return true;
    }

    private static float ComputeWilkinsonShift(Matrix matrix)
    {
        var n = matrix.Rows;
        if (n < 2) return 0;
        
        var a = matrix[n - 2, n - 2];
        var b = matrix[n - 2, n - 1];
        var c = matrix[n - 1, n - 2];
        var d = matrix[n - 1, n - 1];
        
        var trace = a + d;
        var det = a * d - b * c;
        
        var discriminant = trace * trace - 4 * det;
        if (discriminant < 0) return d; // Use bottom-right element if complex eigenvalues
        
        var sqrt_disc = (float)Math.Sqrt(discriminant);
        var lambda1 = (trace + sqrt_disc) * 0.5f;
        var lambda2 = (trace - sqrt_disc) * 0.5f;
        
        // Return the eigenvalue closest to d
        return Math.Abs(lambda1 - d) < Math.Abs(lambda2 - d) ? lambda1 : lambda2;
    }

    private static (Matrix Q, Matrix R) QRDecompositionHessenberg(Matrix hessenberg)
    {
        var n = hessenberg.Rows;
        var q = Matrix.Identity(n);
        var r = hessenberg.Clone();
        
        // Use Givens rotations for Hessenberg matrices
        for (var i = 0; i < n - 1; i++)
        {
            if (Math.Abs(r[i + 1, i]) < NumericalTolerance)
                continue;
                
            var a = r[i, i];
            var b = r[i + 1, i];
            var norm = (float)Math.Sqrt(a * a + b * b);
            
            if (Math.Abs(norm) < NumericalTolerance)
                continue;
                
            var c = a / norm;
            var s = -b / norm;
            
            // Apply Givens rotation
            ApplyGivensRotationRows(r, c, s, i, i + 1);
            ApplyGivensRotationColumns(q, c, s, i, i + 1);
        }
        
        return (q, r);
    }

    private static void MultiplyMatrices(Matrix a, Matrix b, Matrix result)
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
        
        // Compute matrix multiplication
        for (var i = 0; i < m; i++)
        {
            for (var j = 0; j < n; j++)
            {
                for (var l = 0; l < k; l++)
                {
                    result[i, j] += a[i, l] * b[l, j];
                }
            }
        }
    }

    // GPU-accelerated implementations
    private static async Task<(Matrix Q, Matrix R)> QRDecompositionOnGPUAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken)
    {
        var kernelManager = GetKernelManager();
        var m = matrix.Rows;
        var n = matrix.Columns;
        
        var a = matrix.Clone();
        var q = Matrix.Identity(m);

        try
        {
            // Use GPU kernels for Householder transformations
            var context = new KernelGenerationContext
            {
                DeviceInfo = accelerator.Info,
                UseSharedMemory = true,
                Precision = PrecisionMode.Single,
                WorkGroupDimensions = [256] // For vector operations
            };

            // Process each Householder transformation step
            for (var k = 0; k < Math.Min(m - 1, n); k++)
            {
                // Extract column vector for Householder computation
                var columnData = new float[m - k];
                for (var i = k; i < m; i++)
                {
                    columnData[i - k] = a[i, k];
                }

                // GPU kernel for computing Householder vector
                var householderKernel = await kernelManager.GetOrCompileOperationKernelAsync(
                    "HouseholderVector",
                    [typeof(float[])],
                    typeof(float[]),
                    accelerator,
                    context,
                    null,
                    cancellationToken).ConfigureAwait(false);

                var columnBuffer = await accelerator.Memory.AllocateAsync(columnData.Length * sizeof(float), 
                    DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
                var householderBuffer = await accelerator.Memory.AllocateAsync(columnData.Length * sizeof(float), 
                    DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
                var normBuffer = await accelerator.Memory.AllocateAsync(sizeof(float), 
                    DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);

                try
                {
                    await columnBuffer.WriteAsync(columnData, 0, cancellationToken).ConfigureAwait(false);

                    var args = new[]
                    {
                        new KernelArgument { Name = "column", Value = columnBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = columnBuffer },
                        new KernelArgument { Name = "householder", Value = householderBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = householderBuffer },
                        new KernelArgument { Name = "norm_result", Value = normBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = normBuffer },
                        new KernelArgument { Name = "n", Value = m, Type = typeof(int), IsDeviceMemory = false },
                        new KernelArgument { Name = "start_idx", Value = k, Type = typeof(int), IsDeviceMemory = false }
                    };

                    var parameters = LinearAlgebraKernels.GetOptimizedParameters(
                        LinearAlgebraOp.HouseholderVector,
                        (m - k, 1),
                        accelerator.Info.Name);

                    var config = new KernelExecutionConfig
                    {
                        GlobalWorkSize = parameters.GlobalWorkSize,
                        LocalWorkSize = parameters.LocalWorkSize,
                        CaptureTimings = true
                    };

                    var result = await kernelManager.ExecuteKernelAsync(householderKernel, args, accelerator, config, cancellationToken).ConfigureAwait(false);
                    
                    if (!result.Success)
                    {
                        throw new InvalidOperationException($"Householder vector computation failed: {result.ErrorMessage}");
                    }

                    // Read back Householder vector
                    var householderData = new float[columnData.Length];
                    await householderBuffer.ReadAsync(householderData, 0, cancellationToken).ConfigureAwait(false);

                    // Apply Householder transformation using GPU kernel
                    var transformKernel = await kernelManager.GetOrCompileOperationKernelAsync(
                        "HouseholderTransform",
                        [typeof(float[]), typeof(float[])],
                        typeof(float[]),
                        accelerator,
                        context,
                        null,
                        cancellationToken).ConfigureAwait(false);

                    var matrixData = a.ToArray();
                    var matrixBuffer = await accelerator.Memory.AllocateAsync(matrixData.Length * sizeof(float), 
                        DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);

                    await matrixBuffer.WriteAsync(matrixData, 0, cancellationToken).ConfigureAwait(false);

                    var transformArgs = new[]
                    {
                        new KernelArgument { Name = "matrix", Value = matrixBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = matrixBuffer },
                        new KernelArgument { Name = "v", Value = householderBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = householderBuffer },
                        new KernelArgument { Name = "m", Value = m, Type = typeof(int), IsDeviceMemory = false },
                        new KernelArgument { Name = "n", Value = n, Type = typeof(int), IsDeviceMemory = false },
                        new KernelArgument { Name = "v_len", Value = householderData.Length, Type = typeof(int), IsDeviceMemory = false },
                        new KernelArgument { Name = "start_row", Value = k, Type = typeof(int), IsDeviceMemory = false }
                    };

                    var transformResult = await kernelManager.ExecuteKernelAsync(transformKernel, transformArgs, accelerator, config, cancellationToken).ConfigureAwait(false);
                    
                    if (!transformResult.Success)
                    {
                        throw new InvalidOperationException($"Householder transformation failed: {transformResult.ErrorMessage}");
                    }

                    // Read back transformed matrix
                    await matrixBuffer.ReadAsync(matrixData, 0, cancellationToken).ConfigureAwait(false);
                    CopyArrayToMatrix(matrixData, a);

                    // Update Q matrix (could also be done on GPU)
                    var x = new Matrix(m - k, 1);
                    for (var i = 0; i < householderData.Length; i++)
                    {
                        x[i, 0] = householderData[i];
                    }
                    ApplyHouseholderRight(q, x, k);
                }
                finally
                {
                    await columnBuffer.DisposeAsync().ConfigureAwait(false);
                    await householderBuffer.DisposeAsync().ConfigureAwait(false);
                    await normBuffer.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch
        {
            // Fall back to CPU implementation if GPU fails
            await Task.Run(() =>
            {
                for (var k = 0; k < Math.Min(m - 1, n); k++)
                {
                    var x = new Matrix(m - k, 1);
                    for (var i = k; i < m; i++)
                    {
                        x[i - k, 0] = a[i, k];
                    }

                    var norm = ComputeVector2Norm(x);
                    if (Math.Abs(norm) < NumericalTolerance)
                        continue;

                    var sign = x[0, 0] >= 0 ? 1.0f : -1.0f;
                    x[0, 0] += sign * norm;
                    var vnorm = ComputeVector2Norm(x);
                    
                    if (Math.Abs(vnorm) < NumericalTolerance)
                        continue;

                    for (var i = 0; i < x.Rows; i++)
                    {
                        x[i, 0] /= vnorm;
                    }

                    ApplyHouseholderLeft(a, x, k);
                    ApplyHouseholderRight(q, x, k);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        var r = new Matrix(Math.Min(m, n), n);
        for (var i = 0; i < r.Rows; i++)
        {
            for (var j = i; j < n; j++)
            {
                r[i, j] = a[i, j];
            }
        }

        return (q, r);
    }

    private static async Task<(Matrix U, Matrix S, Matrix VT)> SVDOnGPUAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken)
    {
        var kernelManager = GetKernelManager();
        var m = matrix.Rows;
        var n = matrix.Columns;
        
        try
        {
            // Initialize matrices for Jacobi SVD
            var u = Matrix.Identity(m);
            var a = matrix.Clone();
            var v = Matrix.Identity(n);
            
            var context = new KernelGenerationContext
            {
                DeviceInfo = accelerator.Info,
                UseSharedMemory = true,
                Precision = PrecisionMode.Single,
                WorkGroupDimensions = [Math.Min(256, accelerator.Info.MaxThreadsPerBlock)]
            };

            const int maxIterations = 1000;
            // const float tolerance = 1e-10f; // Reserved for future convergence checks

            // Allocate GPU memory for matrices
            var matrixSize = Math.Min(m, n) * Math.Min(m, n);
            var aData = a.ToArray();
            var uData = u.ToArray();
            var vData = v.ToArray();
            
            var aBuffer = await accelerator.Memory.AllocateAsync(aData.Length * sizeof(float), 
                DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            var uBuffer = await accelerator.Memory.AllocateAsync(uData.Length * sizeof(float), 
                DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            var vBuffer = await accelerator.Memory.AllocateAsync(vData.Length * sizeof(float), 
                DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            var convergenceBuffer = await accelerator.Memory.AllocateAsync(sizeof(float), 
                DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);

            try
            {
                await aBuffer.WriteAsync(aData, 0, cancellationToken).ConfigureAwait(false);
                await uBuffer.WriteAsync(uData, 0, cancellationToken).ConfigureAwait(false);
                await vBuffer.WriteAsync(vData, 0, cancellationToken).ConfigureAwait(false);

                // Get Jacobi SVD rotation kernel
                var jacobiKernel = await kernelManager.GetOrCompileOperationKernelAsync(
                    "JacobiSVD",
                    [typeof(float[]), typeof(float[]), typeof(float[])],
                    typeof(float[]),
                    accelerator,
                    context,
                    null,
                    cancellationToken).ConfigureAwait(false);

                // Jacobi SVD iterations
                for (var iter = 0; iter < maxIterations; iter++)
                {
                    var converged = true;
                    
                    // Iterate over all off-diagonal pairs
                    for (var i = 0; i < Math.Min(m, n) && converged; i++)
                    {
                        for (var j = i + 1; j < Math.Min(m, n); j++)
                        {
                            // Set convergence flag to 0 (not converged)
                            var convergenceFlag = new float[] { 0.0f };
                            await convergenceBuffer.WriteAsync(convergenceFlag, 0, cancellationToken).ConfigureAwait(false);

                            var args = new[]
                            {
                                new KernelArgument { Name = "A", Value = aBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = aBuffer },
                                new KernelArgument { Name = "U", Value = uBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = uBuffer },
                                new KernelArgument { Name = "V", Value = vBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = vBuffer },
                                new KernelArgument { Name = "n", Value = Math.Min(m, n), Type = typeof(int), IsDeviceMemory = false },
                                new KernelArgument { Name = "i", Value = i, Type = typeof(int), IsDeviceMemory = false },
                                new KernelArgument { Name = "j", Value = j, Type = typeof(int), IsDeviceMemory = false },
                                new KernelArgument { Name = "convergence_flag", Value = convergenceBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = convergenceBuffer }
                            };

                            var parameters = LinearAlgebraKernels.GetOptimizedParameters(
                                LinearAlgebraOp.JacobiSVD,
                                (Math.Min(m, n), Math.Min(m, n)),
                                accelerator.Info.Name);

                            var config = new KernelExecutionConfig
                            {
                                GlobalWorkSize = parameters.GlobalWorkSize,
                                LocalWorkSize = parameters.LocalWorkSize,
                                CaptureTimings = false // Avoid overhead in tight loop
                            };

                            var result = await kernelManager.ExecuteKernelAsync(jacobiKernel, args, accelerator, config, cancellationToken).ConfigureAwait(false);
                            
                            if (!result.Success)
                            {
                                throw new InvalidOperationException($"Jacobi SVD rotation failed: {result.ErrorMessage}");
                            }

                            // Check convergence
                            await convergenceBuffer.ReadAsync(convergenceFlag, 0, cancellationToken).ConfigureAwait(false);
                            if (convergenceFlag[0] < 0.5f) // Not converged
                            {
                                converged = false;
                            }
                        }
                    }
                    
                    if (converged) break;
                }

                // Extract singular values using GPU kernel
                var singularValuesKernel = await kernelManager.GetOrCompileOperationKernelAsync(
                    "SingularValues",
                    [typeof(float[]), typeof(float[])],
                    typeof(float[]),
                    accelerator,
                    context,
                    null,
                    cancellationToken).ConfigureAwait(false);

                var sData = new float[Math.Min(m, n) * Math.Min(m, n)];
                var sBuffer = await accelerator.Memory.AllocateAsync(sData.Length * sizeof(float), 
                    DotCompute.Abstractions.MemoryOptions.None, cancellationToken).ConfigureAwait(false);

                var svdArgs = new[]
                {
                    new KernelArgument { Name = "A", Value = aBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = aBuffer },
                    new KernelArgument { Name = "S", Value = sBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = sBuffer },
                    new KernelArgument { Name = "U", Value = uBuffer, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = uBuffer },
                    new KernelArgument { Name = "n", Value = Math.Min(m, n), Type = typeof(int), IsDeviceMemory = false }
                };

                var svdConfig = new KernelExecutionConfig
                {
                    GlobalWorkSize = [((Math.Min(m, n) + 127) / 128) * 128],
                    LocalWorkSize = [128],
                    CaptureTimings = true
                };

                var svdResult = await kernelManager.ExecuteKernelAsync(singularValuesKernel, svdArgs, accelerator, svdConfig, cancellationToken).ConfigureAwait(false);
                
                if (!svdResult.Success)
                {
                    throw new InvalidOperationException($"Singular values extraction failed: {svdResult.ErrorMessage}");
                }

                // Read results back from GPU
                await aBuffer.ReadAsync(aData, 0, cancellationToken).ConfigureAwait(false);
                await uBuffer.ReadAsync(uData, 0, cancellationToken).ConfigureAwait(false);
                await vBuffer.ReadAsync(vData, 0, cancellationToken).ConfigureAwait(false);
                await sBuffer.ReadAsync(sData, 0, cancellationToken).ConfigureAwait(false);

                // Construct result matrices
                CopyArrayToMatrix(uData, u);
                CopyArrayToMatrix(vData, v);
                
                var s = new Matrix(Math.Min(m, n), Math.Min(m, n));
                for (var i = 0; i < Math.Min(m, n); i++)
                {
                    s[i, i] = sData[i * Math.Min(m, n) + i];
                }

                await sBuffer.DisposeAsync().ConfigureAwait(false);
                
                return (u, s, TransposeMatrix(v));
            }
            finally
            {
                await aBuffer.DisposeAsync().ConfigureAwait(false);
                await uBuffer.DisposeAsync().ConfigureAwait(false);
                await vBuffer.DisposeAsync().ConfigureAwait(false);
                await convergenceBuffer.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            // Fall back to CPU implementation if GPU fails
            return await Task.Run(() => ComputeJacobiSVD(matrix), cancellationToken).ConfigureAwait(false);
        }
    }
}