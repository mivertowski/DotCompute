// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using CoreKernelExecutionContext = DotCompute.Core.KernelExecutionContext;

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// Sample vectorized kernels demonstrating CPU SIMD operations.
/// </summary>
public static class SampleKernels
{
    /// <summary>
    /// Vectorized addition kernel using SIMD instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void VectorAdditionFloat32(
        float* source1,
        float* source2,
        float* destination,
        int count)
    {
        int vectorSize = Vector256<float>.Count;
        int vectorizedCount = count - (count % vectorSize);

        int i = 0;

        // Process vectors of 8 floats using AVX2
        if (Avx2.IsSupported && vectorizedCount >= vectorSize)
        {
            for (; i < vectorizedCount; i += vectorSize)
            {
                var vec1 = Avx.LoadVector256(source1 + i);
                var vec2 = Avx.LoadVector256(source2 + i);
                var result = Avx.Add(vec1, vec2);
                Avx.Store(destination + i, result);
            }
        }
        // Fall back to SSE for 4 floats
        else if (Sse.IsSupported)
        {
            vectorSize = Vector128<float>.Count;
            vectorizedCount = count - (count % vectorSize);

            for (; i < vectorizedCount; i += vectorSize)
            {
                var vec1 = Sse.LoadVector128(source1 + i);
                var vec2 = Sse.LoadVector128(source2 + i);
                var result = Sse.Add(vec1, vec2);
                Sse.Store(destination + i, result);
            }
        }

        // Process remaining elements scalar
        for (; i < count; i++)
        {
            destination[i] = source1[i] + source2[i];
        }
    }

    /// <summary>
    /// Vectorized multiplication kernel using SIMD instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void VectorMultiplyFloat32(
        float* source1,
        float* source2,
        float* destination,
        int count)
    {
        int vectorSize = Vector256<float>.Count;
        int vectorizedCount = count - (count % vectorSize);

        int i = 0;

        // Process vectors of 8 floats using AVX2
        if (Avx2.IsSupported && vectorizedCount >= vectorSize)
        {
            for (; i < vectorizedCount; i += vectorSize)
            {
                var vec1 = Avx.LoadVector256(source1 + i);
                var vec2 = Avx.LoadVector256(source2 + i);
                var result = Avx.Multiply(vec1, vec2);
                Avx.Store(destination + i, result);
            }
        }
        // Fall back to SSE for 4 floats
        else if (Sse.IsSupported)
        {
            vectorSize = Vector128<float>.Count;
            vectorizedCount = count - (count % vectorSize);

            for (; i < vectorizedCount; i += vectorSize)
            {
                var vec1 = Sse.LoadVector128(source1 + i);
                var vec2 = Sse.LoadVector128(source2 + i);
                var result = Sse.Multiply(vec1, vec2);
                Sse.Store(destination + i, result);
            }
        }

        // Process remaining elements scalar
        for (; i < count; i++)
        {
            destination[i] = source1[i] * source2[i];
        }
    }

    /// <summary>
    /// Vectorized dot product kernel using SIMD instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe float VectorDotProductFloat32(
        float* source1,
        float* source2,
        int count)
    {
        int vectorSize = Vector256<float>.Count;
        int vectorizedCount = count - (count % vectorSize);

        int i = 0;
        var sumVector = Vector256<float>.Zero;

        // Process vectors of 8 floats using AVX2
        if (Avx2.IsSupported && vectorizedCount >= vectorSize)
        {
            for (; i < vectorizedCount; i += vectorSize)
            {
                var vec1 = Avx.LoadVector256(source1 + i);
                var vec2 = Avx.LoadVector256(source2 + i);
                var product = Avx.Multiply(vec1, vec2);
                sumVector = Avx.Add(sumVector, product);
            }

            // Horizontal sum of the vector
            var sum = HorizontalSumAvx(sumVector);

            // Process remaining elements scalar
            for (; i < count; i++)
            {
                sum += source1[i] * source2[i];
            }

            return sum;
        }
        // Fall back to SSE for 4 floats
        else if (Sse.IsSupported)
        {
            vectorSize = Vector128<float>.Count;
            vectorizedCount = count - (count % vectorSize);
            var sumVector128 = Vector128<float>.Zero;

            for (; i < vectorizedCount; i += vectorSize)
            {
                var vec1 = Sse.LoadVector128(source1 + i);
                var vec2 = Sse.LoadVector128(source2 + i);
                var product = Sse.Multiply(vec1, vec2);
                sumVector128 = Sse.Add(sumVector128, product);
            }

            // Horizontal sum of the vector
            var sum = HorizontalSumSse(sumVector128);

            // Process remaining elements scalar
            for (; i < count; i++)
            {
                sum += source1[i] * source2[i];
            }

            return sum;
        }

        // Scalar fallback
        float scalarSum = 0.0f;
        for (; i < count; i++)
        {
            scalarSum += source1[i] * source2[i];
        }

        return scalarSum;
    }

    /// <summary>
    /// Vectorized matrix multiplication kernel (simplified).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void MatrixMultiplyFloat32(
        float* matrixA,
        float* matrixB,
        float* matrixC,
        int rows,
        int cols,
        int commonDim)
    {
        // Simple row-major matrix multiplication with vectorization
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                var sum = 0.0f;

                // Vectorized dot product for this element
                float* rowA = matrixA + i * commonDim;
                float* colB = matrixB + j; // Column-major access would be more complex

                // For simplicity, we'll do a scalar version here
                // In practice, you'd want to reorganize the data or use more sophisticated algorithms
                for (int k = 0; k < commonDim; k++)
                {
                    sum += rowA[k] * colB[k * cols];
                }

                matrixC[i * cols + j] = sum;
            }
        }
    }

    /// <summary>
    /// Vectorized reduction (sum) kernel using SIMD instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe float VectorSumFloat32(float* source, int count)
    {
        int vectorSize = Vector256<float>.Count;
        int vectorizedCount = count - (count % vectorSize);

        int i = 0;
        var sumVector = Vector256<float>.Zero;

        // Process vectors of 8 floats using AVX2
        if (Avx2.IsSupported && vectorizedCount >= vectorSize)
        {
            for (; i < vectorizedCount; i += vectorSize)
            {
                var vec = Avx.LoadVector256(source + i);
                sumVector = Avx.Add(sumVector, vec);
            }

            // Horizontal sum of the vector
            var sum = HorizontalSumAvx(sumVector);

            // Process remaining elements scalar
            for (; i < count; i++)
            {
                sum += source[i];
            }

            return sum;
        }
        // Fall back to SSE for 4 floats
        else if (Sse.IsSupported)
        {
            vectorSize = Vector128<float>.Count;
            vectorizedCount = count - (count % vectorSize);
            var sumVector128 = Vector128<float>.Zero;

            for (; i < vectorizedCount; i += vectorSize)
            {
                var vec = Sse.LoadVector128(source + i);
                sumVector128 = Sse.Add(sumVector128, vec);
            }

            // Horizontal sum of the vector
            var sum = HorizontalSumSse(sumVector128);

            // Process remaining elements scalar
            for (; i < count; i++)
            {
                sum += source[i];
            }

            return sum;
        }

        // Scalar fallback
        float scalarSum = 0.0f;
        for (; i < count; i++)
        {
            scalarSum += source[i];
        }

        return scalarSum;
    }

    /// <summary>
    /// ARM NEON vectorized addition kernel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void VectorAdditionFloat32Neon(
        float* source1,
        float* source2,
        float* destination,
        int count)
    {
        if (!AdvSimd.IsSupported)
        {
            // Fall back to scalar
            for (int j = 0; j < count; j++)
            {
                destination[j] = source1[j] + source2[j];
            }
            return;
        }

        int vectorSize = Vector128<float>.Count;
        int vectorizedCount = count - (count % vectorSize);

        int i = 0;

        // Process vectors of 4 floats using NEON
        for (; i < vectorizedCount; i += vectorSize)
        {
            var vec1 = AdvSimd.LoadVector128(source1 + i);
            var vec2 = AdvSimd.LoadVector128(source2 + i);
            var result = AdvSimd.Add(vec1, vec2);
            AdvSimd.Store(destination + i, result);
        }

        // Process remaining elements scalar
        for (; i < count; i++)
        {
            destination[i] = source1[i] + source2[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalSumAvx(Vector256<float> vector)
    {
        if (Avx2.IsSupported)
        {
            // Extract high and low 128-bit lanes
            var high = Avx.ExtractVector128(vector, 1);
            var low = Avx.ExtractVector128(vector, 0);

            // Add the two lanes
            var sum128 = Sse.Add(high, low);

            return HorizontalSumSse(sum128);
        }

        // Fallback: extract each element
        unsafe
        {
            var span = stackalloc float[8];
            vector.Store(span);

            return span[0] + span[1] + span[2] + span[3] + span[4] + span[5] + span[6] + span[7];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalSumSse(Vector128<float> vector)
    {
        if (Sse3.IsSupported)
        {
            // Horizontal add pairs: [a, b, c, d] -> [a+b, c+d, a+b, c+d]
            var hadd1 = Sse3.HorizontalAdd(vector, vector);
            // Second horizontal add: [a+b, c+d, a+b, c+d] -> [a+b+c+d, ...]
            var hadd2 = Sse3.HorizontalAdd(hadd1, hadd1);
            // Extract the sum
            return hadd2.ToScalar();
        }

        // Fallback: extract each element
        unsafe
        {
            var span = stackalloc float[4];
            vector.Store(span);

            return span[0] + span[1] + span[2] + span[3];
        }
    }
}

/// <summary>
/// Kernel execution utilities for CPU vectorization.
/// </summary>
public static class CpuKernelExecutor
{
    /// <summary>
    /// Executes a vectorized kernel with automatic SIMD selection.
    /// </summary>
    public static void ExecuteVectorizedKernel<T>(
        CoreKernelExecutionContext context,
        VectorizedKernelDelegate<T> kernelDelegate) where T : unmanaged
    {
        // Extract buffer arguments
        var buffers = new List<IMemoryBuffer>();
        if (context.Arguments != null)
        {
            foreach (var arg in context.Arguments)
            {
                if (arg is IMemoryBuffer buffer)
                {
                    buffers.Add(buffer);
                }
            }
        }

        if (buffers.Count < 2)
        {
            throw new ArgumentException("Vector kernel requires at least 2 buffer arguments");
        }

        // Calculate work distribution
        // Calculate total work items from work dimensions
        var totalWorkItems = 1L;
        if (context.WorkDimensions != null)
        {
            foreach (var dim in context.WorkDimensions)
            {
                totalWorkItems *= dim;
            }
        }
        unsafe
        {
            var elementsPerWorkItem = buffers[0].SizeInBytes / sizeof(T);

            // Execute the kernel
            var src1Memory = ((CpuMemoryBuffer)buffers[0]).GetMemory();
            var src2Memory = ((CpuMemoryBuffer)buffers[1]).GetMemory();
            var dstMemory = buffers.Count > 2 ? ((CpuMemoryBuffer)buffers[2]).GetMemory() : Memory<byte>.Empty;

            fixed (byte* src1Ptr = src1Memory.Span)
            fixed (byte* src2Ptr = src2Memory.Span)
            {
                var dst = buffers.Count > 2 ? dstMemory : Memory<byte>.Empty;
                fixed (byte* dstPtr = dst.Span)
                {
                    var src1 = (T*)src1Ptr;
                    var src2 = (T*)src2Ptr;
                    var dstT = buffers.Count > 2 ? (T*)dstPtr : null;

                    kernelDelegate(src1, src2, dstT, (int)elementsPerWorkItem);
                }
            }
        }
    }
}

/// <summary>
/// Delegate for vectorized kernel execution.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="source1">First source buffer.</param>
/// <param name="source2">Second source buffer.</param>
/// <param name="destination">Destination buffer (can be null for reduction operations).</param>
/// <param name="count">Number of elements to process.</param>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - Delegate is the appropriate suffix for a delegate type
public unsafe delegate void VectorizedKernelDelegate<T>(T* source1, T* source2, T* destination, int count) where T : unmanaged;
#pragma warning restore CA1711
