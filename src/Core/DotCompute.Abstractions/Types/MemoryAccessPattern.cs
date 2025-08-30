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
    /// Scatter-gather operations.
    /// Threads gather data from scattered memory locations or scatter data.
    /// </summary>
    ScatterGather,

    /// <summary>
    /// Broadcast operations.
    /// Single value broadcast to multiple threads.
    /// </summary>
    Broadcast
}