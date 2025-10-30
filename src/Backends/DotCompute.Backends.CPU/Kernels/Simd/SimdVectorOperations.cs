// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Kernels.Simd;

/// <summary>
/// Core SIMD vector operations with instruction set specific implementations.
/// Provides optimized vectorized operations for different hardware architectures.
/// </summary>
public static class SimdVectorOperations
{
    #region Vector Addition Operations

    /// <summary>
    /// Performs vectorized addition using AVX-512 instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void AddAvx512<T>(T* ptr1, T* ptr2, T* ptrOut, long elementCount) where T : unmanaged
    {
        if (!Avx512F.IsSupported)
        {
            AddAvx2(ptr1, ptr2, ptrOut, elementCount);
            return;
        }

        var vectorSize = Vector512<T>.Count;
        var vectorCount = elementCount / vectorSize;
        var remainder = elementCount % vectorSize;

        // Vectorized processing with loop unrolling
        long i = 0;
        for (; i < vectorCount - 3; i += 4) // Unroll by 4
        {
            var offset1 = i * vectorSize;
            var offset2 = (i + 1) * vectorSize;
            var offset3 = (i + 2) * vectorSize;
            var offset4 = (i + 3) * vectorSize;

            // Prefetch next cache lines
            SimdMemoryPrefetcher.Prefetch(ptr1 + offset4 + vectorSize, PrefetchMode.Temporal);
            SimdMemoryPrefetcher.Prefetch(ptr2 + offset4 + vectorSize, PrefetchMode.Temporal);

            // Load and process 4 vectors in parallel
            var v1_1 = Vector512.Load(ptr1 + offset1);
            var v1_2 = Vector512.Load(ptr2 + offset1);
            var v2_1 = Vector512.Load(ptr1 + offset2);
            var v2_2 = Vector512.Load(ptr2 + offset2);
            var v3_1 = Vector512.Load(ptr1 + offset3);
            var v3_2 = Vector512.Load(ptr2 + offset3);
            var v4_1 = Vector512.Load(ptr1 + offset4);
            var v4_2 = Vector512.Load(ptr2 + offset4);

            // Perform operations
            var result1 = Vector512.Add(v1_1, v1_2);
            var result2 = Vector512.Add(v2_1, v2_2);
            var result3 = Vector512.Add(v3_1, v3_2);
            var result4 = Vector512.Add(v4_1, v4_2);

            // Store results
            Vector512.Store(result1, ptrOut + offset1);
            Vector512.Store(result2, ptrOut + offset2);
            Vector512.Store(result3, ptrOut + offset3);
            Vector512.Store(result4, ptrOut + offset4);
        }

        // Handle remaining vectors
        for (; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var v1 = Vector512.Load(ptr1 + offset);
            var v2 = Vector512.Load(ptr2 + offset);
            var result = Vector512.Add(v1, v2);
            Vector512.Store(result, ptrOut + offset);
        }

        // Handle scalar remainder
        var scalarStart = vectorCount * vectorSize;
        SimdScalarOperations.AddRemainder(ptr1 + scalarStart, ptr2 + scalarStart,
                                         ptrOut + scalarStart, remainder);
    }

    /// <summary>
    /// Performs vectorized addition using AVX2 instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void AddAvx2<T>(T* ptr1, T* ptr2, T* ptrOut, long elementCount) where T : unmanaged
    {
        if (!Avx2.IsSupported)
        {
            AddSse(ptr1, ptr2, ptrOut, elementCount);
            return;
        }

        var vectorSize = Vector256<T>.Count;
        var vectorCount = elementCount / vectorSize;
        var remainder = elementCount % vectorSize;

        // Optimized loop with prefetching and unrolling
        long i = 0;
        for (; i < vectorCount - 7; i += 8) // Unroll by 8 for better ILP
        {
            // Prefetch data for upcoming iterations
            var prefetchOffset = (i + 8) * vectorSize;
            if (prefetchOffset < elementCount)
            {
                SimdMemoryPrefetcher.Prefetch(ptr1 + prefetchOffset, PrefetchMode.Temporal);
                SimdMemoryPrefetcher.Prefetch(ptr2 + prefetchOffset, PrefetchMode.Temporal);
            }

            // Process 8 vectors in parallel
            for (var j = 0; j < 8; j++)
            {
                var offset = (i + j) * vectorSize;
                var v1 = Vector256.Load(ptr1 + offset);
                var v2 = Vector256.Load(ptr2 + offset);
                var result = Vector256.Add(v1, v2);
                Vector256.Store(result, ptrOut + offset);
            }
        }

        // Handle remaining vectors
        for (; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var v1 = Vector256.Load(ptr1 + offset);
            var v2 = Vector256.Load(ptr2 + offset);
            var result = Vector256.Add(v1, v2);
            Vector256.Store(result, ptrOut + offset);
        }

        // Handle scalar remainder
        var scalarStart = vectorCount * vectorSize;
        SimdScalarOperations.AddRemainder(ptr1 + scalarStart, ptr2 + scalarStart,
                                         ptrOut + scalarStart, remainder);
    }

