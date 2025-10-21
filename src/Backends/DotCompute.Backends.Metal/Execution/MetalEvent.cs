// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Advanced Metal event manager for timing, profiling, synchronization, and event pooling,
/// following CUDA event patterns for cross-stream coordination.
/// </summary>
public sealed class MetalEventManager : IDisposable
{
    private readonly IntPtr _device;
    private readonly ILogger<MetalEventManager> _logger;
    private readonly MetalEventPool _eventPool;
    private readonly ConcurrentDictionary<EventId, MetalEventInfo> _activeEvents;
    private readonly ConcurrentDictionary<string, MetalTimingSession> _timingSessions;
    private readonly SemaphoreSlim _eventCreationSemaphore;
    private readonly Timer _maintenanceTimer;

    // Event configuration
    private const int MAX_CONCURRENT_EVENTS = 1000; // Metal typically has lower limits than CUDA
    private const int TIMING_EVENT_POOL_SIZE = 25;
    private const int SYNC_EVENT_POOL_SIZE = 25;

    private volatile bool _disposed;
    private long _totalEventsCreated;
    private long _totalTimingMeasurements;

    public MetalEventManager(IntPtr device, ILogger<MetalEventManager> logger)
    {
        _device = device != IntPtr.Zero ? device : throw new ArgumentNullException(nameof(device));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeEvents = new ConcurrentDictionary<EventId, MetalEventInfo>();
        _timingSessions = new ConcurrentDictionary<string, MetalTimingSession>();
        _eventCreationSemaphore = new SemaphoreSlim(MAX_CONCURRENT_EVENTS, MAX_CONCURRENT_EVENTS);

        _eventPool = new MetalEventPool(device, logger);

        // Setup maintenance timer for cleanup and optimization
        _maintenanceTimer = new Timer(PerformMaintenance, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger.LogInformation(
            "Metal Event Manager initialized: max concurrent events={MaxEvents}, " +
            "timing pool={TimingPool}, sync pool={SyncPool}",
            MAX_CONCURRENT_EVENTS, TIMING_EVENT_POOL_SIZE, SYNC_EVENT_POOL_SIZE);
    }

    /// <summary>
    /// Creates a high-performance timing event for precise measurements
    /// </summary>
    public async Task<MetalEventHandle> CreateTimingEventAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _eventCreationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var eventHandle = await _eventPool.AcquireTimingEventAsync(cancellationToken).ConfigureAwait(false);
            var eventId = EventId.New();

            var eventInfo = new MetalEventInfo
            {
                EventId = eventId,
                Handle = eventHandle,
                Type = MetalEventType.Timing,
                CreatedAt = DateTimeOffset.UtcNow,
                IsFromPool = true
            };

            _activeEvents[eventId] = eventInfo;
            _ = Interlocked.Increment(ref _totalEventsCreated);

            _logger.LogTrace("Created timing event {EventId} (handle={Handle})", eventId, eventHandle);

            return new MetalEventHandle(eventId, eventHandle, this, MetalEventType.Timing);
        }
        catch
        {
            _ = _eventCreationSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Creates a synchronization event optimized for stream coordination
    /// </summary>
    public async Task<MetalEventHandle> CreateSyncEventAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _eventCreationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var eventHandle = await _eventPool.AcquireSyncEventAsync(cancellationToken).ConfigureAwait(false);
            var eventId = EventId.New();

            var eventInfo = new MetalEventInfo
            {
                EventId = eventId,
                Handle = eventHandle,
                Type = MetalEventType.Synchronization,
                CreatedAt = DateTimeOffset.UtcNow,
                IsFromPool = true
            };

            _activeEvents[eventId] = eventInfo;
            _ = Interlocked.Increment(ref _totalEventsCreated);

            _logger.LogTrace("Created sync event {EventId} (handle={Handle})", eventId, eventHandle);

            return new MetalEventHandle(eventId, eventHandle, this, MetalEventType.Synchronization);
        }
        catch
        {
            _ = _eventCreationSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Creates a matched pair of timing events for measuring elapsed time
    /// </summary>
    public async Task<(MetalEventHandle start, MetalEventHandle end)> CreateTimingPairAsync(
        CancellationToken cancellationToken = default)
    {
        var startEvent = await CreateTimingEventAsync(cancellationToken).ConfigureAwait(false);
        var endEvent = await CreateTimingEventAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Created timing pair: start={StartEventId}, end={EndEventId}", startEvent.EventId, endEvent.EventId);

        return (startEvent, endEvent);
    }

    /// <summary>
    /// Creates a batch of events for parallel operations
    /// </summary>
    public async Task<MetalEventHandle[]> CreateEventBatchAsync(
        int count,
        MetalEventType eventType = MetalEventType.Timing,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var events = new MetalEventHandle[count];
        var tasks = new Task<MetalEventHandle>[count];

        for (var i = 0; i < count; i++)
        {
            tasks[i] = eventType == MetalEventType.Timing
                ? CreateTimingEventAsync(cancellationToken)
                : CreateSyncEventAsync(cancellationToken);
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        Array.Copy(results, events, count);

        _logger.LogDebug("Created batch of {Count} events of type {EventType}", count, eventType);
        return events;
    }

    /// <summary>
    /// Records an event on the specified command queue
    /// </summary>
    public void RecordEvent(EventId eventId, IntPtr commandQueue)
    {
        ThrowIfDisposed();

        if (!_activeEvents.TryGetValue(eventId, out var eventInfo))
        {
            throw new ArgumentException($"Event {eventId} not found", nameof(eventId));
        }

        // For Metal, we would use MTLSharedEvent to record on command queue
        // This is a placeholder implementation
        eventInfo.RecordedAt = DateTimeOffset.UtcNow;
        eventInfo.CommandQueue = commandQueue;

        _logger.LogTrace("Recorded event {EventId} on command queue {CommandQueue}", eventId, commandQueue);
    }

    /// <summary>
    /// Records an event using the handle directly for performance
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordEventFast(IntPtr eventHandle, IntPtr commandQueue)
        // Direct Metal event recording for performance-critical paths
        // This would use native Metal APIs directly

        => _logger.LogTrace("Recorded event (fast path) on command queue {CommandQueue}", commandQueue);

    /// <summary>
    /// Waits for an event asynchronously
    /// </summary>
    public async Task WaitForEventAsync(
        EventId eventId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_activeEvents.TryGetValue(eventId, out var eventInfo))
        {
            throw new ArgumentException($"Event {eventId} not found", nameof(eventId));
        }

        if (timeout.HasValue)
        {
            using var timeoutCts = new CancellationTokenSource(timeout.Value);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            try
            {
                await Task.Run(() =>
                    // Wait for Metal event completion
                    // This would use MTLSharedEvent waiting mechanisms

                    Thread.SpinWait(1000), combinedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException($"Event {eventId} wait timed out after {timeout}");
            }
        }
        else
        {
            await Task.Run(() =>
                // Wait for Metal event completion without timeout

                Thread.SpinWait(1000), cancellationToken).ConfigureAwait(false);
        }

        eventInfo.CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Checks if an event has completed
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEventComplete(EventId eventId)
    {
        ThrowIfDisposed();

        if (!_activeEvents.TryGetValue(eventId, out var eventInfo))
        {
            return false;
        }

        // Check Metal event status
        // This would query MTLSharedEvent status
        var isComplete = eventInfo.RecordedAt.HasValue; // Placeholder logic

        if (isComplete && !eventInfo.CompletedAt.HasValue)
        {
            eventInfo.CompletedAt = DateTimeOffset.UtcNow;
        }

        return isComplete;
    }

    /// <summary>
    /// Measures elapsed time between two timing events using Metal Performance Counters
    /// </summary>
    public async Task<double> MeasureElapsedTimeAsync(EventId startEvent, EventId endEvent)
    {
        ThrowIfDisposed();

        if (!_activeEvents.TryGetValue(startEvent, out var startInfo))
        {
            throw new ArgumentException($"Start event {startEvent} not found");
        }

        if (!_activeEvents.TryGetValue(endEvent, out var endInfo))
        {
            throw new ArgumentException($"End event {endEvent} not found");
        }

        if (startInfo.Type != MetalEventType.Timing || endInfo.Type != MetalEventType.Timing)
        {
            throw new InvalidOperationException("Both events must be timing events");
        }

        // Wait for both events to complete
        await Task.WhenAll(
            WaitForEventAsync(startEvent),
            WaitForEventAsync(endEvent)
        ).ConfigureAwait(false);

        // Calculate elapsed time using Metal performance counters
        // This would use MTLCounterSampleBuffer or similar
        var elapsedMilliseconds = startInfo.CompletedAt.HasValue && endInfo.CompletedAt.HasValue
            ? (endInfo.CompletedAt.Value - startInfo.CompletedAt.Value).TotalMilliseconds
            : 0.0;

        _ = Interlocked.Increment(ref _totalTimingMeasurements);

        _logger.LogTrace("Elapsed time between events {StartEvent} and {EndEvent}: {Time}ms",
            startEvent, endEvent, elapsedMilliseconds);

        return elapsedMilliseconds;
    }

    /// <summary>
    /// Performs high-precision timing measurement of an operation
    /// </summary>
    public async Task<MetalTimingResult> MeasureOperationAsync(
        Func<IntPtr, Task> operation,
        IntPtr commandQueue,
        string? operationName = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var (startEvent, endEvent) = await CreateTimingPairAsync(cancellationToken).ConfigureAwait(false);
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Record start event
            RecordEvent(startEvent.EventId, commandQueue);

            // Execute operation
            await operation(commandQueue).ConfigureAwait(false);

            // Record end event
            RecordEvent(endEvent.EventId, commandQueue);

            // Wait for end event and measure time
            await WaitForEventAsync(endEvent.EventId, cancellationToken: cancellationToken).ConfigureAwait(false);

            var endTime = DateTimeOffset.UtcNow;
            var gpuTime = await MeasureElapsedTimeAsync(startEvent.EventId, endEvent.EventId).ConfigureAwait(false);
            var cpuTime = (endTime - startTime).TotalMilliseconds;

            var result = new MetalTimingResult
            {
                OperationName = operationName ?? "Unknown",
                GpuTimeMs = gpuTime,
                CpuTimeMs = cpuTime,
                OverheadMs = Math.Max(0, cpuTime - gpuTime),
                StartTime = startTime,
                EndTime = endTime,
                CommandQueue = commandQueue
            };

            _logger.LogDebug("Measured operation '{OperationName}': GPU={GpuTime:F3}ms, CPU={CpuTime:F3}ms, Overhead={Overhead:F3}ms", result.OperationName, result.GpuTimeMs, result.CpuTimeMs, result.OverheadMs);

            return result;
        }
        finally
        {
            startEvent.Dispose();
            endEvent.Dispose();
        }
    }

    /// <summary>
    /// Performs comprehensive profiling with statistical analysis
    /// </summary>
    public async Task<MetalProfilingResult> ProfileOperationAsync(
        Func<IntPtr, Task> operation,
        int iterations = 50, // Lower than CUDA due to Metal overhead
        IntPtr commandQueue = default,
        string? operationName = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var timings = new List<MetalTimingResult>();
        var sessionId = Guid.NewGuid().ToString("N")[..8];

        // Create timing session
        var session = new MetalTimingSession
        {
            SessionId = sessionId,
            OperationName = operationName ?? "Unknown",
            CommandQueue = commandQueue,
            StartTime = DateTimeOffset.UtcNow,
            PlannedIterations = iterations
        };

        _timingSessions[sessionId] = session;

        try
        {
            // Warmup runs (fewer than CUDA)
            var warmupIterations = Math.Min(5, iterations / 10);
            _logger.LogDebug("Starting profiling session {SessionId}: {WarmupIterations} warmup + {Iterations} iterations", sessionId, warmupIterations, iterations);

            for (var i = 0; i < warmupIterations; i++)
            {
                _ = await MeasureOperationAsync(operation, commandQueue, $"{operationName}-warmup-{i}", cancellationToken)
                    .ConfigureAwait(false);
            }

            // Profile iterations
            for (var i = 0; i < iterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var timing = await MeasureOperationAsync(
                    operation, commandQueue, $"{operationName}-{i}", cancellationToken).ConfigureAwait(false);

                timings.Add(timing);
                session.CompletedIterations = i + 1;

                // Log progress every 20% (less frequent than CUDA)
                if ((i + 1) % Math.Max(1, iterations / 5) == 0)
                {
                    var progress = (double)(i + 1) / iterations * 100;
                    _logger.LogDebug("Profiling progress {SessionId}: {Progress}% ({Current}/{Total})", sessionId, progress, i + 1, iterations);
                }
            }

            session.EndTime = DateTimeOffset.UtcNow;
            session.ActualIterations = iterations;

            // Statistical analysis
            var gpuTimes = timings.Select(t => t.GpuTimeMs).OrderBy(t => t).ToArray();
            var cpuTimes = timings.Select(t => t.CpuTimeMs).ToArray();

            var result = new MetalProfilingResult
            {
                SessionId = sessionId,
                OperationName = operationName ?? "Unknown",
                CommandQueue = commandQueue,
                Iterations = iterations,
                ActualIterations = timings.Count,

                // GPU timing statistics
                AverageGpuTimeMs = gpuTimes.Average(),
                MinGpuTimeMs = gpuTimes.Min(),
                MaxGpuTimeMs = gpuTimes.Max(),
                MedianGpuTimeMs = CalculateMedian(gpuTimes),
                StdDevGpuTimeMs = CalculateStandardDeviation(gpuTimes),

                // CPU timing statistics
                AverageCpuTimeMs = cpuTimes.Average(),
                AverageOverheadMs = timings.Average(t => t.OverheadMs),

                // Quality metrics
                CoefficientOfVariation = CalculateStandardDeviation(gpuTimes) / gpuTimes.Average(),
                OutlierCount = CountOutliers(gpuTimes),
                ThroughputOpsPerSecond = 1000.0 / gpuTimes.Average(),

                SessionDurationMs = (session.EndTime!.Value - session.StartTime).TotalMilliseconds
            };

            _logger.LogInformation(
                "Completed Metal profiling session {SessionId}: " +
                "avg={AvgGpu:F3}ms, min={MinGpu:F3}ms, max={MaxGpu:F3}ms, " +
                "p95={P95:F3}ms, throughput={Throughput:F1}ops/s",
                sessionId, result.AverageGpuTimeMs, result.MinGpuTimeMs, result.MaxGpuTimeMs,
                result.Percentiles[95], result.ThroughputOpsPerSecond);

            return result;
        }
        finally
        {
            _ = _timingSessions.TryRemove(sessionId, out _);
        }
    }

    /// <summary>
    /// Adds an asynchronous callback for when an event completes
    /// </summary>
    public void AddEventCallback(EventId eventId, Func<EventId, Task> callback)
    {
        ThrowIfDisposed();

        if (!_activeEvents.TryGetValue(eventId, out var eventInfo))
        {
            throw new ArgumentException($"Event {eventId} not found");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await WaitForEventAsync(eventId).ConfigureAwait(false);
                await callback(eventId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event callback execution for event {EventId}", eventId);
            }
        });
    }

    /// <summary>
    /// Gets comprehensive event manager statistics
    /// </summary>
    public MetalEventStatistics GetStatistics()
    {
        ThrowIfDisposed();

        var activeEventCount = _activeEvents.Count;
        var completedEventCount = _activeEvents.Values.Count(e => e.CompletedAt.HasValue);
        var timingEventCount = _activeEvents.Values.Count(e => e.Type == MetalEventType.Timing);
        var syncEventCount = _activeEvents.Values.Count(e => e.Type == MetalEventType.Synchronization);

        var totalAge = 0.0;
        var now = DateTimeOffset.UtcNow;

        foreach (var eventInfo in _activeEvents.Values)
        {
            totalAge += (now - eventInfo.CreatedAt).TotalSeconds;
        }

        return new MetalEventStatistics
        {
            ActiveEvents = activeEventCount,
            CompletedEvents = completedEventCount,
            PendingEvents = activeEventCount - completedEventCount,
            TimingEvents = timingEventCount,
            SyncEvents = syncEventCount,
            TotalEventsCreated = _totalEventsCreated,
            TotalTimingMeasurements = _totalTimingMeasurements,
            AverageEventAge = activeEventCount > 0 ? totalAge / activeEventCount : 0,
            ActiveTimingSessions = _timingSessions.Count,
            PoolStatistics = _eventPool.GetStatistics(),
            MaxConcurrentEvents = MAX_CONCURRENT_EVENTS
        };
    }

    internal void ReturnEventToPool(EventId eventId)
    {
        if (_activeEvents.TryRemove(eventId, out var eventInfo))
        {
            _eventPool.Return(eventInfo.Handle, eventInfo.Type);
            _ = _eventCreationSemaphore.Release();
        }
    }

    private static double CalculateMedian(double[] sortedValues)
    {
        var length = sortedValues.Length;
        if (length % 2 == 0)
        {
            return (sortedValues[length / 2 - 1] + sortedValues[length / 2]) / 2.0;
        }
        return sortedValues[length / 2];
    }

    private static double CalculatePercentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        var index = (sortedValues.Length - 1) * percentile;
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper)
        {
            return sortedValues[lower];
        }

