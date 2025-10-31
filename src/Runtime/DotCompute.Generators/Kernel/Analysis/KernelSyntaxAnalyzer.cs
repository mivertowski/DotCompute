// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Kernel.Analysis;

/// <summary>
/// Analyzes syntax trees to extract kernel information.
/// </summary>
public class KernelSyntaxAnalyzer : IKernelAnalyzer
{
    public KernelSyntaxAnalyzer()
    {
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

        if (model.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        var kernelAttribute = methodSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "KernelAttribute");

        if (kernelAttribute is null)
        {
            return null;
        }

        var methodInfo = new KernelMethodInfo
        {
            Name = methodSymbol.Name,
            ContainingType = methodSymbol.ContainingType.ToDisplayString(),
            Namespace = methodSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : methodSymbol.ContainingNamespace.ToDisplayString(),
            ReturnType = methodSymbol.ReturnType.ToDisplayString(),
            VectorSize = KernelAttributeParser.GetVectorSizeFromAttribute(kernelAttribute),
            IsParallel = KernelAttributeParser.GetIsParallelFromAttribute(kernelAttribute),
            MethodDeclaration = methodDeclaration
        };

        // Populate read-only collections
        var parameters = GetParameterInfo(methodSymbol);
        foreach (var param in parameters)
        {
            methodInfo.Parameters.Add(param);
        }

        var backends = KernelAttributeParser.GetBackendsFromAttribute(kernelAttribute);
        foreach (var backend in backends)
        {
            methodInfo.Backends.Add(backend);
        }

        return methodInfo;
    }

    public KernelClassInfo? AnalyzeClass(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var model = context.SemanticModel;

        if (model.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        var kernelMethods = classSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes().Any(a => a.AttributeClass?.Name == "KernelAttribute"))
            .ToList();

        if (kernelMethods.Count == 0)
        {
            return null;
        }

        var classInfo = new KernelClassInfo
        {
            Name = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : classSymbol.ContainingNamespace.ToDisplayString()
        };

        // Populate read-only collection
        foreach (var method in kernelMethods)
        {
            classInfo.KernelMethodNames.Add(method.Name);
        }

        return classInfo;
    }

    private static List<ParameterInfo> GetParameterInfo(IMethodSymbol method)
    {
        return [.. method.Parameters.Select(p => new ParameterInfo
        {
            Name = p.Name,
            Type = p.Type.ToDisplayString(),
            IsBuffer = IsBufferType(p.Type),
            IsReadOnly = p.RefKind == RefKind.In || p.Type.IsReadOnly
        })];
    }

    private static bool IsBufferType(ITypeSymbol type)
    {
        return type.Name.Contains("Buffer") ||
               type.Name.Contains("Span") ||
               type.TypeKind == TypeKind.Pointer ||
               type.AllInterfaces.Any(i => i.Name == "IBuffer");
    }
}
