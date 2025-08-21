// <copyright file="ResourceUsageEstimate.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Kernels.Validation;

/// <summary>
/// Represents resource usage estimates for a compiled kernel.
/// Provides insights into memory, register usage, and performance characteristics
/// to help optimize kernel execution and occupancy.
/// </summary>
public sealed class ResourceUsageEstimate
{
    /// <summary>
    /// Gets or sets the register count per thread.
    /// Higher register usage may reduce the number of concurrent threads.
    /// </summary>
    public int RegistersPerThread { get; init; }

    /// <summary>
    /// Gets or sets the shared memory per block in bytes.
    /// Shared memory is limited per streaming multiprocessor and affects occupancy.
    /// </summary>
    public int SharedMemoryPerBlock { get; init; }

    /// <summary>
    /// Gets or sets the constant memory usage in bytes.
    /// Constant memory is cached and provides fast access for read-only data.
    /// </summary>
    public int ConstantMemoryUsage { get; init; }

    /// <summary>
    /// Gets or sets the maximum threads per block.
    /// Determined by resource constraints and kernel requirements.
    /// </summary>
    public int MaxThreadsPerBlock { get; init; }

    /// <summary>
    /// Gets or sets the occupancy estimate (0-1).
    /// Represents the ratio of active warps to maximum possible warps.
    /// Higher occupancy generally leads to better resource utilization.
    /// </summary>
    public float OccupancyEstimate { get; init; }
}