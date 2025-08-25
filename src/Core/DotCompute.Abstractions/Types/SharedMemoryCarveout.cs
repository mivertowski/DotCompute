// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Types
{
    /// <summary>
    /// Shared memory carveout preferences for Ada generation GPUs.
    /// </summary>
    public enum SharedMemoryCarveout
    {
        /// <summary>
        /// Default configuration: 48KB shared memory, 16KB L1 cache.
        /// </summary>
        Default,

        /// <summary>
        /// Prefer shared memory: 100KB shared memory, minimal L1 cache.
        /// </summary>
        Prefer100KB,

        /// <summary>
        /// Prefer L1 cache: 16KB shared memory, 48KB L1 cache.
        /// </summary>
        PreferL1
    }
}