// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Cache for compiled CUDA kernels to avoid recompilation.
/// </summary>
public sealed class CudaKernelCache : IDisposable
{
    private readonly ConcurrentDictionary<string, ICompiledKernel> _cache = new();
    private readonly ILogger? _logger;
    private bool _disposed;

    /// <summary>
    /// Gets the singleton instance of the cache.
    /// </summary>
    public static CudaKernelCache Instance { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaKernelCache"/> class.
    /// </summary>
    public CudaKernelCache()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaKernelCache"/> class with logging.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public CudaKernelCache(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Tries to get a cached kernel.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="kernel">The cached kernel if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    public bool TryGetKernel(string key, out ICompiledKernel? kernel)
    {
        return _cache.TryGetValue(key, out kernel);
    }

    /// <summary>
    /// Adds or updates a kernel in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="kernel">The compiled kernel.</param>
    public void AddOrUpdateKernel(string key, ICompiledKernel kernel)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        }

        _cache.AddOrUpdate(key, kernel, (_, _) => kernel);
    }

    /// <summary>
    /// Removes a kernel from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>True if removed; otherwise, false.</returns>
    public bool RemoveKernel(string key)
    {
        return _cache.TryRemove(key, out _);
    }

    /// <summary>
    /// Clears all cached kernels.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Gets the number of cached kernels.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Generates a cache key for a kernel definition.
    /// </summary>
    /// <param name="definition">The kernel definition.</param>
    /// <param name="options">Compilation options.</param>
    /// <returns>A unique cache key.</returns>
    public static string GenerateCacheKey(KernelDefinition definition, CompilationOptions? options = null)
    {
        var key = $"{definition.Name}_{definition.GetHashCode()}";
        if (options != null)
        {
            key += $"_{options.GetHashCode()}";
        }
        return key;
    }

    /// <summary>
    /// Caches a compiled kernel with the specified key.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="kernel">Compiled kernel to cache.</param>
    public void CacheKernel(string key, ICompiledKernel kernel)
    {
        AddOrUpdateKernel(key, kernel);
        _logger?.LogDebug("Cached kernel with key: {Key}", key);
    }

    /// <summary>
    /// Optimizes the cache for a specific workload profile.
    /// </summary>
    /// <param name="profile">Workload profile for optimization.</param>
    public void OptimizeForWorkload(object profile)
    {
        // Basic implementation - could be enhanced with workload-specific optimization
        _logger?.LogDebug("Optimizing cache for workload profile");

        // In a full implementation, this would:
        // - Analyze workload patterns
        // - Evict less-used kernels
        // - Pre-compile frequently used kernels
        // - Adjust cache size based on workload
    }

    /// <summary>
    /// Performs cleanup of unused kernels from the cache.
    /// </summary>
    public void Cleanup()
    {
        // Basic cleanup - remove old or unused entries
        // In a full implementation, this would use LRU or other eviction strategies
        _logger?.LogDebug("Performing cache cleanup. Current size: {Count}", Count);

        // For now, we'll keep all entries as we don't have access tracking
        // A full implementation would track access times and remove stale entries
    }

    /// <summary>
    /// Disposes the cache and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
        _disposed = true;
        _logger?.LogDebug("CudaKernelCache disposed");
    }
}