// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using DotCompute.Backends.CPU.Intrinsics;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.SIMD;

/// <summary>
/// SIMD-optimized matrix operations with cache-friendly algorithms
/// </summary>
public sealed class SimdMatrixOperations(SimdSummary capabilities, ILogger logger) : IDisposable
{
    private readonly SimdSummary _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private volatile bool _disposed;

    /// <summary>
    /// Executes matrix operations with SIMD optimization
    /// </summary>
    public void Execute<T>(
        ReadOnlySpan<T> matrixA,
        ReadOnlySpan<T> matrixB,
        Span<T> result,
        int rows,
        int cols,
        MatrixOperation operation,
        ExecutionContext context) where T : unmanaged
    {
        switch (operation)
        {
            case MatrixOperation.Multiply:
                ExecuteMatrixMultiply(matrixA, matrixB, result, rows, cols, context);
                break;
            case MatrixOperation.Add:
                ExecuteMatrixAdd(matrixA, matrixB, result, rows, cols, context);
                break;
            case MatrixOperation.Subtract:
                ExecuteMatrixSubtract(matrixA, matrixB, result, rows, cols, context);
                break;
            case MatrixOperation.Transpose:
                ExecuteMatrixTranspose(matrixA, result, rows, cols, context);
                break;
            default:
                throw new ArgumentException($"Unsupported matrix operation: {operation}");
        }
    }

    /// <summary>
    /// Matrix multiplication with cache-blocking and SIMD optimization
    /// </summary>
    private void ExecuteMatrixMultiply<T>(
        ReadOnlySpan<T> matrixA,
        ReadOnlySpan<T> matrixB,
        Span<T> result,
        int rows,
        int cols,
        ExecutionContext context) where T : unmanaged
    {
        // Cache-blocking parameters
        const int blockSize = 64;

        if (typeof(T) == typeof(float))
        {
            ExecuteFloatMatrixMultiply(
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(matrixA),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(matrixB),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(result),
                rows, cols, blockSize);
        }
        else if (typeof(T) == typeof(double))
        {
            ExecuteDoubleMatrixMultiply(
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, double>(matrixA),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, double>(matrixB),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, double>(result),
                rows, cols, blockSize);
        }
        else
        {
            // Fallback to scalar implementation
            ExecuteScalarMatrixMultiply(matrixA, matrixB, result, rows, cols);
        }
    }

    /// <summary>
    /// Optimized float matrix multiplication with AVX/SSE
    /// </summary>
    private unsafe void ExecuteFloatMatrixMultiply(
        ReadOnlySpan<float> matrixA,
        ReadOnlySpan<float> matrixB,
        Span<float> result,
        int rows,
        int cols,
        int blockSize)
    {
        fixed (float* ptrA = matrixA, ptrB = matrixB, ptrResult = result)
        {
            // Cache-blocked matrix multiplication
            for (var ii = 0; ii < rows; ii += blockSize)
            {
                for (var jj = 0; jj < cols; jj += blockSize)
                {
                    for (var kk = 0; kk < cols; kk += blockSize)
                    {
                        var iMax = Math.Min(ii + blockSize, rows);
                        var jMax = Math.Min(jj + blockSize, cols);
                        var kMax = Math.Min(kk + blockSize, cols);

                        // Inner block multiplication with SIMD
                        ExecuteBlockMultiply(ptrA, ptrB, ptrResult,
                            ii, iMax, jj, jMax, kk, kMax, cols);
                    }
                }
            }
        }
    }

