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
/// Tests for the executable metadata the generator emits into <c>KernelRegistry.g.cs</c>
/// (issue #182): real CUDA-C source, dimensionality, CPU invoker, and parameter descriptors.
/// The end-to-end target is the reporter's <c>TransposeNaive</c> 2-D kernel.
/// </summary>
public sealed class KernelExecutionMetadataTests
{
    private const string TransposeKernel = @"
using System;
namespace TestApp
{
    public static class Transpose
    {
        [Kernel(Backends = KernelBackends.CPU | KernelBackends.CUDA, VectorSize = 8, IsParallel = true)]
        public static void TransposeNaive(ReadOnlySpan<float> input, Span<float> output, int rows, int cols)
        {
            int row = KernelContext.ThreadId.Y + KernelContext.BlockId.Y * KernelContext.BlockDim.Y;
            int col = KernelContext.ThreadId.X + KernelContext.BlockId.X * KernelContext.BlockDim.X;
            if (row < rows && col < cols)
                output[col * rows + row] = input[row * cols + col];
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public sealed class KernelAttribute : System.Attribute
{
    public KernelBackends Backends { get; set; }
    public int VectorSize { get; set; }
    public bool IsParallel { get; set; }
}

[System.Flags]
public enum KernelBackends { CPU = 1, CUDA = 2, Metal = 4 }

public static class KernelContext
{
    public static Index3 ThreadId => default;
    public static Index3 BlockId => default;
    public static Index3 BlockDim => default;
    public static Index3 GridDim => default;
}

public struct Index3 { public int X => 0; public int Y => 0; public int Z => 0; }
";

    [Fact]
    public void Generator_TransposeNaive_EmitsRealCudaSource()
    {
        var registry = GetRegistry();

        // Real CUDA-C, not a placeholder.
        Assert.DoesNotContain("placeholder", registry);
        Assert.Contains("__global__ void TransposeNaive(const float* input, float* output, int rows, int cols)", registry);
        Assert.Contains("int row = threadIdx.y + blockIdx.y * blockDim.y;", registry);
        Assert.Contains("int col = threadIdx.x + blockIdx.x * blockDim.x;", registry);
        Assert.Contains("threadIdx.x", registry);
        Assert.Contains("output[col * rows + row] = input[row * cols + col];", registry);

        // CUDA entry point = the __global__ function name.
        Assert.Contains("CudaEntryPoint = \"TransposeNaive\"", registry);
    }

    [Fact]
    public void Generator_TransposeNaive_DetectsTwoDimensions()
    {
        var registry = GetRegistry();
        Assert.Contains("Dimensions = 2", registry);
    }

    [Fact]
    public void Generator_TransposeNaive_EmitsCpuInvokerWithSubstitutedBody()
    {
        var registry = GetRegistry();

        // A CpuInvoker class wired up as the metadata delegate.
        Assert.Contains("CpuInvoker", registry);
        Assert.Contains("TransposeNaiveCpuInvoker", registry);
        Assert.Contains("CpuInvoker = (System.Action<object[], int, int>)", registry);
        Assert.Contains("public static void Invoke(object[] args, int start, int end)", registry);

        // Typed span/scalar reconstruction from args.
        Assert.Contains("var input = new System.ReadOnlySpan<float>((float[])args[0]);", registry);
        Assert.Contains("var output = new System.Span<float>((float[])args[1]);", registry);
        Assert.Contains("var rows = (int)args[2];", registry);
        Assert.Contains("var cols = (int)args[3];", registry);

        // 2-D coordinate reconstruction from trailing integer extents (cols=args[3], rows=args[2]).
        Assert.Contains("int __xext = (int)args[3];", registry);
        Assert.Contains("int __yext = (int)args[2];", registry);
        Assert.Contains("int __x = __i % __xext;", registry);
        Assert.Contains("int __y = __i / __xext;", registry);

        // KernelContext intrinsics substituted in the verbatim body.
        Assert.Contains("__y + 0 * __yext", registry);
        Assert.Contains("__x + 0 * __xext", registry);
    }

    [Fact]
    public void Generator_TransposeNaive_EmitsParameterDescriptors()
    {
        var registry = GetRegistry();

        Assert.Contains("new KernelParam { Name = \"input\", IsBuffer = true, IsReadOnly = true, ElementType = \"float\" }", registry);
        Assert.Contains("new KernelParam { Name = \"output\", IsBuffer = true, IsReadOnly = false, ElementType = \"float\" }", registry);
        Assert.Contains("new KernelParam { Name = \"rows\", IsBuffer = false, IsReadOnly = false, ElementType = \"int\" }", registry);
        Assert.Contains("new KernelParam { Name = \"cols\", IsBuffer = false, IsReadOnly = false, ElementType = \"int\" }", registry);
    }

    [Fact]
    public void Generator_TransposeNaive_ExposesGetAllKernels()
    {
        var registry = GetRegistry();
        Assert.Contains("public static IReadOnlyList<KernelMetadata> GetAllKernels()", registry);
    }

    private static string GetRegistry()
    {
        var generatedSources = RunGenerator(TransposeKernel);
        var registry = generatedSources.FirstOrDefault(s => s.HintName == "KernelRegistry.g.cs");
        Assert.False(string.IsNullOrEmpty(registry.HintName), "KernelRegistry.g.cs was not generated");
        return registry.SourceText.ToString();
    }

    private static ImmutableArray<GeneratedSourceResult> RunGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CSharpCompilation.Create(
            "test",
            syntaxTrees: new[] { tree },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Span<>).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var generator = new KernelSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var runResult = driver.GetRunResult();
        return runResult.Results.Length > 0 ? runResult.Results[0].GeneratedSources : ImmutableArray<GeneratedSourceResult>.Empty;
    }
}
