// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using global::System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Logging;

namespace DotCompute.Linq.Execution;


/// <summary>
/// Implements caching for compiled query plans and results.
/// </summary>
public class QueryCache : IQueryCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ILogger<QueryCache> _logger;
    private readonly QueryCacheOptions _options;
    private long _hits;
    private long _misses;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCache"/> class.
    /// </summary>
    /// <param name="options">The cache options.</param>
    /// <param name="logger">The logger instance.</param>
    public QueryCache(QueryCacheOptions options, ILogger<QueryCache> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string GenerateKey(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Generate a unique key based on the expression structure
        var keyBuilder = new ExpressionKeyBuilder();
        var keyString = keyBuilder.BuildKey(expression);

        // Hash the key for consistent length
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(keyString));
        var key = Convert.ToBase64String(hashBytes);

        _logger.LogDebugMessage($"Generated cache key {key} for expression type {expression.NodeType}");

        return key;
    }

    /// <inheritdoc/>
    public bool TryGet(string key, out object? result)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_cache.TryGetValue(key, out var entry))
        {
            // Check if entry has expired
            if (_options.EnableExpiration && DateTime.UtcNow > entry.ExpirationTime)
            {
                _logger.LogDebugMessage("Cache entry {key} has expired");
                _ = _cache.TryRemove(key, out _);
                result = null;
                _ = Interlocked.Increment(ref _misses);
                return false;
            }

            // Update access time for LRU
            entry.LastAccessTime = DateTime.UtcNow;
            entry.AccessCount++;

            result = entry.Value;
            _ = Interlocked.Increment(ref _hits);

            _logger.LogDebugMessage("Cache hit for key {key}");
            return true;
        }

        result = null;
        _ = Interlocked.Increment(ref _misses);

        _logger.LogDebugMessage("Cache miss for key {key}");
        return false;
    }

    /// <inheritdoc/>
    public void Set(string key, object? result)
    {
        ArgumentNullException.ThrowIfNull(key);

        // Check cache size limit
        if (_options.MaxEntries > 0 && _cache.Count >= _options.MaxEntries)
        {
            EvictLeastRecentlyUsed();
        }

        var entry = new CacheEntry
        {
            Key = key,
            Value = result,
            CreationTime = DateTime.UtcNow,
            LastAccessTime = DateTime.UtcNow,
            ExpirationTime = _options.EnableExpiration
                ? DateTime.UtcNow.Add(_options.DefaultExpiration)
                : DateTime.MaxValue,
            Size = EstimateObjectSize(result)
        };

        _cache[key] = entry;

        _logger.LogDebugMessage("Cached result for key {Key}, size: {key, entry.Size} bytes");
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _cache.Clear();
        _hits = 0;
        _misses = 0;

        _logger.LogInfoMessage("Query cache cleared");
    }

    /// <inheritdoc/>
    public CacheStatistics GetStatistics()
    {
        var totalSize = _cache.Values.Sum(e => e.Size);

        return new CacheStatistics
        {
            Hits = _hits,
            Misses = _misses,
            EntryCount = _cache.Count,
            TotalSizeBytes = totalSize
        };
    }

    private void EvictLeastRecentlyUsed()
    {
        if (_cache.IsEmpty)
        {
            return;
        }

        // Find the least recently used entry
        var lruEntry = _cache.Values
        .OrderBy(e => e.LastAccessTime)
        .ThenBy(e => e.AccessCount)
        .FirstOrDefault();

        if (lruEntry != null)
        {
            _ = _cache.TryRemove(lruEntry.Key, out _);
            _logger.LogDebugMessage("Evicted cache entry {lruEntry.Key} (LRU)");
        }
    }

    private static long EstimateObjectSize(object? obj)
    {
        if (obj == null)
        {
            return 0;
        }

        var type = obj.GetType();

        // Estimate size based on type
        if (type.IsPrimitive)
        {
            return type.Name switch
            {
                "Boolean" => sizeof(bool),
                "Byte" => sizeof(byte),
                "SByte" => sizeof(sbyte),
                "Int16" => sizeof(short),
                "UInt16" => sizeof(ushort),
                "Int32" => sizeof(int),
                "UInt32" => sizeof(uint),
                "Int64" => sizeof(long),
                "UInt64" => sizeof(ulong),
                "Single" => sizeof(float),
                "Double" => sizeof(double),
                "Decimal" => sizeof(decimal),
                "Char" => sizeof(char),
                _ => IntPtr.Size
            };
        }

        if (type == typeof(string))
        {
            return ((string)obj).Length * sizeof(char) + IntPtr.Size;
        }

        if (type.IsArray)
        {
            var array = (Array)obj;
            var elementType = type.GetElementType()!;
            var elementSize = EstimateObjectSize(Activator.CreateInstance(elementType));
            return array.Length * elementSize + IntPtr.Size;
        }

        // For complex objects, use a rough estimate
        return 1024; // 1KB default
    }

    /// <summary>
    /// Represents a cached entry.
    /// </summary>
    private class CacheEntry
    {
        public string Key { get; set; } = string.Empty;
        public object? Value { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public DateTime ExpirationTime { get; set; }
        public int AccessCount { get; set; }
        public long Size { get; set; }
    }

    /// <summary>
    /// Builds unique keys for expression trees.
    /// </summary>
    private class ExpressionKeyBuilder : ExpressionVisitor
    {
        private readonly StringBuilder _keyBuilder = new();

        public string BuildKey(Expression expression)
        {
            _ = _keyBuilder.Clear();
            _ = Visit(expression);
            return _keyBuilder.ToString();
        }

        public override Expression? Visit(Expression? node)
        {
            if (node == null)
            {
                _ = _keyBuilder.Append("null");
                return null;
            }

            _ = _keyBuilder.Append($"{node.NodeType}:{node.Type.Name}:");
            return base.Visit(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            _ = _keyBuilder.Append($"Const:{node.Value?.GetHashCode() ?? 0}:");
            return base.VisitConstant(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            _ = _keyBuilder.Append($"Param:{node.Name}:");
            return base.VisitParameter(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            _ = _keyBuilder.Append($"Member:{node.Member.Name}:");
            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            _ = _keyBuilder.Append(System.Globalization.CultureInfo.InvariantCulture, $"Method:{node.Method.Name}:");
            foreach (var arg in node.Arguments)
            {
                _ = Visit(arg);
            }
            return node;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            _ = _keyBuilder.Append($"Binary:{node.NodeType}:");
            _ = Visit(node.Left);
            _ = Visit(node.Right);
            return node;
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            _ = _keyBuilder.Append($"Unary:{node.NodeType}:");
            return base.VisitUnary(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            _ = _keyBuilder.Append($"Lambda:{node.Parameters.Count}:");
            foreach (var param in node.Parameters)
            {
                _ = Visit(param);
            }
            _ = Visit(node.Body);
            return node;
        }
    }
}

/// <summary>
/// Configuration options for the query cache.
/// </summary>
public class QueryCacheOptions
{
    /// <summary>
    /// Gets or sets the maximum number of entries in the cache.
    /// </summary>
    public int MaxEntries { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether to enable expiration.
    /// </summary>
    public bool EnableExpiration { get; set; } = true;

    /// <summary>
    /// Gets or sets the default expiration time for cache entries.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the maximum total size of the cache in bytes.
    /// </summary>
    public long MaxSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Gets or sets a value indicating whether to enable cache statistics.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;
}
