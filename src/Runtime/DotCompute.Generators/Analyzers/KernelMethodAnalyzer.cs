// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotCompute.Generators.Analyzers;

/// <summary>
/// Analyzes kernel method declarations for compliance and optimization opportunities.
/// </summary>
internal static class KernelMethodAnalyzer
{
    /// <summary>
    /// Analyzes a kernel method for various issues and optimization opportunities.
    /// </summary>
    public static void AnalyzeKernelMethod(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // Check for empty or comment-only kernels first
        if (methodSyntax.Body != null)
        {
            var hasStatements = methodSyntax.Body.Statements.Any();
            if (!hasStatements)
            {
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DC003",
                        "Kernel has no implementation",
                        "Kernel method '{0}' has no implementation. Empty kernels should be removed or completed.",
                        "DotCompute.Kernel",
                        DiagnosticSeverity.Warning,
                        isEnabledByDefault: true
                    ),
                    methodSyntax.Identifier.GetLocation(),
                    methodSymbol.Name
                );
                context.ReportDiagnostic(diagnostic);
            }
        }

        // DC001: Check if method is static
        ValidateMethodIsStatic(context, methodSyntax, methodSymbol);

        // DC002: Validate parameters
        ValidateKernelParameters(context, methodSyntax, methodSymbol);

        // DC003: Check for unsupported constructs
        CheckUnsupportedConstructs(context, methodSyntax, methodSymbol);

        // DC004: Check for vectorization opportunities
        CheckVectorizationOpportunities(context, methodSyntax, methodSymbol);

        // DC005: Analyze memory access patterns
        AnalyzeMemoryAccessPatterns(context, methodSyntax, methodSymbol);

        // DC006: Check for potential register spilling
        CheckRegisterSpilling(context, methodSyntax, methodSymbol);

        // DC010: Check for correct indexing patterns
        CheckThreadingModel(context, methodSyntax, methodSymbol);

        // DC011: Check for bounds checking
        CheckBoundsValidation(context, methodSyntax, methodSymbol);

        // DC012: Validate backend selection
        ValidateBackendSelection(context, methodSyntax, methodSymbol);

        // DC008: Check for unnecessary complexity
        CheckUnnecessaryComplexity(context, methodSyntax, methodSymbol);

        // DC009: Check for thread safety issues
        CheckThreadSafety(context, methodSyntax, methodSymbol);
    }

    private static void ValidateMethodIsStatic(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        if (!methodSymbol.IsStatic)
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelMethodMustBeStatic,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ValidateKernelParameters(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // Check if this is a Ring Kernel (different parameter validation rules)
        var isRingKernel = KernelAnalysisHelpers.IsRingKernel(methodSymbol);

        foreach (var parameter in methodSymbol.Parameters)
        {
            var parameterType = parameter.Type;

            if (!KernelAnalysisHelpers.IsValidKernelParameterType(parameterType, isRingKernel))
            {
                var parameterNode = methodSyntax.ParameterList.Parameters
                    .FirstOrDefault(p => p.Identifier.ValueText == parameter.Name);

                if (parameterNode != null)
                {
                    var diagnostic = Diagnostic.Create(
                        KernelDiagnostics.KernelMethodInvalidParameters,
                        parameterNode.GetLocation(),
                        methodSymbol.Name,
                        parameter.Name,
                        $"Type '{parameterType.ToDisplayString()}' is not supported in kernels"
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static void CheckUnsupportedConstructs(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // Check for non-void return type
        if (!methodSymbol.ReturnsVoid)
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelMethodUnsupportedConstruct,
                methodSyntax.ReturnType.GetLocation(),
                methodSymbol.Name,
                $"non-void return type '{methodSymbol.ReturnType.ToDisplayString()}'",
                methodSyntax.ReturnType.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            );
            context.ReportDiagnostic(diagnostic);
        }

        var unsupportedNodes = methodSyntax.DescendantNodes().Where(node => node switch
        {
            ThrowStatementSyntax => true,
            UnsafeStatementSyntax => true,
            _ => false
        });

        foreach (var node in unsupportedNodes)
        {
            var constructName = node switch
            {
                ThrowStatementSyntax => "throw statements",
                UnsafeStatementSyntax => "unsafe code blocks",
                _ => node.GetType().Name
            };

            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelMethodUnsupportedConstruct,
                node.GetLocation(),
                methodSymbol.Name,
                constructName,
                node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CheckVectorizationOpportunities(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // First check for recursive calls - this is an error
        var invocations = methodSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            // Check if the invocation is calling the same method
            if (invocation.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText == methodSymbol.Name)
            {
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DC004",
                        "Kernel contains recursive call",
                        "Kernel method '{0}' contains a recursive call which is not supported in GPU execution",
                        "DotCompute.Kernel",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true
                    ),
                    invocation.GetLocation(),
                    methodSymbol.Name
                );
                context.ReportDiagnostic(diagnostic);
                return; // Don't check for vectorization if there's a recursive call
            }
        }

        // Then check for vectorization opportunities
        var forLoops = methodSyntax.DescendantNodes().OfType<ForStatementSyntax>();

        foreach (var forLoop in forLoops)
        {
            if (KernelAnalysisHelpers.CanBeVectorized(forLoop))
            {
                var diagnostic = Diagnostic.Create(
                    KernelDiagnostics.KernelCanBeVectorized,
                    forLoop.ForKeyword.GetLocation(),
                    methodSymbol.Name
                );
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void AnalyzeMemoryAccessPatterns(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // First check for unsafe pointer parameters - this is a warning
        foreach (var parameter in methodSymbol.Parameters)
        {
            if (parameter.Type is IPointerTypeSymbol)
            {
                var parameterNode = methodSyntax.ParameterList.Parameters
                    .FirstOrDefault(p => p.Identifier.ValueText == parameter.Name);

                if (parameterNode != null)
                {
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DC005",
                            "Kernel uses unsafe pointer parameter",
                            "Kernel method '{0}' uses unsafe pointer parameter which should be avoided. Use Span<T> or ReadOnlySpan<T> instead for safer memory access.",
                            "DotCompute.Kernel",
                            DiagnosticSeverity.Warning,
                            isEnabledByDefault: true
                        ),
                        parameterNode.GetLocation(),
                        methodSymbol.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // Then check for suboptimal memory access patterns
        var elementAccesses = methodSyntax.DescendantNodes().OfType<ElementAccessExpressionSyntax>();

        foreach (var access in elementAccesses)
        {
            if (KernelAnalysisHelpers.HasSuboptimalAccessPattern(access))
            {
                var diagnostic = Diagnostic.Create(
                    KernelDiagnostics.KernelSuboptimalMemoryAccess,
                    access.GetLocation(),
                    methodSymbol.Name,
                    "Non-coalesced memory access detected"
                );
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void CheckRegisterSpilling(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // Check for exception handling first - this uses DC003 for unsupported constructs
        var tryStatements = methodSyntax.DescendantNodes().OfType<TryStatementSyntax>();
        foreach (var tryStatement in tryStatements)
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelMethodUnsupportedConstruct,
                tryStatement.GetLocation(),
                methodSymbol.Name,
                "exception handling (try-catch blocks)",
                tryStatement.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            );
            context.ReportDiagnostic(diagnostic);
        }

        // Then check for register spilling - use DC006
        var localVariables = methodSyntax.DescendantNodes().OfType<VariableDeclarationSyntax>();
        var variableCount = localVariables.SelectMany(v => v.Variables).Count();

        if (variableCount > 16) // GPU register limit threshold
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelRegisterSpilling,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                variableCount
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CheckThreadingModel(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        var forLoops = methodSyntax.DescendantNodes().OfType<ForStatementSyntax>();
        var hasKernelThreadAccess = KernelAnalysisHelpers.UsesKernelThreading(methodSyntax);

        if (forLoops.Any() && !hasKernelThreadAccess)
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelIncorrectIndexing,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CheckBoundsValidation(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        var elementAccesses = methodSyntax.DescendantNodes().OfType<ElementAccessExpressionSyntax>();

        // Check for explicit bounds validation (if statements only, not loop conditions)
        var hasBoundsCheck = methodSyntax.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Any(ifStmt => ifStmt.Condition is BinaryExpressionSyntax condition &&
                          (condition.IsKind(SyntaxKind.LessThanExpression) ||
                           condition.IsKind(SyntaxKind.LessThanOrEqualExpression)) &&
                          (condition.ToString().Contains("Length") || condition.ToString().Contains(".Count")));

        // Also check for early return guard patterns: if (idx >= length) return;
        var hasEarlyReturnGuard = methodSyntax.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Any(ifStmt =>
            {
                // Check if the statement is a return (either direct or inside a block)
                var hasReturn = ifStmt.Statement is ReturnStatementSyntax ||
                               (ifStmt.Statement is BlockSyntax block &&
                                block.Statements.Any(s => s is ReturnStatementSyntax));

                if (!hasReturn)
                {
                    return false;
                }

                // Check the condition
                return ifStmt.Condition is BinaryExpressionSyntax condition &&
                      (condition.IsKind(SyntaxKind.GreaterThanOrEqualExpression) ||
                       condition.IsKind(SyntaxKind.GreaterThanExpression)) &&
                      (condition.ToString().Contains("Length") || condition.ToString().Contains(".Count"));
            });

        if (elementAccesses.Any() && !hasBoundsCheck && !hasEarlyReturnGuard)
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelMissingBoundsCheck,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ValidateBackendSelection(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        var kernelAttribute = KernelAnalysisHelpers.GetKernelAttribute(methodSymbol);
        if (kernelAttribute == null)
        {
            return;
        }


        var complexity = KernelAnalysisHelpers.AnalyzeComputationalComplexity(methodSyntax);
        var suggestedBackends = KernelAnalysisHelpers.GetOptimalBackends(complexity);
        var currentBackends = KernelAnalysisHelpers.ExtractBackendsFromAttribute(kernelAttribute);

        if (currentBackends != null && !currentBackends.Intersect(suggestedBackends).Any())
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelSuboptimalBackendSelection,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                string.Join(", ", currentBackends),
                string.Join(", ", suggestedBackends)
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CheckUnnecessaryComplexity(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // Check for complex control flow patterns (nested loops, switch statements, etc.)
        var hasNestedLoops = KernelAnalysisHelpers.HasNestedLoops(methodSyntax);
        var hasSwitchStatement = methodSyntax.DescendantNodes().OfType<SwitchStatementSyntax>().Any();
        var hasComplexConditionals = KernelAnalysisHelpers.HasComplexConditionals(methodSyntax);

        if (hasNestedLoops || hasSwitchStatement || hasComplexConditionals)
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelUnnecessaryComplexity,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                "Complex control flow detected - consider simplification"
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CheckThreadSafety(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // First check for performance anti-patterns (DC009)
        var forLoops = methodSyntax.DescendantNodes().OfType<ForStatementSyntax>();
        foreach (var forLoop in forLoops)
        {
            // Check for loops with variable iteration count based on thread index
            if (forLoop.Condition != null)
            {
                var conditionText = forLoop.Condition.ToString();
                if (conditionText.Contains("index") || conditionText.Contains("tid") ||
                    conditionText.Contains("Kernel.ThreadId") || conditionText.Contains("ThreadId"))
                {
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DC009",
                            "Kernel contains performance anti-pattern",
                            "Kernel method '{0}' contains a performance anti-pattern with non-uniform work distribution. This can cause thread divergence and reduced GPU utilization.",
                            "DotCompute.Performance",
                            DiagnosticSeverity.Info,
                            isEnabledByDefault: true
                        ),
                        forLoop.GetLocation(),
                        methodSymbol.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                    break; // Only report once
                }
            }
        }

        // Then check for thread safety issues
        var raceConditionAssignments = KernelAnalysisHelpers.CountPotentialRaceConditionAssignments(methodSyntax);
        if (raceConditionAssignments > 1)
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelThreadSafetyWarning,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                "Multiple assignments to non-indexed variables may cause race conditions"
            );
            context.ReportDiagnostic(diagnostic);
        }

        // Check for non-thread-safe method calls
        if (KernelAnalysisHelpers.HasNonThreadSafeMethodCalls(methodSyntax))
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelThreadSafetyWarning,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                "External method calls may not be thread-safe in parallel execution"
            );
            context.ReportDiagnostic(diagnostic);
        }
    }
}
