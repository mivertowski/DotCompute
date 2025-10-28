// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Text.Json;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Serialization;
using DotCompute.Backends.CUDA.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Metadata for cached kernel entries
/// </summary>
internal sealed class KernelCacheMetadata
{
    /// <summary>
    /// Gets or sets the cache key.
    /// </summary>
    /// <value>The cache key.</value>
    public required string CacheKey { get; set; }
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public required string KernelName { get; set; }
    /// <summary>
    /// Gets or sets the source code hash.
    /// </summary>
    /// <value>The source code hash.</value>
    public int SourceCodeHash { get; set; }
    /// <summary>
    /// Gets or sets the compile time.
    /// </summary>
    /// <value>The compile time.</value>
    public DateTime CompileTime { get; set; }
    /// <summary>
    /// Gets or sets the last accessed.
    /// </summary>
    /// <value>The last accessed.</value>
    public DateTime LastAccessed { get; set; }
    /// <summary>
    /// Gets or sets the access count.
    /// </summary>
    /// <value>The access count.</value>
    public int AccessCount { get; set; }
    /// <summary>
    /// Gets or sets the compilation options.
    /// </summary>
    /// <value>The compilation options.</value>
    public CompilationOptions? CompilationOptions { get; set; }
    /// <summary>
    /// Gets or sets the ptx size.
    /// </summary>
    /// <value>The ptx size.</value>
    public int PtxSize { get; set; }
}

/// <summary>
/// Manages persistent caching of compiled CUDA kernels to improve compilation performance.
/// Provides thread-safe cache operations with automatic cleanup and persistence.
/// </summary>
internal sealed partial class CudaCompilationCache : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 5100,
        Level = LogLevel.Debug,
        Message = "Cached compiled kernel {KernelName} with key {CacheKey}")]
    private static partial void LogKernelCached(ILogger logger, string kernelName, string cacheKey);

    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Warning,
        Message = "Failed to cache kernel {KernelName}")]
    private static partial void LogCacheFailure(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Debug,
        Message = "No persistent cache metadata found")]
    private static partial void LogNoCacheMetadata(ILogger logger);

    [LoggerMessage(
        EventId = 5103,
        Level = LogLevel.Warning,
        Message = "Failed to deserialize cache metadata")]
    private static partial void LogMetadataDeserializationFailed(ILogger logger);

    [LoggerMessage(
        EventId = 5104,
        Level = LogLevel.Debug,
        Message = "Cache file missing for key {CacheKey}")]
    private static partial void LogCacheFileMissing(ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 5105,
        Level = LogLevel.Warning,
        Message = "Failed to load cache entry {CacheKey}")]
    private static partial void LogCacheEntryLoadFailed(ILogger logger, Exception ex, string cacheKey);

    [LoggerMessage(
        EventId = 5106,
        Level = LogLevel.Information,
        Message = "Loaded {LoadedCount} cache entries from persistent storage")]
    private static partial void LogCacheEntriesLoaded(ILogger logger, int loadedCount);

    [LoggerMessage(
        EventId = 5107,
        Level = LogLevel.Error,
        Message = "Failed to load persistent cache")]
    private static partial void LogPersistentCacheLoadFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5108,
        Level = LogLevel.Warning,
        Message = "Failed to persist kernel to disk cache for key {CacheKey}")]
    private static partial void LogKernelPersistFailed(ILogger logger, Exception ex, string cacheKey);

    [LoggerMessage(
        EventId = 5109,
        Level = LogLevel.Debug,
        Message = "Cleaned up {RemovedCount} old cache entries")]
    private static partial void LogCacheCleanup(ILogger logger, int removedCount);

    [LoggerMessage(
        EventId = 5110,
        Level = LogLevel.Warning,
        Message = "Failed to cleanup cache")]
    private static partial void LogCacheCleanupFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5111,
        Level = LogLevel.Warning,
        Message = "Failed to delete cache entry {CacheKey}")]
    private static partial void LogCacheEntryDeletionFailed(ILogger logger, Exception ex, string cacheKey);

    [LoggerMessage(
        EventId = 5112,
        Level = LogLevel.Debug,
        Message = "Saved cache metadata for {EntryCount} entries")]
    private static partial void LogMetadataSaved(ILogger logger, int entryCount);

    [LoggerMessage(
        EventId = 5113,
        Level = LogLevel.Error,
        Message = "Failed to save persistent cache metadata")]
    private static partial void LogMetadataSaveFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5114,
        Level = LogLevel.Debug,
        Message = "Cache cleared successfully")]
    private static partial void LogCacheCleared(ILogger logger);

    [LoggerMessage(
        EventId = 5115,
        Level = LogLevel.Warning,
        Message = "Failed to clear disk cache")]
    private static partial void LogDiskCacheClearFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5116,
        Level = LogLevel.Warning,
        Message = "Failed to save cache during disposal")]
    private static partial void LogDisposalSaveFailed(ILogger logger, Exception ex);

    #endregion
    private readonly ConcurrentDictionary<string, CudaCompiledKernel> _kernelCache;
    private readonly ConcurrentDictionary<string, KernelCacheMetadata> _cacheMetadata;
    private readonly string _cacheDirectory;
    private readonly ILogger _logger;
    private bool _disposed;

    // Cache configuration
    private static readonly TimeSpan _cacheExpirationTime = TimeSpan.FromDays(7);
    private const int _maxCacheEntries = 1000;

