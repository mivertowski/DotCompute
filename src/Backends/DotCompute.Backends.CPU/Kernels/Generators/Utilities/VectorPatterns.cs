// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using DotCompute.Backends.CPU.Kernels.Generators.InstructionSets;

namespace DotCompute.Backends.CPU.Kernels.Generators.Utilities;

/// <summary>
/// Provides optimized vector operation patterns for common mathematical operations
/// with automatic hardware acceleration selection.
/// </summary>
public static class VectorPatterns
{
    /// <summary>
    /// Performs vectorized addition of two arrays using the best available instruction set.
    /// </summary>
    /// <typeparam name="T">The element type (must be unmanaged).</typeparam>
    /// <param name="a">First input array.</param>
    /// <param name="b">Second input array.</param>
    /// <param name="result">Output array for results.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAdd<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> result)
        where T : unmanaged
    {
        if (typeof(T) == typeof(float))
        {
            VectorAddFloat32(
                MemoryMarshal.Cast<T, float>(a),
                MemoryMarshal.Cast<T, float>(b),
                MemoryMarshal.Cast<T, float>(result));
        }
        else if (typeof(T) == typeof(double))
        {
            VectorAddFloat64(
                MemoryMarshal.Cast<T, double>(a),
                MemoryMarshal.Cast<T, double>(b),
                MemoryMarshal.Cast<T, double>(result));
        }
        else
        {
            // Generic fallback using Vector<T>
            VectorAddGeneric(a, b, result);
        }
    }

    /// <summary>
    /// Performs vectorized multiplication of two arrays using the best available instruction set.
    /// </summary>
    /// <typeparam name="T">The element type (must be unmanaged).</typeparam>
    /// <param name="a">First input array.</param>
    /// <param name="b">Second input array.</param>
    /// <param name="result">Output array for results.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorMultiply<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> result)
        where T : unmanaged
    {
        if (typeof(T) == typeof(float))
        {
            VectorMultiplyFloat32(
                MemoryMarshal.Cast<T, float>(a),
                MemoryMarshal.Cast<T, float>(b),
                MemoryMarshal.Cast<T, float>(result));
        }
        else if (typeof(T) == typeof(double))
        {
            VectorMultiplyFloat64(
                MemoryMarshal.Cast<T, double>(a),
                MemoryMarshal.Cast<T, double>(b),
                MemoryMarshal.Cast<T, double>(result));
        }
        else
        {
            // Generic fallback using Vector<T>
            VectorMultiplyGeneric(a, b, result);
        }
    }

    /// <summary>
    /// Performs vectorized fused multiply-add operation: result = a * b + c.
    /// </summary>
    /// <typeparam name="T">The element type (must be unmanaged).</typeparam>
    /// <param name="a">First input array (multiplicand).</param>
    /// <param name="b">Second input array (multiplier).</param>
    /// <param name="c">Third input array (addend).</param>
    /// <param name="result">Output array for results.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFMA<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, ReadOnlySpan<T> c, Span<T> result)
        where T : unmanaged
    {
        if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
        {
            throw new ArgumentException("All spans must have the same length");
        }

        if (typeof(T) == typeof(float))
        {
            VectorFMAFloat32(
                MemoryMarshal.Cast<T, float>(a),
                MemoryMarshal.Cast<T, float>(b),
                MemoryMarshal.Cast<T, float>(c),
                MemoryMarshal.Cast<T, float>(result));
        }
        else if (typeof(T) == typeof(double))
        {
            VectorFMAFloat64(
                MemoryMarshal.Cast<T, double>(a),
                MemoryMarshal.Cast<T, double>(b),
                MemoryMarshal.Cast<T, double>(c),
                MemoryMarshal.Cast<T, double>(result));
        }
        else
        {
            // Generic fallback
            for (var i = 0; i < a.Length; i++)
            {
                result[i] = AddGeneric(MultiplyGeneric(a[i], b[i]), c[i]);
            }
        }
    }

