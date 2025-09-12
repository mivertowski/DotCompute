// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Pipelines.Services.Implementation
{
    /// <summary>
    /// Default implementation of kernel chain cache service using in-memory caching.
    /// </summary>
    public sealed class DefaultKernelChainCacheService : IKernelChainCacheService, IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<DefaultKernelChainCacheService>? _logger;
        private long _hits;
        private long _misses;
        private bool _disposed;

        public DefaultKernelChainCacheService(ILogger<DefaultKernelChainCacheService>? logger = null)
        {
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = 256 * 1024 * 1024 // 256 MB limit
            });
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (_cache.TryGetValue(key, out var value) && value is T typedValue)
            {
                Interlocked.Increment(ref _hits);
                _logger?.LogDebug("Cache hit for key '{Key}'", key);
                await Task.CompletedTask;
                return typedValue;
            }

            Interlocked.Increment(ref _misses);
            _logger?.LogDebug("Cache miss for key '{Key}'", key);
            await Task.CompletedTask;
            return null;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class
        {
            var options = new MemoryCacheEntryOptions();


            if (ttl.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = ttl;
            }


            options.Size = 1; // Simple size tracking


            _cache.Set(key, value, options);
            _logger?.LogDebug("Cached value for key '{Key}' with TTL {TTL}", key, ttl);
            await Task.CompletedTask;
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _cache.Remove(key);
            _logger?.LogDebug("Removed cache entry for key '{Key}'", key);
            await Task.CompletedTask;
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Clear();
            }
            _logger?.LogDebug("Cleared all cache entries");
            await Task.CompletedTask;
        }

        public async Task<KernelChainCacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new KernelChainCacheStatistics
            {
                TotalHits = _hits,
                TotalMisses = _misses,
                TotalMemoryUsed = GC.GetTotalMemory(false), // Approximation
                EntryCount = 0, // Not easily available from IMemoryCache
                ExpiredEntriesRemoved = 0 // Not tracked in this implementation
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cache.Dispose();
                _disposed = true;
            }
        }
    }
}
