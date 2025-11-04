// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Compilation;

/// <summary>
/// Thread-safe compilation cache for OpenCL kernel binaries.
/// Provides both in-memory and disk-based caching with LRU eviction policy.
/// </summary>
public sealed class OpenCLCompilationCache : IDisposable
{
    private readonly ConcurrentDictionary<string, CachedProgram> _memoryCache = new();
    private readonly SemaphoreSlim _diskLock = new(1, 1);
    private readonly ILogger<OpenCLCompilationCache>? _logger;
    private readonly string _diskCachePath;
    private readonly int _maxMemoryCacheSize;
    private readonly TimeSpan _memoryCacheTTL;
    private readonly TimeSpan _diskCacheTTL;

    // Statistics tracking (thread-safe using Interlocked operations)
    private long _memoryHits;
    private long _diskHits;
    private long _misses;

    private bool _disposed;

    /// <summary>
    /// Default maximum number of entries in the memory cache.
    /// </summary>
    public const int DefaultMaxMemoryCacheSize = 100;

    /// <summary>
    /// Default memory cache TTL (1 hour).
    /// </summary>
    public static readonly TimeSpan DefaultMemoryCacheTTL = TimeSpan.FromHours(1);

    /// <summary>
    /// Default disk cache TTL (7 days).
    /// </summary>
    public static readonly TimeSpan DefaultDiskCacheTTL = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets the singleton instance of the cache.
    /// </summary>
    public static OpenCLCompilationCache Instance { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLCompilationCache"/> class
    /// with default settings.
    /// </summary>
    public OpenCLCompilationCache()
        : this(null, null, DefaultMaxMemoryCacheSize, DefaultMemoryCacheTTL, DefaultDiskCacheTTL)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLCompilationCache"/> class
    /// with a logger.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public OpenCLCompilationCache(ILogger<OpenCLCompilationCache> logger)
        : this(logger, null, DefaultMaxMemoryCacheSize, DefaultMemoryCacheTTL, DefaultDiskCacheTTL)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLCompilationCache"/> class
    /// with custom settings.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="diskCachePath">Custom disk cache path. If null, uses default (~/.dotcompute/opencl/cache/).</param>
    /// <param name="maxMemoryCacheSize">Maximum number of entries in memory cache.</param>
    /// <param name="memoryCacheTTL">Time-to-live for memory cache entries.</param>
    /// <param name="diskCacheTTL">Time-to-live for disk cache entries.</param>
    public OpenCLCompilationCache(
        ILogger<OpenCLCompilationCache>? logger,
        string? diskCachePath,
        int maxMemoryCacheSize,
        TimeSpan memoryCacheTTL,
        TimeSpan diskCacheTTL)
    {
        _logger = logger;
        _maxMemoryCacheSize = maxMemoryCacheSize > 0 ? maxMemoryCacheSize : DefaultMaxMemoryCacheSize;
        _memoryCacheTTL = memoryCacheTTL > TimeSpan.Zero ? memoryCacheTTL : DefaultMemoryCacheTTL;
        _diskCacheTTL = diskCacheTTL > TimeSpan.Zero ? diskCacheTTL : DefaultDiskCacheTTL;

        // Set up disk cache path
        if (string.IsNullOrWhiteSpace(diskCachePath))
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _diskCachePath = Path.Combine(homeDir, ".dotcompute", "opencl", "cache");
        }
        else
        {
            _diskCachePath = diskCachePath;
        }

        // Ensure cache directory exists
        try
        {
            Directory.CreateDirectory(_diskCachePath);
            _logger?.LogDebug("OpenCL cache directory: {CachePath}", _diskCachePath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create cache directory: {CachePath}", _diskCachePath);
        }
    }

    /// <summary>
    /// Attempts to retrieve a cached program binary.
    /// </summary>
    /// <param name="sourceCode">The kernel source code.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="deviceName">Target device name.</param>
    /// <returns>The cached binary if found; otherwise, null.</returns>
    public async Task<byte[]?> TryGetBinaryAsync(
        string sourceCode,
        CompilationOptions options,
        string deviceName)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code cannot be null or empty", nameof(sourceCode));
        }

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
        }

        var cacheKey = GenerateCacheKey(sourceCode, options, deviceName);

        // Try memory cache first
        if (_memoryCache.TryGetValue(cacheKey, out var cached))
        {
            // Check if expired
            if (DateTime.UtcNow - cached.LastAccessedAt <= _memoryCacheTTL)
            {
                // Update access time and count
                cached.LastAccessedAt = DateTime.UtcNow;
                var currentCount = cached.AccessCount;
                cached.AccessCount = currentCount + 1;
                Interlocked.Increment(ref _memoryHits);

                _logger?.LogTrace("Memory cache hit for key: {CacheKey}", cacheKey);
                return cached.Binary;
            }

            // Expired - remove from cache
            _memoryCache.TryRemove(cacheKey, out _);
            _logger?.LogTrace("Memory cache entry expired: {CacheKey}", cacheKey);
        }

        // Try disk cache
        var binary = await TryLoadFromDiskAsync(cacheKey);
        if (binary != null)
        {
            Interlocked.Increment(ref _diskHits);
            _logger?.LogTrace("Disk cache hit for key: {CacheKey}", cacheKey);

            // Promote to memory cache
            var program = new CachedProgram
            {
                Binary = binary,
                KernelName = ExtractKernelName(sourceCode),
                SourceHash = cacheKey,
                Options = options,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                AccessCount = 1
            };

            AddToMemoryCache(cacheKey, program);
            return binary;
        }

        // Cache miss
        Interlocked.Increment(ref _misses);
        _logger?.LogTrace("Cache miss for key: {CacheKey}", cacheKey);
        return null;
    }

    /// <summary>
    /// Stores a compiled program binary in the cache.
    /// </summary>
    /// <param name="sourceCode">The kernel source code.</param>
    /// <param name="binary">The compiled binary.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="deviceName">Target device name.</param>
    /// <param name="kernelName">Name of the kernel.</param>
    public async Task StoreBinaryAsync(
        string sourceCode,
        byte[] binary,
        CompilationOptions options,
        string deviceName,
        string kernelName)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code cannot be null or empty", nameof(sourceCode));
        }

        if (binary == null || binary.Length == 0)
        {
            throw new ArgumentException("Binary cannot be null or empty", nameof(binary));
        }

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
        }

        if (string.IsNullOrWhiteSpace(kernelName))
        {
            throw new ArgumentException("Kernel name cannot be null or empty", nameof(kernelName));
        }

        var cacheKey = GenerateCacheKey(sourceCode, options, deviceName);

        var program = new CachedProgram
        {
            Binary = binary,
            KernelName = kernelName,
            SourceHash = cacheKey,
            Options = options,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            AccessCount = 0
        };

        // Add to memory cache
        AddToMemoryCache(cacheKey, program);

        // Save to disk asynchronously
        await SaveToDiskAsync(cacheKey, binary, kernelName);

        _logger?.LogDebug("Cached compiled kernel: {KernelName} (key: {CacheKey}, size: {SizeBytes} bytes)",
            kernelName, cacheKey, binary.Length);
    }

    /// <summary>
    /// Invalidates all cached entries for the given source code.
    /// </summary>
    /// <param name="sourceCode">The kernel source code.</param>
    public void Invalidate(string sourceCode)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return;
        }

        // Compute base hash for the source code
        var sourceHash = ComputeHash(sourceCode);

        // Remove all entries with matching source hash from memory
        var keysToRemove = _memoryCache
            .Where(kvp => kvp.Value.SourceHash.Contains(sourceHash, StringComparison.Ordinal))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _memoryCache.TryRemove(key, out _);
        }

        _logger?.LogDebug("Invalidated {Count} cache entries for source hash: {SourceHash}",
            keysToRemove.Count, sourceHash);
    }

    /// <summary>
    /// Clears all cached entries from memory and optionally from disk.
    /// </summary>
    /// <param name="clearDisk">If true, also clears disk cache.</param>
    public async Task ClearAsync(bool clearDisk = false)
    {
        ThrowIfDisposed();

        _memoryCache.Clear();
        _logger?.LogInformation("Cleared memory cache");

        if (clearDisk)
        {
            await _diskLock.WaitAsync();
            try
            {
                if (Directory.Exists(_diskCachePath))
                {
                    var files = Directory.GetFiles(_diskCachePath, "cl_kernel_*.bin");
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to delete cache file: {FilePath}", file);
                        }
                    }
                    _logger?.LogInformation("Cleared disk cache: {Count} files deleted", files.Length);
                }
            }
            finally
            {
                _diskLock.Release();
            }
        }

        // Reset statistics
        Interlocked.Exchange(ref _memoryHits, 0);
        Interlocked.Exchange(ref _diskHits, 0);
        Interlocked.Exchange(ref _misses, 0);
    }

    /// <summary>
    /// Gets current cache statistics.
    /// </summary>
    /// <returns>Cache statistics including hit rates and sizes.</returns>
    public CacheStatistics GetStatistics()
    {
        ThrowIfDisposed();

        var memoryHits = Interlocked.Read(ref _memoryHits);
        var diskHits = Interlocked.Read(ref _diskHits);
        var misses = Interlocked.Read(ref _misses);
        var totalRequests = memoryHits + diskHits + misses;

        var diskCacheSize = 0L;
        try
        {
            if (Directory.Exists(_diskCachePath))
            {
                var files = Directory.GetFiles(_diskCachePath, "cl_kernel_*.bin");
                diskCacheSize = files.Sum(f => new FileInfo(f).Length);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to calculate disk cache size");
        }

        return new CacheStatistics
        {
            MemoryHits = memoryHits,
            DiskHits = diskHits,
            Misses = misses,
            HitRate = totalRequests > 0 ? (double)(memoryHits + diskHits) / totalRequests : 0.0,
            MemoryCacheSize = _memoryCache.Count,
            DiskCacheSizeBytes = diskCacheSize,
            TotalCachedPrograms = _memoryCache.Count + (int)(diskCacheSize > 0 ? GetDiskCacheFileCount() : 0)
        };
    }

    /// <summary>
    /// Removes expired entries from memory and disk caches.
    /// </summary>
    public async Task CleanupExpiredAsync()
    {
        ThrowIfDisposed();

        // Clean memory cache
        var now = DateTime.UtcNow;
        var expiredKeys = _memoryCache
            .Where(kvp => now - kvp.Value.LastAccessedAt > _memoryCacheTTL)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _memoryCache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger?.LogDebug("Removed {Count} expired entries from memory cache", expiredKeys.Count);
        }

        // Clean disk cache
        await _diskLock.WaitAsync();
        try
        {
            if (!Directory.Exists(_diskCachePath))
            {
                return;
            }

            var files = Directory.GetFiles(_diskCachePath, "cl_kernel_*.bin");
            var deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var lastWrite = File.GetLastWriteTimeUtc(file);
                    if (now - lastWrite > _diskCacheTTL)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to process cache file: {FilePath}", file);
                }
            }

            if (deletedCount > 0)
            {
                _logger?.LogDebug("Removed {Count} expired files from disk cache", deletedCount);
            }
        }
        finally
        {
            _diskLock.Release();
        }
    }

    /// <summary>
    /// Prunes disk cache to stay under the specified size limit.
    /// Removes oldest files first.
    /// </summary>
    /// <param name="maxSizeBytes">Maximum size in bytes.</param>
    public async Task PruneDiskCacheAsync(long maxSizeBytes)
    {
        ThrowIfDisposed();

        if (maxSizeBytes <= 0)
        {
            throw new ArgumentException("Maximum size must be positive", nameof(maxSizeBytes));
        }

        await _diskLock.WaitAsync();
        try
        {
            if (!Directory.Exists(_diskCachePath))
            {
                return;
            }

            var files = Directory.GetFiles(_diskCachePath, "cl_kernel_*.bin")
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.LastWriteTimeUtc)
                .ToList();

            var totalSize = files.Sum(f => f.Length);
            if (totalSize <= maxSizeBytes)
            {
                return; // Already under limit
            }

            var deletedCount = 0;
            foreach (var file in files)
            {
                if (totalSize <= maxSizeBytes)
                {
                    break;
                }

                try
                {
                    var fileSize = file.Length;
                    file.Delete();
                    totalSize -= fileSize;
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to delete cache file: {FilePath}", file.FullName);
                }
            }

            _logger?.LogInformation("Pruned disk cache: {Count} files deleted, current size: {SizeBytes} bytes",
                deletedCount, totalSize);
        }
        finally
        {
            _diskLock.Release();
        }
    }

    /// <summary>
    /// Generates a cache key from source code, options, and device name.
    /// </summary>
    /// <param name="sourceCode">The kernel source code.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="deviceName">Target device name.</param>
    /// <returns>A unique cache key.</returns>
    private static string GenerateCacheKey(string sourceCode, CompilationOptions options, string deviceName)
    {
        // Combine source code, relevant options, and device name
        var sb = new StringBuilder();
        sb.Append(sourceCode);
        sb.Append('|');
        sb.Append(options.OptimizationLevel);
        sb.Append('|');
        sb.Append(options.EnableFastMath);
        sb.Append('|');
        sb.Append(options.EnableDebugInfo);
        sb.Append('|');
        sb.Append(deviceName);

        // Add additional flags if specified
        if (options.AdditionalFlags.Count > 0)
        {
            sb.Append('|');
            sb.Append(string.Join(",", options.AdditionalFlags.OrderBy(f => f)));
        }

        return ComputeHash(sb.ToString());
    }

    /// <summary>
    /// Computes SHA256 hash of the input string.
    /// </summary>
    /// <param name="input">Input string to hash.</param>
    /// <returns>Hexadecimal hash string.</returns>
    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToUpperInvariant();
    }

    /// <summary>
    /// Attempts to load a cached binary from disk.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <returns>The cached binary if found and valid; otherwise, null.</returns>
    private async Task<byte[]?> TryLoadFromDiskAsync(string cacheKey)
    {
        var filePath = GetDiskCacheFilePath(cacheKey);

        if (!File.Exists(filePath))
        {
            return null;
        }

        await _diskLock.WaitAsync();
        try
        {
            // Check if file is expired
            var lastWrite = File.GetLastWriteTimeUtc(filePath);
            if (DateTime.UtcNow - lastWrite > _diskCacheTTL)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to delete expired cache file: {FilePath}", filePath);
                }
                return null;
            }

            // Load binary
            return await File.ReadAllBytesAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load cache file: {FilePath}", filePath);
            return null;
        }
        finally
        {
            _diskLock.Release();
        }
    }

    /// <summary>
    /// Saves a compiled binary to disk.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="binary">The compiled binary.</param>
    /// <param name="kernelName">Name of the kernel.</param>
    private async Task SaveToDiskAsync(string cacheKey, byte[] binary, string kernelName)
    {
        var filePath = GetDiskCacheFilePath(cacheKey);

        await _diskLock.WaitAsync();
        try
        {
            if (!Directory.Exists(_diskCachePath))
            {
                Directory.CreateDirectory(_diskCachePath);
            }

            await File.WriteAllBytesAsync(filePath, binary);
            _logger?.LogTrace("Saved cache file: {FilePath} ({SizeBytes} bytes)", filePath, binary.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save cache file: {FilePath}", filePath);
        }
        finally
        {
            _diskLock.Release();
        }
    }

    /// <summary>
    /// Gets the disk cache file path for a cache key.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <returns>Full path to the cache file.</returns>
    private string GetDiskCacheFilePath(string cacheKey)
    {
        var fileName = $"cl_kernel_{cacheKey}.bin";
        return Path.Combine(_diskCachePath, fileName);
    }

    /// <summary>
    /// Adds an entry to the memory cache with LRU eviction.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="program">The cached program.</param>
    private void AddToMemoryCache(string cacheKey, CachedProgram program)
    {
        // Add or update entry
        _memoryCache.AddOrUpdate(cacheKey, program, (_, _) => program);

        // Check if eviction is needed
        if (_memoryCache.Count > _maxMemoryCacheSize)
        {
            EvictLRUEntries();
        }
    }

    /// <summary>
    /// Evicts least recently used entries from memory cache.
    /// </summary>
    private void EvictLRUEntries()
    {
        var entriesToRemove = _memoryCache.Count - _maxMemoryCacheSize;
        if (entriesToRemove <= 0)
        {
            return;
        }

        // Find entries to evict (least recently accessed)
        var lruEntries = _memoryCache
            .OrderBy(kvp => kvp.Value.LastAccessedAt)
            .Take(entriesToRemove)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in lruEntries)
        {
            _memoryCache.TryRemove(key, out _);
        }

        _logger?.LogTrace("Evicted {Count} LRU entries from memory cache", lruEntries.Count);
    }

    /// <summary>
    /// Extracts kernel name from source code (simple heuristic).
    /// </summary>
    /// <param name="sourceCode">The kernel source code.</param>
    /// <returns>Extracted kernel name or "unknown".</returns>
    private static string ExtractKernelName(string sourceCode)
    {
        // Look for __kernel keyword
        var kernelIndex = sourceCode.IndexOf("__kernel", StringComparison.Ordinal);
        if (kernelIndex >= 0)
        {
            // Find function name after __kernel
            var nameStart = sourceCode.IndexOf(' ', kernelIndex + 8);
            if (nameStart >= 0)
            {
                nameStart = sourceCode.IndexOf(' ', nameStart + 1); // Skip return type
                if (nameStart >= 0)
                {
                    var nameEnd = sourceCode.IndexOf('(', nameStart);
                    if (nameEnd >= 0)
                    {
                        return sourceCode.Substring(nameStart, nameEnd - nameStart).Trim();
                    }
                }
            }
        }

        return "unknown";
    }

    /// <summary>
    /// Gets the number of files in disk cache.
    /// </summary>
    /// <returns>File count.</returns>
    private int GetDiskCacheFileCount()
    {
        try
        {
            if (Directory.Exists(_diskCachePath))
            {
                return Directory.GetFiles(_diskCachePath, "cl_kernel_*.bin").Length;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to count disk cache files");
        }

        return 0;
    }

    /// <summary>
    /// Throws if this cache has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Disposes the cache and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _memoryCache.Clear();
        _diskLock.Dispose();
        _disposed = true;

        _logger?.LogDebug("OpenCLCompilationCache disposed");
    }

    /// <summary>
    /// Represents a cached compiled program.
    /// </summary>
    private sealed class CachedProgram
    {
        /// <summary>
        /// Gets or initializes the compiled binary.
        /// </summary>
        public required byte[] Binary { get; init; }

        /// <summary>
        /// Gets or initializes the kernel name.
        /// </summary>
        public required string KernelName { get; init; }

        /// <summary>
        /// Gets or initializes the source code hash.
        /// </summary>
        public required string SourceHash { get; init; }

        /// <summary>
        /// Gets or initializes the compilation options.
        /// </summary>
        public required CompilationOptions Options { get; init; }

        /// <summary>
        /// Gets or initializes the creation timestamp.
        /// </summary>
        public required DateTime CreatedAt { get; init; }

        /// <summary>
        /// Gets or sets the last accessed timestamp.
        /// </summary>
        public DateTime LastAccessedAt { get; set; }

        /// <summary>
        /// Gets or sets the access count.
        /// </summary>
        public int AccessCount { get; set; }
    }
}

