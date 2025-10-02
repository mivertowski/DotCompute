// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using DotCompute.Backends.CPU.Kernels.Generators;

namespace DotCompute.Backends.CPU.Kernels;


/// <summary>
/// Helper methods for SIMD operations across different vector sizes.
/// </summary>
internal static class SimdOperationHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector512<float>, Vector512<float>, Vector512<float>> GetVectorOperationFloat32_512(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Avx512F.Add,
            KernelOperation.Multiply => &Avx512F.Multiply,
            // FusedMultiplyAdd handled separately - requires 3 operands
            KernelOperation.FusedMultiplyAdd => null,
            KernelOperation.Subtract => &Avx512F.Subtract,
            KernelOperation.Divide => &Avx512F.Divide,
            KernelOperation.Maximum => &Avx512F.Max,
            KernelOperation.Minimum => &Avx512F.Min,
            _ => &Avx512F.Add
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector256<float>, Vector256<float>, Vector256<float>> GetVectorOperationFloat32_256(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Avx.Add,
            KernelOperation.Multiply => &Avx.Multiply,
            KernelOperation.Subtract => &Avx.Subtract,
            KernelOperation.Divide => &Avx.Divide,
            KernelOperation.Maximum => &Avx.Max,
            KernelOperation.Minimum => &Avx.Min,
            _ => &Avx.Add
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector128<float>, Vector128<float>, Vector128<float>> GetVectorOperationFloat32_128(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Sse.Add,
            KernelOperation.Multiply => &Sse.Multiply,
            KernelOperation.Subtract => &Sse.Subtract,
            KernelOperation.Divide => &Sse.Divide,
            KernelOperation.Maximum => &Sse.Max,
            KernelOperation.Minimum => &Sse.Min,
            _ => &Sse.Add
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector512<double>, Vector512<double>, Vector512<double>> GetVectorOperationFloat64_512(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Avx512F.Add,
            KernelOperation.Multiply => &Avx512F.Multiply,
            // FusedMultiplyAdd handled separately - requires 3 operands
            KernelOperation.FusedMultiplyAdd => null,
            KernelOperation.Subtract => &Avx512F.Subtract,
            KernelOperation.Divide => &Avx512F.Divide,
            KernelOperation.Maximum => &Avx512F.Max,
            KernelOperation.Minimum => &Avx512F.Min,
            _ => &Avx512F.Add
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector256<double>, Vector256<double>, Vector256<double>> GetVectorOperationFloat64_256(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Avx.Add,
            KernelOperation.Multiply => &Avx.Multiply,
            KernelOperation.Subtract => &Avx.Subtract,
            KernelOperation.Divide => &Avx.Divide,
            KernelOperation.Maximum => &Avx.Max,
            KernelOperation.Minimum => &Avx.Min,
            _ => &Avx.Add
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector128<double>, Vector128<double>, Vector128<double>> GetVectorOperationFloat64_128(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Sse2.Add,
            KernelOperation.Multiply => &Sse2.Multiply,
            KernelOperation.Subtract => &Sse2.Subtract,
            KernelOperation.Divide => &Sse2.Divide,
            KernelOperation.Maximum => &Sse2.Max,
            KernelOperation.Minimum => &Sse2.Min,
            _ => &Sse2.Add
        };
    }
}