    #region Float32 Operations

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat32(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        // Use platform-optimal SIMD based on hardware capabilities and data size
        if (Vector512.IsHardwareAccelerated && a.Length >= 16)
        {
            VectorAddFloat32_Avx512(a, b, result);
        }
        else if (Vector256.IsHardwareAccelerated && a.Length >= 8)
        {
            VectorAddFloat32_Avx2(a, b, result);
        }
        else if (Vector128.IsHardwareAccelerated && a.Length >= 4)
        {
            VectorAddFloat32_Sse(a, b, result);
        }
        else
        {
            // Scalar fallback
            for (var i = 0; i < a.Length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyFloat32(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        if (Vector512.IsHardwareAccelerated && a.Length >= 16)
        {
            VectorMultiplyFloat32_Avx512(a, b, result);
        }
        else if (Vector256.IsHardwareAccelerated && a.Length >= 8)
        {
            VectorMultiplyFloat32_Avx2(a, b, result);
        }
        else if (Vector128.IsHardwareAccelerated && a.Length >= 4)
        {
            VectorMultiplyFloat32_Sse(a, b, result);
        }
        else
        {
            // Scalar fallback
            for (var i = 0; i < a.Length; i++)
            {
                result[i] = a[i] * b[i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorFMAFloat32(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> result)
    {
        // Use the best available FMA implementation
        if (Avx512F.IsSupported && a.Length >= 16)
        {
            Avx512KernelExecutor.ExecuteFloat32FMA(a, b, c, result);
        }
        else if (Fma.IsSupported && Vector256.IsHardwareAccelerated && a.Length >= 8)
        {
            Avx2KernelExecutor.ExecuteFloat32FMA(a, b, c, result);
        }
        else if (AdvSimd.IsSupported && a.Length >= 4)
        {
            NeonKernelExecutor.ExecuteFloat32FMA(a, b, c, result);
        }
        else
        {
            // Fallback to scalar FMA
            for (var i = 0; i < a.Length; i++)
            {
                result[i] = MathF.FusedMultiplyAdd(a[i], b[i], c[i]);
            }
        }
    }

    #endregion

    #region Float64 Operations

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat64(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        if (Vector512.IsHardwareAccelerated && a.Length >= 8)
        {
            VectorAddFloat64_Avx512(a, b, result);
        }
        else if (Vector256.IsHardwareAccelerated && a.Length >= 4)
        {
            VectorAddFloat64_Avx2(a, b, result);
        }
        else if (Vector128.IsHardwareAccelerated && a.Length >= 2)
        {
            VectorAddFloat64_Sse(a, b, result);
        }
        else
        {
            // Scalar fallback
            for (var i = 0; i < a.Length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyFloat64(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        if (Vector512.IsHardwareAccelerated && a.Length >= 8)
        {
            VectorMultiplyFloat64_Avx512(a, b, result);
        }
        else if (Vector256.IsHardwareAccelerated && a.Length >= 4)
        {
            VectorMultiplyFloat64_Avx2(a, b, result);
        }
        else if (Vector128.IsHardwareAccelerated && a.Length >= 2)
        {
            VectorMultiplyFloat64_Sse(a, b, result);
        }
        else
        {
            // Scalar fallback
            for (var i = 0; i < a.Length; i++)
            {
                result[i] = a[i] * b[i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorFMAFloat64(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> result)
    {
        // Use the best available FMA implementation
        if (Avx512F.IsSupported && a.Length >= 8)
        {
            Avx512KernelExecutor.ExecuteFloat64FMA(a, b, c, result);
        }
        else if (Fma.IsSupported && Vector256.IsHardwareAccelerated && a.Length >= 4)
        {
            Avx2KernelExecutor.ExecuteFloat64FMA(a, b, c, result);
        }
        else if (AdvSimd.Arm64.IsSupported && a.Length >= 2)
        {
            NeonKernelExecutor.ExecuteFloat64FMA(a, b, c, result);
        }
        else
        {
            // Fallback to scalar FMA
            for (var i = 0; i < a.Length; i++)
            {
                result[i] = Math.FusedMultiplyAdd(a[i], b[i], c[i]);
            }
        }
    }

    #endregion

    #region AVX-512 Implementations

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat32_Avx512(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        const int vectorSize = 16;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Avx512F.Add(va, vb));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyFloat32_Avx512(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        const int vectorSize = 16;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Avx512F.Multiply(va, vb));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat64_Avx512(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        const int vectorSize = 8;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Avx512F.Add(va, vb));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyFloat64_Avx512(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        const int vectorSize = 8;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Avx512F.Multiply(va, vb));
    }

    #endregion

    #region AVX2 Implementations

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat32_Avx2(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        const int vectorSize = 8;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Avx.Add(va, vb));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyFloat32_Avx2(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        const int vectorSize = 8;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Avx.Multiply(va, vb));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat64_Avx2(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        const int vectorSize = 4;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Avx.Add(va, vb));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyFloat64_Avx2(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        const int vectorSize = 4;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Avx.Multiply(va, vb));
    }

    #endregion

    #region SSE Implementations

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat32_Sse(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        const int vectorSize = 4;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Sse.Add(va, vb));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyFloat32_Sse(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        const int vectorSize = 4;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Sse.Multiply(va, vb));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat64_Sse(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        const int vectorSize = 2;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Sse2.Add(va, vb));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyFloat64_Sse(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        const int vectorSize = 2;
        var vectorCount = a.Length / vectorSize;
        ProcessVectorizedOperation(a, b, result, vectorSize, vectorCount, 
            (va, vb) => Sse2.Multiply(va, vb));
    }

    #endregion

    #region Generic Operations

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddGeneric<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> result)
        where T : unmanaged
    {
        var vectorSize = Vector<T>.Count;
        var vectorizedLength = a.Length - (a.Length % vectorSize);

        // Process vectors
        for (var i = 0; i < vectorizedLength; i += vectorSize)
        {
            var va = new Vector<T>(a.Slice(i, vectorSize));
            var vb = new Vector<T>(b.Slice(i, vectorSize));
            var vr = va + vb;
            vr.CopyTo(result.Slice(i, vectorSize));
        }

        // Process remaining elements
        for (var i = vectorizedLength; i < a.Length; i++)
        {
            result[i] = AddGeneric(a[i], b[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyGeneric<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> result)
        where T : unmanaged
    {
        var vectorSize = Vector<T>.Count;
        var vectorizedLength = a.Length - (a.Length % vectorSize);

        // Process vectors
        for (var i = 0; i < vectorizedLength; i += vectorSize)
        {
            var va = new Vector<T>(a.Slice(i, vectorSize));
            var vb = new Vector<T>(b.Slice(i, vectorSize));
            var vr = va * vb;
            vr.CopyTo(result.Slice(i, vectorSize));
        }

        // Process remaining elements
        for (var i = vectorizedLength; i < a.Length; i++)
        {
            result[i] = MultiplyGeneric(a[i], b[i]);
        }
    }

    #endregion

    #region Helper Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessVectorizedOperation<T>(
        ReadOnlySpan<T> a, 
        ReadOnlySpan<T> b, 
        Span<T> result, 
        int vectorSize, 
        long vectorCount,
        Func<T, T, T> vectorOp) where T : struct
    {
        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var va = Unsafe.As<T, T>(ref Unsafe.Add(ref aRef, offset));
            var vb = Unsafe.As<T, T>(ref Unsafe.Add(ref bRef, offset));
            var vr = vectorOp(va, vb);
            Unsafe.As<T, T>(ref Unsafe.Add(ref resultRef, offset)) = vr;
        }

        // Handle remainder
        var remainder = a.Length % vectorSize;
        if (remainder > 0)
        {
            var lastOffset = (int)(vectorCount * vectorSize);
            for (var i = 0; i < remainder; i++)
            {
                result[lastOffset + i] = AddGeneric(a[lastOffset + i], b[lastOffset + i]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T AddGeneric<T>(T a, T b) where T : unmanaged
    {
        if (typeof(T) == typeof(int))
        {
            return (T)(object)(((int)(object)a) + ((int)(object)b));
        }
        else if (typeof(T) == typeof(long))
        {
            return (T)(object)(((long)(object)a) + ((long)(object)b));
        }
        else if (typeof(T) == typeof(float))
        {
            return (T)(object)(((float)(object)a) + ((float)(object)b));
        }
        else if (typeof(T) == typeof(double))
        {
            return (T)(object)(((double)(object)a) + ((double)(object)b));
        }
        else
        {
            throw new NotSupportedException($"Addition not supported for type {typeof(T)}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T MultiplyGeneric<T>(T a, T b) where T : unmanaged
    {
        if (typeof(T) == typeof(int))
        {
            return (T)(object)(((int)(object)a) * ((int)(object)b));
        }
        else if (typeof(T) == typeof(long))
        {
            return (T)(object)(((long)(object)a) * ((long)(object)b));
        }
        else if (typeof(T) == typeof(float))
        {
            return (T)(object)(((float)(object)a) * ((float)(object)b));
        }
        else if (typeof(T) == typeof(double))
        {
            return (T)(object)(((double)(object)a) * ((double)(object)b));
        }
        else
        {
            throw new NotSupportedException($"Multiplication not supported for type {typeof(T)}");
        }
    }

    #endregion
}