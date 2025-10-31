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
/// Manual test with inline attribute definitions (like regular Kernel tests).
/// </summary>
public sealed class ManualRingKernelTest
{
    [Fact]
    public void Manual_RingKernelWithInlineAttribute_Generates()
    {
        const string code = @"
using System;
using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.Enums;

namespace TestApp
{
    public static class Processors
    {
        [RingKernel]
        public static void SimpleProcess(Span<int> data)
        {
        }
    }
}

// Inline attribute definitions
namespace DotCompute.Abstractions.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class RingKernelAttribute : System.Attribute
    {
        public string? KernelId { get; set; }
        public int Capacity { get; set; } = 1024;
    }
}

namespace DotCompute.Abstractions.Enums
{
    public enum RingKernelMode { Persistent, EventDriven }
}
";

        var (generatedSources, diagnostics) = RunGenerator(code);

        System.Console.WriteLine($"Generated {generatedSources.Length} sources");
        foreach (var src in generatedSources)
        {
            System.Console.WriteLine($"  - {src.HintName}");
        }

        System.Console.WriteLine($"\nDiagnostics: {diagnostics.Length}");
        foreach (var diag in diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning))
        {
            System.Console.WriteLine($"  {diag.Severity}: {diag.Id} - {diag.GetMessage()}");
        }

        Assert.NotEmpty(generatedSources);

        var registry = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        Assert.False(string.IsNullOrEmpty(registry.HintName), "RingKernelRegistry was not generated");
        Assert.NotNull(registry.SourceText);
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

        // Combine generator diagnostics with compilation diagnostics
        var allDiagnostics = generatorDiagnostics.AddRange(outputCompilation.GetDiagnostics());

        return (sources, allDiagnostics);
    }
}
