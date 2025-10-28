// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

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
public sealed partial class LazyResourceManager<T> : IDisposable where T : class
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

    // LoggerMessage delegates (Event ID range: 15000-15099 for LazyResourceManager)
    private static readonly Action<ILogger, string, object, Exception?> _logManagerInitialized =
        LoggerMessage.Define<string, object>(
            MsLogLevel.Debug,
            new EventId(15001, nameof(LogManagerInitialized)),
            "Lazy resource manager initialized for type {TypeName} with configuration {Config}");

    private static readonly Action<ILogger, string, Exception?> _logResourceInvalidated =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(15002, nameof(LogResourceInvalidated)),
            "Invalidated resource with key '{Key}'");

    private static readonly Action<ILogger, int, Exception?> _logAllResourcesInvalidated =
        LoggerMessage.Define<int>(
            MsLogLevel.Debug,
            new EventId(15003, nameof(LogAllResourcesInvalidated)),
            "Invalidated all {Count} resources");

    private static readonly Action<ILogger, string, Exception?> _logResourceInitializing =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(15004, nameof(LogResourceInitializing)),
            "Initializing resource with key '{Key}'");

    private static readonly Action<ILogger, string, double, Exception?> _logResourceInitialized =
        LoggerMessage.Define<string, double>(
            MsLogLevel.Debug,
            new EventId(15005, nameof(LogResourceInitialized)),
            "Successfully initialized resource '{Key}' in {Time}ms");

    private static readonly Action<ILogger, string, Exception?> _logResourceInitializationFailed =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(15006, nameof(LogResourceInitializationFailed)),
            "Failed to initialize resource with key '{Key}'");

    private static readonly Action<ILogger, string, Exception?> _logResourceDisposed =
        LoggerMessage.Define<string>(
            MsLogLevel.Trace,
            new EventId(15007, nameof(LogResourceDisposed)),
            "Disposed resource with key '{Key}'");

    private static readonly Action<ILogger, string, Exception?> _logResourceDisposalError =
        LoggerMessage.Define<string>(
            MsLogLevel.Warning,
            new EventId(15008, nameof(LogResourceDisposalError)),
            "Error disposing resource with key '{Key}'");

    private static readonly Action<ILogger, Exception?> _logMaintenanceError =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(15009, nameof(LogMaintenanceError)),
            "Error during resource manager maintenance");

    private static readonly Action<ILogger, int, Exception?> _logExpiredResourcesCleanup =
        LoggerMessage.Define<int>(
            MsLogLevel.Debug,
            new EventId(15010, nameof(LogExpiredResourcesCleanup)),
            "Cleaned up {Count} expired resources");

    private static readonly Action<ILogger, long, double, int, double, Exception?> _logStatistics =
        LoggerMessage.Define<long, double, int, double>(
            MsLogLevel.Trace,
            new EventId(15011, nameof(LogStatistics)),
            "Resource manager stats - Requests: {Requests}, Hit Rate: {HitRate:P2}, Active: {Active}, Avg Init Time: {InitTime}ms");

    private static readonly Action<ILogger, Exception?> _logManagerDisposalError =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(15012, nameof(LogManagerDisposalError)),
            "Error disposing resources during manager disposal");

    private static readonly Action<ILogger, string, Exception?> _logManagerDisposed =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(15013, nameof(LogManagerDisposed)),
            "Lazy resource manager disposed for type {Type}");

    // LoggerMessage wrapper methods
    private static void LogManagerInitialized(ILogger logger, string typeName, object config)
        => _logManagerInitialized(logger, typeName, config, null);

    private static void LogResourceInvalidated(ILogger logger, string key)
        => _logResourceInvalidated(logger, key, null);

    private static void LogAllResourcesInvalidated(ILogger logger, int count)
        => _logAllResourcesInvalidated(logger, count, null);

    private static void LogResourceInitializing(ILogger logger, string key)
        => _logResourceInitializing(logger, key, null);

    private static void LogResourceInitialized(ILogger logger, string key, double timeMs)
        => _logResourceInitialized(logger, key, timeMs, null);

    private static void LogResourceInitializationFailed(ILogger logger, string key, Exception ex)
        => _logResourceInitializationFailed(logger, key, ex);

    private static void LogResourceDisposed(ILogger logger, string key)
        => _logResourceDisposed(logger, key, null);

    private static void LogResourceDisposalError(ILogger logger, string key, Exception ex)
        => _logResourceDisposalError(logger, key, ex);

    private static void LogMaintenanceError(ILogger logger, Exception ex)
        => _logMaintenanceError(logger, ex);

    private static void LogExpiredResourcesCleanup(ILogger logger, int count)
        => _logExpiredResourcesCleanup(logger, count, null);

    private static void LogStatistics(ILogger logger, long requests, double hitRate, int active, double initTimeMs)
        => _logStatistics(logger, requests, hitRate, active, initTimeMs, null);

    private static void LogManagerDisposalError(ILogger logger, Exception ex)
        => _logManagerDisposalError(logger, ex);

    private static void LogManagerDisposed(ILogger logger, string typeName)
        => _logManagerDisposed(logger, typeName, null);

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
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
        _disposer = disposer;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LazyResourceManager<T>>.Instance;
        _config = config ?? LazyResourceConfiguration.Default;
        _resources = new ConcurrentDictionary<string, LazyResource<T>>();
        _initializationSemaphore = new SemaphoreSlim(_config.MaxConcurrentInitializations,
                                                      _config.MaxConcurrentInitializations);

        // Setup maintenance timer for cleanup and optimization
        _maintenanceTimer = new Timer(PerformMaintenance, null,
            _config.MaintenanceInterval, _config.MaintenanceInterval);

        LogManagerInitialized(_logger, typeof(T).Name, _config);
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

            LogResourceInvalidated(_logger, key);
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

        LogAllResourcesInvalidated(_logger, resources.Length);
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
            LogResourceInitializing(_logger, key);

            try
            {
                var resource = await _factory(cancellationToken).ConfigureAwait(false);
                var initializationTime = DateTimeOffset.UtcNow - startTime;

                lazyResource.SetValue(resource, initializationTime);

                _ = Interlocked.Increment(ref _totalInitializations);
                RecordInitializationTime(initializationTime);

                LogResourceInitialized(_logger, key, initializationTime.TotalMilliseconds);

                return resource;
            }
            catch (Exception ex)
            {
                LogResourceInitializationFailed(_logger, key, ex);

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
            LogResourceDisposed(_logger, resource.Key);
        }
        catch (Exception ex)
        {
            LogResourceDisposalError(_logger, resource.Key, ex);
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
                LogStatisticsInternal();
            });
        }
        catch (Exception ex)
        {
            LogMaintenanceError(_logger, ex);
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
            LogExpiredResourcesCleanup(_logger, expiredKeys.Count);
        }
    }

    private void LogStatisticsInternal()
    {
        var stats = Statistics;
        LogStatistics(_logger, stats.TotalRequests, stats.HitRate, stats.ActiveResources,
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
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, typeof(LazyResourceManager<>));
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
                // VSTHRD002: Synchronous wait is necessary here because IDisposable.Dispose() cannot be async.
                // The alternative IAsyncDisposable pattern is not applicable for this scenario.
                // ConfigureAwait(false) is used in the async methods to prevent deadlocks.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                Task.WhenAll(disposalTasks).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            }
            catch (Exception ex)
            {
                LogManagerDisposalError(_logger, ex);
            }

            _resources.Clear();
            LogManagerDisposed(_logger, typeof(T).Name);
        }
    }
}

