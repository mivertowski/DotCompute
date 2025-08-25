// <copyright file="CudaGraphOptimizationLevel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Execution.Types;

/// <summary>
/// Defines the optimization levels available for CUDA graph operations.
/// Higher levels provide more aggressive optimizations but may increase compilation time.
/// </summary>
public enum CudaGraphOptimizationLevel
{
    /// <summary>
    /// No optimization applied. Graphs are executed as-is without modification.
    /// Best for debugging and development.
    /// </summary>
    None,

    /// <summary>
    /// Basic optimizations including dead code elimination and simple kernel merging.
    /// Minimal impact on compilation time.
    /// </summary>
    Basic,

    /// <summary>
    /// Balanced optimization providing good performance improvements without excessive compilation overhead.
    /// Includes kernel fusion, memory coalescing, and shared memory optimization.
    /// Recommended for most production scenarios.
    /// </summary>
    Balanced,

    /// <summary>
    /// Aggressive optimization with all available techniques applied.
    /// May significantly increase compilation time but provides maximum performance.
    /// Best for performance-critical paths where compilation cost can be amortized.
    /// </summary>
    Aggressive
}