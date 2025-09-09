// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using DotCompute.Linq.Execution;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Services;

/// <summary>
/// Default implementation of query cache using in-memory caching.
/// </summary>
public class DefaultQueryCache : IQueryCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<DefaultQueryCache> _logger;
    private readonly QueryCacheOptions _options;
    private long _hits;
    private long _misses;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultQueryCache"/> class.
    /// </summary>
    /// <param name="options">Cache options</param>
    /// <param name="logger">Logger instance</param>
    public DefaultQueryCache(QueryCacheOptions options, ILogger<DefaultQueryCache> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var memoryCacheOptions = new MemoryCacheOptions
        {
            SizeLimit = options.MaxEntries
        };
        
        _cache = new MemoryCache(memoryCacheOptions);
    }

    /// <inheritdoc/>
    public string GenerateKey(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        
        // Generate a hash of the expression string representation
        var expressionString = expression.ToString();
        var bytes = Encoding.UTF8.GetBytes(expressionString);
        var hash = SHA256.HashData(bytes);
        
        return Convert.ToHexString(hash)[..16]; // Use first 16 characters for reasonable key length
    }

    /// <inheritdoc/>
    public bool TryGet(string key, out object? result)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        if (_cache.TryGetValue(key, out result))
        {
            Interlocked.Increment(ref _hits);
            _logger.LogDebug("Cache hit for key {Key}", key);
            return true;
        }

        Interlocked.Increment(ref _misses);
        _logger.LogDebug("Cache miss for key {Key}", key);
        result = null;
        return false;
    }

    /// <inheritdoc/>
    public void Set(string key, object? result)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        var entryOptions = new MemoryCacheEntryOptions
        {
            Size = 1, // Each entry counts as 1 towards the size limit
            SlidingExpiration = _options.EnableExpiration ? _options.DefaultExpiration : null
        };

        _cache.Set(key, result, entryOptions);
        _logger.LogDebug("Cached result for key {Key}", key);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (_cache is MemoryCache memCache)
        {
            memCache.Dispose();
            
            var memoryCacheOptions = new MemoryCacheOptions
            {
                SizeLimit = _options.MaxEntries
            };
            
            // Note: This is a simplified clear implementation
            // In a real implementation, you'd want to properly reinitialize the cache
        }
        
        _logger.LogInformation("Query cache cleared");
    }

    /// <inheritdoc/>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            Hits = _hits,
            Misses = _misses,
            EntryCount = GetEntryCount(),
            TotalSizeBytes = EstimateSize()
        };
    }

    private int GetEntryCount()
    {
        // MemoryCache doesn't expose count directly
        // This is a simplified implementation
        return 0; // Would need reflection or custom tracking for exact count
    }

    private long EstimateSize()
    {
        // Rough estimation - in real implementation would track actual memory usage
        return GetEntryCount() * 1024; // Assume 1KB per entry average
    }

    /// <summary>
    /// Disposes the cache.
    /// </summary>
    public void Dispose()
    {
        _cache.Dispose();
    }
}

/// <summary>
/// Options for configuring the query cache.
/// </summary>
public class QueryCacheOptions
{
    /// <summary>
    /// Gets or sets the maximum number of cache entries.
    /// </summary>
    public int MaxEntries { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether cache expiration is enabled.
    /// </summary>
    public bool EnableExpiration { get; set; } = true;

    /// <summary>
    /// Gets or sets the default cache expiration time.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(1);
}