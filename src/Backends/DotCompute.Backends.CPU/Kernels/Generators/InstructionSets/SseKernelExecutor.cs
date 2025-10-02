// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Kernels.Generators.Utilities;
using DotCompute.Backends.CPU.Kernels.Models;

namespace DotCompute.Backends.CPU.Kernels.Generators.InstructionSets;

/// <summary>
/// SSE kernel executor implementation providing vectorized operations
/// using 128-bit vector registers for legacy and compatibility scenarios.
/// </summary>
internal sealed class SseKernelExecutor(KernelDefinition definition, KernelExecutionPlan executionPlan) : SimdKernelExecutor(definition, executionPlan)
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
    }

    /// <summary>
    /// Optimized float32 execution using SSE instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat32(
        ReadOnlySpan<float> input1,
        ReadOnlySpan<float> input2,
        Span<float> output,
        long elementCount,
        KernelOperation operation)
    {
        const int vectorSize = 4; // 128 bits / 32 bits per float
        var vectorCount = elementCount / vectorSize;
        var remainder = elementCount % vectorSize;

        // Get function pointer for the specific operation
        var vectorOp = SseOperationHelpers.GetVectorOperationFloat32(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        if (vectorOp != null)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * vectorSize);
                var vec1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input1Ref, offset));
                var vec2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input2Ref, offset));
                var result = vectorOp(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, offset));
            }
        }
        else
        {
            // Handle special cases like FMA that need different treatment
            var scalarOp = ScalarOperationHelpers.GetScalarOperationFloat32(operation);
            for (long i = 0; i < elementCount; i++)
            {
                output[(int)i] = scalarOp(input1[(int)i], input2[(int)i]);
            }
            return;
        }

        // Process remaining elements
        ProcessRemainder(ref input1Ref, ref input2Ref, ref outputRef, (int)(vectorCount * vectorSize), remainder, operation);
    }

    /// <summary>
    /// Processes remaining elements using scalar operations for SSE.
    /// </summary>
    private static unsafe void ProcessRemainder(
        ref float input1Ref,
        ref float input2Ref,
        ref float outputRef,
        int offset,
        long remainder,
        KernelOperation operation)
    {
        // Handle final scalar elements
        if (remainder > 0)
        {
            var scalarOp = ScalarOperationHelpers.GetScalarOperationFloat32(operation);
            for (long i = 0; i < remainder; i++)
            {
                var idx = offset + i;
                Unsafe.Add(ref outputRef, (int)idx) = scalarOp(
                    Unsafe.Add(ref input1Ref, (int)idx),
                    Unsafe.Add(ref input2Ref, (int)idx));
            }
        }
    }

    /// <summary>
    /// Optimized double precision execution using SSE2.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat64(
        ReadOnlySpan<double> input1,
        ReadOnlySpan<double> input2,
        Span<double> output,
        long elementCount,
        KernelOperation operation)
    {
        const int vectorSize = 2; // 128 bits / 64 bits per double
        var vectorCount = elementCount / vectorSize;
        var remainder = elementCount % vectorSize;

        // Get function pointer for the specific operation
        var vectorOp = SseOperationHelpers.GetVectorOperationFloat64(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        if (vectorOp != null)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * vectorSize);
                var vec1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input1Ref, offset));
                var vec2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input2Ref, offset));
                var result = vectorOp(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, offset));
            }
        }
        else
        {
            // Handle special cases like FMA that need different treatment
            var scalarOp = ScalarOperationHelpers.GetScalarOperationFloat64(operation);
            for (long i = 0; i < elementCount; i++)
            {
                output[(int)i] = scalarOp(input1[(int)i], input2[(int)i]);
            }
            return;
        }

        // Process remaining elements
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * vectorSize);
            var scalarOp = ScalarOperationHelpers.GetScalarOperationFloat64(operation);

            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                output[(int)idx] = scalarOp(input1[(int)idx], input2[(int)idx]);
            }
        }
    }
}