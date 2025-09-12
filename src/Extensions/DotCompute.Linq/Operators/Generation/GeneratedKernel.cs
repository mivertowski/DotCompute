// <copyright file="GeneratedKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Generic;
using System.Linq.Expressions;
using DotCompute.Abstractions.Types;

namespace DotCompute.Linq.Operators.Generation;

/// <summary>
/// Represents a kernel generated from expressions with optimization metadata.
/// </summary>
public record GeneratedKernel
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated kernel source code.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the kernel language.
    /// </summary>
    public KernelLanguage Language { get; init; }

    /// <summary>
    /// Gets or sets the kernel parameters.
    /// </summary>
    public GeneratedKernelParameter[] Parameters { get; init; } = [];

    /// <summary>
    /// Gets or sets the required work group size.
    /// </summary>
    public int[]? RequiredWorkGroupSize { get; init; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; init; }

    /// <summary>
    /// Gets or sets optimization metadata from expression analysis.
    /// </summary>
    public Dictionary<string, object>? OptimizationMetadata { get; init; }

    /// <summary>
    /// Gets or sets the source expression this kernel was generated from.
    /// </summary>
    public Expression? SourceExpression { get; init; }

    /// <summary>
    /// Gets or sets the kernel entry point function name.
    /// </summary>
    public string EntryPoint { get; init; } = "main";

    /// <summary>
    /// Gets or sets the required shared memory size in bytes.
    /// </summary>
    public int RequiredSharedMemory { get; init; }

    /// <summary>
    /// Gets the kernel source code (alias for Source).
    /// </summary>
    public string SourceCode => Source;

    /// <summary>
    /// Gets or sets the kernel metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Gets or sets the target backend for this kernel.
    /// </summary>
    public string TargetBackend { get; init; } = string.Empty;
}