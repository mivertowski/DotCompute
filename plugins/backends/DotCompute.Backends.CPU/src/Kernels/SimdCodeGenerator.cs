// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Intrinsics;

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// Generates optimized SIMD code for kernel execution using Native AOT-compatible approaches.
/// </summary>
internal sealed class SimdCodeGenerator
{
    private readonly Dictionary<string, SimdKernelExecutor> _executorCache = new();
    private readonly SimdSummary _simdCapabilities;

    public SimdCodeGenerator(SimdSummary simdCapabilities)
    {
        _simdCapabilities = simdCapabilities ?? throw new ArgumentNullException(nameof(simdCapabilities));
    }

    /// <summary>
    /// Gets or creates a vectorized kernel executor.
    /// </summary>
    public SimdKernelExecutor GetOrCreateVectorizedKernel(
        DotCompute.Abstractions.KernelDefinition definition,
        KernelExecutionPlan executionPlan)
    {
        var cacheKey = $"{definition.Name}_{executionPlan.VectorWidth}_{executionPlan.VectorizationFactor}";

        if (_executorCache.TryGetValue(cacheKey, out var cachedExecutor))
        {
            return cachedExecutor;
        }

        var executor = CreateSimdExecutor(definition, executionPlan);
        _executorCache[cacheKey] = executor;
        return executor;
    }

    private SimdKernelExecutor CreateSimdExecutor(
        DotCompute.Abstractions.KernelDefinition definition,
        KernelExecutionPlan executionPlan)
    {
        // Select the appropriate SIMD implementation based on capabilities and execution plan
        return executionPlan.VectorWidth switch
        {
            512 when _simdCapabilities.SupportsAvx512 => new Avx512KernelExecutor(definition, executionPlan),
            256 when _simdCapabilities.SupportsAvx2 => new Avx2KernelExecutor(definition, executionPlan),
            // ARM NEON support for cross-platform deployment
            128 when _simdCapabilities.SupportsAdvSimd => new NeonKernelExecutor(definition, executionPlan),
            128 when _simdCapabilities.SupportsSse2 => new SseKernelExecutor(definition, executionPlan),
            _ => new ScalarKernelExecutor(definition, executionPlan)
        };
    }
}

/// <summary>
/// Base class for SIMD kernel executors.
/// </summary>
internal abstract class SimdKernelExecutor
{
    protected readonly KernelDefinition Definition;
    protected readonly KernelExecutionPlan ExecutionPlan;

    protected SimdKernelExecutor(DotCompute.Abstractions.KernelDefinition definition, KernelExecutionPlan executionPlan)
    {
        Definition = definition;
        ExecutionPlan = executionPlan;
    }

    /// <summary>
    /// Executes the kernel with the given buffers.
    /// </summary>
    public abstract void Execute(
        ReadOnlySpan<byte> input1,
        ReadOnlySpan<byte> input2,
        Span<byte> output,
        long elementCount,
        int vectorWidth);

    /// <summary>
    /// Gets the operation type from kernel metadata.
    /// </summary>
    protected KernelOperation GetOperationType()
    {
        if (Definition.Metadata?.TryGetValue("Operation", out var op) == true && op is string opStr)
        {
            return Enum.Parse<KernelOperation>(opStr, ignoreCase: true);
        }

        // Default to Add if not specified
        return KernelOperation.Add;
    }

    /// <summary>
    /// Gets the element type from kernel metadata.
    /// </summary>
    protected Type GetElementType()
    {
        // Extract element type from metadata
        if (Definition.Metadata?.TryGetValue("ElementType", out var typeObj) == true && typeObj is Type type)
        {
            return type;
        }
        return typeof(float); // Default to float
    }
}

/// <summary>
/// Supported kernel operations.
/// </summary>
internal enum KernelOperation
{
    Add,
    Multiply,
    FusedMultiplyAdd,
    Subtract,
    Divide,
    Maximum,
    Minimum,
    DotProduct,
    L2Norm,
    Convolution,
    Reduction,
    Gather,
    Scatter
}

/// <summary>
/// AVX-512 kernel executor implementation.
/// </summary>
internal sealed class Avx512KernelExecutor : SimdKernelExecutor
{
    public Avx512KernelExecutor(KernelDefinition definition, KernelExecutionPlan executionPlan)
        : base(definition, executionPlan)
    {
    }

    public override void Execute(
        ReadOnlySpan<byte> input1,
        ReadOnlySpan<byte> input2,
        Span<byte> output,
        long elementCount,
        int vectorWidth)
    {
        var operation = GetOperationType();
        var elementType = GetElementType();

        // Dispatch to the appropriate typed implementation
        if (elementType == typeof(float))
        {
            if (operation == KernelOperation.FusedMultiplyAdd && input1.Length == input2.Length)
            {
                // For 2-operand FMA, treat as a*b+0 (multiply)
                ExecuteFloat32(
                    MemoryMarshal.Cast<byte, float>(input1),
                    MemoryMarshal.Cast<byte, float>(input2),
                    MemoryMarshal.Cast<byte, float>(output),
                    elementCount,
                    KernelOperation.Multiply);
            }
            else
            {
                ExecuteFloat32(
                    MemoryMarshal.Cast<byte, float>(input1),
                    MemoryMarshal.Cast<byte, float>(input2),
                    MemoryMarshal.Cast<byte, float>(output),
                    elementCount,
                    operation);
            }
        }
        else if (elementType == typeof(double))
        {
            if (operation == KernelOperation.FusedMultiplyAdd && input1.Length == input2.Length)
            {
                // For 2-operand FMA, treat as a*b+0 (multiply)
                ExecuteFloat64(
                    MemoryMarshal.Cast<byte, double>(input1),
                    MemoryMarshal.Cast<byte, double>(input2),
                    MemoryMarshal.Cast<byte, double>(output),
                    elementCount,
                    KernelOperation.Multiply);
            }
            else
            {
                ExecuteFloat64(
                    MemoryMarshal.Cast<byte, double>(input1),
                    MemoryMarshal.Cast<byte, double>(input2),
                    MemoryMarshal.Cast<byte, double>(output),
                    elementCount,
                    operation);
            }
        }
    }