    /// <summary>
    /// Performs vectorized addition using SSE instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void AddSse<T>(T* ptr1, T* ptr2, T* ptrOut, long elementCount) where T : unmanaged
    {
        if (!Sse2.IsSupported)
        {
            SimdScalarOperations.AddRemainder(ptr1, ptr2, ptrOut, elementCount);
            return;
        }

        var vectorSize = Vector128<T>.Count;
        var vectorCount = elementCount / vectorSize;
        var remainder = elementCount % vectorSize;

        // SSE processing with optimizations
        for (long i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;

            // Prefetch next iteration data
            if (i + 2 < vectorCount)
            {
                SimdMemoryPrefetcher.Prefetch(ptr1 + (i + 2) * vectorSize, PrefetchMode.Temporal);
                SimdMemoryPrefetcher.Prefetch(ptr2 + (i + 2) * vectorSize, PrefetchMode.Temporal);
            }

            var v1 = Vector128.Load(ptr1 + offset);
            var v2 = Vector128.Load(ptr2 + offset);
            var result = Vector128.Add(v1, v2);
            Vector128.Store(result, ptrOut + offset);
        }

        // Handle scalar remainder
        var scalarStart = vectorCount * vectorSize;
        SimdScalarOperations.AddRemainder(ptr1 + scalarStart, ptr2 + scalarStart,
                                         ptrOut + scalarStart, remainder);
    }

    /// <summary>
    /// Performs vectorized addition using ARM NEON instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void AddNeon<T>(T* ptr1, T* ptr2, T* ptrOut, long elementCount) where T : unmanaged
    {
        if (!AdvSimd.IsSupported)
        {
            SimdScalarOperations.AddRemainder(ptr1, ptr2, ptrOut, elementCount);
            return;
        }

        // Use type-specific NEON processing for optimal performance
        if (typeof(T) == typeof(float))
        {
            AddNeonFloat32((float*)ptr1, (float*)ptr2, (float*)ptrOut, elementCount);
        }
        else if (typeof(T) == typeof(int))
        {
            AddNeonInt32((int*)ptr1, (int*)ptr2, (int*)ptrOut, elementCount);
        }
        else if (typeof(T) == typeof(double))
        {
            AddNeonFloat64((double*)ptr1, (double*)ptr2, (double*)ptrOut, elementCount);
        }
        else
        {
            // Fallback for unsupported types
            SimdScalarOperations.AddRemainder(ptr1, ptr2, ptrOut, elementCount);
        }
    }

    #endregion

