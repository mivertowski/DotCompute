// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Analyzers;

/// <summary>
/// Helper methods for kernel analysis operations.
/// </summary>
internal static class KernelAnalysisHelpers
{
    /// <summary>
    /// Checks if a symbol has the [Kernel] attribute.
    /// </summary>
    public static bool HasKernelAttribute(ISymbol symbol)
        => symbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "KernelAttribute");

    /// <summary>
    /// Gets the [Kernel] attribute from a symbol if it exists.
    /// </summary>
    public static AttributeData? GetKernelAttribute(ISymbol symbol)
        => symbol.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.Name == "KernelAttribute");

    /// <summary>
    /// Determines if a method looks like a kernel based on its signature and body.
    /// </summary>
    public static bool LooksLikeKernelMethod(MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
        => methodSymbol.IsStatic &&
        methodSymbol.Parameters.Any(p => p.Type.ToString().Contains("Span")) &&
        methodSyntax.Body?.Statements.OfType<ForStatementSyntax>().Any() == true;

    /// <summary>
    /// Validates if a type is a valid kernel parameter type.
    /// </summary>
    public static bool IsValidKernelParameterType(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString();
        return typeName.Contains("Span<") ||
               typeName.Contains("ReadOnlySpan<") ||
               type.SpecialType is SpecialType.System_Int32 or SpecialType.System_Single or SpecialType.System_Double;
    }

    /// <summary>
    /// Determines if a for loop can be vectorized.
    /// </summary>
    public static bool CanBeVectorized(ForStatementSyntax forLoop)
    {
        // Simple heuristic - loops with array access and arithmetic operations
        return forLoop.DescendantNodes().OfType<ElementAccessExpressionSyntax>().Any() &&
               forLoop.DescendantNodes().OfType<BinaryExpressionSyntax>().Any(b =>
                   b.IsKind(SyntaxKind.AddExpression) || b.IsKind(SyntaxKind.MultiplyExpression));
    }

    /// <summary>
    /// Checks if an element access has a suboptimal memory access pattern.
    /// </summary>
    public static bool HasSuboptimalAccessPattern(ElementAccessExpressionSyntax access)
    {
        // Heuristic for non-sequential access patterns
        return access.ArgumentList.Arguments.Count == 1 &&
               (access.ArgumentList.Arguments[0].Expression.ToString().Contains("*") ||
                access.ArgumentList.Arguments[0].Expression.ToString().Contains("%"));
    }

    /// <summary>
    /// Analyzes computational complexity of a method.
    /// </summary>
    public static string AnalyzeComputationalComplexity(MethodDeclarationSyntax methodSyntax)
    {
        var loopCount = methodSyntax.DescendantNodes().OfType<ForStatementSyntax>().Count();
        var mathOperations = methodSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Count(inv => inv.ToString().Contains("Math."));

        return (loopCount, mathOperations) switch
        {
            ( > 1, > 5) => "HighComplexity",
            ( > 0, > 2) => "MediumComplexity",
            _ => "LowComplexity"
        };
    }

    /// <summary>
    /// Gets optimal backends for a given complexity level.
    /// </summary>
    public static string[] GetOptimalBackends(string complexity) => complexity switch
    {
        "HighComplexity" => ["CUDA", "Metal", "OpenCL"],
        "MediumComplexity" => ["CUDA", "CPU"],
        "LowComplexity" => ["CPU"],
        _ => ["CPU"]
    };

    /// <summary>
    /// Extracts backend configuration from a kernel attribute.
    /// </summary>
    public static string[]? ExtractBackendsFromAttribute(AttributeData attribute)
    {
        if (attribute.AttributeClass?.Name != "KernelAttribute")
        {
            return null;
        }

        _ = new List<string>();

        // Look for named arguments with 'Backends' parameter
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "Backends" && namedArg.Value.Value is int backendFlags)
            {
                return ParseBackendFlags(backendFlags);
            }
        }

        // Look for constructor arguments
        if (attribute.ConstructorArguments.Length > 0)
        {
            var firstArg = attribute.ConstructorArguments[0];
            if (firstArg.Value is int flags)
            {
                return ParseBackendFlags(flags);
            }
        }

        // Default to CPU and CUDA if no specific backends specified
        return ["CPU", "CUDA"];
    }

    /// <summary>
    /// Parses backend flags into backend names.
    /// </summary>
    private static string[] ParseBackendFlags(int flags)
    {
        var backends = new List<string>();

        if ((flags & 1) != 0)
        {
            backends.Add("CPU");
        }

        if ((flags & 2) != 0)
        {
            backends.Add("CUDA");
        }

        if ((flags & 4) != 0)
        {
            backends.Add("Metal");
        }

        if ((flags & 8) != 0)
        {
            backends.Add("OpenCL");
        }


        return backends.ToArray();
    }

    /// <summary>
    /// Extracts the loop variable from a for statement.
    /// </summary>
    public static string? ExtractLoopVariable(ForStatementSyntax forLoop)
    {
        if (forLoop.Declaration?.Variables.FirstOrDefault() is VariableDeclaratorSyntax variable)
        {
            return variable.Identifier.ValueText;
        }
        return null;
    }

    /// <summary>
    /// Checks if a method uses the Kernel.ThreadId threading model.
    /// </summary>
    public static bool UsesKernelThreading(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(m => m.ToString().Contains("Kernel.ThreadId") || m.ToString().Contains("ThreadId."));
    }

    /// <summary>
    /// Checks if a method has bounds checking for element access.
    /// </summary>
    public static bool HasBoundsCheckForAccess(MethodDeclarationSyntax method, ElementAccessExpressionSyntax elementAccess)
    {
        // Look for bounds check patterns in the method
        var boundsChecks = method.DescendantNodes()
            .OfType<BinaryExpressionSyntax>()
            .Where(b => (b.IsKind(SyntaxKind.LessThanExpression) ||
                        b.IsKind(SyntaxKind.LessThanOrEqualExpression) ||
                        b.IsKind(SyntaxKind.GreaterThanOrEqualExpression)) &&
                       (b.ToString().Contains("Length") || b.ToString().Contains(".Count")));

        return boundsChecks.Any();
    }

    /// <summary>
    /// Checks for nested loops in a method.
    /// </summary>
    public static bool HasNestedLoops(MethodDeclarationSyntax methodSyntax)
    {
        return methodSyntax.DescendantNodes()
            .OfType<ForStatementSyntax>()
            .Any(loop => loop.Ancestors().OfType<ForStatementSyntax>().Any());
    }

    /// <summary>
    /// Checks for complex conditional logic.
    /// </summary>
    public static bool HasComplexConditionals(MethodDeclarationSyntax methodSyntax)
    {
        return methodSyntax.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Any(ifStmt => ifStmt.DescendantNodes().OfType<BinaryExpressionSyntax>()
                .Count(b => b.IsKind(SyntaxKind.LogicalAndExpression) || b.IsKind(SyntaxKind.LogicalOrExpression)) > 2);
    }

    /// <summary>
    /// Counts non-indexed assignments that may cause race conditions.
    /// </summary>
    public static int CountPotentialRaceConditionAssignments(MethodDeclarationSyntax methodSyntax)
    {
        return methodSyntax.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Count(a => !a.Left.ToString().Contains("[") &&
                       !a.Left.ToString().Contains("ThreadId"));
    }

    /// <summary>
    /// Checks for potentially non-thread-safe method calls.
    /// </summary>
    public static bool HasNonThreadSafeMethodCalls(MethodDeclarationSyntax methodSyntax)
    {
        return methodSyntax.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => !inv.ToString().Contains("Math.") &&
                       !inv.ToString().Contains("MathF.") &&
                       !inv.ToString().StartsWith("System."));
    }
}