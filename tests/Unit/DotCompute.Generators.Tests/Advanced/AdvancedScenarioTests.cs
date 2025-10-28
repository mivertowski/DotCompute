// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DotCompute.Generators.Kernel;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace DotCompute.Generators.Tests.Advanced;

/// <summary>
/// Advanced scenario tests for complex kernel patterns.
/// </summary>
public sealed class AdvancedScenarioTests
{
    [Fact]
    public void Advanced_MatrixMultiplication_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class MatrixKernels
    {
        [Kernel]
        public static void MatMul(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c, int width)
        {
            int row = Kernel.ThreadId.Y;
            int col = Kernel.ThreadId.X;
            int idx = row * width + col;

            if (idx < c.Length)
            {
                float sum = 0.0f;
                for (int k = 0; k < width; k++)
                {
                    sum += a[row * width + k] * b[k * width + col];
                }
                c[idx] = sum;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; public int Y => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_ConvolutionKernel_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ConvKernels
    {
        [Kernel]
        public static void Convolution2D(
            ReadOnlySpan<float> input,
            ReadOnlySpan<float> kernel,
            Span<float> output,
            int width, int height, int kernelSize)
        {
            int x = Kernel.ThreadId.X;
            int y = Kernel.ThreadId.Y;
            int outIdx = y * width + x;

            if (outIdx < output.Length)
            {
                float sum = 0.0f;
                int halfKernel = kernelSize / 2;

                for (int ky = 0; ky < kernelSize; ky++)
                {
                    for (int kx = 0; kx < kernelSize; kx++)
                    {
                        int iy = y + ky - halfKernel;
                        int ix = x + kx - halfKernel;

                        if (ix >= 0 && ix < width && iy >= 0 && iy < height)
                        {
                            int inIdx = iy * width + ix;
                            int kIdx = ky * kernelSize + kx;
                            sum += input[inIdx] * kernel[kIdx];
                        }
                    }
                }

                output[outIdx] = sum;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; public int Y => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_ReductionKernel_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ReductionKernels
    {
        [Kernel]
        public static void ParallelSum(ReadOnlySpan<float> input, Span<float> partialSums, int stride)
        {
            int idx = Kernel.ThreadId.X;
            int blockSize = Kernel.BlockDim.X;

            if (idx < partialSums.Length)
            {
                float sum = 0.0f;
                for (int i = idx; i < input.Length; i += stride)
                {
                    sum += input[i];
                }
                partialSums[idx] = sum;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel
{
    public static ThreadId ThreadId => new();
    public static BlockDim BlockDim => new();
}
public struct ThreadId { public int X => 0; }
public struct BlockDim { public int X => 256; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_PrefixSumKernel_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class PrefixSumKernels
    {
        [Kernel]
        public static void PrefixSum(Span<float> data, int offset)
        {
            int idx = Kernel.ThreadId.X;

            if (idx >= offset && idx < data.Length)
            {
                data[idx] += data[idx - offset];
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_HistogramKernel_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class HistogramKernels
    {
        [Kernel]
        public static void ComputeHistogram(ReadOnlySpan<int> data, Span<int> histogram, int numBins)
        {
            int idx = Kernel.ThreadId.X;

            if (idx < data.Length)
            {
                int value = data[idx];
                if (value >= 0 && value < numBins)
                {
                    // Note: Atomic operations would be needed for real implementation
                    histogram[value]++;
                }
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_ImageBlurKernel_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ImageKernels
    {
        [Kernel]
        public static void GaussianBlur(
            ReadOnlySpan<float> input,
            Span<float> output,
            int width, int height)
        {
            int x = Kernel.ThreadId.X;
            int y = Kernel.ThreadId.Y;
            int idx = y * width + x;

            if (x > 0 && x < width - 1 && y > 0 && y < height - 1)
            {
                float sum = 0.0f;
                sum += input[idx - width - 1] * 1.0f / 16.0f;
                sum += input[idx - width] * 2.0f / 16.0f;
                sum += input[idx - width + 1] * 1.0f / 16.0f;
                sum += input[idx - 1] * 2.0f / 16.0f;
                sum += input[idx] * 4.0f / 16.0f;
                sum += input[idx + 1] * 2.0f / 16.0f;
                sum += input[idx + width - 1] * 1.0f / 16.0f;
                sum += input[idx + width] * 2.0f / 16.0f;
                sum += input[idx + width + 1] * 1.0f / 16.0f;

                output[idx] = sum;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; public int Y => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_TransposeKernel_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class TransposeKernels
    {
        [Kernel]
        public static void Transpose(ReadOnlySpan<float> input, Span<float> output, int width, int height)
        {
            int x = Kernel.ThreadId.X;
            int y = Kernel.ThreadId.Y;
            int inIdx = y * width + x;
            int outIdx = x * height + y;

            if (inIdx < input.Length && outIdx < output.Length)
            {
                output[outIdx] = input[inIdx];
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; public int Y => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_BitonicSortKernel_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class SortKernels
    {
        [Kernel]
        public static void BitonicSortStep(Span<float> data, int stage, int step)
        {
            int idx = Kernel.ThreadId.X;
            int ixj = idx ^ step;

            if (ixj > idx && idx < data.Length && ixj < data.Length)
            {
                bool ascending = ((idx & stage) == 0);
                if ((data[idx] > data[ixj]) == ascending)
                {
                    float temp = data[idx];
                    data[idx] = data[ixj];
                    data[ixj] = temp;
                }
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_FFTKernel_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class FFTKernels
    {
        [Kernel]
        public static void FFTStage(Span<float> real, Span<float> imag, int stage, int size)
        {
            int idx = Kernel.ThreadId.X;

            if (idx < size / 2)
            {
                int k = idx % (1 << stage);
                int j = 2 * idx - k;
                int i = j + (1 << stage);

                float angle = -2.0f * 3.14159265f * k / (float)(1 << (stage + 1));
                float wr = MathF.Cos(angle);
                float wi = MathF.Sin(angle);

                float tr = wr * real[i] - wi * imag[i];
                float ti = wr * imag[i] + wi * real[i];

                real[i] = real[j] - tr;
                imag[i] = imag[j] - ti;
                real[j] = real[j] + tr;
                imag[j] = imag[j] + ti;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_RayTracingKernel_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class RayTracingKernels
    {
        [Kernel]
        public static void RayTrace(
            Span<float> pixels,
            ReadOnlySpan<float> spheres,
            int width, int height)
        {
            int x = Kernel.ThreadId.X;
            int y = Kernel.ThreadId.Y;
            int idx = y * width + x;

            if (idx < pixels.Length)
            {
                float rayX = (x - width / 2.0f) / width;
                float rayY = (y - height / 2.0f) / height;
                float rayZ = 1.0f;

                float minDist = float.MaxValue;
                for (int i = 0; i < spheres.Length; i += 4)
                {
                    float sx = spheres[i];
                    float sy = spheres[i + 1];
                    float sz = spheres[i + 2];
                    float sr = spheres[i + 3];

                    float dx = rayX - sx;
                    float dy = rayY - sy;
                    float dz = rayZ - sz;
                    float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

                    if (dist < sr && dist < minDist)
                    {
                        minDist = dist;
                        pixels[idx] = 1.0f - (dist / sr);
                    }
                }
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; public int Y => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_NBodySimulation_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class PhysicsKernels
    {
        [Kernel]
        public static void NBodyForces(
            ReadOnlySpan<float> posX, ReadOnlySpan<float> posY, ReadOnlySpan<float> posZ,
            ReadOnlySpan<float> mass,
            Span<float> forceX, Span<float> forceY, Span<float> forceZ,
            float softening)
        {
            int i = Kernel.ThreadId.X;

            if (i < forceX.Length)
            {
                float fx = 0.0f, fy = 0.0f, fz = 0.0f;

                for (int j = 0; j < mass.Length; j++)
                {
                    if (i != j)
                    {
                        float dx = posX[j] - posX[i];
                        float dy = posY[j] - posY[i];
                        float dz = posZ[j] - posZ[i];

                        float distSqr = dx * dx + dy * dy + dz * dz + softening;
                        float invDist = 1.0f / MathF.Sqrt(distSqr);
                        float invDist3 = invDist * invDist * invDist;

                        fx += dx * mass[j] * invDist3;
                        fy += dy * mass[j] * invDist3;
                        fz += dz * mass[j] * invDist3;
                    }
                }

                forceX[i] = fx;
                forceY[i] = fy;
                forceZ[i] = fz;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_MandelbrotSet_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class FractalKernels
    {
        [Kernel]
        public static void Mandelbrot(
            Span<int> iterations,
            int width, int height,
            float xMin, float xMax, float yMin, float yMax,
            int maxIterations)
        {
            int x = Kernel.ThreadId.X;
            int y = Kernel.ThreadId.Y;
            int idx = y * width + x;

            if (idx < iterations.Length)
            {
                float cx = xMin + (xMax - xMin) * x / width;
                float cy = yMin + (yMax - yMin) * y / height;

                float zx = 0.0f, zy = 0.0f;
                int iter = 0;

                while (zx * zx + zy * zy < 4.0f && iter < maxIterations)
                {
                    float tmp = zx * zx - zy * zy + cx;
                    zy = 2.0f * zx * zy + cy;
                    zx = tmp;
                    iter++;
                }

                iterations[idx] = iter;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; public int Y => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Advanced_WaveletTransform_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class WaveletKernels
    {
        [Kernel]
        public static void HaarWavelet(ReadOnlySpan<float> input, Span<float> output, int stride)
        {
            int idx = Kernel.ThreadId.X;

            if (idx * 2 + 1 < input.Length)
            {
                float a = input[idx * 2];
                float b = input[idx * 2 + 1];

                int lowIdx = idx;
                int highIdx = idx + stride / 2;

                if (lowIdx < output.Length && highIdx < output.Length)
                {
                    output[lowIdx] = (a + b) / MathF.Sqrt(2.0f);
                    output[highIdx] = (a - b) / MathF.Sqrt(2.0f);
                }
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    private static ImmutableArray<GeneratedSourceResult> RunGenerator(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "test",
            syntaxTrees: new[] { tree },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Span<>).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new KernelSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var runResult = driver.GetRunResult();
        return runResult.Results.Length > 0 ? runResult.Results[0].GeneratedSources : ImmutableArray<GeneratedSourceResult>.Empty;
    }
}