    /// <summary>
    /// Executes 3-operand FMA operation: result = a * b + c using AVX-512 FMA instructions.
    /// </summary>
    public void ExecuteFMA(
        ReadOnlySpan<byte> input1,
        ReadOnlySpan<byte> input2,
        ReadOnlySpan<byte> input3,
        Span<byte> output)
    {
        // Execute as float32 operations which are the most common and well-supported
        // across all SIMD instruction sets
        ExecuteFloat32FMA(
            MemoryMarshal.Cast<byte, float>(input1),
            MemoryMarshal.Cast<byte, float>(input2),
            MemoryMarshal.Cast<byte, float>(input3),
            MemoryMarshal.Cast<byte, float>(output));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat32(
        ReadOnlySpan<float> input1,
        ReadOnlySpan<float> input2,
        Span<float> output,
        long elementCount,
        KernelOperation operation)
    {
        const int VectorSize = 16; // 512 bits / 32 bits per float
        const int UnrollFactor = 4; // Process 4 vectors at a time for better pipelining
        var vectorCount = elementCount / VectorSize;
        var remainder = elementCount % VectorSize;

        // Get function pointer for the specific operation
        var vectorOp = GetVectorOperationFloat32(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        if (vectorOp != null)
        {
            // Process unrolled vectors for better instruction-level parallelism
            var unrolledCount = vectorCount / UnrollFactor;
            for (long i = 0; i < unrolledCount; i++)
            {
                var baseOffset = (int)(i * VectorSize * UnrollFactor);

                // Load all vectors first to maximize memory throughput
                var vec1_0 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input1Ref, baseOffset));
                var vec2_0 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input2Ref, baseOffset));
                var vec1_1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input1Ref, baseOffset + VectorSize));
                var vec2_1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input2Ref, baseOffset + VectorSize));
                var vec1_2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input1Ref, baseOffset + 2 * VectorSize));
                var vec2_2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input2Ref, baseOffset + 2 * VectorSize));
                var vec1_3 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input1Ref, baseOffset + 3 * VectorSize));
                var vec2_3 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input2Ref, baseOffset + 3 * VectorSize));

                // Compute all results
                var result0 = vectorOp(vec1_0, vec2_0);
                var result1 = vectorOp(vec1_1, vec2_1);
                var result2 = vectorOp(vec1_2, vec2_2);
                var result3 = vectorOp(vec1_3, vec2_3);

                // Store all results
                result0.StoreUnsafe(ref Unsafe.Add(ref outputRef, baseOffset));
                result1.StoreUnsafe(ref Unsafe.Add(ref outputRef, baseOffset + VectorSize));
                result2.StoreUnsafe(ref Unsafe.Add(ref outputRef, baseOffset + 2 * VectorSize));
                result3.StoreUnsafe(ref Unsafe.Add(ref outputRef, baseOffset + 3 * VectorSize));
            }

            // Process remaining full vectors
            for (long i = unrolledCount * UnrollFactor; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var vec1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input1Ref, offset));
                var vec2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input2Ref, offset));
                var result = vectorOp(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, offset));
            }
        }
        else
        {
            // Handle special cases like FMA that need different treatment
            var scalarOp = GetScalarOperationFloat32(operation);
            for (long i = 0; i < elementCount; i++)
            {
                output[(int)i] = scalarOp(input1[(int)i], input2[(int)i]);
            }
            return;
        }

        // Process remaining elements with optimized handling
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * VectorSize);

            // Try to use smaller SIMD operations for remainder if possible
            if (remainder >= 8 && Avx2.IsSupported)
            {
                // Use AVX2 for 8-element remainder
                var vec1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, lastVectorOffset));
                var vec2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, lastVectorOffset));
                var result = SimdOperationHelpers.GetVectorOperationFloat32_256(operation)(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, lastVectorOffset));
                lastVectorOffset += 8;
                remainder -= 8;
            }
            else if (remainder >= 4 && Sse.IsSupported)
            {
                // Use SSE for 4-element remainder
                var vec1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input1Ref, lastVectorOffset));
                var vec2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input2Ref, lastVectorOffset));
                var result = SimdOperationHelpers.GetVectorOperationFloat32_128(operation)(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, lastVectorOffset));
                lastVectorOffset += 4;
                remainder -= 4;
            }

            // Handle final scalar elements
            if (remainder > 0)
            {
                var scalarOp = GetScalarOperationFloat32(operation);
                for (long i = 0; i < remainder; i++)
                {
                    var idx = lastVectorOffset + i;
                    output[(int)idx] = scalarOp(input1[(int)idx], input2[(int)idx]);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat64(
        ReadOnlySpan<double> input1,
        ReadOnlySpan<double> input2,
        Span<double> output,
        long elementCount,
        KernelOperation operation)
    {
        const int VectorSize = 8; // 512 bits / 64 bits per double
        var vectorCount = elementCount / VectorSize;
        var remainder = elementCount % VectorSize;

        // Get function pointer for the specific operation
        var vectorOp = GetVectorOperationFloat64(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        if (vectorOp != null)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var vec1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input1Ref, (int)offset));
                var vec2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref input2Ref, (int)offset));
                var result = vectorOp(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, (int)offset));
            }
        }
        else
        {
            // Handle special cases like FMA that need different treatment
            var scalarOp = GetScalarOperationFloat64(operation);
            for (long i = 0; i < elementCount; i++)
            {
                output[(int)i] = scalarOp(input1[(int)i], input2[(int)i]);
            }
            return;
        }

        // Process remaining elements
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * VectorSize);
            var scalarOp = GetScalarOperationFloat64(operation);

            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                output[(int)idx] = scalarOp(input1[(int)idx], input2[(int)idx]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate*<Vector512<float>, Vector512<float>, Vector512<float>> GetVectorOperationFloat32(KernelOperation operation)
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
    private static unsafe delegate*<Vector256<float>, Vector256<float>, Vector256<float>> GetVectorOperationFloat32_256(KernelOperation operation)
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
    private static unsafe delegate*<Vector128<float>, Vector128<float>, Vector128<float>> GetVectorOperationFloat32_128(KernelOperation operation)
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
    private static unsafe delegate*<Vector512<double>, Vector512<double>, Vector512<double>> GetVectorOperationFloat64(KernelOperation operation)
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
    private static Func<float, float, float> GetScalarOperationFloat32(KernelOperation operation)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<double, double, double> GetScalarOperationFloat64(KernelOperation operation)
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
    /// Executes 3-operand FMA operation: result = a * b + c using AVX-512 FMA instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void ExecuteFloat32FMA(
        ReadOnlySpan<float> a,
        ReadOnlySpan<float> b,
        ReadOnlySpan<float> c,
        Span<float> result)
    {
        if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
            throw new ArgumentException("All spans must have the same length");

        const int VectorSize = 16; // 512 bits / 32 bits per float
        var vectorCount = a.Length / VectorSize;
        var remainder = a.Length % VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var cRef = ref MemoryMarshal.GetReference(c);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        // Use AVX-512 FMA instructions for maximum performance
        if (Avx512F.IsSupported)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var vecA = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vecB = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vecC = Vector512.LoadUnsafe(ref Unsafe.Add(ref cRef, (int)offset));

                // True FMA: result = a * b + c with single rounding
                var fmaResult = Avx512F.FusedMultiplyAdd(vecA, vecB, vecC);
                fmaResult.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
            }
        }
        else
        {
            // Fallback to scalar if AVX-512 not available
            vectorCount = 0;
            remainder = a.Length;
        }

        // Handle remainder with scalar FMA
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * VectorSize);
            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                result[(int)idx] = MathF.FusedMultiplyAdd(a[(int)idx], b[(int)idx], c[(int)idx]);
            }
        }
    }

    /// <summary>
    /// Executes 3-operand FMA operation: result = a * b + c using AVX-512 FMA instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void ExecuteFloat64FMA(
        ReadOnlySpan<double> a,
        ReadOnlySpan<double> b,
        ReadOnlySpan<double> c,
        Span<double> result)
    {
        if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
            throw new ArgumentException("All spans must have the same length");

        const int VectorSize = 8; // 512 bits / 64 bits per double
        var vectorCount = a.Length / VectorSize;
        var remainder = a.Length % VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var cRef = ref MemoryMarshal.GetReference(c);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        // Use AVX-512 FMA instructions for maximum performance
        if (Avx512F.IsSupported)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var vecA = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vecB = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vecC = Vector512.LoadUnsafe(ref Unsafe.Add(ref cRef, (int)offset));

                // True FMA: result = a * b + c with single rounding
                var fmaResult = Avx512F.FusedMultiplyAdd(vecA, vecB, vecC);
                fmaResult.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
            }
        }
        else
        {
            // Fallback to scalar if AVX-512 not available
            vectorCount = 0;
            remainder = a.Length;
        }

        // Handle remainder with scalar FMA
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * VectorSize);
            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                result[(int)idx] = Math.FusedMultiplyAdd(a[(int)idx], b[(int)idx], c[(int)idx]);
            }
        }
    }
}

