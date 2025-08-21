// <copyright file="TensorCoreConfig.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Execution.Configuration;

/// <summary>
/// Configuration for Tensor Core operations on NVIDIA GPUs.
/// Tensor Cores provide specialized hardware acceleration for matrix operations.
/// </summary>
public sealed class TensorCoreConfig
{
    /// <summary>
    /// Gets or sets the data type for Tensor Core operations.
    /// Supported types include TF32, FP16, BF16, INT8, and FP8.
    /// Default is TF32 for broad compatibility with Ada architecture.
    /// </summary>
    public string DataType { get; set; } = "TF32";

    /// <summary>
    /// Gets or sets the precision mode for Tensor Core operations.
    /// Options include "High" for maximum precision, "Medium" for balanced performance,
    /// and "Fast" for maximum throughput with reduced precision.
    /// </summary>
    public string Precision { get; set; } = "High";
}