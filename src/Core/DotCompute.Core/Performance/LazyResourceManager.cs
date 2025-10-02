// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using global::System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Performance;

/// <summary>
/// Advanced lazy resource manager with intelligent initialization patterns:
/// - Just-in-time resource creation with minimal overhead
/// - Thread-safe lazy initialization with race condition prevention
/// - Resource lifecycle management with automatic cleanup
/// - Dependency injection integration for service resolution
/// - Performance monitoring and optimization metrics
/// - Memory-efficient resource pooling and reuse
/// - Async-first design with cancellation support
/// Target: 70-90% reduction in startup time and memory usage
/// </summary>
public sealed class LazyResourceManager<T> : IDisposable where T : class
{
    private readonly Func<CancellationToken, Task<T>> _factory;
    private readonly Func<T, Task>? _disposer;
    private readonly ILogger<LazyResourceManager<T>> _logger;
    private readonly LazyResourceConfiguration _config;
    private readonly ConcurrentDictionary<string, LazyResource<T>> _resources;
    private readonly SemaphoreSlim _initializationSemaphore;
    private readonly Timer _maintenanceTimer;

    // Performance counters

    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;
    private long _totalInitializations;
    private long _totalDisposals;
    private long _averageInitializationTime;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new lazy resource manager.
    /// </summary>
    /// <param name="factory">Factory function to create resources.</param>
    /// <param name="disposer">Optional disposal function for resources.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="config">Configuration options.</param>
    public LazyResourceManager(
        Func<CancellationToken, Task<T>> factory,
        Func<T, Task>? disposer = null,
        ILogger<LazyResourceManager<T>>? logger = null,
        LazyResourceConfiguration? config = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _disposer = disposer;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LazyResourceManager<T>>.Instance;
        _config = config ?? LazyResourceConfiguration.Default;
        _resources = new ConcurrentDictionary<string, LazyResource<T>>();
        _initializationSemaphore = new SemaphoreSlim(_config.MaxConcurrentInitializations,

                                                      _config.MaxConcurrentInitializations);

        // Setup maintenance timer for cleanup and optimization
        _maintenanceTimer = new Timer(PerformMaintenance, null,
            _config.MaintenanceInterval, _config.MaintenanceInterval);

        _logger.LogDebug("Lazy resource manager initialized for type {Type} with configuration {Config}",
            typeof(T).Name, _config);
    }

    /// <summary>
    /// Gets performance statistics for the resource manager.
    /// </summary>
    public LazyResourceStatistics Statistics => new()
    {
        TotalRequests = Interlocked.Read(ref _totalRequests),
        CacheHits = Interlocked.Read(ref _cacheHits),
        CacheMisses = Interlocked.Read(ref _cacheMisses),
        TotalInitializations = Interlocked.Read(ref _totalInitializations),
        TotalDisposals = Interlocked.Read(ref _totalDisposals),
        AverageInitializationTime = TimeSpan.FromTicks(Interlocked.Read(ref _averageInitializationTime)),
        ActiveResources = _resources.Count,
        HitRate = CalculateHitRate()
    };

    /// <summary>
    /// Gets or creates a resource with lazy initialization.
    /// </summary>
    /// <param name="key">Unique key for the resource.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The initialized resource.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<T> GetResourceAsync(string key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(key);


        _ = Interlocked.Increment(ref _totalRequests);

        // Fast path: resource already exists and is initialized
        if (_resources.TryGetValue(key, out var existingResource))
        {
            if (existingResource.IsInitialized)
            {
                _ = Interlocked.Increment(ref _cacheHits);
                existingResource.UpdateLastAccess();
                return existingResource.Value!;
            }
        }

        // Slow path: resource needs initialization
        _ = Interlocked.Increment(ref _cacheMisses);
        return await InitializeResourceAsync(key, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a resource if it exists and is initialized, without triggering initialization.
    /// </summary>
    /// <param name="key">Unique key for the resource.</param>
    /// <returns>The resource if available, null otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? TryGetResource(string key)
    {
        ThrowIfDisposed();


        if (_resources.TryGetValue(key, out var resource) && resource.IsInitialized)
        {
            resource.UpdateLastAccess();
            return resource.Value;
        }


        return null;
    }

    /// <summary>
    /// Preloads a resource in the background without blocking.
    /// </summary>
    /// <param name="key">Unique key for the resource.</param>
    /// <param name="priority">Preload priority.</param>
    /// <returns>Task representing the preload operation.</returns>
    public Task PreloadResourceAsync(string key, PreloadPriority priority = PreloadPriority.Normal)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(key);

        var cancellationToken = _config.PreloadTimeout.HasValue

            ? new CancellationTokenSource(_config.PreloadTimeout.Value).Token

            : CancellationToken.None;

        return priority switch
        {
            PreloadPriority.High => Task.Run(() => GetResourceAsync(key, cancellationToken)),
            PreloadPriority.Normal => Task.Run(() => GetResourceAsync(key, cancellationToken)),
            PreloadPriority.Low => Task.Delay(100, cancellationToken)
                .ContinueWith(_ => GetResourceAsync(key, cancellationToken),
                             cancellationToken,

                             TaskContinuationOptions.NotOnCanceled,

                             TaskScheduler.Default),
            _ => Task.CompletedTask
        };
    }

