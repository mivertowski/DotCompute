// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Kernels.Generators.Utilities;

/// <summary>
/// Helper methods for AVX-512 SIMD operations with optimized function pointer mappings.
/// </summary>
internal static class Avx512OperationHelpers
{
    /// <summary>
    /// Gets the appropriate AVX-512 vector function for float32 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the AVX-512 vector operation, or null if not supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector512<float>, Vector512<float>, Vector512<float>> GetVectorOperationFloat32(KernelOperation operation)
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

    /// <summary>
    /// Gets the appropriate AVX-512 vector function for float64 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the AVX-512 vector operation, or null if not supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector512<double>, Vector512<double>, Vector512<double>> GetVectorOperationFloat64(KernelOperation operation)
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

    /// <summary>
    /// Gets the appropriate AVX-512 vector function for int32 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the AVX-512 vector operation, or null if not supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector512<int>, Vector512<int>, Vector512<int>> GetVectorOperationInt32(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Avx512F.Add,
            KernelOperation.Multiply => &Avx512F.MultiplyLow, // Use low part for 32-bit multiply
            KernelOperation.Subtract => &Avx512F.Subtract,
            KernelOperation.Maximum => &Avx512F.Max,
            KernelOperation.Minimum => &Avx512F.Min,
            // Division not directly supported in AVX-512 for integers
            _ => &Avx512F.Add
        };
    }

    /// <summary>
    /// Checks if the given operation is supported by AVX-512 for the specified type.
    /// </summary>
    /// <param name="operation">The kernel operation.</param>
    /// <param name="elementType">The element type.</param>
    /// <returns>True if supported, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOperationSupported(KernelOperation operation, Type elementType)
    {
        if (!Avx512F.IsSupported)
        {
            return false;
        }


        return operation switch
        {
            KernelOperation.Add or KernelOperation.Subtract or KernelOperation.Multiply => true,
            KernelOperation.Divide => elementType == typeof(float) || elementType == typeof(double),
            KernelOperation.Maximum or KernelOperation.Minimum => true,
            KernelOperation.FusedMultiplyAdd => elementType == typeof(float) || elementType == typeof(double),
            _ => false
        };
    }

    /// <summary>
    /// Performs a vectorized FMA operation using AVX-512 instructions.
    /// </summary>
    /// <param name="a">First vector operand.</param>
    /// <param name="b">Second vector operand.</param>
    /// <param name="c">Third vector operand.</param>
    /// <returns>The result of a * b + c.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> FusedMultiplyAdd(Vector512<float> a, Vector512<float> b, Vector512<float> c) => Avx512F.FusedMultiplyAdd(a, b, c);

    /// <summary>
    /// Performs a vectorized FMA operation using AVX-512 instructions for double precision.
    /// </summary>
    /// <param name="a">First vector operand.</param>
    /// <param name="b">Second vector operand.</param>
    /// <param name="c">Third vector operand.</param>
    /// <returns>The result of a * b + c.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<double> FusedMultiplyAdd(Vector512<double> a, Vector512<double> b, Vector512<double> c) => Avx512F.FusedMultiplyAdd(a, b, c);
}
