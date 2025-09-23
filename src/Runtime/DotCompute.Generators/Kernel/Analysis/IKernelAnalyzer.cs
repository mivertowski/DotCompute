// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel.Attributes;
using DotCompute.Generators.Kernel.Enums;
using DotCompute.Generators.Kernel.Enums;
using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Kernel.Analysis;

/// <summary>
/// Defines the contract for kernel syntax analysis.
/// </summary>
public interface IKernelAnalyzer
{
    /// <summary>
    /// Analyzes a method to extract kernel information.
    /// </summary>
    public KernelMethodInfo? AnalyzeMethod(GeneratorSyntaxContext context);

    /// <summary>
    /// Analyzes a class to extract kernel class information.
    /// </summary>
    public KernelClassInfo? AnalyzeClass(GeneratorSyntaxContext context);

    /// <summary>
    /// Determines if a syntax node represents a kernel method.
    /// </summary>
    public bool IsKernelMethod(SyntaxNode node);

    /// <summary>
    /// Determines if a syntax node represents a kernel class.
    /// </summary>
    public bool IsKernelClass(SyntaxNode node);
}