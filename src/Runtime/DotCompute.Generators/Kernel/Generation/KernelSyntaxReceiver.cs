// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Kernel.Generation;

/// <summary>
/// Incremental syntax receiver that identifies kernel methods and classes for code generation.
/// Efficiently filters syntax nodes to find only those relevant for kernel generation.
/// </summary>
/// <remarks>
/// This class implements the first phase of kernel source generation by identifying
/// syntax nodes that contain kernel attributes or implement kernel interfaces.
/// It uses efficient filtering to minimize the number of nodes passed to the
/// semantic analysis phase.
/// </remarks>
public sealed class KernelSyntaxReceiver
{
    /// <summary>
    /// Creates syntax providers for incremental generation of kernel methods.
    /// </summary>
    /// <param name="context">The incremental generator initialization context.</param>
    /// <param name="transform">Transform function to convert syntax context to target type.</param>
    /// <returns>An incremental values provider for kernel methods.</returns>
    public static IncrementalValuesProvider<T> CreateKernelMethodProvider<T>(
        IncrementalGeneratorInitializationContext context,
        System.Func<GeneratorSyntaxContext, T?> transform)
        where T : class
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider<T>(
                predicate: static (s, _) => IsKernelMethod(s),
                transform: (ctx, _) => transform(ctx)!)
            .Where(static m => m is not null);
    }

    /// <summary>
    /// Creates syntax providers for incremental generation of kernel classes.
    /// </summary>
    /// <param name="context">The incremental generator initialization context.</param>
    /// <param name="transform">Transform function to convert syntax context to target type.</param>
    /// <returns>An incremental values provider for kernel classes.</returns>
    public static IncrementalValuesProvider<T> CreateKernelClassProvider<T>(
        IncrementalGeneratorInitializationContext context,
        System.Func<GeneratorSyntaxContext, T?> transform)
        where T : class
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider<T>(
                predicate: static (s, _) => IsKernelClass(s),
                transform: (ctx, _) => transform(ctx)!)
            .Where(static c => c is not null);
    }

    /// <summary>
    /// Determines if a syntax node represents a method with a [Kernel] attribute.
    /// </summary>
    /// <param name="node">The syntax node to examine.</param>
    /// <returns>True if the node is a kernel method; otherwise, false.</returns>
    /// <remarks>
    /// This method performs fast syntactic analysis without semantic model access
    /// to efficiently filter potential kernel methods. It looks for:
    /// - Method declarations
    /// - Presence of attributes containing "Kernel" in the name
    /// </remarks>
    public static bool IsKernelMethod(SyntaxNode node)
    {
        if (node is not MethodDeclarationSyntax methodDeclaration)
        {
            return false;
        }

        // Check for [Kernel] attribute using fast string matching
        return HasKernelAttribute(methodDeclaration.AttributeLists);
    }

    /// <summary>
    /// Determines if a syntax node represents a class containing kernel methods.
    /// </summary>
    /// <param name="node">The syntax node to examine.</param>
    /// <returns>True if the node is a class with kernel methods; otherwise, false.</returns>
    /// <remarks>
    /// This method identifies classes that contain kernel methods or implement
    /// kernel interfaces. It performs efficient syntactic filtering to avoid
    /// unnecessary semantic analysis on non-kernel classes.
    /// </remarks>
    public static bool IsKernelClass(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDeclaration)
        {
            return false;
        }

        // Check if class has any methods with kernel attributes
        return classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(m => HasKernelAttribute(m.AttributeLists));
    }

    /// <summary>
    /// Checks if the given attribute lists contain a [Kernel] attribute.
    /// </summary>
    /// <param name="attributeLists">The attribute lists to examine.</param>
    /// <returns>True if a kernel attribute is found; otherwise, false.</returns>
    /// <remarks>
    /// This method uses efficient string matching to identify kernel attributes
    /// without requiring semantic model access. It handles common variations:
    /// - [Kernel]
    /// - [KernelAttribute]
    /// - Fully qualified attribute names
    /// </remarks>
    private static bool HasKernelAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        return attributeLists
            .SelectMany(static al => al.Attributes)
            .Any(static a => IsKernelAttributeName(a.Name.ToString()));
    }

    /// <summary>
    /// Determines if an attribute name represents a kernel attribute.
    /// </summary>
    /// <param name="attributeName">The attribute name to check.</param>
    /// <returns>True if the name represents a kernel attribute; otherwise, false.</returns>
    /// <remarks>
    /// This method handles various forms of kernel attribute names:
    /// - "Kernel" (short form)
    /// - "KernelAttribute" (full form)
    /// - Qualified names containing "Kernel"
    /// </remarks>
    private static bool IsKernelAttributeName(string attributeName)
    {
        return attributeName.Contains("Kernel");
    }

    /// <summary>
    /// Gets all method declarations from a class that have kernel attributes.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to examine.</param>
    /// <returns>An enumerable of method declarations with kernel attributes.</returns>
    public static IEnumerable<MethodDeclarationSyntax> GetKernelMethods(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => HasKernelAttribute(m.AttributeLists));
    }

    /// <summary>
    /// Validates that a method declaration is suitable for kernel generation.
    /// </summary>
    /// <param name="methodDeclaration">The method declaration to validate.</param>
    /// <returns>True if the method is valid for kernel generation; otherwise, false.</returns>
    /// <remarks>
    /// This method performs basic syntactic validation to ensure the method
    /// can be processed for kernel generation. It checks:
    /// - Method has a body or is extern
    /// - Method is static (required for kernels)
    /// - Method signature is compatible with kernel generation
    /// </remarks>
    public static bool IsValidKernelMethod(MethodDeclarationSyntax methodDeclaration)
    {
        // Must be static for kernel methods
        if (!methodDeclaration.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword))
        {
            return false;
        }

        // Must have either a body or be extern (for pre-compiled kernels)
        if (methodDeclaration.Body == null && methodDeclaration.ExpressionBody == null &&
            !methodDeclaration.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ExternKeyword))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Extracts the namespace from a syntax node.
    /// </summary>
    /// <param name="node">The syntax node to examine.</param>
    /// <returns>The namespace containing the node, or empty string if none.</returns>
    public static string GetContainingNamespace(SyntaxNode node)
    {
        var namespaceDeclaration = node.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return namespaceDeclaration?.Name.ToString() ?? string.Empty;
    }
}