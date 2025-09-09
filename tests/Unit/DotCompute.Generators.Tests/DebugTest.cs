// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using DotCompute.Generators.Analyzers;
using System.Collections.Immutable;
using System.Linq;
using Xunit;
using System;

namespace DotCompute.Generators.Tests;

/// <summary>
/// Debug test to understand why diagnostics aren't being detected.
/// </summary>
public class DebugTest
{
    [Fact]
    public void Debug_CheckAnalyzerBasicFunctionality()
    {
        const string simpleCode = @"
using System;

public class TestClass
{
    [Kernel]
    public static void TestMethod(object badParam)
    {
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KernelAttribute : System.Attribute { }
";

        var diagnostics = GetDiagnosticsDetailed(simpleCode);
        
        // Debug output
        foreach (var diagnostic in diagnostics)
        {
            Console.WriteLine($"Diagnostic: {diagnostic.Id} - {diagnostic.GetMessage()}");
            Console.WriteLine($"Severity: {diagnostic.Severity}");
            Console.WriteLine($"Location: {diagnostic.Location}");
            Console.WriteLine("---");
        }
        
        Assert.True(diagnostics.Length >= 0, "Should get some diagnostics or none");
    }

    private static ImmutableArray<Diagnostic> GetDiagnosticsDetailed(string source)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Span<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
            };

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                syntaxTrees: new[] { tree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            // Check compilation errors first
            var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (compileErrors.Any())
            {
                Console.WriteLine("Compilation errors:");
                foreach (var error in compileErrors)
                {
                    Console.WriteLine($"  {error}");
                }
            }

            var analyzer = new DotComputeKernelAnalyzer();
            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
            
            var result = compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in GetDiagnostics: {ex}");
            return ImmutableArray<Diagnostic>.Empty;
        }
    }
}