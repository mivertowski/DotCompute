// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using DotCompute.Generators.Analyzers;
using DotCompute.Generators.CodeFixes;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace DotCompute.Generators.Tests;

/// <summary>
/// Direct tests for DotComputeKernelAnalyzer without complex testing framework.
/// These tests validate that the analyzer correctly identifies issues and that the
/// code fixes work properly.
/// </summary>
public class SimpleAnalyzerTests
{
    [Fact]
    public void Analyzer_ShouldDetectNonStaticKernelMethod()
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

public class KernelAttribute : Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }
";

        var diagnostics = GetDiagnostics(code);
        var dc001Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC001");
        
        Assert.NotNull(dc001Diagnostic);
        Assert.Equal("NonStaticKernel", GetMethodNameFromDiagnostic(dc001Diagnostic));
        Assert.Equal(DiagnosticSeverity.Error, dc001Diagnostic.Severity);
    }

    [Fact]
    public void Analyzer_ShouldDetectInvalidParameterType()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void InvalidKernel(object invalidParam)
    {
    }
}

public class KernelAttribute : Attribute { }
";

        var diagnostics = GetDiagnostics(code);
        var dc002Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC002");
        
        Assert.NotNull(dc002Diagnostic);
        Assert.Contains("InvalidKernel", dc002Diagnostic.GetMessage());
        Assert.Contains("invalidParam", dc002Diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Error, dc002Diagnostic.Severity);
    }

    [Fact]
    public void Analyzer_ShouldDetectMissingKernelAttribute()
    {
        const string code = @"
using System;

public class TestClass
{
    public static void LooksLikeKernel(Span<float> input, Span<float> output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }
}
";

        var diagnostics = GetDiagnostics(code);
        var dc007Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC007");
        
        Assert.NotNull(dc007Diagnostic);
        Assert.Contains("LooksLikeKernel", dc007Diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Info, dc007Diagnostic.Severity);
    }

    [Fact]
    public void Analyzer_ShouldDetectIncorrectThreadingModel()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void IncorrectThreadingKernel(Span<float> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] *= 2.0f;
        }
    }
}

public class KernelAttribute : Attribute { }
";

        var diagnostics = GetDiagnostics(code);
        var dc010Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC010");
        
        Assert.NotNull(dc010Diagnostic);
        Assert.Contains("IncorrectThreadingKernel", dc010Diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Warning, dc010Diagnostic.Severity);
    }

    [Fact]
    public void Analyzer_ShouldDetectMissingBoundsCheck()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void NoBoundsCheckKernel(Span<float> data)
    {
        int index = GetIndex();
        data[index] = data[index] * 2.0f;
    }

    private static int GetIndex() => 0;
}

public class KernelAttribute : Attribute { }
";

        var diagnostics = GetDiagnostics(code);
        var dc011Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC011");
        
        Assert.NotNull(dc011Diagnostic);
        Assert.Contains("NoBoundsCheckKernel", dc011Diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Warning, dc011Diagnostic.Severity);
    }

    [Fact]
    public void Analyzer_ShouldDetectUnsupportedConstructs()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void KernelWithTryCatch(Span<float> data)
    {
        try
        {
            data[0] = 1.0f;
        }
        catch (Exception ex)
        {
            // Error handling
        }
    }
}

public class KernelAttribute : Attribute { }
";

        var diagnostics = GetDiagnostics(code);
        var dc003Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC003");
        
        Assert.NotNull(dc003Diagnostic);
        Assert.Contains("KernelWithTryCatch", dc003Diagnostic.GetMessage());
        Assert.Contains("try-catch blocks", dc003Diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Error, dc003Diagnostic.Severity);
    }

    [Fact]
    public void Analyzer_ShouldDetectVectorizationOpportunities()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void VectorizableKernel(Span<float> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = data[i] * 2.0f + 1.0f;
        }
    }
}

public class KernelAttribute : Attribute { }
";

        var diagnostics = GetDiagnostics(code);
        var dc004Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC004");
        
        Assert.NotNull(dc004Diagnostic);
        Assert.Contains("VectorizableKernel", dc004Diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Info, dc004Diagnostic.Severity);
    }

    [Fact]
    public void Analyzer_ShouldDetectRegisterSpilling()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void RegisterSpillingKernel(Span<float> data)
    {
        float v1 = 1.0f, v2 = 2.0f, v3 = 3.0f, v4 = 4.0f, v5 = 5.0f;
        float v6 = 6.0f, v7 = 7.0f, v8 = 8.0f, v9 = 9.0f, v10 = 10.0f;
        float v11 = 11.0f, v12 = 12.0f, v13 = 13.0f, v14 = 14.0f, v15 = 15.0f;
        float v16 = 16.0f, v17 = 17.0f, v18 = 18.0f; // > 16 variables

        int index = Kernel.ThreadId.X;
        if (index < data.Length)
            data[index] = v1 + v2 + v3 + v4 + v5 + v6 + v7 + v8 + v9 + v10 + v11 + v12 + v13 + v14 + v15 + v16 + v17 + v18;
    }
}

