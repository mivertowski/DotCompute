// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Enums
{
    /// <summary>
    /// Specifies the memory access pattern for kernel operations.
    /// Enables optimization of memory hierarchy usage and cache behavior.
    /// </summary>
    /// <remarks>
    /// Memory access patterns significantly impact GPU performance due to the importance
    /// of memory bandwidth and cache utilization. Specifying the correct pattern allows
    /// the optimizer to apply appropriate memory access optimizations.
    /// </remarks>
    public enum MemoryAccessPattern
    {
        /// <summary>
        /// Sequential memory access pattern with adjacent threads accessing adjacent memory locations.
        /// </summary>
        /// <remarks>
        /// This is the default pattern for many algorithms. While not optimal for GPU memory
        /// systems, it represents straightforward memory access without specific optimization.
        /// </remarks>
        Sequential,

        /// <summary>
        /// Strided memory access pattern with regular spacing between accessed elements.
        /// </summary>
        /// <remarks>
        /// Common in matrix operations where threads access elements with fixed stride patterns.
        /// The optimizer can apply prefetching and cache optimization strategies for this pattern.
        /// </remarks>
        Strided,

        /// <summary>
        /// Coalesced memory access pattern optimized for GPU memory architecture.
        /// </summary>
        /// <remarks>
        /// This is the optimal pattern for GPU memory systems where adjacent threads access
        /// adjacent memory locations within the same cache line, maximizing memory bandwidth
        /// utilization and minimizing memory transactions.
        /// </remarks>
        Coalesced,

        /// <summary>
        /// Random memory access pattern with unpredictable access locations.
        /// </summary>
        /// <remarks>
        /// This pattern is challenging for GPU memory optimization due to poor cache locality.
        /// The optimizer may apply different caching strategies or recommend algorithmic changes
        /// to improve performance for this access pattern.
        /// </remarks>
        Random
    }
}