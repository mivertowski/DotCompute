// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using DotCompute.Generators.Analyzers;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace DotCompute.Generators.Tests;

/// <summary>
/// Comprehensive tests for all 12 diagnostic rules (DC001-DC012) in DotComputeKernelAnalyzer.
/// </summary>
public class ComprehensiveAnalyzerTests
{
    [Fact]
    public void Analyzer_DC003_VoidReturnType_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static int InvalidReturnType(Span<float> data)
    {
        int index = Kernel.ThreadId.X;
        if (index < data.Length)
            data[index] *= 2.0f;
        return index;
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc003Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC003");
        
        Assert.NotNull(dc003Diagnostic);
        Assert.Equal(DiagnosticSeverity.Error, dc003Diagnostic.Severity);
        Assert.Contains("InvalidReturnType", dc003Diagnostic.GetMessage());
    }

    [Fact]
    public void Analyzer_DC004_RecursiveCall_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void RecursiveKernel(Span<float> data, int depth)
    {
        if (depth > 0)
            RecursiveKernel(data, depth - 1); // Recursive call not allowed
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);
        var dc004Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC004");
        
        Assert.NotNull(dc004Diagnostic);
        Assert.Equal(DiagnosticSeverity.Error, dc004Diagnostic.Severity);
        Assert.Contains("RecursiveKernel", dc004Diagnostic.GetMessage());
    }

    [Fact]
    public void Analyzer_DC005_UnsafePointer_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public unsafe static void UnsafeKernel(float* data, int length)
    {
        int index = Kernel.ThreadId.X;
        if (index < length)
            data[index] *= 2.0f; // Unsafe pointer operation
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc005Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC005");
        
        Assert.NotNull(dc005Diagnostic);
        Assert.Equal(DiagnosticSeverity.Warning, dc005Diagnostic.Severity);
        Assert.Contains("UnsafeKernel", dc005Diagnostic.GetMessage());
    }

    [Fact]
    public void Analyzer_DC006_ExceptionHandling_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void ExceptionKernel(Span<float> data)
    {
        try
        {
            int index = Kernel.ThreadId.X;
            if (index < data.Length)
                data[index] = 1.0f / data[index]; // Division that might throw
        }
        catch (Exception ex) // Exception handling not supported
        {
            // Handle error
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc006Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC006");
        
        Assert.NotNull(dc006Diagnostic);
        Assert.Equal(DiagnosticSeverity.Error, dc006Diagnostic.Severity);
        Assert.Contains("ExceptionKernel", dc006Diagnostic.GetMessage());
    }

    [Fact]
    public void Analyzer_DC007_MissingKernelAttribute_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    // Missing [Kernel] attribute but looks like a kernel
    public static void KernelLikeMethod(Span<float> data)
    {
        int index = Kernel.ThreadId.X;
        if (index < data.Length)
            data[index] *= 2.0f;
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc007Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC007");
        
        Assert.NotNull(dc007Diagnostic);
        Assert.Equal(DiagnosticSeverity.Info, dc007Diagnostic.Severity);
        Assert.Contains("KernelLikeMethod", dc007Diagnostic.GetMessage());
    }

    [Fact]
    public void Analyzer_DC008_ComplexControlFlow_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void ComplexFlowKernel(Span<float> data)
    {
        int index = Kernel.ThreadId.X;
        if (index < data.Length)
        {
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    if (i * j > 5)
                    {
                        switch (data[index])
                        {
                            case > 1.0f:
                                data[index] *= 2.0f;
                                break;
                            case > 0.5f:
                                data[index] += 1.0f;
                                break;
                            default:
                                data[index] = 0.0f;
                                break;
                        }
                    }
                }
            }
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc008Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC008");
        
        Assert.NotNull(dc008Diagnostic);
        Assert.Equal(DiagnosticSeverity.Warning, dc008Diagnostic.Severity);
        Assert.Contains("ComplexFlowKernel", dc008Diagnostic.GetMessage());
    }

    [Fact]
    public void Analyzer_DC009_PerformanceAntiPattern_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void InefficientKernel(Span<float> data)
    {
        int index = Kernel.ThreadId.X;
        if (index < data.Length)
        {
            // Performance anti-pattern: unnecessary loop
            float sum = 0;
            for (int i = 0; i < index; i++)
            {
                sum += data[i]; // Accessing data[i] instead of just data[index]
            }
            data[index] = sum / index;
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc009Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC009");
        
        Assert.NotNull(dc009Diagnostic);
        Assert.Equal(DiagnosticSeverity.Info, dc009Diagnostic.Severity);
        Assert.Contains("InefficientKernel", dc009Diagnostic.GetMessage());
    }

    [Fact]
    public void Analyzer_DC012_RegisterSpilling_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void RegisterHeavyKernel(Span<float> data)
    {
        int index = Kernel.ThreadId.X;
        if (index < data.Length)
        {
            // Many local variables that could cause register spilling
            float a = data[index];
            float b = a * 2.0f;
            float c = b + 1.0f;
            float d = c * 3.0f;
            float e = d - 2.0f;
            float f = e * 4.0f;
            float g = f + 3.0f;
            float h = g * 5.0f;
            float i = h - 4.0f;
            float j = i * 6.0f;
            float k = j + 5.0f;
            float l = k * 7.0f;
            float m = l - 6.0f;
            float n = m * 8.0f;
            float o = n + 7.0f;
            float p = o * 9.0f;
            
            data[index] = p;
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc012Diagnostic = diagnostics.FirstOrDefault(d => d.Id == "DC012");
        
        Assert.NotNull(dc012Diagnostic);
        Assert.Equal(DiagnosticSeverity.Info, dc012Diagnostic.Severity);
        Assert.Contains("RegisterHeavyKernel", dc012Diagnostic.GetMessage());
    }

    [Fact]
    public void Analyzer_MultipleIssues_CreatesMultipleDiagnostics()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public void BadKernel(object badParam, Span<float> data) // DC001: not static, DC002: invalid param
    {
        for (int i = 0; i < data.Length; i++) // DC010: incorrect threading
        {
            try
            {
                data[i] = 1.0f / data[i]; // DC006: exception handling, DC011: no bounds check
            }
            catch (Exception ex) 
            {
                data[i] = 0.0f;
            }
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);
        
        // Should detect multiple issues
        Assert.Contains(diagnostics, d => d.Id == "DC001"); // Non-static method
        Assert.Contains(diagnostics, d => d.Id == "DC002"); // Invalid parameter
        Assert.Contains(diagnostics, d => d.Id == "DC006"); // Exception handling
        Assert.Contains(diagnostics, d => d.Id == "DC010"); // Incorrect threading
        Assert.Contains(diagnostics, d => d.Id == "DC011"); // Missing bounds check
    }

    [Fact]
    public void Analyzer_OptimalKernel_NoWarnings()
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
            return;
        
        output[index] = input[index] * 2.0f;
    }
    
    [Kernel]
    public static void OptimalReductionKernel(ReadOnlySpan<float> input, Span<float> partialSums)
    {
        int tid = Kernel.ThreadId.X;
        int blockSize = Kernel.BlockDim.X;
        int gridSize = blockSize * Kernel.GridDim.X;
        
        if (tid >= partialSums.Length)
            return;
            
        float sum = 0.0f;
        for (int i = tid; i < input.Length; i += gridSize)
        {
            sum += input[i];
        }
        
        partialSums[tid] = sum;
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel 
{ 
    public static ThreadId ThreadId => new(); 
    public static BlockDim BlockDim => new();
    public static GridDim GridDim => new();
}
public struct ThreadId { public int X => 0; public int Y => 0; public int Z => 0; }
public struct BlockDim { public int X => 256; }
public struct GridDim { public int X => 1024; }";

        var diagnostics = GetDiagnostics(code);
        var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        
        Assert.Empty(errorDiagnostics);
    }

    [Fact]
    public void Analyzer_EdgeCase_EmptyKernel_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void EmptyKernel()
    {
        // Empty kernel body - should trigger warning
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);
        
        // Should detect the empty kernel as a potential issue
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyzer_EdgeCase_KernelWithOnlyComments_CreatesDiagnostic()
    {
        const string code = @"
using System;

public class TestClass
{
    [Kernel]
    public static void CommentOnlyKernel(Span<float> data)
    {
        // This kernel only has comments
        // No actual implementation
        // Should be flagged as potentially incomplete
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);
        
        // Should detect the comment-only kernel
        Assert.NotEmpty(diagnostics);
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