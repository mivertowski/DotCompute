
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace DotCompute.Algorithms.Optimized.Simd;

/// <summary>
/// SIMD-accelerated comparison operations with cross-platform optimization.
/// Provides vectorized comparisons for all supported architectures.
/// </summary>
public static class SimdComparisonOperations
{
    /// <summary>
    /// Vectorized element-wise equality comparison.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span (1.0f for equal, 0.0f for not equal)</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Equal(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
    {
        if (left.Length != right.Length || left.Length != result.Length)
        {
            throw new ArgumentException("Spans must have equal length");
        }

        var length = left.Length;

        fixed (float* pLeft = left)
        fixed (float* pRight = right)
        fixed (float* pResult = result)
        {
            if (SimdCapabilities.HasAvx512)
            {
                EqualAvx512(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasAvx2)
            {
                EqualAvx2(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasNeon)
            {
                EqualNeon(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasSse42)
            {
                EqualSse(pLeft, pRight, pResult, length);
            }
            else
            {
                EqualFallback(pLeft, pRight, pResult, length);
            }
        }
    }

    /// <summary>
    /// Vectorized element-wise greater than comparison.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span (1.0f for greater, 0.0f otherwise)</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void GreaterThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
    {
        if (left.Length != right.Length || left.Length != result.Length)
        {
            throw new ArgumentException("Spans must have equal length");
        }

        var length = left.Length;

        fixed (float* pLeft = left)
        fixed (float* pRight = right)
        fixed (float* pResult = result)
        {
            if (SimdCapabilities.HasAvx512)
            {
                GreaterThanAvx512(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasAvx2)
            {
                GreaterThanAvx2(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasNeon)
            {
                GreaterThanNeon(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasSse42)
            {
                GreaterThanSse(pLeft, pRight, pResult, length);
            }
            else
            {
                GreaterThanFallback(pLeft, pRight, pResult, length);
            }
        }
    }

    /// <summary>
    /// Vectorized element-wise less than comparison.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span (1.0f for less, 0.0f otherwise)</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void LessThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
    {
        if (left.Length != right.Length || left.Length != result.Length)
        {
            throw new ArgumentException("Spans must have equal length");
        }

        var length = left.Length;

        fixed (float* pLeft = left)
        fixed (float* pRight = right)
        fixed (float* pResult = result)
        {
            if (SimdCapabilities.HasAvx512)
            {
                LessThanAvx512(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasAvx2)
            {
                LessThanAvx2(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasNeon)
            {
                LessThanNeon(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasSse42)
            {
                LessThanSse(pLeft, pRight, pResult, length);
            }
            else
            {
                LessThanFallback(pLeft, pRight, pResult, length);
            }
        }
    }

    // AVX512 Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void EqualAvx512(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector512Size);
        var ones = Vector512.Create(1.0f);
        var zeros = Vector512<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector512Size)
        {
            var leftVec = Avx512F.LoadVector512(left + i);
            var rightVec = Avx512F.LoadVector512(right + i);
            var mask = Avx512F.CompareEqual(leftVec, rightVec);
            var resultVec = Avx512F.BlendVariable(zeros, ones, mask);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] == right[i] ? 1.0f : 0.0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void GreaterThanAvx512(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector512Size);
        var ones = Vector512.Create(1.0f);
        var zeros = Vector512<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector512Size)
        {
            var leftVec = Avx512F.LoadVector512(left + i);
            var rightVec = Avx512F.LoadVector512(right + i);
            var mask = Avx512F.CompareGreaterThan(leftVec, rightVec);
            var resultVec = Avx512F.BlendVariable(zeros, ones, mask);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] > right[i] ? 1.0f : 0.0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void LessThanAvx512(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector512Size);
        var ones = Vector512.Create(1.0f);
        var zeros = Vector512<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector512Size)
        {
            var leftVec = Avx512F.LoadVector512(left + i);
            var rightVec = Avx512F.LoadVector512(right + i);
            var mask = Avx512F.CompareLessThan(leftVec, rightVec);
            var resultVec = Avx512F.BlendVariable(zeros, ones, mask);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] < right[i] ? 1.0f : 0.0f;
        }
    }

    // AVX2 Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void EqualAvx2(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector256Size);
        var ones = Vector256.Create(1.0f);
        var zeros = Vector256<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector256Size)
        {
            var leftVec = Avx.LoadVector256(left + i);
            var rightVec = Avx.LoadVector256(right + i);
            var mask = Avx.Compare(leftVec, rightVec, FloatComparisonMode.OrderedEqualNonSignaling);
            var resultVec = Avx.BlendVariable(zeros, ones, mask);
            Avx.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] == right[i] ? 1.0f : 0.0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void GreaterThanAvx2(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector256Size);
        var ones = Vector256.Create(1.0f);
        var zeros = Vector256<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector256Size)
        {
            var leftVec = Avx.LoadVector256(left + i);
            var rightVec = Avx.LoadVector256(right + i);
            var mask = Avx.Compare(leftVec, rightVec, FloatComparisonMode.OrderedGreaterThanNonSignaling);
            var resultVec = Avx.BlendVariable(zeros, ones, mask);
            Avx.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] > right[i] ? 1.0f : 0.0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void LessThanAvx2(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector256Size);
        var ones = Vector256.Create(1.0f);
        var zeros = Vector256<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector256Size)
        {
            var leftVec = Avx.LoadVector256(left + i);
            var rightVec = Avx.LoadVector256(right + i);
            var mask = Avx.Compare(leftVec, rightVec, FloatComparisonMode.OrderedLessThanNonSignaling);
            var resultVec = Avx.BlendVariable(zeros, ones, mask);
            Avx.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] < right[i] ? 1.0f : 0.0f;
        }
    }

    // ARM NEON Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void EqualNeon(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);
        var ones = Vector128.Create(1.0f);
        var zeros = Vector128<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
        {
            var leftVec = AdvSimd.LoadVector128(left + i);
            var rightVec = AdvSimd.LoadVector128(right + i);
            var mask = AdvSimd.CompareEqual(leftVec, rightVec);
            var resultVec = AdvSimd.BitwiseSelect(mask, ones, zeros);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] == right[i] ? 1.0f : 0.0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void GreaterThanNeon(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);
        var ones = Vector128.Create(1.0f);
        var zeros = Vector128<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
        {
            var leftVec = AdvSimd.LoadVector128(left + i);
            var rightVec = AdvSimd.LoadVector128(right + i);
            var mask = AdvSimd.CompareGreaterThan(leftVec, rightVec);
            var resultVec = AdvSimd.BitwiseSelect(mask, ones, zeros);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] > right[i] ? 1.0f : 0.0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void LessThanNeon(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);
        var ones = Vector128.Create(1.0f);
        var zeros = Vector128<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
        {
            var leftVec = AdvSimd.LoadVector128(left + i);
            var rightVec = AdvSimd.LoadVector128(right + i);
            var mask = AdvSimd.CompareLessThan(leftVec, rightVec);
            var resultVec = AdvSimd.BitwiseSelect(mask, ones, zeros);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] < right[i] ? 1.0f : 0.0f;
        }
    }

    // SSE Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void EqualSse(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);
        var ones = Vector128.Create(1.0f);
        var zeros = Vector128<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
        {
            var leftVec = Sse.LoadVector128(left + i);
            var rightVec = Sse.LoadVector128(right + i);
            var mask = Sse.CompareEqual(leftVec, rightVec);
            var resultVec = Sse41.BlendVariable(zeros, ones, mask);
            Sse.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] == right[i] ? 1.0f : 0.0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void GreaterThanSse(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);
        var ones = Vector128.Create(1.0f);
        var zeros = Vector128<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
        {
            var leftVec = Sse.LoadVector128(left + i);
            var rightVec = Sse.LoadVector128(right + i);
            var mask = Sse.CompareGreaterThan(leftVec, rightVec);
            var resultVec = Sse41.BlendVariable(zeros, ones, mask);
            Sse.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] > right[i] ? 1.0f : 0.0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void LessThanSse(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);
        var ones = Vector128.Create(1.0f);
        var zeros = Vector128<float>.Zero;

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
        {
            var leftVec = Sse.LoadVector128(left + i);
            var rightVec = Sse.LoadVector128(right + i);
            var mask = Sse.CompareLessThan(leftVec, rightVec);
            var resultVec = Sse41.BlendVariable(zeros, ones, mask);
            Sse.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] < right[i] ? 1.0f : 0.0f;
        }
    }

    // Fallback Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void EqualFallback(float* left, float* right, float* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = left[i] == right[i] ? 1.0f : 0.0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void GreaterThanFallback(float* left, float* right, float* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = left[i] > right[i] ? 1.0f : 0.0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void LessThanFallback(float* left, float* right, float* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = left[i] < right[i] ? 1.0f : 0.0f;
        }
    }
}