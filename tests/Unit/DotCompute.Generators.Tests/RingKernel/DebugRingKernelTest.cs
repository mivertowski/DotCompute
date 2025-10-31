// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Xunit;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace DotCompute.Generators.Tests.RingKernel;

public class DebugRingKernelTest
{
    [Fact]
    public void Debug_SimpleRingKernel_InspectGeneration()
    {
        const string source = """
            using DotCompute.Abstractions.Attributes;
            using System;

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
            """;

        System.Console.WriteLine("=== SIMPLE RING KERNEL TEST ===");
        System.Console.WriteLine($"Source contains [RingKernel]: {source.Contains("[RingKernel]")}");
        System.Console.WriteLine($"Source length: {source.Length} chars\n");

        var (diagnostics, generatedSources) = RingKernelTestHelpers.RunGenerator(source);

        // Debug output
        System.Console.WriteLine($"Total generated sources: {generatedSources.Length}");
        foreach (var gen in generatedSources)
        {
            System.Console.WriteLine($"  - HintName: '{gen.HintName}'");
            System.Console.WriteLine($"    SourceText null: {gen.SourceText == null}");
            if (gen.SourceText != null)
            {
                System.Console.WriteLine($"    Content length: {gen.SourceText.Length} chars");
            }
        }

        System.Console.WriteLine($"\nTotal diagnostics: {diagnostics.Length}");
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        System.Console.WriteLine($"  Errors: {errors.Count}");
        System.Console.WriteLine($"  Warnings: {warnings.Count}");

        if (errors.Any())
        {
            System.Console.WriteLine("\nFirst 5 errors:");
            foreach (var err in errors.Take(5))
            {
                System.Console.WriteLine($"  {err.Id}: {err.GetMessage()}");
            }
        }

        var registrySource = generatedSources.FirstOrDefault(s => s.HintName.Contains("RingKernelRegistry"));
        System.Console.WriteLine($"\nRegistry search result: HintName='{registrySource.HintName}'");

        Assert.False(string.IsNullOrEmpty(registrySource.HintName), "Registry source was not generated - HintName is empty");
        Assert.NotNull(registrySource.SourceText);
    }
}
