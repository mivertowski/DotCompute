// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Infrastructure;

/// <summary>
/// Abstract base class for OpenCL resource pool implementations.
/// Provides generic pooling logic with statistics tracking and automatic cleanup.
/// </summary>
/// <typeparam name="T">The type of resource being pooled.</typeparam>
/// <remarks>
/// This base class implements a high-performance object pooling pattern optimized for OpenCL resources:
/// <list type="bullet">
/// <item><description>ConcurrentBag-based pooling for lock-free operations</description></item>
/// <item><description>Automatic resource cleanup on disposal</description></item>
/// <item><description>Comprehensive statistics tracking (hits, misses, recycling)</description></item>
/// <item><description>Configurable pool size limits</description></item>
/// <item><description>Thread-safe operation tracking using Interlocked</description></item>
/// </list>
/// </remarks>
public abstract class OpenCLPoolBase<T> : IAsyncDisposable where T : class
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly int _maxPoolSize;
    private readonly ILogger _logger;
    private bool _isDisposed;

    // Statistics - NOT readonly because they are modified via Interlocked operations
#pragma warning disable IDE0044 // Add readonly modifier
    private long _poolHits;
    private long _poolMisses;
    private long _resourcesCreated;
    private long _resourcesRecycled;
    private long _currentPoolSize;
