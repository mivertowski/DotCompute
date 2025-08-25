// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.Arm;
using global::System.Runtime.Intrinsics.X86;
using DotCompute.Backends.CPU.Kernels;

namespace DotCompute.Backends.CPU.Optimization;


/// <summary>
/// Simplified Native AOT optimization layer that uses the existing AdvancedSimdPatterns
/// for maximum performance with minimal compilation issues.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible")]
public static class NativeAotOptimizations
{
    /// <summary>
    /// Pre-compiled delegates for optimal Native AOT performance.
    /// Uses hardware detection at startup to select best code paths.
    /// </summary>
    internal static class CompiledDelegates
    {
        // Use the robust implementations from AdvancedSimdPatterns
        public static readonly Func<ReadOnlySpan<float>, ReadOnlySpan<float>, float> DotProductFloat32 = AdvancedSimdPatterns.DotProduct;
        public static readonly Func<ReadOnlySpan<double>, ReadOnlySpan<double>, double> DotProductFloat64 = DotProductDouble;

        // Vector operations delegates
        public static readonly Action<ReadOnlySpan<float>, ReadOnlySpan<float>, Span<float>> VectorAddFloat32 = VectorAdd;
        public static readonly Action<ReadOnlySpan<double>, ReadOnlySpan<double>, Span<double>> VectorAddFloat64 = VectorAdd;
    }

    /// <summary>
    /// High-performance vector addition using SIMD with hardware detection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        if (a.Length != b.Length || a.Length != result.Length)
        {
            throw new ArgumentException("All spans must have the same length");
        }

        var length = a.Length;
        var i = 0;

        // AVX-512 path (16 floats at once)
        if (Avx512F.IsSupported && Vector512.IsHardwareAccelerated && length >= 16)
        {
            var vectorLength = Vector512<float>.Count;
            for (; i <= length - vectorLength; i += vectorLength)
            {
                var va = Vector512.LoadUnsafe(ref Unsafe.AsRef(in a[i]));
                var vb = Vector512.LoadUnsafe(ref Unsafe.AsRef(in b[i]));
                var vr = Vector512.Add(va, vb);
                Vector512.StoreUnsafe(vr, ref result[i]);
            }
        }
        // AVX2 path (8 floats at once)
        else if (Avx2.IsSupported && Vector256.IsHardwareAccelerated && length >= 8)
        {
            var vectorLength = Vector256<float>.Count;
            for (; i <= length - vectorLength; i += vectorLength)
            {
                var va = Vector256.LoadUnsafe(ref Unsafe.AsRef(in a[i]));
                var vb = Vector256.LoadUnsafe(ref Unsafe.AsRef(in b[i]));
                var vr = Vector256.Add(va, vb);
                Vector256.StoreUnsafe(vr, ref result[i]);
            }
        }
        // NEON path (4 floats at once)
        else if (AdvSimd.IsSupported && length >= 4)
        {
            var vectorLength = Vector128<float>.Count;
            for (; i <= length - vectorLength; i += vectorLength)
            {
                var va = Vector128.LoadUnsafe(ref Unsafe.AsRef(in a[i]));
                var vb = Vector128.LoadUnsafe(ref Unsafe.AsRef(in b[i]));
                var vr = Vector128.Add(va, vb);
                Vector128.StoreUnsafe(vr, ref result[i]);
            }
        }

