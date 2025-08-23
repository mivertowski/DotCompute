// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Utils;

/// <summary>
/// Helper methods for source generation.
/// This class now acts as a facade, delegating to specialized helper classes.
/// Methods here are maintained for backward compatibility but are marked as obsolete.
/// </summary>
public static class SourceGeneratorHelpers
{
    /// <summary>
    /// Creates a standard file header for generated code.
    /// </summary>
    [Obsolete("Use CodeFormatter.GenerateHeader instead. Will be removed in v2.0", false)]
    public static string GenerateHeader(params string[] usings)
        => CodeFormatter.GenerateHeader(usings);

    /// <summary>
    /// Generates a namespace declaration.
    /// </summary>
    [Obsolete("Use CodeFormatter.BeginNamespace instead. Will be removed in v2.0", false)]
    public static string BeginNamespace(string namespaceName) 
        => CodeFormatter.BeginNamespace(namespaceName);

    /// <summary>
    /// Closes a namespace declaration.
    /// </summary>
    [Obsolete("Use CodeFormatter.EndNamespace instead. Will be removed in v2.0", false)]
    public static string EndNamespace() 
        => CodeFormatter.EndNamespace();

    /// <summary>
    /// Indents code by the specified level.
    /// </summary>
    [Obsolete("Use CodeFormatter.Indent instead. Will be removed in v2.0", false)]
    public static string Indent(string code, int level)
        => CodeFormatter.Indent(code, level);

    /// <summary>
    /// Generates parameter validation code.
    /// </summary>
    [Obsolete("Use ParameterValidator.GenerateParameterValidation instead. Will be removed in v2.0", false)]
    public static string GenerateParameterValidation(IEnumerable<KernelParameter> parameters)
        => ParameterValidator.GenerateParameterValidation(parameters);

    /// <summary>
    /// Generates optimized loop code based on hints.
    /// </summary>
    [Obsolete("Use LoopOptimizer.GenerateOptimizedLoop instead. Will be removed in v2.0", false)]
    public static string GenerateOptimizedLoop(string indexVar, string limitVar, string body, bool unroll = false, int unrollFactor = 4)
        => LoopOptimizer.GenerateOptimizedLoop(indexVar, limitVar, body, unroll, unrollFactor);

    /// <summary>
    /// Extracts the body of a method as a string.
    /// </summary>
    [Obsolete("Use MethodBodyExtractor.ExtractMethodBody instead. Will be removed in v2.0", false)]
    public static string? ExtractMethodBody(MethodDeclarationSyntax method)
        => MethodBodyExtractor.ExtractMethodBody(method);

    /// <summary>
    /// Analyzes method body for vectorization opportunities.
    /// </summary>
    [Obsolete("Use VectorizationAnalyzer.AnalyzeVectorization instead. Will be removed in v2.0", false)]
    public static VectorizationInfo AnalyzeVectorization(MethodDeclarationSyntax method)
        => VectorizationAnalyzer.AnalyzeVectorization(method);

    // Private helper methods have been moved to VectorizationAnalyzer class

    /// <summary>
    /// Generates SIMD types based on element type.
    /// </summary>
    [Obsolete("Use SimdTypeMapper.GetSimdType instead. Will be removed in v2.0", false)]
    public static string GetSimdType(string elementType, int vectorSize)
        => SimdTypeMapper.GetSimdType(elementType, vectorSize);

    /// <summary>
    /// Generates intrinsic operation based on operation type.
    /// </summary>
    [Obsolete("Use SimdTypeMapper.GetIntrinsicOperation instead. Will be removed in v2.0", false)]
    public static string GetIntrinsicOperation(string operation, string vectorType)
        => SimdTypeMapper.GetIntrinsicOperation(operation, vectorType);
}
