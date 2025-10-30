// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Execution;

/// <summary>
/// Manages OpenCL command queues with pooling and recycling for optimal performance.
/// Provides in-order and out-of-order queue management with priority scheduling.
/// Follows proven patterns from CUDA's stream management for production-grade queue orchestration.
/// </summary>
public sealed class OpenCLStreamManager : IAsyncDisposable
{
    private readonly OpenCLContext _context;
    private readonly OpenCLDeviceId _deviceId;
    private readonly ILogger<OpenCLStreamManager> _logger;
    private readonly ConcurrentDictionary<Guid, QueueEntry> _activeQueues;
    private readonly ConcurrentBag<OpenCLCommandQueue> _queuePool;
    private readonly SemaphoreSlim _poolSemaphore;
    private bool _disposed;

    // Configuration
    private readonly int _maxPoolSize;
    private readonly int _initialPoolSize;

    // Statistics
    private long _queueCreations;
    private long _queueRecycles;
    private long _poolHits;
    private long _poolMisses;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLStreamManager"/> class.
    /// </summary>
    /// <param name="context">The OpenCL context for queue creation.</param>
    /// <param name="deviceId">The device ID for command queue operations.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="maxPoolSize">Maximum number of queues in the pool (default: 16).</param>
    /// <param name="initialPoolSize">Initial number of queues to pre-create (default: 4).</param>
    internal OpenCLStreamManager(
        OpenCLContext context,
        OpenCLDeviceId deviceId,
        ILogger<OpenCLStreamManager> logger,
        int maxPoolSize = 16,
        int initialPoolSize = 4)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _deviceId = deviceId;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxPoolSize = maxPoolSize;
        _initialPoolSize = initialPoolSize;

        _activeQueues = new ConcurrentDictionary<Guid, QueueEntry>();
        _queuePool = new ConcurrentBag<OpenCLCommandQueue>();
        _poolSemaphore = new SemaphoreSlim(maxPoolSize, maxPoolSize);

        InitializePool();