        // Handle remaining elements (scalar fallback)
        for (; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    /// <summary>
    /// High-performance vector addition for double precision.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VectorAdd(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        if (a.Length != b.Length || a.Length != result.Length)
        {
            throw new ArgumentException("All spans must have the same length");
        }

        var length = a.Length;
        var i = 0;

        // AVX-512 path (8 doubles at once)
        if (Avx512F.IsSupported && Vector512.IsHardwareAccelerated && length >= 8)
        {
            var vectorLength = Vector512<double>.Count;
            for (; i <= length - vectorLength; i += vectorLength)
            {
                var va = Vector512.LoadUnsafe(ref Unsafe.AsRef(in a[i]));
                var vb = Vector512.LoadUnsafe(ref Unsafe.AsRef(in b[i]));
                var vr = Vector512.Add(va, vb);
                Vector512.StoreUnsafe(vr, ref result[i]);
            }
        }
        // AVX2 path (4 doubles at once)
        else if (Avx2.IsSupported && Vector256.IsHardwareAccelerated && length >= 4)
        {
            var vectorLength = Vector256<double>.Count;
            for (; i <= length - vectorLength; i += vectorLength)
            {
                var va = Vector256.LoadUnsafe(ref Unsafe.AsRef(in a[i]));
                var vb = Vector256.LoadUnsafe(ref Unsafe.AsRef(in b[i]));
                var vr = Vector256.Add(va, vb);
                Vector256.StoreUnsafe(vr, ref result[i]);
            }
        }
        // NEON path (2 doubles at once)
        else if (AdvSimd.IsSupported && length >= 2)
        {
            var vectorLength = Vector128<double>.Count;
            for (; i <= length - vectorLength; i += vectorLength)
            {
                var va = Vector128.LoadUnsafe(ref Unsafe.AsRef(in a[i]));
                var vb = Vector128.LoadUnsafe(ref Unsafe.AsRef(in b[i]));
                var vr = Vector128.Add(va, vb);
                Vector128.StoreUnsafe(vr, ref result[i]);
            }
        }

        // Handle remaining elements (scalar fallback)
        for (; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }

    /// <summary>
    /// Gets the optimal vector width for the current hardware.
    /// </summary>
    public static int GetOptimalVectorWidth<T>() where T : struct
    {
        if (typeof(T) == typeof(float))
        {
            if (Avx512F.IsSupported && Vector512.IsHardwareAccelerated)
            {
                return Vector512<float>.Count;
            }

            if (Avx2.IsSupported && Vector256.IsHardwareAccelerated)
            {
                return Vector256<float>.Count;
            }

            if (AdvSimd.IsSupported || Sse.IsSupported)
            {
                return Vector128<float>.Count;
            }
        }
        else if (typeof(T) == typeof(double))
        {
            if (Avx512F.IsSupported && Vector512.IsHardwareAccelerated)
            {
                return Vector512<double>.Count;
            }

            if (Avx2.IsSupported && Vector256.IsHardwareAccelerated)
            {
                return Vector256<double>.Count;
            }

            if (AdvSimd.IsSupported || Sse2.IsSupported)
            {
                return Vector128<double>.Count;
            }
        }

        return 1; // Scalar fallback
    }

    /// <summary>
    /// Checks if the current hardware supports advanced SIMD operations.
    /// </summary>
    public static bool HasAdvancedSimdSupport
    {
        get
        {
            return (Avx512F.IsSupported && Vector512.IsHardwareAccelerated) ||
                   (Avx2.IsSupported && Vector256.IsHardwareAccelerated) ||
                   AdvSimd.IsSupported ||
                   Sse.IsSupported;
        }
    }

    /// <summary>
    /// Performs vectorized dot product for double precision.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double DotProductDouble(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Arrays must have the same length");
        }

        if (Vector512.IsHardwareAccelerated && a.Length >= 8)
        {
            return DotProductDoubleAvx512(a, b);
        }
        else if (Vector256.IsHardwareAccelerated && a.Length >= 4)
        {
            return DotProductDoubleAvx2(a, b);
        }
        else
        {
            return Vector128.IsHardwareAccelerated && a.Length >= 2 ? AdvSimd.IsSupported ? DotProductDoubleNeon(a, b) : DotProductDoubleSse(a, b) : DotProductDoubleScalar(a, b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductDoubleScalar(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        var result = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            result += a[i] * b[i];
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductDoubleAvx512(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        const int vectorSize = 8;
        var vectorCount = a.Length / vectorSize;

        ref var aRef = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(a);
        ref var bRef = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(b);

        var sum = Vector512<double>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var va = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, offset));
            var vb = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, offset));

            if (Avx512F.IsSupported)
            {
                sum = Avx512F.FusedMultiplyAdd(va, vb, sum);
            }
            else
            {
                sum = Avx512F.Add(sum, Avx512F.Multiply(va, vb));
            }
        }

        var result = Vector512.Sum(sum);

        var remainder = a.Length % vectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * vectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += a[lastOffset + i] * b[lastOffset + i];
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductDoubleAvx2(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        const int vectorSize = 4;
        var vectorCount = a.Length / vectorSize;

        ref var aRef = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(a);
        ref var bRef = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(b);

        var sum = Vector256<double>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var va = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, offset));
            var vb = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, offset));

            if (Fma.IsSupported)
            {
                sum = Fma.MultiplyAdd(va, vb, sum);
            }
            else
            {
                sum = Avx.Add(sum, Avx.Multiply(va, vb));
            }
        }

        var result = Vector256.Sum(sum);

        var remainder = a.Length % vectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * vectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += a[lastOffset + i] * b[lastOffset + i];
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductDoubleNeon(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        const int vectorSize = 2;
        var vectorCount = a.Length / vectorSize;

        ref var aRef = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(a);
        ref var bRef = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(b);

        var sum = Vector128<double>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, offset));
            var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, offset));

            sum = AdvSimd.Arm64.FusedMultiplyAdd(sum, va, vb);
        }

        var result = Vector128.Sum(sum);

        var remainder = a.Length % vectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * vectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += a[lastOffset + i] * b[lastOffset + i];
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductDoubleSse(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        const int vectorSize = 2;
        var vectorCount = a.Length / vectorSize;

        ref var aRef = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(a);
        ref var bRef = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(b);

        var sum = Vector128<double>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * vectorSize;
            var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, offset));
            var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, offset));

            sum = Sse2.Add(sum, Sse2.Multiply(va, vb));
        }

        var result = Vector128.Sum(sum);

        var remainder = a.Length % vectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * vectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += a[lastOffset + i] * b[lastOffset + i];
            }
        }

        return result;
    }
}
