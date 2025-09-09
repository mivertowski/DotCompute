// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Runtime.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Runtime.Services.Implementation;

/// <summary>
/// In-memory implementation of kernel cache with LRU eviction and statistics tracking.
/// </summary>
public class MemoryKernelCache : IKernelCache, IDisposable
{
    private readonly ILogger<MemoryKernelCache> _logger;
    private readonly MemoryCacheOptions _options;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    private readonly ReaderWriterLockSlim _statisticsLock;
    private readonly Timer _cleanupTimer;
    
    private long _hitCount;
    private long _missCount;
    private long _totalCompilationTimeSaved;
    private bool _disposed;

    public MemoryKernelCache(
        ILogger<MemoryKernelCache> logger,
        IOptions<MemoryCacheOptions>? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new MemoryCacheOptions();
        _cache = new ConcurrentDictionary<string, CacheEntry>();
        _statisticsLock = new ReaderWriterLockSlim();
        
        // Setup periodic cleanup for expired entries
        _cleanupTimer = new Timer(
            CleanupExpiredEntries,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }

    /// <inheritdoc />
    public string GenerateCacheKey(
        KernelDefinition kernelDefinition, 
        IAccelerator accelerator,
        CompilationOptions? compilationOptions)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append(kernelDefinition.Name);
        keyBuilder.Append('_');
        keyBuilder.Append(accelerator.Info.DeviceType);
        keyBuilder.Append('_');
        keyBuilder.Append(accelerator.Info.DeviceId);
        
        if (compilationOptions != null)
        {
            keyBuilder.Append('_');
            keyBuilder.Append(compilationOptions.OptimizationLevel);
            keyBuilder.Append('_');
            keyBuilder.Append(compilationOptions.TargetArchitecture);
        }

        // Add kernel source hash for cache invalidation on kernel changes
        if (!string.IsNullOrEmpty(kernelDefinition.Source))
        {
            using var sha256 = SHA256.Create();
            var sourceBytes = Encoding.UTF8.GetBytes(kernelDefinition.Source);
            var hashBytes = sha256.ComputeHash(sourceBytes);
            var hashString = Convert.ToBase64String(hashBytes)[..8]; // Use first 8 chars of hash
            keyBuilder.Append('_');
            keyBuilder.Append(hashString);
        }

        return keyBuilder.ToString();
    }

    /// <inheritdoc />
    public Task<ICompiledKernel?> GetAsync(string cacheKey)
    {
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            // Check if entry is still valid
            if (!entry.IsExpired)
            {
                entry.UpdateLastAccess();
                IncrementHitCount(entry.CompilationTime);
                
                _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
                return Task.FromResult<ICompiledKernel?>(entry.CompiledKernel);
            }
            
            // Remove expired entry
            _cache.TryRemove(cacheKey, out _);
        }