    /// <summary>
    /// Preloads multiple resources concurrently.
    /// </summary>
    /// <param name="keys">Resource keys to preload.</param>
    /// <param name="maxConcurrency">Maximum concurrent preloads.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing all preload operations.</returns>
    public async Task PreloadResourcesAsync(
        IEnumerable<string> keys,

        int maxConcurrency = 4,

        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(keys);


        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = keys.Select(async key =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _ = await GetResourceAsync(key, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ = semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        semaphore.Dispose();
    }

    /// <summary>
    /// Invalidates a resource, causing it to be recreated on next access.
    /// </summary>
    /// <param name="key">Resource key to invalidate.</param>
    /// <param name="disposeExisting">Whether to dispose the existing resource.</param>
    /// <returns>Task representing the invalidation operation.</returns>
    public async Task InvalidateResourceAsync(string key, bool disposeExisting = true)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(key);

        if (_resources.TryRemove(key, out var resource))
        {
            if (disposeExisting && resource.IsInitialized)
            {
                await DisposeResourceAsync(resource).ConfigureAwait(false);
            }


            _logger.LogDebug("Invalidated resource with key '{Key}'", key);
        }
    }

    /// <summary>
    /// Invalidates all resources.
    /// </summary>
    /// <param name="disposeExisting">Whether to dispose existing resources.</param>
    /// <returns>Task representing the invalidation operation.</returns>
    public async Task InvalidateAllResourcesAsync(bool disposeExisting = true)
    {
        ThrowIfDisposed();


        var resources = _resources.ToArray();
        _resources.Clear();

        if (disposeExisting)
        {
            var disposalTasks = resources
                .Where(kvp => kvp.Value.IsInitialized)
                .Select(kvp => DisposeResourceAsync(kvp.Value));


            await Task.WhenAll(disposalTasks).ConfigureAwait(false);
        }


        _logger.LogDebug("Invalidated all {Count} resources", resources.Length);
    }

    /// <summary>
    /// Gets all currently loaded resource keys.
    /// </summary>
    /// <returns>Collection of loaded resource keys.</returns>
    public IReadOnlyCollection<string> GetLoadedResourceKeys()
    {
        ThrowIfDisposed();
        return _resources.Keys.ToArray();
    }

    /// <summary>
    /// Gets resource information including initialization status and access times.
    /// </summary>
    /// <param name="key">Resource key.</param>
    /// <returns>Resource information if found.</returns>
    public ResourceInfo? GetResourceInfo(string key)
    {
        ThrowIfDisposed();


        if (_resources.TryGetValue(key, out var resource))
        {
            return new ResourceInfo
            {
                Key = key,
                IsInitialized = resource.IsInitialized,
                CreatedAt = resource.CreatedAt,
                LastAccessTime = resource.LastAccessTime,
                AccessCount = resource.AccessCount,
                InitializationTime = resource.InitializationTime
            };
        }


        return null;
    }

    private async Task<T> InitializeResourceAsync(string key, CancellationToken cancellationToken)
    {
        // Get or create the lazy resource
        var lazyResource = _resources.GetOrAdd(key, _ => new LazyResource<T>(key));

        // Double-check pattern for thread safety

        if (lazyResource.IsInitialized)
        {
            return lazyResource.Value!;
        }

        // Acquire semaphore to limit concurrent initializations
        await _initializationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check again after acquiring semaphore
            if (lazyResource.IsInitialized)
            {
                return lazyResource.Value!;
            }

            // Initialize the resource
            var startTime = DateTimeOffset.UtcNow;
            _logger.LogDebug("Initializing resource with key '{Key}'", key);


            try
            {
                var resource = await _factory(cancellationToken).ConfigureAwait(false);
                var initializationTime = DateTimeOffset.UtcNow - startTime;


                lazyResource.SetValue(resource, initializationTime);


                _ = Interlocked.Increment(ref _totalInitializations);
                RecordInitializationTime(initializationTime);


                _logger.LogDebug("Successfully initialized resource '{Key}' in {Time}ms",

                    key, initializationTime.TotalMilliseconds);


                return resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize resource with key '{Key}'", key);

                // Remove failed resource from cache

                _ = _resources.TryRemove(key, out _);
                throw;
            }
        }
        finally
        {
            _ = _initializationSemaphore.Release();
        }
    }

