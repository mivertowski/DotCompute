// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Kernels.Generators.Utilities;

/// <summary>
/// Helper methods for AVX2 SIMD operations with optimized function pointer mappings.
/// </summary>
internal static class Avx2OperationHelpers
{
    /// <summary>
    /// Gets the appropriate AVX2 vector function for float32 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the AVX2 vector operation, or null if not supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector256<float>, Vector256<float>, Vector256<float>> GetVectorOperationFloat32(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Avx.Add,
            KernelOperation.Multiply => &Avx.Multiply,
            KernelOperation.Subtract => &Avx.Subtract,
            KernelOperation.Divide => &Avx.Divide,
            KernelOperation.Maximum => &Avx.Max,
            KernelOperation.Minimum => &Avx.Min,
            // FusedMultiplyAdd handled separately - requires 3 operands
            KernelOperation.FusedMultiplyAdd => null,
            _ => &Avx.Add
        };
    }

    /// <summary>
    /// Gets the appropriate AVX2 vector function for float64 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the AVX2 vector operation, or null if not supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector256<double>, Vector256<double>, Vector256<double>> GetVectorOperationFloat64(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Avx.Add,
            KernelOperation.Multiply => &Avx.Multiply,
            KernelOperation.Subtract => &Avx.Subtract,
            KernelOperation.Divide => &Avx.Divide,
            KernelOperation.Maximum => &Avx.Max,
            KernelOperation.Minimum => &Avx.Min,
            // FusedMultiplyAdd handled separately - requires 3 operands  
            KernelOperation.FusedMultiplyAdd => null,
            _ => &Avx.Add
        };
    }

    /// <summary>
    /// Gets the appropriate AVX2 vector function for int32 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the AVX2 vector operation, or null if not supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector256<int>, Vector256<int>, Vector256<int>> GetVectorOperationInt32(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Avx2.Add,
            KernelOperation.Multiply => &Avx2.MultiplyLow,
            KernelOperation.Subtract => &Avx2.Subtract,
            KernelOperation.Maximum => &Avx2.Max,
            KernelOperation.Minimum => &Avx2.Min,
            // Division not directly supported in AVX2 for integers
            _ => &Avx2.Add
        };
    }

    /// <summary>
    /// Checks if the given operation is supported by AVX2 for the specified type.
    /// </summary>
    /// <param name="operation">The kernel operation.</param>
    /// <param name="elementType">The element type.</param>
    /// <returns>True if supported, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOperationSupported(KernelOperation operation, Type elementType)
    {
        if (!Avx2.IsSupported) return false;

        return operation switch
        {
            KernelOperation.Add or KernelOperation.Subtract or KernelOperation.Multiply => true,
            KernelOperation.Divide => elementType == typeof(float) || elementType == typeof(double),
            KernelOperation.Maximum or KernelOperation.Minimum => true,
            KernelOperation.FusedMultiplyAdd => Fma.IsSupported && (elementType == typeof(float) || elementType == typeof(double)),
            _ => false
        };
    }

    /// <summary>
    /// Performs a vectorized FMA operation using FMA instructions if available.
    /// </summary>
    /// <param name="a">First vector operand.</param>
    /// <param name="b">Second vector operand.</param>
    /// <param name="c">Third vector operand.</param>
    /// <returns>The result of a * b + c.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> FusedMultiplyAdd(Vector256<float> a, Vector256<float> b, Vector256<float> c)
    {
        return Fma.IsSupported ? Fma.MultiplyAdd(a, b, c) : Avx.Add(Avx.Multiply(a, b), c);
    }

    /// <summary>
    /// Performs a vectorized FMA operation using FMA instructions if available for double precision.
    /// </summary>
    /// <param name="a">First vector operand.</param>
    /// <param name="b">Second vector operand.</param>
    /// <param name="c">Third vector operand.</param>
    /// <returns>The result of a * b + c.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<double> FusedMultiplyAdd(Vector256<double> a, Vector256<double> b, Vector256<double> c)
    {
        return Fma.IsSupported ? Fma.MultiplyAdd(a, b, c) : Avx.Add(Avx.Multiply(a, b), c);
    }
}