        IncrementMissCount();
        _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
        return Task.FromResult<ICompiledKernel?>(null);
    }

    /// <inheritdoc />
    public Task StoreAsync(string cacheKey, ICompiledKernel compiledKernel)
    {
        // Enforce cache size limit
        if (_cache.Count >= _options.MaxEntries)
        {
            EvictLeastRecentlyUsed();
        }

        var entry = new CacheEntry
        {
            CompiledKernel = compiledKernel,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            CompilationTime = TimeSpan.FromMilliseconds(100), // Estimate, should be tracked during compilation
            ExpiresAt = _options.DefaultExpiration.HasValue 
                ? DateTime.UtcNow + _options.DefaultExpiration.Value 
                : DateTime.MaxValue
        };

        _cache[cacheKey] = entry;
        _logger.LogDebug("Cached kernel with key: {CacheKey}", cacheKey);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> InvalidateAsync(string cacheKey)
    {
        var removed = _cache.TryRemove(cacheKey, out _);
        
        if (removed)
        {
            _logger.LogDebug("Invalidated cache entry: {CacheKey}", cacheKey);
        }
        
        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<int> ClearAsync()
    {
        var count = _cache.Count;
        _cache.Clear();
        
        _logger.LogInformation("Cleared {Count} cache entries", count);
        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task<CacheStatistics> GetStatisticsAsync()
    {
        _statisticsLock.EnterReadLock();
        try
        {
            var stats = new CacheStatistics
            {
                EntryCount = _cache.Count,
                TotalSizeBytes = EstimateCacheSize(),
                HitCount = _hitCount,
                MissCount = _missCount,
                AverageTimeSavedPerHit = _hitCount > 0 
                    ? TimeSpan.FromMilliseconds(_totalCompilationTimeSaved / _hitCount)
                    : TimeSpan.Zero
            };

            return Task.FromResult(stats);
        }
        finally
        {
            _statisticsLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public async Task<int> PrewarmAsync(
        IEnumerable<KernelDefinition> kernelDefinitions,
        IEnumerable<IAccelerator> accelerators)
    {
        var successCount = 0;
        var acceleratorList = accelerators.ToList();

        foreach (var kernel in kernelDefinitions)
        {
            foreach (var accelerator in acceleratorList)
            {
                try
                {
                    // Generate cache key but don't compile here
                    // The actual compilation should be done by the caller
                    var cacheKey = GenerateCacheKey(kernel, accelerator, null);
                    
                    // Check if already cached
                    if (!_cache.ContainsKey(cacheKey))
                    {
                        _logger.LogDebug("Kernel {KernelName} needs pre-warming for {AcceleratorType}", 
                            kernel.Name, accelerator.Info.DeviceType);
                        // Note: Actual pre-warming would require compilation which should be done by the service layer
                    }
                    else
                    {
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to pre-warm kernel {KernelName}", kernel.Name);
                }
            }
        }

        return successCount;
    }

    private void EvictLeastRecentlyUsed()
    {
        if (_cache.IsEmpty) return;

        var lruEntry = _cache
            .OrderBy(kvp => kvp.Value.LastAccessedAt)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(lruEntry.Key))
        {
            _cache.TryRemove(lruEntry.Key, out _);
            _logger.LogDebug("Evicted LRU cache entry: {CacheKey}", lruEntry.Key);
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
        }
    }

    private long EstimateCacheSize()
    {
        // Rough estimation: assume each compiled kernel is ~10KB on average TODO
        return _cache.Count * 10 * 1024;
    }

    private void IncrementHitCount(TimeSpan compilationTimeSaved)
    {
        _statisticsLock.EnterWriteLock();
        try
        {
            _hitCount++;
            _totalCompilationTimeSaved += (long)compilationTimeSaved.TotalMilliseconds;
        }
        finally
        {
            _statisticsLock.ExitWriteLock();
        }
    }

    private void IncrementMissCount()
    {
        _statisticsLock.EnterWriteLock();
        try
        {
            _missCount++;
        }
        finally
        {
            _statisticsLock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cleanupTimer?.Dispose();
        _cache.Clear();
        _statisticsLock?.Dispose();
        
        _disposed = true;
    }

    private class CacheEntry
    {
        public required ICompiledKernel CompiledKernel { get; init; }
        public required DateTime CreatedAt { get; init; }
        public DateTime LastAccessedAt { get; set; }
        public required TimeSpan CompilationTime { get; init; }
        public required DateTime ExpiresAt { get; init; }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        public void UpdateLastAccess()
        {
            LastAccessedAt = DateTime.UtcNow;
        }
    }
}

/// <summary>
/// Options for configuring the memory kernel cache.
/// </summary>
public class MemoryCacheOptions
{
    /// <summary>
    /// Maximum number of cache entries to maintain.
    /// </summary>
    public int MaxEntries { get; set; } = 1000;

    /// <summary>
    /// Default expiration time for cache entries.
    /// </summary>
    public TimeSpan? DefaultExpiration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether to enable cache statistics tracking.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Interval for cleaning up expired entries.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
}