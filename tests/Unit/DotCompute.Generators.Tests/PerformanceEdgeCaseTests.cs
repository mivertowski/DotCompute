// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using DotCompute.Generators.Kernel;
using DotCompute.Generators.Analyzers;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Xunit;

namespace DotCompute.Generators.Tests;

/// <summary>
/// Performance, memory, and edge case tests for generators and analyzers.
/// </summary>
public class PerformanceEdgeCaseTests
{
    [Fact]
    public void Performance_GeneratorHandles500Kernels_WithinTimeLimit()
    {
        // Generate code with 500 kernels to test scalability
        var code = GenerateLargeKernelFile(500);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var generatedSources = RunGenerator(code);
        stopwatch.Stop();

        // Should complete within 10 seconds for 500 kernels
        Assert.True(stopwatch.ElapsedMilliseconds < 10000, 
            $"Generation took {stopwatch.ElapsedMilliseconds}ms, expected < 10000ms");

        Assert.NotEmpty(generatedSources);
        var registry = generatedSources.First(source => source.HintName == "KernelRegistry.g.cs");
        var registryContent = registry.SourceText.ToString();
        
        // Should contain all 500 kernels
        int kernelCount = CountOccurrences(registryContent, "= new KernelRegistration");
        Assert.Equal(500, kernelCount);
    }

    [Fact]
    public void Performance_AnalyzerHandles1000Methods_WithinTimeLimit()
    {
        // Generate code with 1000 methods (100 with issues) to test analyzer performance
        var code = GenerateLargeAnalyzerTestFile(1000, 100);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var diagnostics = GetDiagnostics(code);
        stopwatch.Stop();

        // Should complete within 15 seconds for 1000 methods
        Assert.True(stopwatch.ElapsedMilliseconds < 15000, 
            $"Analysis took {stopwatch.ElapsedMilliseconds}ms, expected < 15000ms");

        // Should find diagnostics for the 100 problematic methods
        Assert.True(diagnostics.Length >= 100, $"Expected at least 100 diagnostics, got {diagnostics.Length}");
    }

