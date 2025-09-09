// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Examples;

/// <summary>
/// Demonstration of how the DotCompute Roslyn analyzers work.
/// This file shows examples of code that would trigger various analyzer rules.
/// </summary>
public static class AnalyzerDemo
{
    // DC001: This will trigger "Kernel methods must be static" 
    // (if [Kernel] attribute was present)
    // [Kernel]
    // public void NonStaticKernel(float[] data) { }

    // DC002: This will trigger "Kernel method has invalid parameters"
    /// <summary>Example demonstrating DC002 diagnostic for invalid parameter types</summary>
    [Kernel]
    public static void InvalidParameterKernel()  // ← DC002: object not supported (parameter removed for compilation)
    {
        // Method body
    }

    // DC007: This will trigger "Method should have [Kernel] attribute"
    /// <summary>Example demonstrating DC007 diagnostic for missing [Kernel] attribute</summary>
    public static void LooksLikeKernel(float[] input, float[] output)  // ← DC007: Missing [Kernel]
    {
        for (var i = 0; i < input.Length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }

    // DC010: This will trigger "Kernel uses incorrect threading model"
    [Kernel]
    public static void IncorrectThreadingKernel(Span<float> data)  // ← DC010: No Kernel.ThreadId usage
    {
        for (int i = 0; i < data.Length; i++)  // ← Should use Kernel.ThreadId.X
        {
            data[i] *= 2.0f;
        }
    }

    // DC011: This will trigger "Kernel missing bounds check"
    [Kernel]
    public static void NoBoundsCheckKernel(Span<float> data)  // ← DC011: Missing bounds check
    {
        int index = GetThreadIndex();
        data[index] = data[index] * 2.0f;  // ← No bounds validation
    }

    // ✅ GOOD EXAMPLE: This kernel follows all best practices
    [Kernel(Backends = KernelBackends.All, VectorSize = 8)]
    public static void OptimalKernel(ReadOnlySpan<float> input, Span<float> output)
    {
        int index = ThreadId.X;  // ✅ Correct threading model (DC010 compliant)


        if (index >= output.Length)
        {
            return;  // ✅ Bounds check (DC011 compliant)
        }

        // Simple, vectorizable operation (DC004 - can benefit from SIMD)

        output[index] = input[index] * 2.0f + 1.0f;
    }

    // ✅ GOOD EXAMPLE: Complex kernel with optimizations
    [Kernel(Backends = KernelBackends.CUDA | KernelBackends.CPU)]
    public static void MatrixMultiplyKernel(
        ReadOnlySpan<float> matrixA,   // ✅ Correct parameter types (DC002 compliant)
        ReadOnlySpan<float> matrixB,
        Span<float> result,
        int width,
        int height)
    {
        int row = ThreadId.Y;    // ✅ 2D threading model
        int col = ThreadId.X;


        if (row >= height || col >= width)
        {
            return;  // ✅ Bounds validation
        }


        float sum = 0.0f;
        for (int k = 0; k < width; k++)
        {
            sum += matrixA[row * width + k] * matrixB[k * width + col];
        }
        result[row * width + col] = sum;
    }

    // This will trigger multiple analyzer warnings for demonstration
    [Kernel]
    public static void ProblematicKernel(float[] data)  // ← DC002: Should use Span<float>
    {
        // DC010: Not using Kernel.ThreadId
        for (int i = 0; i < data.Length; i++)
        {
            // DC011: No bounds check on array access
            // DC005: Potential memory access pattern issues
            data[i] = CalculateComplexValue(data[i], data[i + 1]);  // ← Unsafe access
        }

        // DC006: Too many local variables (register spilling)
        #pragma warning disable CS0219 // Variable is assigned but never used
        float var1 = 1.0f, var2 = 2.0f, var3 = 3.0f, var4 = 4.0f;
        float var5 = 5.0f, var6 = 6.0f, var7 = 7.0f, var8 = 8.0f;
        float var9 = 9.0f, var10 = 10.0f, var11 = 11.0f, var12 = 12.0f;
        float var13 = 13.0f, var14 = 14.0f, var15 = 15.0f, var16 = 16.0f;
        float var17 = 17.0f, var18 = 18.0f;  // ← DC006: > 16 variables
        #pragma warning restore CS0219
    }

    private static float CalculateComplexValue(float a, float b) => a * b + a - b;
    private static int GetThreadIndex() => 0; // Placeholder

    // Fake Kernel attribute and threading model for demo
    private class KernelAttribute : Attribute

    {
        public KernelBackends Backends { get; set; } = KernelBackends.CPU;
        public int VectorSize { get; set; } = 8;
    }

    [Flags]
    private enum KernelBackends
    {
        CPU = 1,
        CUDA = 2,
        Metal = 4,
        OpenCL = 8,
        All = CPU | CUDA | Metal | OpenCL
    }

    private static class Kernel
    {
        public static ThreadId ThreadId => new ThreadId();
    }

    private struct ThreadId
    {
        public static int X => 0;
        public static int Y => 0;
        public static int Z => 0;
    }
}

/// <summary>
/// Examples showing the automated code fixes in action.
/// </summary>
public static class CodeFixExamples
{
    // BEFORE: Code that triggers analyzers
    // [Kernel]
    // public void NonStaticMethod(float[] data) { ... }  // ← DC001 + DC002

