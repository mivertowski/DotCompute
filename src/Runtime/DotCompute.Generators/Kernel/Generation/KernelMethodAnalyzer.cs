// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotCompute.Generators.Models.Kernel;

namespace DotCompute.Generators.Kernel.Generation;

/// <summary>
/// Analyzes kernel methods to extract metadata and configuration for code generation.
/// Provides semantic analysis of kernel methods including attribute processing,
/// parameter analysis, and backend configuration extraction.
/// </summary>
/// <remarks>
/// This class performs the second phase of kernel analysis by using semantic models
/// to extract detailed information about kernel methods. It processes kernel attributes,
/// analyzes method signatures, and extracts configuration data needed for backend
/// code generation.
/// </remarks>
public sealed class KernelMethodAnalyzer
{
    private readonly KernelParameterAnalyzer _parameterAnalyzer;
    private readonly KernelAttributeAnalyzer _attributeAnalyzer;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelMethodAnalyzer"/> class.
    /// </summary>
    public KernelMethodAnalyzer()
    {
        _parameterAnalyzer = new KernelParameterAnalyzer();
        _attributeAnalyzer = new KernelAttributeAnalyzer();
    }

    /// <summary>
    /// Analyzes a kernel method and extracts comprehensive metadata.
    /// </summary>
    /// <param name="context">The generator syntax context containing the method.</param>
    /// <returns>A <see cref="KernelMethodInfo"/> object containing method metadata, or null if analysis fails.</returns>
    /// <remarks>
    /// This method performs comprehensive analysis of a kernel method including:
    /// - Semantic symbol extraction
    /// - Attribute processing and configuration extraction
    /// - Parameter analysis and type information
    /// - Backend compatibility determination
    /// - Performance characteristics analysis
    /// </remarks>
#pragma warning disable CA1822 // Mark members as static - instance method for design consistency
    public KernelMethodInfo? AnalyzeKernelMethod(GeneratorSyntaxContext context)
#pragma warning restore CA1822
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        var model = context.SemanticModel;

        if (model.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        // Extract kernel attribute data
        var kernelAttribute = GetKernelAttribute(methodSymbol);
        if (kernelAttribute is null)
        {
            return null;
        }

        // Analyze kernel configuration from attributes
        var configuration = KernelAttributeAnalyzer.AnalyzeKernelConfiguration(kernelAttribute);

        // Analyze method parameters
        var parameters = KernelParameterAnalyzer.AnalyzeParameters(methodSymbol);

        // Validate method compatibility
        if (!ValidateMethodCompatibility(methodSymbol, parameters))
        {
            return null;
        }

        var methodInfo = new KernelMethodInfo
        {
            Name = methodSymbol.Name,
            ContainingType = methodSymbol.ContainingType.ToDisplayString(),
            Namespace = methodSymbol.ContainingNamespace.ToDisplayString(),
            ReturnType = methodSymbol.ReturnType.ToDisplayString(),
            VectorSize = configuration.VectorSize,
            IsParallel = configuration.IsParallel,
            MethodDeclaration = methodDeclaration
        };

        // Populate read-only collections
        foreach (var param in parameters)
        {
            methodInfo.Parameters.Add(param);
        }

        foreach (var backend in configuration.SupportedBackends)
        {
            methodInfo.Backends.Add(backend);
        }

        return methodInfo;
    }

