// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace DotCompute.Generators.Analyzers;

/// <summary>
/// Comprehensive Roslyn analyzer for DotCompute kernel code.
/// Provides real-time validation, optimization suggestions, and error detection in the IDE.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DotComputeKernelAnalyzer : DiagnosticAnalyzer
{
    // Kernel Method Validation (DC001-DC003)
    public static readonly DiagnosticDescriptor KernelMethodMustBeStatic = new(
        "DC001",
        "Kernel methods must be static",
        "Kernel method '{0}' must be declared as static",
        "DotCompute.Kernel",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Kernel methods must be static to be compatible with compute backends."
    );

    public static readonly DiagnosticDescriptor KernelMethodInvalidParameters = new(
        "DC002",

        "Kernel method has invalid parameters",
        "Kernel method '{0}' has invalid parameter '{1}': {2}",
        "DotCompute.Kernel",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Kernel methods can only use supported parameter types like Span<T>, ReadOnlySpan<T>, and primitive types."
    );

    public static readonly DiagnosticDescriptor KernelMethodUnsupportedConstruct = new(
        "DC003",
        "Kernel method uses unsupported language construct",
        "Kernel method '{0}' uses unsupported construct '{1}' at line {2}",
        "DotCompute.Kernel",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Kernel methods cannot use certain C# constructs like exception handling, dynamic types, or unsafe code."
    );

    // Performance Optimization Suggestions (DC004-DC006)
    public static readonly DiagnosticDescriptor KernelCanBeVectorized = new(
        "DC004",
        "Kernel can benefit from vectorization",
        "Kernel method '{0}' contains loops that can be vectorized using SIMD instructions",
        "DotCompute.Performance",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "This kernel contains patterns that can be optimized using SIMD vectorization."
    );

    public static readonly DiagnosticDescriptor KernelSuboptimalMemoryAccess = new(
        "DC005",
        "Kernel has suboptimal memory access pattern",
        "Kernel method '{0}' has memory access pattern that may cause cache misses: {1}",
        "DotCompute.Performance",

        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Memory access patterns should be optimized for cache locality and coalescing."
    );

    public static readonly DiagnosticDescriptor KernelRegisterSpilling = new(
        "DC006",
        "Kernel may experience register spilling",
        "Kernel method '{0}' uses {1} local variables which may cause register spilling on GPU",
        "DotCompute.Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Too many local variables can cause register spilling, reducing GPU performance."
    );

    // Code Quality Issues (DC007-DC009)
    public static readonly DiagnosticDescriptor KernelMissingKernelAttribute = new(
        "DC007",
        "Method should have [Kernel] attribute",
        "Method '{0}' appears to be a kernel but is missing the [Kernel] attribute",
        "DotCompute.Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Methods that perform compute operations should be marked with [Kernel] attribute for code generation."
    );

    public static readonly DiagnosticDescriptor KernelUnnecessaryComplexity = new(
        "DC008",
        "Kernel can be simplified",
        "Kernel method '{0}' has unnecessary complexity that can be simplified: {1}",
        "DotCompute.Maintainability",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Simpler kernels are easier to optimize and maintain."
    );

    public static readonly DiagnosticDescriptor KernelThreadSafetyWarning = new(
        "DC009",
        "Potential thread safety issue in kernel",
        "Kernel method '{0}' may have thread safety issues: {1}",
        "DotCompute.Reliability",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Kernels must be thread-safe when executed in parallel."
    );

    // Usage Pattern Issues (DC010-DC012)
    public static readonly DiagnosticDescriptor KernelIncorrectIndexing = new(
        "DC010",
        "Kernel uses incorrect threading model",
        "Kernel method '{0}' should use Kernel.ThreadId for indexing instead of loop variables",
        "DotCompute.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Kernels should use the threading model provided by Kernel.ThreadId for optimal performance."
    );

    public static readonly DiagnosticDescriptor KernelMissingBoundsCheck = new(
        "DC011",
        "Kernel missing bounds check",
        "Kernel method '{0}' should check array bounds before accessing elements",
        "DotCompute.Reliability",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Kernels should validate array bounds to prevent runtime errors."
    );

    public static readonly DiagnosticDescriptor KernelSuboptimalBackendSelection = new(
        "DC012",
        "Kernel backend selection can be optimized",
        "Kernel attribute on '{0}' specifies backends '{1}' but only '{2}' are likely to be beneficial",
        "DotCompute.Performance",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Backend selection should match the kernel's computational characteristics."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        KernelMethodMustBeStatic,
        KernelMethodInvalidParameters,

        KernelMethodUnsupportedConstruct,
        KernelCanBeVectorized,
        KernelSuboptimalMemoryAccess,
        KernelRegisterSpilling,
        KernelMissingKernelAttribute,
        KernelUnnecessaryComplexity,
        KernelThreadSafetyWarning,
        KernelIncorrectIndexing,
        KernelMissingBoundsCheck,
        KernelSuboptimalBackendSelection
    );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();


        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeForStatement, SyntaxKind.ForStatement);
        context.RegisterSyntaxNodeAction(AnalyzeElementAccess, SyntaxKind.ElementAccessExpression);
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodSyntax)
        {
            return;
        }


        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodSyntax) as IMethodSymbol;
        if (methodSymbol == null)
        {
            return;
        }


        var hasKernelAttribute = HasKernelAttribute(methodSymbol);


        if (hasKernelAttribute)
        {
            AnalyzeKernelMethod(context, methodSyntax, methodSymbol);
        }
        else if (LooksLikeKernelMethod(methodSyntax, methodSymbol))
        {
            // Suggest adding [Kernel] attribute
            var diagnostic = Diagnostic.Create(
                KernelMissingKernelAttribute,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeKernelMethod(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // DC001: Check if method is static
        if (!methodSymbol.IsStatic)
        {
            var diagnostic = Diagnostic.Create(
                KernelMethodMustBeStatic,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }

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
    }

    private static void ValidateKernelParameters(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        foreach (var parameter in methodSymbol.Parameters)
        {
            var parameterType = parameter.Type;


            if (!IsValidKernelParameterType(parameterType))
            {
                var parameterNode = methodSyntax.ParameterList.Parameters
                    .FirstOrDefault(p => p.Identifier.ValueText == parameter.Name);


                if (parameterNode != null)
                {
                    var diagnostic = Diagnostic.Create(
                        KernelMethodInvalidParameters,
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
            DynamicExpressionSyntax => true,
            LinqExpressionSyntax => true,
            _ => false
        });

        foreach (var node in unsupportedNodes)
        {
            var constructName = node switch
            {
                TryStatementSyntax => "try-catch blocks",
                ThrowStatementSyntax => "throw statements",
                UnsafeStatementSyntax => "unsafe code blocks",
                DynamicExpressionSyntax => "dynamic types",
                _ => node.GetType().Name
            };

            var diagnostic = Diagnostic.Create(
                KernelMethodUnsupportedConstruct,
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
            if (CanBeVectorized(forLoop))
            {
                var diagnostic = Diagnostic.Create(
                    KernelCanBeVectorized,
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
            if (HasSuboptimalAccessPattern(access))
            {
                var diagnostic = Diagnostic.Create(
                    KernelSuboptimalMemoryAccess,
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
                KernelRegisterSpilling,
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
        var hasKernelThreadAccess = methodSyntax.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(m => m.ToString().Contains("Kernel.ThreadId"));

        if (forLoops.Any() && !hasKernelThreadAccess)
        {
            var diagnostic = Diagnostic.Create(
                KernelIncorrectIndexing,
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
                KernelMissingBoundsCheck,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ValidateBackendSelection(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        var kernelAttribute = GetKernelAttribute(methodSymbol);
        if (kernelAttribute == null)
        {
            return;
        }

        // Analyze computational complexity to suggest optimal backends

        var complexity = AnalyzeComputationalComplexity(methodSyntax);
        var suggestedBackends = GetOptimalBackends(complexity);
        var currentBackends = ExtractBackendsFromAttribute(kernelAttribute);

        if (currentBackends != null && !currentBackends.Intersect(suggestedBackends).Any())
        {
            var diagnostic = Diagnostic.Create(
                KernelSuboptimalBackendSelection,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                string.Join(", ", currentBackends),
                string.Join(", ", suggestedBackends)
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeForStatement(SyntaxNodeAnalysisContext context)
    {
        // Additional analysis for for-loops can be added here TODO
    }

    private static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
    {
        // Additional analysis for array access can be added here TODO
    }

    // Helper methods
    private static bool HasKernelAttribute(ISymbol symbol) =>
        symbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "KernelAttribute");

    private static AttributeData? GetKernelAttribute(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.Name == "KernelAttribute");

    private static bool LooksLikeKernelMethod(MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol) =>
        methodSymbol.IsStatic &&
        methodSymbol.Parameters.Any(p => p.Type.ToString().Contains("Span")) &&
        methodSyntax.Body?.Statements.OfType<ForStatementSyntax>().Any() == true;

    private static bool IsValidKernelParameterType(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString();
        return typeName.Contains("Span<") ||
               typeName.Contains("ReadOnlySpan<") ||

               type.SpecialType is SpecialType.System_Int32 or SpecialType.System_Single or SpecialType.System_Double;
    }

    private static bool CanBeVectorized(ForStatementSyntax forLoop)
    {
        // Simple heuristic - loops with array access and arithmetic operations
        return forLoop.DescendantNodes().OfType<ElementAccessExpressionSyntax>().Any() &&
               forLoop.DescendantNodes().OfType<BinaryExpressionSyntax>().Any(b =>

                   b.IsKind(SyntaxKind.AddExpression) || b.IsKind(SyntaxKind.MultiplyExpression));
    }

    private static bool HasSuboptimalAccessPattern(ElementAccessExpressionSyntax access)
    {
        // Heuristic for non-sequential access patterns
        return access.ArgumentList.Arguments.Count == 1 &&
               access.ArgumentList.Arguments[0].Expression.ToString().Contains("*") ||
               access.ArgumentList.Arguments[0].Expression.ToString().Contains("%");
    }

    private static string AnalyzeComputationalComplexity(MethodDeclarationSyntax methodSyntax)
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

    private static string[] GetOptimalBackends(string complexity) => complexity switch
    {
        "HighComplexity" => ["CUDA", "Metal", "OpenCL"],
        "MediumComplexity" => ["CUDA", "CPU"],
        "LowComplexity" => ["CPU"],
        _ => ["CPU"]
    };

    private static string[]? ExtractBackendsFromAttribute(AttributeData attribute)
    {
        // Extract backend flags from KernelAttribute - simplified implementation
        return ["CPU", "CUDA"]; // Placeholder TODO
    }
}