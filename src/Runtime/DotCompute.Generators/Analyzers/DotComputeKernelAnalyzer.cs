// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotCompute.Generators.Analyzers;

/// <summary>
/// Comprehensive Roslyn analyzer for DotCompute kernel code.
/// Provides real-time validation, optimization suggestions, and error detection in the IDE.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DotComputeKernelAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        KernelDiagnostics.KernelMethodMustBeStatic,
        KernelDiagnostics.KernelMethodInvalidParameters,
        KernelDiagnostics.KernelMethodUnsupportedConstruct,
        KernelDiagnostics.KernelCanBeVectorized,
        KernelDiagnostics.KernelSuboptimalMemoryAccess,
        KernelDiagnostics.KernelRegisterSpilling,
        KernelDiagnostics.KernelMissingKernelAttribute,
        KernelDiagnostics.KernelUnnecessaryComplexity,
        KernelDiagnostics.KernelThreadSafetyWarning,
        KernelDiagnostics.KernelIncorrectIndexing,
        KernelDiagnostics.KernelMissingBoundsCheck,
        KernelDiagnostics.KernelSuboptimalBackendSelection
    );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(KernelSyntaxAnalyzer.AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(KernelSyntaxAnalyzer.AnalyzeForStatement, SyntaxKind.ForStatement);
        context.RegisterSyntaxNodeAction(KernelSyntaxAnalyzer.AnalyzeElementAccess, SyntaxKind.ElementAccessExpression);
    }
}
