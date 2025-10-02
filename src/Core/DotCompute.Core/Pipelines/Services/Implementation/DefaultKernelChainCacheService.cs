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
        /// <summary>
        /// Initializes a new instance of the DefaultKernelChainCacheService class.
        /// </summary>
        /// <param name="logger">The logger.</param>

        public DefaultKernelChainCacheService(ILogger<DefaultKernelChainCacheService>? logger = null)
        {
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = 256 * 1024 * 1024 // 256 MB limit
            });
            _logger = logger;
        }
        /// <summary>
        /// Gets the async.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The async.</returns>

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (_cache.TryGetValue(key, out var value) && value is T typedValue)
            {
                _ = Interlocked.Increment(ref _hits);
                _logger?.LogDebug("Cache hit for key '{Key}'", key);
                await Task.CompletedTask;
                return typedValue;
            }

            _ = Interlocked.Increment(ref _misses);
            _logger?.LogDebug("Cache miss for key '{Key}'", key);
            await Task.CompletedTask;
            return null;
        }
        /// <summary>
        /// Sets the async.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="ttl">The ttl.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class
        {
            var options = new MemoryCacheEntryOptions();


            if (ttl.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = ttl;
            }


            options.Size = 1; // Simple size tracking


            _ = _cache.Set(key, value, options);
            _logger?.LogDebug("Cached value for key '{Key}' with TTL {TTL}", key, ttl);
            await Task.CompletedTask;
        }
        /// <summary>
        /// Deletes the async.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _cache.Remove(key);
            _logger?.LogDebug("Removed cache entry for key '{Key}'", key);
            await Task.CompletedTask;
        }
        /// <summary>
        /// Gets clear asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Clear();
            }
            _logger?.LogDebug("Cleared all cache entries");
            await Task.CompletedTask;
        }
        /// <summary>
        /// Gets the statistics async.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The statistics async.</returns>

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
        /// <summary>
        /// Performs dispose.
        /// </summary>

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
