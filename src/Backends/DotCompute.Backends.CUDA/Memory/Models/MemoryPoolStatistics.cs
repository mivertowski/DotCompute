// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Memory.Models
{
    /// <summary>
    /// Statistics for a CUDA memory pool.
    /// </summary>
    public sealed class MemoryPoolStatistics
    {
        /// <summary>
        /// Gets or initializes the amount of used memory in bytes.
        /// </summary>
        public long UsedBytes { get; init; }

        /// <summary>
        /// Gets or initializes the amount of reserved memory in bytes.
        /// </summary>
        public long ReservedBytes { get; init; }

        /// <summary>
        /// Gets or initializes the number of active allocations.
        /// </summary>
        public int AllocationCount { get; init; }
    }
}