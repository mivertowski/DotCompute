// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;

namespace DotCompute.Backends.CPU.Kernels.Generators.Utilities;

/// <summary>
/// Helper methods for scalar fallback operations when SIMD is not available or applicable.
/// </summary>
internal static class ScalarOperationHelpers
{
    /// <summary>
    /// Gets the appropriate scalar function for float32 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the scalar operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<float, float, float> GetScalarOperationFloat32(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => (a, b) => a + b,
            KernelOperation.Multiply => (a, b) => a * b,
            KernelOperation.FusedMultiplyAdd => (a, b) => a * b, // For two-operand FMA, this is just multiplication
            KernelOperation.Subtract => (a, b) => a - b,
            KernelOperation.Divide => (a, b) => a / b,
            KernelOperation.Maximum => Math.Max,
            KernelOperation.Minimum => Math.Min,
            _ => (a, b) => a + b
        };
    }

    /// <summary>
    /// Gets the appropriate scalar function for float64 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the scalar operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<double, double, double> GetScalarOperationFloat64(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => (a, b) => a + b,
            KernelOperation.Multiply => (a, b) => a * b,
            KernelOperation.FusedMultiplyAdd => (a, b) => a * b, // For two-operand FMA, this is just multiplication
            KernelOperation.Subtract => (a, b) => a - b,
            KernelOperation.Divide => (a, b) => a / b,
            KernelOperation.Maximum => Math.Max,
            KernelOperation.Minimum => Math.Min,
            _ => (a, b) => a + b
        };
    }

    /// <summary>
    /// Gets the appropriate scalar function for int32 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the scalar operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<int, int, int> GetScalarOperationInt32(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => (a, b) => a + b,
            KernelOperation.Multiply => (a, b) => a * b,
            KernelOperation.Subtract => (a, b) => a - b,
            KernelOperation.Divide => (a, b) => a / b,
            KernelOperation.Maximum => Math.Max,
            KernelOperation.Minimum => Math.Min,
            _ => (a, b) => a + b
        };
    }

    /// <summary>
    /// Performs a three-operand FMA operation using scalar arithmetic.
    /// </summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <param name="c">Third operand.</param>
    /// <returns>The result of a * b + c.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScalarFMAFloat32(float a, float b, float c) => MathF.FusedMultiplyAdd(a, b, c);

    /// <summary>
    /// Performs a three-operand FMA operation using scalar arithmetic for double precision.
    /// </summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <param name="c">Third operand.</param>
    /// <returns>The result of a * b + c.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ScalarFMAFloat64(double a, double b, double c) => Math.FusedMultiplyAdd(a, b, c);
}