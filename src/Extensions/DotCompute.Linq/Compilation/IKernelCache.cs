using System;
using DotCompute.Linq.CodeGeneration;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Interface for caching compiled compute kernels.
/// </summary>
/// <remarks>
/// Thread-safe cache with LRU eviction and TTL support.
/// Implementations must be safe for concurrent access from multiple threads.
/// </remarks>
public interface IKernelCache : IDisposable
{
    /// <summary>
    /// Gets a cached compiled kernel by key.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <returns>Cached delegate if found, null otherwise.</returns>
    /// <remarks>
    /// This method is thread-safe and updates access statistics.
    /// </remarks>
    Delegate? GetCached(string key);

    /// <summary>
    /// Stores a compiled kernel in the cache.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="compiled">Compiled delegate.</param>
    /// <param name="ttl">Time-to-live for cache entry.</param>
    /// <remarks>
    /// This method is thread-safe. If the cache is full, it will evict
    /// the least recently used entries to make room.
    /// </remarks>
    void Store(string key, Delegate compiled, TimeSpan ttl);

    /// <summary>
    /// Removes a cached kernel by key.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <returns>True if removed, false if not found.</returns>
    bool Remove(string key);

    /// <summary>
    /// Clears all cached kernels.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and resets all cache statistics.
    /// </remarks>
    void Clear();

    /// <summary>
    /// Gets current cache statistics and metrics.
    /// </summary>
    /// <returns>A snapshot of current cache statistics.</returns>
    /// <remarks>
    /// The returned statistics are a point-in-time snapshot and may
    /// become outdated as cache operations continue.
    /// </remarks>
    CacheStatistics GetStatistics();
}
