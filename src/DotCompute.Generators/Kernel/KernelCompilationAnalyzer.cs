// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotCompute.Generators.Kernel
{
    /// <summary>
    /// Analyzer for kernel methods to provide compile-time diagnostics.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class KernelCompilationAnalyzer : DiagnosticAnalyzer
    {
        // Diagnostic IDs
        public const string UnsupportedTypeId = "DC0001";
        public const string MissingBufferParameterId = "DC0002";
        public const string InvalidVectorSizeId = "DC0003";
        public const string UnsafeCodeRequiredId = "DC0004";
        public const string PerformanceWarningId = "DC0005";

        // Diagnostic descriptors
        private static readonly DiagnosticDescriptor UnsupportedTypeRule = new(
            UnsupportedTypeId,
            "Unsupported type in kernel",
            "Type '{0}' is not supported in kernel methods",
            "DotCompute.Kernel",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Kernel methods must use supported primitive types or buffer types.");

        private static readonly DiagnosticDescriptor MissingBufferParameterRule = new(
            MissingBufferParameterId,
            "Kernel method missing buffer parameter",
            "Kernel method '{0}' must have at least one buffer parameter",
            "DotCompute.Kernel",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Kernel methods must operate on buffer data.");

        private static readonly DiagnosticDescriptor InvalidVectorSizeRule = new(
            InvalidVectorSizeId,
            "Invalid vector size",
            "Vector size {0} is not supported. Must be 4, 8, or 16.",
            "DotCompute.Kernel",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Vector size must correspond to valid SIMD register sizes.");

        private static readonly DiagnosticDescriptor UnsafeCodeRequiredRule = new(
            UnsafeCodeRequiredId,
            "Unsafe code context required",
            "Kernel method '{0}' requires unsafe context for optimal performance",
            "DotCompute.Kernel",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Kernel methods should be in unsafe context for best performance.");

        private static readonly DiagnosticDescriptor PerformanceWarningRule = new(
            PerformanceWarningId,
            "Potential performance issue",
            "{0}",
            "DotCompute.Performance",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Potential performance issue detected in kernel method.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
                UnsupportedTypeRule,
                MissingBufferParameterRule,
                InvalidVectorSizeRule,
                UnsafeCodeRequiredRule,
                PerformanceWarningRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Register syntax node action for methods with Kernel attribute
            context.RegisterSyntaxNodeAction(AnalyzeKernelMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeKernelMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol == null)
            {
                return;
            }

            // Check if method has Kernel attribute
            var kernelAttribute = methodSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "KernelAttribute");

            if (kernelAttribute == null)
            {
                return;
            }

            // Perform various checks
            CheckParameterTypes(context, methodSymbol);
            CheckBufferParameters(context, methodSymbol);
            CheckVectorSize(context, kernelAttribute, methodDeclaration);
            CheckUnsafeContext(context, methodSymbol, methodDeclaration);
            CheckPerformanceIssues(context, methodDeclaration);
        }

        private static void CheckParameterTypes(SyntaxNodeAnalysisContext context, IMethodSymbol method)
        {
            foreach (var parameter in method.Parameters)
            {
                if (!IsSupportedType(parameter.Type))
                {
                    var diagnostic = Diagnostic.Create(
                        UnsupportedTypeRule,
                        parameter.Locations[0],
                        parameter.Type.ToDisplayString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static void CheckBufferParameters(SyntaxNodeAnalysisContext context, IMethodSymbol method)
        {
            bool hasBufferParameter = method.Parameters.Any(p => IsBufferType(p.Type));

            if (!hasBufferParameter)
            {
                var diagnostic = Diagnostic.Create(
                    MissingBufferParameterRule,
                    method.Locations[0],
                    method.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void CheckVectorSize(
            SyntaxNodeAnalysisContext context,
            AttributeData kernelAttribute,
            MethodDeclarationSyntax methodDeclaration)
        {
            var vectorSizeArg = kernelAttribute.NamedArguments
                .FirstOrDefault(a => a.Key == "VectorSize");

            if (vectorSizeArg.Value.Value is int vectorSize)
            {
                if (vectorSize != 4 && vectorSize != 8 && vectorSize != 16)
                {
                    var diagnostic = Diagnostic.Create(
                        InvalidVectorSizeRule,
                        methodDeclaration.GetLocation(),
                        vectorSize);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static void CheckUnsafeContext(
            SyntaxNodeAnalysisContext context,
            IMethodSymbol method,
            MethodDeclarationSyntax methodDeclaration)
        {
            // Check if method or containing type has unsafe modifier
            bool isUnsafe = methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.UnsafeKeyword));

            if (!isUnsafe && method.ContainingType != null)
            {
                var typeDeclaration = method.ContainingType.DeclaringSyntaxReferences
                    .FirstOrDefault()?.GetSyntax();

                if (typeDeclaration is TypeDeclarationSyntax typeDecl)
                {
                    isUnsafe = typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.UnsafeKeyword));
                }
            }

            if (!isUnsafe)
            {
                var diagnostic = Diagnostic.Create(
                    UnsafeCodeRequiredRule,
                    methodDeclaration.Identifier.GetLocation(),
                    method.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void CheckPerformanceIssues(
            SyntaxNodeAnalysisContext context,
            MethodDeclarationSyntax methodDeclaration)
        {
            if (methodDeclaration.Body == null)
            {
                return;
            }

            // Check for nested loops
            var loops = methodDeclaration.Body.DescendantNodes()
                .OfType<ForStatementSyntax>()
                .ToList();

            foreach (var outerLoop in loops)
            {
                var nestedLoops = outerLoop.DescendantNodes()
                    .OfType<ForStatementSyntax>()
                    .ToList();

                if (nestedLoops.Count > 0)
                {
                    var diagnostic = Diagnostic.Create(
                        PerformanceWarningRule,
                        outerLoop.GetLocation(),
                        "Nested loops detected. Consider loop flattening or tiling for better performance.");
                    context.ReportDiagnostic(diagnostic);
                }
            }

            // Check for allocations inside loops
            foreach (var loop in loops)
            {
                var allocations = loop.DescendantNodes()
                    .OfType<ObjectCreationExpressionSyntax>()
                    .ToList();

                if (allocations.Count > 0)
                {
                    var diagnostic = Diagnostic.Create(
                        PerformanceWarningRule,
                        allocations[0].GetLocation(),
                        "Object allocation inside loop detected. Move allocations outside the loop.");
                    context.ReportDiagnostic(diagnostic);
                }
            }

            // Check for unnecessary boxing
            var boxingOperations = methodDeclaration.Body.DescendantNodes()
                .OfType<CastExpressionSyntax>()
                .Where(c => MightCauseBoxing(c, context.SemanticModel))
                .ToList();

            foreach (var boxing in boxingOperations)
            {
                var diagnostic = Diagnostic.Create(
                    PerformanceWarningRule,
                    boxing.GetLocation(),
                    "Potential boxing operation detected. Use generic methods or avoid value type to object conversions.");
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsSupportedType(ITypeSymbol type)
        {
            // Primitive types
            if (type.SpecialType != SpecialType.None &&
                type.SpecialType != SpecialType.System_Object &&
                type.SpecialType != SpecialType.System_String)
            {
                return true;
            }

            // Buffer types
            if (IsBufferType(type))
            {
                return true;
            }

            // Pointer types
            if (type.TypeKind == TypeKind.Pointer)
            {
                return IsSupportedType(((IPointerTypeSymbol)type).PointedAtType);
            }

            return false;
        }

        private static bool IsBufferType(ITypeSymbol type)
        {
            var typeName = type.Name;
            return typeName.Contains("Buffer") ||
                   typeName.Contains("Span") ||
                   typeName.Contains("Memory") ||
                   type.AllInterfaces.Any(i => i.Name == "IBuffer");
        }

        private static bool MightCauseBoxing(CastExpressionSyntax cast, SemanticModel semanticModel)
        {
            var typeInfo = semanticModel.GetTypeInfo(cast.Expression);
            var targetType = semanticModel.GetTypeInfo(cast.Type);

            if (typeInfo.Type?.IsValueType == true && targetType.Type?.SpecialType == SpecialType.System_Object)
            {
                return true;
            }

            return false;
        }
    }
}
