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
/// Tests for diagnostic rules DC018 (kernel type parameter must be 'unmanaged') and
/// DC019 (potential buffer aliasing in kernel call).
/// </summary>
public sealed class DC018_DC019_AnalyzerTests
{
    // ===== DC018: Kernel generic type parameter must be 'unmanaged' =====

    [Fact]
    public void DC018_KernelWithoutUnmanagedConstraint_ProducesError()
    {
        const string code = @"
using System;
public static class TestClass
{
    [Kernel]
    public static void ScaleKernel<T>(Span<T> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) { }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc018 = diagnostics.FirstOrDefault(d => d.Id == "DC018");

        Assert.NotNull(dc018);
        Assert.Equal(DiagnosticSeverity.Error, dc018.Severity);
        Assert.Contains("ScaleKernel", dc018.GetMessage());
        Assert.Contains("'T'", dc018.GetMessage());
        Assert.Contains("unmanaged", dc018.GetMessage());
    }

    [Fact]
    public void DC018_KernelWithUnmanagedConstraint_NoDiagnostic()
    {
        const string code = @"
using System;
public static class TestClass
{
    [Kernel]
    public static void ScaleKernel<T>(Span<T> data) where T : unmanaged
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) { }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        Assert.Null(diagnostics.FirstOrDefault(d => d.Id == "DC018"));
    }

    [Fact]
    public void DC018_KernelWithOnlyStructConstraint_StillProducesError()
    {
        // Edge case from the spec: 'struct' is not equivalent to 'unmanaged'
        // because struct allows types with managed reference fields (e.g.,
        // struct S { string Ref; }), which cannot live in GPU memory.
        const string code = @"
using System;
public static class TestClass
{
    [Kernel]
    public static void ScaleKernel<T>(Span<T> data) where T : struct
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) { }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        var dc018 = diagnostics.FirstOrDefault(d => d.Id == "DC018");

        Assert.NotNull(dc018);
        Assert.Contains("'T'", dc018.GetMessage());
    }

    [Fact]
    public void DC018_NonKernelGenericMethod_NoDiagnostic()
    {
        const string code = @"
using System;
public static class TestClass
{
    public static void GenericHelper<T>(Span<T> data) where T : struct
    {
        // Not marked with [Kernel] — analyzer must not fire.
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }";

        var diagnostics = GetDiagnostics(code);
        Assert.Null(diagnostics.FirstOrDefault(d => d.Id == "DC018"));
    }

    [Fact]
    public void DC018_KernelWithMultipleGenericParameters_ReportsEachUnconstrainedOne()
    {
        const string code = @"
using System;
public static class TestClass
{
    [Kernel]
    public static void ConvertKernel<TIn, TOut>(Span<TIn> input, Span<TOut> output)
        where TOut : unmanaged
    {
        int idx = Kernel.ThreadId.X;
        if (idx < input.Length) { }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code).Where(d => d.Id == "DC018").ToList();

        Assert.Single(diagnostics);
        Assert.Contains("'TIn'", diagnostics[0].GetMessage());
    }

    [Fact]
    public void DC018_NonGenericKernel_NoDiagnostic()
    {
        const string code = @"
using System;
public static class TestClass
{
    [Kernel]
    public static void Simple(Span<float> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) { }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        var diagnostics = GetDiagnostics(code);
        Assert.Null(diagnostics.FirstOrDefault(d => d.Id == "DC018"));
    }

    [Fact]
    public void DC018_CodeFix_AppliesUnmanagedConstraint()
    {
        // Before: no constraint → DC018 fires.
        const string before = @"
using System;
public static class TestClass
{
    [Kernel]
    public static void ScaleKernel<T>(Span<T> data)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < data.Length) { }
    }
}
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }";

        // Apply the fix manually (the fix itself is also exercised as a syntactic
        // transform — in a production IDE the [DC018CodeFixProvider] would perform
        // the same edit).
        var fixedSource = KernelUnmanagedConstraintCodeFixProvider.ApplyFix(before, "ScaleKernel", "T");

        Assert.Contains("where T : unmanaged", fixedSource);

        // After the fix, DC018 must no longer fire.
        var diagnosticsAfter = GetDiagnostics(fixedSource);
        Assert.Null(diagnosticsAfter.FirstOrDefault(d => d.Id == "DC018"));
    }

    // ===== DC019: Potential buffer aliasing in kernel call =====

    [Fact]
    public void DC019_SameBufferPassedTwiceToExecuteAsync_ProducesWarning()
    {
        const string code = @"
using System.Threading.Tasks;

public interface IComputeOrchestrator
{
    Task<T> ExecuteAsync<T>(string kernelName, params object[] args);
}

public class Caller
{
    public async Task Run(IComputeOrchestrator orchestrator, object buf)
    {
        await orchestrator.ExecuteAsync<object>(""vector_add"", buf, buf, buf);
    }
}";

        var diagnostics = GetDiagnostics(code).Where(d => d.Id == "DC019").ToList();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("'buf'"));
    }