    private async Task DisposeResourceAsync(LazyResource<T> resource)
    {
        if (!resource.IsInitialized)
        {
            return;
        }


        try
        {
            if (_disposer != null)
            {
                await _disposer(resource.Value!).ConfigureAwait(false);
            }
            else if (resource.Value is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (resource.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }


            _ = Interlocked.Increment(ref _totalDisposals);
            _logger.LogTrace("Disposed resource with key '{Key}'", resource.Key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing resource with key '{Key}'", resource.Key);
        }
    }

    private void PerformMaintenance(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            _ = Task.Run(async () =>
            {
                await CleanupExpiredResourcesAsync().ConfigureAwait(false);
                LogStatistics();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during resource manager maintenance");
        }
    }

    private async Task CleanupExpiredResourcesAsync()
    {
        if (!_config.ResourceLifetime.HasValue)
        {
            return;
        }


        var cutoff = DateTimeOffset.UtcNow - _config.ResourceLifetime.Value;
        var expiredKeys = new List<string>();


        foreach (var kvp in _resources)
        {
            if (kvp.Value.LastAccessTime < cutoff)
            {
                expiredKeys.Add(kvp.Key);
            }
        }


        foreach (var key in expiredKeys)
        {
            await InvalidateResourceAsync(key, disposeExisting: true).ConfigureAwait(false);
        }


        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired resources", expiredKeys.Count);
        }
    }

    private void LogStatistics()
    {
        var stats = Statistics;
        _logger.LogTrace(
            "Resource manager stats - Requests: {Requests}, Hit Rate: {HitRate:P2}, " +
            "Active: {Active}, Avg Init Time: {InitTime}ms",
            stats.TotalRequests, stats.HitRate, stats.ActiveResources,

            stats.AverageInitializationTime.TotalMilliseconds);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordInitializationTime(TimeSpan initializationTime)
    {
        var ticks = initializationTime.Ticks;
        var currentAvg = Interlocked.Read(ref _averageInitializationTime);
        var totalInits = Interlocked.Read(ref _totalInitializations);

        // Update running average

        var newAvg = totalInits > 1

            ? (currentAvg * (totalInits - 1) + ticks) / totalInits

            : ticks;


        _ = Interlocked.Exchange(ref _averageInitializationTime, newAvg);
    }

    private double CalculateHitRate()
    {
        var hits = Interlocked.Read(ref _cacheHits);
        var total = Interlocked.Read(ref _totalRequests);
        return total > 0 ? (double)hits / total : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LazyResourceManager<T>));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _maintenanceTimer?.Dispose();
            _initializationSemaphore?.Dispose();

            // Dispose all resources synchronously

            var disposalTasks = _resources.Values
                .Where(r => r.IsInitialized)
                .Select(DisposeResourceAsync);


            try
            {
                Task.WhenAll(disposalTasks).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing resources during manager disposal");
            }


            _resources.Clear();
            _logger.LogDebug("Lazy resource manager disposed for type {Type}", typeof(T).Name);
        }
    }
}

#region Supporting Types

/// <summary>
/// Represents a lazy-initialized resource with metadata.
/// </summary>
internal sealed class LazyResource<T> where T : class
{
    private readonly object _lock = new();
    private T? _value;
    private volatile bool _isInitialized;
    private long _accessCount;


