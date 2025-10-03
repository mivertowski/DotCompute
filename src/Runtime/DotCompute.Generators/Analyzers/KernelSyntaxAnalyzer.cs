// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotCompute.Generators.Analyzers;

/// <summary>
/// Analyzes specific syntax constructs within kernel methods.
/// </summary>
internal static class KernelSyntaxAnalyzer
{
    /// <summary>
    /// Analyzes for statements within kernel methods.
    /// </summary>
    public static void AnalyzeForStatement(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ForStatementSyntax forLoop)
        {
            return;
        }


        var parentMethod = forLoop.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (parentMethod == null)
        {
            return;
        }


        if (context.SemanticModel.GetDeclaredSymbol(parentMethod) is not IMethodSymbol methodSymbol || !KernelAnalysisHelpers.HasKernelAttribute(methodSymbol))
        {
            return;
        }

        // Check for vectorization opportunities in for loops

        if (KernelAnalysisHelpers.CanBeVectorized(forLoop))
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelCanBeVectorized,
                forLoop.ForKeyword.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }

        // Check for incorrect loop variable usage
        var loopVariable = KernelAnalysisHelpers.ExtractLoopVariable(forLoop);
        if (loopVariable != null && !KernelAnalysisHelpers.UsesKernelThreading(parentMethod))
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelIncorrectIndexing,
                forLoop.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Analyzes element access expressions within kernel methods.
    /// </summary>
    public static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ElementAccessExpressionSyntax elementAccess)
        {
            return;
        }


        var parentMethod = elementAccess.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (parentMethod == null)
        {
            return;
        }


        if (context.SemanticModel.GetDeclaredSymbol(parentMethod) is not IMethodSymbol methodSymbol || !KernelAnalysisHelpers.HasKernelAttribute(methodSymbol))
        {
            return;
        }

        // Check for bounds validation

        if (!KernelAnalysisHelpers.HasBoundsCheckForAccess(parentMethod, elementAccess))
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelMissingBoundsCheck,
                elementAccess.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }

        // Check for suboptimal memory access patterns
        if (KernelAnalysisHelpers.HasSuboptimalAccessPattern(elementAccess))
        {
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelSuboptimalMemoryAccess,
                elementAccess.GetLocation(),
                methodSymbol.Name,
                "Non-sequential array access pattern detected"
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Analyzes method declarations to identify potential kernel methods.
    /// </summary>
    public static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodSyntax)
        {
            return;
        }


        if (context.SemanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol methodSymbol)
        {
            return;
        }


        var hasKernelAttribute = KernelAnalysisHelpers.HasKernelAttribute(methodSymbol);

        if (hasKernelAttribute)
        {
            KernelMethodAnalyzer.AnalyzeKernelMethod(context, methodSyntax, methodSymbol);
        }
        else if (KernelAnalysisHelpers.LooksLikeKernelMethod(methodSyntax, methodSymbol))
        {
            // Suggest adding [Kernel] attribute
            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelMissingKernelAttribute,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }
    }
}