    /// <summary>
    /// SIMD-optimized block multiplication
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ExecuteBlockMultiply(
        float* ptrA, float* ptrB, float* ptrResult,
        int iStart, int iEnd, int jStart, int jEnd, int kStart, int kEnd, int cols)
    {
        for (var i = iStart; i < iEnd; i++)
        {
            for (var j = jStart; j < jEnd; j += 8) // Process 8 elements at once with AVX
            {
                var jMax = Math.Min(j + 8, jEnd);
                var vectorSize = jMax - j;

                if (vectorSize >= 8 && _capabilities.SupportsAvx2)
                {
                    // AVX2 implementation
                    var sum = Vector256<float>.Zero;

                    for (var k = kStart; k < kEnd; k++)
                    {
                        var aVal = Vector256.Create(ptrA[i * cols + k]);
                        var bVec = Vector256.Load(ptrB + k * cols + j);
                        sum = Vector256.Add(sum, Vector256.Multiply(aVal, bVec));
                    }

                    var currentResult = Vector256.Load(ptrResult + i * cols + j);
                    var newResult = Vector256.Add(currentResult, sum);
                    Vector256.Store(newResult, ptrResult + i * cols + j);
                }
                else if (vectorSize >= 4 && _capabilities.SupportsSse2)
                {
                    // SSE implementation
                    var sum = Vector128<float>.Zero;

                    for (var k = kStart; k < kEnd; k++)
                    {
                        var aVal = Vector128.Create(ptrA[i * cols + k]);
                        var bVec = Vector128.Load(ptrB + k * cols + j);
                        sum = Vector128.Add(sum, Vector128.Multiply(aVal, bVec));
                    }

                    var currentResult = Vector128.Load(ptrResult + i * cols + j);
                    var newResult = Vector128.Add(currentResult, sum);
                    Vector128.Store(newResult, ptrResult + i * cols + j);
                }
                else
                {
                    // Scalar fallback
                    for (var jj = j; jj < jMax; jj++)
                    {
                        float sum = 0;
                        for (var k = kStart; k < kEnd; k++)
                        {
                            sum += ptrA[i * cols + k] * ptrB[k * cols + jj];
                        }
                        ptrResult[i * cols + jj] += sum;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Matrix addition with SIMD optimization
    /// </summary>
    private void ExecuteMatrixAdd<T>(
        ReadOnlySpan<T> matrixA,
        ReadOnlySpan<T> matrixB,
        Span<T> result,
        int rows,
        int cols,
        ExecutionContext context) where T : unmanaged
    {
        var totalElements = rows * cols;

        if (typeof(T) == typeof(float))
        {
            ExecuteVectorizedAdd(
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(matrixA),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(matrixB),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(result),
                totalElements);
        }
        else
        {
            // Generic implementation
            for (var i = 0; i < totalElements; i++)
            {
                result[i] = AddGeneric(matrixA[i], matrixB[i]);
            }
        }
    }

    /// <summary>
    /// Matrix subtraction with SIMD optimization
    /// </summary>
    private void ExecuteMatrixSubtract<T>(
        ReadOnlySpan<T> matrixA,
        ReadOnlySpan<T> matrixB,
        Span<T> result,
        int rows,
        int cols,
        ExecutionContext context) where T : unmanaged
    {
        var totalElements = rows * cols;

        if (typeof(T) == typeof(float))
        {
            ExecuteVectorizedSubtract(
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(matrixA),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(matrixB),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(result),
                totalElements);
        }
        else
        {
            // Generic implementation
            for (var i = 0; i < totalElements; i++)
            {
                result[i] = SubtractGeneric(matrixA[i], matrixB[i]);
            }
        }
    }

    /// <summary>
    /// Matrix transpose with cache-friendly access patterns
    /// </summary>
    private static void ExecuteMatrixTranspose<T>(
        ReadOnlySpan<T> matrix,
        Span<T> result,
        int rows,
        int cols,
        ExecutionContext context) where T : unmanaged
    {
        const int blockSize = 32; // Optimized for cache lines

        for (var ii = 0; ii < rows; ii += blockSize)
        {
            for (var jj = 0; jj < cols; jj += blockSize)
            {
                var iMax = Math.Min(ii + blockSize, rows);
                var jMax = Math.Min(jj + blockSize, cols);

                // Transpose block
                for (var i = ii; i < iMax; i++)
                {
                    for (var j = jj; j < jMax; j++)
                    {
                        result[j * rows + i] = matrix[i * cols + j];
                    }
                }
            }
        }
    }

    /// <summary>
    /// Vectorized addition for float arrays
    /// </summary>
    private unsafe void ExecuteVectorizedAdd(
        ReadOnlySpan<float> a,
        ReadOnlySpan<float> b,
        Span<float> result,
        int length)
    {
        fixed (float* ptrA = a, ptrB = b, ptrResult = result)
        {
            var i = 0;

            // AVX2 processing
            if (_capabilities.SupportsAvx2)
            {
                for (; i <= length - 8; i += 8)
                {
                    var vecA = Vector256.Load(ptrA + i);
                    var vecB = Vector256.Load(ptrB + i);
                    var sum = Vector256.Add(vecA, vecB);
                    Vector256.Store(sum, ptrResult + i);
                }
            }
            // SSE processing
            else if (_capabilities.SupportsSse2)
            {
                for (; i <= length - 4; i += 4)
                {
                    var vecA = Vector128.Load(ptrA + i);
                    var vecB = Vector128.Load(ptrB + i);
                    var sum = Vector128.Add(vecA, vecB);
                    Vector128.Store(sum, ptrResult + i);
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                ptrResult[i] = ptrA[i] + ptrB[i];
            }
        }
    }

    /// <summary>
    /// Vectorized subtraction for float arrays
    /// </summary>
    private unsafe void ExecuteVectorizedSubtract(
        ReadOnlySpan<float> a,
        ReadOnlySpan<float> b,
        Span<float> result,
        int length)
    {
        fixed (float* ptrA = a, ptrB = b, ptrResult = result)
        {
            var i = 0;

            // AVX2 processing
            if (_capabilities.SupportsAvx2)
            {
                for (; i <= length - 8; i += 8)
                {
                    var vecA = Vector256.Load(ptrA + i);
                    var vecB = Vector256.Load(ptrB + i);
                    var diff = Vector256.Subtract(vecA, vecB);
                    Vector256.Store(diff, ptrResult + i);
                }
            }
            // SSE processing
            else if (_capabilities.SupportsSse2)
            {
                for (; i <= length - 4; i += 4)
                {
                    var vecA = Vector128.Load(ptrA + i);
                    var vecB = Vector128.Load(ptrB + i);
                    var diff = Vector128.Subtract(vecA, vecB);
                    Vector128.Store(diff, ptrResult + i);
                }
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                ptrResult[i] = ptrA[i] - ptrB[i];
            }
        }
    }

    // Helper methods for generic arithmetic
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe T AddGeneric<T>(T a, T b) where T : unmanaged
    {
        if (typeof(T) == typeof(float))
        {
            var fa = *(float*)&a;
            var fb = *(float*)&b;
            var result = fa + fb;
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var da = *(double*)&a;
            var db = *(double*)&b;
            var result = da + db;
            return *(T*)&result;
        }

        throw new NotSupportedException($"Type {typeof(T)} not supported for addition");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe T SubtractGeneric<T>(T a, T b) where T : unmanaged
    {
        if (typeof(T) == typeof(float))
        {
            var fa = *(float*)&a;
            var fb = *(float*)&b;
            var result = fa - fb;
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var da = *(double*)&a;
            var db = *(double*)&b;
            var result = da - db;
            return *(T*)&result;
        }

        throw new NotSupportedException($"Type {typeof(T)} not supported for subtraction");
    }

    // Stub implementations
    private static void ExecuteDoubleMatrixMultiply(
        ReadOnlySpan<double> matrixA,
        ReadOnlySpan<double> matrixB,
        Span<double> result,
        int rows, int cols, int blockSize)
    {
        // Simplified implementation - full version would use double-precision SIMD
        ExecuteScalarMatrixMultiply(
            System.Runtime.InteropServices.MemoryMarshal.Cast<double, float>(matrixA),
            System.Runtime.InteropServices.MemoryMarshal.Cast<double, float>(matrixB),
            System.Runtime.InteropServices.MemoryMarshal.Cast<double, float>(result),
            rows, cols);
    }

    private static void ExecuteScalarMatrixMultiply<T>(
        ReadOnlySpan<T> matrixA,
        ReadOnlySpan<T> matrixB,
        Span<T> result,
        int rows, int cols) where T : unmanaged
    {
        // Basic scalar matrix multiplication
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                var sum = default(T);
                for (var k = 0; k < cols; k++)
                {
                    sum = AddGeneric(sum, MultiplyGeneric(
                        matrixA[i * cols + k],
                        matrixB[k * cols + j]));
                }
                result[i * cols + j] = sum;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe T MultiplyGeneric<T>(T a, T b) where T : unmanaged
    {
        if (typeof(T) == typeof(float))
        {
            var fa = *(float*)&a;
            var fb = *(float*)&b;
            var result = fa * fb;
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var da = *(double*)&a;
            var db = *(double*)&b;
            var result = da * db;
            return *(T*)&result;
        }

        throw new NotSupportedException($"Type {typeof(T)} not supported for multiplication");
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogDebug("SIMD Matrix Operations disposed");
        }
    }
}
