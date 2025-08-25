// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Advanced.Types;

namespace DotCompute.Backends.CUDA.Advanced.Configuration
{
    /// <summary>
    /// Shared memory configuration for Ada generation GPUs.
    /// </summary>
    public sealed class SharedMemoryConfig
    {
        /// <summary>
        /// Gets or sets the total shared memory bytes per block.
        /// </summary>
        public int BytesPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the shared memory bytes per thread.
        /// </summary>
        public int BytesPerThread { get; set; }

        /// <summary>
        /// Gets or sets whether 100KB shared memory mode can be used.
        /// </summary>
        public bool CanUse100KB { get; set; }

        /// <summary>
        /// Gets or sets the recommended carveout configuration.
        /// </summary>
        public SharedMemoryCarveout RecommendedCarveout { get; set; }
    }
}