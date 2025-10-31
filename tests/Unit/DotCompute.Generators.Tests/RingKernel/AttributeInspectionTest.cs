// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Xunit;

namespace DotCompute.Generators.Tests.RingKernel;

/// <summary>
/// Inspects what Roslyn sees for Ring Kernel attributes.
/// </summary>
public sealed class AttributeInspectionTest
{
    [Fact]
    public void Inspect_RingKernelAttribute_WhatDoesRoslynSee()
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

        // Inspect attributes
        var attributes = methodSymbol.GetAttributes();
        System.Console.WriteLine($"Method has {attributes.Length} attributes");

        foreach (var attr in attributes)
        {
            System.Console.WriteLine($"\nAttribute:");
            System.Console.WriteLine($"  AttributeClass.Name: '{attr.AttributeClass?.Name}'");
            System.Console.WriteLine($"  AttributeClass.ToDisplayString(): '{attr.AttributeClass?.ToDisplayString()}'");
            System.Console.WriteLine($"  AttributeClass.ContainingNamespace: '{attr.AttributeClass?.ContainingNamespace.ToDisplayString()}'");

            // Check the specific condition used by RingKernelMethodAnalyzer
            var matches = attr.AttributeClass?.Name is "RingKernelAttribute" or "RingKernel";
            System.Console.WriteLine($"  Matches (Name is 'RingKernelAttribute' or 'RingKernel'): {matches}");
        }

        // The attribute should be found
        Assert.True(attributes.Length > 0, "Method should have at least one attribute");
    }
}
