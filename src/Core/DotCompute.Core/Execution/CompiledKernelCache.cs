// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Core.Kernels;

namespace DotCompute.Core.Execution
{

/// <summary>
/// Thread-safe cache for compiled kernels across multiple devices.
/// </summary>
public sealed class CompiledKernelCache : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ManagedCompiledKernel> _kernelsByDevice;
    private readonly ConcurrentDictionary<string, KernelMetadata> _metadata;
    private readonly Lock _disposeLock = new();
    private volatile bool _disposed;

    public CompiledKernelCache()
    {
        _kernelsByDevice = new ConcurrentDictionary<string, ManagedCompiledKernel>();
        _metadata = new ConcurrentDictionary<string, KernelMetadata>();
    }

    /// <summary>
    /// Gets the number of cached kernels.
    /// </summary>
    public int Count => _kernelsByDevice.Count;

    /// <summary>
    /// Gets whether the cache is empty.
    /// </summary>
    public bool IsEmpty => _kernelsByDevice.IsEmpty;

    /// <summary>
    /// Adds a compiled kernel for a specific device to the cache.
    /// </summary>
    public void AddKernel(IAccelerator device, ManagedCompiledKernel kernel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CompiledKernelCache));
        }

        var deviceKey = GetDeviceKey(device);
        _kernelsByDevice[deviceKey] = kernel;
        
        _metadata[deviceKey] = new KernelMetadata
        {
            DeviceId = device.Info.Id,
            DeviceType = device.Info.DeviceType,
            KernelName = kernel.Name,
            CachedAt = DateTimeOffset.UtcNow,
            AccessCount = 0,
            LastAccessed = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Tries to get a compiled kernel for a specific device.
    /// </summary>
    public bool TryGetKernel(IAccelerator device, out ManagedCompiledKernel kernel)
    {
        if (_disposed)
        {
            kernel = default!;
            return false;
        }

        var deviceKey = GetDeviceKey(device);
        var found = _kernelsByDevice.TryGetValue(deviceKey, out kernel!);
        
        if (found && _metadata.TryGetValue(deviceKey, out var metadata))
        {
            metadata.AccessCount++;
            metadata.LastAccessed = DateTimeOffset.UtcNow;
        }

        return found;
    }

    /// <summary>
    /// Removes a kernel for a specific device from the cache.
    /// </summary>
    public bool RemoveKernel(IAccelerator device)
    {
        if (_disposed)
        {
            return false;
        }

        var deviceKey = GetDeviceKey(device);
        var removed = _kernelsByDevice.TryRemove(deviceKey, out var kernel);
        
        if (removed)
        {
            _metadata.TryRemove(deviceKey, out _);
            if (kernel != null)
            {
                _ = Task.Run(async () => await kernel.DisposeAsync());
            }
        }

        return removed;
    }

    /// <summary>
    /// Gets all device IDs that have cached kernels.
    /// </summary>
    public string[] GetCachedDeviceIds()
    {
        if (_disposed)
        {
            return [];
        }

        return [.. _metadata.Values.Select(m => m.DeviceId)];
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public KernelCacheStatistics GetStatistics()
    {
        if (_disposed)
        {
            return new KernelCacheStatistics();
        }

        return new KernelCacheStatistics
        {
            TotalKernels = _kernelsByDevice.Count,
            TotalAccessCount = _metadata.Values.Sum(m => m.AccessCount),
            DeviceTypes = _metadata.Values.GroupBy(m => m.DeviceType).ToDictionary(g => g.Key, g => g.Count()),
            OldestCacheTime = _metadata.Values.DefaultIfEmpty().Min(m => m?.CachedAt ?? DateTimeOffset.MaxValue),
            NewestCacheTime = _metadata.Values.DefaultIfEmpty().Max(m => m?.CachedAt ?? DateTimeOffset.MinValue),
            MostAccessedKernel = _metadata.Values.OrderByDescending(m => m.AccessCount).FirstOrDefault()?.KernelName,
            LeastAccessedKernel = _metadata.Values.OrderBy(m => m.AccessCount).FirstOrDefault()?.KernelName
        };
    }

    /// <summary>
    /// Clears all cached kernels.
    /// </summary>
    public async ValueTask ClearAsync()
    {
        if (_disposed)
        {
            return;
        }

        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }

            var kernels = _kernelsByDevice.Values.ToArray();
            _kernelsByDevice.Clear();
            _metadata.Clear();

            // Dispose kernels outside the lock to avoid blocking
            Task.Run(async () =>
            {
                foreach (var kernel in kernels)
                {
                    try
                    {
                        if (kernel != null)
                        {
                            // Handle both IAsyncDisposable and IDisposable
                            await kernel.DisposeAsync();
                        }
                        await Task.Yield(); // Allow other work to proceed
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }
            });
        }

        await ValueTask.CompletedTask;
    }

    /// <summary>
    /// Removes least recently used kernels to free memory.
    /// </summary>
    public async ValueTask EvictLeastRecentlyUsedAsync(int maxKernelsToKeep)
    {
        if (_disposed || _kernelsByDevice.Count <= maxKernelsToKeep)
        {
            return;
        }

        var sortedMetadata = _metadata.Values
            .OrderBy(m => m.LastAccessed)
            .Take(_kernelsByDevice.Count - maxKernelsToKeep)
            .ToArray();

        var kernelsToDispose = new List<ManagedCompiledKernel>();

        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var metadata in sortedMetadata)
            {
                var deviceKey = GetDeviceKeyFromMetadata(metadata);
                if (_kernelsByDevice.TryRemove(deviceKey, out var kernel))
                {
                    _metadata.TryRemove(deviceKey, out _);
                    kernelsToDispose.Add(kernel);
                }
            }
        }

        // Dispose kernels outside the lock
        foreach (var kernel in kernelsToDispose)
        {
            try
            {
                if (kernel != null)
                {
                    await kernel.DisposeAsync();
                }
                await Task.Yield();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
    }

    /// <summary>
    /// Gets kernels that haven't been accessed for the specified duration.
    /// </summary>
    public string[] GetStaleKernels(TimeSpan maxAge)
    {
        if (_disposed)
        {
            return [];
        }

        var cutoffTime = DateTimeOffset.UtcNow - maxAge;
        return [.. _metadata.Values
            .Where(m => m.LastAccessed < cutoffTime)
            .Select(m => m.DeviceId)];
    }

    /// <summary>
    /// Removes stale kernels that haven't been accessed recently.
    /// </summary>
    public async ValueTask<int> RemoveStaleKernelsAsync(TimeSpan maxAge)
    {
        var staleDeviceIds = GetStaleKernels(maxAge);
        var removedCount = 0;
        var kernelsToDispose = new List<ManagedCompiledKernel>();

        lock (_disposeLock)
        {
            if (_disposed)
            {
                return 0;
            }

            foreach (var deviceId in staleDeviceIds)
            {
                // Find the device key that matches this device ID
                var deviceKey = _metadata.FirstOrDefault(kvp => kvp.Value.DeviceId == deviceId).Key;
                if (deviceKey != null && _kernelsByDevice.TryRemove(deviceKey, out var kernel))
                {
                    _metadata.TryRemove(deviceKey, out _);
                    kernelsToDispose.Add(kernel);
                    removedCount++;
                }
            }
        }

        // Dispose kernels outside the lock
        foreach (var kernel in kernelsToDispose)
        {
            try
            {
                if (kernel != null)
                {
                    await kernel.DisposeAsync();
                }
                await Task.Yield();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        return removedCount;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        await ClearAsync().ConfigureAwait(false);
    }

    #region Private Methods

    private static string GetDeviceKey(IAccelerator device)
    {
        // Create a unique key for the device
        return $"{device.Info.DeviceType}_{device.Info.Id}";
    }

    private static string GetDeviceKeyFromMetadata(KernelMetadata metadata)
    {
        return $"{metadata.DeviceType}_{metadata.DeviceId}";
    }

    #endregion
}

/// <summary>
/// Metadata about a cached kernel.
/// </summary>
public class KernelMetadata
{
    /// <summary>Gets or sets the device identifier.</summary>
    public required string DeviceId { get; set; }
    
    /// <summary>Gets or sets the device type.</summary>
    public required string DeviceType { get; set; }
    
    /// <summary>Gets or sets the kernel name.</summary>
    public required string KernelName { get; set; }
    
    /// <summary>Gets or sets when the kernel was cached.</summary>
    public required DateTimeOffset CachedAt { get; set; }
    
    /// <summary>Gets or sets the number of times this kernel has been accessed.</summary>
    public long AccessCount { get; set; }
    
    /// <summary>Gets or sets when the kernel was last accessed.</summary>
    public DateTimeOffset LastAccessed { get; set; }
}

/// <summary>
/// Statistics about the kernel cache.
/// </summary>
public class KernelCacheStatistics
{
    /// <summary>Gets or sets the total number of cached kernels.</summary>
    public int TotalKernels { get; set; }
    
    /// <summary>Gets or sets the total number of cache accesses.</summary>
    public long TotalAccessCount { get; set; }
    
    /// <summary>Gets or sets the number of kernels by device type.</summary>
    public Dictionary<string, int> DeviceTypes { get; set; } = [];
    
    /// <summary>Gets or sets the oldest cache time.</summary>
    public DateTimeOffset OldestCacheTime { get; set; }
    
    /// <summary>Gets or sets the newest cache time.</summary>
    public DateTimeOffset NewestCacheTime { get; set; }
    
    /// <summary>Gets or sets the name of the most accessed kernel.</summary>
    public string? MostAccessedKernel { get; set; }
    
    /// <summary>Gets or sets the name of the least accessed kernel.</summary>
    public string? LeastAccessedKernel { get; set; }
}

/// <summary>
/// Global cache manager for compiled kernels across all parallel execution strategies.
/// </summary>
public sealed class GlobalKernelCacheManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, CompiledKernelCache> _cachesByKernel;
    private readonly Timer _cleanupTimer;
    private readonly Lock _statsLock = new();
    private GlobalCacheStatistics _statistics;
    private bool _disposed;

    private const int CleanupIntervalMs = 300000; // 5 minutes
    private const int MaxKernelsPerCache = 50;
    private static readonly TimeSpan MaxKernelAge = TimeSpan.FromHours(2);

    public GlobalKernelCacheManager()
    {
        _cachesByKernel = new ConcurrentDictionary<string, CompiledKernelCache>();
        _statistics = new GlobalCacheStatistics();
        
        _cleanupTimer = new Timer(async _ => await PerformCleanupAsync(), null, 
            CleanupIntervalMs, CleanupIntervalMs);
    }

    /// <summary>
    /// Gets or creates a cache for a specific kernel.
    /// </summary>
    public CompiledKernelCache GetOrCreateCache(string kernelName)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GlobalKernelCacheManager));
        }

        return _cachesByKernel.GetOrAdd(kernelName, _ => new CompiledKernelCache());
    }

    /// <summary>
    /// Gets global cache statistics.
    /// </summary>
    public GlobalCacheStatistics GetStatistics()
    {
        if (_disposed)
        {
            return new GlobalCacheStatistics();
        }

        lock (_statsLock)
        {
            _statistics.TotalCaches = _cachesByKernel.Count;
            _statistics.TotalKernels = _cachesByKernel.Values.Sum(c => c.Count);
            _statistics.KernelNames = [.. _cachesByKernel.Keys];
            _statistics.LastCleanupTime = _statistics.LastCleanupTime; // Keep existing value
            
            return new GlobalCacheStatistics
            {
                TotalCaches = _statistics.TotalCaches,
                TotalKernels = _statistics.TotalKernels,
                KernelNames = [.. _statistics.KernelNames],
                LastCleanupTime = _statistics.LastCleanupTime,
                CleanupCount = _statistics.CleanupCount
            };
        }
    }

    /// <summary>
    /// Removes all caches and kernels.
    /// </summary>
    public async ValueTask ClearAllAsync()
    {
        if (_disposed)
        {
            return;
        }

        var caches = _cachesByKernel.Values.ToArray();
        _cachesByKernel.Clear();

        var clearTasks = caches.Select(cache => cache.ClearAsync()).ToArray();
        await Task.WhenAll(clearTasks.Select(vt => vt.AsTask())).ConfigureAwait(false);

        lock (_statsLock)
        {
            _statistics = new GlobalCacheStatistics();
        }
    }

    /// <summary>
    /// Forces immediate cleanup of stale kernels.
    /// </summary>
    public async ValueTask ForceCleanupAsync()
    {
        await PerformCleanupAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        
        _cleanupTimer.Dispose();
        await ClearAllAsync().ConfigureAwait(false);
        
        // Dispose all remaining caches
        var disposeTasks = _cachesByKernel.Values.Select(cache => cache.DisposeAsync()).ToArray();
        foreach (var task in disposeTasks)
        {
            await task.ConfigureAwait(false);
        }
    }

    #region Private Methods

    private async ValueTask PerformCleanupAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var cleanupTasks = new List<Task>();

            foreach (var cache in _cachesByKernel.Select(kvp => kvp.Value))
            {
                // Remove stale kernels
                cleanupTasks.Add(cache.RemoveStaleKernelsAsync(MaxKernelAge).AsTask());
                
                // Evict LRU kernels if cache is too large
                if (cache.Count > MaxKernelsPerCache)
                {
                    cleanupTasks.Add(cache.EvictLeastRecentlyUsedAsync(MaxKernelsPerCache).AsTask());
                }
            }

            await Task.WhenAll(cleanupTasks).ConfigureAwait(false);

            // Remove empty caches
            var emptyCaches = _cachesByKernel
                .Where(kvp => kvp.Value.IsEmpty)
                .Select(kvp => kvp.Key)
                .ToArray();

            foreach (var kernelName in emptyCaches)
            {
                if (_cachesByKernel.TryRemove(kernelName, out var emptyCache))
                {
                    await emptyCache.DisposeAsync().ConfigureAwait(false);
                }
            }

            lock (_statsLock)
            {
                _statistics.LastCleanupTime = DateTimeOffset.UtcNow;
                _statistics.CleanupCount++;
            }
        }
        catch (Exception)
        {
            // Ignore cleanup errors - they shouldn't affect the main execution
        }
    }

    #endregion
}

/// <summary>
/// Global cache statistics.
/// </summary>
public class GlobalCacheStatistics
{
    /// <summary>Gets or sets the total number of kernel caches.</summary>
    public int TotalCaches { get; set; }
    
    /// <summary>Gets or sets the total number of cached kernels across all caches.</summary>
    public int TotalKernels { get; set; }
    
    /// <summary>Gets or sets the names of all cached kernels.</summary>
    public string[] KernelNames { get; set; } = [];
    
    /// <summary>Gets or sets when the last cleanup was performed.</summary>
    public DateTimeOffset LastCleanupTime { get; set; }
    
    /// <summary>Gets or sets the number of cleanup operations performed.</summary>
    public long CleanupCount { get; set; }
}}
