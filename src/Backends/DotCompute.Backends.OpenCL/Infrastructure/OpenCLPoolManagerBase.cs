// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Infrastructure;

/// <summary>
/// Abstract base class for OpenCL pool managers providing generic pooling infrastructure
/// for various resource types (streams, events, memory).
/// </summary>
/// <typeparam name="TResource">
/// The type of resource being pooled. Must be a reference type.
/// </typeparam>
/// <remarks>
/// <para>
/// This class implements a production-grade object pooling pattern with:
/// <list type="bullet">
/// <item><description>Thread-safe pool operations with lock-free data structures</description></item>
/// <item><description>Automatic pool growth and shrink based on usage patterns</description></item>
/// <item><description>Resource validation and health monitoring</description></item>
/// <item><description>Comprehensive statistics collection (hits, misses, creations)</description></item>
/// <item><description>Configurable pool sizes and behavior</description></item>
/// <item><description>Proper resource lifecycle management</description></item>
/// </list>
/// </para>
/// <para>
/// Derived classes must implement:
/// <list type="bullet">
/// <item><description><see cref="CreateResourceAsync"/> - Creates new resources</description></item>
/// <item><description><see cref="ValidateResourceAsync"/> - Validates resource health</description></item>
/// <item><description><see cref="ResetResourceAsync"/> - Resets resource for reuse</description></item>
/// </list>
/// </para>
/// <para>
/// Thread Safety: This class is fully thread-safe. All public and protected methods can be
/// called concurrently from multiple threads.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class StreamPool : OpenCLPoolManagerBase&lt;OpenCLCommandQueue&gt;
/// {
///     protected override async ValueTask&lt;OpenCLCommandQueue&gt; CreateResourceAsync(
///         CancellationToken cancellationToken)
///     {
///         return await CreateNewQueueAsync(cancellationToken);
///     }
///
///     protected override async ValueTask&lt;bool&gt; ValidateResourceAsync(
///         OpenCLCommandQueue resource, CancellationToken cancellationToken)
///     {
///         return await IsQueueValidAsync(resource, cancellationToken);
///     }
/// }
/// </code>
/// </example>
public abstract class OpenCLPoolManagerBase<TResource> : OpenCLResourceManager
    where TResource : class
{
    private readonly ConcurrentBag<TResource> _availableResources;
    private readonly ConcurrentDictionary<int, TResource> _activeResources;
    private readonly SemaphoreSlim _poolSemaphore;
    private readonly Timer? _maintenanceTimer;

    // Pool configuration
    private readonly int _minPoolSize;
    private readonly int _maxPoolSize;
    private readonly int _initialPoolSize;
    private readonly TimeSpan _poolTimeout;
    private readonly bool _enableAutoScaling;
    private readonly double _targetHitRate;

    // Pool statistics
    private long _poolHits;
    private long _poolMisses;
    private long _resourceCreations;
    private long _resourceRecycles;
    private long _resourceDestructions;
    private long _validationFailures;
    private int _nextResourceId;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLPoolManagerBase{TResource}"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    /// <param name="minPoolSize">The minimum number of resources to keep in the pool.</param>
    /// <param name="maxPoolSize">The maximum number of resources in the pool.</param>
    /// <param name="initialPoolSize">The number of resources to pre-create.</param>
    /// <param name="poolTimeout">The timeout for acquiring resources from the pool.</param>
    /// <param name="enableAutoScaling">Whether to enable automatic pool size adjustment.</param>
    /// <param name="targetHitRate">The target pool hit rate for auto-scaling (0.0-1.0).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when pool size parameters are invalid.
    /// </exception>
    protected OpenCLPoolManagerBase(
        ILogger logger,
        int minPoolSize = 2,
        int maxPoolSize = 16,
        int initialPoolSize = 4,
        TimeSpan? poolTimeout = null,
        bool enableAutoScaling = true,
        double targetHitRate = 0.8)
        : base(logger)
    {
        if (minPoolSize < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minPoolSize),
                minPoolSize,
                "Minimum pool size must be non-negative.");
        }

        if (maxPoolSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPoolSize),
                maxPoolSize,
                "Maximum pool size must be at least 1.");
        }

        if (initialPoolSize < 0 || initialPoolSize > maxPoolSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialPoolSize),
                initialPoolSize,
                $"Initial pool size must be between 0 and {maxPoolSize}.");
        }

        if (minPoolSize > maxPoolSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minPoolSize),
                minPoolSize,
                $"Minimum pool size cannot exceed maximum pool size ({maxPoolSize}).");
        }

        if (targetHitRate < 0.0 || targetHitRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetHitRate),
                targetHitRate,
                "Target hit rate must be between 0.0 and 1.0.");
        }

        _minPoolSize = minPoolSize;
        _maxPoolSize = maxPoolSize;
        _initialPoolSize = initialPoolSize;
        _poolTimeout = poolTimeout ?? TimeSpan.FromSeconds(30);
        _enableAutoScaling = enableAutoScaling;
        _targetHitRate = targetHitRate;

        _availableResources = new ConcurrentBag<TResource>();
        _activeResources = new ConcurrentDictionary<int, TResource>();
        _poolSemaphore = new SemaphoreSlim(maxPoolSize, maxPoolSize);

        // Setup maintenance timer for periodic pool health checks
        if (_enableAutoScaling)
        {
            _maintenanceTimer = new Timer(
                PerformMaintenance,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));
        }
        else
        {
            _maintenanceTimer = null;
        }

        Logger.LogDebug(
            "{PoolType} created: min={MinSize}, max={MaxSize}, initial={InitialSize}, " +
            "timeout={Timeout}s, autoScaling={AutoScaling}",
            GetType().Name, _minPoolSize, _maxPoolSize, _initialPoolSize,
            _poolTimeout.TotalSeconds, _enableAutoScaling);
    }

    /// <summary>
    /// Gets the current number of available resources in the pool.
    /// </summary>
    protected int AvailableCount => _availableResources.Count;

    /// <summary>
    /// Gets the current number of active (in-use) resources.
    /// </summary>
    protected int ActiveCount => _activeResources.Count;

    /// <summary>
    /// Gets the current pool hit rate (0.0 to 1.0).
    /// </summary>
    protected double PoolHitRate => CalculateHitRate();

    /// <summary>
    /// Acquires a resource from the pool, creating a new one if necessary.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the acquired resource.
    /// </returns>
    /// <exception cref="TimeoutException">
    /// Thrown when a resource cannot be acquired within the configured timeout.
    /// </exception>
    /// <exception cref="Exception">
    /// Thrown when resource creation fails.
    /// </exception>
    /// <remarks>
    /// This method implements the core pooling logic:
    /// <list type="number">
    /// <item><description>Try to get a resource from the pool (cache hit)</description></item>
    /// <item><description>If pool is empty, create a new resource (cache miss)</description></item>
    /// <item><description>Validate the resource before returning it</description></item>
    /// <item><description>Track the resource as active</description></item>
    /// </list>
    /// </remarks>
    public async ValueTask<PooledResource<TResource>> AcquireAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfNotReady();

        // Try to get from pool first
        if (_availableResources.TryTake(out var resource))
        {
            Interlocked.Increment(ref _poolHits);
            RecordSuccess();

            // Validate the pooled resource
            if (await ValidateResourceAsync(resource, cancellationToken).ConfigureAwait(false))
            {
                // Reset resource for reuse
                await ResetResourceAsync(resource, cancellationToken).ConfigureAwait(false);

                var resourceId = Interlocked.Increment(ref _nextResourceId);
                _activeResources[resourceId] = resource;

                Logger.LogTrace(
                    "{PoolType} pool hit: resourceId={ResourceId}, hitRate={HitRate:P2}",
                    GetType().Name, resourceId, PoolHitRate);

                return new PooledResource<TResource>(this, resource, resourceId);
            }

            // Validation failed, create new resource
            Interlocked.Increment(ref _validationFailures);
            Logger.LogWarning(
                "{PoolType} pooled resource failed validation, creating new",
                GetType().Name);
        }

        // Pool miss - need to create new resource
        Interlocked.Increment(ref _poolMisses);

        // Wait for semaphore slot (respects max pool size)
        if (!await _poolSemaphore.WaitAsync(_poolTimeout, cancellationToken).ConfigureAwait(false))
        {
            RecordFailure();
            throw new TimeoutException(
                $"Failed to acquire resource from {GetType().Name} within {_poolTimeout.TotalSeconds:F2}s. " +
                $"Pool is at capacity ({_maxPoolSize} resources).");
        }

        try
        {
            resource = await CreateResourceAsync(cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _resourceCreations);
            RecordSuccess();

            var resourceId = Interlocked.Increment(ref _nextResourceId);
            _activeResources[resourceId] = resource;

            Logger.LogDebug(
                "{PoolType} created resource: resourceId={ResourceId}, totalCreated={TotalCreated}, " +
                "hitRate={HitRate:P2}",
                GetType().Name, resourceId, _resourceCreations, PoolHitRate);

            return new PooledResource<TResource>(this, resource, resourceId);
        }
        catch
        {
            _poolSemaphore.Release();
            RecordFailure();
            throw;
        }
    }

    /// <summary>
    /// Returns a resource to the pool for reuse.
    /// </summary>
    /// <param name="resourceId">The unique identifier of the resource.</param>
    /// <param name="resource">The resource to return.</param>
    /// <remarks>
    /// This method is called automatically when a <see cref="PooledResource{TResource}"/> is disposed.
    /// The resource is either returned to the pool or destroyed based on pool capacity and health.
    /// </remarks>
    internal async ValueTask ReturnAsync(int resourceId, TResource resource)
    {
        if (IsDisposed)
        {
            return;
        }

        if (!_activeResources.TryRemove(resourceId, out _))
        {
            Logger.LogWarning(
                "{PoolType} attempted to return unknown resource: resourceId={ResourceId}",
                GetType().Name, resourceId);
            return;
        }

        // Validate resource before returning to pool
        if (!await ValidateResourceAsync(resource, CancellationToken.None).ConfigureAwait(false))
        {
            Interlocked.Increment(ref _validationFailures);
            await DestroyResourceAsync(resource).ConfigureAwait(false);
            _poolSemaphore.Release();

            Logger.LogDebug(
                "{PoolType} resource failed validation, destroyed: resourceId={ResourceId}",
                GetType().Name, resourceId);
            return;
        }

        // Check if pool has capacity
        if (_availableResources.Count < _maxPoolSize)
        {
            _availableResources.Add(resource);
            Interlocked.Increment(ref _resourceRecycles);
            _poolSemaphore.Release();

            Logger.LogTrace(
                "{PoolType} resource returned to pool: resourceId={ResourceId}, poolSize={PoolSize}",
                GetType().Name, resourceId, _availableResources.Count);
        }
        else
        {
            // Pool at capacity, destroy resource
            await DestroyResourceAsync(resource).ConfigureAwait(false);
            _poolSemaphore.Release();

            Logger.LogTrace(
                "{PoolType} pool at capacity, destroyed resource: resourceId={ResourceId}",
                GetType().Name, resourceId);
        }
    }

    /// <summary>
    /// Gets comprehensive statistics about pool operations and health.
    /// </summary>
    /// <returns>
    /// A <see cref="PoolStatistics"/> object containing pool size, hit rates, and operation counts.
    /// </returns>
    public PoolStatistics GetPoolStatistics()
    {
        var hitRate = CalculateHitRate();
        var baseStats = GetStatistics();

        return new PoolStatistics
        {
            ManagerType = GetType().Name,
            State = State,
            AvailableResources = _availableResources.Count,
            ActiveResources = _activeResources.Count,
            TotalCapacity = _maxPoolSize,
            MinPoolSize = _minPoolSize,
            MaxPoolSize = _maxPoolSize,
            PoolHits = Interlocked.Read(ref _poolHits),
            PoolMisses = Interlocked.Read(ref _poolMisses),
            HitRate = hitRate,
            ResourceCreations = Interlocked.Read(ref _resourceCreations),
            ResourceRecycles = Interlocked.Read(ref _resourceRecycles),
            ResourceDestructions = Interlocked.Read(ref _resourceDestructions),
            ValidationFailures = Interlocked.Read(ref _validationFailures),
            TotalOperations = baseStats.TotalOperations,
            SuccessRate = baseStats.SuccessRate,
            IsHealthy = baseStats.IsHealthy && hitRate >= (_targetHitRate * 0.8)
        };
    }

    /// <summary>
    /// Creates a new resource instance.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the creation.</param>
    /// <returns>
    /// A task that represents the asynchronous creation operation.
    /// The task result contains the newly created resource.
    /// </returns>
    /// <remarks>
    /// Derived classes must implement this method to create their specific resource type.
    /// This method is called when the pool needs to create a new resource.
    /// </remarks>
    protected abstract ValueTask<TResource> CreateResourceAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates that a resource is in a usable state.
    /// </summary>
    /// <param name="resource">The resource to validate.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the validation.</param>
    /// <returns>
    /// A task that represents the asynchronous validation operation.
    /// Returns <c>true</c> if the resource is valid; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Derived classes must implement this method to validate their specific resource type.
    /// Invalid resources are destroyed and not returned to the pool.
    /// </remarks>
    protected abstract ValueTask<bool> ValidateResourceAsync(
        TResource resource,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resets a resource to a clean state for reuse.
    /// </summary>
    /// <param name="resource">The resource to reset.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the reset operation.</param>
    /// <returns>A task that represents the asynchronous reset operation.</returns>
    /// <remarks>
    /// Derived classes can override this method to perform resource-specific cleanup
    /// before returning a resource to the pool. The default implementation does nothing.
    /// </remarks>
    protected virtual ValueTask ResetResourceAsync(
        TResource resource,
        CancellationToken cancellationToken)
    {
        // Default implementation does nothing - derived classes override as needed
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Destroys a resource and releases its associated native resources.
    /// </summary>
    /// <param name="resource">The resource to destroy.</param>
    /// <returns>A task that represents the asynchronous destruction operation.</returns>
    /// <remarks>
    /// Derived classes can override this method to perform resource-specific cleanup.
    /// The default implementation does nothing.
    /// </remarks>
    protected virtual ValueTask DestroyResourceAsync(TResource resource)
    {
        Interlocked.Increment(ref _resourceDestructions);
        // Default implementation does nothing - derived classes override as needed
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Initializes the pool by pre-creating resources.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel initialization.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    protected override async ValueTask InitializeResourcesAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            "{PoolType} pre-creating {InitialSize} resources",
            GetType().Name, _initialPoolSize);

        for (int i = 0; i < _initialPoolSize; i++)
        {
            try
            {
                var resource = await CreateResourceAsync(cancellationToken).ConfigureAwait(false);
                _availableResources.Add(resource);
                Interlocked.Increment(ref _resourceCreations);

                Logger.LogTrace(
                    "{PoolType} pre-created resource {Index}/{Total}",
                    GetType().Name, i + 1, _initialPoolSize);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    ex,
                    "{PoolType} failed to pre-create resource {Index}: {ErrorMessage}",
                    GetType().Name, i + 1, ex.Message);
                break;
            }
        }

        Logger.LogInformation(
            "{PoolType} initialization complete: {ActualSize}/{RequestedSize} resources created",
            GetType().Name, _availableResources.Count, _initialPoolSize);
    }

    /// <summary>
    /// Cleans up all pooled resources.
    /// </summary>
    /// <returns>A task that represents the asynchronous cleanup operation.</returns>
    protected override async ValueTask CleanupResourcesAsync()
    {
        Logger.LogInformation(
            "{PoolType} cleaning up resources: available={Available}, active={Active}",
            GetType().Name, _availableResources.Count, _activeResources.Count);

        // Dispose timer asynchronously if possible
        if (_maintenanceTimer != null)
        {
#pragma warning disable VSTHRD103 // Synchronous disposal is required here as Timer doesn't support DisposeAsync
            _maintenanceTimer.Dispose();
#pragma warning restore VSTHRD103
        }

        // Cleanup available resources
        while (_availableResources.TryTake(out var resource))
        {
            await DestroyResourceAsync(resource).ConfigureAwait(false);
        }

        // Cleanup active resources (should be empty, but handle gracefully)
        foreach (var resource in _activeResources.Values)
        {
            await DestroyResourceAsync(resource).ConfigureAwait(false);
        }

        _activeResources.Clear();
        _poolSemaphore.Dispose();

        var stats = GetPoolStatistics();
        Logger.LogInformation(
            "{PoolType} cleanup complete: created={Created}, recycled={Recycled}, " +
            "destroyed={Destroyed}, hitRate={HitRate:P2}",
            GetType().Name, stats.ResourceCreations, stats.ResourceRecycles,
            stats.ResourceDestructions, stats.HitRate);
    }

    private double CalculateHitRate()
    {
        var hits = Interlocked.Read(ref _poolHits);
        var misses = Interlocked.Read(ref _poolMisses);
        var total = hits + misses;

        return total > 0 ? (double)hits / total : 0.0;
    }

    private void PerformMaintenance(object? state)
    {
        if (IsDisposed || State != ResourceState.Ready)
        {
            return;
        }

        try
        {
            var stats = GetPoolStatistics();

            // Auto-scale pool if hit rate is below target
            if (stats.HitRate < _targetHitRate && stats.AvailableResources < _maxPoolSize)
            {
                Logger.LogDebug(
                    "{PoolType} pool hit rate ({HitRate:P2}) below target ({TargetRate:P2}), " +
                    "consider scaling up",
                    GetType().Name, stats.HitRate, _targetHitRate);
            }

            // Trim excess resources if pool is over capacity
            var targetSize = Math.Max(_minPoolSize, _availableResources.Count / 2);
            int trimmed = 0;

            while (_availableResources.Count > targetSize &&
                   _availableResources.TryTake(out var resource))
            {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                DestroyResourceAsync(resource).AsTask().Wait();
#pragma warning restore VSTHRD002
                trimmed++;
            }

            if (trimmed > 0)
            {
                Logger.LogDebug(
                    "{PoolType} trimmed {TrimmedCount} excess resources from pool",
                    GetType().Name, trimmed);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "{PoolType} maintenance encountered error: {ErrorMessage}",
                GetType().Name, ex.Message);
        }
    }
}

