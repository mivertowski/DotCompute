// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

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
        foreach (var parameter in methodSymbol.Parameters)
        {
            var parameterType = parameter.Type;

            if (!KernelAnalysisHelpers.IsValidKernelParameterType(parameterType))
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
        var unsupportedNodes = methodSyntax.DescendantNodes().Where(node => node switch
        {
            TryStatementSyntax => true,
            ThrowStatementSyntax => true,
            UnsafeStatementSyntax => true,
            _ => false
        });

        foreach (var node in unsupportedNodes)
        {
            var constructName = node switch
            {
                TryStatementSyntax => "try-catch blocks",
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
        var hasBoundsCheck = methodSyntax.DescendantNodes()
            .OfType<BinaryExpressionSyntax>()
            .Any(b => b.IsKind(SyntaxKind.LessThanExpression) && b.ToString().Contains("Length"));

        if (elementAccesses.Any() && !hasBoundsCheck)
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
        // Check for nested loops
        if (KernelAnalysisHelpers.HasNestedLoops(methodSyntax))
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelUnnecessaryComplexity,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                "Nested loops detected - consider vectorization or simplification"
            );
            context.ReportDiagnostic(diagnostic);
        }

        // Check for complex conditional logic
        if (KernelAnalysisHelpers.HasComplexConditionals(methodSyntax))
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelUnnecessaryComplexity,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                "Complex conditional logic detected - consider simplification"
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CheckThreadSafety(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // Check for potential race conditions
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