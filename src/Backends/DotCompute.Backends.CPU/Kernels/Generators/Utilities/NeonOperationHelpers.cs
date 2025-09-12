// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.Arm;

namespace DotCompute.Backends.CPU.Kernels.Generators.Utilities;

/// <summary>
/// Helper methods for ARM NEON SIMD operations with optimized function pointer mappings.
/// </summary>
internal static class NeonOperationHelpers
{
    /// <summary>
    /// Gets the appropriate ARM NEON vector function for float32 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the ARM NEON vector operation, or null if not supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector128<float>, Vector128<float>, Vector128<float>> GetVectorOperationFloat32(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &AdvSimd.Add,
            KernelOperation.Multiply => &AdvSimd.Multiply,
            KernelOperation.Subtract => &AdvSimd.Subtract,
            KernelOperation.Maximum => &AdvSimd.Max,
            KernelOperation.Minimum => &AdvSimd.Min,
            // FusedMultiplyAdd uses ARM NEON FMLA instructions - handled separately
            KernelOperation.FusedMultiplyAdd => null,
            // AdvSimd doesn't have divide, use scalar fallback
            KernelOperation.Divide => null,
            _ => &AdvSimd.Add
        };
    }

    /// <summary>
    /// Gets the appropriate ARM NEON vector function for float64 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the ARM NEON vector operation, or null if not supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector128<double>, Vector128<double>, Vector128<double>> GetVectorOperationFloat64(KernelOperation operation)
    {
        // ARM NEON has limited double precision support, use available operations
        return operation switch
        {
            KernelOperation.Add => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Add : &FallbackAdd,
            KernelOperation.Multiply => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Multiply : &FallbackMultiply,
            KernelOperation.Subtract => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Subtract : &FallbackSubtract,
            KernelOperation.Divide => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Divide : &FallbackDivide,
            KernelOperation.Maximum => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Max : &FallbackMax,
            KernelOperation.Minimum => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Min : &FallbackMin,
            // FusedMultiplyAdd uses ARM NEON FMLA instructions - handled separately
            KernelOperation.FusedMultiplyAdd => null,
            _ => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Add : &FallbackAdd
        };
    }

    /// <summary>
    /// Checks if the given operation is supported by ARM NEON for the specified type.
    /// </summary>
    /// <param name="operation">The kernel operation.</param>
    /// <param name="elementType">The element type.</param>
    /// <returns>True if supported, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOperationSupported(KernelOperation operation, Type elementType)
    {
        if (!AdvSimd.IsSupported)
        {
            return false;
        }


        return operation switch
        {
            KernelOperation.Add or KernelOperation.Subtract or KernelOperation.Multiply => true,
            KernelOperation.Divide => elementType == typeof(double) ? AdvSimd.Arm64.IsSupported : false,
            KernelOperation.Maximum or KernelOperation.Minimum => true,
            KernelOperation.FusedMultiplyAdd => elementType == typeof(float) || (elementType == typeof(double) && AdvSimd.Arm64.IsSupported),
            _ => false
        };
    }

    /// <summary>
    /// Performs a vectorized FMA operation using ARM NEON FMLA instructions.
    /// </summary>
    /// <param name="a">First vector operand.</param>
    /// <param name="b">Second vector operand.</param>
    /// <param name="c">Third vector operand (accumulator).</param>
    /// <returns>The result of a * b + c.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> FusedMultiplyAdd(Vector128<float> a, Vector128<float> b, Vector128<float> c)
        // ARM NEON FMLA: result = a * b + c with single rounding
        // Note: ARM order is (accumulator, multiplicand, multiplier)


        => AdvSimd.FusedMultiplyAdd(c, a, b);

    /// <summary>
    /// Performs a vectorized FMA operation using ARM NEON FMLA instructions for double precision.
    /// </summary>
    /// <param name="a">First vector operand.</param>
    /// <param name="b">Second vector operand.</param>
    /// <param name="c">Third vector operand (accumulator).</param>
    /// <returns>The result of a * b + c.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> FusedMultiplyAdd(Vector128<double> a, Vector128<double> b, Vector128<double> c)
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            // ARM NEON FMLA: result = a * b + c with single rounding
            // Note: ARM order is (accumulator, multiplicand, multiplier)
            return AdvSimd.Arm64.FusedMultiplyAdd(c, a, b);
        }
        else
        {
            return a * b + c; // Fallback to separate operations
        }
    }

    // Fallback implementations for Vector128<double> operations when ARM64 NEON is not available
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> FallbackAdd(Vector128<double> left, Vector128<double> right) => left + right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> FallbackMultiply(Vector128<double> left, Vector128<double> right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> FallbackSubtract(Vector128<double> left, Vector128<double> right) => left - right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> FallbackDivide(Vector128<double> left, Vector128<double> right) => left / right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> FallbackMax(Vector128<double> left, Vector128<double> right) => Vector128.Max(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> FallbackMin(Vector128<double> left, Vector128<double> right) => Vector128.Min(left, right);
}