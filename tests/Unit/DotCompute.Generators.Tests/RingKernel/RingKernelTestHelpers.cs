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
        // Automatically append attribute definitions if not already defined in source
        // Check if types are DEFINED (not just imported via using directives)
        bool hasRingKernelDef = source.Contains("class RingKernelAttribute");
        bool hasKernelDef = source.Contains("class KernelAttribute");
        bool hasEnumsDef = source.Contains("enum RingKernelMode") || source.Contains("enum MessagingStrategy") || source.Contains("enum ComputeDomain");

        // Always append stubs if any required type is missing
        if (!hasRingKernelDef || !hasKernelDef || !hasEnumsDef)
        {
            source = RingKernelTestAttributeDefinitions.WithAttributeDefinitions(source);
        }

        var parseOptions = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp13);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);

        // Get all basic references including System.Collections
        var basicReferences = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Where(a => a.GetName().Name is "System.Runtime" or "System.Collections" or "System.Linq" or
                       "System.Threading.Tasks" or "System.Memory" or "System.Collections.Generic" or
                       "mscorlib" or "netstandard" or "System.Private.CoreLib")
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        // Add specific type references for generated code dependencies
        basicReferences.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        basicReferences.Add(MetadataReference.CreateFromFile(typeof(System.Span<>).Assembly.Location));
        basicReferences.Add(MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.Unsafe).Assembly.Location));
        basicReferences.Add(MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location));
        basicReferences.Add(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.Dictionary<,>).Assembly.Location));
        basicReferences.Add(MetadataReference.CreateFromFile(typeof(System.Threading.CancellationToken).Assembly.Location));
        basicReferences.Add(MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "test",
            syntaxTrees: new[] { tree },
            references: basicReferences.Distinct(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
                .WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp13)
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
        var parseOptions = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp13);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
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
