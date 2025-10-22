
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace DotCompute.Algorithms.Optimized.Simd;

/// <summary>
/// SIMD mathematical operations (Add, Multiply, FMA) with cross-platform optimization.
/// </summary>
internal static class SimdMathOperations
{
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
            if (SimdCapabilities.HasAvx512 && length >= SimdCapabilities.Vector512Size)
            {
                AddAvx512(leftPtr, rightPtr, resultPtr, length);
            }
            else if (SimdCapabilities.HasAvx2 && length >= SimdCapabilities.Vector256Size)
            {
                AddAvx2(leftPtr, rightPtr, resultPtr, length);
            }
            else if (SimdCapabilities.HasNeon && length >= SimdCapabilities.Vector128Size)
            {
                AddNeon(leftPtr, rightPtr, resultPtr, length);
            }
            else if (Sse.IsSupported && length >= SimdCapabilities.Vector128Size)
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
            if (SimdCapabilities.HasAvx512 && length >= SimdCapabilities.Vector512Size)
            {
                MultiplyAvx512(leftPtr, rightPtr, resultPtr, length);
            }
            else if (SimdCapabilities.HasAvx2 && length >= SimdCapabilities.Vector256Size)
            {
                MultiplyAvx2(leftPtr, rightPtr, resultPtr, length);
            }
            else if (SimdCapabilities.HasNeon && length >= SimdCapabilities.Vector128Size)
            {
                MultiplyNeon(leftPtr, rightPtr, resultPtr, length);
            }
            else if (Sse.IsSupported && length >= SimdCapabilities.Vector128Size)
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
            if (SimdCapabilities.HasAvx512 && SimdCapabilities.HasFma && length >= SimdCapabilities.Vector512Size)
            {
                FmaAvx512(aPtr, bPtr, cPtr, resultPtr, length);
            }
            else if (SimdCapabilities.HasAvx2 && SimdCapabilities.HasFma && length >= SimdCapabilities.Vector256Size)
            {
                FmaAvx2(aPtr, bPtr, cPtr, resultPtr, length);
            }
            else if (SimdCapabilities.HasNeon && length >= SimdCapabilities.Vector128Size)
            {
                FmaNeon(aPtr, bPtr, cPtr, resultPtr, length);
            }
            else if (Sse.IsSupported && length >= SimdCapabilities.Vector128Size)
            {
                FmaSse(aPtr, bPtr, cPtr, resultPtr, length);
            }
            else
            {
                FmaFallback(aPtr, bPtr, cPtr, resultPtr, length);
            }
        }
    }

    #region AVX-512 Implementations

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AddAvx512(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector512Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector512Size)
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
        var vectorCount = length - (length % SimdCapabilities.Vector512Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector512Size)
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
        var vectorCount = length - (length % SimdCapabilities.Vector512Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector512Size)
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

    #endregion

    #region AVX2 Implementations

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AddAvx2(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector256Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector256Size)
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
        var vectorCount = length - (length % SimdCapabilities.Vector256Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector256Size)
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
        var vectorCount = length - (length % SimdCapabilities.Vector256Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector256Size)
        {
            var aVec = Avx.LoadVector256(a + i);
            var bVec = Avx.LoadVector256(b + i);
            var cVec = Avx.LoadVector256(c + i);
            Vector256<float> resultVec;

            if (SimdCapabilities.HasFma)
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

    #endregion

    #region ARM NEON Implementations

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AddNeon(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
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
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
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
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
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

    #endregion

    #region SSE Implementations

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AddSse(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
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
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
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
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
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
    private static unsafe void SubtractFallback(float* left, float* right, float* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = left[i] - right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void DivideFallback(float* left, float* right, float* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = left[i] / right[i];
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

    #endregion

    /// <summary>
    /// Cross-platform SIMD vector subtraction with optimal instruction selection.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Subtract(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
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
            if (SimdCapabilities.HasAvx512 && length >= SimdCapabilities.Vector512Size)
            {
                SubtractAvx512(leftPtr, rightPtr, resultPtr, length);
            }
            else if (SimdCapabilities.HasAvx2 && length >= SimdCapabilities.Vector256Size)
            {
                SubtractAvx2(leftPtr, rightPtr, resultPtr, length);
            }
            else if (SimdCapabilities.HasNeon && length >= SimdCapabilities.Vector128Size)
            {
                SubtractNeon(leftPtr, rightPtr, resultPtr, length);
            }
            else if (Sse.IsSupported && length >= SimdCapabilities.Vector128Size)
            {
                SubtractSse(leftPtr, rightPtr, resultPtr, length);
            }
            else
            {
                SubtractFallback(leftPtr, rightPtr, resultPtr, length);
            }
        }
    }

    /// <summary>
    /// Cross-platform SIMD vector division with optimal instruction selection.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Divide(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> result)
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
            if (SimdCapabilities.HasAvx512 && length >= SimdCapabilities.Vector512Size)
            {
                DivideAvx512(leftPtr, rightPtr, resultPtr, length);
            }
            else if (SimdCapabilities.HasAvx2 && length >= SimdCapabilities.Vector256Size)
            {
                DivideAvx2(leftPtr, rightPtr, resultPtr, length);
            }
            else if (SimdCapabilities.HasNeon && length >= SimdCapabilities.Vector128Size)
            {
                DivideNeon(leftPtr, rightPtr, resultPtr, length);
            }
            else if (Sse.IsSupported && length >= SimdCapabilities.Vector128Size)
            {
                DivideSse(leftPtr, rightPtr, resultPtr, length);
            }
            else
            {
                DivideFallback(leftPtr, rightPtr, resultPtr, length);
            }
        }
    }

    #region Additional Implementation Methods

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void SubtractAvx512(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector512Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector512Size)
        {
            var leftVec = Avx512F.LoadVector512(left + i);
            var rightVec = Avx512F.LoadVector512(right + i);
            var resultVec = Avx512F.Subtract(leftVec, rightVec);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] - right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void DivideAvx512(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector512Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector512Size)
        {
            var leftVec = Avx512F.LoadVector512(left + i);
            var rightVec = Avx512F.LoadVector512(right + i);
            var resultVec = Avx512F.Divide(leftVec, rightVec);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] / right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void SubtractAvx2(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector256Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector256Size)
        {
            var leftVec = Avx.LoadVector256(left + i);
            var rightVec = Avx.LoadVector256(right + i);
            var resultVec = Avx.Subtract(leftVec, rightVec);
            Avx.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] - right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void DivideAvx2(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector256Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector256Size)
        {
            var leftVec = Avx.LoadVector256(left + i);
            var rightVec = Avx.LoadVector256(right + i);
            var resultVec = Avx.Divide(leftVec, rightVec);
            Avx.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] / right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void SubtractNeon(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
        {
            var leftVec = AdvSimd.LoadVector128(left + i);
            var rightVec = AdvSimd.LoadVector128(right + i);
            var resultVec = AdvSimd.Subtract(leftVec, rightVec);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] - right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void DivideNeon(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
        {
            var leftVec = AdvSimd.LoadVector128(left + i);
            var rightVec = AdvSimd.LoadVector128(right + i);
            var resultVec = AdvSimd.Divide(leftVec, rightVec);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] / right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void SubtractSse(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
        {
            var leftVec = Sse.LoadVector128(left + i);
            var rightVec = Sse.LoadVector128(right + i);
            var resultVec = Sse.Subtract(leftVec, rightVec);
            Sse.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] - right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void DivideSse(float* left, float* right, float* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % SimdCapabilities.Vector128Size);

        for (; i < vectorCount; i += SimdCapabilities.Vector128Size)
        {
            var leftVec = Sse.LoadVector128(left + i);
            var rightVec = Sse.LoadVector128(right + i);
            var resultVec = Sse.Divide(leftVec, rightVec);
            Sse.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] / right[i];
        }
    }

    #endregion
}