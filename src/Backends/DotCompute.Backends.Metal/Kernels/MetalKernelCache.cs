// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Kernels;

/// <summary>
/// Production-grade cache for compiled Metal kernels with LRU eviction and memory pressure handling.
/// </summary>
public sealed class MetalKernelCache : IDisposable
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
                Directory.CreateDirectory(_persistentCachePath);
                LoadPersistentCache();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize persistent cache at {Path}", _persistentCachePath);
            }
        }
        
        // Setup periodic cleanup every 5 minutes
        _cleanupTimer = new Timer(
            PerformCleanup,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
        
        _logger.LogInformation(
            "Metal kernel cache initialized with max size {MaxSize}, TTL {TTL}, persistent path: {Path}",
            maxCacheSize, _defaultTtl, _persistentCachePath ?? "none");
    }

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
                    
                    Interlocked.Increment(ref _hitCount);
                    
                    var elapsed = Environment.TickCount64 - startTime;
                    Interlocked.Add(ref _totalCacheTimeMs, elapsed);
                    
                    _logger.LogDebug(
                        "Cache hit for kernel '{Name}' (key: {Key}, access count: {Count})",
                        definition.Name, cacheKey[..8], entry.AccessCount);
                    
                    return true;
                }
                else
                {
                    // Entry is expired or invalid, remove it
                    _cache.TryRemove(cacheKey, out _);
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
            
            Interlocked.Increment(ref _hitCount);
            _logger.LogDebug("Cache hit from persistent storage for kernel '{Name}'", definition.Name);
            return true;
        }
        
        Interlocked.Increment(ref _missCount);
        _logger.LogDebug("Cache miss for kernel '{Name}' (key: {Key})", definition.Name, cacheKey[..8]);
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
            _cache.AddOrUpdate(cacheKey, entry, (key, oldEntry) => entry);
            
            // Record compilation time for metrics
            if (compilationTimeMs > 0)
            {
                Interlocked.Add(ref _totalCompilationTimeMs, compilationTimeMs);
            }
            
            _logger.LogDebug(
                "Cached kernel '{Name}' (key: {Key}, size: {Size} bytes, compilation: {Time}ms)",
                definition.Name, cacheKey[..8], entry.SizeInBytes, compilationTimeMs);
            
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
                
                _logger.LogInformation("Invalidated cached kernel '{Name}' (key: {Key})", 
                    definition.Name, cacheKey[..8]);
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
                    _logger.LogWarning(ex, "Failed to clear persistent cache");
                }
            }
            
            _logger.LogInformation("Cleared {Count} kernels from cache", count);
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
        using var sha256 = SHA256.Create();
        var inputBuilder = new StringBuilder();
        
        // Include all relevant properties in the cache key
        inputBuilder.Append(definition.Name);
        inputBuilder.Append('|');
        inputBuilder.Append(definition.Source);
        inputBuilder.Append('|');
        inputBuilder.Append(definition.EntryPoint);
        inputBuilder.Append('|');
        inputBuilder.Append(definition.Language);
        inputBuilder.Append('|');
        inputBuilder.Append(options.OptimizationLevel);
        inputBuilder.Append('|');
        inputBuilder.Append(options.GenerateDebugInfo);
        inputBuilder.Append('|');
        inputBuilder.Append(options.FastMath);
        inputBuilder.Append('|');
        inputBuilder.Append(options.TargetArchitecture ?? "default");
        
        // Add additional flags if any
        if (options.AdditionalFlags != null)
        {
            foreach (var flag in options.AdditionalFlags)
            {
                inputBuilder.Append('|');
                inputBuilder.Append(flag);
            }
        }
        
        var inputBytes = Encoding.UTF8.GetBytes(inputBuilder.ToString());
        var hashBytes = sha256.ComputeHash(inputBytes);
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
                Interlocked.Increment(ref _evictionCount);
                
                _logger.LogDebug(
                    "Evicted kernel '{Name}' (key: {Key}, last access: {LastAccess})",
                    entry.Definition.Name, 
                    entry.CacheKey[..8],
                    entry.LastAccessTime);
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
        
        _cache.TryAdd(cacheKey, entry);
    }

    /// <summary>
    /// Tries to load a kernel from persistent cache.
    /// </summary>
    private bool TryLoadFromPersistentCache(string cacheKey, out CacheEntry entry)
    {
        entry = null!;
        
        if (string.IsNullOrEmpty(_persistentCachePath))
            return false;
        
        var filePath = Path.Combine(_persistentCachePath, $"{cacheKey}.metallib");
        var metaPath = Path.Combine(_persistentCachePath, $"{cacheKey}.meta");
        
        if (!File.Exists(filePath) || !File.Exists(metaPath))
            return false;
        
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
            _logger.LogWarning(ex, "Failed to load kernel from persistent cache: {Key}", cacheKey);
            return false;
        }
    }

    /// <summary>
    /// Saves a kernel to persistent cache.
    /// </summary>
    private void SaveToPersistentCache(string cacheKey, CacheEntry entry)
    {
        if (string.IsNullOrEmpty(_persistentCachePath) || entry.BinaryData == null)
            return;
        
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
            
            _logger.LogDebug("Saved kernel to persistent cache: {Key}", cacheKey[..8]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save kernel to persistent cache: {Key}", cacheKey[..8]);
        }
    }

    /// <summary>
    /// Removes a kernel from persistent cache.
    /// </summary>
    private void RemoveFromPersistentCache(string cacheKey)
    {
        if (string.IsNullOrEmpty(_persistentCachePath))
            return;
        
        try
        {
            var filePath = Path.Combine(_persistentCachePath, $"{cacheKey}.metallib");
            var metaPath = Path.Combine(_persistentCachePath, $"{cacheKey}.meta");
            
            if (File.Exists(filePath))
                File.Delete(filePath);
            
            if (File.Exists(metaPath))
                File.Delete(metaPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove kernel from persistent cache: {Key}", cacheKey[..8]);
        }
    }

    /// <summary>
    /// Loads cached kernels from persistent storage.
    /// </summary>
    private void LoadPersistentCache()
    {
        if (string.IsNullOrEmpty(_persistentCachePath) || !Directory.Exists(_persistentCachePath))
            return;
        
        try
        {
            var metaFiles = Directory.GetFiles(_persistentCachePath, "*.meta");
            _logger.LogInformation("Found {Count} cached kernels in persistent storage", metaFiles.Length);
            
            // Note: We can't fully reconstruct Metal objects from binary data at startup
            // This would require deferred loading when the kernel is first requested
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load persistent cache");
        }
    }

    /// <summary>
    /// Performs periodic cleanup of expired entries.
    /// </summary>
    private void PerformCleanup(object? state)
    {
        if (_disposed) return;
        
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
                    _logger.LogDebug(
                        "Cleaned up expired kernel '{Name}' (key: {Key})",
                        entry.Definition.Name, entry.CacheKey[..8]);
                }
            }
            
            if (expiredEntries.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired kernel cache entries", expiredEntries.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
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
        if (_disposed) return;
        
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
        _logger.LogInformation(
            "Metal kernel cache disposed - Hit rate: {HitRate:P2}, Total hits: {Hits}, Total misses: {Misses}, Evictions: {Evictions}",
            stats.HitRate, stats.HitCount, stats.MissCount, stats.EvictionCount);
        
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