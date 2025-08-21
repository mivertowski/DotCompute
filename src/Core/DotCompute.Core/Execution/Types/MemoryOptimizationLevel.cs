// <copyright file="MemoryOptimizationLevel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines levels of memory optimization to apply during execution.
    /// Higher optimization levels reduce memory usage at the potential cost of execution speed.
    /// </summary>
    public enum MemoryOptimizationLevel
    {
        /// <summary>
        /// No memory optimizations applied.
        /// Prioritizes execution speed over memory efficiency.
        /// Uses straightforward memory allocation without advanced optimization techniques.
        /// </summary>
        None,

        /// <summary>
        /// Basic memory optimizations such as buffer reuse.
        /// Implements simple optimizations like reusing temporary buffers.
        /// Provides modest memory savings with minimal impact on performance.
        /// </summary>
        Basic,

        /// <summary>
        /// Balanced optimizations between memory usage and performance.
        /// Applies moderate optimization techniques that provide good memory efficiency
        /// while maintaining reasonable execution performance.
        /// </summary>
        Balanced,

        /// <summary>
        /// Aggressive memory optimizations prioritizing minimal memory usage.
        /// Implements advanced techniques like gradient checkpointing and memory defragmentation.
        /// Maximizes memory efficiency but may impact execution speed.
        /// </summary>
        Aggressive
    }
}
