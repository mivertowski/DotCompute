// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Execution;

/// <summary>
/// Manages OpenCL events with pooling and recycling for optimal performance.
/// Provides profiling support, event chaining, and wait list management.
/// Follows proven patterns from CUDA's event management for production-grade synchronization.
/// </summary>
public sealed class OpenCLEventManager : IAsyncDisposable
{
    private readonly OpenCLContext _context;
    private readonly ILogger<OpenCLEventManager> _logger;
    private readonly ConcurrentDictionary<Guid, EventEntry> _activeEvents;
    private readonly ConcurrentBag<OpenCLEventHandle> _eventPool;
    private readonly SemaphoreSlim _poolSemaphore;
    private bool _disposed;

    // Configuration
    private readonly int _maxPoolSize;
    private readonly int _initialPoolSize;

    // Statistics
    private long _eventCreations;
    private long _eventRecycles;
    private long _poolHits;
    private long _poolMisses;
    private long _profilingQueriesTotal;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLEventManager"/> class.
    /// </summary>
    /// <param name="context">The OpenCL context for event creation.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="maxPoolSize">Maximum number of events in the pool (default: 32).</param>
    /// <param name="initialPoolSize">Initial number of events to pre-create (default: 8).</param>
    internal OpenCLEventManager(
        OpenCLContext context,
        ILogger<OpenCLEventManager> logger,
        int maxPoolSize = 32,
        int initialPoolSize = 8)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxPoolSize = maxPoolSize;
        _initialPoolSize = initialPoolSize;

        _activeEvents = new ConcurrentDictionary<Guid, EventEntry>();
        _eventPool = new ConcurrentBag<OpenCLEventHandle>();
        _poolSemaphore = new SemaphoreSlim(maxPoolSize, maxPoolSize);

        InitializePool();

