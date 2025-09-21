// <copyright file="KernelDefinition.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Generic;
using DotCompute.Abstractions.Kernels.Types;
using System.Linq.Expressions;
using DotCompute.Linq.Operators.Parameters;
using DotCompute.Abstractions.Types;
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
    /// Gets or sets the source expression for the kernel.
    public Expression? Expression { get; set; }
    /// Gets or sets the compiled source code (if available).
    public string? CompiledSource { get; set; }
    /// Gets or sets the source code (alias for CompiledSource).
    public string? Source
    {
        get => CompiledSource;
        set => CompiledSource = value;
    }
    /// Gets or sets the kernel language.
    public DotCompute.Abstractions.Kernels.Types.KernelLanguage Language { get; set; } = DotCompute.Abstractions.Kernels.Types.KernelLanguage.CSharpIL;
    /// Gets or sets the kernel parameters.
    public List<KernelParameter> Parameters { get; set; } = [];
}
