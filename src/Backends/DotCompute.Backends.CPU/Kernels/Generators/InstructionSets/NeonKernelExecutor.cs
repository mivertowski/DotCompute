// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Kernels.Generators.Utilities;
using DotCompute.Backends.CPU.Kernels.Models;

namespace DotCompute.Backends.CPU.Kernels.Generators.InstructionSets;

/// <summary>
/// ARM NEON kernel executor implementation for cross-platform support
/// providing high-performance vectorized operations on ARM-based processors.
/// </summary>
internal sealed class NeonKernelExecutor(KernelDefinition definition, KernelExecutionPlan executionPlan) : SimdKernelExecutor(definition, executionPlan)
{
    /// <summary>
    /// Performs execute.
    /// </summary>
    /// <param name="input1">The input1.</param>
    /// <param name="input2">The input2.</param>
    /// <param name="output">The output.</param>
    /// <param name="elementCount">The element count.</param>
    /// <param name="vectorWidth">The vector width.</param>
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
    /// Optimized float32 execution using ARM NEON instructions.
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
        var vectorOp = NeonOperationHelpers.GetVectorOperationFloat32(operation);

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
            // Handle special cases like divide or FMA that need different treatment
            var scalarOp = ScalarOperationHelpers.GetScalarOperationFloat32(operation);
            for (long i = 0; i < elementCount; i++)
            {
                output[(int)i] = scalarOp(input1[(int)i], input2[(int)i]);
            }
            return;
        }

        // Process remaining elements with scalar operations
        ProcessRemainder(ref input1Ref, ref input2Ref, ref outputRef, (int)(vectorCount * vectorSize), remainder, operation);
    }

    /// <summary>
    /// Processes remaining elements using scalar operations for ARM NEON.
    /// </summary>
    private static unsafe void ProcessRemainder(
        ref float input1Ref,
        ref float input2Ref,
        ref float outputRef,
        int offset,
        long remainder,
        KernelOperation operation)
    {
        // ARM NEON doesn't benefit from fallback to x86 SIMD, so use scalar for remainder
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
    /// Optimized double precision execution using ARM NEON (ARM64 only).
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
        var vectorOp = NeonOperationHelpers.GetVectorOperationFloat64(operation);

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

    /// <summary>
    /// Executes 3-operand FMA operation: result = a * b + c using ARM NEON FMLA instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void ExecuteFloat32FMA(
        ReadOnlySpan<float> a,
        ReadOnlySpan<float> b,
        ReadOnlySpan<float> c,
        Span<float> result)
    {
        if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
        {
            throw new ArgumentException("All spans must have the same length");
        }

        const int vectorSize = 4; // 128 bits / 32 bits per float
        var vectorCount = a.Length / vectorSize;
        var remainder = a.Length % vectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var cRef = ref MemoryMarshal.GetReference(c);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        // Use ARM NEON FMLA instructions for maximum performance
        if (AdvSimd.IsSupported)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * vectorSize);
                var vecA = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, offset));
                var vecB = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, offset));
                var vecC = Vector128.LoadUnsafe(ref Unsafe.Add(ref cRef, offset));

                // ARM NEON FMLA: result = a * b + c with single rounding
                var fmaResult = NeonOperationHelpers.FusedMultiplyAdd(vecA, vecB, vecC);
                fmaResult.StoreUnsafe(ref Unsafe.Add(ref resultRef, offset));
            }
        }
        else
        {
            // Fallback to scalar if ARM NEON not available
            vectorCount = 0;
            remainder = a.Length;
        }

        // Handle remainder with scalar FMA
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * vectorSize);
            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                result[(int)idx] = MathF.FusedMultiplyAdd(a[(int)idx], b[(int)idx], c[(int)idx]);
            }
        }
    }

    /// <summary>
    /// Executes 3-operand FMA operation: result = a * b + c using ARM NEON FMLA instructions for double precision.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void ExecuteFloat64FMA(
        ReadOnlySpan<double> a,
        ReadOnlySpan<double> b,
        ReadOnlySpan<double> c,
        Span<double> result)
    {
        if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
        {
            throw new ArgumentException("All spans must have the same length");
        }

        const int vectorSize = 2; // 128 bits / 64 bits per double
        var vectorCount = a.Length / vectorSize;
        var remainder = a.Length % vectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var cRef = ref MemoryMarshal.GetReference(c);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        // Use ARM NEON FMLA instructions for maximum performance (ARM64 only for double)
        if (AdvSimd.Arm64.IsSupported)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * vectorSize);
                var vecA = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, offset));
                var vecB = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, offset));
                var vecC = Vector128.LoadUnsafe(ref Unsafe.Add(ref cRef, offset));

                // ARM NEON FMLA: result = a * b + c with single rounding
                var fmaResult = NeonOperationHelpers.FusedMultiplyAdd(vecA, vecB, vecC);
                fmaResult.StoreUnsafe(ref Unsafe.Add(ref resultRef, offset));
            }
        }
        else
        {
            // Fallback to scalar if ARM64 NEON not available
            vectorCount = 0;
            remainder = a.Length;
        }

        // Handle remainder with scalar FMA
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * vectorSize);
            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                result[(int)idx] = Math.FusedMultiplyAdd(a[(int)idx], b[(int)idx], c[(int)idx]);
            }
        }
    }
}