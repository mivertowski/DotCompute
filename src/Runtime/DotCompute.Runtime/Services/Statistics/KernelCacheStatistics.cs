// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Statistics;

/// <summary>
/// Kernel cache statistics
/// </summary>
public class KernelCacheStatistics
{
    /// <summary>
    /// Gets the total number of cache requests
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Gets the number of cache hits
    /// </summary>
    public long CacheHits { get; init; }

    /// <summary>
    /// Gets the number of cache misses
    /// </summary>
    public long CacheMisses { get; init; }

    /// <summary>
    /// Gets the cache hit rate
    /// </summary>
    public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0.0;

    /// <summary>
    /// Gets the number of cached kernels
    /// </summary>
    public int CachedKernelCount { get; init; }

    /// <summary>
    /// Gets the total cache size in bytes
    /// </summary>
    public long TotalCacheSize { get; init; }

    /// <summary>
    /// Gets the number of cache evictions
    /// </summary>
    public long EvictionCount { get; init; }

    /// <summary>
    /// Gets the timestamp when these statistics were collected
    /// </summary>
    public DateTime CollectedAt { get; init; } = DateTime.UtcNow;
}