    [Fact]
    public void Memory_GeneratorDoesNotLeakMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);
        
        // Generate multiple large files to test for memory leaks
        for (int i = 0; i < 10; i++)
        {
            var code = GenerateLargeKernelFile(50);
            var generatedSources = RunGenerator(code);
            Assert.NotEmpty(generatedSources);
        }
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Memory increase should be reasonable (less than 100MB)
        Assert.True(memoryIncrease < 100 * 1024 * 1024, 
            $"Memory increased by {memoryIncrease / 1024 / 1024}MB, expected < 100MB");
    }

    [Fact]
    public void EdgeCase_MalformedKernel_HandledGracefully()
    {
        const string malformedCode = @"
using System;

public class TestClass
{
    [Kernel
    public static void IncompleteKernel(Span<float> data
    {
        int index = Kernel.ThreadId.X
        if (index < data.Length
            data[index] *= 2.0f;
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        // Should not crash on malformed code
        var exception = Record.Exception(() =>
        {
            var generatedSources = RunGenerator(malformedCode);
            var diagnostics = GetDiagnostics(malformedCode);
        });
        
        Assert.Null(exception);
    }

    [Fact]
    public void EdgeCase_EmptyFile_HandledCorrectly()
    {
        const string emptyCode = "";
        
        var generatedSources = RunGenerator(emptyCode);
        var diagnostics = GetDiagnostics(emptyCode);
        
        // Should handle empty files without crashing
        Assert.NotNull(generatedSources);
        Assert.NotNull(diagnostics);
    }

    [Fact]
    public void EdgeCase_OnlyComments_HandledCorrectly()
    {
        const string commentsOnlyCode = @"
// This file only contains comments
// No actual code here
/* Multi-line comment
   spans multiple lines
   but contains no kernels */";

        var generatedSources = RunGenerator(commentsOnlyCode);
        var diagnostics = GetDiagnostics(commentsOnlyCode);
        
        Assert.NotNull(generatedSources);
        Assert.NotNull(diagnostics);
    }

    [Fact]
    public void EdgeCase_VeryLongKernelName_HandledCorrectly()
    {
        var longName = new string('A', 1000); // 1000 character method name
        var code = $@"
using System;

public class TestClass
{{
    [Kernel]
    public static void {longName}(Span<float> data)
    {{
        int index = Kernel.ThreadId.X;
        if (index < data.Length)
            data[index] *= 2.0f;
    }}
}}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute {{ }}
public static class Kernel {{ public static ThreadId ThreadId => new(); }}
public struct ThreadId {{ public int X => 0; }}";

        var exception = Record.Exception(() =>
        {
            var generatedSources = RunGenerator(code);
            var diagnostics = GetDiagnostics(code);
        });
        
        Assert.Null(exception);
    }

    [Fact]
    public void EdgeCase_DeeplyNestedNamespaces_HandledCorrectly()
    {
        const string deeplyNestedCode = @"
using System;

namespace Level1.Level2.Level3.Level4.Level5.Level6.Level7.Level8.Level9.Level10
{
    public class TestClass
    {
        [Kernel]
        public static void DeepKernel(Span<float> data)
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
}";

        var generatedSources = RunGenerator(deeplyNestedCode);
        var diagnostics = GetDiagnostics(deeplyNestedCode);
        
        Assert.NotEmpty(generatedSources);
        
        var registry = generatedSources.First(source => source.HintName == "KernelRegistry.g.cs");
        var registryContent = registry.SourceText.ToString();
        
        // Should handle deeply nested namespaces
        Assert.Contains("Level1.Level2.Level3.Level4.Level5.Level6.Level7.Level8.Level9.Level10.TestClass", registryContent);
    }

    [Fact]
    public void EdgeCase_UnicodeKernelNames_HandledCorrectly()
    {
        const string unicodeCode = @"
using System;

public class TestClass
{
    [Kernel]
    public static void 计算内核(Span<float> data) // Chinese characters
    {
        int index = Kernel.ThreadId.X;
        if (index < data.Length)
            data[index] *= 2.0f;
    }
    
    [Kernel]
    public static void КернелНаРусском(Span<float> data) // Cyrillic characters
    {
        int index = Kernel.ThreadId.X;
        if (index < data.Length)
            data[index] *= 3.0f;
    }
    
    [Kernel]
    public static void 🚀RocketKernel🚀(Span<float> data) // Emoji (if supported)
    {
        int index = Kernel.ThreadId.X;
        if (index < data.Length)
            data[index] *= 4.0f;
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var exception = Record.Exception(() =>
        {
            var generatedSources = RunGenerator(unicodeCode);
            var diagnostics = GetDiagnostics(unicodeCode);
        });
        
        Assert.Null(exception);
    }

    [Fact]
    public void EdgeCase_CircularDependencies_DetectedCorrectly()
    {
        const string circularCode = @"
using System;

namespace A { public class ClassA { public static B.ClassB GetB() => new(); } }
namespace B { public class ClassB { public static A.ClassA GetA() => new(); } }

public class TestClass
{
    [Kernel]
    public static void CircularKernel(Span<float> data)
    {
        var a = A.ClassA.GetB();
        var b = B.ClassB.GetA(); // Potential circular reference
        
        int index = Kernel.ThreadId.X;
        if (index < data.Length)
            data[index] *= 2.0f;
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var exception = Record.Exception(() =>
        {
            var generatedSources = RunGenerator(circularCode);
            var diagnostics = GetDiagnostics(circularCode);
        });
        
        Assert.Null(exception);
    }

    private static string GenerateLargeKernelFile(int kernelCount)
    {
        var codeBuilder = new StringBuilder();
        codeBuilder.AppendLine("using System;");
        codeBuilder.AppendLine("namespace LargeTest {");
        codeBuilder.AppendLine("public class LargeKernelClass {");

        for (int i = 0; i < kernelCount; i++)
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

        return codeBuilder.ToString();
    }

    private static string GenerateLargeAnalyzerTestFile(int totalMethods, int problematicMethods)
    {
        var codeBuilder = new StringBuilder();
        codeBuilder.AppendLine("using System;");
        codeBuilder.AppendLine("public class LargeAnalyzerTestClass {");

        // Generate good methods
        for (int i = 0; i < totalMethods - problematicMethods; i++)
        {
            codeBuilder.AppendLine($@"
    [Kernel]
    public static void GoodKernel{i}(ReadOnlySpan<float> input, Span<float> output)
    {{
        int index = Kernel.ThreadId.X;
        if (index < output.Length)
            output[index] = input[index] * 2.0f;
    }}");
        }

        // Generate problematic methods
        for (int i = 0; i < problematicMethods; i++)
        {
            codeBuilder.AppendLine($@"
    [Kernel]
    public void BadKernel{i}(object badParam, Span<float> data) // DC001: not static, DC002: bad param
    {{
        for (int j = 0; j < data.Length; j++) // DC010: wrong threading
        {{
            data[j] *= 2.0f; // DC011: no bounds check
        }}
    }}");
        }

        codeBuilder.AppendLine("}");
        codeBuilder.AppendLine("[System.AttributeUsage(System.AttributeTargets.Method)]");
        codeBuilder.AppendLine("public class KernelAttribute : System.Attribute { }");
        codeBuilder.AppendLine("public static class Kernel { public static ThreadId ThreadId => new(); }");
        codeBuilder.AppendLine("public struct ThreadId { public int X => 0; }");

        return codeBuilder.ToString();
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
        return runResult.Results.Length > 0 ? runResult.Results[0].GeneratedSources : ImmutableArray<GeneratedSourceResult>.Empty;
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