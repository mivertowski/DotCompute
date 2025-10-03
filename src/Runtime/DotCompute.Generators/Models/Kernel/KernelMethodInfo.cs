// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Models.Kernel;

/// <summary>
/// Represents metadata about a kernel method for code generation.
/// </summary>
public sealed class KernelMethodInfo
{
    private readonly Collection<ParameterInfo> _parameters = [];
    private readonly Collection<string> _backends = [];

    /// <summary>
    /// Gets or sets the method name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fully qualified name of the containing type.
    /// </summary>
    public string ContainingType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the namespace containing the method.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Gets the method parameters.
    /// </summary>
    public Collection<ParameterInfo> Parameters => _parameters;

    /// <summary>
    /// Gets or sets the return type of the method.
    /// </summary>
    public string ReturnType { get; set; } = string.Empty;

    /// <summary>
    /// Gets the list of supported backend accelerators.
    /// </summary>
    public Collection<string> Backends => _backends;

    /// <summary>
    /// Gets or sets the vector size for SIMD operations.
    /// </summary>
    public int VectorSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether parallel execution is enabled.
    /// </summary>
    public bool IsParallel { get; set; }

    /// <summary>
    /// Gets or sets the original method declaration syntax.
    /// </summary>
    public MethodDeclarationSyntax? MethodDeclaration { get; set; }
}