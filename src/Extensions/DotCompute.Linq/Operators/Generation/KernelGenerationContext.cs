// <copyright file="KernelGenerationContext.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Generic;
using DotCompute.Abstractions;

namespace DotCompute.Linq.Operators.Generation;

/// <summary>
/// Context for kernel generation containing optimization hints and metadata.
/// </summary>
public class KernelGenerationContext
{
    /// <summary>
    /// Gets or sets the target accelerator type.
    /// </summary>
    public AcceleratorType TargetAcceleratorType { get; set; } = AcceleratorType.CPU;

    /// <summary>
    /// Gets or sets a value indicating whether to enable vectorization.
    /// </summary>
    public bool EnableVectorization { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable loop unrolling.
    /// </summary>
    public bool EnableLoopUnrolling { get; set; } = true;

    /// <summary>
    /// Gets or sets the precision mode for floating-point operations.
    /// </summary>
    public PrecisionMode PrecisionMode { get; set; } = PrecisionMode.Default;

    /// <summary>
    /// Gets or sets custom optimization hints.
    /// </summary>
    public Dictionary<string, object> OptimizationHints { get; set; } = new();
}