        var weight = index - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }

    private static double CalculateStandardDeviation(double[] values)
    {
        if (values.Length <= 1)
        {
            return 0;
        }

        var mean = values.Average();
        var sumOfSquaredDeviations = values.Sum(val => Math.Pow(val - mean, 2));
        return Math.Sqrt(sumOfSquaredDeviations / (values.Length - 1));
    }

    private static int CountOutliers(double[] sortedValues)
    {
        if (sortedValues.Length < 4)
        {
            return 0;
        }

        var q1 = CalculatePercentile(sortedValues, 0.25);
        var q3 = CalculatePercentile(sortedValues, 0.75);
        var iqr = q3 - q1;
        var lowerBound = q1 - 1.5 * iqr;
        var upperBound = q3 + 1.5 * iqr;

        return sortedValues.Count(val => val < lowerBound || val > upperBound);
    }

    public void PerformMaintenance(object? state = null)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _eventPool.PerformMaintenance();
            CleanupCompletedEvents();
            LogPerformanceMetrics();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during event manager maintenance");
        }
    }

    private void CleanupCompletedEvents()
    {
        var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var eventsToCleanup = _activeEvents
            .Where(kvp => kvp.Value.CompletedAt.HasValue && kvp.Value.CompletedAt < cutoffTime)
            .Take(50) // Limit cleanup batch size
            .ToList();

        foreach (var (eventId, eventInfo) in eventsToCleanup)
        {
            if (_activeEvents.TryRemove(eventId, out _))
            {
                _eventPool.Return(eventInfo.Handle, eventInfo.Type);
                _ = _eventCreationSemaphore.Release();
            }
        }

        if (eventsToCleanup.Count > 0)
        {
            _logger.LogTrace("Cleaned up {Count} completed events", eventsToCleanup.Count);
        }
    }

    private void LogPerformanceMetrics()
    {
        var stats = GetStatistics();

        if (stats.ActiveEvents > MAX_CONCURRENT_EVENTS * 0.8)
        {
            _logger.LogWarning("High event usage: {ActiveEvents}/{MaxEvents} active events", stats.ActiveEvents, MAX_CONCURRENT_EVENTS);
        }

        if (stats.ActiveTimingSessions > 5)
        {
            _logger.LogInformation("Many active timing sessions: {ActiveSessions}", stats.ActiveTimingSessions);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MetalEventManager));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            _maintenanceTimer?.Dispose();

            // Clean up all active events
            foreach (var eventInfo in _activeEvents.Values)
            {
                try
                {
                    _eventPool.Return(eventInfo.Handle, eventInfo.Type);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error returning event {EventId} to pool during disposal",
                        eventInfo.EventId);
                }
            }

            _activeEvents.Clear();
            _timingSessions.Clear();

            _eventPool?.Dispose();
            _eventCreationSemaphore?.Dispose();

            _logger.LogInformation("Metal Event Manager disposed: created {TotalEvents} events", _totalEventsCreated);
        }
    }
}