/// <summary>
/// RAII handle for pooled resources with automatic return-to-pool on disposal.
/// </summary>
/// <typeparam name="TResource">The type of pooled resource.</typeparam>
/// <remarks>
/// This struct ensures that resources are properly returned to the pool even if
/// exceptions occur during usage.
/// </remarks>
public readonly struct PooledResource<TResource> : IAsyncDisposable, IEquatable<PooledResource<TResource>>
    where TResource : class
{
    private readonly OpenCLPoolManagerBase<TResource>? _manager;
    private readonly int _resourceId;

    internal PooledResource(
        OpenCLPoolManagerBase<TResource> manager,
        TResource resource,
        int resourceId)
    {
        _manager = manager;
        Resource = resource;
        _resourceId = resourceId;
    }

    /// <summary>
    /// Gets the underlying pooled resource.
    /// </summary>
    public TResource Resource { get; }

    /// <summary>
    /// Asynchronously returns the resource to the pool.
    /// </summary>
    /// <returns>A task that represents the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_manager != null)
        {
            await _manager.ReturnAsync(_resourceId, Resource).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Determines whether the specified pooled resource is equal to the current pooled resource.
    /// </summary>
    /// <param name="other">The pooled resource to compare with the current pooled resource.</param>
    /// <returns>
    /// <c>true</c> if the specified pooled resource is equal to the current pooled resource; otherwise, <c>false</c>.
    /// </returns>
    public bool Equals(PooledResource<TResource> other)
    {
        return _resourceId == other._resourceId && ReferenceEquals(_manager, other._manager);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current pooled resource.
    /// </summary>
    /// <param name="obj">The object to compare with the current pooled resource.</param>
    /// <returns>
    /// <c>true</c> if the specified object is equal to the current pooled resource; otherwise, <c>false</c>.
    /// </returns>
    public override bool Equals(object? obj)
    {
        return obj is PooledResource<TResource> other && Equals(other);
    }

    /// <summary>
    /// Returns the hash code for this pooled resource.
    /// </summary>
    /// <returns>A hash code for the current pooled resource.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(_resourceId, _manager);
    }

    /// <summary>
    /// Determines whether two pooled resources are equal.
    /// </summary>
    /// <param name="left">The first pooled resource to compare.</param>
    /// <param name="right">The second pooled resource to compare.</param>
    /// <returns>
    /// <c>true</c> if the pooled resources are equal; otherwise, <c>false</c>.
    /// </returns>
    public static bool operator ==(PooledResource<TResource> left, PooledResource<TResource> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two pooled resources are not equal.
    /// </summary>
    /// <param name="left">The first pooled resource to compare.</param>
    /// <param name="right">The second pooled resource to compare.</param>
    /// <returns>
    /// <c>true</c> if the pooled resources are not equal; otherwise, <c>false</c>.
    /// </returns>
    public static bool operator !=(PooledResource<TResource> left, PooledResource<TResource> right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// Comprehensive statistics about pool operations and health.
/// </summary>
public sealed record PoolStatistics
{
    /// <summary>Gets the type name of the pool manager.</summary>
    public required string ManagerType { get; init; }

    /// <summary>Gets the current state of the pool.</summary>
    public ResourceState State { get; init; }

    /// <summary>Gets the number of resources currently available in the pool.</summary>
    public int AvailableResources { get; init; }

    /// <summary>Gets the number of resources currently in use.</summary>
    public int ActiveResources { get; init; }

    /// <summary>Gets the total capacity of the pool.</summary>
    public int TotalCapacity { get; init; }

    /// <summary>Gets the minimum pool size.</summary>
    public int MinPoolSize { get; init; }

    /// <summary>Gets the maximum pool size.</summary>
    public int MaxPoolSize { get; init; }

    /// <summary>Gets the total number of pool hits (successful immediate acquisitions).</summary>
    public long PoolHits { get; init; }

    /// <summary>Gets the total number of pool misses (required new resource creation).</summary>
    public long PoolMisses { get; init; }

    /// <summary>Gets the pool hit rate (0.0 to 1.0).</summary>
    public double HitRate { get; init; }

    /// <summary>Gets the total number of resources created.</summary>
    public long ResourceCreations { get; init; }

    /// <summary>Gets the total number of resources recycled back to the pool.</summary>
    public long ResourceRecycles { get; init; }

    /// <summary>Gets the total number of resources destroyed.</summary>
    public long ResourceDestructions { get; init; }

    /// <summary>Gets the total number of resource validation failures.</summary>
    public long ValidationFailures { get; init; }

    /// <summary>Gets the total number of operations performed.</summary>
    public long TotalOperations { get; init; }

    /// <summary>Gets the overall success rate (0.0 to 1.0).</summary>
    public double SuccessRate { get; init; }

    /// <summary>Gets a value indicating whether the pool is in a healthy state.</summary>
    public bool IsHealthy { get; init; }
}
