// <copyright file="KernelCompilationRequest.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Generic;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Operators.Types;

namespace DotCompute.Linq.Operators.Models;

/// <summary>
/// Represents a request to compile a kernel.
/// </summary>
public class KernelCompilationRequest
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kernel source code.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kernel language.
    /// </summary>
    public KernelLanguage Language { get; set; } = KernelLanguage.CSharpIL;

    /// <summary>
    /// Gets or sets the target accelerator for compilation.
    /// </summary>
    public IAccelerator? TargetAccelerator { get; set; }

    /// <summary>
    /// Gets or sets the optimization level.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;

    /// <summary>
    /// Gets or sets additional metadata for compilation.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}