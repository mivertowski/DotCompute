// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.Arm;
using global::System.Runtime.Intrinsics.X86;
using DotCompute.Backends.CPU.Intrinsics;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CPU.Kernels.Simd;

namespace DotCompute.Backends.CPU.SIMD;

/// <summary>
/// High-performance vectorized operations with instruction-specific optimizations
/// </summary>
public sealed class SimdVectorOperations : IDisposable
{
    private readonly SimdSummary _capabilities;
    private readonly ILogger _logger;
    private volatile bool _disposed;

    public SimdVectorOperations(SimdSummary capabilities, ILogger logger)
    {
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes vectorized operations based on strategy
    /// </summary>
    public unsafe void ExecuteVectorized<T>(
        SimdExecutionStrategy strategy,
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        switch (strategy)
        {
            case SimdExecutionStrategy.Avx512:
                ExecuteAvx512<T>(input1, input2, output, elementCount, context);
                break;
            case SimdExecutionStrategy.Avx2:
                ExecuteAvx2<T>(input1, input2, output, elementCount, context);
                break;
            case SimdExecutionStrategy.Sse:
                ExecuteSse<T>(input1, input2, output, elementCount, context);
                break;
            case SimdExecutionStrategy.Neon:
                ExecuteNeon<T>(input1, input2, output, elementCount, context);
                break;
            case SimdExecutionStrategy.Scalar:
                ExecuteScalar<T>(input1, input2, output, elementCount, context);
                break;
            default:
                throw new InvalidOperationException($"Unsupported execution strategy: {strategy}");
        }
    }

    /// <summary>
    /// Executes reduction operations with optimal SIMD patterns
    /// </summary>
    public unsafe T ExecuteReduction<T>(
        ReadOnlySpan<T> input,
        ReductionOperation operation,
        ExecutionContext context) where T : unmanaged
    {
        return operation switch
        {
            ReductionOperation.Sum => ExecuteVectorizedSum<T>(input, context),
            ReductionOperation.Min => ExecuteVectorizedMin<T>(input, context),
            ReductionOperation.Max => ExecuteVectorizedMax<T>(input, context),
            ReductionOperation.Product => ExecuteVectorizedProduct<T>(input, context),
            _ => throw new ArgumentException($"Unsupported reduction operation: {operation}")
        };
    }

    #region AVX-512 Implementation

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe void ExecuteAvx512<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        if (!Avx512F.IsSupported)
        {
            ExecuteAvx2(input1, input2, output, elementCount, context);
            return;
        }

        fixed (T* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
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
                Prefetch(ptr1 + offset4 + vectorSize, PrefetchMode.Temporal);
                Prefetch(ptr2 + offset4 + vectorSize, PrefetchMode.Temporal);

                // Load and process 4 vectors in parallel
                var v1_1 = Vector512.Load(ptr1 + offset1);
                var v1_2 = Vector512.Load(ptr2 + offset1);
                var v2_1 = Vector512.Load(ptr1 + offset2);
                var v2_2 = Vector512.Load(ptr2 + offset2);
                var v3_1 = Vector512.Load(ptr1 + offset3);
                var v3_2 = Vector512.Load(ptr2 + offset3);
                var v4_1 = Vector512.Load(ptr1 + offset4);
                var v4_2 = Vector512.Load(ptr2 + offset4);

                // Perform operations (assuming addition for example)
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
            ExecuteScalarRemainder(ptr1 + scalarStart, ptr2 + scalarStart,
                                 ptrOut + scalarStart, remainder);
        }
    }

    #endregion

    #region AVX2 Implementation

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe void ExecuteAvx2<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        if (!Avx2.IsSupported)
        {
            ExecuteSse(input1, input2, output, elementCount, context);
            return;
        }

