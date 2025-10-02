// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Text.Json;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Metadata for cached kernel entries
/// </summary>
internal sealed class KernelCacheMetadata
{
    public required string CacheKey { get; set; }
    public required string KernelName { get; set; }
    public int SourceCodeHash { get; set; }
    public DateTime CompileTime { get; set; }
    public DateTime LastAccessed { get; set; }
    public int AccessCount { get; set; }
    public CompilationOptions? CompilationOptions { get; set; }
    public int PtxSize { get; set; }
}

/// <summary>
/// Manages persistent caching of compiled CUDA kernels to improve compilation performance.
/// Provides thread-safe cache operations with automatic cleanup and persistence.
/// </summary>
internal sealed class CudaCompilationCache : IDisposable
{
    private readonly ConcurrentDictionary<string, CudaCompiledKernel> _kernelCache;
    private readonly ConcurrentDictionary<string, KernelCacheMetadata> _cacheMetadata;
    private readonly string _cacheDirectory;
    private readonly ILogger _logger;
    private bool _disposed;

    // Cache configuration
    private static readonly TimeSpan _cacheExpirationTime = TimeSpan.FromDays(7);
    private static readonly int _maxCacheEntries = 1000;

    // Cached JsonSerializerOptions to avoid CA1869
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public CudaCompilationCache(string cacheDirectory, ILogger logger)
    {
        _cacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kernelCache = new ConcurrentDictionary<string, CudaCompiledKernel>();
        _cacheMetadata = new ConcurrentDictionary<string, KernelCacheMetadata>();

        // Ensure cache directory exists
        _ = Directory.CreateDirectory(_cacheDirectory);

        // Load persistent cache on initialization
        _ = Task.Run(LoadPersistentCacheAsync);
    }

    /// <summary>
    /// Generates a cache key for the given kernel definition and options.
    /// </summary>
    public static string GenerateCacheKey(KernelDefinition definition, CompilationOptions? options)
    {
        var optionsHash = options?.GetHashCode() ?? 0;
        var sourceHash = definition.Code?.GetHashCode() ?? 0;
        var entryPointHash = definition.EntryPoint?.GetHashCode() ?? 0;

        return $"{definition.Name}_{sourceHash:X8}_{entryPointHash:X8}_{optionsHash:X8}";
    }

