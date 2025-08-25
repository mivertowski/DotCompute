// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.X86;
using DotCompute.Backends.CPU.Intrinsics;

namespace DotCompute.Backends.CPU.Kernels;


/// <summary>
/// High-performance SIMD kernel executor with hardware-specific optimizations.
/// </summary>
public sealed class HardwareSimdKernelExecutor
{
    private readonly SimdSummary _simdCapabilities;
    private readonly int _preferredVectorWidth;
    private readonly bool _supportsAvx512;
    private readonly bool _supportsAvx2;
    private readonly bool _supportsFma;

    /// <summary>
    /// Initializes a new instance of the <see cref="HardwareSimdKernelExecutor"/> class.
    /// </summary>
    /// <param name="simdCapabilities">The simd capabilities.</param>
    /// <exception cref="System.ArgumentNullException">simdCapabilities</exception>
    public HardwareSimdKernelExecutor(SimdSummary simdCapabilities)
    {
        _simdCapabilities = simdCapabilities ?? throw new ArgumentNullException(nameof(simdCapabilities));
        _preferredVectorWidth = simdCapabilities.PreferredVectorWidth;
        _supportsAvx512 = simdCapabilities.SupportedInstructionSets.Contains("AVX512F");
        _supportsAvx2 = simdCapabilities.SupportedInstructionSets.Contains("AVX2");
        _supportsFma = simdCapabilities.SupportedInstructionSets.Contains("FMA");
    }

