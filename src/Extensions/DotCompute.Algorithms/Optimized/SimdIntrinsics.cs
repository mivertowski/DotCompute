// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace DotCompute.Algorithms.Optimized;

/// <summary>
/// Cross-platform SIMD intrinsics layer providing optimal vector operations
/// for all target architectures (x86-64 AVX2/SSE, ARM64 NEON, fallback).
/// Abstracts away hardware differences while maximizing performance.
/// </summary>
public static class SimdIntrinsics
{
    // Hardware capability detection
    public static readonly bool HasAvx512 = Avx512F.IsSupported;
    public static readonly bool HasAvx2 = Avx2.IsSupported;
    public static readonly bool HasFma = Fma.IsSupported;
    public static readonly bool HasSse42 = Sse42.IsSupported;
    public static readonly bool HasNeon = AdvSimd.IsSupported;

    // Vector sizes for different architectures

    public static readonly int Vector512Size = HasAvx512 ? Vector512<float>.Count : 0;
    public static readonly int Vector256Size = HasAvx2 ? Vector256<float>.Count : 0;
    public static readonly int Vector128Size = Vector128<float>.Count;
    public static readonly int VectorSize = Vector<float>.Count;

    // Optimal vector size for current architecture

    public static readonly int OptimalVectorSize = HasAvx512 ? Vector512Size :

                                                    HasAvx2 ? Vector256Size :
                                                    Vector128Size;


    /// <summary>
    /// Cross-platform SIMD vector addition with optimal instruction selection.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Add(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
    {
        if (left.Length != right.Length || left.Length != result.Length)
        {

            throw new ArgumentException("All spans must have the same length");
        }


        var length = left.Length;
        if (length == 0)
        {
            return;
        }


        fixed (float* leftPtr = left, rightPtr = right, resultPtr = result)
        {
            if (HasAvx512 && length >= Vector512Size)
            {
                AddAvx512(leftPtr, rightPtr, resultPtr, length);
            }
            else if (HasAvx2 && length >= Vector256Size)
            {
                AddAvx2(leftPtr, rightPtr, resultPtr, length);
            }
            else if (HasNeon && length >= Vector128Size)
            {
                AddNeon(leftPtr, rightPtr, resultPtr, length);
            }
            else if (Sse.IsSupported && length >= Vector128Size)
            {
                AddSse(leftPtr, rightPtr, resultPtr, length);
            }
            else
            {
                AddFallback(leftPtr, rightPtr, resultPtr, length);
            }
        }
    }


    /// <summary>
    /// Cross-platform SIMD vector multiplication with FMA support.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Multiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
    {
        if (left.Length != right.Length || left.Length != result.Length)
        {

            throw new ArgumentException("All spans must have the same length");
        }


        var length = left.Length;
        if (length == 0)
        {
            return;
        }


        fixed (float* leftPtr = left, rightPtr = right, resultPtr = result)
        {
            if (HasAvx512 && length >= Vector512Size)
            {
                MultiplyAvx512(leftPtr, rightPtr, resultPtr, length);
            }
            else if (HasAvx2 && length >= Vector256Size)
            {
                MultiplyAvx2(leftPtr, rightPtr, resultPtr, length);
            }
            else if (HasNeon && length >= Vector128Size)
            {
                MultiplyNeon(leftPtr, rightPtr, resultPtr, length);
            }
            else if (Sse.IsSupported && length >= Vector128Size)
            {
                MultiplySse(leftPtr, rightPtr, resultPtr, length);
            }
            else
            {
                MultiplyFallback(leftPtr, rightPtr, resultPtr, length);
            }
        }
    }


    /// <summary>
    /// Cross-platform SIMD fused multiply-add (FMA) operation: result = (a * b) + c
    /// </summary>
    /// <param name="a">First multiplicand</param>
    /// <param name="b">Second multiplicand</param>
    /// <param name="c">Addend</param>
    /// <param name="result">Result span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void FusedMultiplyAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b,

