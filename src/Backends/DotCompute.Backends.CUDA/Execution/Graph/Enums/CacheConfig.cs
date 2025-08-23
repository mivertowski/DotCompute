// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Enums
{
    /// <summary>
    /// Specifies the cache configuration preference for kernel execution.
    /// Controls the balance between L1 cache and shared memory usage on the GPU.
    /// </summary>
    /// <remarks>
    /// Modern NVIDIA GPUs allow configuring the balance between L1 cache and shared memory.
    /// The optimal configuration depends on the kernel's memory access patterns and
    /// shared memory usage characteristics.
    /// </remarks>
    public enum CacheConfig
    {
        /// <summary>
        /// No specific cache configuration preference. Uses the device default.
        /// </summary>
        /// <remarks>
        /// This option allows the CUDA runtime to use the default cache configuration
        /// for the device, which is typically a balanced setting suitable for most kernels.
        /// Corresponds to cudaFuncCachePreferNone.
        /// </remarks>
        PreferNone,

        /// <summary>
        /// Prefer larger shared memory at the expense of L1 cache.
        /// </summary>
        /// <remarks>
        /// Use this configuration for kernels that make heavy use of shared memory
        /// and perform relatively few L1-cacheable memory accesses. Maximizes the
        /// amount of available shared memory per streaming multiprocessor.
        /// Corresponds to cudaFuncCachePreferShared.
        /// </remarks>
        PreferShared,

        /// <summary>
        /// Prefer larger L1 cache at the expense of shared memory.
        /// </summary>
        /// <remarks>
        /// Use this configuration for kernels that perform many L1-cacheable memory
        /// accesses and use little or no shared memory. Maximizes L1 cache capacity
        /// to improve memory access performance for irregular access patterns.
        /// Corresponds to cudaFuncCachePreferL1.
        /// </remarks>
        PreferL1,

        /// <summary>
        /// Balanced configuration between L1 cache and shared memory.
        /// </summary>
        /// <remarks>
        /// This configuration provides equal amounts of L1 cache and shared memory,
        /// suitable for kernels that use both shared memory and benefit from L1 caching.
        /// Corresponds to cudaFuncCachePreferEqual.
        /// </remarks>
        PreferEqual
    }
}