        _logger.LogInformation(
            "OpenCL event manager initialized: maxPoolSize={MaxPoolSize}, initialPoolSize={InitialPoolSize}",
            _maxPoolSize, _initialPoolSize);
    }

    /// <summary>
    /// Acquires an event from the pool or creates a new one.
    /// This is the primary method for obtaining events with pooling optimization.
    /// </summary>
    /// <param name="properties">Event properties specifying profiling and user event modes.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A handle to the acquired event with RAII cleanup semantics.</returns>
    public async ValueTask<EventHandle> AcquireEventAsync(
        EventProperties properties,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Only pool non-profiling, non-user events for efficiency
        if (!properties.EnableProfiling && !properties.IsUserEvent && _eventPool.TryTake(out var pooledEvent))
        {
            Interlocked.Increment(ref _poolHits);
            _logger.LogTrace("Acquired event from pool (pool hits: {PoolHits})", _poolHits);
            return CreateHandle(pooledEvent, properties);
        }

        // Pool empty or profiling needed, create new event
        Interlocked.Increment(ref _poolMisses);
        await _poolSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var evt = await CreateEventAsync(properties, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug(
                "Created new event (pool misses: {PoolMisses}, total created: {TotalCreated})",
                _poolMisses, _eventCreations);
            return CreateHandle(evt, properties);
        }
        catch
        {
            _poolSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Releases an event back to the pool for reuse.
    /// Called automatically when EventHandle is disposed.
    /// </summary>
    /// <param name="handle">The event handle to release.</param>
    internal async ValueTask ReleaseEventAsync(EventHandle handle)
    {
        if (_disposed)
        {
            return;
        }

        if (_activeEvents.TryRemove(handle.Id, out var entry))
        {
            // Wait for event completion before recycling
            await WaitForEventAsync(entry.Event).ConfigureAwait(false);

            // Only recycle non-profiling, non-user events
            if (!entry.Properties.EnableProfiling && !entry.Properties.IsUserEvent && _eventPool.Count < _maxPoolSize)
            {
                _eventPool.Add(entry.Event);
                Interlocked.Increment(ref _eventRecycles);
                _logger.LogTrace("Recycled event to pool (total recycled: {Recycled})", _eventRecycles);
            }
            else
            {
                // Destroy event (profiling events or pool full)
                await DestroyEventAsync(entry.Event).ConfigureAwait(false);
            }

            _poolSemaphore.Release();
        }
    }

    /// <summary>
    /// Waits for a single event to complete.
    /// </summary>
    /// <param name="evt">The event to wait for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask WaitForEventAsync(
        OpenCLEventHandle evt,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (evt.Handle == nint.Zero)
        {
            return;
        }

        await Task.Run(() =>
        {
            var eventArray = new[] { evt.Handle };
            var error = OpenCLRuntime.clWaitForEvents(1, eventArray);
            OpenCLException.ThrowIfError(error, "Wait for event");
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits for multiple events to complete.
    /// Efficiently batches waiting using clWaitForEvents for optimal performance.
    /// </summary>
    /// <param name="events">Collection of events to wait for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask WaitForEventsAsync(
        IEnumerable<OpenCLEventHandle> events,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var eventArray = events.Where(e => e.Handle != nint.Zero).Select(e => e.Handle).ToArray();
        if (eventArray.Length == 0)
        {
            return;
        }

        await Task.Run(() =>
        {
            var error = OpenCLRuntime.clWaitForEvents((uint)eventArray.Length, eventArray);
            OpenCLException.ThrowIfError(error, "Wait for events");
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogTrace("Waited for {Count} events", eventArray.Length);
    }

    /// <summary>
    /// Gets profiling information for an event (if profiling was enabled).
    /// Returns null if profiling is not available or event has not completed.
    /// </summary>
    /// <param name="evt">The event to query profiling information for.</param>
    /// <returns>Profiling information or null if not available.</returns>
    public async ValueTask<EventProfilingInfo?> GetProfilingInfoAsync(OpenCLEventHandle evt)
    {
        ThrowIfDisposed();

        if (evt.Handle == nint.Zero)
        {
            return null;
        }

        try
        {
            // Query all profiling timestamps
            var queued = await QueryProfilingInfoAsync(evt, ProfilingInfo.CommandQueued).ConfigureAwait(false);
            var submitted = await QueryProfilingInfoAsync(evt, ProfilingInfo.CommandSubmit).ConfigureAwait(false);
            var start = await QueryProfilingInfoAsync(evt, ProfilingInfo.CommandStart).ConfigureAwait(false);
            var end = await QueryProfilingInfoAsync(evt, ProfilingInfo.CommandEnd).ConfigureAwait(false);

            Interlocked.Increment(ref _profilingQueriesTotal);

            return new EventProfilingInfo
            {
                QueuedTime = queued,
                SubmittedTime = submitted,
                StartTime = start,
                EndTime = end
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query profiling info for event {Event}", evt);
            return null;
        }
    }

    /// <summary>
    /// Creates a user event for host-side synchronization.
    /// User events allow the host to signal completion of operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A handle to the created user event.</returns>
    public async ValueTask<EventHandle> CreateUserEventAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var properties = new EventProperties
        {
            IsUserEvent = true,
            EnableProfiling = false,
            Name = "UserEvent"
        };

        return await AcquireEventAsync(properties, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the execution status of a user event.
    /// Use this to signal completion or failure of host-side operations.
    /// </summary>
    /// <param name="evt">The user event to update.</param>
    /// <param name="status">Execution status (0 for complete, negative for error).</param>
    public void SetUserEventStatus(OpenCLEventHandle evt, int status)
    {
        ThrowIfDisposed();

        if (evt.Handle == nint.Zero)
        {
            throw new ArgumentException("Invalid event handle", nameof(evt));
        }

        var error = OpenCLRuntime.clSetUserEventStatus(evt.Handle, status);
        OpenCLException.ThrowIfError(error, $"Set user event status to {status}");

        _logger.LogTrace("Set user event {Event} status to {Status}", evt, status);
    }

    /// <summary>
    /// Gets comprehensive statistics about event usage and pool performance.
    /// </summary>
    /// <returns>Statistics snapshot for monitoring and optimization.</returns>
    public EventStatistics GetStatistics()
    {
        ThrowIfDisposed();

        return new EventStatistics
        {
            TotalEventsCreated = Interlocked.Read(ref _eventCreations),
            EventRecycles = Interlocked.Read(ref _eventRecycles),
            PoolHits = Interlocked.Read(ref _poolHits),
            PoolMisses = Interlocked.Read(ref _poolMisses),
            ActiveEvents = _activeEvents.Count,
            PooledEvents = _eventPool.Count,
            ProfilingQueries = Interlocked.Read(ref _profilingQueriesTotal)
        };
    }

    /// <summary>
    /// Creates a new OpenCL event with specified properties.
    /// </summary>
    private async ValueTask<OpenCLEventHandle> CreateEventAsync(
        EventProperties properties,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            OpenCLEventHandle evt;

            if (properties.IsUserEvent)
            {
                // Create user event
                evt = new OpenCLEventHandle(
                    OpenCLRuntime.clCreateUserEvent(_context.Context.Handle, out var error));
                OpenCLException.ThrowIfError(error, "Create user event");
            }
            else
            {
                // Regular events are created by enqueue operations, not directly
                // For pooling purposes, we use dummy event creation
                // In production, events come from actual operations
                throw new InvalidOperationException(
                    "Regular events should be obtained from enqueue operations, not created directly");
            }

            Interlocked.Increment(ref _eventCreations);

            _logger.LogDebug(
                "Created event: isUser={IsUser}, profiling={Profiling}, name={Name}",
                properties.IsUserEvent, properties.EnableProfiling, properties.Name ?? "unnamed");

            return evt;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Destroys an OpenCL event and releases its resources.
    /// </summary>
    private async ValueTask DestroyEventAsync(OpenCLEventHandle evt)
    {
        if (evt.Handle == nint.Zero)
        {
            return;
        }

        await Task.Run(() =>
        {
            var error = OpenCLRuntime.clReleaseEvent(evt.Handle);
            if (error != OpenCLError.Success)
            {
                _logger.LogWarning(
                    "Failed to release event {Event}: {Error}",
                    evt, error);
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Queries profiling information for a specific parameter.
    /// </summary>
    private static async ValueTask<long> QueryProfilingInfoAsync(OpenCLEventHandle evt, ProfilingInfo info)
    {
        return await Task.Run(() =>
        {
            long value = 0;
            unsafe
            {
                var error = OpenCLRuntime.clGetEventProfilingInfo(
                    evt.Handle,
                    (uint)info,
                    (nuint)sizeof(long),
                    (nint)(&value),
                    out _);

                OpenCLException.ThrowIfError(error, $"Query profiling info {info}");
            }
            return value;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an event handle with tracking in active events dictionary.
    /// </summary>
    private EventHandle CreateHandle(OpenCLEventHandle evt, EventProperties properties)
    {
        var id = Guid.NewGuid();
        var entry = new EventEntry(evt, properties);
        _activeEvents[id] = entry;

        return new EventHandle(this, evt, id);
    }

    /// <summary>
    /// Initializes the event pool with pre-created events for immediate use.
    /// Note: OpenCL events are typically created by operations, not pre-allocated.
    /// Pool is populated dynamically as events are recycled.
    /// </summary>
    private void InitializePool()
    {
        _logger.LogDebug("Event pool initialized (dynamic allocation on demand)");
    }

    /// <summary>
    /// Throws if this manager has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Asynchronously disposes the event manager and all managed events.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _logger.LogInformation("Disposing OpenCL event manager");

        try
        {
            // Wait for and release all active events
            var activeTasks = _activeEvents.Values
                .Select(async entry =>
                {
                    await WaitForEventAsync(entry.Event).ConfigureAwait(false);
                    await DestroyEventAsync(entry.Event).ConfigureAwait(false);
                })
                .ToArray();

            await Task.WhenAll(activeTasks).ConfigureAwait(false);

            // Release all pooled events
            while (_eventPool.TryTake(out var evt))
            {
                await DestroyEventAsync(evt).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during event manager disposal");
        }
        finally
        {
            _poolSemaphore?.Dispose();
            _activeEvents.Clear();

            var stats = GetFinalStatistics();

            _logger.LogInformation(
                "Event manager disposed. Final stats: created={Created}, recycled={Recycled}, " +
                "hits={Hits}, misses={Misses}, hit rate={HitRate:P2}, profiling queries={ProfilingQueries}",
                stats.TotalEventsCreated, stats.EventRecycles,
                stats.PoolHits, stats.PoolMisses, stats.PoolHitRate, stats.ProfilingQueries);
        }
    }

    /// <summary>
    /// Gets final statistics for disposal logging.
    /// </summary>
    private EventStatistics GetFinalStatistics()
    {
        return new EventStatistics
        {
            TotalEventsCreated = Interlocked.Read(ref _eventCreations),
            EventRecycles = Interlocked.Read(ref _eventRecycles),
            PoolHits = Interlocked.Read(ref _poolHits),
            PoolMisses = Interlocked.Read(ref _poolMisses),
            ActiveEvents = 0,
            PooledEvents = 0,
            ProfilingQueries = Interlocked.Read(ref _profilingQueriesTotal)
        };
    }

    /// <summary>
    /// Internal entry tracking event state and properties.
    /// </summary>
    private sealed class EventEntry
    {
        public OpenCLEventHandle Event { get; }
        public EventProperties Properties { get; }
        public DateTimeOffset CreatedAt { get; }

        public EventEntry(OpenCLEventHandle evt, EventProperties properties)
        {
            Event = evt;
            Properties = properties;
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// OpenCL profiling information parameter names.
    /// </summary>
    private enum ProfilingInfo : uint
    {
        CommandQueued = 0x1280,
        CommandSubmit = 0x1281,
        CommandStart = 0x1282,
        CommandEnd = 0x1283
    }
}

/// <summary>
/// Properties for event creation and behavior.
/// </summary>
public sealed class EventProperties
{
    /// <summary>
    /// Gets or initializes whether profiling is enabled for this event (default: false).
    /// Profiling allows timing measurements but prevents pooling.
    /// </summary>
    public bool EnableProfiling { get; init; }

    /// <summary>
    /// Gets or initializes whether this is a user event for host-side synchronization (default: false).
    /// User events are signaled explicitly by the host application.
    /// </summary>
    public bool IsUserEvent { get; init; }

    /// <summary>
    /// Gets or initializes an optional descriptive name for debugging purposes.
    /// </summary>
    public string? Name { get; init; }
}

/// <summary>
/// RAII handle for managed events with automatic cleanup.
/// Ensures events are properly returned to the pool or destroyed when no longer needed.
/// </summary>
public readonly struct EventHandle : IAsyncDisposable, IEquatable<EventHandle>
{
    private readonly OpenCLEventManager? _manager;
    private readonly OpenCLEventHandle _event;
    private readonly Guid _id;

    internal EventHandle(OpenCLEventManager manager, OpenCLEventHandle evt, Guid id)
    {
        _manager = manager;
        _event = evt;
        _id = id;
    }

    /// <summary>
    /// Gets the underlying OpenCL event handle.
    /// </summary>
    public OpenCLEventHandle Event => _event;

    /// <summary>
    /// Gets the unique identifier for this event handle.
    /// </summary>
    internal Guid Id => _id;

    /// <summary>
    /// Waits for the event to complete.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_manager != null)
        {
            await _manager.WaitForEventAsync(_event, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets profiling information for this event (if profiling was enabled).
    /// Returns null if profiling is not available.
    /// </summary>
    public async ValueTask<EventProfilingInfo?> GetProfilingInfoAsync()
    {
        if (_manager != null)
        {
            return await _manager.GetProfilingInfoAsync(_event).ConfigureAwait(false);
        }
        return null;
    }

    /// <summary>
    /// Asynchronously releases the event back to the manager's pool.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_manager != null)
        {
            await _manager.ReleaseEventAsync(this).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Determines whether the specified handle is equal to the current handle.
    /// </summary>
    public bool Equals(EventHandle other) => _id.Equals(other._id);

    /// <summary>
    /// Determines whether the specified object is equal to the current handle.
    /// </summary>
    public override bool Equals(object? obj) => obj is EventHandle other && Equals(other);

    /// <summary>
    /// Returns the hash code for this handle.
    /// </summary>
    public override int GetHashCode() => _id.GetHashCode();

    /// <summary>
    /// Determines whether two handles are equal.
    /// </summary>
    public static bool operator ==(EventHandle left, EventHandle right) => left.Equals(right);

    /// <summary>
    /// Determines whether two handles are not equal.
    /// </summary>
    public static bool operator !=(EventHandle left, EventHandle right) => !left.Equals(right);
}

/// <summary>
/// Profiling information for an OpenCL event.
/// Contains timing data for queue, submission, start, and completion timestamps.
/// </summary>
public sealed record EventProfilingInfo
{
    /// <summary>Gets the timestamp when the command was queued (nanoseconds).</summary>
    public long QueuedTime { get; init; }

    /// <summary>Gets the timestamp when the command was submitted (nanoseconds).</summary>
    public long SubmittedTime { get; init; }

    /// <summary>Gets the timestamp when execution started (nanoseconds).</summary>
    public long StartTime { get; init; }

    /// <summary>Gets the timestamp when execution completed (nanoseconds).</summary>
    public long EndTime { get; init; }

    /// <summary>
    /// Gets the execution time in nanoseconds (end - start).
    /// </summary>
    public long ExecutionTimeNanoseconds => EndTime - StartTime;

    /// <summary>
    /// Gets the queue-to-start latency in nanoseconds (start - queued).
    /// Measures the time spent waiting in the queue before execution.
    /// </summary>
    public long QueueToStartNanoseconds => StartTime - QueuedTime;

    /// <summary>
    /// Gets the execution time in milliseconds for easier interpretation.
    /// </summary>
    public double ExecutionTimeMilliseconds => ExecutionTimeNanoseconds / 1_000_000.0;

    /// <summary>
    /// Gets the queue-to-start latency in milliseconds.
    /// </summary>
    public double QueueToStartMilliseconds => QueueToStartNanoseconds / 1_000_000.0;
}

/// <summary>
/// Statistics for event usage and pool performance.
/// Used for monitoring, optimization, and capacity planning.
/// </summary>
public sealed record EventStatistics
{
    /// <summary>Gets the total number of events created since initialization.</summary>
    public long TotalEventsCreated { get; init; }

    /// <summary>Gets the total number of events recycled back to the pool.</summary>
    public long EventRecycles { get; init; }

    /// <summary>Gets the number of successful pool acquisitions (cache hits).</summary>
    public long PoolHits { get; init; }

    /// <summary>Gets the number of pool misses requiring new event creation.</summary>
    public long PoolMisses { get; init; }

    /// <summary>Gets the current number of active (in-use) events.</summary>
    public int ActiveEvents { get; init; }

    /// <summary>Gets the current number of events available in the pool.</summary>
    public int PooledEvents { get; init; }

    /// <summary>Gets the total number of profiling information queries performed.</summary>
    public long ProfilingQueries { get; init; }

    /// <summary>
    /// Gets the pool hit rate as a percentage (0.0 to 1.0).
    /// Higher values indicate better pooling efficiency.
    /// Target: >0.8 (80% hit rate) for optimal performance.
    /// </summary>
    public double PoolHitRate => PoolHits + PoolMisses > 0
        ? (double)PoolHits / (PoolHits + PoolMisses)
        : 0.0;
}
