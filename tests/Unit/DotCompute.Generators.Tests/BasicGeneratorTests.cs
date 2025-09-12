// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using DotCompute.Generators.Kernel;
using DotCompute.Generators.Analyzers;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace DotCompute.Generators.Tests;

/// <summary>
/// Basic tests for DotCompute generators and analyzers.
/// These tests verify that the core functionality works without relying on complex testing frameworks.
/// </summary>
public class BasicGeneratorTests
{
    [Fact]
    public void KernelSourceGenerator_SupportsKernelGeneration()
    {
        var generator = new KernelSourceGenerator();
        Assert.NotNull(generator);
        
        // Test that the generator can be instantiated
        // This is a basic smoke test
    }

    [Fact]
    public void DotComputeKernelAnalyzer_SupportsAllDiagnostics()
    {
        var analyzer = new DotComputeKernelAnalyzer();
        var diagnostics = analyzer.SupportedDiagnostics;
        
        // Should support all 12 diagnostic rules
        Assert.Equal(12, diagnostics.Length);
        
        // Verify all diagnostic IDs are present
        var diagnosticIds = diagnostics.Select(d => d.Id).ToHashSet();
        for (int i = 1; i <= 12; i++)
        {
            var expectedId = $"DC{i:D3}";
            Assert.Contains(expectedId, diagnosticIds);
        }
    }

    [Fact]
    public void Analyzer_DC001_NonStaticMethod_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public void NonStaticKernel(Span<float> data)
    {
        int index = Kernel.ThreadId.X;
        if (index < data.Length)
            data[index] *= 2.0f;
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }
";

        var diagnostics = GetDiagnostics(code);
        var dc001Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC001");
        