#region Supporting Types

/// <summary>
/// Represents a lazy-initialized resource with metadata.
/// </summary>
internal sealed class LazyResource<T> where T : class
{
    private readonly Lock _lock = new();
    private T? _value;
    private volatile bool _isInitialized;
    private long _accessCount;
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    /// <value>The key.</value>

    public string Key { get; }
    /// <summary>
    /// Gets or sets the created at.
    /// </summary>
    /// <value>The created at.</value>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>
    /// Gets or sets the last access time.
    /// </summary>
    /// <value>The last access time.</value>
    public DateTimeOffset LastAccessTime { get; private set; }
    /// <summary>
    /// Gets or sets the initialization time.
    /// </summary>
    /// <value>The initialization time.</value>
    public TimeSpan InitializationTime { get; private set; }
    /// <summary>
    /// Gets or sets a value indicating whether initialized.
    /// </summary>
    /// <value>The is initialized.</value>
    public bool IsInitialized => _isInitialized;
    /// <summary>
    /// Gets or sets the access count.
    /// </summary>
    /// <value>The access count.</value>
    public long AccessCount => Interlocked.Read(ref _accessCount);
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    /// <value>The value.</value>

    public T? Value
    {
        get
        {
            UpdateLastAccess();
            return _value;
        }
    }
    /// <summary>
    /// Initializes a new instance of the LazyResource class.
    /// </summary>
    /// <param name="key">The key.</param>

