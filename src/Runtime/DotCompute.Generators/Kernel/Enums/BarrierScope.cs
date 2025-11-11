// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Kernel.Enums;

/// <summary>
/// Defines the synchronization scope for GPU thread barriers.
/// </summary>
/// <remarks>
/// This enum mirrors DotCompute.Abstractions.Barriers.BarrierScope for use in source generators.
/// Source generators target netstandard2.0 and cannot reference the main Abstractions assembly.
/// </remarks>
public enum BarrierScope
{
    /// <summary>
    /// Synchronize all threads within a single thread block.
    /// Maps to __syncthreads() in CUDA, threadgroup_barrier() in Metal.
    /// </summary>
    ThreadBlock = 0,

    /// <summary>
    /// Synchronize all threads across all blocks in the entire kernel grid.
    /// Requires cooperative launch. Not supported on Metal.
    /// </summary>
    Grid = 1,

    /// <summary>
    /// Synchronize all threads within a single 32-thread warp.
    /// Maps to __syncwarp() in CUDA, simdgroup_barrier() in Metal.
    /// </summary>
    Warp = 2,

    /// <summary>
    /// Synchronize an arbitrary subset of threads (tile) within a thread block.
    /// </summary>
    Tile = 3,

    /// <summary>
    /// Synchronize threads across multiple GPUs and the CPU.
    /// </summary>
    System = 4
}
