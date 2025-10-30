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
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "KernelAttribute")
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Gets the [Kernel] attribute from a symbol if it exists.
    /// </summary>
    public static AttributeData? GetKernelAttribute(ISymbol symbol)
        => symbol.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.Name == "KernelAttribute");

    /// <summary>
    /// Determines if a method looks like a kernel based on its signature and body.
    /// </summary>
    public static bool LooksLikeKernelMethod(MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        if (!methodSymbol.IsStatic)
        {
            return false;
        }

        var hasSpanParameter = false;
        foreach (var p in methodSymbol.Parameters)
        {
            if (p.Type.ToString().IndexOf("Span", StringComparison.Ordinal) >= 0)
            {
                hasSpanParameter = true;
                break;
            }
        }

        if (!hasSpanParameter)
        {
            return false;
        }

        if (methodSyntax.Body == null)
        {
            return false;
        }

        foreach (var stmt in methodSyntax.Body.Statements)
        {
            if (stmt is ForStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates if a type is a valid kernel parameter type.
    /// </summary>
    public static bool IsValidKernelParameterType(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString();
        return typeName.IndexOf("Span<", StringComparison.Ordinal) >= 0 ||
               typeName.IndexOf("ReadOnlySpan<", StringComparison.Ordinal) >= 0 ||
               type.SpecialType is SpecialType.System_Int32 or SpecialType.System_Single or SpecialType.System_Double;
    }

    /// <summary>
    /// Determines if a for loop can be vectorized.
    /// </summary>
    public static bool CanBeVectorized(ForStatementSyntax forLoop)
    {
        // Simple heuristic - loops with array access and arithmetic operations
        var hasElementAccess = false;
        var hasArithmeticOps = false;

        foreach (var node in forLoop.DescendantNodes())
        {
            if (node is ElementAccessExpressionSyntax)
            {
                hasElementAccess = true;
            }
            else if (node is BinaryExpressionSyntax b &&
                     (b.IsKind(SyntaxKind.AddExpression) || b.IsKind(SyntaxKind.MultiplyExpression)))
            {
                hasArithmeticOps = true;
            }

            if (hasElementAccess && hasArithmeticOps)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if an element access has a suboptimal memory access pattern.
    /// </summary>
    public static bool HasSuboptimalAccessPattern(ElementAccessExpressionSyntax access)
    {
        // Heuristic for non-sequential access patterns
        if (access.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        var exprText = access.ArgumentList.Arguments[0].Expression.ToString();
        return exprText.IndexOf("*", StringComparison.Ordinal) >= 0 ||
               exprText.IndexOf("%", StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Analyzes computational complexity of a method.
    /// </summary>
    public static string AnalyzeComputationalComplexity(MethodDeclarationSyntax methodSyntax)
    {
        var loopCount = 0;
        var mathOperations = 0;

        foreach (var node in methodSyntax.DescendantNodes())
        {
            if (node is ForStatementSyntax)
            {
                loopCount++;
            }
            else if (node is InvocationExpressionSyntax inv &&
                     inv.ToString().IndexOf("Math.", StringComparison.Ordinal) >= 0)
            {
                mathOperations++;
            }
        }

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


        return [.. backends];
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
            .Any(m => m.ToString().IndexOf("Kernel.ThreadId", StringComparison.Ordinal) >= 0 || m.ToString().IndexOf("ThreadId.", StringComparison.Ordinal) >= 0);
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
                       (b.ToString().IndexOf("Length", StringComparison.Ordinal) >= 0 || b.ToString().IndexOf(".Count", StringComparison.Ordinal) >= 0));

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
            .Count(a => a.Left.ToString().IndexOf("[", StringComparison.Ordinal) < 0 &&
                       a.Left.ToString().IndexOf("ThreadId", StringComparison.Ordinal) < 0);
    }

    /// <summary>
    /// Checks for potentially non-thread-safe method calls.
    /// </summary>
    public static bool HasNonThreadSafeMethodCalls(MethodDeclarationSyntax methodSyntax)
    {
        return methodSyntax.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv =>
            {
                var invString = inv.ToString();
                return invString.IndexOf("Math.", StringComparison.Ordinal) < 0 &&
                       invString.IndexOf("MathF.", StringComparison.Ordinal) < 0 &&
                       !invString.StartsWith("System.", StringComparison.Ordinal);
            });
    }
}