/// <summary>
/// AVX2 kernel executor implementation.
/// </summary>
internal sealed class Avx2KernelExecutor : SimdKernelExecutor
{
    public Avx2KernelExecutor(KernelDefinition definition, KernelExecutionPlan executionPlan)
        : base(definition, executionPlan)
    {
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat32(
        ReadOnlySpan<float> input1,
        ReadOnlySpan<float> input2,
        Span<float> output,
        long elementCount,
        KernelOperation operation)
    {
        const int VectorSize = 8; // 256 bits / 32 bits per float
        const int UnrollFactor = 2; // Process 2 vectors at a time for better pipelining
        var vectorCount = elementCount / VectorSize;
        var remainder = elementCount % VectorSize;

        // Get function pointer for the specific operation
        var vectorOp = GetVectorOperationFloat32(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        // Process unrolled vectors for better instruction-level parallelism
        var unrolledCount = vectorCount / UnrollFactor;
        for (long i = 0; i < unrolledCount; i++)
        {
            var baseOffset = (int)(i * VectorSize * UnrollFactor);

            // Load both vector pairs
            var vec1_0 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, baseOffset));
            var vec2_0 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, baseOffset));
            var vec1_1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, baseOffset + VectorSize));
            var vec2_1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, baseOffset + VectorSize));

            // Compute both results
            var result0 = vectorOp(vec1_0, vec2_0);
            var result1 = vectorOp(vec1_1, vec2_1);

            // Store both results
            result0.StoreUnsafe(ref Unsafe.Add(ref outputRef, baseOffset));
            result1.StoreUnsafe(ref Unsafe.Add(ref outputRef, baseOffset + VectorSize));
        }

        // Process remaining full vectors
        for (long i = unrolledCount * UnrollFactor; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var vec1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, offset));
            var vec2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, offset));
            var result = vectorOp(vec1, vec2);
            result.StoreUnsafe(ref Unsafe.Add(ref outputRef, offset));
        }

        // Process remaining elements with optimized handling
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * VectorSize);

            // Try to use smaller SIMD operations for remainder if possible
            if (remainder >= 8 && Avx2.IsSupported)
            {
                // Use AVX2 for 8-element remainder
                var vec1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, lastVectorOffset));
                var vec2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, lastVectorOffset));
                var result = SimdOperationHelpers.GetVectorOperationFloat32_256(operation)(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, lastVectorOffset));
                lastVectorOffset += 8;
                remainder -= 8;
            }
            else if (remainder >= 4 && Sse.IsSupported)
            {
                // Use SSE for 4-element remainder
                var vec1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input1Ref, lastVectorOffset));
                var vec2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input2Ref, lastVectorOffset));
                var result = SimdOperationHelpers.GetVectorOperationFloat32_128(operation)(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, lastVectorOffset));
                lastVectorOffset += 4;
                remainder -= 4;
            }

            // Handle final scalar elements
            if (remainder > 0)
            {
                var scalarOp = GetScalarOperationFloat32(operation);
                for (long i = 0; i < remainder; i++)
                {
                    var idx = lastVectorOffset + i;
                    output[(int)idx] = scalarOp(input1[(int)idx], input2[(int)idx]);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat64(
        ReadOnlySpan<double> input1,
        ReadOnlySpan<double> input2,
        Span<double> output,
        long elementCount,
        KernelOperation operation)
    {
        const int VectorSize = 4; // 256 bits / 64 bits per double
        var vectorCount = elementCount / VectorSize;
        var remainder = elementCount % VectorSize;

        // Get function pointer for the specific operation
        var vectorOp = GetVectorOperationFloat64(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        for (long i = 0; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var vec1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, (int)offset));
            var vec2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, (int)offset));
            var result = vectorOp(vec1, vec2);
            result.StoreUnsafe(ref Unsafe.Add(ref outputRef, (int)offset));
        }

        // Process remaining elements
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * VectorSize);
            var scalarOp = GetScalarOperationFloat64(operation);

            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                output[(int)idx] = scalarOp(input1[(int)idx], input2[(int)idx]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate*<Vector256<float>, Vector256<float>, Vector256<float>> GetVectorOperationFloat32(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Avx.Add,
            KernelOperation.Multiply => &Avx.Multiply,
            // FusedMultiplyAdd handled separately with proper FMA instructions
            KernelOperation.FusedMultiplyAdd => null, // Use specialized 3-argument FMA method
            KernelOperation.Subtract => &Avx.Subtract,
            KernelOperation.Divide => &Avx.Divide,
            KernelOperation.Maximum => &Avx.Max,
            KernelOperation.Minimum => &Avx.Min,
            _ => &Avx.Add
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate*<Vector256<double>, Vector256<double>, Vector256<double>> GetVectorOperationFloat64(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &Avx.Add,
            KernelOperation.Multiply => &Avx.Multiply,
            // FusedMultiplyAdd handled separately with proper FMA instructions
            KernelOperation.FusedMultiplyAdd => null, // Use specialized 3-argument FMA method
            KernelOperation.Subtract => &Avx.Subtract,
            KernelOperation.Divide => &Avx.Divide,
            KernelOperation.Maximum => &Avx.Max,
            KernelOperation.Minimum => &Avx.Min,
            _ => &Avx.Add
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<float, float, float> GetScalarOperationFloat32(KernelOperation operation)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<double, double, double> GetScalarOperationFloat64(KernelOperation operation)
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
            throw new ArgumentException("All spans must have the same length");

        const int VectorSize = 8; // 256 bits / 32 bits per float
        var vectorCount = a.Length / VectorSize;
        var remainder = a.Length % VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var cRef = ref MemoryMarshal.GetReference(c);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        // Use AVX2 FMA instructions for maximum performance
        if (Fma.IsSupported && Avx2.IsSupported)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var vecA = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vecB = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vecC = Vector256.LoadUnsafe(ref Unsafe.Add(ref cRef, (int)offset));

                // True FMA: result = a * b + c with single rounding
                var fmaResult = Fma.MultiplyAdd(vecA, vecB, vecC);
                fmaResult.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
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
            var lastVectorOffset = (int)(vectorCount * VectorSize);
            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                result[(int)idx] = MathF.FusedMultiplyAdd(a[(int)idx], b[(int)idx], c[(int)idx]);
            }
        }
    }

    /// <summary>
    /// Executes 3-operand FMA operation: result = a * b + c using AVX2 FMA instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void ExecuteFloat64FMA(
        ReadOnlySpan<double> a,
        ReadOnlySpan<double> b,
        ReadOnlySpan<double> c,
        Span<double> result)
    {
        if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
            throw new ArgumentException("All spans must have the same length");

        const int VectorSize = 4; // 256 bits / 64 bits per double
        var vectorCount = a.Length / VectorSize;
        var remainder = a.Length % VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var cRef = ref MemoryMarshal.GetReference(c);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        // Use AVX2 FMA instructions for maximum performance
        if (Fma.IsSupported && Avx2.IsSupported)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var vecA = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vecB = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vecC = Vector256.LoadUnsafe(ref Unsafe.Add(ref cRef, (int)offset));

                // True FMA: result = a * b + c with single rounding
                var fmaResult = Fma.MultiplyAdd(vecA, vecB, vecC);
                fmaResult.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
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
            var lastVectorOffset = (int)(vectorCount * VectorSize);
            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                result[(int)idx] = Math.FusedMultiplyAdd(a[(int)idx], b[(int)idx], c[(int)idx]);
            }
        }
    }
}

