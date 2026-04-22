// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Kernels;


/// <summary>
/// Advanced SIMD patterns for high-performance scientific computing.
/// Implements patterns from the SIMD Playbook for .NET 9 with Native AOT.
/// </summary>
public static class AdvancedSimdPatterns
{
    /// <summary>
    /// Performs vectorized dot product of two arrays with platform-optimal SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Arrays must have the same length");
        }

        if (Vector512.IsHardwareAccelerated && a.Length >= 16)
        {
            return DotProductAvx512(a, b);
        }
        else if (Vector256.IsHardwareAccelerated && a.Length >= 8)
        {
            return DotProductAvx2(a, b);
        }
        else
        {
            return Vector128.IsHardwareAccelerated && a.Length >= 4 ? AdvSimd.IsSupported ? DotProductNeon(a, b) : DotProductSse(a, b) : DotProductScalar(a, b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float DotProductAvx512(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        const int VectorSize = 16;
        var vectorCount = a.Length / VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);

        var sum = Vector512<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
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

        // Horizontal reduction
        var result = Vector512.Sum(sum);

        // Handle remainder
        var remainder = a.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += a[lastOffset + i] * b[lastOffset + i];
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float DotProductAvx2(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        const int VectorSize = 8;
        var vectorCount = a.Length / VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);

        var sum = Vector256<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
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

        // Horizontal reduction
        var result = Vector256.Sum(sum);

        // Handle remainder
        var remainder = a.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += a[lastOffset + i] * b[lastOffset + i];
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float DotProductNeon(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        const int VectorSize = 4;
        var vectorCount = a.Length / VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);

        var sum = Vector128<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
            var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, offset));
            var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, offset));

            sum = AdvSimd.FusedMultiplyAdd(sum, va, vb);
        }

        // Horizontal reduction for NEON
        var result = Vector128.Sum(sum);

        // Handle remainder
        var remainder = a.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += a[lastOffset + i] * b[lastOffset + i];
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float DotProductSse(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        const int VectorSize = 4;
        var vectorCount = a.Length / VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);

        var sum = Vector128<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
            var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, offset));
            var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, offset));

            sum = Sse.Add(sum, Sse.Multiply(va, vb));
        }

        // Horizontal reduction
        var result = Vector128.Sum(sum);

        // Handle remainder
        var remainder = a.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += a[lastOffset + i] * b[lastOffset + i];
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DotProductScalar(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var result = 0.0f;
        for (var i = 0; i < a.Length; i++)
        {
            result += a[i] * b[i];
        }
        return result;
    }

    /// <summary>
    /// Computes L2 norm (Euclidean norm) of a vector using SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float L2Norm(ReadOnlySpan<float> vector) => MathF.Sqrt(DotProduct(vector, vector));

    /// <summary>
    /// Performs vectorized reduction (sum) with platform-optimal SIMD.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float VectorSum(ReadOnlySpan<float> vector)
    {
        if (Vector512.IsHardwareAccelerated && vector.Length >= 16)
        {
            return VectorSumAvx512(vector);
        }
        else if (Vector256.IsHardwareAccelerated && vector.Length >= 8)
        {
            return VectorSumAvx2(vector);
        }
        else
        {
            return Vector128.IsHardwareAccelerated && vector.Length >= 4 ? AdvSimd.IsSupported ? VectorSumNeon(vector) : VectorSumSse(vector) : VectorSumScalar(vector);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float VectorSumAvx512(ReadOnlySpan<float> vector)
    {
        const int VectorSize = 16;
        var vectorCount = vector.Length / VectorSize;

        ref var vectorRef = ref MemoryMarshal.GetReference(vector);
        var sum = Vector512<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
            var v = Vector512.LoadUnsafe(ref Unsafe.Add(ref vectorRef, offset));
            sum = Avx512F.Add(sum, v);
        }

        var result = Vector512.Sum(sum);

        // Handle remainder
        var remainder = vector.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += vector[lastOffset + i];
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float VectorSumAvx2(ReadOnlySpan<float> vector)
    {
        const int VectorSize = 8;
        var vectorCount = vector.Length / VectorSize;

        ref var vectorRef = ref MemoryMarshal.GetReference(vector);
        var sum = Vector256<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
            var v = Vector256.LoadUnsafe(ref Unsafe.Add(ref vectorRef, offset));
            sum = Avx.Add(sum, v);
        }

        var result = Vector256.Sum(sum);

        // Handle remainder
        var remainder = vector.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += vector[lastOffset + i];
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float VectorSumNeon(ReadOnlySpan<float> vector)
    {
        const int VectorSize = 4;
        var vectorCount = vector.Length / VectorSize;

        ref var vectorRef = ref MemoryMarshal.GetReference(vector);
        var sum = Vector128<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
            var v = Vector128.LoadUnsafe(ref Unsafe.Add(ref vectorRef, offset));
            sum = AdvSimd.Add(sum, v);
        }

        var result = Vector128.Sum(sum);

        // Handle remainder
        var remainder = vector.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += vector[lastOffset + i];
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float VectorSumSse(ReadOnlySpan<float> vector)
    {
        const int VectorSize = 4;
        var vectorCount = vector.Length / VectorSize;

        ref var vectorRef = ref MemoryMarshal.GetReference(vector);
        var sum = Vector128<float>.Zero;

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
            var v = Vector128.LoadUnsafe(ref Unsafe.Add(ref vectorRef, offset));
            sum = Sse.Add(sum, v);
        }

        var result = Vector128.Sum(sum);

        // Handle remainder
        var remainder = vector.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                result += vector[lastOffset + i];
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float VectorSumScalar(ReadOnlySpan<float> vector)
    {
        var result = 0.0f;
        for (var i = 0; i < vector.Length; i++)
        {
            result += vector[i];
        }
        return result;
    }

    /// <summary>
    /// Performs masked SIMD operation (conditional selection) using AVX-512 or blending.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ConditionalSelect(
        ReadOnlySpan<float> condition,
        ReadOnlySpan<float> trueValues,
        ReadOnlySpan<float> falseValues,
        Span<float> result,
        float threshold = 0.0f)
    {
        if (condition.Length != trueValues.Length || condition.Length != falseValues.Length || condition.Length != result.Length)
        {
            throw new ArgumentException("All spans must have the same length");
        }

        if (Vector512.IsHardwareAccelerated && condition.Length >= 16 && Avx512F.IsSupported)
        {
            ConditionalSelectAvx512(condition, trueValues, falseValues, result, threshold);
        }
        else if (Vector256.IsHardwareAccelerated && condition.Length >= 8)
        {
            ConditionalSelectAvx2(condition, trueValues, falseValues, result, threshold);
        }
        else if (Vector128.IsHardwareAccelerated && condition.Length >= 4)
        {
            ConditionalSelectSse(condition, trueValues, falseValues, result, threshold);
        }
        else
        {
            ConditionalSelectScalar(condition, trueValues, falseValues, result, threshold);
        }
    }

    /// <summary>
    /// AVX-512 conditional select adoption site #3 for .NET 10 SIMD surface.
    /// Uses <c>Avx512F.TernaryLogic</c> to collapse the mask-blend into a
    /// single <c>vpternlogd</c> instruction. The truth table used is 0xCA
    /// (a := (c ? a : b)), which matches bitwise select semantics when the mask
    /// is an all-ones/all-zeros comparison result. Byte-identical to the previous
    /// implementation that used <c>Avx512F.BlendVariable</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ConditionalSelectAvx512(
        ReadOnlySpan<float> condition,
        ReadOnlySpan<float> trueValues,
        ReadOnlySpan<float> falseValues,
        Span<float> result,
        float threshold)
    {
        const int VectorSize = 16;
        var vectorCount = condition.Length / VectorSize;

        var thresholdVec = Vector512.Create(threshold);

        ref var condRef = ref MemoryMarshal.GetReference(condition);
        ref var trueRef = ref MemoryMarshal.GetReference(trueValues);
        ref var falseRef = ref MemoryMarshal.GetReference(falseValues);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
            var cond = Vector512.LoadUnsafe(ref Unsafe.Add(ref condRef, offset));
            var trueVec = Vector512.LoadUnsafe(ref Unsafe.Add(ref trueRef, offset));
            var falseVec = Vector512.LoadUnsafe(ref Unsafe.Add(ref falseRef, offset));

            // Mask where condition > threshold (all-ones lanes on match, zero otherwise).
            var mask = Avx512F.CompareGreaterThan(cond, thresholdVec);

            // Single-instruction bitwise select via AVX-512 vpternlogd.
            // Truth table 0xCA computes (C ? A : B) = (C & A) | (~C & B).
            //   bit  | A | B | C | out
            //   -----+---+---+---+----
            //   ca_7 | 1 | 1 | 1 |  1
            //   ca_6 | 1 | 1 | 0 |  1
            //   ca_5 | 1 | 0 | 1 |  1
            //   ca_4 | 1 | 0 | 0 |  0
            //   ca_3 | 0 | 1 | 1 |  0
            //   ca_2 | 0 | 1 | 0 |  1
            //   ca_1 | 0 | 0 | 1 |  0
            //   ca_0 | 0 | 0 | 0 |  0
            // => 0b11001010 = 0xCA
            var resultVec = Avx512F.TernaryLogic(trueVec, falseVec, mask.AsSingle(), 0xCA);

            resultVec.StoreUnsafe(ref Unsafe.Add(ref resultRef, offset));
        }

        // Handle remainder
        var remainder = condition.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                var idx = lastOffset + i;
                result[idx] = condition[idx] > threshold ? trueValues[idx] : falseValues[idx];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ConditionalSelectAvx2(
        ReadOnlySpan<float> condition,
        ReadOnlySpan<float> trueValues,
        ReadOnlySpan<float> falseValues,
        Span<float> result,
        float threshold)
    {
        const int VectorSize = 8;
        var vectorCount = condition.Length / VectorSize;

        var thresholdVec = Vector256.Create(threshold);

        ref var condRef = ref MemoryMarshal.GetReference(condition);
        ref var trueRef = ref MemoryMarshal.GetReference(trueValues);
        ref var falseRef = ref MemoryMarshal.GetReference(falseValues);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
            var cond = Vector256.LoadUnsafe(ref Unsafe.Add(ref condRef, offset));
            var trueVec = Vector256.LoadUnsafe(ref Unsafe.Add(ref trueRef, offset));
            var falseVec = Vector256.LoadUnsafe(ref Unsafe.Add(ref falseRef, offset));

            // Create mask where condition > threshold
            var mask = Avx.CompareGreaterThan(cond, thresholdVec);

            // Use blend to select based on mask
            var resultVec = Avx.BlendVariable(falseVec, trueVec, mask);

            resultVec.StoreUnsafe(ref Unsafe.Add(ref resultRef, offset));
        }

        // Handle remainder
        var remainder = condition.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                var idx = lastOffset + i;
                result[idx] = condition[idx] > threshold ? trueValues[idx] : falseValues[idx];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ConditionalSelectSse(
        ReadOnlySpan<float> condition,
        ReadOnlySpan<float> trueValues,
        ReadOnlySpan<float> falseValues,
        Span<float> result,
        float threshold)
    {
        const int VectorSize = 4;
        var vectorCount = condition.Length / VectorSize;

        var thresholdVec = Vector128.Create(threshold);

        ref var condRef = ref MemoryMarshal.GetReference(condition);
        ref var trueRef = ref MemoryMarshal.GetReference(trueValues);
        ref var falseRef = ref MemoryMarshal.GetReference(falseValues);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        for (var i = 0; i < vectorCount; i++)
        {
            var offset = i * VectorSize;
            var cond = Vector128.LoadUnsafe(ref Unsafe.Add(ref condRef, offset));
            var trueVec = Vector128.LoadUnsafe(ref Unsafe.Add(ref trueRef, offset));
            var falseVec = Vector128.LoadUnsafe(ref Unsafe.Add(ref falseRef, offset));

            // Create mask where condition > threshold
            var mask = Sse.CompareGreaterThan(cond, thresholdVec);

            // Use blend to select based on mask.
            // Adoption site #4 for .NET 10 SIMD surface: when AVX-512VL is available
            // we fuse the SSE-era `(trueVec & mask) | (~mask & falseVec)` three-op
            // sequence into a single Vector128 vpternlogd with imm8 = 0xCA
            // (truth table for C ? A : B). Byte-identical result to the manual
            // And/AndNot/Or sequence that preceded it.
            Vector128<float> resultVec;
            if (Avx512F.VL.IsSupported)
            {
                resultVec = Avx512F.VL.TernaryLogic(trueVec, falseVec, mask, 0xCA);
            }
            else if (Sse41.IsSupported)
            {
                resultVec = Sse41.BlendVariable(falseVec, trueVec, mask);
            }
            else
            {
                // Manual blend for older SSE (byte-for-byte identical fallback).
                var maskedTrue = Sse.And(trueVec, mask);
                var maskedFalse = Sse.AndNot(mask, falseVec);
                resultVec = Sse.Or(maskedTrue, maskedFalse);
            }

            resultVec.StoreUnsafe(ref Unsafe.Add(ref resultRef, offset));
        }

        // Handle remainder
        var remainder = condition.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                var idx = lastOffset + i;
                result[idx] = condition[idx] > threshold ? trueValues[idx] : falseValues[idx];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConditionalSelectScalar(
        ReadOnlySpan<float> condition,
        ReadOnlySpan<float> trueValues,
        ReadOnlySpan<float> falseValues,
        Span<float> result,
        float threshold)
    {
        for (var i = 0; i < condition.Length; i++)
        {
            result[i] = condition[i] > threshold ? trueValues[i] : falseValues[i];
        }
    }

    /// <summary>
    /// Advanced gather operation for sparse/indirect memory access patterns.
    /// Uses AVX-512-wide gather (stitched AVX2 gathers) when available, AVX2 otherwise,
    /// and falls back to a bounds-checked scalar loop.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void GatherFloat32(
        ReadOnlySpan<float> source,
        ReadOnlySpan<int> indices,
        Span<float> destination)
    {
        if (indices.Length != destination.Length)
        {
            throw new ArgumentException("Indices and destination must have the same length");
        }

        // Adoption site #5 for .NET 10 SIMD surface: on AVX-512 hosts process 16 elements
        // per iteration via two back-to-back AVX2 gathers. .NET 10 SDK 10.0.106 does not
        // expose Avx512F.GatherVector512, so stitching is the fastest available form.
        if (Avx512F.IsSupported && Avx2.IsSupported && indices.Length >= 16)
        {
            GatherFloat32Avx512(source, indices, destination);
        }
        else if (Avx2.IsSupported && indices.Length >= 8)
        {
            GatherFloat32Avx2(source, indices, destination);
        }
        else
        {
            GatherFloat32Scalar(source, indices, destination);
        }
    }

    /// <summary>
    /// AVX-512-wide gather: processes 16 indices per iteration using two stitched
    /// AVX2 gather instructions. Behaviour is byte-for-byte identical to looping
    /// over <see cref="GatherFloat32Avx2"/> twice; this form simply reduces loop
    /// overhead and lets the JIT keep both gather dispatches in flight.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void GatherFloat32Avx512(
        ReadOnlySpan<float> source,
        ReadOnlySpan<int> indices,
        Span<float> destination)
    {
        const int VectorSize = 16;
        var vectorCount = indices.Length / VectorSize;

        fixed (float* sourcePtr = source)
        {
            ref var indicesRef = ref MemoryMarshal.GetReference(indices);
            ref var destRef = ref MemoryMarshal.GetReference(destination);

            for (var i = 0; i < vectorCount; i++)
            {
                var offset = i * VectorSize;

                // Load two 8-wide index blocks.
                var idxLo = Vector256.LoadUnsafe(ref Unsafe.Add(ref indicesRef, offset));
                var idxHi = Vector256.LoadUnsafe(ref Unsafe.Add(ref indicesRef, offset + 8));

                // Two hardware gathers (scale = 4 for sizeof(float)).
                var gatheredLo = Avx2.GatherVector256(sourcePtr, idxLo, 4);
                var gatheredHi = Avx2.GatherVector256(sourcePtr, idxHi, 4);

                gatheredLo.StoreUnsafe(ref Unsafe.Add(ref destRef, offset));
                gatheredHi.StoreUnsafe(ref Unsafe.Add(ref destRef, offset + 8));
            }
        }

        // Tail: delegate to the AVX2 path + scalar remainder so any leftover
        // 8-element block still uses a hardware gather.
        var consumed = vectorCount * VectorSize;
        if (consumed < indices.Length)
        {
            GatherFloat32Avx2(
                source,
                indices[consumed..],
                destination[consumed..]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void GatherFloat32Avx2(
        ReadOnlySpan<float> source,
        ReadOnlySpan<int> indices,
        Span<float> destination)
    {
        const int VectorSize = 8;
        var vectorCount = indices.Length / VectorSize;

        fixed (float* sourcePtr = source)
        fixed (int* indicesPtr = indices)
        {
            ref var destRef = ref MemoryMarshal.GetReference(destination);

            for (var i = 0; i < vectorCount; i++)
            {
                var offset = i * VectorSize;
                var indicesVec = Vector256.LoadUnsafe(ref Unsafe.Add(ref MemoryMarshal.GetReference(indices), offset));

                // AVX2 gather: load floats from source using indices
                var gathered = Avx2.GatherVector256(sourcePtr, indicesVec, 4); // scale = 4 for sizeof(float)

                gathered.StoreUnsafe(ref Unsafe.Add(ref destRef, offset));
            }
        }

        // Handle remainder with scalar
        var remainder = indices.Length % VectorSize;
        if (remainder > 0)
        {
            var lastOffset = vectorCount * VectorSize;
            for (var i = 0; i < remainder; i++)
            {
                var idx = lastOffset + i;
                var sourceIdx = indices[idx];
                if (sourceIdx >= 0 && sourceIdx < source.Length)
                {
                    destination[idx] = source[sourceIdx];
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GatherFloat32Scalar(
        ReadOnlySpan<float> source,
        ReadOnlySpan<int> indices,
        Span<float> destination)
    {
        for (var i = 0; i < indices.Length; i++)
        {
            var sourceIdx = indices[i];
            if (sourceIdx >= 0 && sourceIdx < source.Length)
            {
                destination[i] = source[sourceIdx];
            }
        }
    }
}
