// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;
using DotCompute.Generators.Kernel.Enums;

namespace DotCompute.Generators.Models.Kernel;

/// <summary>
/// Optimization levels for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    None,
    Size,
    Speed,
    Maximum
}

/// <summary>
/// Represents configuration extracted from kernel attributes.
/// </summary>
public sealed class KernelConfiguration
{
    private readonly Collection<string> _supportedBackends = [];

    /// <summary>
    /// Gets the list of supported backend accelerators.
    /// </summary>
    public Collection<string> SupportedBackends => _supportedBackends;

    /// <summary>
    /// Gets or sets the vector size for SIMD operations.
    /// </summary>
    public int VectorSize { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether parallel execution is enabled.
    /// </summary>
    public bool IsParallel { get; set; } = true;

    /// <summary>
    /// Gets or sets the optimization level.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Speed;

    /// <summary>
    /// Gets or sets the memory access pattern.
    /// </summary>
    public MemoryAccessPattern MemoryPattern { get; set; } = MemoryAccessPattern.Sequential;
}