    /// <summary>
    /// Analyzes a kernel class and extracts information about contained kernel methods.
    /// </summary>
    /// <param name="context">The generator syntax context containing the class.</param>
    /// <returns>A <see cref="KernelClassInfo"/> object containing class metadata, or null if analysis fails.</returns>
    public static KernelClassInfo? AnalyzeKernelClass(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var model = context.SemanticModel;

        if (model.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        var kernelMethods = GetKernelMethodsFromClass(classSymbol);
        if (kernelMethods.Count == 0)
        {
            return null;
        }

        var classInfo = new KernelClassInfo
        {
            Name = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace.ToDisplayString()
        };

        // Populate read-only collection
        foreach (var method in kernelMethods)
        {
            classInfo.KernelMethodNames.Add(method.Name);
        }

        return classInfo;
    }

    /// <summary>
    /// Extracts the kernel attribute from a method symbol.
    /// </summary>
    /// <param name="methodSymbol">The method symbol to examine.</param>
    /// <returns>The kernel attribute data, or null if not found.</returns>
    private static AttributeData? GetKernelAttribute(IMethodSymbol methodSymbol)
    {
        return methodSymbol.GetAttributes()
            .FirstOrDefault(a => IsKernelAttribute(a.AttributeClass));
    }

    /// <summary>
    /// Determines if an attribute class represents a kernel attribute.
    /// </summary>
    /// <param name="attributeClass">The attribute class to check.</param>
    /// <returns>True if the attribute is a kernel attribute; otherwise, false.</returns>
    private static bool IsKernelAttribute(INamedTypeSymbol? attributeClass)
    {
        return attributeClass?.Name == "KernelAttribute" ||
               attributeClass?.Name == "Kernel";
    }

    /// <summary>
    /// Gets all kernel methods from a class symbol.
    /// </summary>
    /// <param name="classSymbol">The class symbol to examine.</param>
    /// <returns>A list of method symbols that have kernel attributes.</returns>
    private static List<IMethodSymbol> GetKernelMethodsFromClass(INamedTypeSymbol classSymbol)
    {
        return [.. classSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes().Any(a => IsKernelAttribute(a.AttributeClass)))];
    }

