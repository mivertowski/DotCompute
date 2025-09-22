// <copyright file="KernelGenerationContext.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Generic;
using DotCompute.Abstractions;
namespace DotCompute.Linq.Operators.Generation;
{
/// <summary>
/// Context for kernel generation containing optimization hints and metadata.
/// </summary>
public class KernelGenerationContext
{
    /// <summary>
    /// Gets or sets the target accelerator type.
    /// </summary>
    public AcceleratorType TargetAcceleratorType { get; set; } = AcceleratorType.CPU;
    /// Gets or sets a value indicating whether to enable vectorization.
    public bool EnableVectorization { get; set; } = true;
    /// Gets or sets a value indicating whether to enable loop unrolling.
    public bool EnableLoopUnrolling { get; set; } = true;
    /// Gets or sets the precision mode for floating-point operations.
    public PrecisionMode PrecisionMode { get; set; } = PrecisionMode.Default;
    /// Gets or sets custom optimization hints.
    public Dictionary<string, object> OptimizationHints { get; set; } = [];
    /// Gets or sets the work group dimensions for kernel execution.
    public int[] WorkGroupDimensions { get; set; } = [256, 1, 1];
    /// Gets or sets metadata for kernel generation.
    public Dictionary<string, object> Metadata { get; set; } = [];
    /// Gets or sets device information for the target accelerator.
    public object? DeviceInfo { get; set; }
    /// Gets or sets whether to use shared memory.
    public bool UseSharedMemory { get; set; }
    /// Gets or sets whether to use vector types.
    public bool UseVectorTypes { get; set; } = true;
    /// Gets or sets the precision level for floating-point operations.
    public string Precision { get; set; } = "Default";
}
