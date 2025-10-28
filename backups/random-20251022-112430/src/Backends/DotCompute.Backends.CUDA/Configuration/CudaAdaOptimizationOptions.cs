// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Advanced.Features.Types;

namespace DotCompute.Backends.CUDA.Advanced.Features.Configuration
{
    /// <summary>
    /// Optimization options for Ada Lovelace architecture.
    /// </summary>
    public sealed class CudaAdaOptimizationOptions
    {
        /// <summary>
        /// Gets or sets whether to enable tensor core optimizations.
        /// </summary>
        public bool EnableTensorCores { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable cooperative groups.
        /// </summary>
        public bool EnableCooperativeGroups { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable unified memory optimization.
        /// </summary>
        public bool EnableUnifiedMemoryOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable dynamic parallelism.
        /// More conservative default as it has overhead.
        /// </summary>
        public bool EnableDynamicParallelism { get; set; }

        /// <summary>
        /// Gets or sets whether to enable L2 cache optimization.
        /// </summary>
        public bool EnableL2CacheOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable shared memory optimization.
        /// </summary>
        public bool EnableSharedMemoryOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable warp specialization.
        /// </summary>
        public bool EnableWarpSpecialization { get; set; } = true;

        /// <summary>
        /// Gets or sets the optimization profile to use.
        /// </summary>
        public CudaOptimizationProfile Profile { get; set; } = CudaOptimizationProfile.Balanced;
    }
}