    public string Key { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset LastAccessTime { get; private set; }
    public TimeSpan InitializationTime { get; private set; }
    public bool IsInitialized => _isInitialized;
    public long AccessCount => Interlocked.Read(ref _accessCount);


    public T? Value
    {
        get
        {
            UpdateLastAccess();
            return _value;
        }
    }

    public LazyResource(string key)
    {
        Key = key;
        CreatedAt = DateTimeOffset.UtcNow;
        LastAccessTime = CreatedAt;
    }

    public void SetValue(T value, TimeSpan initializationTime)
    {
        lock (_lock)
        {
            if (!_isInitialized)
            {
                _value = value;
                InitializationTime = initializationTime;
                _isInitialized = true;
                UpdateLastAccess();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateLastAccess()
    {
        LastAccessTime = DateTimeOffset.UtcNow;
        _ = Interlocked.Increment(ref _accessCount);
    }
}

/// <summary>
/// Configuration options for lazy resource management.
/// </summary>
public sealed class LazyResourceConfiguration
{
    /// <summary>
    /// Maximum number of concurrent resource initializations.
    /// </summary>
    public int MaxConcurrentInitializations { get; init; } = 4;


    /// <summary>
    /// Lifetime of resources before cleanup.
    /// </summary>
    public TimeSpan? ResourceLifetime { get; init; } = TimeSpan.FromHours(1);


    /// <summary>
    /// Interval for maintenance operations.
    /// </summary>
    public TimeSpan MaintenanceInterval { get; init; } = TimeSpan.FromMinutes(10);


    /// <summary>
    /// Timeout for preload operations.
    /// </summary>
    public TimeSpan? PreloadTimeout { get; init; } = TimeSpan.FromMinutes(5);


    /// <summary>
    /// Default configuration optimized for general use.
    /// </summary>
    public static LazyResourceConfiguration Default => new();


    /// <summary>
    /// Configuration optimized for high-frequency access.
    /// </summary>
    public static LazyResourceConfiguration HighFrequency => new()
    {
        MaxConcurrentInitializations = 8,
        ResourceLifetime = TimeSpan.FromMinutes(30),
        MaintenanceInterval = TimeSpan.FromMinutes(5),
        PreloadTimeout = TimeSpan.FromMinutes(2)
    };


    /// <summary>
    /// Configuration optimized for memory-constrained environments.
    /// </summary>
    public static LazyResourceConfiguration MemoryConstrained => new()
    {
        MaxConcurrentInitializations = 2,
        ResourceLifetime = TimeSpan.FromMinutes(15),
        MaintenanceInterval = TimeSpan.FromMinutes(2),
        PreloadTimeout = TimeSpan.FromMinutes(1)
    };


    public override string ToString()
    {
        return $"LazyResourceConfig(MaxConcurrent={MaxConcurrentInitializations}, " +
               $"Lifetime={ResourceLifetime}, Maintenance={MaintenanceInterval})";
    }
}

/// <summary>
/// Performance statistics for the lazy resource manager.
/// </summary>
public readonly record struct LazyResourceStatistics
{
    public long TotalRequests { get; init; }
    public long CacheHits { get; init; }
    public long CacheMisses { get; init; }
    public long TotalInitializations { get; init; }
    public long TotalDisposals { get; init; }
    public TimeSpan AverageInitializationTime { get; init; }
    public int ActiveResources { get; init; }
    public double HitRate { get; init; }


    public long LiveResources => TotalInitializations - TotalDisposals;
    public double InitializationRatio => TotalRequests > 0 ? (double)TotalInitializations / TotalRequests : 0.0;
}

/// <summary>
/// Information about a specific resource.
/// </summary>
public readonly record struct ResourceInfo
{
    public string Key { get; init; }
    public bool IsInitialized { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastAccessTime { get; init; }
    public long AccessCount { get; init; }
    public TimeSpan InitializationTime { get; init; }


    public TimeSpan Age => DateTimeOffset.UtcNow - CreatedAt;
    public TimeSpan TimeSinceLastAccess => DateTimeOffset.UtcNow - LastAccessTime;
}

/// <summary>
/// Priority levels for resource preloading.
/// </summary>
public enum PreloadPriority
{
    Low,
    Normal,
    High
}

#endregion

/// <summary>
/// Factory class for creating typed lazy resource managers.
/// </summary>
public static class LazyResourceManager
{
    /// <summary>
    /// Creates a new lazy resource manager for the specified type.
    /// </summary>
    /// <typeparam name="T">Resource type.</typeparam>
    /// <param name="factory">Factory function to create resources.</param>
    /// <param name="disposer">Optional disposal function.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="config">Optional configuration.</param>
    /// <returns>Configured lazy resource manager.</returns>
    public static LazyResourceManager<T> Create<T>(
        Func<CancellationToken, Task<T>> factory,
        Func<T, Task>? disposer = null,
        ILogger<LazyResourceManager<T>>? logger = null,
        LazyResourceConfiguration? config = null) where T : class
    {
        return new LazyResourceManager<T>(factory, disposer, logger, config);
    }


    /// <summary>
    /// Creates a new lazy resource manager with synchronous factory.
    /// </summary>
    /// <typeparam name="T">Resource type.</typeparam>
    /// <param name="factory">Synchronous factory function.</param>
    /// <param name="disposer">Optional disposal function.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="config">Optional configuration.</param>
    /// <returns>Configured lazy resource manager.</returns>
    public static LazyResourceManager<T> Create<T>(
        Func<T> factory,
        Action<T>? disposer = null,
        ILogger<LazyResourceManager<T>>? logger = null,
        LazyResourceConfiguration? config = null) where T : class
    {
        var asyncFactory = new Func<CancellationToken, Task<T>>(_ => Task.FromResult(factory()));
        var asyncDisposer = disposer != null

            ? new Func<T, Task>(resource => { disposer(resource); return Task.CompletedTask; })
            : null;


        return new LazyResourceManager<T>(asyncFactory, asyncDisposer, logger, config);
    }
}