    /// <summary>
    /// Attempts to retrieve a compiled kernel from cache.
    /// </summary>
    public bool TryGetCachedKernel(string cacheKey, out CudaCompiledKernel? cachedKernel, out KernelCacheMetadata? metadata)
    {
        cachedKernel = null;
        metadata = null;

        if (_kernelCache.TryGetValue(cacheKey, out cachedKernel))
        {
            // Update cache access statistics
            if (_cacheMetadata.TryGetValue(cacheKey, out metadata))
            {
                metadata.LastAccessed = DateTime.UtcNow;
                metadata.AccessCount++;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Stores a compiled kernel in the cache with metadata.
    /// </summary>
    public async Task CacheKernelAsync(
        string cacheKey,
        CudaCompiledKernel compiledKernel,
        KernelDefinition definition,
        CompilationOptions? options)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            // Create metadata
            var metadata = new KernelCacheMetadata
            {
                CacheKey = cacheKey,
                KernelName = definition.Name,
                SourceCodeHash = definition.Code?.GetHashCode() ?? 0,
                CompileTime = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                AccessCount = 1,
                CompilationOptions = options,
                PtxSize = 0 // Will be updated if needed
            };

            // Add to in-memory cache
            _ = _kernelCache.TryAdd(cacheKey, compiledKernel);
            _ = _cacheMetadata.TryAdd(cacheKey, metadata);

            // Persist to disk asynchronously
            await PersistKernelToDiskAsync(cacheKey, compiledKernel, metadata).ConfigureAwait(false);

            _logger.LogDebug("Cached compiled kernel {KernelName} with key {CacheKey}", definition.Name, cacheKey);

            // Clean up old entries if cache is too large
            await CleanupCacheIfNeededAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache kernel {KernelName}", definition.Name);
        }
    }

    /// <summary>
    /// Loads the persistent cache from disk.
    /// </summary>
    private async Task LoadPersistentCacheAsync()
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var metadataFile = Path.Combine(_cacheDirectory, "metadata.json");
            if (!File.Exists(metadataFile))
            {
                _logger.LogDebug("No persistent cache metadata found");
                return;
            }

            var metadataJson = await File.ReadAllTextAsync(metadataFile).ConfigureAwait(false);
            var metadataEntries = JsonSerializer.Deserialize<Dictionary<string, KernelCacheMetadata>>(metadataJson, _jsonOptions);

            if (metadataEntries == null)
            {
                _logger.LogWarning("Failed to deserialize cache metadata");
                return;
            }

            var loadedCount = 0;
            foreach (var (cacheKey, metadata) in metadataEntries)
            {
                try
                {
                    // Check if entry is expired
                    if (IsCacheEntryExpired(metadata))
                    {
                        await DeleteCacheEntryAsync(cacheKey).ConfigureAwait(false);
                        continue;
                    }

                    // Try to load the cached kernel file
                    var kernelFile = Path.Combine(_cacheDirectory, SanitizeFileName(cacheKey) + ".cache");
                    if (File.Exists(kernelFile))
                    {
                        _ = _cacheMetadata.TryAdd(cacheKey, metadata);
                        loadedCount++;
                    }
                    else
                    {
                        _logger.LogDebug("Cache file missing for key {CacheKey}", cacheKey);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load cache entry {CacheKey}", cacheKey);
                }
            }

            _logger.LogInformation("Loaded {LoadedCount} cache entries from persistent storage", loadedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load persistent cache");
        }
    }

    /// <summary>
    /// Persists a kernel to disk cache.
    /// </summary>
    private async Task PersistKernelToDiskAsync(string cacheKey, CudaCompiledKernel compiledKernel, KernelCacheMetadata metadata)
    {
        try
        {
            var sanitizedKey = SanitizeFileName(cacheKey);
            var kernelFile = Path.Combine(_cacheDirectory, sanitizedKey + ".cache");

            // For now, we'll just store metadata since CudaCompiledKernel is complex
            // In a full implementation, you'd serialize the kernel binary data
            await File.WriteAllTextAsync(kernelFile, JsonSerializer.Serialize(metadata, _jsonOptions)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist kernel to disk cache for key {CacheKey}", cacheKey);
        }
    }

    /// <summary>
    /// Cleans up the cache if it exceeds size limits.
    /// </summary>
    private async Task CleanupCacheIfNeededAsync()
    {
        if (_cacheMetadata.Count <= _maxCacheEntries)
        {
            return;
        }


        try
        {
            // Remove oldest entries based on last access time
            var entriesToRemove = _cacheMetadata.Values
                .OrderBy(m => m.LastAccessed)
                .Take(_cacheMetadata.Count - _maxCacheEntries + 100) // Remove extra to avoid frequent cleanup
                .ToList();

            foreach (var metadata in entriesToRemove)
            {
                await DeleteCacheEntryAsync(metadata.CacheKey).ConfigureAwait(false);
            }

            _logger.LogDebug("Cleaned up {RemovedCount} old cache entries", entriesToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup cache");
        }
    }

    /// <summary>
    /// Deletes a cache entry from both memory and disk.
    /// </summary>
    private async Task DeleteCacheEntryAsync(string cacheKey)
    {
        try
        {
            _ = _kernelCache.TryRemove(cacheKey, out _);
            _ = _cacheMetadata.TryRemove(cacheKey, out _);

            var sanitizedKey = SanitizeFileName(cacheKey);
            var kernelFile = Path.Combine(_cacheDirectory, sanitizedKey + ".cache");
            if (File.Exists(kernelFile))
            {
                File.Delete(kernelFile);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete cache entry {CacheKey}", cacheKey);
        }
    }

    /// <summary>
    /// Saves the current cache metadata to disk.
    /// </summary>
    public async Task SavePersistentCacheAsync()
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var metadataFile = Path.Combine(_cacheDirectory, "metadata.json");
            var metadataSnapshot = _cacheMetadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var metadataJson = JsonSerializer.Serialize(metadataSnapshot, _jsonOptions);
            await File.WriteAllTextAsync(metadataFile, metadataJson).ConfigureAwait(false);

            _logger.LogDebug("Saved cache metadata for {EntryCount} entries", metadataSnapshot.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save persistent cache metadata");
        }
    }

    /// <summary>
    /// Checks if a cache entry has expired.
    /// </summary>
    private static bool IsCacheEntryExpired(KernelCacheMetadata metadata)
    {
        return DateTime.UtcNow - metadata.CompileTime > _cacheExpirationTime;
    }

    /// <summary>
    /// Sanitizes a filename for safe disk storage.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
    }

    /// <summary>
    /// Gets cache statistics including hit rate and entry information.
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        var totalRequests = 0;
        var totalAccessCount = 0;
        DateTime? oldestEntry = null;
        DateTime? newestEntry = null;

        foreach (var metadata in _cacheMetadata.Values)
        {
            totalRequests += metadata.AccessCount;
            totalAccessCount += metadata.AccessCount;

            if (oldestEntry == null || metadata.CompileTime < oldestEntry)
            {
                oldestEntry = metadata.CompileTime;
            }

            if (newestEntry == null || metadata.CompileTime > newestEntry)
            {
                newestEntry = metadata.CompileTime;
            }
        }

        var entryCount = _cacheMetadata.Count;

        return new CacheStatistics
        {
            HitCount = totalRequests,
            MissCount = 0, // Would need to track this separately
            TotalEntries = entryCount,
            TotalSizeBytes = 0, // Would need to calculate from actual sizes
            HitRate = totalRequests > 0 ? 1.0 : 0.0, // Simplified
            AverageAccessCount = entryCount > 0 ? (double)totalAccessCount / entryCount : 0.0,
            OldestEntryTime = oldestEntry,
            NewestEntryTime = newestEntry,
            CacheSizeBytes = 0, // Would need to calculate from actual sizes
            LastAccess = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Clears all cached kernels and metadata.
    /// </summary>
    public void ClearCache()
    {
        _kernelCache.Clear();
        _cacheMetadata.Clear();

        try
        {
            // Clean up disk cache
            if (Directory.Exists(_cacheDirectory))
            {
                foreach (var file in Directory.GetFiles(_cacheDirectory, "*.cache"))
                {
                    File.Delete(file);
                }

                var metadataFile = Path.Combine(_cacheDirectory, "metadata.json");
                if (File.Exists(metadataFile))
                {
                    File.Delete(metadataFile);
                }
            }

            _logger.LogDebug("Cache cleared successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear disk cache");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            // Save cache state before disposing
            _ = Task.Run(SavePersistentCacheAsync);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save cache during disposal");
        }

        _disposed = true;
    }
}