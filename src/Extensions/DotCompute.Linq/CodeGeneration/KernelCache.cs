using DotCompute.Abstractions;
using DotCompute.Linq.CodeGeneration;
// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Thread-safe cache for compiled kernel delegates with LRU eviction and TTL support.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses a <see cref="ReaderWriterLockSlim"/> to provide thread-safe
/// concurrent access with minimal contention. Multiple readers can access the cache
/// simultaneously, while writers acquire exclusive locks.
/// </para>
/// <para>
/// The cache enforces both entry count limits and memory limits. When either limit is
/// exceeded, the least recently used (LRU) entries are evicted. Additionally, entries
/// are automatically removed when their TTL expires.
/// </para>
/// <para>
/// Memory estimation is performed using heuristics based on delegate metadata. While not
/// exact, these estimates are sufficient for preventing unbounded memory growth.
/// </para>
/// </remarks>
public sealed class KernelCache : IKernelCache
{
    private readonly Dictionary<string, CacheEntry> _cache;
    private readonly ReaderWriterLockSlim _lock;
    private readonly int _maxEntries;
    private readonly long _maxMemoryBytes;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _cleanupInterval;

    // Statistics - use Interlocked for thread-safe updates
    private long _hits;
    private long _misses;
    private long _evictions;
    private long _sequenceNumber; // For stable LRU ordering

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelCache"/> class.
    /// </summary>
    /// <param name="maxEntries">
    /// Maximum number of entries to cache. Default is 1000.
    /// </param>
    /// <param name="maxMemoryBytes">
    /// Maximum estimated memory usage in bytes. Default is 100MB.
    /// </param>
    /// <param name="cleanupInterval">
    /// Interval for automatic cleanup of expired entries. Default is 60 seconds.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxEntries"/> is less than 1 or
    /// <paramref name="maxMemoryBytes"/> is less than 1.
    /// </exception>
    public KernelCache(
        int maxEntries = 1000,
        long maxMemoryBytes = 100 * 1024 * 1024,
        TimeSpan? cleanupInterval = null)
    {
        if (maxEntries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "Must be at least 1");
        }

