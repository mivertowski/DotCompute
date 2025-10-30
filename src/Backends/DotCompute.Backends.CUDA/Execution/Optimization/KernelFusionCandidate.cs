// <copyright file="KernelFusionCandidate.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Backends.CUDA.Execution.Graph;

namespace DotCompute.Backends.CUDA.Execution.Optimization;

/// <summary>
/// Represents a pair of kernels that are candidates for fusion optimization.
/// Kernel fusion reduces memory traffic by combining operations.
/// </summary>
public sealed class KernelFusionCandidate
{
    /// <summary>
    /// Gets or sets the first kernel in the fusion candidate pair.
    /// This kernel's output typically feeds into the second kernel.
    /// </summary>
    public CudaKernelOperation FirstKernel { get; set; } = null!;

    /// <summary>
    /// Gets or sets the second kernel in the fusion candidate pair.
    /// This kernel consumes the output of the first kernel.
    /// </summary>
    public CudaKernelOperation SecondKernel { get; set; } = null!;

    /// <summary>
    /// Gets or sets the estimated performance benefit of fusion.
    /// Higher values indicate greater expected improvement.
    /// Calculated based on memory bandwidth savings and computation overlap.
    /// </summary>
    public double EstimatedBenefit { get; set; }
}
