// <copyright file="MemoryAccessPattern.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Defines memory access patterns for kernel optimization.
/// Understanding access patterns enables better memory coalescing and cache utilization.
/// </summary>
public enum MemoryAccessPattern
{
    /// <summary>
    /// Sequential memory access pattern.
    /// Threads access consecutive memory locations in order.
    /// Provides optimal memory coalescing and cache line utilization.
    /// </summary>
    Sequential,

    /// <summary>
    /// Strided memory access pattern.
    /// Threads access memory with a fixed stride between accesses.
    /// May result in reduced memory bandwidth utilization.
    /// </summary>
    Strided,

    /// <summary>
    /// Coalesced memory access pattern.
    /// Threads in a warp access contiguous memory segments.
    /// Optimized for GPU memory architecture, maximizes bandwidth.
    /// </summary>
    Coalesced,

    /// <summary>
    /// Random memory access pattern.
    /// No predictable pattern in memory accesses.
    /// Worst case for cache utilization and memory coalescing.
    /// May benefit from texture memory or cache hints.
    /// </summary>
    Random,

    /// <summary>
    /// Mixed access patterns.
    /// Combination of different access patterns.
    /// </summary>
    Mixed,

    /// <summary>
    /// Scatter operations.
    /// Threads scatter data to non-contiguous memory locations.
    /// May result in poor memory coalescing and cache utilization.
    /// </summary>
    Scatter,

    /// <summary>
    /// Gather operations.
    /// Threads gather data from scattered memory locations.
    /// May benefit from texture memory or vectorized loads.
    /// </summary>
    Gather,

    /// <summary>
    /// Scatter-gather operations.
    /// Threads gather data from scattered memory locations or scatter data.
    /// </summary>
    ScatterGather,

    /// <summary>
    /// Broadcast operations.
    /// Single value broadcast to multiple threads.
    /// </summary>
    Broadcast,

    /// <summary>
    /// Tiled memory access pattern.
    /// Data is accessed in small, localized blocks (tiles).
    /// Benefits from blocking algorithms and shared memory usage on GPUs.
    /// Common in matrix operations and image processing kernels.
    /// </summary>
    Tiled,

    /// <summary>
    /// Unknown access pattern.
    /// Pattern cannot be determined through static analysis.
    /// Requires runtime profiling for optimization.
    /// </summary>
    Unknown
}
