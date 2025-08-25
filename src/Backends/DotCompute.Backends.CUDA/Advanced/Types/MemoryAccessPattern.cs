// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Types
{
    /// <summary>
    /// Memory access patterns for optimization.
    /// </summary>
    public enum MemoryAccessPattern
    {
        /// <summary>
        /// Data fits in and should use shared memory.
        /// </summary>
        SharedMemory,

        /// <summary>
        /// Data fits in L2 cache.
        /// </summary>
        L2Cache,

        /// <summary>
        /// Global memory with coalesced access pattern.
        /// </summary>
        GlobalMemoryCoalesced,

        /// <summary>
        /// Global memory with strided access pattern.
        /// </summary>
        GlobalMemoryStrided
    }
}