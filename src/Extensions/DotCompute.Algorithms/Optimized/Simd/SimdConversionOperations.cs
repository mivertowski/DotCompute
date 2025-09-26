// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace DotCompute.Algorithms.Optimized.Simd;

/// <summary>
/// SIMD-accelerated type conversion operations with cross-platform optimization.
/// Provides vectorized type conversions between different numeric types.
/// </summary>
public static class SimdConversionOperations
{
    /// <summary>
    /// Vectorized conversion from int32 to float32.
    /// </summary>
    /// <param name="input">Input int32 span</param>
    /// <param name="result">Result float32 span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void ConvertInt32ToFloat32(ReadOnlySpan<int> input, Span<float> result)
    {
        if (input.Length != result.Length)
        {
            throw new ArgumentException("Spans must have equal length");
        }

        var length = input.Length;
        
        fixed (int* pInput = input)
        fixed (float* pResult = result)
        {
            if (SimdCapabilities.HasAvx512)
            {
                ConvertInt32ToFloat32Avx512(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasAvx2)
            {
                ConvertInt32ToFloat32Avx2(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasNeon)
            {
                ConvertInt32ToFloat32Neon(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasSse42)
            {
                ConvertInt32ToFloat32Sse(pInput, pResult, length);
            }
            else
            {
                ConvertInt32ToFloat32Fallback(pInput, pResult, length);
            }
        }
    }

    /// <summary>
    /// Vectorized conversion from float32 to int32 with truncation.
    /// </summary>
    /// <param name="input">Input float32 span</param>
    /// <param name="result">Result int32 span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void ConvertFloat32ToInt32(ReadOnlySpan<float> input, Span<int> result)
    {
        if (input.Length != result.Length)
        {
            throw new ArgumentException("Spans must have equal length");
        }

        var length = input.Length;
        
        fixed (float* pInput = input)
        fixed (int* pResult = result)
        {
            if (SimdCapabilities.HasAvx512)
            {
                ConvertFloat32ToInt32Avx512(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasAvx2)
            {
                ConvertFloat32ToInt32Avx2(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasNeon)
            {
                ConvertFloat32ToInt32Neon(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasSse42)
            {
                ConvertFloat32ToInt32Sse(pInput, pResult, length);
            }
            else
            {
                ConvertFloat32ToInt32Fallback(pInput, pResult, length);
            }
        }
    }

    /// <summary>
    /// Vectorized conversion from double to float32 with precision loss.
    /// </summary>
    /// <param name="input">Input double span</param>
    /// <param name="result">Result float32 span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void ConvertDoubleToFloat32(ReadOnlySpan<double> input, Span<float> result)
    {
        if (input.Length != result.Length)
        {
            throw new ArgumentException("Spans must have equal length");
        }

        var length = input.Length;
        
        fixed (double* pInput = input)
        fixed (float* pResult = result)
        {
            if (SimdCapabilities.HasAvx512)
            {
                ConvertDoubleToFloat32Avx512(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasAvx2)
            {
                ConvertDoubleToFloat32Avx2(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasNeon)
            {
                ConvertDoubleToFloat32Neon(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasSse42)
            {
                ConvertDoubleToFloat32Sse(pInput, pResult, length);
            }
            else
            {
                ConvertDoubleToFloat32Fallback(pInput, pResult, length);
            }
        }
    }

    /// <summary>
    /// Vectorized conversion from float32 to double with precision extension.
    /// </summary>
    /// <param name="input">Input float32 span</param>
    /// <param name="result">Result double span</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void ConvertFloat32ToDouble(ReadOnlySpan<float> input, Span<double> result)
    {
        if (input.Length != result.Length)
        {
            throw new ArgumentException("Spans must have equal length");
        }

        var length = input.Length;
        
        fixed (float* pInput = input)
        fixed (double* pResult = result)
        {
            if (SimdCapabilities.HasAvx512)
            {
                ConvertFloat32ToDoubleAvx512(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasAvx2)
            {
                ConvertFloat32ToDoubleAvx2(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasNeon)
            {
                ConvertFloat32ToDoubleNeon(pInput, pResult, length);
            }
            else if (SimdCapabilities.HasSse42)
            {
                ConvertFloat32ToDoubleSse(pInput, pResult, length);
            }
            else
            {
                ConvertFloat32ToDoubleFallback(pInput, pResult, length);
            }
        }
    }

    // AVX512 Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertInt32ToFloat32Avx512(int* input, float* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector512Size / sizeof(int);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var intVec = Avx512F.LoadVector512(input + i);
            var floatVec = Avx512F.ConvertToVector512Single(intVec);
            Avx512F.Store(result + i, floatVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertFloat32ToInt32Avx512(float* input, int* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector512Size / sizeof(float);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var floatVec = Avx512F.LoadVector512(input + i);
            var intVec = Avx512F.ConvertToVector512Int32(floatVec);
            Avx512F.Store(result + i, intVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = (int)input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertDoubleToFloat32Avx512(double* input, float* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector512Size / sizeof(double);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var doubleVec = Avx512F.LoadVector512(input + i);
            var floatVec = Avx512F.ConvertToVector256Single(doubleVec);
            Avx.Store(result + i, floatVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = (float)input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertFloat32ToDoubleAvx512(float* input, double* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector256Size / sizeof(float); // Process 8 floats -> 8 doubles
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var floatVec = Avx.LoadVector256(input + i);
            var doubleVec = Avx512F.ConvertToVector512Double(floatVec);
            Avx512F.Store(result + i, doubleVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = input[i];
        }
    }

    // AVX2 Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertInt32ToFloat32Avx2(int* input, float* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector256Size / sizeof(int);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var intVec = Avx2.LoadVector256(input + i);
            var floatVec = Avx2.ConvertToVector256Single(intVec);
            Avx.Store(result + i, floatVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertFloat32ToInt32Avx2(float* input, int* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector256Size / sizeof(float);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var floatVec = Avx.LoadVector256(input + i);
            var intVec = Avx.ConvertToVector256Int32(floatVec);
            Avx.Store(result + i, intVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = (int)input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertDoubleToFloat32Avx2(double* input, float* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector256Size / sizeof(double);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var doubleVec = Avx.LoadVector256(input + i);
            var floatVec = Avx.ConvertToVector128Single(doubleVec);
            Sse.Store(result + i, floatVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = (float)input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertFloat32ToDoubleAvx2(float* input, double* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(float); // Process 4 floats -> 4 doubles
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var floatVec = Sse.LoadVector128(input + i);
            var doubleVec = Avx.ConvertToVector256Double(floatVec);
            Avx.Store(result + i, doubleVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = input[i];
        }
    }

    // ARM NEON Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertInt32ToFloat32Neon(int* input, float* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(int);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var intVec = AdvSimd.LoadVector128(input + i);
            var floatVec = AdvSimd.ConvertToSingle(intVec);
            AdvSimd.Store(result + i, floatVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertFloat32ToInt32Neon(float* input, int* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(float);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var floatVec = AdvSimd.LoadVector128(input + i);
            var intVec = AdvSimd.ConvertToInt32RoundToZero(floatVec);
            AdvSimd.Store(result + i, intVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = (int)input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertDoubleToFloat32Neon(double* input, float* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(double);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var doubleVec = AdvSimd.LoadVector128(input + i);
            var floatVec = AdvSimd.ConvertToSingle(doubleVec);
            // Note: This produces a 64-bit vector with 2 floats, need to handle appropriately
            result[i] = floatVec.ToScalar();
            if (i + 1 < length)
                result[i + 1] = floatVec.AsDouble().GetElement(1); // Get second element
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = (float)input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertFloat32ToDoubleNeon(float* input, double* result, int length)
    {
        var i = 0;
        // Process 2 floats at a time to produce 2 doubles
        var vectorCount = length - (length % 2);

        for (; i < vectorCount; i += 2)
        {
            var floatVec = Vector64.Create(input[i], input[i + 1]);
            var doubleVec = AdvSimd.ConvertToDouble(floatVec);
            AdvSimd.Store(result + i, doubleVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = input[i];
        }
    }

    // SSE Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertInt32ToFloat32Sse(int* input, float* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(int);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var intVec = Sse2.LoadVector128(input + i);
            var floatVec = Sse2.ConvertToVector128Single(intVec);
            Sse.Store(result + i, floatVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertFloat32ToInt32Sse(float* input, int* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(float);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var floatVec = Sse.LoadVector128(input + i);
            var intVec = Sse2.ConvertToVector128Int32(floatVec);
            Sse2.Store(result + i, intVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = (int)input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertDoubleToFloat32Sse(double* input, float* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(double);
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var doubleVec = Sse2.LoadVector128(input + i);
            var floatVec = Sse2.ConvertToVector128Single(doubleVec);
            // ConvertToVector128Single from double produces only 2 floats in lower half
            Sse.StoreLow(result + i, floatVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = (float)input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertFloat32ToDoubleSse(float* input, double* result, int length)
    {
        var i = 0;
        var vectorSize = SimdCapabilities.Vector128Size / sizeof(double); // Process 2 floats -> 2 doubles
        var vectorCount = length - (length % vectorSize);

        for (; i < vectorCount; i += vectorSize)
        {
            var floatVec = Sse.LoadLow(Sse.StaticCast<double, float>(Sse2.LoadVector128(result + i)), input + i); // Load 2 floats
            var doubleVec = Sse2.ConvertToVector128Double(floatVec);
            Sse2.Store(result + i, doubleVec);
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            result[i] = input[i];
        }
    }

    // Fallback Implementations
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertInt32ToFloat32Fallback(int* input, float* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertFloat32ToInt32Fallback(float* input, int* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = (int)input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertDoubleToFloat32Fallback(double* input, float* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = (float)input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ConvertFloat32ToDoubleFallback(float* input, double* result, int length)
    {
        for (var i = 0; i < length; i++)
        {
            result[i] = input[i];
        }
    }
}