    /// <summary>
    /// Validates that a method is compatible with kernel generation.
    /// </summary>
    /// <param name="methodSymbol">The method symbol to validate.</param>
    /// <param name="parameters">The analyzed parameter information.</param>
    /// <returns>True if the method is compatible; otherwise, false.</returns>
    /// <remarks>
    /// This method performs semantic validation to ensure the method can be
    /// successfully generated for kernel execution. It checks:
    /// - Method is static (required for kernels)
    /// - Return type is void or compatible
    /// - Parameter types are supported
    /// - Method accessibility is appropriate
    /// </remarks>
    private static bool ValidateMethodCompatibility(IMethodSymbol methodSymbol, IReadOnlyList<ParameterInfo> parameters)
    {
        // Must be static
        if (!methodSymbol.IsStatic)
        {
            return false;
        }

        // Must return void or compatible type
        if (!IsCompatibleReturnType(methodSymbol.ReturnType))
        {
            return false;
        }

        // All parameters must be compatible
        if (parameters.Any(p => !IsCompatibleParameterType(p)))
        {
            return false;
        }

        // Must be publicly accessible
        if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines if a return type is compatible with kernel generation.
    /// </summary>
    /// <param name="returnType">The return type to check.</param>
    /// <returns>True if the return type is compatible; otherwise, false.</returns>
    private static bool IsCompatibleReturnType(ITypeSymbol returnType)
        // Currently only void is supported for kernels
        => returnType.SpecialType == SpecialType.System_Void;

    /// <summary>
    /// Determines if a parameter type is compatible with kernel generation.
    /// </summary>
    /// <param name="parameter">The parameter information to check.</param>
    /// <returns>True if the parameter type is compatible; otherwise, false.</returns>
    private static bool IsCompatibleParameterType(ParameterInfo parameter)
    {
        // Check for supported buffer types
        if (parameter.IsBuffer)
        {
            return IsSupportedBufferType(parameter.Type);
        }

        // Check for supported scalar types
        return IsSupportedScalarType(parameter.Type);
    }

    /// <summary>
    /// Determines if a buffer type is supported for kernel generation.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if the buffer type is supported; otherwise, false.</returns>
    private static bool IsSupportedBufferType(string typeName)
    {
        return typeName.Contains("Span<") ||
               typeName.Contains("ReadOnlySpan<") ||
               typeName.Contains("UnifiedBuffer<") ||
               typeName.Contains("Buffer<") ||
               typeName.EndsWith("[]", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines if a scalar type is supported for kernel generation.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if the scalar type is supported; otherwise, false.</returns>
    private static bool IsSupportedScalarType(string typeName)
    {
        return typeName switch
        {
            "int" or "uint" or "long" or "ulong" => true,
            "float" or "double" => true,
            "byte" or "sbyte" or "short" or "ushort" => true,
            "bool" or "char" => true,
            _ => false
        };
    }

    /// <summary>
    /// Analyzes method body to determine optimization opportunities.
    /// </summary>
    /// <param name="methodDeclaration">The method declaration to analyze.</param>
    /// <returns>Information about optimization opportunities.</returns>
    public static MethodOptimizationInfo AnalyzeOptimizationOpportunities(MethodDeclarationSyntax methodDeclaration)
    {
        var methodBody = methodDeclaration.Body?.ToString() ??
                        methodDeclaration.ExpressionBody?.ToString() ?? string.Empty;

        return new MethodOptimizationInfo
        {
            HasArithmeticOperations = ContainsArithmeticOperations(methodBody),
            HasMemoryOperations = ContainsMemoryOperations(methodBody),
            HasLoops = ContainsLoops(methodDeclaration),
            IsVectorizable = IsVectorizable(methodBody),
            ComplexityScore = CalculateComplexityScore(methodDeclaration)
        };
    }

    /// <summary>
    /// Determines if method body contains arithmetic operations suitable for vectorization.
    /// </summary>
    private static bool ContainsArithmeticOperations(string methodBody)
    {
        return methodBody.Contains("+") || methodBody.Contains("-") ||
               methodBody.Contains("*") || methodBody.Contains("/") ||
               methodBody.Contains("Math.") || methodBody.Contains("MathF.");
    }

    /// <summary>
    /// Determines if method body contains memory operations.
    /// </summary>
    private static bool ContainsMemoryOperations(string methodBody)
    {
        return (methodBody.Contains("[") && methodBody.Contains("]")) ||
               methodBody.Contains("Span") || methodBody.Contains("Memory");
    }

    /// <summary>
    /// Determines if method contains loop constructs.
    /// </summary>
    private static bool ContainsLoops(MethodDeclarationSyntax methodDeclaration)
    {
        return methodDeclaration.DescendantNodes()
            .Any(n => n is ForStatementSyntax or WhileStatementSyntax or ForEachStatementSyntax);
    }

    /// <summary>
    /// Determines if method is suitable for vectorization.
    /// </summary>
    private static bool IsVectorizable(string methodBody) => ContainsArithmeticOperations(methodBody) && !methodBody.Contains("goto");

    /// <summary>
    /// Calculates a complexity score for the method.
    /// </summary>
    private static int CalculateComplexityScore(MethodDeclarationSyntax methodDeclaration)
    {
        var score = 1; // Base complexity

        // Count control flow statements
        score += methodDeclaration.DescendantNodes().Count(n =>
            n is IfStatementSyntax or SwitchStatementSyntax or
            ForStatementSyntax or WhileStatementSyntax);

        // Count method calls
        score += methodDeclaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Count();

        return score;
    }
}

/// <summary>
/// Contains optimization analysis results for a kernel method.
/// </summary>
public sealed class MethodOptimizationInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether the method contains arithmetic operations.
    /// </summary>
    public bool HasArithmeticOperations { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method contains memory operations.
    /// </summary>
    public bool HasMemoryOperations { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method contains loops.
    /// </summary>
    public bool HasLoops { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method is suitable for vectorization.
    /// </summary>
    public bool IsVectorizable { get; set; }

    /// <summary>
    /// Gets or sets the complexity score of the method.
    /// </summary>
    public int ComplexityScore { get; set; }
}