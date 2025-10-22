#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Metadata;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Infrastructure;

/// <summary>
/// Provides caching and performance optimization for algorithm plugin operations.
/// Manages assembly loading cache, metadata cache, and compilation result cache.
/// </summary>
public sealed partial class AlgorithmPluginCache : IAsyncDisposable, IDisposable
{
    private readonly ILogger<AlgorithmPluginCache> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly ConcurrentDictionary<string, CachedAssembly> _assemblyCache = new();
    private readonly ConcurrentDictionary<string, PluginMetadata> _metadataCache = new();
    private readonly ConcurrentDictionary<string, CachedExecutionResult> _executionCache = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Represents a cached assembly with load information.
    /// </summary>
    private sealed class CachedAssembly
    {
        /// <summary>
        /// Gets or sets the assembly bytes.
        /// </summary>
        /// <value>The assembly bytes.</value>
        public required byte[] AssemblyBytes { get; init; }
        /// <summary>
        /// Gets or sets the load time.
        /// </summary>
        /// <value>The load time.</value>
        public required DateTime LoadTime { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether h.
        /// </summary>
        /// <value>The hash.</value>
        public required string Hash { get; init; }
        /// <summary>
        /// Gets or sets the last accessed.
        /// </summary>
        /// <value>The last accessed.</value>
        public DateTime LastAccessed { get; set; }
        /// <summary>
        /// Gets or sets the access count.
        /// </summary>
        /// <value>The access count.</value>
        public long AccessCount { get; set; }
        /// <summary>
        /// Gets or sets the load duration.
        /// </summary>
        /// <value>The load duration.</value>
        public TimeSpan LoadDuration { get; init; }
    }

    /// <summary>
    /// Represents a cached execution result.
    /// </summary>
    private sealed class CachedExecutionResult
    {
        /// <summary>
        /// Gets or sets the result.
        /// </summary>
        /// <value>The result.</value>
        public required object Result { get; init; }
        /// <summary>
        /// Gets or sets the execution time.
        /// </summary>
        /// <value>The execution time.</value>
        public required DateTime ExecutionTime { get; init; }
        /// <summary>
        /// Gets or sets the input hash.
        /// </summary>
        /// <value>The input hash.</value>
        public required string InputHash { get; init; }
        /// <summary>
        /// Gets or sets the last accessed.
        /// </summary>
        /// <value>The last accessed.</value>
        public DateTime LastAccessed { get; set; }
        /// <summary>
        /// Gets or sets the execution duration.
        /// </summary>
        /// <value>The execution duration.</value>
        public TimeSpan ExecutionDuration { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether valid.
        /// </summary>
        /// <value>The is valid.</value>
        public bool IsValid { get; set; } = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmPluginCache"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options.</param>
    public AlgorithmPluginCache(
        ILogger<AlgorithmPluginCache> logger,
        AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Setup cleanup timer to run every 5 minutes
        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new CacheStatistics
        {
            AssemblyCacheCount = _assemblyCache.Count,
            MetadataCacheCount = _metadataCache.Count,
            ExecutionCacheCount = _executionCache.Count,
            TotalMemoryUsage = CalculateMemoryUsage(),
            CacheHitRate = CalculateHitRate()
        };
    }

    /// <summary>
    /// Caches assembly bytes with metadata.
    /// </summary>
    /// <param name="assemblyPath">The assembly path.</param>
    /// <param name="assemblyBytes">The assembly bytes.</param>
    /// <param name="loadDuration">The time taken to load.</param>
    /// <returns>True if cached successfully; otherwise, false.</returns>
    public bool CacheAssembly(string assemblyPath, byte[] assemblyBytes, TimeSpan loadDuration)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentNullException.ThrowIfNull(assemblyBytes);

        try
        {
            var hash = ComputeHash(assemblyBytes);
            var cachedAssembly = new CachedAssembly
            {
                AssemblyBytes = assemblyBytes,
                LoadTime = DateTime.UtcNow,
                Hash = hash,
                LastAccessed = DateTime.UtcNow,
                AccessCount = 1,
                LoadDuration = loadDuration
            };

            var added = _assemblyCache.TryAdd(assemblyPath, cachedAssembly);
            if (added)
            {
                LogCachedAssembly(assemblyPath, hash);
            }

            return added;
        }
        catch (Exception ex)
        {
            LogFailedToCacheAssembly(ex, assemblyPath);
            return false;
        }
    }

