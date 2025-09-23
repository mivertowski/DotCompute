// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using DotCompute.Generators.Kernel.Attributes;
using DotCompute.Generators.Kernel.Enums;
using DotCompute.Generators.Kernel.Enums;
using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Kernel.Analysis;

/// <summary>
/// Analyzes syntax trees to extract kernel information.
/// </summary>
public class KernelSyntaxAnalyzer : IKernelAnalyzer
{
    private readonly KernelAttributeParser _attributeParser;

    public KernelSyntaxAnalyzer()
    {
        _attributeParser = new KernelAttributeParser();
    }

    public bool IsKernelMethod(SyntaxNode node)
    {
        if (node is not MethodDeclarationSyntax methodDeclaration)
        {
            return false;
        }

        return methodDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString().Contains("Kernel"));
    }

    public bool IsKernelClass(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDeclaration)
        {
            return false;
        }

        return classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(m => m.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString().Contains("Kernel")));
    }

    public KernelMethodInfo? AnalyzeMethod(GeneratorSyntaxContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        var model = context.SemanticModel;
        var methodSymbol = model.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;

        if (methodSymbol is null)
        {
            return null;
        }

        var kernelAttribute = methodSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "KernelAttribute");

        if (kernelAttribute is null)
        {
            return null;
        }

        return new KernelMethodInfo
        {
            Name = methodSymbol.Name,
            ContainingType = methodSymbol.ContainingType.ToDisplayString(),
            Namespace = methodSymbol.ContainingNamespace.ToDisplayString(),
            Parameters = GetParameterInfo(methodSymbol),
            ReturnType = methodSymbol.ReturnType.ToDisplayString(),
            Backends = KernelAttributeParser.GetBackendsFromAttribute(kernelAttribute),
            VectorSize = KernelAttributeParser.GetVectorSizeFromAttribute(kernelAttribute),
            IsParallel = KernelAttributeParser.GetIsParallelFromAttribute(kernelAttribute),
            MethodDeclaration = methodDeclaration
        };
    }

    public KernelClassInfo? AnalyzeClass(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var model = context.SemanticModel;
        var classSymbol = model.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        if (classSymbol is null)
        {
            return null;
        }

        var kernelMethods = classSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes().Any(a => a.AttributeClass?.Name == "KernelAttribute"))
            .ToList();

        if (!kernelMethods.Any())
        {
            return null;
        }

        return new KernelClassInfo
        {
            Name = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
            KernelMethodNames = kernelMethods.Select(m => m.Name).ToList()
        };
    }

    private static List<ParameterInfo> GetParameterInfo(IMethodSymbol method)
    {
        return method.Parameters.Select(p => new ParameterInfo
        {
            Name = p.Name,
            Type = p.Type.ToDisplayString(),
            IsBuffer = IsBufferType(p.Type),
            IsReadOnly = p.RefKind == RefKind.In || p.Type.IsReadOnly
        }).ToList();
    }

    private static bool IsBufferType(ITypeSymbol type)
    {
        return type.Name.Contains("Buffer") ||
               type.Name.Contains("Span") ||
               type.TypeKind == TypeKind.Pointer ||
               type.AllInterfaces.Any(i => i.Name == "IBuffer");
    }
}