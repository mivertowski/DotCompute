// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Caching;

/// <summary>
/// Cache policies for managing pipeline result caching.
/// </summary>
/// <remarks>
/// <para>
/// Abstract base class for cache eviction policies that determine when
/// cached items should be removed from memory.
/// </para>
/// <para>
/// Implementations define specific algorithms (LRU, TTL, etc.) for
/// balancing cache hit rates with memory usage.
/// </para>
/// </remarks>
public abstract class CachePolicy
{
    /// <summary>Default cache policy with reasonable defaults.</summary>
    /// <remarks>
    /// Provides LRU eviction with 100 item capacity and 1-hour TTL.
    /// Suitable for most general-purpose caching scenarios.
    /// </remarks>
    public static CachePolicy Default => new LRUCachePolicy(maxSize: 100, maxAge: TimeSpan.FromHours(1));

    /// <summary>Determines if a cached item should be evicted.</summary>
    /// <param name="entry">Cache entry to evaluate.</param>
    /// <returns>True if entry should be evicted, false otherwise.</returns>
    public abstract bool ShouldEvict(ICacheEntry entry);

    /// <summary>Gets the priority for cache eviction (higher = keep longer).</summary>
    /// <param name="entry">Cache entry to evaluate.</param>
    /// <returns>Eviction priority score. Higher values are retained longer.</returns>
    public abstract int GetEvictionPriority(ICacheEntry entry);
}

/// <summary>
/// LRU (Least Recently Used) cache policy implementation.
/// </summary>
/// <remarks>
/// <para>
/// Evicts least recently accessed items first, based on the principle that
/// recently used items are more likely to be used again soon (temporal locality).
/// </para>
/// <para>
/// Combines LRU eviction with optional age-based expiration for better
/// control over stale data removal.
/// </para>
/// </remarks>
/// <param name="maxSize">Maximum number of items to keep in cache.</param>
/// <param name="maxAge">Optional maximum age for cached items.</param>
public class LRUCachePolicy(int maxSize, TimeSpan? maxAge = null) : CachePolicy
{
    /// <summary>Maximum number of items to keep in cache.</summary>
    public int MaxSize { get; } = maxSize;

    /// <summary>Maximum age for cached items.</summary>
    public TimeSpan? MaxAge { get; } = maxAge;

    /// <inheritdoc/>
    public override bool ShouldEvict(ICacheEntry entry)
    {
        return MaxAge.HasValue && DateTimeOffset.UtcNow - entry.CreatedAt > MaxAge.Value;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns negative time since last access, so older (less recently used)
    /// items have lower (more negative) priority and are evicted first.
    /// </remarks>
    public override int GetEvictionPriority(ICacheEntry entry)
    {
        var timeSinceAccess = DateTimeOffset.UtcNow - entry.LastAccessedAt;
        return -(int)timeSinceAccess.TotalSeconds; // Negative so older = lower priority
    }
}

/// <summary>
/// Time-to-live cache policy implementation.
/// </summary>
/// <remarks>
/// <para>
/// Evicts items after a fixed TTL period regardless of access patterns.
/// Simple and predictable, ideal for time-sensitive data.
/// </para>
/// <para>
/// TTL is measured from creation time, not last access time.
/// </para>
/// </remarks>
/// <param name="timeToLive">Time-to-live for cached items.</param>
public class TTLCachePolicy(TimeSpan timeToLive) : CachePolicy
{
    /// <summary>Time-to-live for cached items.</summary>
    public TimeSpan TimeToLive { get; } = timeToLive;

    /// <inheritdoc/>
    public override bool ShouldEvict(ICacheEntry entry)
    {
        return DateTimeOffset.UtcNow - entry.CreatedAt > TimeToLive;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns time remaining until expiry. Items closer to expiration
    /// have lower priority and are evicted first.
    /// </remarks>
    public override int GetEvictionPriority(ICacheEntry entry)
    {
        var timeUntilExpiry = TimeToLive - (DateTimeOffset.UtcNow - entry.CreatedAt);
        return (int)timeUntilExpiry.TotalSeconds;
    }
}

/// <summary>
/// Represents a cache entry with metadata.
/// </summary>
/// <remarks>
/// <para>
/// Encapsulates a cached value along with access tracking metadata
/// used by cache policies for eviction decisions.
/// </para>
/// <para>
/// Implementations should be thread-safe for concurrent cache access.
/// </para>
/// </remarks>
public interface ICacheEntry
{
    /// <summary>Unique key for the cache entry.</summary>
    public string Key { get; }

    /// <summary>Cached value.</summary>
    public object Value { get; }

    /// <summary>Size of the cached value in bytes.</summary>
    /// <remarks>
    /// Used for memory-based eviction policies. May be approximate.
    /// </remarks>
    public long Size { get; }

    /// <summary>When the entry was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When the entry was last accessed.</summary>
    /// <remarks>
    /// Updated on each cache hit. Setter allows LRU tracking.
    /// </remarks>
    public DateTimeOffset LastAccessedAt { get; set; }

    /// <summary>Number of times the entry has been accessed.</summary>
    /// <remarks>
    /// Useful for LFU (Least Frequently Used) policies and popularity tracking.
    /// </remarks>
    public long AccessCount { get; set; }

    /// <summary>Metadata associated with the cache entry.</summary>
    /// <remarks>
    /// Extensibility point for custom cache policies and diagnostics.
    /// </remarks>
    public IReadOnlyDictionary<string, object> Metadata { get; }
}