        ReadOnlySpan<float> c, Span<float> result)
    {
        if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
        {

            throw new ArgumentException("All spans must have the same length");
        }


        var length = a.Length;
        if (length == 0)
        {
            return;
        }


        fixed (float* aPtr = a, bPtr = b, cPtr = c, resultPtr = result)
        {
            if (HasAvx512 && HasFma && length >= Vector512Size)
            {
                FmaAvx512(aPtr, bPtr, cPtr, resultPtr, length);
            }
            else if (HasAvx2 && HasFma && length >= Vector256Size)
            {
                FmaAvx2(aPtr, bPtr, cPtr, resultPtr, length);
            }
            else if (HasNeon && length >= Vector128Size)
            {
                FmaNeon(aPtr, bPtr, cPtr, resultPtr, length);
            }
            else if (Sse.IsSupported && length >= Vector128Size)
            {
                FmaSse(aPtr, bPtr, cPtr, resultPtr, length);
            }
            else
            {
                FmaFallback(aPtr, bPtr, cPtr, resultPtr, length);
            }
        }
    }


    /// <summary>
    /// Cross-platform SIMD dot product with horizontal reduction.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <returns>Dot product result</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe float DotProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length)
        {

            throw new ArgumentException("Both spans must have the same length");
        }


        var length = left.Length;
        if (length == 0)
        {
            return 0.0f;
        }


        fixed (float* leftPtr = left, rightPtr = right)
        {
            if (HasAvx512 && length >= Vector512Size)
            {
                return DotProductAvx512(leftPtr, rightPtr, length);
            }
            else if (HasAvx2 && length >= Vector256Size)
            {
                return DotProductAvx2(leftPtr, rightPtr, length);
            }
            else if (HasNeon && length >= Vector128Size)
            {
                return DotProductNeon(leftPtr, rightPtr, length);
            }
            else if (Sse.IsSupported && length >= Vector128Size)
            {
                return DotProductSse(leftPtr, rightPtr, length);
            }
            else
            {
                return DotProductFallback(leftPtr, rightPtr, length);
            }
        }
    }


    /// <summary>
    /// Cross-platform SIMD matrix transpose with cache-friendly blocking.
    /// </summary>
    /// <param name="source">Source matrix data (row-major)</param>
    /// <param name="dest">Destination matrix data (column-major)</param>
    /// <param name="rows">Number of rows</param>
    /// <param name="cols">Number of columns</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Transpose(ReadOnlySpan<float> source, Span<float> dest, int rows, int cols)
    {
        if (source.Length != rows * cols || dest.Length != rows * cols)
        {

            throw new ArgumentException("Source and destination must match matrix dimensions");
        }


        fixed (float* srcPtr = source, dstPtr = dest)
        {
            if (HasAvx2 && rows >= 8 && cols >= 8)
            {
                TransposeAvx2(srcPtr, dstPtr, rows, cols);
            }
            else if (HasNeon && rows >= 4 && cols >= 4)
            {
                TransposeNeon(srcPtr, dstPtr, rows, cols);
            }
            else if (Sse2.IsSupported && rows >= 4 && cols >= 4)
            {
                TransposeSse2(srcPtr, dstPtr, rows, cols);
            }
            else
            {
                TransposeFallback(srcPtr, dstPtr, rows, cols);
            }
        }
    }


    /// <summary>
    /// Cross-platform SIMD horizontal sum with optimal reduction.
    /// </summary>
    /// <param name="values">Values to sum</param>
    /// <returns>Sum of all values</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe float HorizontalSum(ReadOnlySpan<float> values)
    {
        var length = values.Length;
        if (length == 0)
        {
            return 0.0f;
        }


        fixed (float* valuesPtr = values)
        {
            if (HasAvx512 && length >= Vector512Size)
            {
                return HorizontalSumAvx512(valuesPtr, length);
            }
            else if (HasAvx2 && length >= Vector256Size)
            {
                return HorizontalSumAvx2(valuesPtr, length);
            }
            else if (HasNeon && length >= Vector128Size)
            {
                return HorizontalSumNeon(valuesPtr, length);
            }
            else if (Sse.IsSupported && length >= Vector128Size)
            {
                return HorizontalSumSse(valuesPtr, length);
            }
            else
            {
                return HorizontalSumFallback(valuesPtr, length);
            }
        }
    }

    #region AVX-512 Implementations


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AddAvx512(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector512Size);


        for (; i < vectorCount; i += Vector512Size)
        {
            var leftVec = Avx512F.LoadVector512(left + i);
            var rightVec = Avx512F.LoadVector512(right + i);
            var resultVec = Avx512F.Add(leftVec, rightVec);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = left[i] + right[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void MultiplyAvx512(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector512Size);


        for (; i < vectorCount; i += Vector512Size)
        {
            var leftVec = Avx512F.LoadVector512(left + i);
            var rightVec = Avx512F.LoadVector512(right + i);
            var resultVec = Avx512F.Multiply(leftVec, rightVec);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = left[i] * right[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void FmaAvx512(float* a, float* b, float* c, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector512Size);


        for (; i < vectorCount; i += Vector512Size)
        {
            var aVec = Avx512F.LoadVector512(a + i);
            var bVec = Avx512F.LoadVector512(b + i);
            var cVec = Avx512F.LoadVector512(c + i);
            var resultVec = Avx512F.FusedMultiplyAdd(aVec, bVec, cVec);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = a[i] * b[i] + c[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float DotProductAvx512(float* left, float* right, int length)
    {
        var sum = Vector512<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % Vector512Size);


        for (; i < vectorCount; i += Vector512Size)
        {
            var leftVec = Avx512F.LoadVector512(left + i);
            var rightVec = Avx512F.LoadVector512(right + i);
            sum = Avx512F.FusedMultiplyAdd(leftVec, rightVec, sum);
        }

        // Horizontal sum of vector

        var result = HorizontalSumVector512(sum);

        // Handle remaining elements

        for (; i < length; i++)
        {
            result += left[i] * right[i];
        }


        return result;
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float HorizontalSumAvx512(float* values, int length)
    {
        var sum = Vector512<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % Vector512Size);


        for (; i < vectorCount; i += Vector512Size)
        {
            var vec = Avx512F.LoadVector512(values + i);
            sum = Avx512F.Add(sum, vec);
        }


        var result = HorizontalSumVector512(sum);

        // Handle remaining elements

        for (; i < length; i++)
        {
            result += values[i];
        }


        return result;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalSumVector512(Vector512<float> vec)
    {
        var hi256 = Avx512F.ExtractVector256(vec, 1);
        var lo256 = Avx512F.ExtractVector256(vec, 0);
        var sum256 = Avx.Add(hi256, lo256);
        return HorizontalSumVector256(sum256);
    }

    #endregion

    #region AVX2 Implementations


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AddAvx2(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector256Size);


        for (; i < vectorCount; i += Vector256Size)
        {
            var leftVec = Avx.LoadVector256(left + i);
            var rightVec = Avx.LoadVector256(right + i);
            var resultVec = Avx.Add(leftVec, rightVec);
            Avx.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = left[i] + right[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void MultiplyAvx2(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector256Size);


        for (; i < vectorCount; i += Vector256Size)
        {
            var leftVec = Avx.LoadVector256(left + i);
            var rightVec = Avx.LoadVector256(right + i);
            var resultVec = Avx.Multiply(leftVec, rightVec);
            Avx.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = left[i] * right[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void FmaAvx2(float* a, float* b, float* c, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector256Size);


        for (; i < vectorCount; i += Vector256Size)
        {
            var aVec = Avx.LoadVector256(a + i);
            var bVec = Avx.LoadVector256(b + i);
            var cVec = Avx.LoadVector256(c + i);
            Vector256<float> resultVec;


            if (HasFma)
            {
                resultVec = Fma.MultiplyAdd(aVec, bVec, cVec);
            }
            else
            {
                resultVec = Avx.Add(Avx.Multiply(aVec, bVec), cVec);
            }


            Avx.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = a[i] * b[i] + c[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float DotProductAvx2(float* left, float* right, int length)
    {
        var sum = Vector256<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % Vector256Size);


        for (; i < vectorCount; i += Vector256Size)
        {
            var leftVec = Avx.LoadVector256(left + i);
            var rightVec = Avx.LoadVector256(right + i);


            if (HasFma)
            {
                sum = Fma.MultiplyAdd(leftVec, rightVec, sum);
            }
            else
            {
                sum = Avx.Add(sum, Avx.Multiply(leftVec, rightVec));
            }
        }

        // Horizontal sum of vector

        var result = HorizontalSumVector256(sum);

        // Handle remaining elements

        for (; i < length; i++)
        {
            result += left[i] * right[i];
        }


        return result;
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float HorizontalSumAvx2(float* values, int length)
    {
        var sum = Vector256<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % Vector256Size);


        for (; i < vectorCount; i += Vector256Size)
        {
            var vec = Avx.LoadVector256(values + i);
            sum = Avx.Add(sum, vec);
        }


        var result = HorizontalSumVector256(sum);

        // Handle remaining elements

        for (; i < length; i++)
        {
            result += values[i];
        }


        return result;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalSumVector256(Vector256<float> vec)
    {
        var hi128 = Avx.ExtractVector128(vec, 1);
        var lo128 = Avx.ExtractVector128(vec, 0);
        var sum128 = Sse.Add(hi128, lo128);
        return HorizontalSumVector128(sum128);
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeAvx2(float* source, float* dest, int rows, int cols)
    {
        // 8x8 transpose using AVX2
        const int blockSize = 8;


        for (var ii = 0; ii < rows; ii += blockSize)
        {
            for (var jj = 0; jj < cols; jj += blockSize)
            {
                TransposeBlock8x8Avx2(source, dest, ii, jj, rows, cols, cols, rows);
            }
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeBlock8x8Avx2(float* src, float* dst,

        int blockRow, int blockCol, int srcRows, int srcCols, int srcStride, int dstStride)
    {
        var effectiveRows = Math.Min(8, srcRows - blockRow);
        var effectiveCols = Math.Min(8, srcCols - blockCol);

        // Fallback to scalar for partial blocks

        for (var i = 0; i < effectiveRows; i++)
        {
            for (var j = 0; j < effectiveCols; j++)
            {
                dst[(blockCol + j) * dstStride + (blockRow + i)] =

                    src[(blockRow + i) * srcStride + (blockCol + j)];
            }
        }
    }

    #endregion

    #region ARM NEON Implementations


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AddNeon(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector128Size);


        for (; i < vectorCount; i += Vector128Size)
        {
            var leftVec = AdvSimd.LoadVector128(left + i);
            var rightVec = AdvSimd.LoadVector128(right + i);
            var resultVec = AdvSimd.Add(leftVec, rightVec);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = left[i] + right[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void MultiplyNeon(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector128Size);


        for (; i < vectorCount; i += Vector128Size)
        {
            var leftVec = AdvSimd.LoadVector128(left + i);
            var rightVec = AdvSimd.LoadVector128(right + i);
            var resultVec = AdvSimd.Multiply(leftVec, rightVec);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = left[i] * right[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void FmaNeon(float* a, float* b, float* c, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector128Size);


        for (; i < vectorCount; i += Vector128Size)
        {
            var aVec = AdvSimd.LoadVector128(a + i);
            var bVec = AdvSimd.LoadVector128(b + i);
            var cVec = AdvSimd.LoadVector128(c + i);
            var resultVec = AdvSimd.FusedMultiplyAdd(cVec, aVec, bVec);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = a[i] * b[i] + c[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float DotProductNeon(float* left, float* right, int length)
    {
        var sum = Vector128<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % Vector128Size);


        for (; i < vectorCount; i += Vector128Size)
        {
            var leftVec = AdvSimd.LoadVector128(left + i);
            var rightVec = AdvSimd.LoadVector128(right + i);
            sum = AdvSimd.FusedMultiplyAdd(sum, leftVec, rightVec);
        }

        // Horizontal sum of vector

        var result = HorizontalSumVector128(sum);

        // Handle remaining elements

        for (; i < length; i++)
        {
            result += left[i] * right[i];
        }


        return result;
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float HorizontalSumNeon(float* values, int length)
    {
        var sum = Vector128<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % Vector128Size);


        for (; i < vectorCount; i += Vector128Size)
        {
            var vec = AdvSimd.LoadVector128(values + i);
            sum = AdvSimd.Add(sum, vec);
        }


        var result = HorizontalSumVector128(sum);

        // Handle remaining elements

        for (; i < length; i++)
        {
            result += values[i];
        }


        return result;
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeNeon(float* source, float* dest, int rows, int cols)
    {
        // 4x4 transpose using NEON
        const int blockSize = 4;


        for (var ii = 0; ii < rows; ii += blockSize)
        {
            for (var jj = 0; jj < cols; jj += blockSize)
            {
                TransposeBlock4x4Neon(source, dest, ii, jj, rows, cols, cols, rows);
            }
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeBlock4x4Neon(float* src, float* dst,

        int blockRow, int blockCol, int srcRows, int srcCols, int srcStride, int dstStride)
    {
        var effectiveRows = Math.Min(4, srcRows - blockRow);
        var effectiveCols = Math.Min(4, srcCols - blockCol);

        // Fallback to scalar for now

        for (var i = 0; i < effectiveRows; i++)
        {
            for (var j = 0; j < effectiveCols; j++)
            {
                dst[(blockCol + j) * dstStride + (blockRow + i)] =

                    src[(blockRow + i) * srcStride + (blockCol + j)];
            }
        }
    }

    #endregion

    #region SSE Implementations


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AddSse(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector128Size);


        for (; i < vectorCount; i += Vector128Size)
        {
            var leftVec = Sse.LoadVector128(left + i);
            var rightVec = Sse.LoadVector128(right + i);
            var resultVec = Sse.Add(leftVec, rightVec);
            Sse.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = left[i] + right[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void MultiplySse(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector128Size);


        for (; i < vectorCount; i += Vector128Size)
        {
            var leftVec = Sse.LoadVector128(left + i);
            var rightVec = Sse.LoadVector128(right + i);
            var resultVec = Sse.Multiply(leftVec, rightVec);
            Sse.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = left[i] * right[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void FmaSse(float* a, float* b, float* c, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % Vector128Size);


        for (; i < vectorCount; i += Vector128Size)
        {
            var aVec = Sse.LoadVector128(a + i);
            var bVec = Sse.LoadVector128(b + i);
            var cVec = Sse.LoadVector128(c + i);
            var resultVec = Sse.Add(Sse.Multiply(aVec, bVec), cVec);
            Sse.Store(result + i, resultVec);
        }

        // Handle remaining elements

        for (; i < length; i++)
        {
            result[i] = a[i] * b[i] + c[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float DotProductSse(float* left, float* right, int length)
    {
        var sum = Vector128<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % Vector128Size);


        for (; i < vectorCount; i += Vector128Size)
        {
            var leftVec = Sse.LoadVector128(left + i);
            var rightVec = Sse.LoadVector128(right + i);
            sum = Sse.Add(sum, Sse.Multiply(leftVec, rightVec));
        }

        // Horizontal sum of vector

        var result = HorizontalSumVector128(sum);

        // Handle remaining elements

        for (; i < length; i++)
        {
            result += left[i] * right[i];
        }


        return result;
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float HorizontalSumSse(float* values, int length)
    {
        var sum = Vector128<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % Vector128Size);


        for (; i < vectorCount; i += Vector128Size)
        {
            var vec = Sse.LoadVector128(values + i);
            sum = Sse.Add(sum, vec);
        }


        var result = HorizontalSumVector128(sum);

        // Handle remaining elements

        for (; i < length; i++)
        {
            result += values[i];
        }


        return result;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalSumVector128(Vector128<float> vec)
    {
        var temp = Sse.Add(vec, Sse.Shuffle(vec, vec, 0b_11_10_01_00));
        temp = Sse.Add(temp, Sse.Shuffle(temp, temp, 0b_01_00_11_10));
        return temp.ToScalar();
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeSse2(float* source, float* dest, int rows, int cols)
    {
        // 4x4 transpose using SSE2
        const int blockSize = 4;


        for (var ii = 0; ii < rows; ii += blockSize)
        {
            for (var jj = 0; jj < cols; jj += blockSize)
            {
                TransposeBlock4x4Sse2(source, dest, ii, jj, rows, cols, cols, rows);
            }
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeBlock4x4Sse2(float* src, float* dst,

        int blockRow, int blockCol, int srcRows, int srcCols, int srcStride, int dstStride)
    {
        var effectiveRows = Math.Min(4, srcRows - blockRow);
        var effectiveCols = Math.Min(4, srcCols - blockCol);


        if (effectiveRows == 4 && effectiveCols == 4)
        {
            // Full 4x4 block - use SSE2 transpose
            var row0 = Sse.LoadVector128(src + (blockRow + 0) * srcStride + blockCol);
            var row1 = Sse.LoadVector128(src + (blockRow + 1) * srcStride + blockCol);
            var row2 = Sse.LoadVector128(src + (blockRow + 2) * srcStride + blockCol);
            var row3 = Sse.LoadVector128(src + (blockRow + 3) * srcStride + blockCol);

            // Transpose 4x4 using shuffle operations

            var tmp0 = Sse.UnpackLow(row0, row1);
            var tmp2 = Sse.UnpackLow(row2, row3);
            var tmp1 = Sse.UnpackHigh(row0, row1);
            var tmp3 = Sse.UnpackHigh(row2, row3);


            var col0 = Sse.MoveLowToHigh(tmp0, tmp2);
            var col1 = Sse.MoveHighToLow(tmp2, tmp0);
            var col2 = Sse.MoveLowToHigh(tmp1, tmp3);
            var col3 = Sse.MoveHighToLow(tmp3, tmp1);


            Sse.Store(dst + (blockCol + 0) * dstStride + blockRow, col0);
            Sse.Store(dst + (blockCol + 1) * dstStride + blockRow, col1);
            Sse.Store(dst + (blockCol + 2) * dstStride + blockRow, col2);
            Sse.Store(dst + (blockCol + 3) * dstStride + blockRow, col3);
        }
        else
        {
            // Partial block - use scalar transpose
            for (var i = 0; i < effectiveRows; i++)
            {
                for (var j = 0; j < effectiveCols; j++)
                {
                    dst[(blockCol + j) * dstStride + (blockRow + i)] =

                        src[(blockRow + i) * srcStride + (blockCol + j)];
                }
            }
        }
    }

    #endregion

    #region Fallback Implementations


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AddFallback(float* left, float* right, float* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = left[i] + right[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void MultiplyFallback(float* left, float* right, float* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = left[i] * right[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void FmaFallback(float* a, float* b, float* c, float* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = a[i] * b[i] + c[i];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float DotProductFallback(float* left, float* right, int length)
    {
        var sum = 0.0f;
        for (var i = 0; i < length; i++)
        {
            sum += left[i] * right[i];
        }
        return sum;
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float HorizontalSumFallback(float* values, int length)
    {
        var sum = 0.0f;
        for (var i = 0; i < length; i++)
        {
            sum += values[i];
        }
        return sum;
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void TransposeFallback(float* source, float* dest, int rows, int cols)
    {
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                dest[j * rows + i] = source[i * cols + j];
            }
        }
    }


    #endregion
}