    #region Type-Specific NEON Operations

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void AddNeonFloat32(float* ptr1, float* ptr2, float* ptrOut, long elementCount)
    {
        var vectorSize = Vector128<float>.Count;
        var vectorCount = elementCount / vectorSize;
        var remainder = elementCount % vectorSize;

        for (long i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var v1 = AdvSimd.LoadVector128(ptr1 + offset);
            var v2 = AdvSimd.LoadVector128(ptr2 + offset);
            var result = AdvSimd.Add(v1, v2);
            AdvSimd.Store(ptrOut + offset, result);
        }

        // Handle scalar remainder
        var scalarStart = vectorCount * vectorSize;
        for (var i = scalarStart; i < elementCount; i++)
        {
            ptrOut[i] = ptr1[i] + ptr2[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void AddNeonInt32(int* ptr1, int* ptr2, int* ptrOut, long elementCount)
    {
        var vectorSize = Vector128<int>.Count;
        var vectorCount = elementCount / vectorSize;
        var remainder = elementCount % vectorSize;

        for (long i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var v1 = AdvSimd.LoadVector128(ptr1 + offset);
            var v2 = AdvSimd.LoadVector128(ptr2 + offset);
            var result = AdvSimd.Add(v1, v2);
            AdvSimd.Store(ptrOut + offset, result);
        }

        // Handle scalar remainder
        var scalarStart = vectorCount * vectorSize;
        for (var i = scalarStart; i < elementCount; i++)
        {
            ptrOut[i] = ptr1[i] + ptr2[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void AddNeonFloat64(double* ptr1, double* ptr2, double* ptrOut, long elementCount)
    {
        var vectorSize = Vector128<double>.Count;
        var vectorCount = elementCount / vectorSize;
        var remainder = elementCount % vectorSize;

        for (long i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var v1 = AdvSimd.LoadVector128(ptr1 + offset);
            var v2 = AdvSimd.LoadVector128(ptr2 + offset);
            var result = AdvSimd.Arm64.Add(v1, v2);
            AdvSimd.Store(ptrOut + offset, result);
        }

        // Handle scalar remainder
        var scalarStart = vectorCount * vectorSize;
        for (var i = scalarStart; i < elementCount; i++)
        {
            ptrOut[i] = ptr1[i] + ptr2[i];
        }
    }

    #endregion

    #region Reduction Operations

    /// <summary>
    /// Performs vectorized sum reduction operation.
    /// </summary>
    public static unsafe T Sum<T>(ReadOnlySpan<T> input) where T : unmanaged
    {
        if (typeof(T) == typeof(float))
        {
            var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input);
            var result = SumFloat32(floatSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var doubleSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, double>(input);
            var result = SumFloat64(doubleSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(int))
        {
            var intSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, int>(input);
            var result = SumInt32(intSpan);
            return *(T*)&result;
        }

        return SimdScalarOperations.Sum(input);
    }

    /// <summary>
    /// Performs vectorized min reduction operation.
    /// </summary>
    public static unsafe T Min<T>(ReadOnlySpan<T> input) where T : unmanaged
    {
        if (input.IsEmpty)
        {
            return default;
        }

        if (typeof(T) == typeof(float))
        {
            var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input);
            var result = MinFloat32(floatSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var doubleSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, double>(input);
            var result = MinFloat64(doubleSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(int))
        {
            var intSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, int>(input);
            var result = MinInt32(intSpan);
            return *(T*)&result;
        }

        return SimdScalarOperations.Min(input);
    }

    /// <summary>
    /// Performs vectorized max reduction operation.
    /// </summary>
    public static unsafe T Max<T>(ReadOnlySpan<T> input) where T : unmanaged
    {
        if (input.IsEmpty)
        {
            return default;
        }

        if (typeof(T) == typeof(float))
        {
            var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input);
            var result = MaxFloat32(floatSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var doubleSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, double>(input);
            var result = MaxFloat64(doubleSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(int))
        {
            var intSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, int>(input);
            var result = MaxInt32(intSpan);
            return *(T*)&result;
        }

        return SimdScalarOperations.Max(input);
    }

    #endregion

    #region Private Reduction Implementations

    private static unsafe float SumFloat32(ReadOnlySpan<float> input)
    {
        fixed (float* ptr = input)
        {
            if (Avx512F.IsSupported)
            {
                return SumFloat32Avx512(ptr, input.Length);
            }
            if (Avx2.IsSupported)
            {
                return SumFloat32Avx2(ptr, input.Length);
            }
            if (Sse2.IsSupported)
            {
                return SumFloat32Sse(ptr, input.Length);
            }

            return SimdScalarOperations.SumFloat32(ptr, input.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe float SumFloat32Avx512(float* data, int count)
    {
        const int vectorSize = 16;
        var vectorCount = count / vectorSize;
        var remainder = count % vectorSize;

        var accumulator = Vector512<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var vector = Avx512F.LoadVector512(data + i * vectorSize);
            accumulator = Avx512F.Add(accumulator, vector);
        }

        // Horizontal sum of accumulator
        var sum = SimdHorizontalOperations.HorizontalSum(accumulator);

        // Add remainder
        for (var i = vectorCount * vectorSize; i < count; i++)
        {
            sum += data[i];
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe float SumFloat32Avx2(float* data, int count)
    {
        const int vectorSize = 8;
        var vectorCount = count / vectorSize;
        var remainder = count % vectorSize;

        var accumulator = Vector256<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var vector = Avx.LoadVector256(data + i * vectorSize);
            accumulator = Avx.Add(accumulator, vector);
        }

        var sum = SimdHorizontalOperations.HorizontalSum(accumulator);

        for (var i = vectorCount * vectorSize; i < count; i++)
        {
            sum += data[i];
        }

        return sum;
    }

    private static unsafe float SumFloat32Sse(float* data, int count)
    {
        const int vectorSize = 4;
        var vectorCount = count / vectorSize;
        var accumulator = Vector128<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var vector = Sse.LoadVector128(data + i * vectorSize);
            accumulator = Sse.Add(accumulator, vector);
        }

        // Horizontal sum
        var temp = Sse.Shuffle(accumulator, accumulator, 0x4E);
        accumulator = Sse.Add(accumulator, temp);
        temp = Sse.Shuffle(accumulator, accumulator, 0xB1);
        accumulator = Sse.Add(accumulator, temp);
        var sum = accumulator.ToScalar();

        // Add remainder
        for (var i = vectorCount * vectorSize; i < count; i++)
        {
            sum += data[i];
        }

        return sum;
    }

    // Additional reduction implementations for double and int types
    private static unsafe double SumFloat64(ReadOnlySpan<double> input)
    {
        if (input.IsEmpty)
        {
            return 0.0;
        }

        fixed (double* ptr = input)
        {
            return SimdScalarOperations.SumFloat64(ptr, input.Length);
        }
    }

    private static unsafe int SumInt32(ReadOnlySpan<int> input)
    {
        if (input.IsEmpty)
        {
            return 0;
        }

        fixed (int* ptr = input)
        {
            return SimdScalarOperations.SumInt32(ptr, input.Length);
        }
    }

    private static unsafe float MinFloat32(ReadOnlySpan<float> input)
    {
        fixed (float* ptr = input)
        {
            if (Avx512F.IsSupported)
            {
                return MinFloat32Avx512(ptr, input.Length);
            }
            return SimdScalarOperations.MinFloat32(ptr, input.Length);
        }
    }

    private static unsafe float MaxFloat32(ReadOnlySpan<float> input)
    {
        fixed (float* ptr = input)
        {
            if (Avx512F.IsSupported)
            {
                return MaxFloat32Avx512(ptr, input.Length);
            }
            return SimdScalarOperations.MaxFloat32(ptr, input.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe float MinFloat32Avx512(float* data, int count)
    {
        const int vectorSize = 16;
        var vectorCount = count / vectorSize;

        if (vectorCount == 0)
        {
            return SimdScalarOperations.MinFloat32(data, count);
        }

        var minVector = Avx512F.LoadVector512(data);

        for (var i = 1; i < vectorCount; i++)
        {
            var vector = Avx512F.LoadVector512(data + i * vectorSize);
            minVector = Avx512F.Min(minVector, vector);
        }

        // Horizontal min of the vector
        var min = SimdHorizontalOperations.HorizontalMin(minVector);

        // Handle remainder
        for (var i = vectorCount * vectorSize; i < count; i++)
        {
            if (data[i] < min)
            {
                min = data[i];
            }
        }

        return min;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe float MaxFloat32Avx512(float* data, int count)
    {
        const int vectorSize = 16;
        var vectorCount = count / vectorSize;

        if (vectorCount == 0)
        {
            return SimdScalarOperations.MaxFloat32(data, count);
        }

        var maxVector = Avx512F.LoadVector512(data);

        for (var i = 1; i < vectorCount; i++)
        {
            var vector = Avx512F.LoadVector512(data + i * vectorSize);
            maxVector = Avx512F.Max(maxVector, vector);
        }

        var max = SimdHorizontalOperations.HorizontalMax(maxVector);

        for (var i = vectorCount * vectorSize; i < count; i++)
        {
            if (data[i] > max)
            {
                max = data[i];
            }
        }

        return max;
    }

    // Placeholder implementations for type completeness
    private static unsafe double MinFloat64(ReadOnlySpan<double> input) => input.IsEmpty ? 0.0 : input[0];
    private static unsafe double MaxFloat64(ReadOnlySpan<double> input) => input.IsEmpty ? 0.0 : input[0];
    private static unsafe int MinInt32(ReadOnlySpan<int> input) => input.IsEmpty ? 0 : input[0];
    private static unsafe int MaxInt32(ReadOnlySpan<int> input) => input.IsEmpty ? 0 : input[0];

    #endregion
}