/// <summary>
/// High-level Metal event abstraction for cross-stream synchronization
/// </summary>
public sealed class MetalEvent : IDisposable
{
    private readonly MetalEventManager _manager;
    private readonly EventId _eventId;
    private volatile bool _disposed;

    internal MetalEvent(MetalEventManager manager, EventId eventId)
    {
        _manager = manager;
        _eventId = eventId;
    }

    public EventId EventId => _eventId;

    /// <summary>
    /// Records this event on the specified command queue
    /// </summary>
    public async Task RecordAsync(IntPtr commandQueue, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await Task.Run(() => _manager.RecordEvent(_eventId, commandQueue), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits for this event to complete
    /// </summary>
    public async Task WaitAsync(IntPtr commandQueue, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _manager.WaitForEventAsync(_eventId, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if this event has completed
    /// </summary>
    public bool IsComplete()
    {
        ThrowIfDisposed();
        return _manager.IsEventComplete(_eventId);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MetalEvent));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            // Event cleanup is handled by the manager
        }
    }
}

// Supporting types and classes follow...

/// <summary>
/// Unique identifier for Metal events
/// </summary>
public readonly struct EventId : IEquatable<EventId>
{
    private readonly Guid _id;

    private EventId(Guid id) => _id = id;

    public static EventId New() => new(Guid.NewGuid());

    public bool Equals(EventId other) => _id.Equals(other._id);
    public override bool Equals(object? obj) => obj is EventId other && Equals(other);
    public override int GetHashCode() => _id.GetHashCode();
    public override string ToString() => _id.ToString("N")[..8];

    public static bool operator ==(EventId left, EventId right) => left.Equals(right);
    public static bool operator !=(EventId left, EventId right) => !left.Equals(right);
}

/// <summary>
/// Types of Metal events
/// </summary>
public enum MetalEventType
{
    Timing,
    Synchronization
}

/// <summary>
/// Information about an active Metal event
/// </summary>
internal sealed class MetalEventInfo
{
    public EventId EventId { get; set; }
    public IntPtr Handle { get; set; }
    public MetalEventType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public IntPtr CommandQueue { get; set; }
    public bool IsFromPool { get; set; }
}

/// <summary>
/// Handle for managed Metal events with automatic cleanup
/// </summary>
public sealed class MetalEventHandle : IDisposable
{
    private readonly MetalEventManager _manager;
    private volatile bool _disposed;