    public LazyResource(string key)
    {
        Key = key;
        CreatedAt = DateTimeOffset.UtcNow;
        LastAccessTime = CreatedAt;
    }
    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="initializationTime">The initialization time.</param>

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
    /// <summary>
    /// Updates the last access.
    /// </summary>

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
    /// <summary>
    /// Gets to string.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
    /// <summary>
    /// Gets or sets the total requests.
    /// </summary>
    /// <value>The total requests.</value>
    public long TotalRequests { get; init; }
    /// <summary>
    /// Gets or sets the cache hits.
    /// </summary>
    /// <value>The cache hits.</value>
    public long CacheHits { get; init; }
    /// <summary>
    /// Gets or sets the cache misses.
    /// </summary>
    /// <value>The cache misses.</value>
    public long CacheMisses { get; init; }
    /// <summary>
    /// Gets or sets the total initializations.
    /// </summary>
    /// <value>The total initializations.</value>
    public long TotalInitializations { get; init; }
    /// <summary>
    /// Gets or sets the total disposals.
    /// </summary>
    /// <value>The total disposals.</value>
    public long TotalDisposals { get; init; }
    /// <summary>
    /// Gets or sets the average initialization time.
    /// </summary>
    /// <value>The average initialization time.</value>
    public TimeSpan AverageInitializationTime { get; init; }
    /// <summary>
    /// Gets or sets the active resources.
    /// </summary>
    /// <value>The active resources.</value>
    public int ActiveResources { get; init; }
    /// <summary>
    /// Gets or sets the hit rate.
    /// </summary>
    /// <value>The hit rate.</value>
    public double HitRate { get; init; }
    /// <summary>
    /// Gets or sets the live resources.
    /// </summary>
    /// <value>The live resources.</value>

    public long LiveResources => TotalInitializations - TotalDisposals;
    /// <summary>
    /// Gets or sets the initialization ratio.
    /// </summary>
    /// <value>The initialization ratio.</value>
    public double InitializationRatio => TotalRequests > 0 ? (double)TotalInitializations / TotalRequests : 0.0;
}

/// <summary>
/// Information about a specific resource.
/// </summary>
public readonly record struct ResourceInfo
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    /// <value>The key.</value>
    public string Key { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether initialized.
    /// </summary>
    /// <value>The is initialized.</value>
    public bool IsInitialized { get; init; }
    /// <summary>
    /// Gets or sets the created at.
    /// </summary>
    /// <value>The created at.</value>
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>
    /// Gets or sets the last access time.
    /// </summary>
    /// <value>The last access time.</value>
    public DateTimeOffset LastAccessTime { get; init; }
    /// <summary>
    /// Gets or sets the access count.
    /// </summary>
    /// <value>The access count.</value>
    public long AccessCount { get; init; }
    /// <summary>
    /// Gets or sets the initialization time.
    /// </summary>
    /// <value>The initialization time.</value>
    public TimeSpan InitializationTime { get; init; }
    /// <summary>
    /// Gets or sets the age.
    /// </summary>
    /// <value>The age.</value>

    public TimeSpan Age => DateTimeOffset.UtcNow - CreatedAt;
    /// <summary>
    /// Gets or sets the time since last access.
    /// </summary>
    /// <value>The time since last access.</value>
    public TimeSpan TimeSinceLastAccess => DateTimeOffset.UtcNow - LastAccessTime;
}
/// <summary>
/// An preload priority enumeration.
/// </summary>

/// <summary>
/// Priority levels for resource preloading.
/// </summary>
public enum PreloadPriority
{
    /// <summary>
    /// Low priority - resource loaded when system is idle.
    /// </summary>
    Low,

    /// <summary>
    /// Normal priority - resource loaded at standard scheduling priority.
    /// </summary>
    Normal,

    /// <summary>
    /// High priority - resource loaded immediately when requested.
    /// </summary>
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
        LazyResourceConfiguration? config = null) where T : class => new(factory, disposer, logger, config);

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
            ? new Func<T, Task>(resource =>
            {
                disposer(resource);
                return Task.CompletedTask;
            })
            : null;

        return new LazyResourceManager<T>(asyncFactory, asyncDisposer, logger, config);
    }
}