    [Fact]
    public void DC019_DistinctBuffers_NoDiagnostic()
    {
        const string code = @"
using System.Threading.Tasks;

public interface IComputeOrchestrator
{
    Task<T> ExecuteAsync<T>(string kernelName, params object[] args);
}

public class Caller
{
    public async Task Run(IComputeOrchestrator orchestrator, object a, object b, object c)
    {
        await orchestrator.ExecuteAsync<object>(""vector_add"", a, b, c);
    }
}";

        var diagnostics = GetDiagnostics(code).Where(d => d.Id == "DC019").ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void DC019_AliasAllowedAttribute_SuppressesDiagnostic()
    {
        // Even though 'buf' appears twice, the caller has opted-in to aliasing.
        const string code = @"
using System.Threading.Tasks;

public interface IComputeOrchestrator
{
    Task<T> ExecuteAsync<T>(string kernelName, params object[] args);
}

public class AliasAllowedAttribute : System.Attribute { }

public class Caller
{
    [AliasAllowed]
    public async Task Run(IComputeOrchestrator orchestrator, object buf)
    {
        await orchestrator.ExecuteAsync<object>(""inplace"", buf, buf);
    }
}";

        var diagnostics = GetDiagnostics(code).Where(d => d.Id == "DC019").ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void DC019_ScalarLiteralsNotFlagged()
    {
        // Literal arguments — even if repeated — are never buffer aliases.
        const string code = @"
using System.Threading.Tasks;

public interface IComputeOrchestrator
{
    Task<T> ExecuteAsync<T>(string kernelName, params object[] args);
}

public class Caller
{
    public async Task Run(IComputeOrchestrator orchestrator, object buf)
    {
        await orchestrator.ExecuteAsync<object>(""repeated_scalars"", buf, 0, 0, 0);
    }
}";

        var diagnostics = GetDiagnostics(code).Where(d => d.Id == "DC019").ToList();
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void DC019_UnrelatedInvocation_NoDiagnostic()
    {
        const string code = @"
using System.Threading.Tasks;

public class Caller
{
    public void Other(string a, string b, string c) { }

    public void Run(string x)
    {
        // Not a kernel launch — must not fire.
        Other(x, x, x);
    }
}";

        var diagnostics = GetDiagnostics(code).Where(d => d.Id == "DC019").ToList();
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void DC019_LaunchAsyncWithAliasedBuffer_ProducesWarning()
    {
        const string code = @"
using System.Threading.Tasks;

public interface ICompiledKernel
{
    ValueTask LaunchAsync(object launchConfig, params object[] arguments);
}

public class Caller
{
    public async Task Run(ICompiledKernel kernel, object cfg, object buf)
    {
        await kernel.LaunchAsync(cfg, buf, buf);
    }
}";

        var diagnostics = GetDiagnostics(code).Where(d => d.Id == "DC019").ToList();
        Assert.NotEmpty(diagnostics);
    }

    // ===== Wiring / meta checks =====

    [Fact]
    public void KernelTypeSafetyAnalyzer_RegistersBothRules()
    {
        var analyzer = new KernelTypeSafetyAnalyzer();
        var ids = analyzer.SupportedDiagnostics.Select(d => d.Id).ToHashSet();

        Assert.Contains("DC018", ids);
        Assert.Contains("DC019", ids);
        Assert.Equal(2, ids.Count);
    }

    // ===== Helpers =====

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
                MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.ValueTask).Assembly.Location),
            });

        var analyzer = new KernelTypeSafetyAnalyzer();
        var compilationWithAnalyzers = compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
    }
}
