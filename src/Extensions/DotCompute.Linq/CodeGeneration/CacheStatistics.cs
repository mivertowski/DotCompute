// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Statistics and metrics for the kernel cache.
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>
    /// Gets or sets the total number of cache hits (successful lookups).
    /// </summary>
    public long Hits { get; set; }

    /// <summary>
    /// Gets or sets the total number of cache misses (failed lookups).
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    /// Gets or sets the current number of entries in the cache.
    /// </summary>
    public int CurrentEntries { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory usage of the cache in bytes.
    /// </summary>
    public long EstimatedMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the total number of evictions that have occurred.
    /// </summary>
    public long EvictionCount { get; set; }

    /// <summary>
    /// Gets the cache hit ratio (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// Returns 0 if no cache operations have occurred.
    /// </remarks>
    public double HitRatio
    {
        get
        {
            var total = Hits + Misses;
            return total > 0 ? Hits / (double)total : 0.0;
        }
    }

    /// <summary>
    /// Gets the cache miss ratio (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// Returns 0 if no cache operations have occurred.
    /// </remarks>
    public double MissRatio
    {
        get
        {
            var total = Hits + Misses;
            return total > 0 ? Misses / (double)total : 0.0;
        }
    }

    /// <summary>
    /// Creates a snapshot copy of these statistics.
    /// </summary>
    /// <returns>A new <see cref="CacheStatistics"/> instance with copied values.</returns>
    public CacheStatistics Clone()
    {
        return new CacheStatistics
        {
            Hits = Hits,
            Misses = Misses,
            CurrentEntries = CurrentEntries,
            EstimatedMemoryBytes = EstimatedMemoryBytes,
            EvictionCount = EvictionCount
        };
    }
}
