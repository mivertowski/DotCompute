// <copyright file="CudaGraphOptimizationOptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Backends.CUDA.Types;

namespace DotCompute.Backends.CUDA.Configuration;

/// <summary>
/// Configuration options for CUDA graph optimization.
/// Controls various optimization strategies and target architectures.
/// </summary>
public sealed class CudaGraphOptimizationOptions
{
    /// <summary>
    /// Gets or sets whether optimization is enabled for the graph.
    /// When false, graphs are executed without any transformations.
    /// </summary>
    public bool EnableOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether kernel fusion is enabled.
    /// Kernel fusion combines multiple operations to reduce memory traffic.
    /// </summary>
    public bool EnableKernelFusion { get; set; } = true;

    /// <summary>
    /// Gets or sets the optimization level for graph transformations.
    /// Higher levels provide more aggressive optimizations.
    /// </summary>
    public CudaGraphOptimizationLevel OptimizationLevel { get; set; } = CudaGraphOptimizationLevel.Balanced;

    /// <summary>
    /// Gets or sets the target GPU architecture for optimization.
    /// Optimizations are tailored to specific architecture capabilities.
    /// </summary>
    public CudaArchitecture TargetArchitecture { get; set; } = CudaArchitecture.Ada;
}