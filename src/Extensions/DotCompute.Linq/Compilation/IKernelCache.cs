using System;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Interface for caching compiled compute kernels.
/// </summary>
/// <remarks>
/// ⚠️ STUB IMPLEMENTATION - Phase 2 (Test Infrastructure)
/// Full implementation planned for Phase 4 (CPU SIMD Code Generation).
/// </remarks>
public interface IKernelCache
{
    /// <summary>
    /// Gets a cached compiled kernel by key.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <returns>Cached delegate if found, null otherwise.</returns>
    public Delegate? GetCached(string key);

    /// <summary>
    /// Stores a compiled kernel in the cache.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="compiled">Compiled delegate.</param>
    /// <param name="ttl">Time-to-live for cache entry.</param>
    public void Store(string key, Delegate compiled, TimeSpan ttl);

    /// <summary>
    /// Removes a cached kernel by key.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool Remove(string key);

    /// <summary>
    /// Clears all cached kernels.
    /// </summary>
    public void Clear();
}
