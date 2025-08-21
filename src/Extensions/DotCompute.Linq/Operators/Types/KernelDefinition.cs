// <copyright file="KernelDefinition.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Linq.Expressions;

namespace DotCompute.Linq.Operators.Types;

/// <summary>
/// Defines a kernel with its source expression and metadata.
/// </summary>
public class KernelDefinition
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source expression for the kernel.
    /// </summary>
    public Expression? Expression { get; set; }

    /// <summary>
    /// Gets or sets the compiled source code (if available).
    /// </summary>
    public string? CompiledSource { get; set; }

    /// <summary>
    /// Gets or sets the kernel language.
    /// </summary>
    public KernelLanguage Language { get; set; } = KernelLanguage.CSharp;
}