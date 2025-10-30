// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;

namespace DotCompute.Backends.CPU.Kernels.Simd;

/// <summary>
/// Scalar operations for fallback and remainder processing in SIMD execution.
/// Provides optimized scalar implementations when vectorization is not available or beneficial.
/// </summary>
public static class SimdScalarOperations
{
    /// <summary>
    /// Optimized scalar addition with loop unrolling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void AddRemainder<T>(T* ptr1, T* ptr2, T* ptrOut, long count) where T : unmanaged
    {
        // Optimized scalar loop with unrolling
        long i = 0;
        for (; i < count - 3; i += 4)
        {
            ptrOut[i] = AddGeneric(ptr1[i], ptr2[i]);
            ptrOut[i + 1] = AddGeneric(ptr1[i + 1], ptr2[i + 1]);
            ptrOut[i + 2] = AddGeneric(ptr1[i + 2], ptr2[i + 2]);
            ptrOut[i + 3] = AddGeneric(ptr1[i + 3], ptr2[i + 3]);
        }

        for (; i < count; i++)
        {
            ptrOut[i] = AddGeneric(ptr1[i], ptr2[i]);
        }
    }

    /// <summary>
    /// Generic type-safe addition operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T AddGeneric<T>(T a, T b) where T : unmanaged
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
        if (typeof(T) == typeof(int))
        {
            var ia = *(int*)&a;
            var ib = *(int*)&b;
            var result = ia + ib;
            return *(T*)&result;
        }
        if (typeof(T) == typeof(long))
        {
            var la = *(long*)&a;
            var lb = *(long*)&b;
            var result = la + lb;
            return *(T*)&result;
        }

        throw new NotSupportedException($"Type {typeof(T)} not supported for addition");
    }

    /// <summary>
    /// Scalar sum reduction with fallback to dynamic operations.
    /// </summary>
    public static T Sum<T>(ReadOnlySpan<T> input) where T : unmanaged
    {
        dynamic sum = default(T);
        foreach (var item in input)
        {
            sum += (dynamic)item;
        }
        return (T)sum;
    }

    /// <summary>
    /// Scalar min reduction with fallback to dynamic operations.
    /// </summary>
    public static T Min<T>(ReadOnlySpan<T> input) where T : unmanaged
    {
        if (input.IsEmpty)
        {
            return default;
        }

        dynamic min = input[0];
        foreach (var item in input.Slice(1))
        {
            var dynamicItem = (dynamic)item;
            if (dynamicItem < min)
            {
                min = dynamicItem;
            }
        }
        return (T)min;
    }

    /// <summary>
    /// Scalar max reduction with fallback to dynamic operations.
    /// </summary>
    public static T Max<T>(ReadOnlySpan<T> input) where T : unmanaged
    {
        if (input.IsEmpty)
        {
            return default;
        }

        dynamic max = input[0];
        foreach (var item in input.Slice(1))
        {
            var dynamicItem = (dynamic)item;
            if (dynamicItem > max)
            {
                max = dynamicItem;
            }
        }
        return (T)max;
    }

    /// <summary>
    /// Scalar product reduction with fallback to dynamic operations.
    /// </summary>
    public static T Product<T>(ReadOnlySpan<T> input) where T : unmanaged
    {
        if (input.IsEmpty)
        {
            return default;
        }

        dynamic product = (dynamic)input[0];
        foreach (var item in input.Slice(1))
        {
            product *= (dynamic)item;
        }
        return (T)product;
    }
    /// <summary>
    /// Gets sum float32.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="count">The count.</param>
    /// <returns>The result of the operation.</returns>

    // Type-specific optimized implementations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe float SumFloat32(float* data, int count)
    {
        float sum = 0;
        for (var i = 0; i < count; i++)
        {
            sum += data[i];
        }
        return sum;
    }
    /// <summary>
    /// Gets sum float64.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="count">The count.</param>
    /// <returns>The result of the operation.</returns>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe double SumFloat64(double* data, int count)
    {
        double sum = 0;
        for (var i = 0; i < count; i++)
        {
            sum += data[i];
        }
        return sum;
    }
    /// <summary>
    /// Gets sum int32.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="count">The count.</param>
    /// <returns>The result of the operation.</returns>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int SumInt32(int* data, int count)
    {
        var sum = 0;
        for (var i = 0; i < count; i++)
        {
            sum += data[i];
        }
        return sum;
    }
    /// <summary>
    /// Gets min float32.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="count">The count.</param>
    /// <returns>The result of the operation.</returns>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe float MinFloat32(float* data, int count)
    {
        var min = data[0];
        for (var i = 1; i < count; i++)
        {
            if (data[i] < min)
            {
                min = data[i];
            }
        }
        return min;
    }
    /// <summary>
    /// Gets max float32.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="count">The count.</param>
    /// <returns>The result of the operation.</returns>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe float MaxFloat32(float* data, int count)
    {
        var max = data[0];
        for (var i = 1; i < count; i++)
        {
            if (data[i] > max)
            {
                max = data[i];
            }
        }
        return max;
    }
}
