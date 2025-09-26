// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.X86;
using global::System.Runtime.Intrinsics.Arm;

namespace DotCompute.Algorithms.Optimized.Simd;

/// <summary>
/// SIMD reduction operations (DotProduct, HorizontalSum) with cross-platform optimization.
/// </summary>
internal static class SimdReductionOperations
{
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
            if (SimdCapabilities.HasAvx512 && length >= SimdCapabilities.Vector512Size)
            {
                return DotProductAvx512(leftPtr, rightPtr, length);
            }
            else if (SimdCapabilities.HasAvx2 && length >= SimdCapabilities.Vector256Size)
            {
                return DotProductAvx2(leftPtr, rightPtr, length);
            }
            else if (SimdCapabilities.HasNeon && length >= SimdCapabilities.Vector128Size)
            {
                return DotProductNeon(leftPtr, rightPtr, length);
            }
            else if (Sse.IsSupported && length >= SimdCapabilities.Vector128Size)
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
            if (SimdCapabilities.HasAvx512 && length >= SimdCapabilities.Vector512Size)
            {
                return HorizontalSumAvx512(valuesPtr, length);
            }
            else if (SimdCapabilities.HasAvx2 && length >= SimdCapabilities.Vector256Size)
            {
                return HorizontalSumAvx2(valuesPtr, length);
            }
            else if (SimdCapabilities.HasNeon && length >= SimdCapabilities.Vector128Size)
            {
                return HorizontalSumNeon(valuesPtr, length);
            }
            else if (Sse.IsSupported && length >= SimdCapabilities.Vector128Size)
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
    private static unsafe float DotProductAvx512(float* left, float* right, int length)
    {
        var sum = Vector512<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector512Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector512Size)
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
        var vectorCount = length - (length % SimdCapabilities.Vector512Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector512Size)
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
    private static unsafe float DotProductAvx2(float* left, float* right, int length)
    {
        var sum = Vector256<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector256Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector256Size)
        {
            var leftVec = Avx.LoadVector256(left + i);
            var rightVec = Avx.LoadVector256(right + i);

            if (SimdCapabilities.HasFma)
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
        var vectorCount = length - (length % SimdCapabilities.Vector256Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector256Size)
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

    #endregion

    #region ARM NEON Implementations

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float DotProductNeon(float* left, float* right, int length)
    {
        var sum = Vector128<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
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
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
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

    #endregion

    #region SSE Implementations

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe float DotProductSse(float* left, float* right, int length)
    {
        var sum = Vector128<float>.Zero;
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
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
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
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

    #endregion

    #region Fallback Implementations

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

    #endregion
}