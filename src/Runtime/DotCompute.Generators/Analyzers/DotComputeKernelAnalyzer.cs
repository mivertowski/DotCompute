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
        "Kernel method '{0}' must be declared as static for GPU execution. Add 'static' modifier to the method declaration.",
        "DotCompute.Kernel",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Kernel methods must be static to be compatible with compute backends. Instance methods cannot be executed on GPU devices as they require object context that doesn't exist in kernel execution. Example: public static void MyKernel(Span<float> data) {{ }}"
    );

    public static readonly DiagnosticDescriptor KernelMethodInvalidParameters = new(
        "DC002",
        "Kernel method has invalid parameters",
        "Kernel method '{0}' has invalid parameter '{1}': {2}. Use Span<T> or ReadOnlySpan<T> instead of arrays for better performance.",
        "DotCompute.Kernel",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Kernel methods can only use supported parameter types like Span<T>, ReadOnlySpan<T>, and primitive types (int, float, double). Arrays should be converted to Span<T> for zero-copy memory access and better performance. Example: Use 'Span<float>' instead of 'float[]'."
    );

    public static readonly DiagnosticDescriptor KernelMethodUnsupportedConstruct = new(
        "DC003",
        "Kernel method uses unsupported language construct",
        "Kernel method '{0}' uses unsupported construct '{1}' at line {2}. Remove this construct as it cannot be translated to GPU code.",
        "DotCompute.Kernel",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Kernel methods cannot use certain C# constructs like exception handling (try-catch), dynamic types, unsafe code, or LINQ expressions. These features are not available on GPU backends and will cause compilation failures. Use simple control flow and arithmetic operations instead."
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
        "Method '{0}' appears to be a compute kernel but is missing the [Kernel] attribute. Add '[Kernel]' above the method declaration to enable GPU execution.",
        "DotCompute.Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Methods that perform compute operations on Span<T> parameters should be marked with [Kernel] attribute for automatic code generation and GPU compilation. The attribute enables the source generator to create optimized CPU and GPU versions of your code."
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
        "Kernel method '{0}' should use Kernel.ThreadId.X for indexing instead of loop variables for optimal GPU performance. Replace 'for' loops with 'int index = Kernel.ThreadId.X;'.",
        "DotCompute.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Kernels should use the threading model provided by Kernel.ThreadId for optimal performance. GPU threads execute in parallel, so traditional for-loops should be replaced with thread-based indexing. Example: 'int index = Kernel.ThreadId.X; if (index < data.Length) data[index] = ...'."
    );

    public static readonly DiagnosticDescriptor KernelMissingBoundsCheck = new(
        "DC011",
        "Kernel missing bounds check",
        "Kernel method '{0}' should check array bounds before accessing elements. Add 'if (index < data.Length)' to prevent out-of-bounds access.",
        "DotCompute.Reliability",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Kernels should validate array bounds to prevent runtime errors and undefined behavior on GPU. GPU memory access violations can crash the entire system. Example: 'if (index < data.Length) {{ data[index] = value; }}'."
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

        // DC008: Check for unnecessary complexity
        CheckUnnecessaryComplexity(context, methodSyntax, methodSymbol);

        // DC009: Check for thread safety issues
        CheckThreadSafety(context, methodSyntax, methodSymbol);
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
            // Dynamic and LINQ expressions would require more specific checks
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
        if (context.Node is not ForStatementSyntax forLoop)
        {
            return;
        }

        var parentMethod = forLoop.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (parentMethod == null)
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(parentMethod) as IMethodSymbol;
        if (methodSymbol == null || !HasKernelAttribute(methodSymbol))
        {
            return;
        }

        // Check for vectorization opportunities in for loops
        if (CanBeVectorized(forLoop))
        {
            var diagnostic = Diagnostic.Create(
                KernelCanBeVectorized,
                forLoop.ForKeyword.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }

        // Check for incorrect loop variable usage
        var loopVariable = ExtractLoopVariable(forLoop);
        if (loopVariable != null && !UsesKernelThreading(parentMethod))
        {
            var diagnostic = Diagnostic.Create(
                KernelIncorrectIndexing,
                forLoop.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
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

        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(parentMethod) as IMethodSymbol;
        if (methodSymbol == null || !HasKernelAttribute(methodSymbol))
        {
            return;
        }

        // Check for bounds validation
        if (!HasBoundsCheckForAccess(parentMethod, elementAccess))
        {
            var diagnostic = Diagnostic.Create(
                KernelMissingBoundsCheck,
                elementAccess.GetLocation(),
                methodSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }

        // Check for suboptimal memory access patterns
        if (HasSuboptimalAccessPattern(elementAccess))
        {
            var diagnostic = Diagnostic.Create(
                KernelSuboptimalMemoryAccess,
                elementAccess.GetLocation(),
                methodSymbol.Name,
                "Non-sequential array access pattern detected"
            );
            context.ReportDiagnostic(diagnostic);
        }
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
        if (attribute.AttributeClass?.Name != "KernelAttribute")
        {
            return null;
        }

        var backends = new List<string>();
        
        // Look for named arguments with 'Backends' parameter
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "Backends" && namedArg.Value.Value is int backendFlags)
            {
                // Parse backend flags (assuming flags enum)
                if ((backendFlags & 1) != 0) backends.Add("CPU");
                if ((backendFlags & 2) != 0) backends.Add("CUDA");
                if ((backendFlags & 4) != 0) backends.Add("Metal");
                if ((backendFlags & 8) != 0) backends.Add("OpenCL");
                return backends.ToArray();
            }
        }

        // Look for constructor arguments
        if (attribute.ConstructorArguments.Length > 0)
        {
            var firstArg = attribute.ConstructorArguments[0];
            if (firstArg.Value is int flags)
            {
                if ((flags & 1) != 0) backends.Add("CPU");
                if ((flags & 2) != 0) backends.Add("CUDA");
                if ((flags & 4) != 0) backends.Add("Metal");
                if ((flags & 8) != 0) backends.Add("OpenCL");
                return backends.ToArray();
            }
        }

        // Default to CPU and CUDA if no specific backends specified
        return ["CPU", "CUDA"];
    }

    // Additional helper methods for enhanced analysis
    private static string? ExtractLoopVariable(ForStatementSyntax forLoop)
    {
        if (forLoop.Declaration?.Variables.FirstOrDefault() is VariableDeclaratorSyntax variable)
        {
            return variable.Identifier.ValueText;
        }
        return null;
    }

    private static bool UsesKernelThreading(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(m => m.ToString().Contains("Kernel.ThreadId") || m.ToString().Contains("ThreadId."));
    }

    private static bool HasBoundsCheckForAccess(MethodDeclarationSyntax method, ElementAccessExpressionSyntax elementAccess)
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

    private static void CheckUnnecessaryComplexity(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // Check for nested loops (potential complexity issue)
        var nestedLoops = methodSyntax.DescendantNodes()
            .OfType<ForStatementSyntax>()
            .Where(loop => loop.Ancestors().OfType<ForStatementSyntax>().Any());

        if (nestedLoops.Any())
        {
            var diagnostic = Diagnostic.Create(
                KernelUnnecessaryComplexity,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                "Nested loops detected - consider vectorization or simplification"
            );
            context.ReportDiagnostic(diagnostic);
        }

        // Check for complex conditional logic
        var complexConditionals = methodSyntax.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Where(ifStmt => ifStmt.DescendantNodes().OfType<BinaryExpressionSyntax>()
                .Count(b => b.IsKind(SyntaxKind.LogicalAndExpression) || b.IsKind(SyntaxKind.LogicalOrExpression)) > 2);

        if (complexConditionals.Any())
        {
            var diagnostic = Diagnostic.Create(
                KernelUnnecessaryComplexity,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                "Complex conditional logic detected - consider simplification"
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CheckThreadSafety(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodSyntax, IMethodSymbol methodSymbol)
    {
        // Check for potential race conditions - shared variable access without synchronization
        var memberAccesses = methodSyntax.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(m => !m.ToString().Contains("ThreadId") && !m.ToString().Contains("Length"));

        var assignments = methodSyntax.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(a => !a.Left.ToString().Contains("[") && 
                       !a.Left.ToString().Contains("ThreadId"));

        if (assignments.Count() > 1)
        {
            var diagnostic = Diagnostic.Create(
                KernelThreadSafetyWarning,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                "Multiple assignments to non-indexed variables may cause race conditions"
            );
            context.ReportDiagnostic(diagnostic);
        }

        // Check for method calls that might not be thread-safe
        var methodCalls = methodSyntax.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => !inv.ToString().Contains("Math.") && 
                         !inv.ToString().Contains("MathF.") &&
                         !inv.ToString().StartsWith("System."));

        if (methodCalls.Any())
        {
            var diagnostic = Diagnostic.Create(
                KernelThreadSafetyWarning,
                methodSyntax.Identifier.GetLocation(),
                methodSymbol.Name,
                "External method calls may not be thread-safe in parallel execution"
            );
            context.ReportDiagnostic(diagnostic);
        }
    }
}