/// <summary>
/// SSE kernel executor implementation.
/// </summary>
internal sealed class SseKernelExecutor : SimdKernelExecutor
{
    public SseKernelExecutor(KernelDefinition definition, KernelExecutionPlan executionPlan)
        : base(definition, executionPlan)
    {
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat32(
        ReadOnlySpan<float> input1,
        ReadOnlySpan<float> input2,
        Span<float> output,
        long elementCount,
        KernelOperation operation)
    {
        const int VectorSize = 4; // 128 bits / 32 bits per float
        var vectorCount = elementCount / VectorSize;
        var remainder = elementCount % VectorSize;

        // Get function pointer for the specific operation
        var vectorOp = GetVectorOperationFloat32(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        for (long i = 0; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var vec1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input1Ref, (int)offset));
            var vec2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input2Ref, (int)offset));
            var result = vectorOp(vec1, vec2);
            result.StoreUnsafe(ref Unsafe.Add(ref outputRef, (int)offset));
        }

        // Process remaining elements with optimized handling
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * VectorSize);

            // Try to use smaller SIMD operations for remainder if possible
            if (remainder >= 8 && Avx2.IsSupported)
            {
                // Use AVX2 for 8-element remainder
                var vec1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, lastVectorOffset));
                var vec2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, lastVectorOffset));
                var result = SimdOperationHelpers.GetVectorOperationFloat32_256(operation)(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, lastVectorOffset));
                lastVectorOffset += 8;
                remainder -= 8;
            }
            else if (remainder >= 4 && Sse.IsSupported)
            {
                // Use SSE for 4-element remainder
                var vec1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input1Ref, lastVectorOffset));
                var vec2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input2Ref, lastVectorOffset));
                var result = SimdOperationHelpers.GetVectorOperationFloat32_128(operation)(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, lastVectorOffset));
                lastVectorOffset += 4;
                remainder -= 4;
            }

            // Handle final scalar elements
            if (remainder > 0)
            {
                var scalarOp = GetScalarOperationFloat32(operation);
                for (long i = 0; i < remainder; i++)
                {
                    var idx = lastVectorOffset + i;
                    output[(int)idx] = scalarOp(input1[(int)idx], input2[(int)idx]);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat64(
        ReadOnlySpan<double> input1,
        ReadOnlySpan<double> input2,
        Span<double> output,
        long elementCount,
        KernelOperation operation)
    {
        const int VectorSize = 2; // 128 bits / 64 bits per double
        var vectorCount = elementCount / VectorSize;
        var remainder = elementCount % VectorSize;

        // Get function pointer for the specific operation
        var vectorOp = GetVectorOperationFloat64(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        for (long i = 0; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var vec1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input1Ref, (int)offset));
            var vec2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input2Ref, (int)offset));
            var result = vectorOp(vec1, vec2);
            result.StoreUnsafe(ref Unsafe.Add(ref outputRef, (int)offset));
        }

        // Process remaining elements
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * VectorSize);
            var scalarOp = GetScalarOperationFloat64(operation);

            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                output[(int)idx] = scalarOp(input1[(int)idx], input2[(int)idx]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate*<Vector128<float>, Vector128<float>, Vector128<float>> GetVectorOperationFloat32(KernelOperation operation)
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
    private static unsafe delegate*<Vector128<double>, Vector128<double>, Vector128<double>> GetVectorOperationFloat64(KernelOperation operation)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<float, float, float> GetScalarOperationFloat32(KernelOperation operation)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<double, double, double> GetScalarOperationFloat64(KernelOperation operation)
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
}