        if (maxMemoryBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMemoryBytes), "Must be at least 1");
        }

        _maxEntries = maxEntries;
        _maxMemoryBytes = maxMemoryBytes;
        _cache = new Dictionary<string, CacheEntry>(maxEntries);
        _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        _cleanupInterval = cleanupInterval ?? TimeSpan.FromSeconds(60);

        // Start background cleanup timer
        _cleanupTimer = new Timer(
            callback: _ => RemoveExpiredEntries(),
            state: null,
            dueTime: _cleanupInterval,
            period: _cleanupInterval);
    }

    /// <inheritdoc/>
    public Delegate? GetCached(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(key);

        // Use upgradeable read lock to simplify lock management
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (!_cache.TryGetValue(key, out var entry))
            {
                IncrementMiss();
                return null;
            }

            // Check if entry has expired
            if (IsExpired(entry))
            {
                // Upgrade to write lock to remove expired entry
                _lock.EnterWriteLock();
                try
                {
                    // Double-check after acquiring write lock
                    if (_cache.TryGetValue(key, out entry) && IsExpired(entry))
                    {
                        _cache.Remove(key);
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }

                IncrementMiss();
                return null;
            }

            // Capture the delegate reference before upgrading lock
            var delegateRef = entry.CompiledDelegate;

            // Update access statistics with write lock
            _lock.EnterWriteLock();
            try
            {
                // Re-check entry still exists and valid after lock upgrade
                if (_cache.TryGetValue(key, out entry) && !IsExpired(entry))
                {
                    entry.LastAccessTime = DateTime.UtcNow;
                    entry.AccessCount++;
                    // NOTE: Don't update SequenceNumber - it represents insertion order, not access order
                    // LastAccessTime already tracks most recent access for LRU
                    IncrementHit();
                    return entry.CompiledDelegate;
                }

                // Entry was removed/expired during lock upgrade
                // But we already validated it was valid earlier, so this is a race condition
                // We can still return the delegate we captured earlier and count it as a hit
                // since it was valid when we checked
                IncrementHit();
                return delegateRef;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    /// <inheritdoc/>
    public void Store(string key, Delegate compiled, TimeSpan ttl)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(compiled);

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive");
        }

        _lock.EnterWriteLock();
        try
        {
            // Create new cache entry with sequence number for stable LRU ordering
            // Use a large increment to ensure newly stored entries have much higher sequence numbers
            // than accessed entries, making them less likely to be evicted immediately
            var sequenceNum = Interlocked.Add(ref _sequenceNumber, 1000);

            // Set LastAccessTime to future to protect from immediate eviction in concurrent scenarios
            // This prevents race conditions where a newly stored entry is evicted by another thread
            // before the storing thread can access it. The entry will "age" to current time after 1 second.
            var protectionPeriod = TimeSpan.FromSeconds(1);
            var protectedLastAccessTime = DateTime.UtcNow.Add(protectionPeriod);

            var entry = new CacheEntry
            {
                CompiledDelegate = compiled,
                ExpirationTime = DateTime.UtcNow.Add(ttl),
                LastAccessTime = protectedLastAccessTime,
                AccessCount = 0,
                EstimatedSizeBytes = EstimateSize(compiled),
                SequenceNumber = sequenceNum
            };

            // Store or update entry FIRST
            _cache[key] = entry;

            // Then check if we need to evict entries (excluding the one we just added)
            if (_cache.Count > _maxEntries || WillExceedMemoryLimit(0))
            {
                EvictLeastRecentlyUsedExcluding(Math.Max(1, _maxEntries / 10), key); // Evict 10% of entries, excluding newly added
            }

            // Final memory pressure check (also exclude the just-added entry)
            CheckMemoryPressure(key);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public bool Remove(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(key);

        _lock.EnterWriteLock();
        try
        {
            return _cache.Remove(key);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _lock.EnterWriteLock();
        try
        {
            _cache.Clear();

            // Reset statistics
            Interlocked.Exchange(ref _hits, 0);
            Interlocked.Exchange(ref _misses, 0);
            Interlocked.Exchange(ref _evictions, 0);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public CacheStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _lock.EnterReadLock();
        try
        {
            return new CacheStatistics
            {
                Hits = Interlocked.Read(ref _hits),
                Misses = Interlocked.Read(ref _misses),
                CurrentEntries = _cache.Count,
                EstimatedMemoryBytes = _cache.Values.Sum(e => e.EstimatedSizeBytes),
                EvictionCount = Interlocked.Read(ref _evictions)
            };
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Generates a cache key from an operation graph, type metadata, and compilation options.
    /// </summary>
    /// <param name="graph">The operation graph to generate a key for.</param>
    /// <param name="metadata">Type metadata for the operation.</param>
    /// <param name="options">Compilation options.</param>
    /// <returns>A unique cache key as a Base64-encoded hash.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public static string GenerateCacheKey(
        OperationGraph graph,
        TypeMetadata metadata,
        CompilationOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(options);

        var sb = new StringBuilder(512);

        // Include operation types in order
        foreach (var operation in graph.Operations)
        {
            sb.Append(operation.Type.ToString());
            sb.Append('|');
            sb.Append(operation.Id);
            sb.Append('|');

            // Include dependencies
            foreach (var dep in operation.Dependencies)
            {
                sb.Append(dep);
                sb.Append(',');
            }
            sb.Append(';');
        }

        // Include type information
        sb.Append("IN:");
        sb.Append(metadata.InputType.FullName ?? metadata.InputType.Name);
        sb.Append("|OUT:");
        sb.Append(metadata.ResultType.FullName ?? metadata.ResultType.Name);
        sb.Append('|');

        // Include compilation options
        sb.Append("BACKEND:");
        sb.Append(options.CompilerBackend ?? "default");
        sb.Append("|ARCH:");
        sb.Append(options.TargetArchitecture ?? "default");
        sb.Append("|OPT:");
        sb.Append(options.OptimizationLevel);
        sb.Append("|DEBUG:");
        sb.Append(options.GenerateDebugInfo);
        sb.Append("|FUSION:");
        sb.Append(options.EnableOperatorFusion);
        sb.Append("|CUBIN:");
        sb.Append(options.CompileToCubin);

        // Hash the key for consistent length
        var keyBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hashBytes = SHA256.HashData(keyBytes);

        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Evicts the specified number of least recently used entries.
    /// </summary>
    /// <param name="count">Number of entries to evict.</param>
    /// <remarks>
    /// Must be called while holding a write lock.
    /// Uses sequence number as secondary sort for stable ordering when access times are equal.
    /// </remarks>
    private void EvictLeastRecentlyUsed(int count)
    {
        EvictLeastRecentlyUsedExcluding(count, null);
    }

    /// <summary>
    /// Evicts the specified number of least recently used entries, excluding a specific key.
    /// </summary>
    /// <param name="count">Number of entries to evict.</param>
    /// <param name="excludeKey">Key to exclude from eviction (e.g., just-added entry).</param>
    /// <remarks>
    /// Must be called while holding a write lock.
    /// Uses sequence number as secondary sort for stable ordering when access times are equal.
    /// </remarks>
    private void EvictLeastRecentlyUsedExcluding(int count, string? excludeKey)
    {
        if (count <= 0 || _cache.Count == 0)
        {
            return;
        }

        // Sort by last access time (oldest first), then by sequence number (oldest first)
        // This ensures stable, deterministic LRU eviction
        var query = _cache.AsEnumerable();

        // Exclude the specified key if provided
        if (excludeKey != null)
        {
            query = query.Where(kvp => kvp.Key != excludeKey);
        }

        var toEvict = query
            .OrderBy(kvp => kvp.Value.LastAccessTime)
            .ThenBy(kvp => kvp.Value.SequenceNumber)
            .Take(count)
            .Select(kvp => kvp.Key)
            .ToList(); // Materialize to avoid collection modification issues

        foreach (var key in toEvict)
        {
            _cache.Remove(key);
            Interlocked.Increment(ref _evictions);
        }
    }

    /// <summary>
    /// Checks if adding the specified memory would exceed the limit.
    /// </summary>
    /// <param name="additionalBytes">Additional bytes to check.</param>
    /// <returns>True if the limit would be exceeded; otherwise, false.</returns>
    /// <remarks>
    /// Must be called while holding at least a read lock.
    /// </remarks>
    private bool WillExceedMemoryLimit(long additionalBytes)
    {
        var currentMemory = _cache.Values.Sum(e => e.EstimatedSizeBytes);
        return (currentMemory + additionalBytes) > _maxMemoryBytes;
    }

    /// <summary>
    /// Checks memory pressure and evicts entries if necessary.
    /// </summary>
    /// <param name="excludeKey">Optional key to exclude from eviction (e.g., just-added entry).</param>
    /// <remarks>
    /// Must be called while holding a write lock.
    /// </remarks>
    private void CheckMemoryPressure(string? excludeKey = null)
    {
        var totalMemory = _cache.Values.Sum(e => e.EstimatedSizeBytes);

        if (totalMemory > _maxMemoryBytes)
        {
            // Evict 25% of entries to get back under the limit
            var entriesToEvict = Math.Max(1, _cache.Count / 4);
            EvictLeastRecentlyUsedExcluding(entriesToEvict, excludeKey);
        }

        // Also check system memory pressure
        var gcInfo = GC.GetGCMemoryInfo();
        if (gcInfo.MemoryLoadBytes > (gcInfo.TotalAvailableMemoryBytes * 0.9)) // 90% memory usage
        {
            // Under high system memory pressure, evict more aggressively
            var entriesToEvict = Math.Max(1, _cache.Count / 2);
            EvictLeastRecentlyUsedExcluding(entriesToEvict, excludeKey);
        }
    }

    /// <summary>
    /// Removes all expired entries from the cache.
    /// </summary>
    /// <remarks>
    /// This method is called periodically by a background timer and
    /// acquires a write lock internally.
    /// </remarks>
    private void RemoveExpiredEntries()
    {
        if (_disposed)
        {
            return;
        }

        _lock.EnterWriteLock();
        try
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _cache
                .Where(kvp => IsExpired(kvp.Value, now))
                .Select(kvp => kvp.Key)
                .ToList(); // Materialize to avoid collection modification issues

            foreach (var key in expiredKeys)
            {
                _cache.Remove(key);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Checks if a cache entry has expired.
    /// </summary>
    /// <param name="entry">The entry to check.</param>
    /// <param name="now">Optional current time (for consistency in batch operations).</param>
    /// <returns>True if expired; otherwise, false.</returns>
    private static bool IsExpired(CacheEntry entry, DateTime? now = null)
    {
        var currentTime = now ?? DateTime.UtcNow;

        // Entry is expired if current time is past the expiration time
        return currentTime >= entry.ExpirationTime;
    }

    /// <summary>
    /// Estimates the memory size of a compiled delegate.
    /// </summary>
    /// <param name="compiled">The delegate to estimate.</param>
    /// <returns>Estimated size in bytes.</returns>
    /// <remarks>
    /// <para>
    /// This is a heuristic estimate based on delegate metadata. The actual memory
    /// usage may vary, but this provides a reasonable approximation for cache management.
    /// </para>
    /// <para>
    /// The estimate includes:
    /// - Fixed overhead for delegate object (64 bytes)
    /// - IL code size from method body
    /// - Estimated closure size (if any captured variables)
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "Method body inspection is best-effort and gracefully falls back to conservative estimates if unavailable.")]
    private static long EstimateSize(Delegate compiled)
    {
        const long DelegateOverhead = 64; // Base object overhead
        const long ClosureEstimate = 128; // Estimate for captured variables

        var estimatedSize = DelegateOverhead;

        try
        {
            var method = compiled.Method;

            // Try to get IL code size as a proxy for method complexity
            var methodBody = method.GetMethodBody();
            if (methodBody != null)
            {
                estimatedSize += methodBody.GetILAsByteArray()?.Length ?? 256;
            }
            else
            {
                // Default estimate for methods without body info
                estimatedSize += 256;
            }

            // If delegate has a target (closure), add estimate for captured variables
            if (compiled.Target != null)
            {
                estimatedSize += ClosureEstimate;
            }
        }
        catch
        {
            // If we can't get method info, use conservative estimate
            estimatedSize += 512;
        }

        return estimatedSize;
    }

    /// <summary>
    /// Increments the cache hit counter in a thread-safe manner.
    /// </summary>
    private void IncrementHit()
    {
        Interlocked.Increment(ref _hits);
    }

    /// <summary>
    /// Increments the cache miss counter in a thread-safe manner.
    /// </summary>
    private void IncrementMiss()
    {
        Interlocked.Increment(ref _misses);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop cleanup timer
        _cleanupTimer.Dispose();

        // Clear cache and dispose lock
        _lock.EnterWriteLock();
        try
        {
            _cache.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        _lock.Dispose();
    }

    /// <summary>
    /// Represents a single cache entry with metadata.
    /// </summary>
    private sealed class CacheEntry
    {
        /// <summary>
        /// Gets or sets the compiled delegate.
        /// </summary>
        public required Delegate CompiledDelegate { get; set; }

        /// <summary>
        /// Gets or sets the expiration time (UTC).
        /// </summary>
        public required DateTime ExpirationTime { get; set; }

        /// <summary>
        /// Gets or sets the last access time (UTC).
        /// </summary>
        public required DateTime LastAccessTime { get; set; }

        /// <summary>
        /// Gets or sets the number of times this entry has been accessed.
        /// </summary>
        public required int AccessCount { get; set; }

        /// <summary>
        /// Gets or sets the estimated memory size in bytes.
        /// </summary>
        public required long EstimatedSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the sequence number for stable LRU ordering.
        /// Higher numbers indicate more recent access/creation.
        /// </summary>
        public required long SequenceNumber { get; set; }
    }
}
