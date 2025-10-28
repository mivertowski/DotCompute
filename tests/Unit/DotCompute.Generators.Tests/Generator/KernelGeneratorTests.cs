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
/// Comprehensive tests for KernelSourceGenerator covering various kernel patterns.
/// </summary>
public sealed class KernelGeneratorTests
{
    [Fact]
    public void Generator_SimpleKernel_GeneratesKernelRegistration()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class SimpleKernels
    {
        [Kernel]
        public static void AddKernel(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < result.Length)
                result[idx] = a[idx] + b[idx];
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);

        Assert.NotEmpty(generatedSources);

        var registry = generatedSources.FirstOrDefault(s => s.HintName.Contains("KernelRegistry"));
        Assert.NotNull(registry);

        var registryContent = registry.SourceText.ToString();
        Assert.Contains("AddKernel", registryContent);
        Assert.Contains("TestApp.SimpleKernels", registryContent);
    }

    [Fact]
    public void Generator_MultipleKernelsInClass_GeneratesAllRegistrations()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class MathKernels
    {
        [Kernel]
        public static void Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> r)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < r.Length) r[idx] = a[idx] + b[idx];
        }

        [Kernel]
        public static void Multiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> r)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < r.Length) r[idx] = a[idx] * b[idx];
        }

        [Kernel]
        public static void Subtract(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> r)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < r.Length) r[idx] = a[idx] - b[idx];
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        var registry = generatedSources.First(s => s.HintName.Contains("KernelRegistry"));
        var content = registry.SourceText.ToString();

        Assert.Contains("Add", content);
        Assert.Contains("Multiply", content);
        Assert.Contains("Subtract", content);
    }

    [Fact]
    public void Generator_KernelWithScalarParameters_GeneratesCorrectSignature()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ScalarKernels
    {
        [Kernel]
        public static void Scale(ReadOnlySpan<float> input, Span<float> output, float scale)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < output.Length)
                output[idx] = input[idx] * scale;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        var registry = generatedSources.First(s => s.HintName.Contains("KernelRegistry"));
        var content = registry.SourceText.ToString();

        Assert.Contains("Scale", content);
        Assert.Contains("float", content);
    }

    [Fact]
    public void Generator_KernelWithDifferentDataTypes_GeneratesForEachType()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class TypedKernels
    {
        [Kernel]
        public static void FloatKernel(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length) data[idx] *= 2.0f;
        }

        [Kernel]
        public static void DoubleKernel(Span<double> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length) data[idx] *= 2.0;
        }

        [Kernel]
        public static void IntKernel(Span<int> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length) data[idx] *= 2;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        var registry = generatedSources.First(s => s.HintName.Contains("KernelRegistry"));
        var content = registry.SourceText.ToString();

        Assert.Contains("FloatKernel", content);
        Assert.Contains("DoubleKernel", content);
        Assert.Contains("IntKernel", content);
    }

    [Fact]
    public void Generator_NestedClass_GeneratesWithFullyQualifiedName()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class OuterClass
    {
        public class NestedKernels
        {
            [Kernel]
            public static void NestedKernel(Span<float> data)
            {
                int idx = Kernel.ThreadId.X;
                if (idx < data.Length) data[idx] *= 2.0f;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        var registry = generatedSources.First(s => s.HintName.Contains("KernelRegistry"));
        var content = registry.SourceText.ToString();

        Assert.Contains("NestedKernel", content);
        Assert.Contains("TestApp.OuterClass", content);
    }

    [Fact]
    public void Generator_KernelInMultipleNamespaces_GeneratesForAll()
    {
        const string code = @"
using System;
namespace App.Math
{
    public class MathKernels
    {
        [Kernel]
        public static void MathKernel(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length) data[idx] *= 2.0f;
        }
    }
}

namespace App.Physics
{
    public class PhysicsKernels
    {
        [Kernel]
        public static void PhysicsKernel(Span<float> data)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < data.Length) data[idx] *= 0.5f;
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        var registry = generatedSources.First(s => s.HintName.Contains("KernelRegistry"));
        var content = registry.SourceText.ToString();

        Assert.Contains("MathKernel", content);
        Assert.Contains("PhysicsKernel", content);
        Assert.Contains("App.Math", content);
        Assert.Contains("App.Physics", content);
    }

    [Fact]
    public void Generator_KernelWithMultipleSpanParameters_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class MultiSpanKernels
    {
        [Kernel]
        public static void TripleAdd(
            ReadOnlySpan<float> a,
            ReadOnlySpan<float> b,
            ReadOnlySpan<float> c,
            Span<float> result)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < result.Length)
                result[idx] = a[idx] + b[idx] + c[idx];
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        var registry = generatedSources.First(s => s.HintName.Contains("KernelRegistry"));
        var content = registry.SourceText.ToString();

        Assert.Contains("TripleAdd", content);
    }

    [Fact]
    public void Generator_EmptyKernelBody_StillGeneratesRegistration()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class EmptyKernels
    {
        [Kernel]
        public static void EmptyKernel(Span<float> data)
        {
            // Empty body
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var generatedSources = RunGenerator(code);
        var registry = generatedSources.FirstOrDefault(s => s.HintName.Contains("KernelRegistry"));

        Assert.NotNull(registry);
        var content = registry.SourceText.ToString();
        Assert.Contains("EmptyKernel", content);
    }

    [Fact]
    public void Generator_KernelWithComplexArithmetic_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ComplexKernels
    {
        [Kernel]
        public static void ComplexMath(ReadOnlySpan<float> input, Span<float> output)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < output.Length)
            {
                float x = input[idx];
                output[idx] = (x * x + 2.0f * x + 1.0f) / (x + 1.0f);
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        var registry = generatedSources.First(s => s.HintName.Contains("KernelRegistry"));
        var content = registry.SourceText.ToString();

        Assert.Contains("ComplexMath", content);
    }

    [Fact]
    public void Generator_KernelWithConditionalLogic_GeneratesCorrectly()
    {
        const string code = @"
using System;
namespace TestApp
{
    public class ConditionalKernels
    {
        [Kernel]
        public static void Clamp(ReadOnlySpan<float> input, Span<float> output, float min, float max)
        {
            int idx = Kernel.ThreadId.X;
            if (idx < output.Length)
            {
                float value = input[idx];
                if (value < min) value = min;
                if (value > max) value = max;
                output[idx] = value;
            }
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var generatedSources = RunGenerator(code);
        var registry = generatedSources.First(s => s.HintName.Contains("KernelRegistry"));
        var content = registry.SourceText.ToString();

        Assert.Contains("Clamp", content);
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
