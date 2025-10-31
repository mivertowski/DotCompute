// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DotCompute.Generators.Kernel;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace DotCompute.Generators.Tests.RingKernel;

/// <summary>
/// Test both [Kernel] and [RingKernel] in the same source to see the difference.
/// </summary>
public sealed class SideBySideTest
{
    [Fact]
    public void SideBySide_BothAttributeTypes_OnlyKernelGenerates()
    {
        const string code = @"
using System;

namespace TestApp
{
    public static class Processors
    {
        [Kernel]
        public static void RegularKernel(Span<int> data)
        {
            int idx = 0;
            if (idx < data.Length) data[idx] = idx;
        }

        [RingKernel]
        public static void RingMethod(Span<int> data)
        {
        }
    }
}

// Inline attribute definitions (global namespace like regular Kernel tests)
[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }

[System.AttributeUsage(System.AttributeTargets.Method)]
public class RingKernelAttribute : System.Attribute { }

public static class Kernel { public static ThreadId ThreadId => new(); }
public struct ThreadId { public int X => 0; }
";

        var (generatedSources, diagnostics) = RunGenerator(code);

        System.Console.WriteLine($"Generated {generatedSources.Length} sources");
        foreach (var src in generatedSources)
        {
            System.Console.WriteLine($"  - {src.HintName}");
        }

        System.Console.WriteLine($"\nDiagnostics (warnings+errors): {diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).Count()}");

        // Check what was generated
        var hasKernelRegistry = generatedSources.Any(s => s.HintName.Contains("KernelRegistry"));
        var hasRingKernelRegistry = generatedSources.Any(s => s.HintName.Contains("RingKernelRegistry"));

        System.Console.WriteLine($"Has KernelRegistry: {hasKernelRegistry}");
        System.Console.WriteLine($"Has RingKernelRegistry: {hasRingKernelRegistry}");

        // The hypothesis is: regular Kernel WILL generate, RingKernel WON'T
        Assert.True(hasKernelRegistry, "Regular Kernel should generate KernelRegistry");

        // This will likely FAIL, proving Ring Kernels aren't being recognized
        Assert.True(hasRingKernelRegistry, "Ring Kernel should generate RingKernelRegistry but doesn't!");
    }

    private static (ImmutableArray<GeneratedSourceResult>, ImmutableArray<Diagnostic>) RunGenerator(string source)
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

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        var runResult = driver.GetRunResult();
        var sources = runResult.Results.Length > 0 ? runResult.Results[0].GeneratedSources : ImmutableArray<GeneratedSourceResult>.Empty;

        var allDiagnostics = generatorDiagnostics.AddRange(outputCompilation.GetDiagnostics());

        return (sources, allDiagnostics);
    }
}
