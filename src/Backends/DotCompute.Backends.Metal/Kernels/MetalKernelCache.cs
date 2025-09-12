// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Kernels
{
    /// <summary>
    /// Cache for compiled Metal libraries and compute pipeline states
    /// </summary>
    public sealed class MetalKernelCache : IDisposable
    {
        private readonly ILogger<MetalKernelCache> _logger;
        private readonly ConcurrentDictionary<string, CachedKernel> _cache = new();
        private readonly string? _persistentCachePath;
        private readonly ReaderWriterLockSlim _cacheLock = new();
        private bool _disposed;

        /// <summary>
        /// Represents a cached Metal kernel
        /// </summary>
        private sealed class CachedKernel
        {
            public IntPtr Library { get; set; }
            public IntPtr Function { get; set; }
            public IntPtr PipelineState { get; set; }
            public DateTime LastAccessed { get; set; }
            public int AccessCount { get; set; }
            public long CompilationTimeMs { get; set; }
            public byte[]? BinaryData { get; set; }
        }

        public MetalKernelCache(ILogger<MetalKernelCache> logger, string? persistentCachePath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _persistentCachePath = persistentCachePath;

            if (!string.IsNullOrEmpty(_persistentCachePath))
            {
                try
                {
                    Directory.CreateDirectory(_persistentCachePath);
                    LoadPersistentCache();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to initialize persistent cache at {Path}", _persistentCachePath);
                }
            }
        }

        /// <summary>
        /// Try to get a cached kernel
        /// </summary>
        public bool TryGetCachedKernel(string sourceHash, out IntPtr library, out IntPtr function, out IntPtr pipelineState)
        {
            library = IntPtr.Zero;
            function = IntPtr.Zero;
            pipelineState = IntPtr.Zero;

            _cacheLock.EnterReadLock();
            try
            {
                if (_cache.TryGetValue(sourceHash, out var cached))
                {
                    cached.LastAccessed = DateTime.UtcNow;
                    cached.AccessCount++;
                    
                    library = cached.Library;
                    function = cached.Function;
                    pipelineState = cached.PipelineState;
                    
                    _logger.LogDebug("Cache hit for kernel {Hash}. Access count: {Count}", 
                        sourceHash, cached.AccessCount);
                    return true;
                }
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }

            _logger.LogDebug("Cache miss for kernel {Hash}", sourceHash);
            return false;
        }

        /// <summary>
        /// Add a compiled kernel to the cache
        /// </summary>
        public void AddToCache(string sourceHash, IntPtr library, IntPtr function, IntPtr pipelineState, 
            long compilationTimeMs, byte[]? binaryData = null)
        {
            _cacheLock.EnterWriteLock();
            try
            {
                var cached = new CachedKernel
                {
                    Library = library,
                    Function = function,
                    PipelineState = pipelineState,
                    LastAccessed = DateTime.UtcNow,
                    AccessCount = 1,
                    CompilationTimeMs = compilationTimeMs,
                    BinaryData = binaryData
                };

                _cache[sourceHash] = cached;
                _logger.LogDebug("Added kernel {Hash} to cache. Compilation time: {Time}ms", 
                    sourceHash, compilationTimeMs);

                // Save to persistent cache if enabled
                if (!string.IsNullOrEmpty(_persistentCachePath) && binaryData != null)
                {
                    SaveToPersistentCache(sourceHash, binaryData);
                }
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Compute hash for Metal source code
        /// </summary>
        public static string ComputeSourceHash(string source)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(source);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-");
        }

        /// <summary>
        /// Clear the cache
        /// </summary>
        public void Clear()
        {
            _cacheLock.EnterWriteLock();
            try
            {
                foreach (var item in _cache.Values)
                {
                    // Note: We don't release the Metal objects here as they might still be in use
                    // The MetalAccelerator or MetalKernelCompiler should handle their lifecycle
                }
                
                _cache.Clear();
                _logger.LogInformation("Cleared kernel cache");
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            _cacheLock.EnterReadLock();
            try
            {
                var totalAccessCount = 0;
                var totalCompilationTime = 0L;
                var totalBinarySize = 0L;

                foreach (var item in _cache.Values)
                {
                    totalAccessCount += item.AccessCount;
                    totalCompilationTime += item.CompilationTimeMs;
                    if (item.BinaryData != null)
                    {
                        totalBinarySize += item.BinaryData.Length;
                    }
                }

                return new CacheStatistics
                {
                    CachedKernelCount = _cache.Count,
                    TotalAccessCount = totalAccessCount,
                    TotalCompilationTimeMs = totalCompilationTime,
                    TotalBinarySizeBytes = totalBinarySize,
                    AverageAccessCount = _cache.Count > 0 ? (double)totalAccessCount / _cache.Count : 0,
                    AverageCompilationTimeMs = _cache.Count > 0 ? (double)totalCompilationTime / _cache.Count : 0
                };
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Evict least recently used kernels if cache size exceeds limit
        /// </summary>
        public void EvictLRU(int maxCacheSize)
        {
            if (_cache.Count <= maxCacheSize)
            {
                return;
            }

            _cacheLock.EnterWriteLock();
            try
            {
                var itemsToEvict = _cache
                    .OrderBy(x => x.Value.LastAccessed)
                    .Take(_cache.Count - maxCacheSize)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var key in itemsToEvict)
                {
                    if (_cache.TryRemove(key, out var evicted))
                    {
                        _logger.LogDebug("Evicted kernel {Hash} from cache. Last accessed: {Time}", 
                            key, evicted.LastAccessed);
                    }
                }
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        private void LoadPersistentCache()
        {
            if (string.IsNullOrEmpty(_persistentCachePath) || !Directory.Exists(_persistentCachePath))
            {
                return;
            }

            try
            {
                var cacheFiles = Directory.GetFiles(_persistentCachePath, "*.metallib");
                _logger.LogInformation("Loading {Count} cached kernels from persistent storage", cacheFiles.Length);

                foreach (var file in cacheFiles)
                {
                    try
                    {
                        var hash = Path.GetFileNameWithoutExtension(file);
                        var binaryData = File.ReadAllBytes(file);
                        
                        // Note: We can't reconstruct the Metal objects from binary data alone
                        // This would require runtime compilation from the binary
                        // For now, we just store the binary data for future use
                        
                        _logger.LogDebug("Loaded cached kernel {Hash} from persistent storage", hash);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load cached kernel from {File}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load persistent cache");
            }
        }

        private void SaveToPersistentCache(string hash, byte[] binaryData)
        {
            if (string.IsNullOrEmpty(_persistentCachePath))
            {
                return;
            }

            try
            {
                var filePath = Path.Combine(_persistentCachePath, $"{hash}.metallib");
                File.WriteAllBytes(filePath, binaryData);
                _logger.LogDebug("Saved kernel {Hash} to persistent cache", hash);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save kernel {Hash} to persistent cache", hash);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Clear();
            _cacheLock?.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Cache statistics
        /// </summary>
        public record CacheStatistics
        {
            public int CachedKernelCount { get; init; }
            public int TotalAccessCount { get; init; }
            public long TotalCompilationTimeMs { get; init; }
            public long TotalBinarySizeBytes { get; init; }
            public double AverageAccessCount { get; init; }
            public double AverageCompilationTimeMs { get; init; }
        }
    }
}