/// <summary>
/// ARM NEON kernel executor implementation for cross-platform support.
/// </summary>
internal sealed class NeonKernelExecutor : SimdKernelExecutor
{
    public NeonKernelExecutor(KernelDefinition definition, KernelExecutionPlan executionPlan)
        : base(definition, executionPlan)
    {
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat32(
        ReadOnlySpan<float> input1,
        ReadOnlySpan<float> input2,
        Span<float> output,
        long elementCount,
        KernelOperation operation)
    {
        const int VectorSize = 4; // 128 bits / 32 bits per float
        var vectorCount = elementCount / VectorSize;
        var remainder = elementCount % VectorSize;

        // Get function pointer for the specific operation
        var vectorOp = GetVectorOperationFloat32(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        for (long i = 0; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var vec1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input1Ref, (int)offset));
            var vec2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input2Ref, (int)offset));
            var result = vectorOp(vec1, vec2);
            result.StoreUnsafe(ref Unsafe.Add(ref outputRef, (int)offset));
        }

        // Process remaining elements with optimized handling
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * VectorSize);

            // Try to use smaller SIMD operations for remainder if possible
            if (remainder >= 8 && Avx2.IsSupported)
            {
                // Use AVX2 for 8-element remainder
                var vec1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input1Ref, lastVectorOffset));
                var vec2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref input2Ref, lastVectorOffset));
                var result = SimdOperationHelpers.GetVectorOperationFloat32_256(operation)(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, lastVectorOffset));
                lastVectorOffset += 8;
                remainder -= 8;
            }
            else if (remainder >= 4 && Sse.IsSupported)
            {
                // Use SSE for 4-element remainder
                var vec1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input1Ref, lastVectorOffset));
                var vec2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input2Ref, lastVectorOffset));
                var result = SimdOperationHelpers.GetVectorOperationFloat32_128(operation)(vec1, vec2);
                result.StoreUnsafe(ref Unsafe.Add(ref outputRef, lastVectorOffset));
                lastVectorOffset += 4;
                remainder -= 4;
            }

            // Handle final scalar elements
            if (remainder > 0)
            {
                var scalarOp = GetScalarOperationFloat32(operation);
                for (long i = 0; i < remainder; i++)
                {
                    var idx = lastVectorOffset + i;
                    output[(int)idx] = scalarOp(input1[(int)idx], input2[(int)idx]);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat64(
        ReadOnlySpan<double> input1,
        ReadOnlySpan<double> input2,
        Span<double> output,
        long elementCount,
        KernelOperation operation)
    {
        const int VectorSize = 2; // 128 bits / 64 bits per double
        var vectorCount = elementCount / VectorSize;
        var remainder = elementCount % VectorSize;

        // Get function pointer for the specific operation
        var vectorOp = GetVectorOperationFloat64(operation);

        // Process full vectors
        ref var input1Ref = ref MemoryMarshal.GetReference(input1);
        ref var input2Ref = ref MemoryMarshal.GetReference(input2);
        ref var outputRef = ref MemoryMarshal.GetReference(output);

        for (long i = 0; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var vec1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input1Ref, (int)offset));
            var vec2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref input2Ref, (int)offset));
            var result = vectorOp(vec1, vec2);
            result.StoreUnsafe(ref Unsafe.Add(ref outputRef, (int)offset));
        }

        // Process remaining elements
        if (remainder > 0)
        {
            var lastVectorOffset = (int)(vectorCount * VectorSize);
            var scalarOp = GetScalarOperationFloat64(operation);

            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                output[(int)idx] = scalarOp(input1[(int)idx], input2[(int)idx]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate*<Vector128<float>, Vector128<float>, Vector128<float>> GetVectorOperationFloat32(KernelOperation operation)
    {
        return operation switch
        {
            KernelOperation.Add => &AdvSimd.Add,
            KernelOperation.Multiply => &AdvSimd.Multiply,
            // FusedMultiplyAdd uses ARM NEON FMLA instructions - handled separately
            KernelOperation.FusedMultiplyAdd => null, // Use specialized 3-argument FMLA method
            KernelOperation.Subtract => &AdvSimd.Subtract,
            KernelOperation.Divide => null, // AdvSimd doesn't have divide, use scalar fallback
            KernelOperation.Maximum => &AdvSimd.Max,
            KernelOperation.Minimum => &AdvSimd.Min,
            _ => &AdvSimd.Add
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate*<Vector128<double>, Vector128<double>, Vector128<double>> GetVectorOperationFloat64(KernelOperation operation)
    {
        // ARM NEON has limited double precision support, use available operations
        return operation switch
        {
            KernelOperation.Add => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Add : &FallbackAdd,
            KernelOperation.Multiply => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Multiply : &FallbackMultiply,
            // FusedMultiplyAdd uses ARM NEON FMLA instructions - handled separately
            KernelOperation.FusedMultiplyAdd => null,
            KernelOperation.Subtract => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Subtract : &FallbackSubtract,
            KernelOperation.Divide => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Divide : &FallbackDivide,
            KernelOperation.Maximum => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Max : &FallbackMax,
            KernelOperation.Minimum => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Min : &FallbackMin,
            _ => AdvSimd.Arm64.IsSupported ? &AdvSimd.Arm64.Add : &FallbackAdd
        };
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<float, float, float> GetScalarOperationFloat32(KernelOperation operation)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<double, double, double> GetScalarOperationFloat64(KernelOperation operation)
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
            throw new ArgumentException("All spans must have the same length");

        const int VectorSize = 4; // 128 bits / 32 bits per float
        var vectorCount = a.Length / VectorSize;
        var remainder = a.Length % VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var cRef = ref MemoryMarshal.GetReference(c);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        // Use ARM NEON FMLA instructions for maximum performance
        if (AdvSimd.IsSupported)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var vecA = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vecB = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vecC = Vector128.LoadUnsafe(ref Unsafe.Add(ref cRef, (int)offset));

                // ARM NEON FMLA: result = a * b + c with single rounding
                var fmaResult = AdvSimd.FusedMultiplyAdd(vecC, vecA, vecB); // Note: ARM order is (accumulator, multiplicand, multiplier)
                fmaResult.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
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
            var lastVectorOffset = (int)(vectorCount * VectorSize);
            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                result[(int)idx] = MathF.FusedMultiplyAdd(a[(int)idx], b[(int)idx], c[(int)idx]);
            }
        }
    }

    /// <summary>
    /// Executes 3-operand FMA operation: result = a * b + c using ARM NEON FMLA instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void ExecuteFloat64FMA(
        ReadOnlySpan<double> a,
        ReadOnlySpan<double> b,
        ReadOnlySpan<double> c,
        Span<double> result)
    {
        if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
            throw new ArgumentException("All spans must have the same length");

        const int VectorSize = 2; // 128 bits / 64 bits per double
        var vectorCount = a.Length / VectorSize;
        var remainder = a.Length % VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var cRef = ref MemoryMarshal.GetReference(c);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        // Use ARM NEON FMLA instructions for maximum performance (ARM64 only for double)
        if (AdvSimd.Arm64.IsSupported)
        {
            for (long i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var vecA = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vecB = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vecC = Vector128.LoadUnsafe(ref Unsafe.Add(ref cRef, (int)offset));

                // ARM NEON FMLA: result = a * b + c with single rounding
                var fmaResult = AdvSimd.Arm64.FusedMultiplyAdd(vecC, vecA, vecB); // Note: ARM order is (accumulator, multiplicand, multiplier)
                fmaResult.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
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
            var lastVectorOffset = (int)(vectorCount * VectorSize);
            for (long i = 0; i < remainder; i++)
            {
                var idx = lastVectorOffset + i;
                result[(int)idx] = Math.FusedMultiplyAdd(a[(int)idx], b[(int)idx], c[(int)idx]);
            }
        }
    }
}

/// <summary>
/// Scalar kernel executor implementation (fallback).
/// </summary>
internal sealed class ScalarKernelExecutor : SimdKernelExecutor
{
    public ScalarKernelExecutor(KernelDefinition definition, KernelExecutionPlan executionPlan)
        : base(definition, executionPlan)
    {
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat32(
        ReadOnlySpan<float> input1,
        ReadOnlySpan<float> input2,
        Span<float> output,
        long elementCount,
        KernelOperation operation)
    {
        var scalarOp = GetScalarOperationFloat32(operation);

        for (long i = 0; i < elementCount; i++)
        {
            output[(int)i] = scalarOp(input1[(int)i], input2[(int)i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFloat64(
        ReadOnlySpan<double> input1,
        ReadOnlySpan<double> input2,
        Span<double> output,
        long elementCount,
        KernelOperation operation)
    {
        var scalarOp = GetScalarOperationFloat64(operation);

        for (long i = 0; i < elementCount; i++)
        {
            output[(int)i] = scalarOp(input1[(int)i], input2[(int)i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<float, float, float> GetScalarOperationFloat32(KernelOperation operation)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<double, double, double> GetScalarOperationFloat64(KernelOperation operation)
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
}

/// <summary>
/// Provides optimized vector operation patterns for common use cases.
/// </summary>
public static class VectorPatterns
{
    /// <summary>
    /// Performs vectorized addition of two arrays.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAdd<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> result)
        where T : unmanaged
    {
        if (typeof(T) == typeof(float))
        {
            VectorAddFloat32(
                MemoryMarshal.Cast<T, float>(a),
                MemoryMarshal.Cast<T, float>(b),
                MemoryMarshal.Cast<T, float>(result));
        }
        else if (typeof(T) == typeof(double))
        {
            VectorAddFloat64(
                MemoryMarshal.Cast<T, double>(a),
                MemoryMarshal.Cast<T, double>(b),
                MemoryMarshal.Cast<T, double>(result));
        }
        else
        {
            // Generic fallback using Vector<T>
            VectorAddGeneric(a, b, result);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat32(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        int vectorSize = Vector<float>.Count;
        int vectorizedLength = a.Length - (a.Length % vectorSize);

        // Use platform-optimal SIMD
        if (Vector512.IsHardwareAccelerated && a.Length >= 16)
        {
            VectorAddFloat32_Avx512(a, b, result);
        }
        else if (Vector256.IsHardwareAccelerated && a.Length >= 8)
        {
            VectorAddFloat32_Avx2(a, b, result);
        }
        else if (Vector128.IsHardwareAccelerated && a.Length >= 4)
        {
            VectorAddFloat32_Sse(a, b, result);
        }
        else
        {
            // Scalar fallback
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat32_Avx512(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        const int VectorSize = 16;
        int vectorCount = a.Length / VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        for (int i = 0; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var va = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
            var vb = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
            var vr = Avx512F.Add(va, vb);
            vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
        }

        // Handle remainder
        int remainder = a.Length % VectorSize;
        if (remainder > 0)
        {
            int lastOffset = vectorCount * VectorSize;
            for (int i = 0; i < remainder; i++)
            {
                result[lastOffset + i] = a[lastOffset + i] + b[lastOffset + i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat32_Avx2(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        const int VectorSize = 8;
        int vectorCount = a.Length / VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        for (int i = 0; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var va = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
            var vb = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
            var vr = Avx.Add(va, vb);
            vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
        }

        // Handle remainder
        int remainder = a.Length % VectorSize;
        if (remainder > 0)
        {
            int lastOffset = vectorCount * VectorSize;
            for (int i = 0; i < remainder; i++)
            {
                result[lastOffset + i] = a[lastOffset + i] + b[lastOffset + i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat32_Sse(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        const int VectorSize = 4;
        int vectorCount = a.Length / VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        for (int i = 0; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
            var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
            var vr = Sse.Add(va, vb);
            vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
        }

        // Handle remainder
        int remainder = a.Length % VectorSize;
        if (remainder > 0)
        {
            int lastOffset = vectorCount * VectorSize;
            for (int i = 0; i < remainder; i++)
            {
                result[lastOffset + i] = a[lastOffset + i] + b[lastOffset + i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat64(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        if (Vector512.IsHardwareAccelerated && a.Length >= 8)
        {
            VectorAddFloat64_Avx512(a, b, result);
        }
        else if (Vector256.IsHardwareAccelerated && a.Length >= 4)
        {
            VectorAddFloat64_Avx2(a, b, result);
        }
        else if (Vector128.IsHardwareAccelerated && a.Length >= 2)
        {
            VectorAddFloat64_Sse(a, b, result);
        }
        else
        {
            // Scalar fallback
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat64_Avx512(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        const int VectorSize = 8;
        int vectorCount = a.Length / VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        for (int i = 0; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var va = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
            var vb = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
            var vr = Avx512F.Add(va, vb);
            vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
        }

        // Handle remainder
        int remainder = a.Length % VectorSize;
        if (remainder > 0)
        {
            int lastOffset = vectorCount * VectorSize;
            for (int i = 0; i < remainder; i++)
            {
                result[lastOffset + i] = a[lastOffset + i] + b[lastOffset + i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat64_Avx2(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        const int VectorSize = 4;
        int vectorCount = a.Length / VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        for (int i = 0; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var va = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
            var vb = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
            var vr = Avx.Add(va, vb);
            vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
        }

        // Handle remainder
        int remainder = a.Length % VectorSize;
        if (remainder > 0)
        {
            int lastOffset = vectorCount * VectorSize;
            for (int i = 0; i < remainder; i++)
            {
                result[lastOffset + i] = a[lastOffset + i] + b[lastOffset + i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddFloat64_Sse(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        const int VectorSize = 2;
        int vectorCount = a.Length / VectorSize;

        ref var aRef = ref MemoryMarshal.GetReference(a);
        ref var bRef = ref MemoryMarshal.GetReference(b);
        ref var resultRef = ref MemoryMarshal.GetReference(result);

        for (int i = 0; i < vectorCount; i++)
        {
            var offset = (int)(i * VectorSize);
            var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
            var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
            var vr = Sse2.Add(va, vb);
            vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
        }

        // Handle remainder
        int remainder = a.Length % VectorSize;
        if (remainder > 0)
        {
            int lastOffset = vectorCount * VectorSize;
            for (int i = 0; i < remainder; i++)
            {
                result[lastOffset + i] = a[lastOffset + i] + b[lastOffset + i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorAddGeneric<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> result)
        where T : unmanaged
    {
        int vectorSize = Vector<T>.Count;
        int vectorizedLength = a.Length - (a.Length % vectorSize);

        // Process vectors
        for (int i = 0; i < vectorizedLength; i += vectorSize)
        {
            var va = new Vector<T>(a.Slice(i, vectorSize));
            var vb = new Vector<T>(b.Slice(i, vectorSize));
            var vr = va + vb;
            vr.CopyTo(result.Slice(i, vectorSize));
        }

        // Process remaining elements
        for (int i = vectorizedLength; i < a.Length; i++)
        {
            // Use dynamic dispatch for generic arithmetic
            result[i] = AddGeneric(a[i], b[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T AddGeneric<T>(T a, T b) where T : unmanaged
    {
        // This will be optimized by JIT for specific types
        if (typeof(T) == typeof(int))
        {
            return (T)(object)(((int)(object)a) + ((int)(object)b));
        }
        else if (typeof(T) == typeof(long))
        {
            return (T)(object)(((long)(object)a) + ((long)(object)b));
        }
        else if (typeof(T) == typeof(float))
        {
            return (T)(object)(((float)(object)a) + ((float)(object)b));
        }
        else if (typeof(T) == typeof(double))
        {
            return (T)(object)(((double)(object)a) + ((double)(object)b));
        }
        else
        {
            throw new NotSupportedException($"Addition not supported for type {typeof(T)}");
        }
    }

    /// <summary>
    /// Performs vectorized multiplication of two arrays.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorMultiply<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> result)
        where T : unmanaged
    {
        if (typeof(T) == typeof(float))
        {
            VectorMultiplyFloat32(
                MemoryMarshal.Cast<T, float>(a),
                MemoryMarshal.Cast<T, float>(b),
                MemoryMarshal.Cast<T, float>(result));
        }
        else if (typeof(T) == typeof(double))
        {
            VectorMultiplyFloat64(
                MemoryMarshal.Cast<T, double>(a),
                MemoryMarshal.Cast<T, double>(b),
                MemoryMarshal.Cast<T, double>(result));
        }
        else
        {
            // Generic fallback using Vector<T>
            VectorMultiplyGeneric(a, b, result);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyFloat32(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        if (Vector512.IsHardwareAccelerated && a.Length >= 16)
        {
            const int VectorSize = 16;
            int vectorCount = a.Length / VectorSize;

            ref var aRef = ref MemoryMarshal.GetReference(a);
            ref var bRef = ref MemoryMarshal.GetReference(b);
            ref var resultRef = ref MemoryMarshal.GetReference(result);

            for (int i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var va = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vb = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vr = Avx512F.Multiply(va, vb);
                vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
            }

            // Handle remainder
            int remainder = a.Length % VectorSize;
            if (remainder > 0)
            {
                int lastOffset = vectorCount * VectorSize;
                for (int i = 0; i < remainder; i++)
                {
                    result[lastOffset + i] = a[lastOffset + i] * b[lastOffset + i];
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && a.Length >= 8)
        {
            const int VectorSize = 8;
            int vectorCount = a.Length / VectorSize;

            ref var aRef = ref MemoryMarshal.GetReference(a);
            ref var bRef = ref MemoryMarshal.GetReference(b);
            ref var resultRef = ref MemoryMarshal.GetReference(result);

            for (int i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var va = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vb = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vr = Avx.Multiply(va, vb);
                vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
            }

            // Handle remainder
            int remainder = a.Length % VectorSize;
            if (remainder > 0)
            {
                int lastOffset = vectorCount * VectorSize;
                for (int i = 0; i < remainder; i++)
                {
                    result[lastOffset + i] = a[lastOffset + i] * b[lastOffset + i];
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated && a.Length >= 4)
        {
            const int VectorSize = 4;
            int vectorCount = a.Length / VectorSize;

            ref var aRef = ref MemoryMarshal.GetReference(a);
            ref var bRef = ref MemoryMarshal.GetReference(b);
            ref var resultRef = ref MemoryMarshal.GetReference(result);

            for (int i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vr = Sse.Multiply(va, vb);
                vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
            }

            // Handle remainder
            int remainder = a.Length % VectorSize;
            if (remainder > 0)
            {
                int lastOffset = vectorCount * VectorSize;
                for (int i = 0; i < remainder; i++)
                {
                    result[lastOffset + i] = a[lastOffset + i] * b[lastOffset + i];
                }
            }
        }
        else
        {
            // Scalar fallback
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] * b[i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyFloat64(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        if (Vector512.IsHardwareAccelerated && a.Length >= 8)
        {
            const int VectorSize = 8;
            int vectorCount = a.Length / VectorSize;

            ref var aRef = ref MemoryMarshal.GetReference(a);
            ref var bRef = ref MemoryMarshal.GetReference(b);
            ref var resultRef = ref MemoryMarshal.GetReference(result);

            for (int i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var va = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vb = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vr = Avx512F.Multiply(va, vb);
                vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
            }

            // Handle remainder
            int remainder = a.Length % VectorSize;
            if (remainder > 0)
            {
                int lastOffset = vectorCount * VectorSize;
                for (int i = 0; i < remainder; i++)
                {
                    result[lastOffset + i] = a[lastOffset + i] * b[lastOffset + i];
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && a.Length >= 4)
        {
            const int VectorSize = 4;
            int vectorCount = a.Length / VectorSize;

            ref var aRef = ref MemoryMarshal.GetReference(a);
            ref var bRef = ref MemoryMarshal.GetReference(b);
            ref var resultRef = ref MemoryMarshal.GetReference(result);

            for (int i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var va = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vb = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vr = Avx.Multiply(va, vb);
                vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
            }

            // Handle remainder
            int remainder = a.Length % VectorSize;
            if (remainder > 0)
            {
                int lastOffset = vectorCount * VectorSize;
                for (int i = 0; i < remainder; i++)
                {
                    result[lastOffset + i] = a[lastOffset + i] * b[lastOffset + i];
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated && a.Length >= 2)
        {
            const int VectorSize = 2;
            int vectorCount = a.Length / VectorSize;

            ref var aRef = ref MemoryMarshal.GetReference(a);
            ref var bRef = ref MemoryMarshal.GetReference(b);
            ref var resultRef = ref MemoryMarshal.GetReference(result);

            for (int i = 0; i < vectorCount; i++)
            {
                var offset = (int)(i * VectorSize);
                var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, (int)offset));
                var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, (int)offset));
                var vr = Sse2.Multiply(va, vb);
                vr.StoreUnsafe(ref Unsafe.Add(ref resultRef, (int)offset));
            }

            // Handle remainder
            int remainder = a.Length % VectorSize;
            if (remainder > 0)
            {
                int lastOffset = vectorCount * VectorSize;
                for (int i = 0; i < remainder; i++)
                {
                    result[lastOffset + i] = a[lastOffset + i] * b[lastOffset + i];
                }
            }
        }
        else
        {
            // Scalar fallback
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] * b[i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorMultiplyGeneric<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> result)
        where T : unmanaged
    {
        int vectorSize = Vector<T>.Count;
        int vectorizedLength = a.Length - (a.Length % vectorSize);

        // Process vectors
        for (int i = 0; i < vectorizedLength; i += vectorSize)
        {
            var va = new Vector<T>(a.Slice(i, vectorSize));
            var vb = new Vector<T>(b.Slice(i, vectorSize));
            var vr = va * vb;
            vr.CopyTo(result.Slice(i, vectorSize));
        }

        // Process remaining elements
        for (int i = vectorizedLength; i < a.Length; i++)
        {
            result[i] = MultiplyGeneric(a[i], b[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T MultiplyGeneric<T>(T a, T b) where T : unmanaged
    {
        if (typeof(T) == typeof(int))
        {
            return (T)(object)(((int)(object)a) * ((int)(object)b));
        }
        else if (typeof(T) == typeof(long))
        {
            return (T)(object)(((long)(object)a) * ((long)(object)b));
        }
        else if (typeof(T) == typeof(float))
        {
            return (T)(object)(((float)(object)a) * ((float)(object)b));
        }
        else if (typeof(T) == typeof(double))
        {
            return (T)(object)(((double)(object)a) * ((double)(object)b));
        }
        else
        {
            throw new NotSupportedException($"Multiplication not supported for type {typeof(T)}");
        }
    }

    /// <summary>
    /// Performs vectorized fused multiply-add operation: result = a * b + c.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFMA<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, ReadOnlySpan<T> c, Span<T> result)
        where T : unmanaged
    {
        if (a.Length != b.Length || a.Length != c.Length || a.Length != result.Length)
            throw new ArgumentException("All spans must have the same length");

        if (typeof(T) == typeof(float))
        {
            VectorFMAFloat32(
                MemoryMarshal.Cast<T, float>(a),
                MemoryMarshal.Cast<T, float>(b),
                MemoryMarshal.Cast<T, float>(c),
                MemoryMarshal.Cast<T, float>(result));
        }
        else if (typeof(T) == typeof(double))
        {
            VectorFMAFloat64(
                MemoryMarshal.Cast<T, double>(a),
                MemoryMarshal.Cast<T, double>(b),
                MemoryMarshal.Cast<T, double>(c),
                MemoryMarshal.Cast<T, double>(result));
        }
        else
        {
            // Generic fallback
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = AddGeneric(MultiplyGeneric(a[i], b[i]), c[i]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorFMAFloat32(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> result)
    {
        // Use the best available FMA implementation
        if (Avx512F.IsSupported && a.Length >= 16)
        {
            Avx512KernelExecutor.ExecuteFloat32FMA(a, b, c, result);
        }
        else if (Fma.IsSupported && Vector256.IsHardwareAccelerated && a.Length >= 8)
        {
            Avx2KernelExecutor.ExecuteFloat32FMA(a, b, c, result);
        }
        else if (AdvSimd.IsSupported && a.Length >= 4)
        {
            NeonKernelExecutor.ExecuteFloat32FMA(a, b, c, result);
        }
        else
        {
            // Fallback to scalar FMA
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = MathF.FusedMultiplyAdd(a[i], b[i], c[i]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void VectorFMAFloat64(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> result)
    {
        // Use the best available FMA implementation
        if (Avx512F.IsSupported && a.Length >= 8)
        {
            Avx512KernelExecutor.ExecuteFloat64FMA(a, b, c, result);
        }
        else if (Fma.IsSupported && Vector256.IsHardwareAccelerated && a.Length >= 4)
        {
            Avx2KernelExecutor.ExecuteFloat64FMA(a, b, c, result);
        }
        else if (AdvSimd.Arm64.IsSupported && a.Length >= 2)
        {
            NeonKernelExecutor.ExecuteFloat64FMA(a, b, c, result);
        }
        else
        {
            // Fallback to scalar FMA
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = Math.FusedMultiplyAdd(a[i], b[i], c[i]);
            }
        }
    }
}
