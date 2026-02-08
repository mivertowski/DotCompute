// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pooling;

/// <summary>
/// Generic interface for high-performance resource pooling.
/// Provides thread-safe rent/return semantics with statistics tracking.
/// </summary>
/// <typeparam name="T">The type of resource being pooled.</typeparam>
/// <remarks>
/// <para>
/// This interface defines the contract for all resource pools in DotCompute, including:
/// </para>
/// <list type="bullet">
/// <item><description>Memory buffer pools (CUDA, Metal, OpenCL)</description></item>
/// <item><description>Event pools (CUDA, Metal, OpenCL)</description></item>
/// <item><description>Stream/queue pools</description></item>
/// <item><description>Generic object pools</description></item>
/// </list>
/// <para>
/// Implementations should use lock-free data structures where possible
/// and track hit/miss statistics for performance monitoring.
/// </para>
/// </remarks>
public interface IResourcePool<T> : IAsyncDisposable, IDisposable where T : class
{
    /// <summary>
    /// Gets the pool identifier for logging and diagnostics.
    /// </summary>
    public string PoolId { get; }

    /// <summary>
    /// Gets the current number of resources available in the pool.
    /// </summary>
    public int AvailableCount { get; }

    /// <summary>
    /// Gets the maximum number of resources the pool can hold.
    /// </summary>
    public int MaxPoolSize { get; }

    /// <summary>
    /// Gets the total number of resources created by this pool.
    /// </summary>
    public long TotalCreated { get; }

    /// <summary>
    /// Gets the pool hit rate as a fraction (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// Hit rate = PoolHits / (PoolHits + PoolMisses).
    /// A high hit rate (>80%) indicates efficient pool utilization.
    /// </remarks>
    public double HitRate { get; }

    /// <summary>
    /// Gets comprehensive statistics about pool performance.
    /// </summary>
    public ResourcePoolStatistics Statistics { get; }

    /// <summary>
    /// Rents a resource from the pool, creating a new one if the pool is empty.
    /// </summary>
    /// <returns>A resource instance.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    public T Rent();

    /// <summary>
    /// Rents a resource from the pool asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the rented resource.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    public ValueTask<T> RentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a resource to the pool for reuse.
    /// </summary>
    /// <param name="resource">The resource to return.</param>
    /// <remarks>
    /// If the pool is full or disposed, the resource will be cleaned up immediately.
    /// Resources that fail validation will also be cleaned up rather than returned.
    /// </remarks>
    public void Return(T resource);

    /// <summary>
    /// Returns a resource to the pool asynchronously.
    /// </summary>
    /// <param name="resource">The resource to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask ReturnAsync(T resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all resources from the pool, cleaning them up.
    /// </summary>
    public void Clear();

    /// <summary>
    /// Clears all resources from the pool asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs maintenance operations on the pool (e.g., trimming excess resources).
    /// </summary>
    /// <returns>The number of resources cleaned up.</returns>
    public int PerformMaintenance();

    /// <summary>
    /// Performs maintenance operations asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of resources cleaned up.</returns>
    public ValueTask<int> PerformMaintenanceAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about resource pool performance.
/// </summary>
public sealed class ResourcePoolStatistics
{
    /// <summary>
    /// Gets the pool identifier.
    /// </summary>
    public string PoolId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current number of resources in the pool.
    /// </summary>
    public int CurrentPoolSize { get; init; }

    /// <summary>
    /// Gets the maximum pool size.
    /// </summary>
    public int MaxPoolSize { get; init; }

    /// <summary>
    /// Gets the total number of rent operations (hits + misses).
    /// </summary>
    public long TotalRentOperations { get; init; }

    /// <summary>
    /// Gets the number of pool hits (resources retrieved from pool).
    /// </summary>
    public long PoolHits { get; init; }

    /// <summary>
    /// Gets the number of pool misses (new resources created).
    /// </summary>
    public long PoolMisses { get; init; }

    /// <summary>
    /// Gets the total number of resources created by this pool.
    /// </summary>
    public long TotalCreated { get; init; }

    /// <summary>
    /// Gets the total number of resources destroyed by this pool.
    /// </summary>
    public long TotalDestroyed { get; init; }

    /// <summary>
    /// Gets the number of resources recycled (returned and reused).
    /// </summary>
    public long TotalRecycled { get; init; }

    /// <summary>
    /// Gets the pool hit rate as a fraction (0.0 to 1.0).
    /// </summary>
    public double HitRate => TotalRentOperations > 0
        ? (double)PoolHits / TotalRentOperations
        : 0.0;

    /// <summary>
    /// Gets the hit rate as a percentage (0.0 to 100.0).
    /// </summary>
    public double HitRatePercentage => HitRate * 100.0;

    /// <summary>
    /// Gets the pool utilization rate (current size / max size).
    /// </summary>
    public double UtilizationRate => MaxPoolSize > 0
        ? (double)CurrentPoolSize / MaxPoolSize
        : 0.0;

    /// <summary>
    /// Gets the time of last maintenance operation.
    /// </summary>
    public DateTimeOffset? LastMaintenanceTime { get; init; }

    /// <summary>
    /// Gets the number of maintenance operations performed.
    /// </summary>
    public long MaintenanceCount { get; init; }

    /// <summary>
    /// Gets the total resources cleaned up during maintenance.
    /// </summary>
    public long TotalMaintenanceCleanups { get; init; }
}
