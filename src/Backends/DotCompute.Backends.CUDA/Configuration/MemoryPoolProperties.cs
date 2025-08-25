// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Memory.Configuration
{
    /// <summary>
    /// Configuration properties for CUDA memory pools.
    /// </summary>
    public sealed class MemoryPoolProperties
    {
        /// <summary>
        /// Gets or initializes the release threshold in bytes.
        /// Memory is returned to the OS when this threshold is exceeded.
        /// Default is 2GB.
        /// </summary>
        public long ReleaseThreshold { get; init; } = 2L * 1024 * 1024 * 1024;

        /// <summary>
        /// Gets or initializes whether to allow opportunistic reuse of allocations.
        /// Default is true.
        /// </summary>
        public bool AllowOpportunisticReuse { get; init; } = true;
    }
}