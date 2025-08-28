// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Kernels.Generators.Utilities;

/// <summary>
/// Helper methods for SSE SIMD operations with optimized function pointer mappings.
/// </summary>
internal static class SseOperationHelpers
{
    /// <summary>
    /// Gets the appropriate SSE vector function for float32 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the SSE vector operation, or null if not supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector128<float>, Vector128<float>, Vector128<float>> GetVectorOperationFloat32(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Sse.Add,
            KernelOperation.Multiply => &Sse.Multiply,
            KernelOperation.Subtract => &Sse.Subtract,
            KernelOperation.Divide => &Sse.Divide,
            KernelOperation.Maximum => &Sse.Max,
            KernelOperation.Minimum => &Sse.Min,
            // FusedMultiplyAdd not directly supported in SSE
            KernelOperation.FusedMultiplyAdd => null,
            _ => &Sse.Add
        };
    }

    /// <summary>
    /// Gets the appropriate SSE vector function for float64 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the SSE vector operation, or null if not supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector128<double>, Vector128<double>, Vector128<double>> GetVectorOperationFloat64(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Sse2.Add,
            KernelOperation.Multiply => &Sse2.Multiply,
            KernelOperation.Subtract => &Sse2.Subtract,
            KernelOperation.Divide => &Sse2.Divide,
            KernelOperation.Maximum => &Sse2.Max,
            KernelOperation.Minimum => &Sse2.Min,
            // FusedMultiplyAdd not directly supported in SSE2
            KernelOperation.FusedMultiplyAdd => null,
            _ => &Sse2.Add
        };
    }

    /// <summary>
    /// Gets the appropriate SSE vector function for int32 operations.
    /// </summary>
    /// <param name="operation">The kernel operation type.</param>
    /// <returns>A function pointer for the SSE vector operation, or null if not supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe delegate*<Vector128<int>, Vector128<int>, Vector128<int>> GetVectorOperationInt32(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Sse2.Add,
            KernelOperation.Subtract => &Sse2.Subtract,
            // SSE2 doesn't have direct integer min/max, would need SSE4.1
            KernelOperation.Maximum => Sse41.IsSupported ? &Sse41.Max : null,
            KernelOperation.Minimum => Sse41.IsSupported ? &Sse41.Min : null,
            // Multiply and divide not directly supported for 32-bit integers in SSE2
            _ => &Sse2.Add
        };
    }

    /// <summary>
    /// Checks if the given operation is supported by SSE for the specified type.
    /// </summary>
    /// <param name="operation">The kernel operation.</param>
    /// <param name="elementType">The element type.</param>
    /// <returns>True if supported, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOperationSupported(KernelOperation operation, Type elementType)
    {
        if (!Sse.IsSupported && !Sse2.IsSupported)
        {
            return false;
        }


        return operation switch
        {
            KernelOperation.Add or KernelOperation.Subtract => true,
            KernelOperation.Multiply => elementType == typeof(float) || elementType == typeof(double),
            KernelOperation.Divide => elementType == typeof(float) || elementType == typeof(double),
            KernelOperation.Maximum or KernelOperation.Minimum => 
                elementType == typeof(float) || elementType == typeof(double) ||
                (elementType == typeof(int) && Sse41.IsSupported),
            KernelOperation.FusedMultiplyAdd => false, // Not supported in SSE
            _ => false
        };
    }

    /// <summary>
    /// Performs a simulated FMA operation using separate multiply and add for SSE.
    /// </summary>
    /// <param name="a">First vector operand.</param>
    /// <param name="b">Second vector operand.</param>
    /// <param name="c">Third vector operand.</param>
    /// <returns>The result of a * b + c.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> FusedMultiplyAdd(Vector128<float> a, Vector128<float> b, Vector128<float> c) => Sse.Add(Sse.Multiply(a, b), c);

    /// <summary>
    /// Performs a simulated FMA operation using separate multiply and add for SSE2 double precision.
    /// </summary>
    /// <param name="a">First vector operand.</param>
    /// <param name="b">Second vector operand.</param>
    /// <param name="c">Third vector operand.</param>
    /// <returns>The result of a * b + c.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> FusedMultiplyAdd(Vector128<double> a, Vector128<double> b, Vector128<double> c) => Sse2.Add(Sse2.Multiply(a, b), c);
}