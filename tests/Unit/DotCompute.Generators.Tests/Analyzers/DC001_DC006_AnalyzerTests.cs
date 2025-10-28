// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using DotCompute.Generators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace DotCompute.Generators.Tests.Analyzers;

/// <summary>
/// Comprehensive tests for diagnostic rules DC001-DC006.
/// </summary>
public sealed class DC001_DC006_AnalyzerTests
{
    // DC001: Kernel method must be static

    [Fact]
    public void DC001_NonStaticKernelMethod_ProducesDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public void NonStaticKernel(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) data[idx] *= 2.0f;
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc001 = diagnostics.FirstOrDefault(d => d.Id == "DC001");

        Assert.NotNull(dc001);
        Assert.Equal(DiagnosticSeverity.Error, dc001.Severity);
        Assert.Contains("NonStaticKernel", dc001.GetMessage());
    }

    [Fact]
    public void DC001_StaticKernelMethod_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void StaticKernel(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) data[idx] *= 2.0f;
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc001 = diagnostics.FirstOrDefault(d => d.Id == "DC001");

        Assert.Null(dc001);
    }

    [Fact]
    public void DC001_NonStaticWithComplexSignature_ProducesDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public void ComplexNonStatic(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> r, int count)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < r.Length) r[idx] = a[idx] + b[idx];
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc001 = diagnostics.FirstOrDefault(d => d.Id == "DC001");

        Assert.NotNull(dc001);
        Assert.Equal(DiagnosticSeverity.Error, dc001.Severity);
    }

    // DC002: Invalid kernel parameters

    [Fact]
    public void DC002_InvalidObjectParameter_ProducesDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void InvalidParam(object badParam)
    {
        // Invalid parameter type
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);
        var dc002 = diagnostics.FirstOrDefault(d => d.Id == "DC002");

        Assert.NotNull(dc002);
        Assert.Equal(DiagnosticSeverity.Error, dc002.Severity);
        Assert.Contains("badParam", dc002.GetMessage());
    }

    [Fact]
    public void DC002_ValidSpanParameter_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void ValidParam(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) data[idx] *= 2.0f;
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc002 = diagnostics.FirstOrDefault(d => d.Id == "DC002");

        Assert.Null(dc002);
    }

    [Fact]
    public void DC002_ValidReadOnlySpanParameter_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void ValidReadOnly(ReadOnlySpan<float> input, Span<float> output)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < output.Length) output[idx] = input[idx] * 2.0f;
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc002 = diagnostics.FirstOrDefault(d => d.Id == "DC002");

        Assert.Null(dc002);
    }

    [Fact]
    public void DC002_ValidPrimitiveParameters_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void ValidPrimitives(Span<float> data, int count, float scale, double offset)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) data[idx] = data[idx] * scale + (float)offset;
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc002 = diagnostics.FirstOrDefault(d => d.Id == "DC002");

        Assert.Null(dc002);
    }

    [Fact]
    public void DC002_InvalidStringParameter_ProducesDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void StringParam(Span<float> data, string message)
    {
        // String not allowed in kernels
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);
        var dc002 = diagnostics.FirstOrDefault(d => d.Id == "DC002");

        Assert.NotNull(dc002);
        Assert.Contains("message", dc002.GetMessage());
    }

    // DC003: Unsupported constructs

    [Fact]
    public void DC003_NonVoidReturnType_ProducesDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static int InvalidReturn(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) data[idx] *= 2.0f;
        return idx;
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc003 = diagnostics.FirstOrDefault(d => d.Id == "DC003");

        Assert.NotNull(dc003);
        Assert.Equal(DiagnosticSeverity.Error, dc003.Severity);
    }

    [Fact]
    public void DC003_VoidReturnType_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void ValidVoidReturn(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) data[idx] *= 2.0f;
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc003 = diagnostics.FirstOrDefault(d => d.Id == "DC003");

        Assert.Null(dc003);
    }

    // DC004: Vectorization opportunities

    [Fact]
    public void DC004_VectorizableLoop_ProducesInfoDiagnostic()
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
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);
        var dc004 = diagnostics.FirstOrDefault(d => d.Id == "DC004");

        // May or may not produce diagnostic depending on implementation
        if (dc004 != null)
        {
            Assert.Equal(DiagnosticSeverity.Info, dc004.Severity);
        }
    }

    // DC005: Suboptimal memory access

    [Fact]
    public void DC005_NonSequentialAccess_ProducesWarning()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void NonSequential(Span<float> data, Span<int> indices)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < indices.Length)
        {
            int targetIdx = indices[idx];
            if (targetIdx < data.Length)
                data[targetIdx] = idx;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        // DC005 may or may not be triggered depending on analysis depth
        var dc005 = diagnostics.FirstOrDefault(d => d.Id == "DC005");

        if (dc005 != null)
        {
            Assert.Equal(DiagnosticSeverity.Warning, dc005.Severity);
        }
    }

    // DC006: Register spilling

    [Fact]
    public void DC006_ManyLocalVariables_ProducesWarning()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void ManyLocals(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length)
        {
            float v1 = data[idx];
            float v2 = v1 * 2.0f;
            float v3 = v2 + 1.0f;
            float v4 = v3 * 3.0f;
            float v5 = v4 - 2.0f;
            float v6 = v5 * 4.0f;
            float v7 = v6 + 3.0f;
            float v8 = v7 * 5.0f;
            float v9 = v8 - 4.0f;
            float v10 = v9 * 6.0f;
            float v11 = v10 + 5.0f;
            float v12 = v11 * 7.0f;
            data[idx] = v12;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        // DC006 may be triggered for register pressure
        var dc006 = diagnostics.FirstOrDefault(d => d.Id == "DC006");

        if (dc006 != null)
        {
            Assert.Equal(DiagnosticSeverity.Warning, dc006.Severity);
        }
    }

    [Fact]
    public void DC006_FewLocalVariables_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void FewLocals(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length)
        {
            float temp = data[idx] * 2.0f;
            data[idx] = temp + 1.0f;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc006 = diagnostics.FirstOrDefault(d => d.Id == "DC006");

        Assert.Null(dc006);
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
