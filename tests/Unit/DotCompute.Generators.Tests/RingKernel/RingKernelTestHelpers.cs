// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using DotCompute.Generators.Analyzers;
using DotCompute.Generators.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotCompute.Generators.Tests.RingKernel;

/// <summary>
/// Shared helper methods for Ring Kernel tests.
/// </summary>
internal static class RingKernelTestHelpers
{
    /// <summary>
    /// Runs the KernelSourceGenerator and returns diagnostics and generated sources.
    /// </summary>
    public static (ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<GeneratedSourceResult> GeneratedSources) RunGenerator(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "test",
            syntaxTrees: new[] { tree },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Span<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.Unsafe).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new KernelSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.Results.Length > 0
            ? runResult.Results[0].GeneratedSources
            : ImmutableArray<GeneratedSourceResult>.Empty;

        // Combine generator diagnostics with compilation diagnostics
        var allDiagnostics = diagnostics.AddRange(outputCompilation.GetDiagnostics());

        return (allDiagnostics, generatedSources);
    }

    /// <summary>
    /// Runs the analyzer and returns diagnostics.
    /// </summary>
    public static ImmutableArray<Diagnostic> GetDiagnostics(string source)
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