public class KernelAttribute : Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }
";

        var diagnostics = GetDiagnostics(code);
        var dc006Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC006");
        
        Assert.NotNull(dc006Diagnostic);
        Assert.Contains("RegisterSpillingKernel", dc006Diagnostic.GetMessage());
        Assert.Contains("18", dc006Diagnostic.GetMessage()); // Variable count
        Assert.Equal(DiagnosticSeverity.Warning, dc006Diagnostic.Severity);
    }

    [Fact]
    public void Analyzer_ValidKernel_ShouldHaveNoCriticalDiagnostics()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void OptimalKernel(ReadOnlySpan<float> input, Span<float> output)
    {
        int index = Kernel.ThreadId.X;

        if (index >= output.Length)
        {
            return;
        }

        output[index] = input[index] * 2.0f + 1.0f;
    }
}

public class KernelAttribute : Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }
";

        var diagnostics = GetDiagnostics(code);
        var criticalDiagnostics = diagnostics.Where(d => 
            d.Severity == DiagnosticSeverity.Error).ToList();
        
        Assert.Empty(criticalDiagnostics);
        
        // Should still detect vectorization opportunity (DC004)
        var infoDiagnostics = diagnostics.Where(d => 
            d.Severity == DiagnosticSeverity.Info && d.Id == "DC004").ToList();
        Assert.NotEmpty(infoDiagnostics);
    }

    [Fact]
    public void CodeFixProvider_ShouldSupportRequiredDiagnostics()
    {
        var codeFixProvider = new KernelCodeFixProvider();
        var fixableDiagnostics = codeFixProvider.FixableDiagnosticIds;
        
        Assert.Contains("DC001", fixableDiagnostics); // Make static
        Assert.Contains("DC002", fixableDiagnostics); // Fix parameters
        Assert.Contains("DC007", fixableDiagnostics); // Add [Kernel] attribute
        Assert.Contains("DC010", fixableDiagnostics); // Fix threading model
        Assert.Contains("DC011", fixableDiagnostics); // Add bounds check
        
        Assert.Equal(5, fixableDiagnostics.Length); // Exactly 5 fixable diagnostics
    }

    [Fact]
    public void Analyzer_ShouldSupportAllTwelveDiagnosticRules()
    {
        var analyzer = new DotComputeKernelAnalyzer();
        var supportedDiagnostics = analyzer.SupportedDiagnostics;
        
        Assert.Equal(12, supportedDiagnostics.Length);
        
        // Verify all diagnostic IDs are present
        var diagnosticIds = supportedDiagnostics.Select(d => d.Id).ToHashSet();
        for (int i = 1; i <= 12; i++)
        {
            var id = $"DC{i:D3}";
            Assert.Contains(id, diagnosticIds);
        }
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

    private static string GetMethodNameFromDiagnostic(Diagnostic diagnostic)
    {
        // Extract method name from diagnostic message
        var message = diagnostic.GetMessage();
        var start = message.IndexOf('\'') + 1;
        var end = message.IndexOf('\'', start);
        return message.Substring(start, end - start);
    }
}

/// <summary>
/// Integration tests to validate that the analyzer works correctly with the examples
/// from AnalyzerDemo.cs
/// </summary>
public class AnalyzerDemoIntegrationTests
{
    [Fact]
    public void AnalyzerDemo_Examples_ShouldTriggerExpectedDiagnostics()
    {
        // Test that our examples actually trigger the diagnostics as expected
        const string invalidParameterExample = @"
[Kernel]
public static void InvalidParameterKernel(object invalidParam) { }

public class KernelAttribute : System.Attribute { }
";

        var diagnostics = GetDiagnostics(invalidParameterExample);
        Assert.Contains(diagnostics, d => d.Id == "DC002");
    }

    [Fact]
    public void AnalyzerDemo_OptimalKernel_ShouldPassValidation()
    {
        const string optimalKernelExample = @"
[Kernel]
public static void OptimalKernel(System.ReadOnlySpan<float> input, System.Span<float> output)
{
    int index = ThreadId.X;

    if (index >= output.Length)
    {
        return;
    }

    output[index] = input[index] * 2.0f + 1.0f;
}

public class KernelAttribute : System.Attribute { }
public struct ThreadId { public static int X => 0; }
";

        var diagnostics = GetDiagnostics(optimalKernelExample);
        var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        
        // Should have no errors
        Assert.Empty(errorDiagnostics);
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
}