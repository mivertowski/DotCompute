// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DotCompute.Generators.Kernel.Generation;
using System.Linq;
using Xunit;

namespace DotCompute.Generators.Tests.RingKernel;

/// <summary>
/// Tests backend extraction logic.
/// </summary>
public sealed class BackendExtractionTest
{
    [Fact]
    public void Test_BackendExtraction_FromAttributeWithNoArguments()
    {
        const string code = @"
using System;

namespace TestApp
{
    public static class Processors
    {
        [RingKernel]
        public static void RingMethod(Span<int> data)
        {
        }
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public class RingKernelAttribute : System.Attribute { }
";

        var tree = CSharpSyntaxTree.ParseText(code);
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

        var model = compilation.GetSemanticModel(tree);

        // Find the method
        var methodNode = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "RingMethod");

        var methodSymbol = model.GetDeclaredSymbol(methodNode) as IMethodSymbol;
        Assert.NotNull(methodSymbol);

        // Get the Ring Kernel attribute
        var attr = methodSymbol.GetAttributes().First();

        // Analyze the configuration
        var info = RingKernelAttributeAnalyzer.AnalyzeRingKernelConfiguration(attr);

        System.Console.WriteLine($"Backends count: {info.Backends.Count}");
        foreach (var backend in info.Backends)
        {
            System.Console.WriteLine($"  - {backend}");
        }

        // Validate configuration
        var errors = RingKernelAttributeAnalyzer.ValidateConfiguration(info);
        System.Console.WriteLine($"\nValidation errors: {errors.Count}");
        foreach (var error in errors)
        {
            System.Console.WriteLine($"  - {error}");
        }

        Assert.NotEqual(0, info.Backends.Count);
    }
}