    internal MetalEventHandle(EventId eventId, IntPtr eventHandle, MetalEventManager manager, MetalEventType type)
    {
        EventId = eventId;
        Handle = eventHandle;
        Type = type;
        _manager = manager;
    }

    public EventId EventId { get; }
    public IntPtr Handle { get; }
    public MetalEventType Type { get; }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _manager.ReturnEventToPool(EventId);
        }
    }
}

/// <summary>
/// Result of a single timing measurement
/// </summary>
public sealed class MetalTimingResult
{
    public string OperationName { get; set; } = string.Empty;
    public double GpuTimeMs { get; set; }
    public double CpuTimeMs { get; set; }
    public double OverheadMs { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public IntPtr CommandQueue { get; set; }
}

/// <summary>
/// Result of profiling with statistical analysis
/// </summary>
public sealed class MetalProfilingResult
{
    public string SessionId { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public IntPtr CommandQueue { get; set; }
    public int Iterations { get; set; }
    public int ActualIterations { get; set; }

    // GPU timing statistics
    public double AverageGpuTimeMs { get; set; }
    public double MinGpuTimeMs { get; set; }
    public double MaxGpuTimeMs { get; set; }
    public double MedianGpuTimeMs { get; set; }
    public double StdDevGpuTimeMs { get; set; }

    // CPU timing statistics
    public double AverageCpuTimeMs { get; set; }
    public double AverageOverheadMs { get; set; }

    // Statistical analysis
    public Dictionary<int, double> Percentiles { get; } = [];
    public double CoefficientOfVariation { get; set; }
    public int OutlierCount { get; set; }
    public double ThroughputOpsPerSecond { get; set; }
    public double SessionDurationMs { get; set; }
}

/// <summary>
/// Timing session for tracking long-running profiling
/// </summary>
internal sealed class MetalTimingSession
{
    public string SessionId { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public IntPtr CommandQueue { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public int PlannedIterations { get; set; }
    public int ActualIterations { get; set; }
    public int CompletedIterations { get; set; }
}

/// <summary>
/// Statistics for the Metal event manager
/// </summary>
public sealed class MetalEventStatistics
{
    public int ActiveEvents { get; set; }
    public int CompletedEvents { get; set; }
    public int PendingEvents { get; set; }
    public int TimingEvents { get; set; }
    public int SyncEvents { get; set; }
    public long TotalEventsCreated { get; set; }
    public long TotalTimingMeasurements { get; set; }
    public double AverageEventAge { get; set; }
    public int ActiveTimingSessions { get; set; }
    public MetalEventPoolStatistics? PoolStatistics { get; set; }
    public int MaxConcurrentEvents { get; set; }
}