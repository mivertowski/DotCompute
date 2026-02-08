// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.Pooling;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Pooling;

/// <summary>
/// Abstract base class for high-performance resource pools.
/// Provides thread-safe pooling with statistics tracking, maintenance timers, and disposal patterns.
/// </summary>
/// <typeparam name="T">The type of resource being pooled.</typeparam>
/// <remarks>
/// <para>
/// This base class implements the common patterns shared across all DotCompute resource pools:
/// </para>
/// <list type="bullet">
/// <item><description>ConcurrentBag-based storage for lock-free operations</description></item>
/// <item><description>Interlocked statistics tracking (hits, misses, created, destroyed)</description></item>
/// <item><description>Optional maintenance timer for periodic cleanup</description></item>
/// <item><description>Standard disposal pattern with async support</description></item>
/// <item><description>Resource validation before returning to pool</description></item>
/// </list>
/// <para>
/// Derived classes must implement resource creation, validation, reset, and cleanup.
/// </para>
/// </remarks>
public abstract class ResourcePoolBase<T> : IResourcePool<T> where T : class
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly int _maxPoolSize;
    private readonly ILogger _logger;
    private readonly Timer? _maintenanceTimer;
    private readonly TimeSpan _maintenanceInterval;
    private bool _disposed;

    // Statistics - modified via Interlocked operations
    private long _poolHits;
    private long _poolMisses;
    private long _totalCreated;
    private long _totalDestroyed;
    private long _totalRecycled;
    private long _currentPoolSize;
    private long _maintenanceCount;
    private long _totalMaintenanceCleanups;
    private DateTimeOffset? _lastMaintenanceTime;

    /// <summary>
    /// Gets the logger instance for this pool.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Gets whether this pool has been disposed.
    /// </summary>
    protected bool IsDisposed => _disposed;

    /// <summary>
    /// Gets the name of this pool for logging purposes.
    /// </summary>
    protected abstract string PoolName { get; }

    /// <inheritdoc/>
    public string PoolId { get; }

    /// <inheritdoc/>
    public int AvailableCount => (int)Interlocked.Read(ref _currentPoolSize);

    /// <inheritdoc/>
    public int MaxPoolSize => _maxPoolSize;

    /// <inheritdoc/>
    public long TotalCreated => Interlocked.Read(ref _totalCreated);

    /// <inheritdoc/>
    public double HitRate
    {
        get
        {
            var hits = Interlocked.Read(ref _poolHits);
            var misses = Interlocked.Read(ref _poolMisses);
            var total = hits + misses;
            return total > 0 ? (double)hits / total : 0.0;
        }
    }

    /// <inheritdoc/>
    public ResourcePoolStatistics Statistics => new()
    {
        PoolId = PoolId,
        CurrentPoolSize = AvailableCount,
        MaxPoolSize = _maxPoolSize,
        TotalRentOperations = Interlocked.Read(ref _poolHits) + Interlocked.Read(ref _poolMisses),
        PoolHits = Interlocked.Read(ref _poolHits),
        PoolMisses = Interlocked.Read(ref _poolMisses),
        TotalCreated = Interlocked.Read(ref _totalCreated),
        TotalDestroyed = Interlocked.Read(ref _totalDestroyed),
        TotalRecycled = Interlocked.Read(ref _totalRecycled),
        LastMaintenanceTime = _lastMaintenanceTime,
        MaintenanceCount = Interlocked.Read(ref _maintenanceCount),
        TotalMaintenanceCleanups = Interlocked.Read(ref _totalMaintenanceCleanups)
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourcePoolBase{T}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="maxPoolSize">Maximum number of resources to hold in the pool.</param>
    /// <param name="maintenanceInterval">Interval for automatic maintenance. Use TimeSpan.Zero to disable.</param>
    /// <param name="poolId">Optional pool identifier. If not provided, a GUID will be generated.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxPoolSize is less than 1.</exception>
    protected ResourcePoolBase(
        ILogger logger,
        int maxPoolSize = 256,
        TimeSpan maintenanceInterval = default,
        string? poolId = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (maxPoolSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPoolSize), "Max pool size must be at least 1");
        }

        _logger = logger;
        _maxPoolSize = maxPoolSize;
        _maintenanceInterval = maintenanceInterval == default ? TimeSpan.FromMinutes(5) : maintenanceInterval;
        PoolId = poolId ?? Guid.NewGuid().ToString("N")[..8];

        // Start maintenance timer if interval is positive
        if (_maintenanceInterval > TimeSpan.Zero)
        {
            _maintenanceTimer = new Timer(
                MaintenanceCallback,
                null,
                _maintenanceInterval,
                _maintenanceInterval);
        }

        _logger.LogDebug("{PoolName}[{PoolId}]: Initialized (MaxSize={MaxSize}, MaintenanceInterval={Interval})",
            PoolName, PoolId, maxPoolSize, _maintenanceInterval);
    }

    /// <inheritdoc/>
    public T Rent()
    {
        ThrowIfDisposed();

        if (_pool.TryTake(out var resource))
        {
            Interlocked.Increment(ref _poolHits);
            Interlocked.Decrement(ref _currentPoolSize);

            _logger.LogTrace("{PoolName}[{PoolId}]: Pool hit (Hits={Hits}, Size={Size})",
                PoolName, PoolId, Interlocked.Read(ref _poolHits), AvailableCount);

            return resource;
        }

        // Pool miss - create new resource
        Interlocked.Increment(ref _poolMisses);
        Interlocked.Increment(ref _totalCreated);

        _logger.LogTrace("{PoolName}[{PoolId}]: Pool miss, creating new resource (Misses={Misses}, Created={Created})",
            PoolName, PoolId, Interlocked.Read(ref _poolMisses), Interlocked.Read(ref _totalCreated));

        return CreateResource();
    }

    /// <inheritdoc/>
    public virtual ValueTask<T> RentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Rent());
    }

    /// <inheritdoc/>
    public void Return(T resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (_disposed)
        {
            // Pool is disposed, clean up immediately
            CleanupResource(resource);
            Interlocked.Increment(ref _totalDestroyed);
            return;
        }

        var currentSize = Interlocked.Read(ref _currentPoolSize);

        if (currentSize >= _maxPoolSize)
        {
            _logger.LogTrace("{PoolName}[{PoolId}]: Pool full, disposing resource (Size={Size}/{Max})",
                PoolName, PoolId, currentSize, _maxPoolSize);

            CleanupResource(resource);
            Interlocked.Increment(ref _totalDestroyed);
            return;
        }

        if (!ValidateResource(resource))
        {
            _logger.LogDebug("{PoolName}[{PoolId}]: Resource validation failed, disposing instead of pooling",
                PoolName, PoolId);

            CleanupResource(resource);
            Interlocked.Increment(ref _totalDestroyed);
            return;
        }

        ResetResource(resource);
        _pool.Add(resource);
        Interlocked.Increment(ref _currentPoolSize);
        Interlocked.Increment(ref _totalRecycled);

        _logger.LogTrace("{PoolName}[{PoolId}]: Resource returned (Size={Size}/{Max}, Recycled={Recycled})",
            PoolName, PoolId, AvailableCount, _maxPoolSize, Interlocked.Read(ref _totalRecycled));
    }

    /// <inheritdoc/>
    public virtual ValueTask ReturnAsync(T resource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Return(resource);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _logger.LogInformation("{PoolName}[{PoolId}]: Clearing pool (CurrentSize={Size})",
            PoolName, PoolId, AvailableCount);

        var clearedCount = 0;
        while (_pool.TryTake(out var resource))
        {
            CleanupResource(resource);
            Interlocked.Decrement(ref _currentPoolSize);
            Interlocked.Increment(ref _totalDestroyed);
            clearedCount++;
        }

        _logger.LogInformation("{PoolName}[{PoolId}]: Pool cleared ({Count} resources)",
            PoolName, PoolId, clearedCount);
    }

    /// <inheritdoc/>
    public virtual ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Clear();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public int PerformMaintenance()
    {
        if (_disposed)
        {
            return 0;
        }

        var targetSize = _maxPoolSize / 2;
        var cleanedCount = 0;

        while (AvailableCount > targetSize && _pool.TryTake(out var resource))
        {
            CleanupResource(resource);
            Interlocked.Decrement(ref _currentPoolSize);
            Interlocked.Increment(ref _totalDestroyed);
            cleanedCount++;
        }

        if (cleanedCount > 0)
        {
            Interlocked.Add(ref _totalMaintenanceCleanups, cleanedCount);
            _logger.LogDebug("{PoolName}[{PoolId}]: Maintenance cleaned up {Count} resources (Size={Size}/{Max})",
                PoolName, PoolId, cleanedCount, AvailableCount, _maxPoolSize);
        }

        Interlocked.Increment(ref _maintenanceCount);
        _lastMaintenanceTime = DateTimeOffset.UtcNow;

        return cleanedCount;
    }

    /// <inheritdoc/>
    public virtual ValueTask<int> PerformMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(PerformMaintenance());
    }

    /// <summary>
    /// Creates a new resource instance.
    /// </summary>
    /// <returns>A new resource instance.</returns>
    protected abstract T CreateResource();

    /// <summary>
    /// Validates whether a resource is still valid for pooling.
    /// </summary>
    /// <param name="resource">The resource to validate.</param>
    /// <returns>True if the resource is valid and can be returned to the pool.</returns>
    protected virtual bool ValidateResource(T resource) => true;

    /// <summary>
    /// Resets a resource to its initial state before returning to the pool.
    /// </summary>
    /// <param name="resource">The resource to reset.</param>
    protected virtual void ResetResource(T resource) { }

    /// <summary>
    /// Cleans up a resource that is being removed from the pool.
    /// </summary>
    /// <param name="resource">The resource to clean up.</param>
    protected virtual void CleanupResource(T resource)
    {
        if (resource is IDisposable disposable)
        {
            disposable.Dispose();
        }
        else if (resource is IAsyncDisposable asyncDisposable)
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits - required in synchronous cleanup path
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
    }

    /// <summary>
    /// Throws an ObjectDisposedException if this pool has been disposed.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }

    private void MaintenanceCallback(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            PerformMaintenance();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{PoolName}[{PoolId}]: Error during maintenance", PoolName, PoolId);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by this pool.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(); false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _maintenanceTimer?.Dispose();
            Clear();

            var stats = Statistics;
            _logger.LogInformation(
                "{PoolName}[{PoolId}]: Disposed - Stats: Created={Created}, Destroyed={Destroyed}, HitRate={HitRate:P1}",
                PoolName, PoolId, stats.TotalCreated, stats.TotalDestroyed, stats.HitRate);
        }

        _disposed = true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _maintenanceTimer?.Dispose();
        await ClearAsync().ConfigureAwait(false);

        var stats = Statistics;
        _logger.LogInformation(
            "{PoolName}[{PoolId}]: Disposed - Stats: Created={Created}, Destroyed={Destroyed}, HitRate={HitRate:P1}",
            PoolName, PoolId, stats.TotalCreated, stats.TotalDestroyed, stats.HitRate);

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