#pragma warning disable CA1823 // Field is used for JSON serialization configuration - future-proofing
    // Cached JsonSerializerOptions to avoid CA1869
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };
#pragma warning restore CA1823
    /// <summary>
    /// Initializes a new instance of the CudaCompilationCache class.
    /// </summary>
    /// <param name="cacheDirectory">The cache directory.</param>
    /// <param name="logger">The logger.</param>

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
        var sourceHash = definition.Code?.GetHashCode(StringComparison.Ordinal) ?? 0;
        var entryPointHash = definition.EntryPoint?.GetHashCode(StringComparison.Ordinal) ?? 0;

        return $"{definition.Name}_{sourceHash:X8}_{entryPointHash:X8}_{optionsHash:X8}";
    }

    /// <summary>
    /// Attempts to retrieve a compiled kernel from cache.
    /// </summary>
    public bool TryGetCachedKernel(string cacheKey, out CudaCompiledKernel? cachedKernel, out KernelCacheMetadata? metadata)
    {
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
                SourceCodeHash = definition.Code?.GetHashCode(StringComparison.Ordinal) ?? 0,
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

            LogKernelCached(_logger, definition.Name, cacheKey);

            // Clean up old entries if cache is too large
            await CleanupCacheIfNeededAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogCacheFailure(_logger, ex, definition.Name);
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
                LogNoCacheMetadata(_logger);
                return;
            }

            var metadataJson = await File.ReadAllTextAsync(metadataFile).ConfigureAwait(false);
            var metadataEntries = JsonSerializer.Deserialize(metadataJson, CudaJsonContext.Default.DictionaryStringKernelCacheMetadata);

            if (metadataEntries == null)
            {
                LogMetadataDeserializationFailed(_logger);
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
                        LogCacheFileMissing(_logger, cacheKey);
                    }
                }
                catch (Exception ex)
                {
                    LogCacheEntryLoadFailed(_logger, ex, cacheKey);
                }
            }

            LogCacheEntriesLoaded(_logger, loadedCount);
        }
        catch (Exception ex)
        {
            LogPersistentCacheLoadFailed(_logger, ex);
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
            await File.WriteAllTextAsync(kernelFile, JsonSerializer.Serialize(metadata, CudaJsonContext.Default.KernelCacheMetadata)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogKernelPersistFailed(_logger, ex, cacheKey);
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

            LogCacheCleanup(_logger, entriesToRemove.Count);
        }
        catch (Exception ex)
        {
            LogCacheCleanupFailed(_logger, ex);
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
            LogCacheEntryDeletionFailed(_logger, ex, cacheKey);
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
            var metadataJson = JsonSerializer.Serialize(metadataSnapshot, CudaJsonContext.Default.DictionaryStringKernelCacheMetadata);
            await File.WriteAllTextAsync(metadataFile, metadataJson).ConfigureAwait(false);

            LogMetadataSaved(_logger, metadataSnapshot.Count);
        }
        catch (Exception ex)
        {
            LogMetadataSaveFailed(_logger, ex);
        }
    }

    /// <summary>
    /// Checks if a cache entry has expired.
    /// </summary>
    private static bool IsCacheEntryExpired(KernelCacheMetadata metadata) => DateTime.UtcNow - metadata.CompileTime > _cacheExpirationTime;

    /// <summary>
    /// Sanitizes a filename for safe disk storage.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string([.. fileName.Where(c => !invalidChars.Contains(c))]);
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

            LogCacheCleared(_logger);
        }
        catch (Exception ex)
        {
            LogDiskCacheClearFailed(_logger, ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
            LogDisposalSaveFailed(_logger, ex);
        }

        _disposed = true;
    }
}