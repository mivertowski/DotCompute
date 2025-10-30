// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Features.Types
{
    /// <summary>
    /// Memory access patterns for optimization.
    /// </summary>
    public enum CudaMemoryAccessPattern
    {
        /// <summary>
        /// Sequential memory access pattern.
        /// </summary>
        Sequential,

        /// <summary>
        /// Random memory access pattern.
        /// </summary>
        Random,

        /// <summary>
        /// One-time streaming access pattern.
        /// </summary>
        Streaming,

        /// <summary>
        /// Repeated access to same data (temporal locality).
        /// </summary>
        Temporal,

        /// <summary>
        /// Access to nearby memory locations (spatial locality).
        /// </summary>
        Spatial
    }
}
