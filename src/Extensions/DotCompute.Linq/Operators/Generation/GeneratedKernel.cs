// <copyright file="GeneratedKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotCompute.Linq.Operators.Generation;

/// <summary>
/// Represents a generated kernel with its metadata for the operators generation system.
/// </summary>
public record GeneratedKernel
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated source code.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source code (alias for Source).
    /// </summary>
    public string SourceCode 
    { 
        get => Source; 
        set => Source = value; 
    }

    /// <summary>
    /// Gets or sets the kernel parameters.
    /// </summary>
    public GeneratedKernelParameter[] Parameters { get; set; } = Array.Empty<GeneratedKernelParameter>();

    /// <summary>
    /// Gets or sets the entry point function name.
    /// </summary>
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target backend.
    /// </summary>
    public string TargetBackend { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the kernel language.
    /// </summary>
    public DotCompute.Abstractions.Types.KernelLanguage Language { get; set; } = DotCompute.Abstractions.Types.KernelLanguage.CSharp;

    /// <summary>
    /// Gets or sets the required work group size for GPU execution.
    /// </summary>
    public int[]? RequiredWorkGroupSize { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; set; }

    /// <summary>
    /// Gets or sets optimization metadata for the kernel.
    /// </summary>
    public Dictionary<string, object> OptimizationMetadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the source expression that generated this kernel.
    /// </summary>
    public Expression? SourceExpression { get; set; }
}