        fixed (T* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
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
                    Prefetch(ptr1 + prefetchOffset, PrefetchMode.Temporal);
                    Prefetch(ptr2 + prefetchOffset, PrefetchMode.Temporal);
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
            ExecuteScalarRemainder(ptr1 + scalarStart, ptr2 + scalarStart,
                                 ptrOut + scalarStart, remainder);
        }
    }

    #endregion

    #region SSE Implementation

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe void ExecuteSse<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        if (!Sse2.IsSupported)
        {
            ExecuteScalar(input1, input2, output, elementCount, context);
            return;
        }

        fixed (T* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
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
                    Prefetch(ptr1 + (i + 2) * vectorSize, PrefetchMode.Temporal);
                    Prefetch(ptr2 + (i + 2) * vectorSize, PrefetchMode.Temporal);
                }

                var v1 = Vector128.Load(ptr1 + offset);
                var v2 = Vector128.Load(ptr2 + offset);
                var result = Vector128.Add(v1, v2);
                Vector128.Store(result, ptrOut + offset);
            }

            // Handle scalar remainder
            var scalarStart = vectorCount * vectorSize;
            ExecuteScalarRemainder(ptr1 + scalarStart, ptr2 + scalarStart,
                                 ptrOut + scalarStart, remainder);
        }
    }

    #endregion

    #region ARM NEON Implementation

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe void ExecuteNeon<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        if (!AdvSimd.IsSupported)
        {
            ExecuteScalar(input1, input2, output, elementCount, context);
            return;
        }

        // Use type-specific NEON processing for optimal performance
        if (typeof(T) == typeof(float))
        {
            ExecuteNeonFloat32(
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input1),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input2),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(output),
                elementCount, context);
            return;
        }
        else if (typeof(T) == typeof(int))
        {
            ExecuteNeonInt32(
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, int>(input1),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, int>(input2),
                System.Runtime.InteropServices.MemoryMarshal.Cast<T, int>(output),
                elementCount, context);
            return;
        }

        // Fallback for unsupported types
        ExecuteScalar(input1, input2, output, elementCount, context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ExecuteNeonFloat32(
        ReadOnlySpan<float> input1,
        ReadOnlySpan<float> input2,
        Span<float> output,
        long elementCount,
        ExecutionContext context)
    {
        fixed (float* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
            var vectorSize = Vector128<float>.Count;
            var vectorCount = elementCount / vectorSize;

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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ExecuteNeonInt32(
        ReadOnlySpan<int> input1,
        ReadOnlySpan<int> input2,
        Span<int> output,
        long elementCount,
        ExecutionContext context)
    {
        fixed (int* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
            var vectorSize = Vector128<int>.Count;
            var vectorCount = elementCount / vectorSize;

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
    }

    #endregion

    #region Scalar Implementation

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ExecuteScalar<T>(
        ReadOnlySpan<T> input1,
        ReadOnlySpan<T> input2,
        Span<T> output,
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        fixed (T* ptr1 = input1, ptr2 = input2, ptrOut = output)
        {
            ExecuteScalarRemainder(ptr1, ptr2, ptrOut, elementCount);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ExecuteScalarRemainder<T>(T* ptr1, T* ptr2, T* ptrOut, long count) where T : unmanaged
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

    #endregion

    #region Reduction Operations

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe T ExecuteVectorizedSum<T>(ReadOnlySpan<T> input, ExecutionContext context) where T : unmanaged
    {
        if (typeof(T) == typeof(float))
        {
            var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input);
            var result = VectorizedSumFloat32(floatSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(double))
        {
            var doubleSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, double>(input);
            var result = VectorizedSumFloat64(doubleSpan);
            return *(T*)&result;
        }
        if (typeof(T) == typeof(int))
        {
            var intSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, int>(input);
            var result = VectorizedSumInt32(intSpan);
            return *(T*)&result;
        }

        return ScalarSum(input);
    }

    private unsafe T ExecuteVectorizedMin<T>(ReadOnlySpan<T> input, ExecutionContext context) where T : unmanaged
    {
        if (input.IsEmpty)
        {
            return default;
        }


        if (typeof(T) == typeof(float))
        {
            var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input);
            var result = VectorizedMinFloat32(floatSpan);
            return *(T*)&result;
        }

        return ScalarMin(input);
    }

    private unsafe T ExecuteVectorizedMax<T>(ReadOnlySpan<T> input, ExecutionContext context) where T : unmanaged
    {
        if (input.IsEmpty)
        {
            return default;
        }


        if (typeof(T) == typeof(float))
        {
            var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input);
            var result = VectorizedMaxFloat32(floatSpan);
            return *(T*)&result;
        }

        return ScalarMax(input);
    }

    private unsafe T ExecuteVectorizedProduct<T>(ReadOnlySpan<T> input, ExecutionContext context) where T : unmanaged
    {
        if (input.IsEmpty)
        {
            return default;
        }


        if (typeof(T) == typeof(float))
        {
            var floatSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, float>(input);
            var result = VectorizedProductFloat32(floatSpan);
            return *(T*)&result;
        }

        return ScalarProduct(input);
    }

    #endregion

    #region Helper Methods

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
        if (typeof(T) == typeof(int))
        {
            var ia = *(int*)&a;
            var ib = *(int*)&b;
            var result = ia + ib;
            return *(T*)&result;
        }

        throw new NotSupportedException($"Type {typeof(T)} not supported for addition");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void Prefetch(void* address, PrefetchMode mode)
    {
        if (Sse.IsSupported)
        {
            switch (mode)
            {
                case PrefetchMode.Temporal:
                    Sse.Prefetch0(address);
                    break;
                case PrefetchMode.NonTemporal:
                    Sse.PrefetchNonTemporal(address);
                    break;
            }
        }
    }

    // Stub implementations for reduction operations TODO
    private static unsafe float VectorizedSumFloat32(ReadOnlySpan<float> input) => input.IsEmpty ? 0f : input[0];
    private static unsafe double VectorizedSumFloat64(ReadOnlySpan<double> input) => input.IsEmpty ? 0.0 : input[0];
    private static unsafe int VectorizedSumInt32(ReadOnlySpan<int> input) => input.IsEmpty ? 0 : input[0];

    private static unsafe float VectorizedMinFloat32(ReadOnlySpan<float> input) => input.IsEmpty ? 0f : input[0];
    private static unsafe float VectorizedMaxFloat32(ReadOnlySpan<float> input) => input.IsEmpty ? 0f : input[0];
    private static unsafe float VectorizedProductFloat32(ReadOnlySpan<float> input) => input.IsEmpty ? 1f : input[0];

    private static T ScalarSum<T>(ReadOnlySpan<T> input) where T : unmanaged => input.IsEmpty ? default : input[0];
    private static T ScalarMin<T>(ReadOnlySpan<T> input) where T : unmanaged => input.IsEmpty ? default : input[0];
    private static T ScalarMax<T>(ReadOnlySpan<T> input) where T : unmanaged => input.IsEmpty ? default : input[0];
    private static T ScalarProduct<T>(ReadOnlySpan<T> input) where T : unmanaged => input.IsEmpty ? default : input[0];

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogDebug("SIMD Vector Operations disposed");
        }
    }
}