/// <summary>
/// Cache statistics for monitoring and diagnostics.
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>
    /// Gets the number of memory cache hits.
    /// </summary>
    public required long MemoryHits { get; init; }

    /// <summary>
    /// Gets the number of disk cache hits.
    /// </summary>
    public required long DiskHits { get; init; }

    /// <summary>
    /// Gets the number of cache misses.
    /// </summary>
    public required long Misses { get; init; }

    /// <summary>
    /// Gets the overall cache hit rate (0.0 to 1.0).
    /// </summary>
    public required double HitRate { get; init; }

    /// <summary>
    /// Gets the current memory cache size (number of entries).
    /// </summary>
    public required int MemoryCacheSize { get; init; }

    /// <summary>
    /// Gets the disk cache size in bytes.
    /// </summary>
    public required long DiskCacheSizeBytes { get; init; }

    /// <summary>
    /// Gets the total number of cached programs (memory + disk).
    /// </summary>
    public required int TotalCachedPrograms { get; init; }

    /// <summary>
    /// Returns a string representation of the statistics.
    /// </summary>
    /// <returns>Formatted statistics string.</returns>
    public override string ToString()
    {
        return $"Cache Statistics: MemoryHits={MemoryHits}, DiskHits={DiskHits}, Misses={Misses}, " +
               $"HitRate={HitRate:P2}, MemorySize={MemoryCacheSize}, DiskSize={DiskCacheSizeBytes} bytes, " +
               $"TotalPrograms={TotalCachedPrograms}";
    }
}
