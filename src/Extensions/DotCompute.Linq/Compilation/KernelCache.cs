using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Compilation;

/// <summary>
/// Cache for compiled GPU kernels to avoid recompilation.
/// </summary>
/// <remarks>
/// The cache uses source code hash + backend + architecture as the cache key.
/// Cached kernels are kept in memory for the lifetime of the application.
/// Future enhancements could add:
/// - Disk-based caching for persistence across runs
/// - TTL-based expiration
/// - Size limits and LRU eviction
/// </remarks>
public sealed class KernelCache
{
    private readonly ConcurrentDictionary<string, CompiledKernel> _cache = new();
    private readonly ILogger<KernelCache>? _logger;

    /// <summary>
    /// Initializes a new kernel cache.
    /// </summary>
    public KernelCache(ILogger<KernelCache>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the number of cached kernels.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Tries to get a compiled kernel from the cache.
    /// </summary>
    /// <param name="sourceCode">The kernel source code.</param>
    /// <param name="backend">The target compute backend.</param>
    /// <param name="architecture">The target architecture (e.g., "sm_75", "1.2").</param>
    /// <param name="kernel">The cached kernel if found.</param>
    /// <returns>True if kernel was found in cache; false otherwise.</returns>
    public bool TryGet(
        string sourceCode,
        ComputeBackend backend,
        string? architecture,
        out CompiledKernel? kernel)
    {
        string cacheKey = GenerateCacheKey(sourceCode, backend, architecture);

        if (_cache.TryGetValue(cacheKey, out kernel))
        {
            _logger?.LogDebug("Kernel cache hit: {Key}", cacheKey);
            return true;
        }

        _logger?.LogDebug("Kernel cache miss: {Key}", cacheKey);
        kernel = null;
        return false;
    }

    /// <summary>
    /// Adds a compiled kernel to the cache.
    /// </summary>
    /// <param name="sourceCode">The kernel source code.</param>
    /// <param name="backend">The target compute backend.</param>
    /// <param name="architecture">The target architecture.</param>
    /// <param name="kernel">The compiled kernel to cache.</param>
    public void Add(
        string sourceCode,
        ComputeBackend backend,
        string? architecture,
        CompiledKernel kernel)
    {
        if (kernel == null)
            throw new ArgumentNullException(nameof(kernel));

        string cacheKey = GenerateCacheKey(sourceCode, backend, architecture);

        _cache.AddOrUpdate(cacheKey, kernel, (key, existing) =>
        {
            // Dispose old kernel if it exists
            existing?.Dispose();
            return kernel;
        });

        _logger?.LogInformation("Cached compiled kernel: {Key}", cacheKey);
    }

    /// <summary>
    /// Removes a kernel from the cache.
    /// </summary>
    /// <param name="sourceCode">The kernel source code.</param>
    /// <param name="backend">The target compute backend.</param>
    /// <param name="architecture">The target architecture.</param>
    /// <returns>True if kernel was removed; false if not found.</returns>
    public bool Remove(string sourceCode, ComputeBackend backend, string? architecture)
    {
        string cacheKey = GenerateCacheKey(sourceCode, backend, architecture);

        if (_cache.TryRemove(cacheKey, out var kernel))
        {
            kernel?.Dispose();
            _logger?.LogDebug("Removed kernel from cache: {Key}", cacheKey);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears all cached kernels.
    /// </summary>
    public void Clear()
    {
        int count = _cache.Count;

        foreach (var kvp in _cache)
        {
            kvp.Value?.Dispose();
        }

        _cache.Clear();
        _logger?.LogInformation("Cleared kernel cache: {Count} kernels disposed", count);
    }

    /// <summary>
    /// Generates a cache key from source code, backend, and architecture.
    /// </summary>
    private static string GenerateCacheKey(string sourceCode, ComputeBackend backend, string? architecture)
    {
        // Use SHA256 hash of source code + backend + architecture
        string input = $"{backend}|{architecture ?? "default"}|{sourceCode}";
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = SHA256.HashData(inputBytes);

        // Convert to hex string
        var sb = new StringBuilder(64);
        foreach (byte b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    public CacheStatistics GetStatistics() => new()
    {
        TotalCachedKernels = _cache.Count,
        CacheKeys = new List<string>(_cache.Keys)
    };
}

/// <summary>
/// Statistics about the kernel cache.
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>
    /// Total number of cached kernels.
    /// </summary>
    public int TotalCachedKernels { get; init; }

    /// <summary>
    /// List of cache keys (hashes).
    /// </summary>
    public List<string> CacheKeys { get; init; } = new();
}
