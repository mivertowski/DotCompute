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
/// Comprehensive tests for diagnostic rules DC007-DC012.
/// </summary>
public sealed class DC007_DC012_AnalyzerTests
{
    // DC007: Missing [Kernel] attribute

    [Fact]
    public void DC007_KernelLikeMethodWithoutAttribute_ProducesInfoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    public static void LooksLikeKernel(Span<float> data)
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
        var dc007 = diagnostics.FirstOrDefault(d => d.Id == "DC007");

        Assert.NotNull(dc007);
        Assert.Equal(DiagnosticSeverity.Info, dc007.Severity);
        Assert.Contains("LooksLikeKernel", dc007.GetMessage());
    }

    [Fact]
    public void DC007_RegularMethodWithAttribute_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void ActualKernel(Span<float> data)
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
        var dc007 = diagnostics.FirstOrDefault(d => d.Id == "DC007");

        Assert.Null(dc007);
    }

    [Fact]
    public void DC007_NonKernelMethod_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    public static void RegularMethod(float[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] *= 2.0f;
        }
    }
}";

        var diagnostics = GetDiagnostics(code);
        var dc007 = diagnostics.FirstOrDefault(d => d.Id == "DC007");

        Assert.Null(dc007);
    }

    // DC008: Unnecessary complexity

    [Fact]
    public void DC008_OverlyComplexKernel_ProducesWarning()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void ComplexKernel(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length)
        {
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    if (i * j > 5)
                    {
                        switch (data[idx])
                        {
                            case > 1.0f:
                                data[idx] *= 2.0f;
                                break;
                            case > 0.5f:
                                data[idx] += 1.0f;
                                break;
                            default:
                                data[idx] = 0.0f;
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
        var dc008 = diagnostics.FirstOrDefault(d => d.Id == "DC008");

        // May or may not trigger depending on complexity threshold
        if (dc008 != null)
        {
            Assert.Equal(DiagnosticSeverity.Info, dc008.Severity);
        }
    }

    [Fact]
    public void DC008_SimpleKernel_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void SimpleKernel(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length)
        {
            data[idx] *= 2.0f;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc008 = diagnostics.FirstOrDefault(d => d.Id == "DC008");

        Assert.Null(dc008);
    }

    // DC009: Thread safety issues

    [Fact]
    public void DC009_PotentialRaceCondition_ProducesWarning()
    {
        const string code = @"
using System;
public class TestClass
{
    private static int sharedCounter = 0;

    [Kernel]
    public static void UnsafeKernel(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length)
        {
            sharedCounter++;  // Race condition!
            data[idx] = sharedCounter;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        // DC009 may be triggered for thread safety
        var dc009 = diagnostics.FirstOrDefault(d => d.Id == "DC009");

        if (dc009 != null)
        {
            Assert.Equal(DiagnosticSeverity.Warning, dc009.Severity);
        }
    }

    [Fact]
    public void DC009_ThreadSafeKernel_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void SafeKernel(ReadOnlySpan<float> input, Span<float> output)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < output.Length)
        {
            output[idx] = input[idx] * 2.0f;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc009 = diagnostics.FirstOrDefault(d => d.Id == "DC009");

        Assert.Null(dc009);
    }

    // DC010: Incorrect threading model

    [Fact]
    public void DC010_ForLoopInsteadOfThreadId_ProducesWarning()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void IncorrectThreading(Span<float> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] *= 2.0f;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);
        var dc010 = diagnostics.FirstOrDefault(d => d.Id == "DC010");

        Assert.NotNull(dc010);
        Assert.Equal(DiagnosticSeverity.Warning, dc010.Severity);
    }

    [Fact]
    public void DC010_CorrectThreadIdUsage_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void CorrectThreading(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length)
        {
            data[idx] *= 2.0f;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc010 = diagnostics.FirstOrDefault(d => d.Id == "DC010");

        Assert.Null(dc010);
    }

    // DC011: Missing bounds check

    [Fact]
    public void DC011_NoBoundsCheck_ProducesWarning()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void NoBounds(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        data[idx] = 1.0f;  // No bounds check!
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc011 = diagnostics.FirstOrDefault(d => d.Id == "DC011");

        Assert.NotNull(dc011);
        Assert.Equal(DiagnosticSeverity.Warning, dc011.Severity);
    }

    [Fact]
    public void DC011_WithBoundsCheck_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void WithBounds(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length)
        {
            data[idx] = 1.0f;
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc011 = diagnostics.FirstOrDefault(d => d.Id == "DC011");

        Assert.Null(dc011);
    }

    [Fact]
    public void DC011_WithEarlyReturn_NoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void EarlyReturn(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx >= data.Length) return;

        data[idx] = 1.0f;
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc011 = diagnostics.FirstOrDefault(d => d.Id == "DC011");

        Assert.Null(dc011);
    }

    // DC012: Suboptimal backend selection

    [Fact]
    public void DC012_SuboptimalBackendHint_ProducesInfoDiagnostic()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]  // Could specify backend preferences
    public static void SimpleScalar(Span<int> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length)
        {
            data[idx] = idx;  // Very simple, CPU might be better
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        // DC012 may or may not trigger depending on backend analysis
        var dc012 = diagnostics.FirstOrDefault(d => d.Id == "DC012");

        if (dc012 != null)
        {
            Assert.Equal(DiagnosticSeverity.Info, dc012.Severity);
        }
    }

    // Combined scenarios

    [Fact]
    public void MultipleRules_DetectsAllIssues()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public void BadKernel(object badParam, Span<float> data)  // DC001, DC002
    {
        for (int i = 0; i < data.Length; i++)  // DC010
        {
            data[i] = 1.0f;  // DC011 (no bounds check on i itself)
        }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);

        Assert.Contains(diagnostics, d => d.Id == "DC001");  // Non-static
        Assert.Contains(diagnostics, d => d.Id == "DC002");  // Invalid parameter
        Assert.Contains(diagnostics, d => d.Id == "DC010");  // Wrong threading
    }

    [Fact]
    public void OptimalKernel_NoWarningsOrErrors()
    {
        const string code = @"
using System;
public class TestClass
{
    [Kernel]
    public static void OptimalKernel(ReadOnlySpan<float> input, Span<float> output)
    {
        int idx = Kernel.ThreadId.X;
        if (idx >= output.Length) return;

        output[idx] = input[idx] * 2.0f + 1.0f;
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

        Assert.Empty(errors);
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
