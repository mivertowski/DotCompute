// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Kernel.Enums;

/// <summary>
/// Describes the expected memory access pattern for a compute kernel.
/// </summary>
/// <remarks>
/// Memory access patterns significantly impact performance, especially on GPUs
/// where memory bandwidth and latency are critical factors. Providing accurate
/// hints helps the code generator optimize memory layout, prefetching, and caching strategies.
/// </remarks>
public enum MemoryAccessPattern
{
    /// <summary>
    /// Sequential memory access pattern.
    /// </summary>
    /// <remarks>
    /// Data is accessed in a predictable, linear fashion. This pattern
    /// benefits from hardware prefetching and provides optimal cache utilization.
    /// Common in array processing and streaming operations.
    /// </remarks>
    Sequential,

    /// <summary>
    /// Strided memory access pattern.
    /// </summary>
    /// <remarks>
    /// Data is accessed with a fixed stride (skip pattern). This occurs when
    /// processing every nth element or accessing specific fields in structures.
    /// May benefit from software prefetching and vector gather operations.
    /// </remarks>
    Strided,

    /// <summary>
    /// Random memory access pattern.
    /// </summary>
    /// <remarks>
    /// Data is accessed in an unpredictable pattern. This is the most challenging
    /// pattern for optimization and may benefit from larger cache sizes and
    /// reduced memory latency. Common in sparse matrix operations and lookup tables.
    /// </remarks>
    Random,

    /// <summary>
    /// Coalesced memory access pattern (optimized for GPU).
    /// </summary>
    /// <remarks>
    /// Memory accesses from adjacent threads access adjacent memory locations.
    /// This pattern is crucial for GPU performance as it maximizes memory
    /// bandwidth utilization and minimizes the number of memory transactions.
    /// </remarks>
    Coalesced,

    /// <summary>
    /// Tiled memory access pattern.
    /// </summary>
    /// <remarks>
    /// Data is accessed in small, localized blocks (tiles). This pattern
    /// benefits from blocking algorithms and shared memory usage on GPUs.
    /// Common in matrix operations and image processing kernels.
    /// </remarks>
    Tiled
}