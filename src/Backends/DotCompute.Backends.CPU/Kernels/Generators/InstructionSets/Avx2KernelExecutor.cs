// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Kernels.Generators.Utilities;
using DotCompute.Backends.CPU.Kernels.Models;

namespace DotCompute.Backends.CPU.Kernels.Generators.InstructionSets;

/// <summary>
/// AVX2 kernel executor implementation providing high-performance vectorized operations
/// using 256-bit vector registers with FMA support when available.
/// </summary>
internal sealed class Avx2KernelExecutor(KernelDefinition definition, KernelExecutionPlan executionPlan) : SimdKernelExecutor(definition, executionPlan)
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
    /// Optimized float32 execution with loop unrolling for AVX2.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat32(
        ReadOnlySpan<float> input1,
        ReadOnlySpan<float> input2,
        Span<float> output,
        long elementCount,
        KernelOperation operation)
    {
        const int vectorSize = 8; // 256 bits / 32 bits per float
        const int unrollFactor = 2; // Process 2 vectors at a time for better pipelining
        var vectorCount = elementCount / vectorSize;
        var remainder = elementCount % vectorSize;

        // Get function pointer for the specific operation
        var vectorOp = Avx2OperationHelpers.GetVectorOperationFloat32(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        if (vectorOp != null)
        {
            // Process unrolled vectors for better instruction-level parallelism
            var unrolledCount = vectorCount / unrollFactor;
            for (long i = 0; i < unrolledCount; i++)
            {
                var baseOffset = (int)(i * vectorSize * unrollFactor);

                // Load both vector pairs
                var vec1_0 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, baseOffset));
                var vec2_0 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, baseOffset));
                var vec1_1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, baseOffset + vectorSize));
                var vec2_1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, baseOffset + vectorSize));

                // Compute both results
                var result0 = vectorOp(vec1_0, vec2_0);
                var result1 = vectorOp(vec1_1, vec2_1);

                // Store both results
                result0.StoreUnsafe(ref Unsafe.Add(ref outputRef, baseOffset));
                result1.StoreUnsafe(ref Unsafe.Add(ref outputRef, baseOffset + vectorSize));
            }

            // Process remaining full vectors
            for (var i = unrolledCount * unrollFactor; i < vectorCount; i++)
            {
                var offset = (int)(i * vectorSize);
                var vec1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, offset));
                var vec2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, offset));
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

        // Process remaining elements with optimized fallback to smaller SIMD operations
        ProcessRemainder(ref input1Ref, ref input2Ref, ref outputRef, (int)(vectorCount * vectorSize), remainder, operation);
    }

    /// <summary>
    /// Processes remaining elements using smaller SIMD operations when possible.
    /// </summary>
    private static unsafe void ProcessRemainder(
        ref float input1Ref,
        ref float input2Ref,
        ref float outputRef,
        int offset,
        long remainder,
        KernelOperation operation)
    {
        // Try to use smaller SIMD operations for remainder if possible
        if (remainder >= 8 && Avx2.IsSupported)
        {
            // Use AVX2 for 8-element remainder
            var vec1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, offset));
            var vec2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, offset));
            var result = Avx2OperationHelpers.GetVectorOperationFloat32(operation)(vec1, vec2);
            result.StoreUnsafe(ref Unsafe.Add(ref outputRef, offset));
            offset += 8;
            remainder -= 8;
        }

        if (remainder >= 4 && Sse.IsSupported)
        {
            // Use SSE for 4-element remainder
            var vec1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input1Ref, offset));
            var vec2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input2Ref, offset));
            var result = SseOperationHelpers.GetVectorOperationFloat32(operation)(vec1, vec2);
            result.StoreUnsafe(ref Unsafe.Add(ref outputRef, offset));
            offset += 4;
            remainder -= 4;
        }

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
    /// Optimized double precision execution using AVX2.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat64(
        ReadOnlySpan<double> input1,
        ReadOnlySpan<double> input2,
        Span<double> output,
        long elementCount,
        KernelOperation operation)
    {
        const int vectorSize = 4; // 256 bits / 64 bits per double
        var vectorCount = elementCount / vectorSize;
        var remainder = elementCount % vectorSize;

        // Get function pointer for the specific operation
        var vectorOp = Avx2OperationHelpers.GetVectorOperationFloat64(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        if (vectorOp != null)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * vectorSize);
                var vec1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, offset));
                var vec2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, offset));
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
    /// Executes 3-operand FMA operation: result = a * b + c using AVX2 FMA instructions.
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

        const int vectorSize = 8; // 256 bits / 32 bits per float
        var vectorCount = a.Length / vectorSize;
        var remainder = a.Length % vectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var cRef = ref MemoryMarshal.GetReference(c);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        // Use AVX2 FMA instructions for maximum performance
        if (Fma.IsSupported && Avx2.IsSupported)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * vectorSize);
                var vecA = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, offset));
                var vecB = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, offset));
                var vecC = Vector256.LoadUnsafe(ref Unsafe.Add(ref cRef, offset));

                // True FMA: result = a * b + c with single rounding
                var fmaResult = Fma.MultiplyAdd(vecA, vecB, vecC);
                fmaResult.StoreUnsafe(ref Unsafe.Add(ref resultRef, offset));
            }
        }
        else
        {
            // Fallback to scalar if FMA/AVX2 not available
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
    /// Executes 3-operand FMA operation: result = a * b + c using AVX2 FMA instructions for double precision.
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

        const int vectorSize = 4; // 256 bits / 64 bits per double
        var vectorCount = a.Length / vectorSize;
        var remainder = a.Length % vectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var cRef = ref MemoryMarshal.GetReference(c);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        // Use AVX2 FMA instructions for maximum performance
        if (Fma.IsSupported && Avx2.IsSupported)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * vectorSize);
                var vecA = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, offset));
                var vecB = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, offset));
                var vecC = Vector256.LoadUnsafe(ref Unsafe.Add(ref cRef, offset));

                // True FMA: result = a * b + c with single rounding
                var fmaResult = Fma.MultiplyAdd(vecA, vecB, vecC);
                fmaResult.StoreUnsafe(ref Unsafe.Add(ref resultRef, offset));
            }
        }
        else
        {
            // Fallback to scalar if FMA/AVX2 not available
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