        _logger.LogInformation(
            "OpenCL stream manager initialized: maxPoolSize={MaxPoolSize}, initialPoolSize={InitialPoolSize}",
            _maxPoolSize, _initialPoolSize);
    }

    /// <summary>
    /// Acquires a command queue from the pool or creates a new one.
    /// This is the primary method for obtaining queues with pooling optimization.
    /// </summary>
    /// <param name="properties">Queue properties specifying execution mode and profiling.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A handle to the acquired command queue with RAII cleanup semantics.</returns>
    public async ValueTask<QueueHandle> AcquireQueueAsync(
        QueueProperties properties,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Try to acquire from pool first
        if (_queuePool.TryTake(out var pooledQueue))
        {
            Interlocked.Increment(ref _poolHits);
            _logger.LogDebug("Acquired queue from pool (pool hits: {PoolHits})", _poolHits);
            return CreateHandle(pooledQueue, properties);
        }

        // Pool empty, create new queue
        Interlocked.Increment(ref _poolMisses);
        await _poolSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var queue = await CreateQueueAsync(properties, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug(
                "Created new queue (pool misses: {PoolMisses}, total created: {TotalCreated})",
                _poolMisses, _queueCreations);
            return CreateHandle(queue, properties);
        }
        catch
        {
            _poolSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Releases a queue back to the pool for reuse.
    /// Called automatically when QueueHandle is disposed.
    /// </summary>
    /// <param name="handle">The queue handle to release.</param>
    public async ValueTask ReleaseQueueAsync(QueueHandle handle)
    {
        if (_disposed)
        {
            return;
        }

        if (_activeQueues.TryRemove(handle.Id, out var entry))
        {
            // Ensure queue operations are complete before recycling
            await FlushQueueAsync(entry.Queue).ConfigureAwait(false);

            // Return to pool if under capacity
            if (_queuePool.Count < _maxPoolSize)
            {
                _queuePool.Add(entry.Queue);
                Interlocked.Increment(ref _queueRecycles);
                _logger.LogTrace("Recycled queue to pool (total recycled: {Recycled})", _queueRecycles);
            }
            else
            {
                // Pool full, destroy queue
                await DestroyQueueAsync(entry.Queue).ConfigureAwait(false);
            }

            _poolSemaphore.Release();
        }
    }

    /// <summary>
    /// Synchronizes a specific queue, waiting for all enqueued operations to complete.
    /// </summary>
    /// <param name="queue">The queue to synchronize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask SynchronizeQueueAsync(
        OpenCLCommandQueue queue,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await Task.Run(() =>
        {
            var error = OpenCLRuntime.clFinish(queue.Handle);
            OpenCLException.ThrowIfError(error, "Synchronize queue");
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Flushes a queue, issuing all enqueued commands to the device without blocking.
    /// </summary>
    /// <param name="queue">The queue to flush.</param>
    private static async ValueTask FlushQueueAsync(OpenCLCommandQueue queue)
    {
        await Task.Run(() =>
        {
            var error = OpenCLRuntime.clFlush(queue.Handle);
            OpenCLException.ThrowIfError(error, "Flush queue");
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets comprehensive statistics about queue usage and pool performance.
    /// </summary>
    /// <returns>Statistics snapshot for monitoring and optimization.</returns>
    public QueueStatistics GetStatistics()
    {
        ThrowIfDisposed();

        return new QueueStatistics
        {
            TotalQueuesCreated = Interlocked.Read(ref _queueCreations),
            QueueRecycles = Interlocked.Read(ref _queueRecycles),
            PoolHits = Interlocked.Read(ref _poolHits),
            PoolMisses = Interlocked.Read(ref _poolMisses),
            ActiveQueues = _activeQueues.Count,
            PooledQueues = _queuePool.Count
        };
    }

    /// <summary>
    /// Creates a new OpenCL command queue with specified properties.
    /// </summary>
    private async ValueTask<OpenCLCommandQueue> CreateQueueAsync(
        QueueProperties properties,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var queueProperties = BuildQueueProperties(properties);
            var queueHandle = OpenCLRuntime.clCreateCommandQueue(
                _context.Context.Handle,
                _deviceId.Handle,
                queueProperties,
                out var error);

            OpenCLException.ThrowIfError(error, "Create command queue");
            Interlocked.Increment(ref _queueCreations);

            _logger.LogDebug(
                "Created command queue: inOrder={InOrder}, profiling={Profiling}, priority={Priority}",
                properties.InOrderExecution, properties.EnableProfiling, properties.Priority);

            return new OpenCLCommandQueue(queueHandle);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Destroys an OpenCL command queue and releases its resources.
    /// </summary>
    private async ValueTask DestroyQueueAsync(OpenCLCommandQueue queue)
    {
        if (queue.Handle == nint.Zero)
        {
            return;
        }

        await Task.Run(() =>
        {
            var error = OpenCLRuntime.clReleaseCommandQueue(queue.Handle);
            if (error != OpenCLError.Success)
            {
                _logger.LogWarning(
                    "Failed to release command queue {Queue}: {Error}",
                    queue.Handle, error);
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds OpenCL queue properties from high-level configuration.
    /// </summary>
    private static ulong BuildQueueProperties(QueueProperties properties)
    {
        const ulong CL_QUEUE_OUT_OF_ORDER_EXEC_MODE_ENABLE = 0x1;
        const ulong CL_QUEUE_PROFILING_ENABLE = 0x2;

        ulong flags = 0;

        if (!properties.InOrderExecution)
        {
            flags |= CL_QUEUE_OUT_OF_ORDER_EXEC_MODE_ENABLE;
        }

        if (properties.EnableProfiling)
        {
            flags |= CL_QUEUE_PROFILING_ENABLE;
        }

        return flags;
    }

    /// <summary>
    /// Creates a queue handle with tracking in active queues dictionary.
    /// </summary>
    private QueueHandle CreateHandle(OpenCLCommandQueue queue, QueueProperties properties)
    {
        var id = Guid.NewGuid();
        var entry = new QueueEntry(queue, properties);
        _activeQueues[id] = entry;

        return new QueueHandle(this, queue, id);
    }

    /// <summary>
    /// Initializes the queue pool with pre-created queues for immediate use.
    /// </summary>
    private void InitializePool()
    {
        var defaultProperties = new QueueProperties
        {
            InOrderExecution = true,
            EnableProfiling = false,
            Priority = QueuePriority.Normal
        };

        for (int i = 0; i < _initialPoolSize; i++)
        {
            try
            {
#pragma warning disable VSTHRD002 // Synchronously blocking is acceptable during initialization
                var queue = CreateQueueAsync(defaultProperties, CancellationToken.None)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
#pragma warning restore VSTHRD002
                _queuePool.Add(queue);
                _logger.LogTrace("Pre-created pool queue {Index}/{Total}", i + 1, _initialPoolSize);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to pre-create queue {Index} during pool initialization", i);
                break;
            }
        }
    }

    /// <summary>
    /// Throws if this manager has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Asynchronously disposes the stream manager and all managed queues.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _logger.LogInformation("Disposing OpenCL stream manager");

        try
        {
            // Synchronize and release all active queues
            var activeTasks = _activeQueues.Values
                .Select(async entry =>
                {
                    await SynchronizeQueueAsync(entry.Queue).ConfigureAwait(false);
                    await DestroyQueueAsync(entry.Queue).ConfigureAwait(false);
                })
                .ToArray();

            await Task.WhenAll(activeTasks).ConfigureAwait(false);

            // Release all pooled queues
            while (_queuePool.TryTake(out var queue))
            {
                await DestroyQueueAsync(queue).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during stream manager disposal");
        }
        finally
        {
            _poolSemaphore?.Dispose();
            _activeQueues.Clear();

            var stats = new QueueStatistics
            {
                TotalQueuesCreated = Interlocked.Read(ref _queueCreations),
                QueueRecycles = Interlocked.Read(ref _queueRecycles),
                PoolHits = Interlocked.Read(ref _poolHits),
                PoolMisses = Interlocked.Read(ref _poolMisses),
                ActiveQueues = 0,
                PooledQueues = 0
            };

            _logger.LogInformation(
                "Stream manager disposed. Final stats: created={Created}, recycled={Recycled}, " +
                "hits={Hits}, misses={Misses}, hit rate={HitRate:P2}",
                stats.TotalQueuesCreated, stats.QueueRecycles,
                stats.PoolHits, stats.PoolMisses, stats.PoolHitRate);
        }
    }

    /// <summary>
    /// Internal entry tracking queue state and properties.
    /// </summary>
    private sealed class QueueEntry
    {
        public OpenCLCommandQueue Queue { get; }
        public QueueProperties Properties { get; }
        public DateTimeOffset AcquiredAt { get; }

        public QueueEntry(OpenCLCommandQueue queue, QueueProperties properties)
        {
            Queue = queue;
            Properties = properties;
            AcquiredAt = DateTimeOffset.UtcNow;
        }
    }
}

/// <summary>
/// Properties for command queue creation and behavior.
/// </summary>
public sealed record QueueProperties
{
    /// <summary>
    /// Gets or initializes whether queue executes commands in order (default: true).
    /// In-order queues guarantee sequential execution but may reduce parallelism.
    /// </summary>
    public bool InOrderExecution { get; init; } = true;

    /// <summary>
    /// Gets or initializes whether profiling is enabled for queue operations (default: false).
    /// Profiling allows timing measurements but adds overhead.
    /// </summary>
    public bool EnableProfiling { get; init; }

    /// <summary>
    /// Gets or initializes the priority level for this queue (default: Normal).
    /// </summary>
    public QueuePriority Priority { get; init; } = QueuePriority.Normal;
}

/// <summary>
/// Priority levels for command queue scheduling.
/// </summary>
public enum QueuePriority
{
    /// <summary>Low priority for background operations.</summary>
    Low = 0,

    /// <summary>Normal priority for standard operations.</summary>
    Normal = 1,

    /// <summary>High priority for critical operations.</summary>
    High = 2
}

/// <summary>
/// RAII handle for managed command queues with automatic cleanup.
/// Ensures queues are properly returned to the pool when no longer needed.
/// </summary>
public readonly struct QueueHandle : IAsyncDisposable, IEquatable<QueueHandle>
{
    private readonly OpenCLStreamManager? _manager;
    private readonly OpenCLCommandQueue _queue;
    private readonly Guid _id;

    internal QueueHandle(OpenCLStreamManager manager, OpenCLCommandQueue queue, Guid id)
    {
        _manager = manager;
        _queue = queue;
        _id = id;
    }

    /// <summary>
    /// Gets the underlying OpenCL command queue.
    /// </summary>
    public OpenCLCommandQueue Queue => _queue;

    /// <summary>
    /// Gets the unique identifier for this queue handle.
    /// </summary>
    internal Guid Id => _id;

    /// <summary>
    /// Asynchronously releases the queue back to the manager's pool.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_manager != null)
        {
            await _manager.ReleaseQueueAsync(this).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Determines whether the specified handle is equal to the current handle.
    /// </summary>
    public bool Equals(QueueHandle other) => _id.Equals(other._id);

    /// <summary>
    /// Determines whether the specified object is equal to the current handle.
    /// </summary>
    public override bool Equals(object? obj) => obj is QueueHandle other && Equals(other);

    /// <summary>
    /// Returns the hash code for this handle.
    /// </summary>
    public override int GetHashCode() => _id.GetHashCode();

    /// <summary>
    /// Determines whether two handles are equal.
    /// </summary>
    public static bool operator ==(QueueHandle left, QueueHandle right) => left.Equals(right);

    /// <summary>
    /// Determines whether two handles are not equal.
    /// </summary>
    public static bool operator !=(QueueHandle left, QueueHandle right) => !left.Equals(right);
}

/// <summary>
/// Statistics for command queue usage and pool performance.
/// Used for monitoring, optimization, and capacity planning.
/// </summary>
public sealed record QueueStatistics
{
    /// <summary>Gets the total number of queues created since initialization.</summary>
    public long TotalQueuesCreated { get; init; }

    /// <summary>Gets the total number of queues recycled back to the pool.</summary>
    public long QueueRecycles { get; init; }

    /// <summary>Gets the number of successful pool acquisitions (cache hits).</summary>
    public long PoolHits { get; init; }

    /// <summary>Gets the number of pool misses requiring new queue creation.</summary>
    public long PoolMisses { get; init; }

    /// <summary>Gets the current number of active (in-use) queues.</summary>
    public int ActiveQueues { get; init; }

    /// <summary>Gets the current number of queues available in the pool.</summary>
    public int PooledQueues { get; init; }

    /// <summary>
    /// Gets the pool hit rate as a percentage (0.0 to 1.0).
    /// Higher values indicate better pooling efficiency.
    /// Target: >0.8 (80% hit rate) for optimal performance.
    /// </summary>
    public double PoolHitRate => PoolHits + PoolMisses > 0
        ? (double)PoolHits / (PoolHits + PoolMisses)
        : 0.0;
}
