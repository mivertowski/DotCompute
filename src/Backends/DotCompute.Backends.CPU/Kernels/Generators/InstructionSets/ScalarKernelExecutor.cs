// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Kernels.Generators.Utilities;
using DotCompute.Backends.CPU.Kernels.Models;

namespace DotCompute.Backends.CPU.Kernels.Generators.InstructionSets;

/// <summary>
/// Scalar kernel executor implementation providing fallback execution
/// when SIMD instruction sets are not available or applicable.
/// </summary>
internal sealed class ScalarKernelExecutor(KernelDefinition definition, KernelExecutionPlan executionPlan) : SimdKernelExecutor(definition, executionPlan)
{
    public override void Execute(
        ReadOnlySpan<byte> input1,
        ReadOnlySpan<byte> input2,
        Span<byte> output,
        long elementCount,
        int vectorWidth)
    {
        var operation = GetOperationType();
        var elementType = GetElementType();

        if (elementType == typeof(float))
        {
            ExecuteFloat32(
                MemoryMarshal.Cast<byte, float>(input1),
                MemoryMarshal.Cast<byte, float>(input2),
                MemoryMarshal.Cast<byte, float>(output),
                elementCount,
                operation);
        }
        else if (elementType == typeof(double))
        {
            ExecuteFloat64(
                MemoryMarshal.Cast<byte, double>(input1),
                MemoryMarshal.Cast<byte, double>(input2),
                MemoryMarshal.Cast<byte, double>(output),
                elementCount,
                operation);
        }
        else if (elementType == typeof(int))
        {
            ExecuteInt32(
                MemoryMarshal.Cast<byte, int>(input1),
                MemoryMarshal.Cast<byte, int>(input2),
                MemoryMarshal.Cast<byte, int>(output),
                elementCount,
                operation);
        }
    }

    /// <summary>
    /// Scalar float32 execution with loop optimizations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteFloat32(
        ReadOnlySpan<float> input1,
        ReadOnlySpan<float> input2,
        Span<float> output,
        long elementCount,
        KernelOperation operation)
    {
        var scalarOp = ScalarOperationHelpers.GetScalarOperationFloat32(operation);

        // Use simple loop with compiler optimizations
        for (long i = 0; i < elementCount; i++)
        {
            output[(int)i] = scalarOp(input1[(int)i], input2[(int)i]);
        }
    }

    /// <summary>
    /// Scalar double precision execution with loop optimizations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteFloat64(
        ReadOnlySpan<double> input1,
        ReadOnlySpan<double> input2,
        Span<double> output,
        long elementCount,
        KernelOperation operation)
    {
        var scalarOp = ScalarOperationHelpers.GetScalarOperationFloat64(operation);

        // Use simple loop with compiler optimizations
        for (long i = 0; i < elementCount; i++)
        {
            output[(int)i] = scalarOp(input1[(int)i], input2[(int)i]);
        }
    }

    /// <summary>
    /// Scalar int32 execution with loop optimizations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInt32(
        ReadOnlySpan<int> input1,
        ReadOnlySpan<int> input2,
        Span<int> output,
        long elementCount,
        KernelOperation operation)
    {
        var scalarOp = ScalarOperationHelpers.GetScalarOperationInt32(operation);

        // Use simple loop with compiler optimizations
        for (long i = 0; i < elementCount; i++)
        {
            output[(int)i] = scalarOp(input1[(int)i], input2[(int)i]);
        }
    }

    /// <summary>
    /// Executes 3-operand FMA operation: result = a * b + c using scalar arithmetic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ExecuteFloat32FMA(
        ReadOnlySpan<float> a,
        ReadOnlySpan<float> b,
        ReadOnlySpan<float> c,
        Span<float> result)
    {
        if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
        {
            throw new ArgumentException("All spans must have the same length");
        }

        // Use scalar FMA operations
        for (var i = 0; i < a.Length; i++)
        {
            result[i] = ScalarOperationHelpers.ScalarFMAFloat32(a[i], b[i], c[i]);
        }
    }

    /// <summary>
    /// Executes 3-operand FMA operation: result = a * b + c using scalar arithmetic for double precision.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ExecuteFloat64FMA(
        ReadOnlySpan<double> a,
        ReadOnlySpan<double> b,
        ReadOnlySpan<double> c,
        Span<double> result)
    {
        if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
        {
            throw new ArgumentException("All spans must have the same length");
        }

        // Use scalar FMA operations
        for (var i = 0; i < a.Length; i++)
        {
            result[i] = ScalarOperationHelpers.ScalarFMAFloat64(a[i], b[i], c[i]);
        }
    }
}