    // AFTER: Code fix applied

    // [Kernel] // Commented out due to local Kernel conflict
    public static void FixedMethod(Span<float> data)  // ✅ Now static + Span<T>
    {
        int index = 0; // Kernel.ThreadId.X;  // ✅ Added by code fixer - using 0 as Kernel is not accessible
        if (index >= data.Length)
        {
            return;  // ✅ Added by code fixer
        }


        data[index] *= 2.0f;
    }

    // BEFORE: Missing [Kernel] attribute
    // public static void ProcessData(Span<float> input, Span<float> output) { ... }

    // AFTER: Code fixer adds attribute

    // [Kernel]  // ✅ Added by code fixer - Commented out due to local Kernel conflict
    public static void ProcessData(Span<float> input, Span<float> output)
    {
        int index = 0; // Kernel.ThreadId.X; - using 0 as Kernel is not accessible
        if (index >= output.Length)
        {
            return;
        }


        output[index] = input[index] * 2.0f;
    }
}

/// <summary>
/// Performance analysis examples showing how analyzers detect optimization opportunities.
/// </summary>
public static class PerformanceAnalysisExamples
{
    // DC004: This will suggest vectorization opportunities
    [Kernel]
    public static void VectorizableLoop(ReadOnlySpan<float> input, Span<float> output)
    {
        int index = ThreadId.X;
        if (index >= output.Length)
        {
            return;
        }

        // ✅ DC004: This pattern can benefit from SIMD vectorization

        output[index] = input[index] * 2.0f + 1.0f;
    }

    // DC005: This will warn about suboptimal memory access
    [Kernel]

    public static void SuboptimalMemoryAccess(Span<float> data)
    {
        int index = ThreadId.X;
        if (index >= data.Length)
        {
            return;
        }

        // ⚠️ DC005: Non-coalesced memory access pattern

        int strideIndex = (index * 7) % data.Length;  // Non-sequential access
        data[strideIndex] = data[index] * 2.0f;
    }

    // DC012: This will suggest better backend selection
    [Kernel(Backends = KernelBackends.CUDA)]  // ⚠️ DC012: Simple operation doesn't need GPU
    public static void SimpleOperation(Span<float> data)
    {
        int index = ThreadId.X;
        if (index >= data.Length)
        {
            return;
        }


        data[index] += 1.0f;  // Simple operation - CPU might be better
    }

    private class KernelAttribute : Attribute

    {
        public KernelBackends Backends { get; set; } = KernelBackends.CPU;
        public int VectorSize { get; set; } = 8;
    }

    [Flags]
    private enum KernelBackends
    {
        CPU = 1,
        CUDA = 2,
        Metal = 4,

        OpenCL = 8,
        All = CPU | CUDA | Metal | OpenCL
    }

    private static class Kernel
    {
        public static ThreadId ThreadId => new ThreadId();
    }

    private struct ThreadId
    {
        public static int X => 0;
    }
}