    /// <summary>
    /// Gets cached assembly bytes.
    /// </summary>
    /// <param name="assemblyPath">The assembly path.</param>
    /// <returns>The cached assembly bytes if found; otherwise, null.</returns>
    public byte[]? GetCachedAssembly(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (_assemblyCache.TryGetValue(assemblyPath, out var cachedAssembly))
        {
            // Update access statistics
            cachedAssembly.LastAccessed = DateTime.UtcNow;
            cachedAssembly.AccessCount++;

            LogCacheHitForAssembly(assemblyPath);
            return cachedAssembly.AssemblyBytes;
        }

        LogCacheMissForAssembly(assemblyPath);
        return null;
    }

    /// <summary>
    /// Checks if an assembly is cached and up-to-date.
    /// </summary>
    /// <param name="assemblyPath">The assembly path.</param>
    /// <returns>True if cached and current; otherwise, false.</returns>
    public bool IsAssemblyCached(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!_assemblyCache.TryGetValue(assemblyPath, out var cachedAssembly))
        {
            return false;
        }

        // Check if file has been modified since caching
        try
        {
            var fileInfo = new FileInfo(assemblyPath);
            if (!fileInfo.Exists)
            {
                // File no longer exists, remove from cache
                _ = _assemblyCache.TryRemove(assemblyPath, out _);
                return false;
            }

            // Check if file has been modified
            if (fileInfo.LastWriteTimeUtc > cachedAssembly.LoadTime)
            {
                // File has been modified, remove from cache
                _ = _assemblyCache.TryRemove(assemblyPath, out _);
                LogAssemblyModified(assemblyPath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogErrorCheckingAssemblyCacheValidity(ex, assemblyPath);
            return false;
        }
    }

    /// <summary>
    /// Caches plugin metadata.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="metadata">The metadata to cache.</param>
    /// <returns>True if cached successfully; otherwise, false.</returns>
    public bool CacheMetadata(string pluginId, PluginMetadata metadata)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(metadata);

        try
        {
            var added = _metadataCache.TryAdd(pluginId, metadata);
            if (added)
            {
                LogCachedMetadata(pluginId);
            }

            return added;
        }
        catch (Exception ex)
        {
            LogFailedToCacheMetadata(ex, pluginId);
            return false;
        }
    }

    /// <summary>
    /// Gets cached plugin metadata.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The cached metadata if found; otherwise, null.</returns>
    public PluginMetadata? GetCachedMetadata(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        if (_metadataCache.TryGetValue(pluginId, out var metadata))
        {
            LogCacheHitForMetadata(pluginId);
            return metadata;
        }

        LogCacheMissForMetadata(pluginId);
        return null;
    }

    /// <summary>
    /// Caches execution result for performance optimization.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="inputHash">Hash of the input parameters.</param>
    /// <param name="result">The execution result.</param>
    /// <param name="executionDuration">The execution duration.</param>
    /// <returns>True if cached successfully; otherwise, false.</returns>
    public bool CacheExecutionResult(string pluginId, string inputHash, object result, TimeSpan executionDuration)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputHash);
        ArgumentNullException.ThrowIfNull(result);

        // Only cache if execution caching is enabled and result is cacheable
        if (!_options.EnableExecutionCaching || !IsResultCacheable(result))
        {
            return false;
        }

        try
        {
            var cacheKey = $"{pluginId}:{inputHash}";
            var cachedResult = new CachedExecutionResult
            {
                Result = result,
                ExecutionTime = DateTime.UtcNow,
                InputHash = inputHash,
                LastAccessed = DateTime.UtcNow,
                ExecutionDuration = executionDuration
            };

            var added = _executionCache.TryAdd(cacheKey, cachedResult);
            if (added)
            {
                LogCachedExecutionResult(pluginId, inputHash);
            }

            return added;
        }
        catch (Exception ex)
        {
            LogFailedToCacheExecutionResult(ex, pluginId);
            return false;
        }
    }

