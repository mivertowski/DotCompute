// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace DotCompute.Algorithms.Optimized.Simd;

/// <summary>
/// SIMD-accelerated bitwise operations with cross-platform optimization.
/// Provides vectorized bitwise operations for all supported architectures.
/// </summary>
public static class SimdBitwiseOperations
{
    /// <summary>
    /// Vectorized bitwise AND operation.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void And(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> result)
    {
        if (left.Length != right.Length || left.Length != result.Length)
        {
            throw new ArgumentException("Spans must have equal length");
        }

        var length = left.Length;
        
        fixed (int* pLeft = left)
        fixed (int* pRight = right) 
        fixed (int* pResult = result)
        {
            if (SimdCapabilities.HasAvx512)
            {
                AndAvx512(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasAvx2)
            {
                AndAvx2(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasNeon)
            {
                AndNeon(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasSse42)
            {
                AndSse(pLeft, pRight, pResult, length);
            }
            else
            {
                AndFallback(pLeft, pRight, pResult, length);
            }
        }
    }

    /// <summary>
    /// Vectorized bitwise OR operation.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Or(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> result)
    {
        if (left.Length != right.Length || left.Length != result.Length)
        {
            throw new ArgumentException("Spans must have equal length");
        }

        var length = left.Length;
        
        fixed (int* pLeft = left)
        fixed (int* pRight = right) 
        fixed (int* pResult = result)
        {
            if (SimdCapabilities.HasAvx512)
            {
                OrAvx512(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasAvx2)
            {
                OrAvx2(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasNeon)
            {
                OrNeon(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasSse42)
            {
                OrSse(pLeft, pRight, pResult, length);
            }
            else
            {
                OrFallback(pLeft, pRight, pResult, length);
            }
        }
    }

    /// <summary>
    /// Vectorized bitwise XOR operation.
    /// </summary>
    /// <param name="left">Left operand span</param>
    /// <param name="right">Right operand span</param>
    /// <param name="result">Result span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Xor(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> result)
    {
        if (left.Length != right.Length || left.Length != result.Length)
        {
            throw new ArgumentException("Spans must have equal length");
        }

        var length = left.Length;
        
        fixed (int* pLeft = left)
        fixed (int* pRight = right) 
        fixed (int* pResult = result)
        {
            if (SimdCapabilities.HasAvx512)
            {
                XorAvx512(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasAvx2)
            {
                XorAvx2(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasNeon)
            {
                XorNeon(pLeft, pRight, pResult, length);
            }
            else if (SimdCapabilities.HasSse42)
            {
                XorSse(pLeft, pRight, pResult, length);
            }
            else
            {
                XorFallback(pLeft, pRight, pResult, length);
            }
        }
    }

    /// <summary>
    /// Vectorized bitwise NOT operation.
    /// </summary>
    /// <param name="input">Input span</param>
    /// <param name="result">Result span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Not(ReadOnlySpan<int> input, Span<int> result)
    {
        if (input.Length != result.Length)
        {
            throw new ArgumentException("Spans must have equal length");
        }

        var length = input.Length;
        
        fixed (int* pInput = input)
        fixed (int* pResult = result)
        {
            if (SimdCapabilities.HasAvx512)
            {
                NotAvx512(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasAvx2)
            {
                NotAvx2(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasNeon)
            {
                NotNeon(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasSse42)
            {
                NotSse(pInput, pResult, length);
            }
            else
            {
                NotFallback(pInput, pResult, length);
            }
        }
    }

    // AVX512 Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AndAvx512(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector512Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector512Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = Avx512F.LoadVector512(left + i);
            var rightVec = Avx512F.LoadVector512(right + i);
            var resultVec = Avx512F.And(leftVec, rightVec);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] & right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void OrAvx512(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector512Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector512Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = Avx512F.LoadVector512(left + i);
            var rightVec = Avx512F.LoadVector512(right + i);
            var resultVec = Avx512F.Or(leftVec, rightVec);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] | right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void XorAvx512(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector512Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector512Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = Avx512F.LoadVector512(left + i);
            var rightVec = Avx512F.LoadVector512(right + i);
            var resultVec = Avx512F.Xor(leftVec, rightVec);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] ^ right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void NotAvx512(int* input, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector512Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector512Size / sizeof(int);
        var allOnes = Vector512.Create(-1); // All bits set to 1

        for (; i < vectorCount; i += vectorSize)
        {
            var inputVec = Avx512F.LoadVector512(input + i);
            var resultVec = Avx512F.Xor(inputVec, allOnes);
            Avx512F.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = ~input[i];
        }
    }

    // AVX2 Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AndAvx2(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector256Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector256Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = Avx2.LoadVector256(left + i);
            var rightVec = Avx2.LoadVector256(right + i);
            var resultVec = Avx2.And(leftVec, rightVec);
            Avx2.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] & right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void OrAvx2(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector256Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector256Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = Avx2.LoadVector256(left + i);
            var rightVec = Avx2.LoadVector256(right + i);
            var resultVec = Avx2.Or(leftVec, rightVec);
            Avx2.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] | right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void XorAvx2(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector256Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector256Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = Avx2.LoadVector256(left + i);
            var rightVec = Avx2.LoadVector256(right + i);
            var resultVec = Avx2.Xor(leftVec, rightVec);
            Avx2.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] ^ right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void NotAvx2(int* input, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector256Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector256Size / sizeof(int);
        var allOnes = Vector256.Create(-1); // All bits set to 1

        for (; i < vectorCount; i += vectorSize)
        {
            var inputVec = Avx2.LoadVector256(input + i);
            var resultVec = Avx2.Xor(inputVec, allOnes);
            Avx2.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = ~input[i];
        }
    }

    // ARM NEON Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AndNeon(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector128Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = AdvSimd.LoadVector128(left + i);
            var rightVec = AdvSimd.LoadVector128(right + i);
            var resultVec = AdvSimd.And(leftVec, rightVec);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] & right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void OrNeon(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector128Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = AdvSimd.LoadVector128(left + i);
            var rightVec = AdvSimd.LoadVector128(right + i);
            var resultVec = AdvSimd.Or(leftVec, rightVec);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] | right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void XorNeon(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector128Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = AdvSimd.LoadVector128(left + i);
            var rightVec = AdvSimd.LoadVector128(right + i);
            var resultVec = AdvSimd.Xor(leftVec, rightVec);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] ^ right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void NotNeon(int* input, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector128Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(int);
        var allOnes = Vector128.Create(-1); // All bits set to 1

        for (; i < vectorCount; i += vectorSize)
        {
            var inputVec = AdvSimd.LoadVector128(input + i);
            var resultVec = AdvSimd.Not(inputVec);
            AdvSimd.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = ~input[i];
        }
    }

    // SSE Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AndSse(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector128Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = Sse2.LoadVector128(left + i);
            var rightVec = Sse2.LoadVector128(right + i);
            var resultVec = Sse2.And(leftVec, rightVec);
            Sse2.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] & right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void OrSse(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector128Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = Sse2.LoadVector128(left + i);
            var rightVec = Sse2.LoadVector128(right + i);
            var resultVec = Sse2.Or(leftVec, rightVec);
            Sse2.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] | right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void XorSse(int* left, int* right, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector128Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(int);

        for (; i < vectorCount; i += vectorSize)
        {
            var leftVec = Sse2.LoadVector128(left + i);
            var rightVec = Sse2.LoadVector128(right + i);
            var resultVec = Sse2.Xor(leftVec, rightVec);
            Sse2.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = left[i] ^ right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void NotSse(int* input, int* result, int length)
    {
        var i = 0;
        var vectorCount = length - (length % (SimdCapabilities.Vector128Size / sizeof(int)));
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(int);
        var allOnes = Vector128.Create(-1); // All bits set to 1

        for (; i < vectorCount; i += vectorSize)
        {
            var inputVec = Sse2.LoadVector128(input + i);
            var resultVec = Sse2.Xor(inputVec, allOnes);
            Sse2.Store(result + i, resultVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = ~input[i];
        }
    }

    // Fallback Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void AndFallback(int* left, int* right, int* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = left[i] & right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void OrFallback(int* left, int* right, int* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = left[i] | right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void XorFallback(int* left, int* right, int* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = left[i] ^ right[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void NotFallback(int* input, int* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = ~input[i];
        }
    }
}