        Assert.NotNull(dc001Diagnostic);
        Assert.Equal(DiagnosticSeverity.Error, dc001Diagnostic.Severity);
        Assert.Contains("NonStaticKernel", dc001Diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Analyzer_DC002_InvalidParameter_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void InvalidKernel(object badParam)
    {
        // Invalid parameter type
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
";

        var diagnostics = GetDiagnostics(code);
        var dc002Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC002");
        
        Assert.NotNull(dc002Diagnostic);
        Assert.Equal(DiagnosticSeverity.Error, dc002Diagnostic.Severity);
        Assert.Contains("InvalidKernel", dc002Diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("badParam", dc002Diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Analyzer_DC010_IncorrectThreading_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void LoopKernel(Span<float> data)
    {
        for (int i = 0; i < data.Length; i++)  // Should use Kernel.ThreadId.X
        {
            data[i] *= 2.0f;
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
";

        var diagnostics = GetDiagnostics(code);
        var dc010Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC010");
        
        Assert.NotNull(dc010Diagnostic);
        Assert.Equal(DiagnosticSeverity.Warning, dc010Diagnostic.Severity);
        Assert.Contains("LoopKernel", dc010Diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Analyzer_DC011_MissingBoundsCheck_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void NoBoundsKernel(Span<float> data)
    {
        int index = 0;
        data[index] = 1.0f; // No bounds check
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
";

        var diagnostics = GetDiagnostics(code);
        var dc011Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC011");
        
        Assert.NotNull(dc011Diagnostic);
        Assert.Equal(DiagnosticSeverity.Warning, dc011Diagnostic.Severity);
        Assert.Contains("NoBoundsKernel", dc011Diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Analyzer_ValidKernel_NoErrors()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void ValidKernel(ReadOnlySpan<float> input, Span<float> output)
    {
        int index = Kernel.ThreadId.X;
        if (index >= output.Length)
            return;
        
        output[index] = input[index] * 2.0f;
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }
";

        var diagnostics = GetDiagnostics(code);
        var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        
        Assert.Empty(errorDiagnostics);
    }

    [Fact]
    public void Generator_SimpleKernel_GeneratesBasicOutput()
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
            int index = Kernel.ThreadId.X;
            if (index < result.Length)
                result[index] = a[index] + b[index];
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }
";

        var generatedSources = RunGenerator(code);
        
        Assert.NotEmpty(generatedSources);
        
        // Should generate at least a kernel registry
        var hasRegistry = generatedSources.Any(source => source.HintName == "KernelRegistry.g.cs");
        Assert.True(hasRegistry, "Should generate KernelRegistry.g.cs");
        
        var registry = generatedSources.First(source => source.HintName == "KernelRegistry.g.cs");
        Assert.Contains("AddKernel", registry.SourceText.ToString());
        Assert.Contains("TestApp.SimpleKernels", registry.SourceText.ToString());
    }

    [Fact]
    public void Generator_MultipleKernels_GeneratesAllRegistrations()
    {
        const string code = @"
using System;

namespace TestApp
{
    public class MathKernels
    {
        [Kernel]
        public static void Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            int index = Kernel.ThreadId.X;
            if (index < result.Length)
                result[index] = a[index] + b[index];
        }

        [Kernel]
        public static void Multiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            int index = Kernel.ThreadId.X;
            if (index < result.Length)
                result[index] = a[index] * b[index];
        }

        [Kernel]
        public static void Subtract(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            int index = Kernel.ThreadId.X;
            if (index < result.Length)
                result[index] = a[index] - b[index];
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }
";

        var generatedSources = RunGenerator(code);
        var registry = generatedSources.First(source => source.HintName == "KernelRegistry.g.cs");
        var registryContent = registry.SourceText.ToString();
        
        // Should contain all three kernels
        Assert.Contains("Add", registryContent);
        Assert.Contains("Multiply", registryContent);
        Assert.Contains("Subtract", registryContent);
        
        // Should have 3 kernel registrations
        int kernelCount = CountOccurrences(registryContent, "= new KernelRegistration");
        Assert.Equal(3, kernelCount);
    }

    [Fact]
    public void Generator_Performance_HandlesReasonableLoad()
    {
        // Generate code with 50 kernels to test performance
        var codeBuilder = new System.Text.StringBuilder();
        codeBuilder.AppendLine("using System;");
        codeBuilder.AppendLine("namespace PerfTest {");
        codeBuilder.AppendLine("public class PerfKernels {");
        
        for (int i = 0; i < 50; i++)
        {
            codeBuilder.AppendLine($@"
    [Kernel]
    public static void Kernel{i}(ReadOnlySpan<float> input, Span<float> output)
    {{
        int index = Kernel.ThreadId.X;
        if (index < output.Length)
            output[index] = input[index] * {i + 1}f;
    }}");
        }
        
        codeBuilder.AppendLine("}");
        codeBuilder.AppendLine("}");
        codeBuilder.AppendLine("[System.AttributeUsage(System.AttributeTargets.Method)]");
        codeBuilder.AppendLine("public class KernelAttribute : System.Attribute { }");
        codeBuilder.AppendLine("public static class Kernel { public static ThreadId ThreadId => new(); }");
        codeBuilder.AppendLine("public struct ThreadId { public int X => 0; }");

        var code = codeBuilder.ToString();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var generatedSources = RunGenerator(code);
        stopwatch.Stop();

        // Should complete within reasonable time
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
            $"Generation took {stopwatch.ElapsedMilliseconds}ms, expected < 5000ms");

        // Should generate registry with all kernels
        var registry = generatedSources.First(source => source.HintName == "KernelRegistry.g.cs");
        var registryContent = registry.SourceText.ToString();
        int kernelCount = CountOccurrences(registryContent, "= new KernelRegistration");
        Assert.Equal(50, kernelCount);
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "test",
            syntaxTrees: new[] { tree },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Span<>).Assembly.Location),
            }
        );

        var analyzer = new DotComputeKernelAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        
        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
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
        
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        
        var runResult = driver.GetRunResult();
        return runResult.Results[0].GeneratedSources;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}