    /// <summary>
    /// Gets cached execution result.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="inputHash">Hash of the input parameters.</param>
    /// <returns>The cached result if found; otherwise, null.</returns>
    public object? GetCachedExecutionResult(string pluginId, string inputHash)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputHash);

        if (!_options.EnableExecutionCaching)
        {
            return null;
        }

        var cacheKey = $"{pluginId}:{inputHash}";
        if (_executionCache.TryGetValue(cacheKey, out var cachedResult))
        {
            // Check if result is still valid (not expired)
            var maxAge = _options.ExecutionCacheMaxAge;
            if (DateTime.UtcNow - cachedResult.ExecutionTime > maxAge)
            {
                _ = _executionCache.TryRemove(cacheKey, out _);
                LogExecutionCacheEntryExpired(pluginId);
                return null;
            }

            // Update access time
            cachedResult.LastAccessed = DateTime.UtcNow;

            LogCacheHitForExecutionResult(pluginId, inputHash);
            return cachedResult.Result;
        }

        LogCacheMissForExecutionResult(pluginId, inputHash);
        return null;
    }

    /// <summary>
    /// Invalidates cached data for a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID to invalidate.</param>
    public void InvalidatePlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        // Remove metadata cache
        _ = _metadataCache.TryRemove(pluginId, out _);

        // Remove execution cache entries
        var keysToRemove = _executionCache.Keys
            .Where(key => key.StartsWith($"{pluginId}:", StringComparison.Ordinal))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _ = _executionCache.TryRemove(key, out _);
        }

        LogInvalidatedCache(pluginId);
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public void ClearAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var assemblyCount = _assemblyCache.Count;
        var metadataCount = _metadataCache.Count;
        var executionCount = _executionCache.Count;

        _assemblyCache.Clear();
        _metadataCache.Clear();
        _executionCache.Clear();

        LogClearedAllCaches(assemblyCount, metadataCount, executionCount);
    }

    /// <summary>
    /// Computes a hash for the given bytes.
    /// </summary>
    private static string ComputeHash(byte[] bytes)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Determines if a result can be cached.
    /// </summary>
    private static bool IsResultCacheable(object result)
    {
        // Don't cache null results
        if (result == null)
        {
            return false;
        }

        var type = result.GetType();

        // Cache primitive types and strings
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
        {
            return true;
        }

        // Cache arrays and collections of primitive types
        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            return elementType?.IsPrimitive == true || elementType == typeof(string);
        }

        // Don't cache complex objects by default for safety
        return false;
    }

    /// <summary>
    /// Calculates current memory usage of the cache.
    /// </summary>
    private long CalculateMemoryUsage()
    {
        long total = 0;

        // Assembly cache
        foreach (var assembly in _assemblyCache.Values)
        {
            total += assembly.AssemblyBytes.Length;
        }

        // Rough estimate for metadata and execution results
        total += _metadataCache.Count * 1024; // Estimate 1KB per metadata
        total += _executionCache.Count * 512;  // Estimate 512 bytes per execution result

        return total;
    }

    /// <summary>
    /// Calculates cache hit rate.
    /// </summary>
    private double CalculateHitRate()
    {
        var totalAccesses = _assemblyCache.Values.Sum(a => a.AccessCount);
        if (totalAccesses == 0)
        {
            return 0.0;
        }

        var hits = _assemblyCache.Values.Count(a => a.AccessCount > 1);
        return (double)hits / _assemblyCache.Count;
    }

    /// <summary>
    /// Performs periodic cleanup of expired cache entries.
    /// </summary>
    private void PerformCleanup(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var maxAge = TimeSpan.FromHours(24); // Cache entries older than 24 hours
            var cleanupCount = 0;

            // Cleanup assembly cache
            var expiredAssemblies = _assemblyCache.Where(kvp =>
                now - kvp.Value.LastAccessed > maxAge).ToList();

            foreach (var kvp in expiredAssemblies)
            {
                if (_assemblyCache.TryRemove(kvp.Key, out _))
                {
                    cleanupCount++;
                }
            }

            // Cleanup execution cache
            var expiredExecutions = _executionCache.Where(kvp =>
                now - kvp.Value.LastAccessed > _options.ExecutionCacheMaxAge).ToList();

            foreach (var kvp in expiredExecutions)
            {
                if (_executionCache.TryRemove(kvp.Key, out _))
                {
                    cleanupCount++;
                }
            }

            if (cleanupCount > 0)
            {
                LogCacheCleanupRemoved(cleanupCount);
            }
        }
        catch (Exception ex)
        {
            LogErrorDuringCacheCleanup(ex);
        }
    }

    /// <summary>
    /// Gets information about cached NuGet packages.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Array of cached package information.</returns>
    public async Task<CachedPackageInfo[]> GetCachedNuGetPackagesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // TODO: Implement actual NuGet package caching
        // For now, return empty array as placeholder
        await Task.CompletedTask;
        return [];
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cached assembly {AssemblyPath} with hash {Hash}")]
    private partial void LogCachedAssembly(string assemblyPath, string hash);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to cache assembly {AssemblyPath}")]
    private partial void LogFailedToCacheAssembly(Exception ex, string assemblyPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache hit for assembly {AssemblyPath}")]
    private partial void LogCacheHitForAssembly(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache miss for assembly {AssemblyPath}")]
    private partial void LogCacheMissForAssembly(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Assembly {AssemblyPath} has been modified, removing from cache")]
    private partial void LogAssemblyModified(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error checking assembly cache validity for {AssemblyPath}")]
    private partial void LogErrorCheckingAssemblyCacheValidity(Exception ex, string assemblyPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cached metadata for plugin {PluginId}")]
    private partial void LogCachedMetadata(string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to cache metadata for plugin {PluginId}")]
    private partial void LogFailedToCacheMetadata(Exception ex, string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache hit for metadata {PluginId}")]
    private partial void LogCacheHitForMetadata(string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache miss for metadata {PluginId}")]
    private partial void LogCacheMissForMetadata(string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cached execution result for {PluginId} with input hash {InputHash}")]
    private partial void LogCachedExecutionResult(string pluginId, string inputHash);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to cache execution result for plugin {PluginId}")]
    private partial void LogFailedToCacheExecutionResult(Exception ex, string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Execution cache entry expired for {PluginId}")]
    private partial void LogExecutionCacheEntryExpired(string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache hit for execution result {PluginId}:{InputHash}")]
    private partial void LogCacheHitForExecutionResult(string pluginId, string inputHash);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache miss for execution result {PluginId}:{InputHash}")]
    private partial void LogCacheMissForExecutionResult(string pluginId, string inputHash);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Invalidated cache for plugin {PluginId}")]
    private partial void LogInvalidatedCache(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cleared all caches: {AssemblyCount} assemblies, {MetadataCount} metadata, {ExecutionCount} execution results")]
    private partial void LogClearedAllCaches(int assemblyCount, int metadataCount, int executionCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache cleanup removed {CleanupCount} expired entries")]
    private partial void LogCacheCleanupRemoved(int cleanupCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error during cache cleanup")]
    private partial void LogErrorDuringCacheCleanup(Exception ex);

    #endregion

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cleanupTimer.Dispose();
            ClearAll();
            await Task.CompletedTask;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cleanupTimer.Dispose();
            ClearAll();
        }
    }
}

/// <summary>
/// Represents cache statistics.
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>
    /// Gets or sets the number of cached assemblies.
    /// </summary>
    public int AssemblyCacheCount { get; init; }

    /// <summary>
    /// Gets or sets the number of cached metadata entries.
    /// </summary>
    public int MetadataCacheCount { get; init; }

    /// <summary>
    /// Gets or sets the number of cached execution results.
    /// </summary>
    public int ExecutionCacheCount { get; init; }

    /// <summary>
    /// Gets or sets the total memory usage in bytes.
    /// </summary>
    public long TotalMemoryUsage { get; init; }

    /// <summary>
    /// Gets or sets the cache hit rate (0.0 to 1.0).
    /// </summary>
    public double CacheHitRate { get; init; }
}