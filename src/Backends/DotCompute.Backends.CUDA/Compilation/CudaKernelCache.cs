// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Advanced CUDA kernel compilation cache with memory-mapped file storage,
/// TTL support, and intelligent invalidation strategies.
/// </summary>
public sealed class CudaKernelCache : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _memoryCache;
    private readonly ConcurrentDictionary<string, MemoryMappedFile> _mappedFiles;
    private readonly ConcurrentDictionary<string, MemoryMappedViewAccessor> _mappedViews;
    private readonly string _cacheDirectory;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _diskOperationsSemaphore;
    private readonly ReaderWriterLockSlim _cacheLock;
    private bool _disposed;

    // Cache configuration
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(15);
    private const int MaxMemoryCacheEntries = 100;
    private const long MaxCacheFileSize = 50 * 1024 * 1024; // 50 MB per file

    // JSON serializer options for cache metadata
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CudaKernelCache(ILogger logger, string? customCacheDirectory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = new ConcurrentDictionary<string, CacheEntry>();
        _mappedFiles = new ConcurrentDictionary<string, MemoryMappedFile>();
        _mappedViews = new ConcurrentDictionary<string, MemoryMappedViewAccessor>();
        _diskOperationsSemaphore = new SemaphoreSlim(4, 4); // Allow up to 4 concurrent disk operations
        _cacheLock = new ReaderWriterLockSlim();

        _cacheDirectory = customCacheDirectory ?? 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "DotCompute", "Cache", "CUDA", "Kernels");

        Directory.CreateDirectory(_cacheDirectory);

        // Start cleanup timer
        _cleanupTimer = new Timer(PerformCleanup, null, CleanupInterval, CleanupInterval);

        // Load existing cache entries
        _ = Task.Run(LoadExistingCacheAsync);

        _logger.LogInformation("CUDA kernel cache initialized with directory: {CacheDirectory}", _cacheDirectory);
    }

    /// <summary>
    /// Gets cache statistics for monitoring and debugging.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        _cacheLock.EnterReadLock();
        try
        {
            var entries = _memoryCache.Values.ToArray();
            return new CacheStatistics
            {
                TotalEntries = entries.Length,
                TotalMemoryUsage = entries.Sum(e => e.PtxData?.Length ?? 0),
                TotalDiskUsage = GetDiskUsage(),
                HitRate = CalculateHitRate(entries),
                AverageAccessTime = CalculateAverageAccessTime(entries),
                OldestEntry = entries.MinBy(e => e.CreatedAt)?.CreatedAt,
                NewestEntry = entries.MaxBy(e => e.CreatedAt)?.CreatedAt,
                ExpiredEntries = entries.Count(e => IsExpired(e)),
                MappedFileCount = _mappedFiles.Count
            };
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Attempts to get a compiled kernel from the cache.
    /// </summary>
    public bool TryGet(string cacheKey, out byte[] ptxData, out CompilationMetadata metadata)
    {
        ptxData = Array.Empty<byte>();
        metadata = default;

        if (string.IsNullOrEmpty(cacheKey))
        {
            return false;
        }

        _cacheLock.EnterUpgradeableReadLock();
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out var entry))
            {
                // Check if entry is expired
                if (IsExpired(entry))
                {
                    _cacheLock.EnterWriteLock();
                    try
                    {
                        RemoveEntryInternal(cacheKey);
                        return false;
                    }
                    finally
                    {
                        _cacheLock.ExitWriteLock();
                    }
                }

                // Update access statistics
                entry.AccessCount++;
                entry.LastAccessed = DateTimeOffset.UtcNow;

                if (entry.PtxData != null)
                {
                    ptxData = entry.PtxData;
                    metadata = entry.Metadata;
                    _logger.LogDebug("Cache hit for kernel: {CacheKey} (access count: {AccessCount})", 
                        cacheKey, entry.AccessCount);
                    return true;
                }

                // Try to load from memory-mapped file
                if (TryLoadFromMappedFile(cacheKey, entry, out ptxData, out metadata))
                {
                    return true;
                }
            }

            _logger.LogDebug("Cache miss for kernel: {CacheKey}", cacheKey);
            return false;
        }
        finally
        {
            _cacheLock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Stores a compiled kernel in the cache with optional TTL.
    /// </summary>
    public async Task<bool> StoreAsync(string cacheKey, byte[] ptxData, CompilationMetadata metadata, 
        TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cacheKey) || ptxData == null || ptxData.Length == 0)
        {
            return false;
        }

        var effectiveTtl = ttl ?? DefaultTtl;
        var expiresAt = DateTimeOffset.UtcNow.Add(effectiveTtl);

        _cacheLock.EnterWriteLock();
        try
        {
            // Check if we need to evict entries to make room
            await EnsureCacheCapacityAsync(cancellationToken).ConfigureAwait(false);

            var entry = new CacheEntry
            {
                CacheKey = cacheKey,
                PtxData = ptxData,
                Metadata = metadata,
                CreatedAt = DateTimeOffset.UtcNow,
                LastAccessed = DateTimeOffset.UtcNow,
                ExpiresAt = expiresAt,
                AccessCount = 1,
                DataSize = ptxData.Length
            };

            _memoryCache[cacheKey] = entry;

            // Store to disk asynchronously
            _ = Task.Run(async () => await PersistToDiskAsync(entry, cancellationToken).ConfigureAwait(false), cancellationToken);

            _logger.LogDebug("Stored kernel in cache: {CacheKey} (size: {Size} bytes, expires: {ExpiresAt})", 
                cacheKey, ptxData.Length, expiresAt);
            return true;
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes a specific entry from the cache.
    /// </summary>
    public bool Remove(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
        {
            return false;
        }

        _cacheLock.EnterWriteLock();
        try
        {
            return RemoveEntryInternal(cacheKey);
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears all cache entries.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _cacheLock.EnterWriteLock();
        try
        {
            // Clean up memory-mapped files
            foreach (var view in _mappedViews.Values)
            {
                view?.Dispose();
            }
            _mappedViews.Clear();

            foreach (var file in _mappedFiles.Values)
            {
                file?.Dispose();
            }
            _mappedFiles.Clear();

            _memoryCache.Clear();
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }

        // Clean up disk cache
        await _diskOperationsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                var files = Directory.GetFiles(_cacheDirectory, "*.cache");
                await Task.WhenAll(files.Select(f => Task.Run(() =>
                {
                    try
                    {
                        File.Delete(f);
                        var metadataFile = Path.ChangeExtension(f, ".metadata");
                        if (File.Exists(metadataFile))
                        {
                            File.Delete(metadataFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cache file: {File}", f);
                    }
                }, cancellationToken))).ConfigureAwait(false);
            }
        }
        finally
        {
            _diskOperationsSemaphore.Release();
        }

        _logger.LogInformation("CUDA kernel cache cleared");
    }

    /// <summary>
    /// Forces immediate cleanup of expired entries.
    /// </summary>
    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var expiredKeys = new List<string>();

        _cacheLock.EnterReadLock();
        try
        {
            expiredKeys.AddRange(_memoryCache
                .Where(kvp => IsExpired(kvp.Value))
                .Select(kvp => kvp.Key));
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }

        if (expiredKeys.Count == 0)
        {
            return 0;
        }

        _cacheLock.EnterWriteLock();
        try
        {
            foreach (var key in expiredKeys)
            {
                RemoveEntryInternal(key);
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }

        _logger.LogInformation("Cleaned up {ExpiredCount} expired cache entries", expiredKeys.Count);
        return expiredKeys.Count;
    }

    /// <summary>
    /// Generates a cache key from kernel source and compilation options.
    /// </summary>
    public static string GenerateCacheKey(string kernelName, byte[] sourceCode, CompilationOptions? options = null)
    {
        using var sha256 = SHA256.Create();
        var sourceHash = sha256.ComputeHash(sourceCode);
        
        var keyBuilder = new StringBuilder();
        keyBuilder.Append(kernelName).Append('_');
        keyBuilder.Append(Convert.ToHexString(sourceHash));

        if (options != null)
        {
            keyBuilder.Append('_').Append(options.OptimizationLevel);
            keyBuilder.Append('_').Append(options.EnableDebugInfo ? "debug" : "release");
            
            if (options.AdditionalFlags?.Length > 0)
            {
                var flagsHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(string.Join(",", options.AdditionalFlags)));
                keyBuilder.Append('_').Append(Convert.ToHexString(flagsHash)[..8]);
            }
        }

        return keyBuilder.ToString();
    }

    #region Private Methods

    private bool RemoveEntryInternal(string cacheKey)
    {
        if (!_memoryCache.TryRemove(cacheKey, out var entry))
        {
            return false;
        }

        // Clean up memory-mapped file resources
        if (_mappedViews.TryRemove(cacheKey, out var view))
        {
            view?.Dispose();
        }

        if (_mappedFiles.TryRemove(cacheKey, out var file))
        {
            file?.Dispose();
        }

        // Schedule disk cleanup
        _ = Task.Run(() => CleanupDiskEntry(cacheKey));

        return true;
    }

    private async Task CleanupDiskEntry(string cacheKey)
    {
        try
        {
            var cacheFile = Path.Combine(_cacheDirectory, $"{SanitizeFileName(cacheKey)}.cache");
            var metadataFile = Path.ChangeExtension(cacheFile, ".metadata");

            if (File.Exists(cacheFile))
            {
                File.Delete(cacheFile);
            }

            if (File.Exists(metadataFile))
            {
                File.Delete(metadataFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup disk entry for cache key: {CacheKey}", cacheKey);
        }
    }

    private bool TryLoadFromMappedFile(string cacheKey, CacheEntry entry, out byte[] ptxData, out CompilationMetadata metadata)
    {
        ptxData = Array.Empty<byte>();
        metadata = default;

        try
        {
            if (_mappedViews.TryGetValue(cacheKey, out var view) && view != null)
            {
                var dataSize = view.ReadInt32(0);
                if (dataSize > 0 && dataSize <= MaxCacheFileSize)
                {
                    ptxData = new byte[dataSize];
                    view.ReadArray(4, ptxData, 0, dataSize);
                    metadata = entry.Metadata;

                    _logger.LogDebug("Loaded kernel from memory-mapped file: {CacheKey}", cacheKey);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load from memory-mapped file: {CacheKey}", cacheKey);
        }

        return false;
    }

    private async Task PersistToDiskAsync(CacheEntry entry, CancellationToken cancellationToken)
    {
        if (entry.PtxData == null || entry.DataSize > MaxCacheFileSize)
        {
            return;
        }

        await _diskOperationsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var fileName = SanitizeFileName(entry.CacheKey);
            var cacheFile = Path.Combine(_cacheDirectory, $"{fileName}.cache");
            var metadataFile = Path.ChangeExtension(cacheFile, ".metadata");

            // Create memory-mapped file for efficient access
            var mappedFile = MemoryMappedFile.CreateNew(entry.CacheKey, entry.DataSize + 4);
            var view = mappedFile.CreateViewAccessor(0, entry.DataSize + 4);

            view.Write(0, entry.DataSize);
            view.WriteArray(4, entry.PtxData, 0, entry.DataSize);

            _mappedFiles[entry.CacheKey] = mappedFile;
            _mappedViews[entry.CacheKey] = view;

            // Also persist to physical file for durability
            await File.WriteAllBytesAsync(cacheFile, entry.PtxData, cancellationToken).ConfigureAwait(false);

            // Save metadata
            var metadataJson = JsonSerializer.Serialize(new CacheMetadata
            {
                CacheKey = entry.CacheKey,
                CreatedAt = entry.CreatedAt,
                ExpiresAt = entry.ExpiresAt,
                AccessCount = entry.AccessCount,
                DataSize = entry.DataSize,
                Metadata = entry.Metadata
            }, JsonOptions);

            await File.WriteAllTextAsync(metadataFile, metadataJson, cancellationToken).ConfigureAwait(false);

            // Clear PTX data from memory to save space (it's now in memory-mapped file)
            entry.PtxData = null;

            _logger.LogTrace("Persisted kernel to disk: {CacheKey}", entry.CacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist kernel to disk: {CacheKey}", entry.CacheKey);
        }
        finally
        {
            _diskOperationsSemaphore.Release();
        }
    }

    private async Task LoadExistingCacheAsync()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                return;
            }

            var metadataFiles = Directory.GetFiles(_cacheDirectory, "*.metadata");
            var loadedCount = 0;

            foreach (var metadataFile in metadataFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(metadataFile).ConfigureAwait(false);
                    var metadata = JsonSerializer.Deserialize<CacheMetadata>(json, JsonOptions);

                    if (metadata == null || IsMetadataExpired(metadata))
                    {
                        // Clean up expired entries
                        File.Delete(metadataFile);
                        var associatedCacheFile = Path.ChangeExtension(metadataFile, ".cache");
                        if (File.Exists(associatedCacheFile))
                        {
                            File.Delete(associatedCacheFile);
                        }
                        continue;
                    }

                    var cacheFile = Path.ChangeExtension(metadataFile, ".cache");
                    if (!File.Exists(cacheFile))
                    {
                        continue;
                    }

                    var entry = new CacheEntry
                    {
                        CacheKey = metadata.CacheKey,
                        PtxData = null, // Will be loaded from memory-mapped file on demand
                        Metadata = metadata.Metadata,
                        CreatedAt = metadata.CreatedAt,
                        LastAccessed = metadata.CreatedAt,
                        ExpiresAt = metadata.ExpiresAt,
                        AccessCount = metadata.AccessCount,
                        DataSize = metadata.DataSize
                    };

                    _memoryCache[metadata.CacheKey] = entry;
                    loadedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load cache entry from: {MetadataFile}", metadataFile);
                }
            }

            if (loadedCount > 0)
            {
                _logger.LogInformation("Loaded {LoadedCount} cached kernels from disk", loadedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load existing cache entries");
        }
    }

    private async Task EnsureCacheCapacityAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.Count < MaxMemoryCacheEntries)
        {
            return;
        }

        // Evict least recently used entries
        var entriesToEvict = _memoryCache.Values
            .OrderBy(e => e.LastAccessed)
            .Take(_memoryCache.Count - MaxMemoryCacheEntries + 10) // Evict extra to avoid frequent cleanup
            .Select(e => e.CacheKey)
            .ToArray();

        foreach (var key in entriesToEvict)
        {
            RemoveEntryInternal(key);
        }

        _logger.LogDebug("Evicted {EvictedCount} cache entries to maintain capacity", entriesToEvict.Length);
    }

    private void PerformCleanup(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _ = Task.Run(async () => await CleanupExpiredAsync().ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
    }

    private static bool IsExpired(CacheEntry entry) => 
        DateTimeOffset.UtcNow > entry.ExpiresAt;

    private static bool IsMetadataExpired(CacheMetadata metadata) => 
        DateTimeOffset.UtcNow > metadata.ExpiresAt;

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(fileName.Select(c => invalidChars.Contains(c) ? '_' : c));
    }

    private long GetDiskUsage()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                return 0;
            }

            return Directory.GetFiles(_cacheDirectory, "*.cache")
                .Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    private static double CalculateHitRate(CacheEntry[] entries)
    {
        if (entries.Length == 0)
        {
            return 0;
        }

        var totalAccesses = entries.Sum(e => e.AccessCount);
        return totalAccesses > entries.Length 
            ? (double)(totalAccesses - entries.Length) / totalAccesses 
            : 0;
    }

    private static double CalculateAverageAccessTime(CacheEntry[] entries)
    {
        if (entries.Length == 0)
        {
            return 0;
        }

        var totalTime = entries.Sum(e => (DateTimeOffset.UtcNow - e.CreatedAt).TotalMilliseconds);
        return totalTime / entries.Length;
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _cleanupTimer?.Dispose();
        
        _cacheLock.EnterWriteLock();
        try
        {
            foreach (var view in _mappedViews.Values)
            {
                view?.Dispose();
            }
            _mappedViews.Clear();

            foreach (var file in _mappedFiles.Values)
            {
                file?.Dispose();
            }
            _mappedFiles.Clear();

            _memoryCache.Clear();
        }
        finally
        {
            _cacheLock.ExitWriteLock();
            _cacheLock.Dispose();
        }

        _diskOperationsSemaphore?.Dispose();

        _logger.LogInformation("CUDA kernel cache disposed");
    }
}

/// <summary>
/// Represents a cache entry with metadata and access statistics.
/// </summary>
internal class CacheEntry
{
    public required string CacheKey { get; set; }
    public byte[]? PtxData { get; set; }
    public CompilationMetadata Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastAccessed { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public long AccessCount { get; set; }
    public int DataSize { get; set; }
}

/// <summary>
/// Serializable cache metadata for disk storage.
/// </summary>
public class CacheMetadata
{
    public required string CacheKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public long AccessCount { get; set; }
    public int DataSize { get; set; }
    public CompilationMetadata Metadata { get; set; }
}

/// <summary>
/// Cache performance statistics.
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public long TotalMemoryUsage { get; set; }
    public long TotalDiskUsage { get; set; }
    public double HitRate { get; set; }
    public double AverageAccessTime { get; set; }
    public DateTimeOffset? OldestEntry { get; set; }
    public DateTimeOffset? NewestEntry { get; set; }
    public int ExpiredEntries { get; set; }
    public int MappedFileCount { get; set; }
}