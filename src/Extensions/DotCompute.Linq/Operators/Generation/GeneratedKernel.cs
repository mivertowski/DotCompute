// <copyright file="GeneratedKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using DotCompute.Abstractions.Kernels.Types;
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
    /// Gets or sets the generated source code.
    public string Source { get; set; } = string.Empty;
    /// Gets or sets the source code (alias for Source).
    public string SourceCode 
    { 
        get => Source; 
        set => Source = value; 
    }
    /// Gets or sets the kernel parameters.
    public GeneratedKernelParameter[] Parameters { get; set; } = Array.Empty<GeneratedKernelParameter>();
    /// Gets or sets the entry point function name.
    public string EntryPoint { get; set; } = string.Empty;
    /// Gets or sets the target backend.
    public string TargetBackend { get; set; } = string.Empty;
    /// Gets or sets additional metadata.
    public Dictionary<string, object> Metadata { get; set; } = [];
    /// Gets or sets the kernel language.
    public DotCompute.Abstractions.Types.DotCompute.Abstractions.Kernels.Types.KernelLanguage Language { get; set; } = DotCompute.Abstractions.Types.DotCompute.Abstractions.Kernels.Types.KernelLanguage.CSharp;
    /// Gets or sets the required work group size for GPU execution.
    public int[]? RequiredWorkGroupSize { get; set; }
    /// Gets or sets the shared memory size in bytes.
    public int SharedMemorySize { get; set; }
    /// Gets or sets optimization metadata for the kernel.
    public Dictionary<string, object> OptimizationMetadata { get; set; } = [];
    /// Gets or sets the source expression that generated this kernel.
    public Expression? SourceExpression { get; set; }
}
