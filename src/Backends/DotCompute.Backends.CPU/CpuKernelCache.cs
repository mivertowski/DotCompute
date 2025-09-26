// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Performance;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Provides caching logic and compiled kernel management for CPU backend.
/// Manages kernel compilation cache, optimization profiles, and performance metrics.
/// </summary>
internal sealed class CpuKernelCache : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, CachedKernel> _kernelCache;
    private readonly ConcurrentDictionary<string, OptimizationProfile> _optimizationCache;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _performanceCache;
    private readonly Timer _cleanupTimer;
    private readonly CacheConfiguration _configuration;
    private readonly SemaphoreSlim _cacheLock;
    private bool _disposed;

    // Cache configuration defaults
    private readonly TimeSpan DefaultExpiryTime = TimeSpan.FromHours(1);
    private readonly int DefaultMaxCacheSize = 1000;
    private readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromMinutes(5);

    public CpuKernelCache(ILogger logger, CacheConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? new CacheConfiguration();
        _kernelCache = new ConcurrentDictionary<string, CachedKernel>();
        _optimizationCache = new ConcurrentDictionary<string, OptimizationProfile>();
        _performanceCache = new ConcurrentDictionary<string, PerformanceMetrics>();
        _cacheLock = new SemaphoreSlim(1, 1);

        // Set up periodic cache cleanup
        _cleanupTimer = new Timer(PerformCacheCleanup, null,
            _configuration.CleanupInterval ?? DefaultCleanupInterval,
            _configuration.CleanupInterval ?? DefaultCleanupInterval);

        _logger.LogDebug("CpuKernelCache initialized with max size: {maxSize}, expiry: {expiry}",
            _configuration.MaxCacheSize ?? DefaultMaxCacheSize,
            _configuration.ExpiryTime ?? DefaultExpiryTime);
    }

    /// <summary>
    /// Gets a compiled kernel from the cache.
    /// </summary>
    public async Task<CpuCompiledKernel?> GetKernelAsync(string cacheKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        if (_kernelCache.TryGetValue(cacheKey, out var cachedKernel))
        {
            // Check if kernel is still valid
            if (IsKernelValid(cachedKernel))
            {
                // Update access time and hit count
                cachedKernel.LastAccessed = DateTimeOffset.UtcNow;
                Interlocked.Increment(ref cachedKernel.HitCount);

                _logger.LogDebug("Cache hit for kernel: {cacheKey}", cacheKey);
                return cachedKernel.CompiledKernel;
            }
            else
            {
                // Remove expired kernel
                await RemoveKernelAsync(cacheKey);
                _logger.LogDebug("Cache miss (expired) for kernel: {cacheKey}", cacheKey);
            }
        }
        else
        {
            _logger.LogDebug("Cache miss for kernel: {cacheKey}", cacheKey);
        }

        return null;
    }

    /// <summary>
    /// Stores a compiled kernel in the cache.
    /// </summary>
    public async Task<bool> StoreKernelAsync(
        string cacheKey,
        CpuCompiledKernel compiledKernel,
        KernelDefinition definition,
        TimeSpan? customExpiry = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(compiledKernel);
        ArgumentNullException.ThrowIfNull(definition);

        await _cacheLock.WaitAsync();
        try
        {
            // Check cache size limits
            if (await ShouldEvictCacheEntries())
            {
                await EvictLeastRecentlyUsedEntriesAsync();
            }

            var expiryTime = customExpiry ?? _configuration.ExpiryTime ?? DefaultExpiryTime;
            var cachedKernel = new CachedKernel
            {
                CacheKey = cacheKey,
                CompiledKernel = compiledKernel,
                Definition = definition,
                CreationTime = DateTimeOffset.UtcNow,
                LastAccessed = DateTimeOffset.UtcNow,
                ExpiryTime = DateTimeOffset.UtcNow + expiryTime,
                HitCount = 0,
                Size = EstimateKernelSize(compiledKernel, definition)
            };

            var added = _kernelCache.TryAdd(cacheKey, cachedKernel);
            if (added)
            {
                _logger.LogDebug("Stored kernel in cache: {cacheKey}, expires: {expiry}",
                    cacheKey, cachedKernel.ExpiryTime);
            }

            return added;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Removes a kernel from the cache.
    /// </summary>
    public async Task<bool> RemoveKernelAsync(string cacheKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        if (_kernelCache.TryRemove(cacheKey, out var removedKernel))
        {
            // Dispose the kernel if it implements IDisposable
            if (removedKernel.CompiledKernel is IDisposable disposableKernel)
            {
                disposableKernel.Dispose();
            }

            _logger.LogDebug("Removed kernel from cache: {cacheKey}", cacheKey);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets an optimization profile from the cache.
    /// </summary>
    public async Task<OptimizationProfile?> GetOptimizationProfileAsync(string profileKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey);

        if (_optimizationCache.TryGetValue(profileKey, out var profile))
        {
            if (IsProfileValid(profile))
            {
                profile.LastAccessed = DateTimeOffset.UtcNow;
                _logger.LogTrace("Optimization profile cache hit: {profileKey}", profileKey);
                return profile;
            }
            else
            {
                _optimizationCache.TryRemove(profileKey, out _);
                _logger.LogTrace("Optimization profile cache miss (expired): {profileKey}", profileKey);
            }
        }

        return null;
    }

    /// <summary>
    /// Stores an optimization profile in the cache.
    /// </summary>
    public async Task<bool> StoreOptimizationProfileAsync(string profileKey, OptimizationProfile profile)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey);
        ArgumentNullException.ThrowIfNull(profile);

        profile.LastAccessed = DateTimeOffset.UtcNow;
        var added = _optimizationCache.TryAdd(profileKey, profile);

        if (added)
        {
            _logger.LogTrace("Stored optimization profile: {profileKey}", profileKey);
        }

        return added;
    }

    /// <summary>
    /// Gets performance metrics from the cache.
    /// </summary>
    public async Task<PerformanceMetrics?> GetPerformanceMetricsAsync(string metricsKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(metricsKey);

        if (_performanceCache.TryGetValue(metricsKey, out var metrics))
        {
            if (IsMetricsValid(metrics))
            {
                _logger.LogTrace("Performance metrics cache hit: {metricsKey}", metricsKey);
                return metrics;
            }
            else
            {
                _performanceCache.TryRemove(metricsKey, out _);
                _logger.LogTrace("Performance metrics cache miss (expired): {metricsKey}", metricsKey);
            }
        }

        return null;
    }

    /// <summary>
    /// Stores performance metrics in the cache.
    /// </summary>
    public async Task<bool> StorePerformanceMetricsAsync(string metricsKey, PerformanceMetrics metrics)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(metricsKey);
        ArgumentNullException.ThrowIfNull(metrics);

        var added = _performanceCache.TryAdd(metricsKey, metrics);

        if (added)
        {
            _logger.LogTrace("Stored performance metrics: {metricsKey}", metricsKey);
        }

        return added;
    }

    /// <summary>
    /// Updates performance metrics for a kernel.
    /// </summary>
    public async Task UpdateKernelPerformanceAsync(string cacheKey, ExecutionStatistics statistics)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(statistics);

        if (_kernelCache.TryGetValue(cacheKey, out var cachedKernel))
        {
            cachedKernel.ExecutionStatistics = statistics;
            cachedKernel.LastAccessed = DateTimeOffset.UtcNow;

            _logger.LogTrace("Updated performance metrics for kernel: {cacheKey}", cacheKey);
        }
    }

    /// <summary>
    /// Generates a cache key for a kernel definition and configuration.
    /// </summary>
    public static string GenerateCacheKey(
        KernelDefinition definition,
        WorkDimensions workDimensions,
        OptimizationLevel optimizationLevel)
    {
        var key = $"{definition.Name}_{workDimensions.X}x{workDimensions.Y}x{workDimensions.Z}_{optimizationLevel}";

        // Include parameter types in key for disambiguation
        if (definition.Parameters?.Any() == true)
        {
            var paramTypes = string.Join(",", definition.Parameters.Select(p => p.Type.Name));
            key += $"_{paramTypes.GetHashCode():X}";
        }

        return key;
    }

    /// <summary>
    /// Gets cache statistics and performance information.
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var kernelStats = CalculateKernelCacheStatistics();
        _ = CalculateOptimizationCacheStatistics();
        _ = CalculatePerformanceCacheStatistics();

        return new CacheStatistics
        {
            KernelCacheSize = _kernelCache.Count,
            OptimizationCacheSize = _optimizationCache.Count,
            PerformanceCacheSize = _performanceCache.Count,
            TotalMemoryUsage = kernelStats.TotalMemoryUsage,
            HitRate = kernelStats.HitRate,
            EvictionCount = kernelStats.EvictionCount,
            OldestEntry = kernelStats.OldestEntry,
            MostAccessedEntry = kernelStats.MostAccessedEntry
        };
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public async Task ClearAllAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _cacheLock.WaitAsync();
        try
        {
            // Dispose all cached kernels
            foreach (var cachedKernel in _kernelCache.Values)
            {
                if (cachedKernel.CompiledKernel is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _kernelCache.Clear();
            _optimizationCache.Clear();
            _performanceCache.Clear();

            _logger.LogInformation("Cleared all cache entries");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Preloads frequently used kernels into the cache.
    /// </summary>
    public async Task PreloadFrequentKernelsAsync(IEnumerable<string> kernelNames)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(kernelNames);

        foreach (var kernelName in kernelNames)
        {
            // This would typically involve compiling and caching common kernel configurations
            // For now, this is a placeholder that could be implemented based on usage patterns
            _logger.LogDebug("Preload requested for kernel: {kernelName}", kernelName);
        }
    }

    // Private implementation methods

    private static bool IsKernelValid(CachedKernel cachedKernel)
    {
        return DateTimeOffset.UtcNow < cachedKernel.ExpiryTime;
    }

    private bool IsProfileValid(OptimizationProfile profile)
    {
        var maxAge = _configuration.ProfileMaxAge ?? TimeSpan.FromHours(24);
        return DateTimeOffset.UtcNow - profile.CreationTime < maxAge;
    }

    private bool IsMetricsValid(PerformanceMetrics metrics)
    {
        var maxAge = _configuration.MetricsMaxAge ?? TimeSpan.FromHours(1);
        return DateTimeOffset.UtcNow - metrics.LastUpdated < maxAge;
    }

    private async Task<bool> ShouldEvictCacheEntries()
    {
        var maxSize = _configuration.MaxCacheSize ?? DefaultMaxCacheSize;
        return _kernelCache.Count >= maxSize;
    }

    private async Task EvictLeastRecentlyUsedEntriesAsync()
    {
        var entriesToEvict = _kernelCache.Values
            .OrderBy(k => k.LastAccessed)
            .Take(_kernelCache.Count / 4) // Evict 25% of entries
            .ToList();

        foreach (var entry in entriesToEvict)
        {
            await RemoveKernelAsync(entry.CacheKey);
        }

        _logger.LogDebug("Evicted {count} cache entries due to size limit", entriesToEvict.Count);
    }

    private void PerformCacheCleanup(object? state)
    {
        try
        {
            _ = Task.Run(async () =>
            {
                await CleanupExpiredEntriesAsync();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
    }

    private async Task CleanupExpiredEntriesAsync()
    {
        var currentTime = DateTimeOffset.UtcNow;
        var expiredKernels = new List<string>();
        var expiredProfiles = new List<string>();
        var expiredMetrics = new List<string>();

        // Find expired kernel cache entries
        foreach (var kvp in _kernelCache)
        {
            if (currentTime >= kvp.Value.ExpiryTime)
            {
                expiredKernels.Add(kvp.Key);
            }
        }

        // Find expired optimization profiles
        foreach (var kvp in _optimizationCache)
        {
            if (!IsProfileValid(kvp.Value))
            {
                expiredProfiles.Add(kvp.Key);
            }
        }

        // Find expired performance metrics
        foreach (var kvp in _performanceCache)
        {
            if (!IsMetricsValid(kvp.Value))
            {
                expiredMetrics.Add(kvp.Key);
            }
        }

        // Remove expired entries
        foreach (var key in expiredKernels)
        {
            await RemoveKernelAsync(key);
        }

        foreach (var key in expiredProfiles)
        {
            _optimizationCache.TryRemove(key, out _);
        }

        foreach (var key in expiredMetrics)
        {
            _performanceCache.TryRemove(key, out _);
        }

        if (expiredKernels.Count > 0 || expiredProfiles.Count > 0 || expiredMetrics.Count > 0)
        {
            _logger.LogDebug("Cleaned up expired cache entries: {kernels} kernels, {profiles} profiles, {metrics} metrics",
                expiredKernels.Count, expiredProfiles.Count, expiredMetrics.Count);
        }
    }

    private static long EstimateKernelSize(CpuCompiledKernel kernel, KernelDefinition definition)
    {
        // Simplified size estimation
        long size = 1024; // Base overhead

        // Add estimated size for definition
        size += definition.Name.Length * 2; // Unicode characters
        size += definition.Code?.Length * 2 ?? 0;

        // Add estimated size for parameters
        if (definition.Parameters != null)
        {
            size += definition.Parameters.Count * 64; // Estimated parameter overhead
        }

        return size;
    }

    private KernelCacheStatistics CalculateKernelCacheStatistics()
    {
        var kernels = _kernelCache.Values.ToList();

        var totalHits = kernels.Sum(k => k.HitCount);
        var totalAccesses = kernels.Count; // Simplified - would track actual access count
        var hitRate = totalAccesses > 0 ? (double)totalHits / totalAccesses : 0.0;

        var totalMemory = kernels.Sum(k => k.Size);
        var oldestEntry = kernels.MinBy(k => k.CreationTime)?.CreationTime;
        var mostAccessed = kernels.MaxBy(k => k.HitCount)?.CacheKey;

        return new KernelCacheStatistics
        {
            TotalMemoryUsage = totalMemory,
            HitRate = hitRate,
            EvictionCount = 0, // Would be tracked separately
            OldestEntry = oldestEntry,
            MostAccessedEntry = mostAccessed
        };
    }

    private OptimizationCacheStatistics CalculateOptimizationCacheStatistics()
    {
        return new OptimizationCacheStatistics
        {
            EntryCount = _optimizationCache.Count,
            AverageAge = _optimizationCache.Values.Any()
                ? _optimizationCache.Values.Average(p => (DateTimeOffset.UtcNow - p.CreationTime).TotalMinutes)
                : 0
        };
    }

    private PerformanceCacheStatistics CalculatePerformanceCacheStatistics()
    {
        return new PerformanceCacheStatistics
        {
            EntryCount = _performanceCache.Count,
            AverageAge = _performanceCache.Values.Any()
                ? _performanceCache.Values.Average(m => (DateTimeOffset.UtcNow - m.LastUpdated).TotalMinutes)
                : 0
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer?.Dispose();

            // Dispose all cached kernels
            foreach (var cachedKernel in _kernelCache.Values)
            {
                if (cachedKernel.CompiledKernel is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing cached kernel");
                    }
                }
            }

            _kernelCache.Clear();
            _optimizationCache.Clear();
            _performanceCache.Clear();
            _cacheLock?.Dispose();

            _disposed = true;
        }
    }
}

// Supporting classes for cache management

public class CacheConfiguration
{
    public int? MaxCacheSize { get; set; }
    public TimeSpan? ExpiryTime { get; set; }
    public TimeSpan? CleanupInterval { get; set; }
    public TimeSpan? ProfileMaxAge { get; set; }
    public TimeSpan? MetricsMaxAge { get; set; }
}

public class CachedKernel
{
    public required string CacheKey { get; set; }
    public required CpuCompiledKernel CompiledKernel { get; set; }
    public required KernelDefinition Definition { get; set; }
    public DateTimeOffset CreationTime { get; set; }
    public DateTimeOffset LastAccessed { get; set; }
    public DateTimeOffset ExpiryTime { get; set; }
    public long HitCount { get; set; }
    public long Size { get; set; }
    public ExecutionStatistics? ExecutionStatistics { get; set; }
}

public class CacheStatistics
{
    public int KernelCacheSize { get; set; }
    public int OptimizationCacheSize { get; set; }
    public int PerformanceCacheSize { get; set; }
    public long TotalMemoryUsage { get; set; }
    public double HitRate { get; set; }
    public long EvictionCount { get; set; }
    public DateTimeOffset? OldestEntry { get; set; }
    public string? MostAccessedEntry { get; set; }
}

public class KernelCacheStatistics
{
    public long TotalMemoryUsage { get; set; }
    public double HitRate { get; set; }
    public long EvictionCount { get; set; }
    public DateTimeOffset? OldestEntry { get; set; }
    public string? MostAccessedEntry { get; set; }
}

public class OptimizationCacheStatistics
{
    public int EntryCount { get; set; }
    public double AverageAge { get; set; }
}

public class PerformanceCacheStatistics
{
    public int EntryCount { get; set; }
    public double AverageAge { get; set; }
}

// Use the canonical PerformanceMetrics from DotCompute.Abstractions.Performance
// This local class has been replaced with the unified type