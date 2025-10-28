// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Performance;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Provides caching logic and compiled kernel management for CPU backend.
/// Manages kernel compilation cache, optimization profiles, and performance metrics.
/// </summary>
internal sealed partial class CpuKernelCache : IDisposable
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
    private readonly TimeSpan _defaultExpiryTime = TimeSpan.FromHours(1);
    private readonly int _defaultMaxCacheSize = 1000;
    private readonly TimeSpan _defaultCleanupInterval = TimeSpan.FromMinutes(5);

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 7100,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "CpuKernelCache initialized with max size: {MaxSize}, expiry: {Expiry}")]
    private static partial void LogCacheInitialized(ILogger logger, int maxSize, TimeSpan expiry);

    [LoggerMessage(
        EventId = 7101,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Cache hit for kernel: {CacheKey}")]
    private static partial void LogCacheHit(ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 7102,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Cache miss (expired) for kernel: {CacheKey}")]
    private static partial void LogCacheMissExpired(ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 7103,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Cache miss for kernel: {CacheKey}")]
    private static partial void LogCacheMiss(ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 7104,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Stored kernel in cache: {CacheKey}, expires: {Expiry}")]
    private static partial void LogKernelStored(ILogger logger, string cacheKey, DateTimeOffset expiry);

    [LoggerMessage(
        EventId = 7105,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Removed kernel from cache: {CacheKey}")]
    private static partial void LogKernelRemoved(ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 7106,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Optimization profile cache hit: {ProfileKey}")]
    private static partial void LogOptimizationProfileHit(ILogger logger, string profileKey);

    [LoggerMessage(
        EventId = 7107,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Optimization profile cache miss (expired): {ProfileKey}")]
    private static partial void LogOptimizationProfileMissExpired(ILogger logger, string profileKey);

    [LoggerMessage(
        EventId = 7108,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Stored optimization profile: {ProfileKey}")]
    private static partial void LogOptimizationProfileStored(ILogger logger, string profileKey);

    [LoggerMessage(
        EventId = 7109,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Performance metrics cache hit: {MetricsKey}")]
    private static partial void LogPerformanceMetricsHit(ILogger logger, string metricsKey);

    [LoggerMessage(
        EventId = 7110,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Performance metrics cache miss (expired): {MetricsKey}")]
    private static partial void LogPerformanceMetricsMissExpired(ILogger logger, string metricsKey);

    [LoggerMessage(
        EventId = 7111,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Stored performance metrics: {MetricsKey}")]
    private static partial void LogPerformanceMetricsStored(ILogger logger, string metricsKey);

    [LoggerMessage(
        EventId = 7112,
        Level = Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "Updated performance metrics for kernel: {CacheKey}")]
    private static partial void LogPerformanceMetricsUpdated(ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 7113,
        Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Cleared all cache entries")]
    private static partial void LogCacheCleared(ILogger logger);

    [LoggerMessage(
        EventId = 7114,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Preload requested for kernel: {KernelName}")]
    private static partial void LogPreloadRequested(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7115,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Evicted {Count} cache entries due to size limit")]
    private static partial void LogCacheEntriesEvicted(ILogger logger, int count);

    [LoggerMessage(
        EventId = 7116,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Error during cache cleanup")]
    private static partial void LogCacheCleanupError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 7117,
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Cleaned up expired cache entries: {Kernels} kernels, {Profiles} profiles, {Metrics} metrics")]
    private static partial void LogExpiredEntriesCleanup(ILogger logger, int kernels, int profiles, int metrics);

    [LoggerMessage(
        EventId = 7118,
        Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Error disposing cached kernel")]
    private static partial void LogKernelDisposeError(ILogger logger, Exception ex);

    #endregion
    /// <summary>
    /// Initializes a new instance of the CpuKernelCache class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>

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
            _configuration.CleanupInterval ?? _defaultCleanupInterval,
            _configuration.CleanupInterval ?? _defaultCleanupInterval);

        LogCacheInitialized(_logger,
            _configuration.MaxCacheSize ?? _defaultMaxCacheSize,
            _configuration.ExpiryTime ?? _defaultExpiryTime);
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
                cachedKernel.HitCount++;

                LogCacheHit(_logger, cacheKey);
                return cachedKernel.CompiledKernel;
            }
            else
            {
                // Remove expired kernel
                _ = await RemoveKernelAsync(cacheKey);
                LogCacheMissExpired(_logger, cacheKey);
            }
        }
        else
        {
            LogCacheMiss(_logger, cacheKey);
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
            if (await ShouldEvictCacheEntriesAsync())
            {
                await EvictLeastRecentlyUsedEntriesAsync();
            }

            var expiryTime = customExpiry ?? _configuration.ExpiryTime ?? _defaultExpiryTime;
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
                LogKernelStored(_logger, cacheKey, cachedKernel.ExpiryTime);
            }

            return added;
        }
        finally
        {
            _ = _cacheLock.Release();
        }
    }

    /// <summary>
    /// Removes a kernel from the cache.
    /// </summary>
    public Task<bool> RemoveKernelAsync(string cacheKey)
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

            LogKernelRemoved(_logger, cacheKey);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets an optimization profile from the cache.
    /// </summary>
    public Task<OptimizationProfile?> GetOptimizationProfileAsync(string profileKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey);

        if (_optimizationCache.TryGetValue(profileKey, out var profile))
        {
            if (IsProfileValid(profile))
            {
                profile.LastAccessed = DateTimeOffset.UtcNow;
                LogOptimizationProfileHit(_logger, profileKey);
                return Task.FromResult<OptimizationProfile?>(profile);
            }
            else
            {
                _ = _optimizationCache.TryRemove(profileKey, out _);
                LogOptimizationProfileMissExpired(_logger, profileKey);
            }
        }

        return Task.FromResult<OptimizationProfile?>(null);
    }

    /// <summary>
    /// Stores an optimization profile in the cache.
    /// </summary>
    public Task<bool> StoreOptimizationProfileAsync(string profileKey, OptimizationProfile profile)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey);
        ArgumentNullException.ThrowIfNull(profile);

        profile.LastAccessed = DateTimeOffset.UtcNow;
        var added = _optimizationCache.TryAdd(profileKey, profile);

        if (added)
        {
            LogOptimizationProfileStored(_logger, profileKey);
        }

        return Task.FromResult(added);
    }

    /// <summary>
    /// Gets performance metrics from the cache.
    /// </summary>
    public Task<PerformanceMetrics?> GetPerformanceMetricsAsync(string metricsKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(metricsKey);

        if (_performanceCache.TryGetValue(metricsKey, out var metrics))
        {
            if (IsMetricsValid(metrics))
            {
                LogPerformanceMetricsHit(_logger, metricsKey);
                return Task.FromResult<PerformanceMetrics?>(metrics);
            }
            else
            {
                _ = _performanceCache.TryRemove(metricsKey, out _);
                LogPerformanceMetricsMissExpired(_logger, metricsKey);
            }
        }

        return Task.FromResult<PerformanceMetrics?>(null);
    }

    /// <summary>
    /// Stores performance metrics in the cache.
    /// </summary>
    public Task<bool> StorePerformanceMetricsAsync(string metricsKey, PerformanceMetrics metrics)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(metricsKey);
        ArgumentNullException.ThrowIfNull(metrics);

        var added = _performanceCache.TryAdd(metricsKey, metrics);

        if (added)
        {
            LogPerformanceMetricsStored(_logger, metricsKey);
        }

        return Task.FromResult(added);
    }

    /// <summary>
    /// Updates performance metrics for a kernel.
    /// </summary>
    public Task UpdateKernelPerformanceAsync(string cacheKey, ExecutionStatistics statistics)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(statistics);

        if (_kernelCache.TryGetValue(cacheKey, out var cachedKernel))
        {
            cachedKernel.ExecutionStatistics = statistics;
            cachedKernel.LastAccessed = DateTimeOffset.UtcNow;

            LogPerformanceMetricsUpdated(_logger, cacheKey);
        }

        return Task.CompletedTask;
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

        // Include metadata types in key for disambiguation if available
        if (definition.Metadata?.Count > 0)
        {
            var metadataHash = string.Join(",", definition.Metadata.Keys.OrderBy(k => k));
            key += $"_{metadataHash.GetHashCode(StringComparison.Ordinal):X}";
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

            LogCacheCleared(_logger);
        }
        finally
        {
            _ = _cacheLock.Release();
        }
    }

    /// <summary>
    /// Preloads frequently used kernels into the cache.
    /// </summary>
    public Task PreloadFrequentKernelsAsync(IEnumerable<string> kernelNames)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(kernelNames);

        foreach (var kernelName in kernelNames)
        {
            // This would typically involve compiling and caching common kernel configurations
            // For now, this is a placeholder that could be implemented based on usage patterns
            LogPreloadRequested(_logger, kernelName);
        }

        return Task.CompletedTask;
    }

    // Private implementation methods

    private static bool IsKernelValid(CachedKernel cachedKernel) => DateTimeOffset.UtcNow < cachedKernel.ExpiryTime;

    private bool IsProfileValid(OptimizationProfile profile)
    {
        var maxAge = _configuration.ProfileMaxAge ?? TimeSpan.FromHours(24);
        return DateTimeOffset.UtcNow - profile.CreationTime < maxAge;
    }

    private bool IsMetricsValid(PerformanceMetrics metrics)
    {
        var maxAge = _configuration.MetricsMaxAge ?? TimeSpan.FromHours(1);
        return DateTimeOffset.UtcNow - metrics.Timestamp < maxAge;
    }

    private Task<bool> ShouldEvictCacheEntriesAsync()
    {
        var maxSize = _configuration.MaxCacheSize ?? _defaultMaxCacheSize;
        return Task.FromResult(_kernelCache.Count >= maxSize);
    }

    private async Task EvictLeastRecentlyUsedEntriesAsync()
    {
        var entriesToEvict = _kernelCache.Values
            .OrderBy(k => k.LastAccessed)
            .Take(_kernelCache.Count / 4) // Evict 25% of entries
            .ToList();

        foreach (var entry in entriesToEvict)
        {
            _ = await RemoveKernelAsync(entry.CacheKey);
        }

        LogCacheEntriesEvicted(_logger, entriesToEvict.Count);
    }

    private void PerformCacheCleanup(object? state)
    {
        try
        {
            _ = Task.Run(CleanupExpiredEntriesAsync);
        }
        catch (Exception ex)
        {
            LogCacheCleanupError(_logger, ex);
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
            _ = await RemoveKernelAsync(key);
        }

        foreach (var key in expiredProfiles)
        {
            _ = _optimizationCache.TryRemove(key, out _);
        }

        foreach (var key in expiredMetrics)
        {
            _ = _performanceCache.TryRemove(key, out _);
        }

        if (expiredKernels.Count > 0 || expiredProfiles.Count > 0 || expiredMetrics.Count > 0)
        {
            LogExpiredEntriesCleanup(_logger, expiredKernels.Count, expiredProfiles.Count, expiredMetrics.Count);
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
        // NOTE: KernelDefinition.Parameters doesn't exist in current API
        // Use metadata count as a rough estimate if available
        if (definition.Metadata?.Count > 0)
        {
            size += definition.Metadata.Count * 64; // Estimated metadata overhead
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
            AverageAge = !_optimizationCache.IsEmpty
                ? _optimizationCache.Values.Average(p => (DateTimeOffset.UtcNow - p.CreationTime).TotalMinutes)
                : 0
        };
    }

    private PerformanceCacheStatistics CalculatePerformanceCacheStatistics()
    {
        return new PerformanceCacheStatistics
        {
            EntryCount = _performanceCache.Count,
            AverageAge = !_performanceCache.IsEmpty
                ? _performanceCache.Values.Average(m => (DateTimeOffset.UtcNow - m.Timestamp).TotalMinutes)
                : 0
        };
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
                        LogKernelDisposeError(_logger, ex);
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
/// <summary>
/// A class that represents cache configuration.
/// </summary>

// Supporting classes for cache management

public class CacheConfiguration
{
    /// <summary>
    /// Gets or sets the max cache size.
    /// </summary>
    /// <value>The max cache size.</value>
    public int? MaxCacheSize { get; set; }
    /// <summary>
    /// Gets or sets the expiry time.
    /// </summary>
    /// <value>The expiry time.</value>
    public TimeSpan? ExpiryTime { get; set; }
    /// <summary>
    /// Gets or sets the cleanup interval.
    /// </summary>
    /// <value>The cleanup interval.</value>
    public TimeSpan? CleanupInterval { get; set; }
    /// <summary>
    /// Gets or sets the profile max age.
    /// </summary>
    /// <value>The profile max age.</value>
    public TimeSpan? ProfileMaxAge { get; set; }
    /// <summary>
    /// Gets or sets the metrics max age.
    /// </summary>
    /// <value>The metrics max age.</value>
    public TimeSpan? MetricsMaxAge { get; set; }
}
/// <summary>
/// A class that represents cached kernel.
/// </summary>

public class CachedKernel
{
    /// <summary>
    /// Gets or sets the cache key.
    /// </summary>
    /// <value>The cache key.</value>
    public required string CacheKey { get; set; }
    /// <summary>
    /// Gets or sets the compiled kernel.
    /// </summary>
    /// <value>The compiled kernel.</value>
    public required CpuCompiledKernel CompiledKernel { get; set; }
    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    /// <value>The definition.</value>
    public required KernelDefinition Definition { get; set; }
    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    /// <value>The creation time.</value>
    public DateTimeOffset CreationTime { get; set; }
    /// <summary>
    /// Gets or sets the last accessed.
    /// </summary>
    /// <value>The last accessed.</value>
    public DateTimeOffset LastAccessed { get; set; }
    /// <summary>
    /// Gets or sets the expiry time.
    /// </summary>
    /// <value>The expiry time.</value>
    public DateTimeOffset ExpiryTime { get; set; }
    /// <summary>
    /// Gets or sets the hit count.
    /// </summary>
    /// <value>The hit count.</value>
    public long HitCount { get; set; }
    /// <summary>
    /// Gets or sets the size.
    /// </summary>
    /// <value>The size.</value>
    public long Size { get; set; }
    /// <summary>
    /// Gets or sets the execution statistics.
    /// </summary>
    /// <value>The execution statistics.</value>
    public ExecutionStatistics? ExecutionStatistics { get; set; }
}
/// <summary>
/// A class that represents cache statistics.
/// </summary>

public class CacheStatistics
{
    /// <summary>
    /// Gets or sets the kernel cache size.
    /// </summary>
    /// <value>The kernel cache size.</value>
    public int KernelCacheSize { get; set; }
    /// <summary>
    /// Gets or sets the optimization cache size.
    /// </summary>
    /// <value>The optimization cache size.</value>
    public int OptimizationCacheSize { get; set; }
    /// <summary>
    /// Gets or sets the performance cache size.
    /// </summary>
    /// <value>The performance cache size.</value>
    public int PerformanceCacheSize { get; set; }
    /// <summary>
    /// Gets or sets the total memory usage.
    /// </summary>
    /// <value>The total memory usage.</value>
    public long TotalMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the hit rate.
    /// </summary>
    /// <value>The hit rate.</value>
    public double HitRate { get; set; }
    /// <summary>
    /// Gets or sets the eviction count.
    /// </summary>
    /// <value>The eviction count.</value>
    public long EvictionCount { get; set; }
    /// <summary>
    /// Gets or sets the oldest entry.
    /// </summary>
    /// <value>The oldest entry.</value>
    public DateTimeOffset? OldestEntry { get; set; }
    /// <summary>
    /// Gets or sets the most accessed entry.
    /// </summary>
    /// <value>The most accessed entry.</value>
    public string? MostAccessedEntry { get; set; }
}
/// <summary>
/// A class that represents kernel cache statistics.
/// </summary>

public class KernelCacheStatistics
{
    /// <summary>
    /// Gets or sets the total memory usage.
    /// </summary>
    /// <value>The total memory usage.</value>
    public long TotalMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the hit rate.
    /// </summary>
    /// <value>The hit rate.</value>
    public double HitRate { get; set; }
    /// <summary>
    /// Gets or sets the eviction count.
    /// </summary>
    /// <value>The eviction count.</value>
    public long EvictionCount { get; set; }
    /// <summary>
    /// Gets or sets the oldest entry.
    /// </summary>
    /// <value>The oldest entry.</value>
    public DateTimeOffset? OldestEntry { get; set; }
    /// <summary>
    /// Gets or sets the most accessed entry.
    /// </summary>
    /// <value>The most accessed entry.</value>
    public string? MostAccessedEntry { get; set; }
}
/// <summary>
/// A class that represents optimization cache statistics.
/// </summary>

public class OptimizationCacheStatistics
{
    /// <summary>
    /// Gets or sets the entry count.
    /// </summary>
    /// <value>The entry count.</value>
    public int EntryCount { get; set; }
    /// <summary>
    /// Gets or sets the average age.
    /// </summary>
    /// <value>The average age.</value>
    public double AverageAge { get; set; }
}
/// <summary>
/// A class that represents performance cache statistics.
/// </summary>

public class PerformanceCacheStatistics
{
    /// <summary>
    /// Gets or sets the entry count.
    /// </summary>
    /// <value>The entry count.</value>
    public int EntryCount { get; set; }
    /// <summary>
    /// Gets or sets the average age.
    /// </summary>
    /// <value>The average age.</value>
    public double AverageAge { get; set; }
}

// Use the canonical PerformanceMetrics from DotCompute.Abstractions.Performance


// This local class has been replaced with the unified type