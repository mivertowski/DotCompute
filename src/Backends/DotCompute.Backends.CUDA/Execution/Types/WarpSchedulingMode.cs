// <copyright file="WarpSchedulingMode.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Execution.Types;

/// <summary>
/// Defines warp scheduling modes for CUDA kernel execution.
/// Controls how warps are scheduled on streaming multiprocessors.
/// </summary>
public enum WarpSchedulingMode
{
    /// <summary>
    /// Default warp scheduling mode.
    /// Uses the GPU's default scheduling policy.
    /// Suitable for most general-purpose kernels.
    /// </summary>
    Default,

    /// <summary>
    /// Persistent warp scheduling mode.
    /// Warps remain resident on SMs for the kernel duration.
    /// Reduces scheduling overhead for iterative kernels.
    /// Best for kernels with consistent workload per warp.
    /// </summary>
    Persistent,

    /// <summary>
    /// Dynamic warp scheduling mode.
    /// Warps are dynamically assigned work as they become available.
    /// Provides better load balancing for irregular workloads.
    /// May increase scheduling overhead but improves utilization.
    /// </summary>
    Dynamic
}