#pragma warning restore IDE0044 // Add readonly modifier

    /// <summary>
    /// Gets the logger instance for this pool.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Gets a value indicating whether this pool has been disposed.
    /// </summary>
    protected bool IsDisposed => _isDisposed;

    /// <summary>
    /// Increments a counter atomically.
    /// </summary>
    protected static void IncrementCounter(ref long counter)
    {
        Interlocked.Increment(ref counter);
    }

    /// <summary>
    /// Decrements a counter atomically.
    /// </summary>
    protected static void DecrementCounter(ref long counter)
    {
        Interlocked.Decrement(ref counter);
    }

    /// <summary>
    /// Ensures the pool is initialized and not disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    protected void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, GetType());
    }

    /// <summary>
    /// Gets the name of this manager for logging purposes.
    /// </summary>
    protected abstract string ManagerName { get; }

    /// <summary>
    /// Gets the maximum number of items that can be stored in the pool.
    /// </summary>
    public int MaxPoolSize => _maxPoolSize;

    /// <summary>
    /// Gets the current number of items in the pool.
    /// </summary>
    public long CurrentPoolSize => Interlocked.Read(ref _currentPoolSize);

    /// <summary>
    /// Gets the total number of pool hits (items retrieved from pool).
    /// </summary>
    public long PoolHits => Interlocked.Read(ref _poolHits);

    /// <summary>
    /// Gets the total number of pool misses (items created because pool was empty).
    /// </summary>
    public long PoolMisses => Interlocked.Read(ref _poolMisses);

    /// <summary>
    /// Gets the total number of resources created by this pool.
    /// </summary>
    public long ResourcesCreated => Interlocked.Read(ref _resourcesCreated);

    /// <summary>
    /// Gets the total number of resources recycled back to the pool.
    /// </summary>
    public long ResourcesRecycled => Interlocked.Read(ref _resourcesRecycled);

    /// <summary>
    /// Gets the pool hit rate as a percentage (0-100).
    /// </summary>
    public double HitRate
    {
        get
        {
            var hits = PoolHits;
            var total = hits + PoolMisses;
            return total == 0 ? 0.0 : (hits * 100.0) / total;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLPoolBase{T}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for this pool.</param>
    /// <param name="maxPoolSize">The maximum number of items that can be stored in the pool.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxPoolSize"/> is less than 1.</exception>
    protected OpenCLPoolBase(ILogger logger, int maxPoolSize = 256)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (maxPoolSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPoolSize), "Max pool size must be at least 1");
        }

        _logger = logger;
        _maxPoolSize = maxPoolSize;
    }

    /// <summary>
    /// Rents a resource from the pool or creates a new one if the pool is empty.
    /// </summary>
    /// <returns>A resource instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pool is not initialized or has been disposed.</exception>
    public T Rent()
    {
        EnsureInitialized();

        if (_pool.TryTake(out T? item))
        {
            IncrementCounter(ref _poolHits);
            DecrementCounter(ref _currentPoolSize);

            Logger.LogDebug("{ManagerName}: Pool hit (Total hits: {Hits}, Pool size: {Size})",
                ManagerName, PoolHits, CurrentPoolSize);

            return item;
        }

        IncrementCounter(ref _poolMisses);
        IncrementCounter(ref _resourcesCreated);

        Logger.LogDebug("{ManagerName}: Pool miss, creating new resource (Total misses: {Misses}, Created: {Created})",
            ManagerName, PoolMisses, ResourcesCreated);

        return CreateResource();
    }

    /// <summary>
    /// Returns a resource to the pool for reuse.
    /// </summary>
    /// <param name="item">The resource to return to the pool.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
    public void Return(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IsDisposed)
        {
            // If disposed, clean up the item immediately
            CleanupResource(item);
            return;
        }

        var currentSize = Interlocked.Read(ref _currentPoolSize);

        if (currentSize >= _maxPoolSize)
        {
            Logger.LogDebug("{ManagerName}: Pool full, disposing resource (Size: {Size}/{Max})",
                ManagerName, currentSize, MaxPoolSize);

            CleanupResource(item);
            return;
        }

        if (ValidateResource(item))
        {
            ResetResource(item);
            _pool.Add(item);
            IncrementCounter(ref _currentPoolSize);
            IncrementCounter(ref _resourcesRecycled);

            Logger.LogDebug("{ManagerName}: Resource returned to pool (Size: {Size}/{Max}, Recycled: {Recycled})",
                ManagerName, CurrentPoolSize, MaxPoolSize, ResourcesRecycled);
        }
        else
        {
            Logger.LogWarning("{ManagerName}: Resource validation failed, disposing instead of pooling", ManagerName);
            CleanupResource(item);
        }
    }

    /// <summary>
    /// Clears all items from the pool and cleans them up.
    /// </summary>
    public void Clear()
    {
        Logger.LogInformation("{ManagerName}: Clearing pool (Current size: {Size})", ManagerName, CurrentPoolSize);

        while (_pool.TryTake(out T? item))
        {
            CleanupResource(item);
            DecrementCounter(ref _currentPoolSize);
        }

        Logger.LogInformation("{ManagerName}: Pool cleared", ManagerName);
    }

    /// <summary>
    /// Gets statistics about pool performance.
    /// </summary>
    /// <value>A formatted string containing pool statistics.</value>
    public string Statistics
    {
        get
        {
            return $"{ManagerName} Statistics:\n" +
                   $"  Pool Size: {CurrentPoolSize}/{MaxPoolSize}\n" +
                   $"  Pool Hits: {PoolHits}\n" +
                   $"  Pool Misses: {PoolMisses}\n" +
                   $"  Hit Rate: {HitRate:F2}%\n" +
                   $"  Resources Created: {ResourcesCreated}\n" +
                   $"  Resources Recycled: {ResourcesRecycled}";
        }
    }

    /// <summary>
    /// Creates a new resource instance.
    /// </summary>
    /// <returns>A new resource instance.</returns>
    protected abstract T CreateResource();

    /// <summary>
    /// Validates whether a resource is still valid for pooling.
    /// </summary>
    /// <param name="item">The resource to validate.</param>
    /// <returns>True if the resource is valid; otherwise, false.</returns>
    protected virtual bool ValidateResource(T item) => true;

    /// <summary>
    /// Resets a resource to its initial state before returning it to the pool.
    /// </summary>
    /// <param name="item">The resource to reset.</param>
    protected virtual void ResetResource(T item) { }

    /// <summary>
    /// Cleans up a resource that is being removed from the pool.
    /// </summary>
    /// <param name="item">The resource to clean up.</param>
    protected virtual void CleanupResource(T item)
    {
        if (item is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Disposes the pool and releases all resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Clear();
        await Task.CompletedTask.ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
