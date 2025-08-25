// <copyright file="CudaArchitecture.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Execution.Types;

/// <summary>
/// Represents NVIDIA GPU architectures for optimization targeting.
/// Each architecture has specific capabilities and optimization strategies.
/// </summary>
public enum CudaArchitecture
{
    /// <summary>
    /// Turing architecture (SM 7.5).
    /// Features include RT cores, Tensor cores, and improved integer operations.
    /// Examples: RTX 2060, RTX 2070, RTX 2080, T4.
    /// </summary>
    Turing,

    /// <summary>
    /// Ampere architecture (SM 8.0, 8.6).
    /// Enhanced Tensor cores, improved RT cores, and better async compute.
    /// Examples: RTX 3060, RTX 3070, RTX 3080, RTX 3090, A100, A40.
    /// </summary>
    Ampere,

    /// <summary>
    /// Ada Lovelace architecture (SM 8.9).
    /// Third-generation RT cores, fourth-generation Tensor cores, and improved efficiency.
    /// Examples: RTX 4060, RTX 4070, RTX 4080, RTX 4090, RTX 2000 Ada, L40.
    /// </summary>
    Ada,

    /// <summary>
    /// Hopper architecture (SM 9.0).
    /// Designed for datacenter, featuring Transformer Engine and improved multi-instance GPU.
    /// Examples: H100, H200.
    /// </summary>
    Hopper
}