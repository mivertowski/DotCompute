// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Caches compiled kernels to avoid recompilation overhead.
/// </summary>
public interface IKernelCache
{
    /// <summary>
    /// Generates a cache key for a kernel configuration.
    /// </summary>
    /// <param name="kernelDefinition">The kernel definition</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <param name="compilationOptions">Optional compilation options</param>
    /// <returns>A unique cache key</returns>
    public string GenerateCacheKey(
        KernelDefinition kernelDefinition,

        IAccelerator accelerator,
        CompilationOptions? compilationOptions);

    /// <summary>
    /// Retrieves a compiled kernel from the cache.
    /// </summary>
    /// <param name="cacheKey">The cache key</param>
    /// <returns>The cached compiled kernel, or null if not found</returns>
    public Task<ICompiledKernel?> GetAsync(string cacheKey);

    /// <summary>
    /// Stores a compiled kernel in the cache.
    /// </summary>
    /// <param name="cacheKey">The cache key</param>
    /// <param name="compiledKernel">The compiled kernel to cache</param>
    /// <returns>A task representing the store operation</returns>
    public Task StoreAsync(string cacheKey, ICompiledKernel compiledKernel);

    /// <summary>
    /// Invalidates a specific cache entry.
    /// </summary>
    /// <param name="cacheKey">The cache key to invalidate</param>
    /// <returns>True if the entry was removed, false if not found</returns>
    public Task<bool> InvalidateAsync(string cacheKey);

    /// <summary>
    /// Clears all cached kernels.
    /// </summary>
    /// <returns>The number of entries cleared</returns>
    public Task<int> ClearAsync();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>Cache statistics including hit rate, size, etc.</returns>
    public Task<CacheStatistics> GetStatisticsAsync();

    /// <summary>
    /// Pre-warms the cache with commonly used kernels.
    /// </summary>
    /// <param name="kernelDefinitions">Kernel definitions to pre-compile and cache</param>
    /// <param name="accelerators">Available accelerators</param>
    /// <returns>Number of kernels successfully cached</returns>
    public Task<int> PrewarmAsync(
        IEnumerable<KernelDefinition> kernelDefinitions,
        IEnumerable<IAccelerator> accelerators);
}

/// <summary>
/// Statistics about kernel cache usage.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Gets or sets the total number of cache entries.
    /// </summary>
    public int EntryCount { get; init; }

    /// <summary>
    /// Gets or sets the total cache size in bytes.
    /// </summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>
    /// Gets or sets the number of cache hits.
    /// </summary>
    public long HitCount { get; init; }

    /// <summary>
    /// Gets or sets the number of cache misses.
    /// </summary>
    public long MissCount { get; init; }

    /// <summary>
    /// Gets the cache hit rate.
    /// </summary>
    public double HitRate => HitCount + MissCount > 0

        ? (double)HitCount / (HitCount + MissCount)

        : 0;

    /// <summary>
    /// Gets or sets the average compilation time saved per hit.
    /// </summary>
    public TimeSpan AverageTimeSavedPerHit { get; init; }
}
