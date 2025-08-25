// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotCompute.Abstractions.Enums;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Native.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Production-grade CUDA kernel cache with PTX/binary caching,
/// version management, and LRU eviction policies.
/// </summary>
public sealed class CudaKernelCache : IDisposable
{
    private readonly ILogger<CudaKernelCache> _logger;
    private readonly ConcurrentDictionary<string, CachedKernel> _memoryCache;
    private readonly ConcurrentDictionary<string, KernelMetadata> _metadataCache;
    private readonly LinkedList<string> _lruList;
    private readonly Lock _lruLock = new();
    private readonly KernelCacheConfig _config;
    private readonly string _diskCachePath;
    private readonly Timer _cleanupTimer;
    private long _totalCacheHits;
    private long _totalCacheMisses;
    private long _currentCacheSize;
    private bool _disposed;

    public CudaKernelCache(
        ILogger<CudaKernelCache> logger,
        KernelCacheConfig? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new KernelCacheConfig();
        _memoryCache = new ConcurrentDictionary<string, CachedKernel>();
        _metadataCache = new ConcurrentDictionary<string, KernelMetadata>();
        _lruList = new LinkedList<string>();
        
        // Initialize disk cache directory
        _diskCachePath = _config.DiskCachePath ?? 
            Path.Combine(Path.GetTempPath(), "dotcompute", "cuda_kernel_cache");
        
        InitializeDiskCache();
        
        // Start cleanup timer
        _cleanupTimer = new Timer(
            PerformCacheMaintenance,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
        
        _logger.LogInformation(
            "Kernel cache initialized - Memory: {MemLimit}MB, Disk: {DiskPath}",
            _config.MaxMemoryCacheSizeMB, _diskCachePath);
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public KernelCacheStatistics Statistics => new()
    {
        TotalHits = Interlocked.Read(ref _totalCacheHits),
        TotalMisses = Interlocked.Read(ref _totalCacheMisses),
        HitRate = CalculateHitRate(),
        MemoryCacheEntries = _memoryCache.Count,
        DiskCacheEntries = _metadataCache.Count,
        CurrentMemoryUsageMB = Interlocked.Read(ref _currentCacheSize) / (1024.0 * 1024.0)
    };

    /// <summary>
    /// Initializes the disk cache directory.
    /// </summary>
    private void InitializeDiskCache()
    {
        try
        {
            if (!Directory.Exists(_diskCachePath))
            {
                Directory.CreateDirectory(_diskCachePath);
                _logger.LogInformation("Created disk cache directory: {Path}", _diskCachePath);
            }
            
            // Load existing cache metadata
            LoadCacheMetadata();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize disk cache, using memory-only mode");
            _config.EnableDiskCache = false;
        }
    }

    /// <summary>
    /// Loads cache metadata from disk.
    /// </summary>
    private void LoadCacheMetadata()
    {
        var metadataFile = Path.Combine(_diskCachePath, "cache_metadata.json");
        
        if (!File.Exists(metadataFile))
            return;
        
        try
        {
            var json = File.ReadAllText(metadataFile);
            var metadata = JsonSerializer.Deserialize<Dictionary<string, KernelMetadata>>(json);
            
            if (metadata != null)
            {
                foreach (var (key, value) in metadata)
                {
                    // Validate that cached files still exist
                    if (File.Exists(value.DiskPath))
                    {
                        _metadataCache.TryAdd(key, value);
                    }
                }
                
                _logger.LogInformation("Loaded {Count} kernel metadata entries from disk cache",
                    _metadataCache.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cache metadata");
        }
    }

    /// <summary>
    /// Gets or compiles a kernel with caching.
    /// </summary>
    public async Task<CompiledKernel> GetOrCompileKernelAsync(
        string sourceCode,
        string kernelName,
        CompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        // Generate cache key
        var cacheKey = GenerateCacheKey(sourceCode, kernelName, options);
        
        // Check memory cache first
        if (TryGetFromMemoryCache(cacheKey, out var cached))
        {
            RecordCacheHit();
            _logger.LogDebug("Kernel cache hit (memory): {Key}", cacheKey);
            return cached;
        }
        
        // Check disk cache
        if (_config.EnableDiskCache && await TryGetFromDiskCacheAsync(cacheKey, cancellationToken))
        {
            if (TryGetFromMemoryCache(cacheKey, out cached))
            {
                RecordCacheHit();
                _logger.LogDebug("Kernel cache hit (disk): {Key}", cacheKey);
                return cached;
            }
        }
        
        // Cache miss - compile kernel
        RecordCacheMiss();
        _logger.LogInformation("Kernel cache miss, compiling: {Name}", kernelName);
        
        var compiled = await CompileKernelAsync(
            sourceCode, kernelName, options, cancellationToken);
        
        // Add to cache
        await AddToCacheAsync(cacheKey, compiled, cancellationToken);
        
        return compiled;
    }

    /// <summary>
    /// Compiles a CUDA kernel.
    /// </summary>
    private async Task<CompiledKernel> CompileKernelAsync(
        string sourceCode,
        string kernelName,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;
        
        try
        {
            // Step 1: Compile to PTX using NVRTC
            var ptx = await CompileToPtxAsync(sourceCode, kernelName, options, cancellationToken);
            
            // Step 2: Optionally compile PTX to cubin for better performance
            byte[]? cubin = null;
            if (options.CompileToCubin)
            {
                cubin = await CompilePtxToCubinAsync(ptx, options, cancellationToken);
            }
            
            // Step 3: JIT compile to final binary if needed
            var binary = cubin ?? Encoding.UTF8.GetBytes(ptx);
            
            var endTime = DateTimeOffset.UtcNow;
            var compilationTime = (endTime - startTime).TotalMilliseconds;
            
            _logger.LogInformation(
                "Compiled kernel '{Name}' in {Time:F2}ms - PTX: {PtxSize} bytes, Binary: {BinSize} bytes",
                kernelName, compilationTime, ptx.Length, binary.Length);
            
            return new CompiledKernel
            {
                Name = kernelName,
                Ptx = ptx,
                Cubin = cubin,
                Binary = binary,
                ComputeCapability = options.ComputeCapability,
                CompilationTime = compilationTime,
                CompiledAt = endTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile kernel '{Name}'", kernelName);
            throw new KernelCompilationException($"Kernel compilation failed: {kernelName}", ex);
        }
    }

    /// <summary>
    /// Compiles CUDA code to PTX using NVRTC.
    /// </summary>
    private async Task<string> CompileToPtxAsync(
        string sourceCode,
        string kernelName,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            // Create NVRTC program
            var result = NvrtcInterop.nvrtcCreateProgram(
                out IntPtr prog,
                sourceCode,
                kernelName,
                0,
                IntPtr.Zero,
                IntPtr.Zero);
            
            if (result != NvrtcResult.Success)
            {
                throw new KernelCompilationException($"Failed to create NVRTC program: {result}");
            }
            
            try
            {
                // Build compilation options
                var compileOptions = BuildNvrtcOptions(options);
                
                // Compile program
                result = NvrtcInterop.nvrtcCompileProgram(
                    prog,
                    compileOptions.Length,
                    compileOptions);
                
                if (result != NvrtcResult.Success)
                {
                    // Get compilation log
                    NvrtcInterop.nvrtcGetProgramLogSize(prog, out ulong logSize);
                    var log = new byte[logSize];
                    NvrtcInterop.nvrtcGetProgramLog(prog, log);
                    var logString = Encoding.UTF8.GetString(log);
                    
                    throw new KernelCompilationException(
                        $"NVRTC compilation failed: {result}\nLog: {logString}");
                }
                
                // Get PTX
                NvrtcInterop.nvrtcGetPTXSize(prog, out ulong ptxSize);
                var ptxBytes = new byte[ptxSize];
                NvrtcInterop.nvrtcGetPTX(prog, ptxBytes);
                
                return Encoding.UTF8.GetString(ptxBytes);
            }
            finally
            {
                // Destroy program
                NvrtcInterop.nvrtcDestroyProgram(ref prog);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Compiles PTX to cubin.
    /// </summary>
    private async Task<byte[]> CompilePtxToCubinAsync(
        string ptx,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            // Use CUDA driver API to compile PTX to cubin
            // This is a simplified version - real implementation would use cuLinkCreate/cuLinkAddData
            
            _logger.LogDebug("Compiling PTX to cubin for compute_{Major}_{Minor}",
                options.ComputeCapability.Major, options.ComputeCapability.Minor);
            
            // For now, return PTX as bytes
            // TODO: Implement actual PTX to cubin compilation
            return Encoding.UTF8.GetBytes(ptx);
        }, cancellationToken);
    }

    /// <summary>
    /// Builds NVRTC compilation options.
    /// </summary>
    private string[] BuildNvrtcOptions(CompilationOptions options)
    {
        var nvrtcOptions = new List<string>();
        
        // Compute capability
        nvrtcOptions.Add($"--gpu-architecture=compute_{options.ComputeCapability.Major}{options.ComputeCapability.Minor}");
        
        // Optimization level
        nvrtcOptions.Add(options.OptimizationLevel switch
        {
            OptimizationLevel.O0 => "-O0",
            OptimizationLevel.O1 => "-O1",
            OptimizationLevel.O2 => "-O2",
            OptimizationLevel.O3 => "-O3",
            _ => "-O2"
        });
        
        // Debug info
        if (options.GenerateDebugInfo)
        {
            nvrtcOptions.Add("-G");
            nvrtcOptions.Add("--generate-line-info");
        }
        
        // Fast math
        if (options.UseFastMath)
        {
            nvrtcOptions.Add("--use_fast_math");
        }
        
        // FMA instructions
        if (options.FusedMultiplyAdd)
        {
            nvrtcOptions.Add("--fmad=true");
        }
        
        // Relocatable device code
        if (options.RelocatableDeviceCode)
        {
            nvrtcOptions.Add("--relocatable-device-code=true");
        }
        
        // Max registers per thread
        if (options.MaxRegistersPerThread > 0)
        {
            nvrtcOptions.Add($"--maxrregcount={options.MaxRegistersPerThread}");
        }
        
        // Include paths
        foreach (var includePath in options.IncludePaths)
        {
            nvrtcOptions.Add($"-I{includePath}");
        }
        
        // Preprocessor defines
        foreach (var define in options.Defines)
        {
            nvrtcOptions.Add($"-D{define.Key}={define.Value}");
        }
        
        return nvrtcOptions.ToArray();
    }

    /// <summary>
    /// Generates a cache key for the kernel.
    /// </summary>
    private static string GenerateCacheKey(
        string sourceCode,
        string kernelName,
        CompilationOptions options)
    {
        using var sha256 = SHA256.Create();
        
        var input = new StringBuilder();
        input.Append(sourceCode);
        input.Append(kernelName);
        input.Append(options.ComputeCapability.Major);
        input.Append(options.ComputeCapability.Minor);
        input.Append((int)options.OptimizationLevel);
        input.Append(options.GenerateDebugInfo);
        input.Append(options.UseFastMath);
        
        foreach (var define in options.Defines.OrderBy(kvp => kvp.Key))
        {
            input.Append($"{define.Key}={define.Value}");
        }
        
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Tries to get kernel from memory cache.
    /// </summary>
    private bool TryGetFromMemoryCache(string cacheKey, out CompiledKernel kernel)
    {
        if (_memoryCache.TryGetValue(cacheKey, out var cached))
        {
            // Update LRU
            UpdateLru(cacheKey);
            
            kernel = cached.Kernel;
            cached.LastAccessTime = DateTimeOffset.UtcNow;
            cached.AccessCount++;
            
            return true;
        }
        
        kernel = null!;
        return false;
    }

    /// <summary>
    /// Tries to get kernel from disk cache.
    /// </summary>
    private async Task<bool> TryGetFromDiskCacheAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        if (!_metadataCache.TryGetValue(cacheKey, out var metadata))
            return false;
        
        try
        {
            // Load from disk
            var ptxPath = metadata.DiskPath;
            var cubinPath = Path.ChangeExtension(ptxPath, ".cubin");
            
            if (!File.Exists(ptxPath))
            {
                _metadataCache.TryRemove(cacheKey, out _);
                return false;
            }
            
            var ptx = await File.ReadAllTextAsync(ptxPath, cancellationToken);
            
            byte[]? cubin = null;
            if (File.Exists(cubinPath))
            {
                cubin = await File.ReadAllBytesAsync(cubinPath, cancellationToken);
            }
            
            var kernel = new CompiledKernel
            {
                Name = metadata.KernelName,
                Ptx = ptx,
                Cubin = cubin,
                Binary = cubin ?? Encoding.UTF8.GetBytes(ptx),
                ComputeCapability = metadata.ComputeCapability,
                CompilationTime = metadata.CompilationTime,
                CompiledAt = metadata.CompiledAt
            };
            
            // Add to memory cache
            AddToMemoryCache(cacheKey, kernel);
            
            _logger.LogDebug("Loaded kernel from disk cache: {Key}", cacheKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load kernel from disk cache: {Key}", cacheKey);
            _metadataCache.TryRemove(cacheKey, out _);
            return false;
        }
    }

    /// <summary>
    /// Adds kernel to cache.
    /// </summary>
    private async Task AddToCacheAsync(
        string cacheKey,
        CompiledKernel kernel,
        CancellationToken cancellationToken)
    {
        // Add to memory cache
        AddToMemoryCache(cacheKey, kernel);
        
        // Add to disk cache if enabled
        if (_config.EnableDiskCache)
        {
            await AddToDiskCacheAsync(cacheKey, kernel, cancellationToken);
        }
    }

    /// <summary>
    /// Adds kernel to memory cache with LRU eviction.
    /// </summary>
    private void AddToMemoryCache(string cacheKey, CompiledKernel kernel)
    {
        var kernelSize = CalculateKernelSize(kernel);
        
        // Check if we need to evict
        while (Interlocked.Read(ref _currentCacheSize) + kernelSize > 
               _config.MaxMemoryCacheSizeMB * 1024 * 1024)
        {
            if (!EvictLeastRecentlyUsed())
                break;
        }
        
        var cached = new CachedKernel
        {
            Kernel = kernel,
            CacheKey = cacheKey,
            Size = kernelSize,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessTime = DateTimeOffset.UtcNow,
            AccessCount = 1
        };
        
        if (_memoryCache.TryAdd(cacheKey, cached))
        {
            Interlocked.Add(ref _currentCacheSize, kernelSize);
            UpdateLru(cacheKey);
            
            _logger.LogDebug("Added kernel to memory cache: {Key} ({Size} bytes)", 
                cacheKey, kernelSize);
        }
    }

    /// <summary>
    /// Adds kernel to disk cache.
    /// </summary>
    private async Task AddToDiskCacheAsync(
        string cacheKey,
        CompiledKernel kernel,
        CancellationToken cancellationToken)
    {
        try
        {
            var ptxPath = Path.Combine(_diskCachePath, $"{cacheKey}.ptx");
            await File.WriteAllTextAsync(ptxPath, kernel.Ptx, cancellationToken);
            
            if (kernel.Cubin != null)
            {
                var cubinPath = Path.Combine(_diskCachePath, $"{cacheKey}.cubin");
                await File.WriteAllBytesAsync(cubinPath, kernel.Cubin, cancellationToken);
            }
            
            var metadata = new KernelMetadata
            {
                CacheKey = cacheKey,
                KernelName = kernel.Name,
                DiskPath = ptxPath,
                ComputeCapability = kernel.ComputeCapability,
                CompilationTime = kernel.CompilationTime,
                CompiledAt = kernel.CompiledAt,
                FileSize = new FileInfo(ptxPath).Length
            };
            
            _metadataCache.TryAdd(cacheKey, metadata);
            
            // Persist metadata
            await SaveCacheMetadataAsync(cancellationToken);
            
            _logger.LogDebug("Added kernel to disk cache: {Key}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add kernel to disk cache: {Key}", cacheKey);
        }
    }

    /// <summary>
    /// Saves cache metadata to disk.
    /// </summary>
    private async Task SaveCacheMetadataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var metadataFile = Path.Combine(_diskCachePath, "cache_metadata.json");
            var json = JsonSerializer.Serialize(_metadataCache.ToDictionary(
                kvp => kvp.Key, kvp => kvp.Value),
                new JsonSerializerOptions { WriteIndented = true });
            
            await File.WriteAllTextAsync(metadataFile, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save cache metadata");
        }
    }

    /// <summary>
    /// Updates LRU list for cache key.
    /// </summary>
    private void UpdateLru(string cacheKey)
    {
        lock (_lruLock)
        {
            _lruList.Remove(cacheKey);
            _lruList.AddFirst(cacheKey);
        }
    }

    /// <summary>
    /// Evicts least recently used kernel from memory cache.
    /// </summary>
    private bool EvictLeastRecentlyUsed()
    {
        lock (_lruLock)
        {
            if (_lruList.Count == 0)
                return false;
            
            var lruKey = _lruList.Last?.Value;
            if (lruKey != null)
            {
                _lruList.RemoveLast();
                
                if (_memoryCache.TryRemove(lruKey, out var cached))
                {
                    Interlocked.Add(ref _currentCacheSize, -cached.Size);
                    _logger.LogDebug("Evicted kernel from memory cache: {Key}", lruKey);
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Calculates kernel size in bytes.
    /// </summary>
    private static long CalculateKernelSize(CompiledKernel kernel)
    {
        long size = 0;
        
        if (!string.IsNullOrEmpty(kernel.Ptx))
            size += kernel.Ptx.Length * 2; // Unicode chars
        
        if (kernel.Cubin != null)
            size += kernel.Cubin.Length;
        
        if (kernel.Binary != null)
            size += kernel.Binary.Length;
        
        return size;
    }

    /// <summary>
    /// Performs cache maintenance.
    /// </summary>
    private void PerformCacheMaintenance(object? state)
    {
        try
        {
            // Clean up old disk cache entries
            if (_config.EnableDiskCache && _config.DiskCacheExpirationDays > 0)
            {
                CleanupOldDiskCacheEntries();
            }
            
            // Log statistics
            var stats = Statistics;
            _logger.LogInformation(
                "Cache stats - Hit rate: {HitRate:P2}, Memory: {MemEntries} entries ({MemUsage:F2}MB), Disk: {DiskEntries} entries",
                stats.HitRate, stats.MemoryCacheEntries, stats.CurrentMemoryUsageMB, stats.DiskCacheEntries);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during cache maintenance");
        }
    }

    /// <summary>
    /// Cleans up old disk cache entries.
    /// </summary>
    private void CleanupOldDiskCacheEntries()
    {
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-_config.DiskCacheExpirationDays);
        var keysToRemove = new List<string>();
        
        foreach (var (key, metadata) in _metadataCache)
        {
            if (metadata.CompiledAt < cutoffDate)
            {
                keysToRemove.Add(key);
                
                try
                {
                    if (File.Exists(metadata.DiskPath))
                        File.Delete(metadata.DiskPath);
                    
                    var cubinPath = Path.ChangeExtension(metadata.DiskPath, ".cubin");
                    if (File.Exists(cubinPath))
                        File.Delete(cubinPath);
                }
                catch { /* Best effort */ }
            }
        }
        
        foreach (var key in keysToRemove)
        {
            _metadataCache.TryRemove(key, out _);
        }
        
        if (keysToRemove.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired disk cache entries", keysToRemove.Count);
            _ = SaveCacheMetadataAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    private void RecordCacheHit()
    {
        Interlocked.Increment(ref _totalCacheHits);
    }

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    private void RecordCacheMiss()
    {
        Interlocked.Increment(ref _totalCacheMisses);
    }

    /// <summary>
    /// Calculates cache hit rate.
    /// </summary>
    private double CalculateHitRate()
    {
        var hits = Interlocked.Read(ref _totalCacheHits);
        var misses = Interlocked.Read(ref _totalCacheMisses);
        var total = hits + misses;
        
        return total > 0 ? (double)hits / total : 0;
    }

    /// <summary>
    /// Clears all caches.
    /// </summary>
    public void ClearCache()
    {
        ThrowIfDisposed();
        
        lock (_lruLock)
        {
            _memoryCache.Clear();
            _lruList.Clear();
            Interlocked.Exchange(ref _currentCacheSize, 0);
        }
        
        _logger.LogInformation("Cleared kernel cache");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            
            _cleanupTimer?.Dispose();
            
            // Save metadata before disposal
            _ = SaveCacheMetadataAsync(CancellationToken.None).GetAwaiter().GetResult();
            
            _memoryCache.Clear();
            _metadataCache.Clear();
            _lruList.Clear();
            _lruLock?.Dispose();
        }
    }

    /// <summary>
    /// Cached kernel information.
    /// </summary>
    private sealed class CachedKernel
    {
        public CompiledKernel Kernel { get; init; } = null!;
        public string CacheKey { get; init; } = string.Empty;
        public long Size { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset LastAccessTime { get; set; }
        public int AccessCount { get; set; }
    }

    /// <summary>
    /// Kernel metadata for disk cache.
    /// </summary>
    private sealed class KernelMetadata
    {
        public string CacheKey { get; init; } = string.Empty;
        public string KernelName { get; init; } = string.Empty;
        public string DiskPath { get; init; } = string.Empty;
        public ComputeCapability ComputeCapability { get; init; }
        public double CompilationTime { get; init; }
        public DateTimeOffset CompiledAt { get; init; }
        public long FileSize { get; init; }
    }
}

/// <summary>
/// Compiled kernel result.
/// </summary>
public sealed class CompiledKernel
{
    public string Name { get; init; } = string.Empty;
    public string Ptx { get; init; } = string.Empty;
    public byte[]? Cubin { get; init; }
    public byte[] Binary { get; init; } = Array.Empty<byte>();
    public ComputeCapability ComputeCapability { get; init; }
    public double CompilationTime { get; init; }
    public DateTimeOffset CompiledAt { get; init; }
}

/// <summary>
/// Kernel compilation options.
/// </summary>
public sealed class CompilationOptions
{
    public ComputeCapability ComputeCapability { get; init; } = new() { Major = 7, Minor = 0 };
    public OptimizationLevel OptimizationLevel { get; init; } = OptimizationLevel.Default;
    public bool GenerateDebugInfo { get; init; }
    public bool UseFastMath { get; init; } = true;
    public bool FusedMultiplyAdd { get; init; } = true;
    public bool RelocatableDeviceCode { get; init; }
    public bool CompileToCubin { get; init; } = true;
    public int MaxRegistersPerThread { get; init; }
    public List<string> IncludePaths { get; init; } = new();
    public Dictionary<string, string> Defines { get; init; } = new();
}

/// <summary>
/// Compute capability.
/// </summary>
public struct ComputeCapability
{
    public int Major { get; init; }
    public int Minor { get; init; }
}


/// <summary>
/// Kernel cache configuration.
/// </summary>
public sealed class KernelCacheConfig
{
    public int MaxMemoryCacheSizeMB { get; init; } = 256;
    public bool EnableDiskCache { get; set; } = true;
    public string? DiskCachePath { get; init; }
    public int DiskCacheExpirationDays { get; init; } = 30;
    public int MaxCacheEntries { get; init; } = 1000;
}

/// <summary>
/// Kernel cache statistics.
/// </summary>
public sealed class KernelCacheStatistics
{
    public long TotalHits { get; init; }
    public long TotalMisses { get; init; }
    public double HitRate { get; init; }
    public int MemoryCacheEntries { get; init; }
    public int DiskCacheEntries { get; init; }
    public double CurrentMemoryUsageMB { get; init; }
}

/// <summary>
/// Kernel compilation exception.
/// </summary>
public sealed class KernelCompilationException : Exception
{
    public KernelCompilationException(string message) : base(message) { }
    public KernelCompilationException(string message, Exception inner) : base(message, inner) { }
}
