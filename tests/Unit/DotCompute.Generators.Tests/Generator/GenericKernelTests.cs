// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DotCompute.Generators.Kernel;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace DotCompute.Generators.Tests.Generator;

/// <summary>
/// Tests for generic kernel patterns and edge cases.
/// </summary>
public sealed class GenericKernelTests
{
    [Fact]
    public void Generator_KernelWithLocalVariables_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class LocalVarKernels
    {
        [Kernel]
        public static void LocalVars(ReadOnlySpan<float> input, Span<float> output)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < output.Length)
            {
                float temp1 = input[idx];
                float temp2 = temp1 * 2.0f;
                float temp3 = temp2 + 1.0f;
                output[idx] = temp3;
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
    public void Generator_KernelWithMathFunctions_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class MathFuncKernels
    {
        [Kernel]
        public static void MathFunctions(ReadOnlySpan<float> input, Span<float> output)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < output.Length)
            {
                float x = input[idx];
                output[idx] = MathF.Sqrt(x * x + 1.0f);
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
    public void Generator_KernelWithConstantValues_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ConstantKernels
    {
        [Kernel]
        public static void UseConstants(ReadOnlySpan<float> input, Span<float> output)
        {
            const float PI = 3.14159f;
            const float TWO_PI = 2.0f * PI;

            int idx = Kernel.ThreadId.X;
            if (idx < output.Length)
            {
                output[idx] = input[idx] * TWO_PI;
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
    public void Generator_KernelWithBitwiseOperations_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class BitwiseKernels
    {
        [Kernel]
        public static void BitwiseOps(ReadOnlySpan<int> input, Span<int> output)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < output.Length)
            {
                int value = input[idx];
                output[idx] = (value << 1) | (value >> 1) & 0xFF;
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
    public void Generator_KernelWithTernaryOperator_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class TernaryKernels
    {
        [Kernel]
        public static void Ternary(ReadOnlySpan<float> input, Span<float> output)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < output.Length)
            {
                output[idx] = input[idx] > 0 ? input[idx] : -input[idx];
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
    public void Generator_KernelWithMultipleReturns_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class EarlyReturnKernels
    {
        [Kernel]
        public static void EarlyReturn(ReadOnlySpan<float> input, Span<float> output)
        {
            int idx = Kernel.ThreadId.X;
            if (idx >= output.Length) return;
            if (input[idx] < 0) return;

            output[idx] = input[idx] * 2.0f;
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
    public void Generator_KernelWithSwitchStatement_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class SwitchKernels
    {
        [Kernel]
        public static void SwitchCase(ReadOnlySpan<int> input, Span<float> output, int mode)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < output.Length)
            {
                int value = input[idx];
                switch (mode)
                {
                    case 1:
                        output[idx] = value * 2.0f;
                        break;
                    case 2:
                        output[idx] = value + 10.0f;
                        break;
                    default:
                        output[idx] = value;
                        break;
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
    public void Generator_KernelWithThreadId2D_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ThreadId2DKernels
    {
        [Kernel]
        public static void Use2DThreading(Span<float> data, int width)
        {
            int x = Kernel.ThreadId.X;
            int y = Kernel.ThreadId.Y;
            int idx = y * width + x;

            if (idx < data.Length)
            {
                data[idx] = x + y;
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
    public void Generator_KernelWithThreadId3D_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ThreadId3DKernels
    {
        [Kernel]
        public static void Use3DThreading(Span<float> data, int width, int height)
        {
            int x = Kernel.ThreadId.X;
            int y = Kernel.ThreadId.Y;
            int z = Kernel.ThreadId.Z;
            int idx = z * width * height + y * width + x;

            if (idx < data.Length)
            {
                data[idx] = x + y + z;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; public int Y => 0; public int Z => 0; }";

        var generatedSources = RunGenerator(code);
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Generator_KernelWithMinMax_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class MinMaxKernels
    {
        [Kernel]
        public static void MinMaxOps(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> output)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < output.Length)
            {
                float min = MathF.Min(a[idx], b[idx]);
                float max = MathF.Max(a[idx], b[idx]);
                output[idx] = max - min;
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
