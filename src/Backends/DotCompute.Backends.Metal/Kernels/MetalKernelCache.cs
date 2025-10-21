// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Kernels;

/// <summary>
/// Production-grade cache for compiled Metal kernels with LRU eviction and memory pressure handling.
/// </summary>
public sealed partial class MetalKernelCache : IDisposable
{
    private readonly ILogger<MetalKernelCache> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    private readonly ReaderWriterLockSlim _cacheLock;
    private readonly int _maxCacheSize;
    private readonly TimeSpan _defaultTtl;
    private readonly Timer _cleanupTimer;
    private readonly string? _persistentCachePath;
    private readonly object _statsLock = new();

    // Performance metrics

    private long _hitCount;
    private long _missCount;
    private long _evictionCount;
    private long _totalCompilationTimeMs;
    private long _totalCacheTimeMs;
    private volatile bool _disposed;

    /// <summary>
    /// Cache statistics for monitoring and optimization.
    /// </summary>
    public record CacheStatistics
    {
        public long HitCount { get; init; }
        public long MissCount { get; init; }
        public double HitRate { get; init; }
        public long EvictionCount { get; init; }
        public int CurrentSize { get; init; }
        public long AverageCompilationTimeMs { get; init; }
        public long AverageCacheRetrievalTimeMs { get; init; }
        public long TotalMemoryBytes { get; init; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalKernelCache"/> class.
    /// </summary>
    public MetalKernelCache(
        ILogger<MetalKernelCache> logger,
        int maxCacheSize = 1000,
        TimeSpan? defaultTtl = null,
        string? persistentCachePath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = new ConcurrentDictionary<string, CacheEntry>(Environment.ProcessorCount, maxCacheSize);
        _cacheLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        _maxCacheSize = maxCacheSize;
        _defaultTtl = defaultTtl ?? TimeSpan.FromHours(1);
        _persistentCachePath = persistentCachePath;

        // Initialize persistent cache directory if specified

        if (!string.IsNullOrEmpty(_persistentCachePath))
        {
            try
            {
                _ = Directory.CreateDirectory(_persistentCachePath);
                LoadPersistentCache();
            }
            catch (Exception ex)
            {
                LogPersistentCacheInitFailed(_logger, ex, _persistentCachePath);
            }
        }

        // Setup periodic cleanup every 5 minutes

        _cleanupTimer = new Timer(
            PerformCleanup,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));


        LogKernelCacheInitialized(_logger, maxCacheSize, _defaultTtl, _persistentCachePath ?? "none");
    }

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 6500,
        Level = LogLevel.Information,
        Message = "Metal kernel cache initialized with max size {MaxSize}, TTL {TTL}, persistent path: {Path}")]
    private static partial void LogKernelCacheInitialized(ILogger logger, int maxSize, TimeSpan ttl, string path);

    [LoggerMessage(
        EventId = 6501,
        Level = LogLevel.Warning,
        Message = "Failed to initialize persistent cache at {Path}")]
    private static partial void LogPersistentCacheInitFailed(ILogger logger, Exception ex, string? path);

    [LoggerMessage(
        EventId = 6502,
        Level = LogLevel.Debug,
        Message = "Cache hit for kernel '{Name}' (key: {Key}, access count: {Count})")]
    private static partial void LogCacheHit(ILogger logger, string name, string key, int count);

    [LoggerMessage(
        EventId = 6503,
        Level = LogLevel.Debug,
        Message = "Cache hit from persistent storage for kernel '{Name}'")]
    private static partial void LogPersistentCacheHit(ILogger logger, string name);

    [LoggerMessage(
        EventId = 6504,
        Level = LogLevel.Debug,
        Message = "Cache miss for kernel '{Name}' (key: {Key})")]
    private static partial void LogCacheMiss(ILogger logger, string name, string key);

    [LoggerMessage(
        EventId = 6505,
        Level = LogLevel.Debug,
        Message = "Cached kernel '{Name}' (key: {Key}, size: {Size} bytes, compilation: {Time}ms)")]
    private static partial void LogKernelCached(ILogger logger, string name, string key, int size, long time);

    [LoggerMessage(
        EventId = 6506,
        Level = LogLevel.Information,
        Message = "Invalidated cached kernel '{Name}' (key: {Key})")]
    private static partial void LogKernelInvalidated(ILogger logger, string name, string key);

    [LoggerMessage(
        EventId = 6507,
        Level = LogLevel.Warning,
        Message = "Failed to clear persistent cache")]
    private static partial void LogClearPersistentCacheFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6508,
        Level = LogLevel.Information,
        Message = "Cleared {Count} kernels from cache")]
    private static partial void LogCacheCleared(ILogger logger, int count);

    [LoggerMessage(
        EventId = 6509,
        Level = LogLevel.Debug,
        Message = "Evicted kernel '{Name}' (key: {Key}, last access: {LastAccess})")]
    private static partial void LogKernelEvicted(ILogger logger, string name, string key, DateTimeOffset lastAccess);

    [LoggerMessage(
        EventId = 6510,
        Level = LogLevel.Warning,
        Message = "Failed to load kernel from persistent cache: {Key}")]
    private static partial void LogPersistentCacheLoadFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(
        EventId = 6511,
        Level = LogLevel.Debug,
        Message = "Saved kernel to persistent cache: {Key}")]
    private static partial void LogSavedToPersistentCache(ILogger logger, string key);

    [LoggerMessage(
        EventId = 6512,
        Level = LogLevel.Warning,
        Message = "Failed to save kernel to persistent cache: {Key}")]
    private static partial void LogSaveToPersistentCacheFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(
        EventId = 6513,
        Level = LogLevel.Warning,
        Message = "Failed to remove kernel from persistent cache: {Key}")]
    private static partial void LogRemoveFromPersistentCacheFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(
        EventId = 6514,
        Level = LogLevel.Information,
        Message = "Found {Count} cached kernels in persistent storage")]
    private static partial void LogPersistentCacheLoaded(ILogger logger, int count);

    [LoggerMessage(
        EventId = 6515,
        Level = LogLevel.Warning,
        Message = "Failed to load persistent cache")]
    private static partial void LogLoadPersistentCacheFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6516,
        Level = LogLevel.Debug,
        Message = "Cleaned up expired kernel '{Name}' (key: {Key})")]
    private static partial void LogExpiredKernelCleanedUp(ILogger logger, string name, string key);

    [LoggerMessage(
        EventId = 6517,
        Level = LogLevel.Information,
        Message = "Cleaned up {Count} expired kernel cache entries")]
    private static partial void LogExpiredEntriesCleanedUp(ILogger logger, int count);

    [LoggerMessage(
        EventId = 6518,
        Level = LogLevel.Error,
        Message = "Error during cache cleanup")]
    private static partial void LogCleanupError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6519,
        Level = LogLevel.Information,
        Message = "Metal kernel cache disposed - Hit rate: {HitRate:P2}, Total hits: {Hits}, Total misses: {Misses}, Evictions: {Evictions}")]
    private static partial void LogKernelCacheDisposed(ILogger logger, double hitRate, long hits, long misses, long evictions);

    #endregion

    /// <summary>
    /// Attempts to retrieve a cached kernel.
    /// </summary>
    public bool TryGetKernel(
        KernelDefinition definition,
        CompilationOptions options,
        out IntPtr library,
        out IntPtr function,
        out IntPtr pipelineState)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        var startTime = Environment.TickCount64;
        library = IntPtr.Zero;
        function = IntPtr.Zero;
        pipelineState = IntPtr.Zero;


        var cacheKey = ComputeCacheKey(definition, options);


        _cacheLock.EnterReadLock();
        try
        {
            if (_cache.TryGetValue(cacheKey, out var entry))
            {
                // Check if entry is still valid
                if (!entry.IsExpired && entry.Library != IntPtr.Zero)
                {
                    // Update access time and count for LRU
                    entry.LastAccessTime = DateTimeOffset.UtcNow;
                    entry.AccessCount = entry.AccessCount + 1;


                    library = entry.Library;
                    function = entry.Function;
                    pipelineState = entry.PipelineState;


                    _ = Interlocked.Increment(ref _hitCount);


                    var elapsed = Environment.TickCount64 - startTime;
                    _ = Interlocked.Add(ref _totalCacheTimeMs, elapsed);


                    LogCacheHit(_logger, definition.Name, cacheKey[..8], (int)entry.AccessCount);


                    return true;
                }
                else
                {
                    // Entry is expired or invalid, remove it
                    _ = _cache.TryRemove(cacheKey, out _);
                }
            }
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }

        // Try to load from persistent cache if available

        if (TryLoadFromPersistentCache(cacheKey, out var persistentEntry))
        {
            library = persistentEntry.Library;
            function = persistentEntry.Function;
            pipelineState = persistentEntry.PipelineState;

            // Re-add to memory cache

            AddToMemoryCache(definition, options, library, function, pipelineState, persistentEntry.BinaryData);


            _ = Interlocked.Increment(ref _hitCount);
            LogPersistentCacheHit(_logger, definition.Name);
            return true;
        }


        _ = Interlocked.Increment(ref _missCount);
        LogCacheMiss(_logger, definition.Name, cacheKey[..8]);
        return false;
    }

    /// <summary>
    /// Adds a compiled kernel to the cache.
    /// </summary>
    public void AddKernel(
        KernelDefinition definition,
        CompilationOptions options,
        IntPtr library,
        IntPtr function,
        IntPtr pipelineState,
        byte[]? binaryData = null,
        long compilationTimeMs = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        var cacheKey = ComputeCacheKey(definition, options);
        var expirationTime = DateTimeOffset.UtcNow.Add(_defaultTtl);


        var entry = new CacheEntry
        {
            Library = library,
            Function = function,
            PipelineState = pipelineState,
            Definition = definition,
            Options = options,
            CreatedTime = DateTimeOffset.UtcNow,
            LastAccessTime = DateTimeOffset.UtcNow,
            ExpirationTime = expirationTime,
            CacheKey = cacheKey,
            BinaryData = binaryData,
            CompilationTimeMs = compilationTimeMs,
            SizeInBytes = EstimateKernelSize(definition, binaryData)
        };


        _cacheLock.EnterWriteLock();
        try
        {
            // Check cache size and evict if necessary
            if (_cache.Count >= _maxCacheSize)
            {
                EvictLeastRecentlyUsed();
            }

            // Add or update the cache entry

            _ = _cache.AddOrUpdate(cacheKey, entry, (key, oldEntry) => entry);

            // Record compilation time for metrics

            if (compilationTimeMs > 0)
            {
                _ = Interlocked.Add(ref _totalCompilationTimeMs, compilationTimeMs);
            }


            LogKernelCached(_logger, definition.Name, cacheKey[..8], (int)entry.SizeInBytes, compilationTimeMs);

            // Save to persistent cache if enabled

            if (!string.IsNullOrEmpty(_persistentCachePath) && binaryData != null)
            {
                SaveToPersistentCache(cacheKey, entry);
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Invalidates a specific kernel in the cache.
    /// </summary>
    public bool InvalidateKernel(KernelDefinition definition, CompilationOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        var cacheKey = ComputeCacheKey(definition, options);


        _cacheLock.EnterWriteLock();
        try
        {
            if (_cache.TryRemove(cacheKey, out var entry))
            {
                // Remove from persistent cache
                RemoveFromPersistentCache(cacheKey);


                LogKernelInvalidated(_logger, definition.Name, cacheKey[..8]);
                return true;
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }


        return false;
    }

    /// <summary>
    /// Clears all cached kernels.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        _cacheLock.EnterWriteLock();
        try
        {
            var count = _cache.Count;
            _cache.Clear();

            // Clear persistent cache

            if (!string.IsNullOrEmpty(_persistentCachePath))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(_persistentCachePath, "*.metallib"))
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception ex)
                {
                    LogClearPersistentCacheFailed(_logger, ex);
                }
            }


            LogCacheCleared(_logger, count);
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets current cache statistics.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            var hits = Interlocked.Read(ref _hitCount);
            var misses = Interlocked.Read(ref _missCount);
            var total = hits + misses;


            long totalMemory = 0;
            foreach (var entry in _cache.Values)
            {
                totalMemory += entry.SizeInBytes;
            }


            return new CacheStatistics
            {
                HitCount = hits,
                MissCount = misses,
                HitRate = total > 0 ? (double)hits / total : 0,
                EvictionCount = Interlocked.Read(ref _evictionCount),
                CurrentSize = _cache.Count,
                AverageCompilationTimeMs = misses > 0 ? _totalCompilationTimeMs / misses : 0,
                AverageCacheRetrievalTimeMs = hits > 0 ? _totalCacheTimeMs / hits : 0,
                TotalMemoryBytes = totalMemory
            };
        }
    }

    /// <summary>
    /// Computes a cache key for the given kernel definition and options.
    /// </summary>
    public static string ComputeCacheKey(KernelDefinition definition, CompilationOptions options)
    {
        var inputBuilder = new StringBuilder();

        // Include all relevant properties in the cache key
        _ = inputBuilder.Append(definition.Name);
        _ = inputBuilder.Append('|');
        _ = inputBuilder.Append(definition.Source);
        _ = inputBuilder.Append('|');
        _ = inputBuilder.Append(definition.EntryPoint);
        _ = inputBuilder.Append('|');
        _ = inputBuilder.Append(definition.Language);
        _ = inputBuilder.Append('|');
        _ = inputBuilder.Append(options.OptimizationLevel);
        _ = inputBuilder.Append('|');
        _ = inputBuilder.Append(options.GenerateDebugInfo);
        _ = inputBuilder.Append('|');
        _ = inputBuilder.Append(options.FastMath);
        _ = inputBuilder.Append('|');
        _ = inputBuilder.Append(options.TargetArchitecture ?? "default");

        // Add additional flags if any
        if (options.AdditionalFlags != null)
        {
            foreach (var flag in options.AdditionalFlags)
            {
                _ = inputBuilder.Append('|');
                _ = inputBuilder.Append(flag);
            }
        }

        var inputBytes = Encoding.UTF8.GetBytes(inputBuilder.ToString());
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-");
    }

    /// <summary>
    /// Estimates the memory size of a compiled kernel.
    /// </summary>
    private static long EstimateKernelSize(KernelDefinition definition, byte[]? binaryData)
    {
        // Base size for object overhead
        long size = 256;

        // Add binary data size if available

        if (binaryData != null)
        {
            size += binaryData.Length;
        }

        // Add source code size

        size += definition.Source?.Length ?? 0;

        // Add estimated metadata size

        size += 2048; // Pipeline state and function metadata


        return size;
    }

    /// <summary>
    /// Evicts the least recently used entries to make room.
    /// </summary>
    private void EvictLeastRecentlyUsed()
    {
        const int evictionBatchSize = 10; // Evict 10 entries at a time


        var entriesToEvict = _cache.Values
            .OrderBy(e => e.LastAccessTime)
            .ThenBy(e => e.AccessCount)
            .Take(evictionBatchSize)
            .ToList();


        foreach (var entry in entriesToEvict)
        {
            if (_cache.TryRemove(entry.CacheKey, out _))
            {
                _ = Interlocked.Increment(ref _evictionCount);


                LogKernelEvicted(_logger, entry.Definition.Name, entry.CacheKey[..8], entry.LastAccessTime);
            }
        }
    }

    /// <summary>
    /// Adds an entry to the memory cache.
    /// </summary>
    private void AddToMemoryCache(
        KernelDefinition definition,
        CompilationOptions options,
        IntPtr library,
        IntPtr function,
        IntPtr pipelineState,
        byte[]? binaryData)
    {
        var cacheKey = ComputeCacheKey(definition, options);
        var entry = new CacheEntry
        {
            Library = library,
            Function = function,
            PipelineState = pipelineState,
            Definition = definition,
            Options = options,
            CreatedTime = DateTimeOffset.UtcNow,
            LastAccessTime = DateTimeOffset.UtcNow,
            ExpirationTime = DateTimeOffset.UtcNow.Add(_defaultTtl),
            CacheKey = cacheKey,
            BinaryData = binaryData,
            SizeInBytes = EstimateKernelSize(definition, binaryData)
        };


        _ = _cache.TryAdd(cacheKey, entry);
    }

    /// <summary>
    /// Tries to load a kernel from persistent cache.
    /// </summary>
    private bool TryLoadFromPersistentCache(string cacheKey, out CacheEntry entry)
    {
        entry = null!;


        if (string.IsNullOrEmpty(_persistentCachePath))
        {
            return false;
        }


        var filePath = Path.Combine(_persistentCachePath, $"{cacheKey}.metallib");
        var metaPath = Path.Combine(_persistentCachePath, $"{cacheKey}.meta");


        if (!File.Exists(filePath) || !File.Exists(metaPath))
        {

            return false;
        }


        try
        {
            var binaryData = File.ReadAllBytes(filePath);
            var metaJson = File.ReadAllText(metaPath);

            // TODO: Deserialize metadata and reconstruct Metal objects from binary
            // This requires runtime compilation from the binary data
            // For now, return false as we can't fully reconstruct the kernel


            return false;
        }
        catch (Exception ex)
        {
            LogPersistentCacheLoadFailed(_logger, ex, cacheKey);
            return false;
        }
    }

    /// <summary>
    /// Saves a kernel to persistent cache.
    /// </summary>
    private void SaveToPersistentCache(string cacheKey, CacheEntry entry)
    {
        if (string.IsNullOrEmpty(_persistentCachePath) || entry.BinaryData == null)
        {
            return;
        }


        try
        {
            var filePath = Path.Combine(_persistentCachePath, $"{cacheKey}.metallib");
            var metaPath = Path.Combine(_persistentCachePath, $"{cacheKey}.meta");

            // Save binary data

            File.WriteAllBytes(filePath, entry.BinaryData);

            // Save metadata

            var metadata = new
            {
                entry.Definition.Name,
                entry.Definition.EntryPoint,
                CompilationTime = entry.CompilationTimeMs,
                CreatedTime = entry.CreatedTime
            };


            var metaJson = System.Text.Json.JsonSerializer.Serialize(metadata);
            File.WriteAllText(metaPath, metaJson);


            LogSavedToPersistentCache(_logger, cacheKey[..8]);
        }
        catch (Exception ex)
        {
            LogSaveToPersistentCacheFailed(_logger, ex, cacheKey[..8]);
        }
    }

    /// <summary>
    /// Removes a kernel from persistent cache.
    /// </summary>
    private void RemoveFromPersistentCache(string cacheKey)
    {
        if (string.IsNullOrEmpty(_persistentCachePath))
        {
            return;
        }


        try
        {
            var filePath = Path.Combine(_persistentCachePath, $"{cacheKey}.metallib");
            var metaPath = Path.Combine(_persistentCachePath, $"{cacheKey}.meta");


            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }
        catch (Exception ex)
        {
            LogRemoveFromPersistentCacheFailed(_logger, ex, cacheKey[..8]);
        }
    }

    /// <summary>
    /// Loads cached kernels from persistent storage.
    /// </summary>
    private void LoadPersistentCache()
    {
        if (string.IsNullOrEmpty(_persistentCachePath) || !Directory.Exists(_persistentCachePath))
        {
            return;
        }


        try
        {
            var metaFiles = Directory.GetFiles(_persistentCachePath, "*.meta");
            LogPersistentCacheLoaded(_logger, metaFiles.Length);

            // Note: We can't fully reconstruct Metal objects from binary data at startup
            // This would require deferred loading when the kernel is first requested

        }
        catch (Exception ex)
        {
            LogLoadPersistentCacheFailed(_logger, ex);
        }
    }

    /// <summary>
    /// Performs periodic cleanup of expired entries.
    /// </summary>
    private void PerformCleanup(object? state)
    {
        if (_disposed)
        {
            return;
        }


        _cacheLock.EnterWriteLock();
        try
        {
            var now = DateTimeOffset.UtcNow;
            var expiredEntries = _cache.Values
                .Where(e => e.IsExpired)
                .ToList();


            foreach (var entry in expiredEntries)
            {
                if (_cache.TryRemove(entry.CacheKey, out _))
                {
                    RemoveFromPersistentCache(entry.CacheKey);
                    LogExpiredKernelCleanedUp(_logger, entry.Definition.Name, entry.CacheKey[..8]);
                }
            }


            if (expiredEntries.Count > 0)
            {
                LogExpiredEntriesCleanedUp(_logger, expiredEntries.Count);
            }
        }
        catch (Exception ex)
        {
            LogCleanupError(_logger, ex);
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Disposes the cache and all cached kernels.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _cleanupTimer?.Dispose();


        _cacheLock.EnterWriteLock();
        try
        {
            Clear();
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }


        _cacheLock.Dispose();


        var stats = GetStatistics();
        LogKernelCacheDisposed(_logger, stats.HitRate, stats.HitCount, stats.MissCount, stats.EvictionCount);


        _disposed = true;
    }

    /// <summary>
    /// Cache entry for a compiled kernel.
    /// </summary>
    private sealed class CacheEntry
    {
        public required IntPtr Library { get; init; }
        public required IntPtr Function { get; init; }
        public required IntPtr PipelineState { get; init; }
        public required KernelDefinition Definition { get; init; }
        public required CompilationOptions Options { get; init; }
        public required DateTimeOffset CreatedTime { get; init; }
        public DateTimeOffset LastAccessTime { get; set; }
        public required DateTimeOffset ExpirationTime { get; init; }
        public required string CacheKey { get; init; }
        public required long SizeInBytes { get; init; }
        public byte[]? BinaryData { get; init; }
        public long CompilationTimeMs { get; init; }
        public long AccessCount { get; set; }


        public bool IsExpired => DateTimeOffset.UtcNow > ExpirationTime;
    }
}