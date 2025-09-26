// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Configuration
{
    /// <summary>
    /// Configuration options for kernel chaining functionality.
    /// </summary>
    public sealed class KernelChainingOptions
    {
        /// <summary>
        /// Gets or sets the section name for configuration.
        /// </summary>
        public const string SectionName = "DotCompute:KernelChaining";

        /// <summary>
        /// Gets or sets whether caching is enabled by default.
        /// </summary>
        public bool EnableCachingByDefault { get; set; } = true;

        /// <summary>
        /// Gets or sets the default cache TTL.
        /// </summary>
        public TimeSpan DefaultCacheTtl { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets whether profiling is enabled by default.
        /// </summary>
        public bool EnableProfilingByDefault { get; set; }


        /// <summary>
        /// Gets or sets whether validation is enabled by default.
        /// </summary>
        public bool EnableValidationByDefault { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of parallel kernels.
        /// </summary>
        public int MaxParallelKernels { get; set; } = Environment.ProcessorCount * 2;

        /// <summary>
        /// Gets or sets the default execution timeout.
        /// </summary>
        public TimeSpan DefaultExecutionTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether to use optimistic execution (continue on errors when possible).
        /// </summary>
        public bool UseOptimisticExecution { get; set; }


        /// <summary>
        /// Gets or sets the maximum cache memory size in bytes.
        /// </summary>
        public long MaxCacheMemorySize { get; set; } = 256 * 1024 * 1024; // 256 MB

        /// <summary>
        /// Gets or sets whether to enable automatic optimization recommendations.
        /// </summary>
        public bool EnableOptimizationRecommendations { get; set; } = true;

        /// <summary>
        /// Gets or sets the kernel resolution timeout.
        /// </summary>
        public TimeSpan KernelResolutionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