    /// <summary>
    /// Executes a vectorized kernel with optimal SIMD instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe void Execute(
        Span<byte> input1,
        Span<byte> input2,
        Span<byte> output,
        int elementCount,
        int vectorWidth)
    {
        // Ensure proper alignment and size
        if (input1.Length < elementCount * sizeof(float) ||
            input2.Length < elementCount * sizeof(float) ||
            output.Length < elementCount * sizeof(float))
        {
            throw new ArgumentException("Buffer sizes are insufficient for the requested element count");
        }

        // Choose optimal execution path based on hardware capabilities
        if (_supportsAvx512 && vectorWidth >= 512 && elementCount >= 16)
        {
            ExecuteAvx512(input1, input2, output, elementCount);
        }
        else if (_supportsAvx2 && vectorWidth >= 256 && elementCount >= 8)
        {
            ExecuteAvx2(input1, input2, output, elementCount);
        }
        else if (Vector128.IsHardwareAccelerated && elementCount >= 4)
        {
            ExecuteVector128(input1, input2, output, elementCount);
        }
        else
        {
            ExecuteScalar(input1, input2, output, elementCount);
        }
    }

    /// <summary>
    /// Executes a unary vectorized operation (single input, single output).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe void ExecuteUnary(
        Span<byte> input,
        Span<byte> output,
        int elementCount,
        UnaryOperation operation)
    {
        if (input.Length < elementCount * sizeof(float) ||
            output.Length < elementCount * sizeof(float))
        {
            throw new ArgumentException("Buffer sizes are insufficient for the requested element count");
        }

        // Choose optimal execution path based on hardware capabilities
        if (_supportsAvx512 && elementCount >= 16)
        {
            ExecuteUnaryAvx512(input, output, elementCount, operation);
        }
        else if (_supportsAvx2 && elementCount >= 8)
        {
            ExecuteUnaryAvx2(input, output, elementCount, operation);
        }
        else if (Vector128.IsHardwareAccelerated && elementCount >= 4)
        {
            ExecuteUnaryVector128(input, output, elementCount, operation);
        }
        else
        {
            ExecuteUnaryScalar(input, output, elementCount, operation);
        }
    }

    /// <summary>
    /// Executes a fused multiply-add operation: output = (input1 * input2) + input3.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe void ExecuteFma(
        Span<byte> input1,
        Span<byte> input2,
        Span<byte> input3,
        Span<byte> output,
        int elementCount)
    {
        if (!_supportsFma)
        {
            // Fallback to separate multiply and add
            ExecuteSeparateMultiplyAdd(input1, input2, input3, output, elementCount);
            return;
        }

        if (input1.Length < elementCount * sizeof(float) ||
            input2.Length < elementCount * sizeof(float) ||
            input3.Length < elementCount * sizeof(float) ||
            output.Length < elementCount * sizeof(float))
        {
            throw new ArgumentException("Buffer sizes are insufficient for the requested element count");
        }

        if (_supportsAvx512 && elementCount >= 16)
        {
            ExecuteFmaAvx512(input1, input2, input3, output, elementCount);
        }
        else if (_supportsAvx2 && elementCount >= 8)
        {
            ExecuteFmaAvx2(input1, input2, input3, output, elementCount);
        }
        else
        {
            ExecuteSeparateMultiplyAdd(input1, input2, input3, output, elementCount);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteAvx512(Span<byte> input1, Span<byte> input2, Span<byte> output, int elementCount)
    {
        fixed (byte* pIn1 = input1)
        fixed (byte* pIn2 = input2)
        fixed (byte* pOut = output)
        {
            var f1 = (float*)pIn1;
            var f2 = (float*)pIn2;
            var fOut = (float*)pOut;

            var vectorCount = elementCount / 16;
            var remainder = elementCount % 16;

            // Process 16 floats at a time with AVX-512
            for (var i = 0; i < vectorCount; i++)
            {
                var v1 = Avx512F.LoadVector512(f1 + i * 16);
                var v2 = Avx512F.LoadVector512(f2 + i * 16);
                var result = Avx512F.Add(v1, v2);
                Avx512F.Store(fOut + i * 16, result);
            }

            // Handle remaining elements
            if (remainder > 0)
            {
                var offset = vectorCount * 16;
                ExecuteScalarRange(f1 + offset, f2 + offset, fOut + offset, remainder);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteAvx2(Span<byte> input1, Span<byte> input2, Span<byte> output, int elementCount)
    {
        fixed (byte* pIn1 = input1)
        fixed (byte* pIn2 = input2)
        fixed (byte* pOut = output)
        {
            var f1 = (float*)pIn1;
            var f2 = (float*)pIn2;
            var fOut = (float*)pOut;

            var vectorCount = elementCount / 8;
            var remainder = elementCount % 8;

            // Process 8 floats at a time with AVX2
            for (var i = 0; i < vectorCount; i++)
            {
                var v1 = Avx.LoadVector256(f1 + i * 8);
                var v2 = Avx.LoadVector256(f2 + i * 8);
                var result = Avx.Add(v1, v2);
                Avx.Store(fOut + i * 8, result);
            }

            // Handle remaining elements
            if (remainder > 0)
            {
                var offset = vectorCount * 8;
                if (remainder >= 4)
                {
                    // Use SSE for 4 elements
                    var v1 = Sse.LoadVector128(f1 + offset);
                    var v2 = Sse.LoadVector128(f2 + offset);
                    var result = Sse.Add(v1, v2);
                    Sse.Store(fOut + offset, result);
                    offset += 4;
                    remainder -= 4;
                }

                if (remainder > 0)
                {
                    ExecuteScalarRange(f1 + offset, f2 + offset, fOut + offset, remainder);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteVector128(Span<byte> input1, Span<byte> input2, Span<byte> output, int elementCount)
    {
        fixed (byte* pIn1 = input1)
        fixed (byte* pIn2 = input2)
        fixed (byte* pOut = output)
        {
            var f1 = (float*)pIn1;
            var f2 = (float*)pIn2;
            var fOut = (float*)pOut;

            var vectorCount = elementCount / 4;
            var remainder = elementCount % 4;

            // Process 4 floats at a time with Vector128
            for (var i = 0; i < vectorCount; i++)
            {
                var v1 = Vector128.Load(f1 + i * 4);
                var v2 = Vector128.Load(f2 + i * 4);
                var result = Vector128.Add(v1, v2);
                result.Store(fOut + i * 4);
            }

            // Handle remaining elements
            if (remainder > 0)
            {
                var offset = vectorCount * 4;
                ExecuteScalarRange(f1 + offset, f2 + offset, fOut + offset, remainder);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteScalar(Span<byte> input1, Span<byte> input2, Span<byte> output, int elementCount)
    {
        fixed (byte* pIn1 = input1)
        fixed (byte* pIn2 = input2)
        fixed (byte* pOut = output)
        {
            ExecuteScalarRange((float*)pIn1, (float*)pIn2, (float*)pOut, elementCount);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ExecuteScalarRange(float* input1, float* input2, float* output, int count)
    {
        for (var i = 0; i < count; i++)
        {
            output[i] = input1[i] + input2[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteUnaryAvx512(Span<byte> input, Span<byte> output, int elementCount, UnaryOperation operation)
    {
        fixed (byte* pIn = input)
        fixed (byte* pOut = output)
        {
            var fIn = (float*)pIn;
            var fOut = (float*)pOut;

            var vectorCount = elementCount / 16;
            var remainder = elementCount % 16;

            for (var i = 0; i < vectorCount; i++)
            {
                var v = Avx512F.LoadVector512(fIn + i * 16);
                var result = operation switch
                {
                    UnaryOperation.Sqrt => Avx512F.Sqrt(v),
                    UnaryOperation.Abs => Vector512.Abs(v),
                    UnaryOperation.Negate => Vector512.Negate(v),
                    _ => v
                };
                Avx512F.Store(fOut + i * 16, result);
            }

            if (remainder > 0)
            {
                var offset = vectorCount * 16;
                ExecuteUnaryScalarRange(fIn + offset, fOut + offset, remainder, operation);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteUnaryAvx2(Span<byte> input, Span<byte> output, int elementCount, UnaryOperation operation)
    {
        fixed (byte* pIn = input)
        fixed (byte* pOut = output)
        {
            var fIn = (float*)pIn;
            var fOut = (float*)pOut;

            var vectorCount = elementCount / 8;
            var remainder = elementCount % 8;

            for (var i = 0; i < vectorCount; i++)
            {
                var v = Avx.LoadVector256(fIn + i * 8);
                var result = operation switch
                {
                    UnaryOperation.Sqrt => Avx.Sqrt(v),
                    UnaryOperation.Abs => Vector256.Abs(v),
                    UnaryOperation.Negate => Vector256.Negate(v),
                    _ => v
                };
                Avx.Store(fOut + i * 8, result);
            }

            if (remainder > 0)
            {
                var offset = vectorCount * 8;
                if (remainder >= 4)
                {
                    var v = Sse.LoadVector128(fIn + offset);
                    var result = operation switch
                    {
                        UnaryOperation.Sqrt => Sse.Sqrt(v),
                        UnaryOperation.Abs => Vector128.Abs(v),
                        UnaryOperation.Negate => Vector128.Negate(v),
                        _ => v
                    };
                    Sse.Store(fOut + offset, result);
                    offset += 4;
                    remainder -= 4;
                }

                if (remainder > 0)
                {
                    ExecuteUnaryScalarRange(fIn + offset, fOut + offset, remainder, operation);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteUnaryVector128(Span<byte> input, Span<byte> output, int elementCount, UnaryOperation operation)
    {
        fixed (byte* pIn = input)
        fixed (byte* pOut = output)
        {
            var fIn = (float*)pIn;
            var fOut = (float*)pOut;

            var vectorCount = elementCount / 4;
            var remainder = elementCount % 4;

            for (var i = 0; i < vectorCount; i++)
            {
                var v = Vector128.Load(fIn + i * 4);
                var result = operation switch
                {
                    UnaryOperation.Sqrt => Vector128.Sqrt(v),
                    UnaryOperation.Abs => Vector128.Abs(v),
                    UnaryOperation.Negate => Vector128.Negate(v),
                    _ => v
                };
                result.Store(fOut + i * 4);
            }

            if (remainder > 0)
            {
                var offset = vectorCount * 4;
                ExecuteUnaryScalarRange(fIn + offset, fOut + offset, remainder, operation);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ExecuteUnaryScalar(Span<byte> input, Span<byte> output, int elementCount, UnaryOperation operation)
    {
        fixed (byte* pIn = input)
        fixed (byte* pOut = output)
        {
            ExecuteUnaryScalarRange((float*)pIn, (float*)pOut, elementCount, operation);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ExecuteUnaryScalarRange(float* input, float* output, int count, UnaryOperation operation)
    {
        for (var i = 0; i < count; i++)
        {
            output[i] = operation switch
            {
                UnaryOperation.Sqrt => MathF.Sqrt(input[i]),
                UnaryOperation.Abs => MathF.Abs(input[i]),
                UnaryOperation.Negate => -input[i],
                UnaryOperation.Exp => MathF.Exp(input[i]),
                UnaryOperation.Log => MathF.Log(input[i]),
                UnaryOperation.Sin => MathF.Sin(input[i]),
                UnaryOperation.Cos => MathF.Cos(input[i]),
                UnaryOperation.Tan => MathF.Tan(input[i]),
                _ => input[i]
            };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFmaAvx512(Span<byte> input1, Span<byte> input2, Span<byte> input3, Span<byte> output, int elementCount)
    {
        fixed (byte* pIn1 = input1)
        fixed (byte* pIn2 = input2)
        fixed (byte* pIn3 = input3)
        fixed (byte* pOut = output)
        {
            var f1 = (float*)pIn1;
            var f2 = (float*)pIn2;
            var f3 = (float*)pIn3;
            var fOut = (float*)pOut;

            var vectorCount = elementCount / 16;
            var remainder = elementCount % 16;

            for (var i = 0; i < vectorCount; i++)
            {
                var v1 = Avx512F.LoadVector512(f1 + i * 16);
                var v2 = Avx512F.LoadVector512(f2 + i * 16);
                var v3 = Avx512F.LoadVector512(f3 + i * 16);
                var result = Avx512F.FusedMultiplyAdd(v1, v2, v3);
                Avx512F.Store(fOut + i * 16, result);
            }

            if (remainder > 0)
            {
                var offset = vectorCount * 16;
                ExecuteFmaScalarRange(f1 + offset, f2 + offset, f3 + offset, fOut + offset, remainder);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe void ExecuteFmaAvx2(Span<byte> input1, Span<byte> input2, Span<byte> input3, Span<byte> output, int elementCount)
    {
        fixed (byte* pIn1 = input1)
        fixed (byte* pIn2 = input2)
        fixed (byte* pIn3 = input3)
        fixed (byte* pOut = output)
        {
            var f1 = (float*)pIn1;
            var f2 = (float*)pIn2;
            var f3 = (float*)pIn3;
            var fOut = (float*)pOut;

            var vectorCount = elementCount / 8;
            var remainder = elementCount % 8;

            for (var i = 0; i < vectorCount; i++)
            {
                var v1 = Avx.LoadVector256(f1 + i * 8);
                var v2 = Avx.LoadVector256(f2 + i * 8);
                var v3 = Avx.LoadVector256(f3 + i * 8);
                var result = Fma.MultiplyAdd(v1, v2, v3);
                Avx.Store(fOut + i * 8, result);
            }

            if (remainder > 0)
            {
                var offset = vectorCount * 8;
                ExecuteFmaScalarRange(f1 + offset, f2 + offset, f3 + offset, fOut + offset, remainder);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ExecuteSeparateMultiplyAdd(Span<byte> input1, Span<byte> input2, Span<byte> input3, Span<byte> output, int elementCount)
    {
        fixed (byte* pIn1 = input1)
        fixed (byte* pIn2 = input2)
        fixed (byte* pIn3 = input3)
        fixed (byte* pOut = output)
        {
            ExecuteFmaScalarRange((float*)pIn1, (float*)pIn2, (float*)pIn3, (float*)pOut, elementCount);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ExecuteFmaScalarRange(float* input1, float* input2, float* input3, float* output, int count)
    {
        for (var i = 0; i < count; i++)
        {
            output[i] = (input1[i] * input2[i]) + input3[i];
        }
    }

    /// <summary>
    /// Gets the maximum number of elements that can be processed in a single vector operation.
    /// </summary>
    public int GetMaxVectorElements() => _preferredVectorWidth / 32; // Assuming 32-bit floats

    /// <summary>
    /// Determines if the specified element count is suitable for vectorization.
    /// </summary>
    public bool IsVectorizationBeneficial(int elementCount)
    {
        var minElements = GetMaxVectorElements();
        return elementCount >= minElements && _simdCapabilities.IsHardwareAccelerated;
    }

    /// <summary>
    /// Gets the optimal work group size for vectorized operations.
    /// </summary>
    public int GetOptimalWorkGroupSize()
    {
        return _preferredVectorWidth switch
        {
            512 => 256,  // AVX-512: process 256 elements (16 vectors) per work group
            256 => 128,  // AVX2: process 128 elements (16 vectors) per work group
            128 => 64,   // SSE: process 64 elements (16 vectors) per work group
            _ => 32      // Fallback
        };
    }
}

/// <summary>
/// Unary operations supported by the SIMD kernel executor.
/// </summary>
public enum UnaryOperation
{
    /// <summary>
    /// The copy
    /// </summary>
    Copy,

    /// <summary>
    /// The SQRT
    /// </summary>
    Sqrt,

    /// <summary>
    /// The abs
    /// </summary>
    Abs,

    /// <summary>
    /// The negate
    /// </summary>
    Negate,

    /// <summary>
    /// The exp
    /// </summary>
    Exp,

    /// <summary>
    /// The log
    /// </summary>
    Log,

    /// <summary>
    /// The sin
    /// </summary>
    Sin,

    /// <summary>
    /// The cos
    /// </summary>
    Cos,

    /// <summary>
    /// The tan
    /// </summary>
    Tan
}
