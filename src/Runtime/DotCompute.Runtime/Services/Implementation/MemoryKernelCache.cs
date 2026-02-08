// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Runtime.Logging;
using DotCompute.Runtime.Services.Configuration;
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
    /// <summary>
    /// Initializes a new instance of the MemoryKernelCache class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>

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
        _ = keyBuilder.Append(kernelDefinition.Name);
        _ = keyBuilder.Append('_');
        _ = keyBuilder.Append(accelerator.Info.DeviceType);
        _ = keyBuilder.Append('_');
        _ = keyBuilder.Append(accelerator.Info.Id);


        if (compilationOptions != null)
        {
            _ = keyBuilder.Append('_');
            _ = keyBuilder.Append(compilationOptions.OptimizationLevel);
            _ = keyBuilder.Append('_');
            _ = keyBuilder.Append(compilationOptions.TargetArchitecture);
        }

        // Add kernel source hash for cache invalidation on kernel changes
        if (!string.IsNullOrEmpty(kernelDefinition.Source))
        {
            var sourceBytes = Encoding.UTF8.GetBytes(kernelDefinition.Source);
            var hashBytes = SHA256.HashData(sourceBytes);
            var hashString = Convert.ToBase64String(hashBytes)[..8]; // Use first 8 chars of hash
            _ = keyBuilder.Append('_');
            _ = keyBuilder.Append(hashString);
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


                _logger.CacheHit(cacheKey);
                return Task.FromResult<ICompiledKernel?>(entry.CompiledKernel);
            }

            // Remove expired entry

            _ = _cache.TryRemove(cacheKey, out _);
        }

        IncrementMissCount();
        _logger.CacheMiss(cacheKey);
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
        _logger.CacheStored(cacheKey);


        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> InvalidateAsync(string cacheKey)
    {
        var removed = _cache.TryRemove(cacheKey, out _);


        if (removed)
        {
            _logger.CacheInvalidated(cacheKey);
        }


        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<int> ClearAsync()
    {
        var count = _cache.Count;
        _cache.Clear();


        _logger.CacheCleared(count);
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
        await Task.CompletedTask; // Make async
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
                        _logger.CachePrewarmNeeded(kernel.Name, accelerator.Info.DeviceType);
                        // Note: Actual pre-warming would require compilation which should be done by the service layer
                    }
                    else
                    {
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.CachePrewarmFailed(kernel.Name, ex);
                }
            }
        }

        return successCount;
    }

    private void EvictLeastRecentlyUsed()
    {
        if (_cache.IsEmpty)
        {
            return;
        }


        var lruEntry = _cache
            .OrderBy(kvp => kvp.Value.LastAccessedAt)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(lruEntry.Key))
        {
            _ = _cache.TryRemove(lruEntry.Key, out _);
            _logger.CacheEvicted(lruEntry.Key);
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
            _ = _cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.CacheExpiredCleaned(expiredKeys.Count);
        }
    }

    private long EstimateCacheSize()
    {
        long totalSize = 0;

        foreach (var entry in _cache.Values)
        {
            var kernel = entry.CompiledKernel;

            // Base overhead per cache entry: object headers, dictionary slot, timestamps, etc.
            long entrySize = 512;

            // Kernel name contributes to in-memory footprint (2 bytes per char for .NET strings)
            entrySize += (kernel.Name?.Length ?? 0) * 2;

            // Backend-specific compiled kernel size estimate:
            // CUDA kernels include PTX/CUBIN binary data, typically larger
            // CPU kernels are managed code references, smaller footprint
            var backendType = kernel is Abstractions.Interfaces.Kernels.ICompiledKernel typedKernel
                ? typedKernel.BackendType
                : null;
            entrySize += backendType switch
            {
                "CUDA" => 20 * 1024,       // ~20KB for PTX + CUBIN binary
                "Metal" => 16 * 1024,       // ~16KB for MSL compiled shaders
                "OpenCL" => 12 * 1024,      // ~12KB for SPIR-V / CL binary
                "CPU" => 4 * 1024,          // ~4KB for managed code references
                _ => 8 * 1024               // ~8KB default estimate
            };

            // Compilation time metadata overhead
            entrySize += 64;

            totalSize += entrySize;
        }

        return totalSize;
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
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _cleanupTimer?.Dispose();
        _cache.Clear();
        _statisticsLock?.Dispose();


        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private class CacheEntry
    {
        /// <summary>
        /// Gets or sets the compiled kernel.
        /// </summary>
        /// <value>The compiled kernel.</value>
        public required ICompiledKernel CompiledKernel { get; init; }
        /// <summary>
        /// Gets or sets the created at.
        /// </summary>
        /// <value>The created at.</value>
        public required DateTime CreatedAt { get; init; }
        /// <summary>
        /// Gets or sets the last accessed at.
        /// </summary>
        /// <value>The last accessed at.</value>
        public DateTime LastAccessedAt { get; set; }
        /// <summary>
        /// Gets or sets the compilation time.
        /// </summary>
        /// <value>The compilation time.</value>
        public required TimeSpan CompilationTime { get; init; }
        /// <summary>
        /// Gets or sets the expires at.
        /// </summary>
        /// <value>The expires at.</value>
        public required DateTime ExpiresAt { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether expired.
        /// </summary>
        /// <value>The is expired.</value>

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        /// <summary>
        /// Updates the last access.
        /// </summary>

        public void UpdateLastAccess() => LastAccessedAt = DateTime.UtcNow;
    }
}
