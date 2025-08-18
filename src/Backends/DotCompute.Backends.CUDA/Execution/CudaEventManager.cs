// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution;

/// <summary>
/// Advanced CUDA event manager with timing, profiling, synchronization, and event pooling
/// </summary>
public sealed class CudaEventManager : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly CudaEventPool _eventPool;
    private readonly ConcurrentDictionary<EventId, CudaEventInfo> _activeEvents;
    private readonly ConcurrentDictionary<string, CudaTimingSession> _timingSessions;
    private readonly SemaphoreSlim _eventCreationSemaphore;
    private readonly Timer _maintenanceTimer;
    private readonly object _lockObject = new();

    // Event configuration
    private const int MAX_CONCURRENT_EVENTS = 2000;
    private const int INITIAL_POOL_SIZE = 100;
    private const int TIMING_EVENT_POOL_SIZE = 50;
    private const int SYNC_EVENT_POOL_SIZE = 50;

    private volatile bool _disposed;
    private long _totalEventsCreated;
    private long _totalTimingMeasurements;

    public CudaEventManager(CudaContext context, ILogger<CudaEventManager> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeEvents = new ConcurrentDictionary<EventId, CudaEventInfo>();
        _timingSessions = new ConcurrentDictionary<string, CudaTimingSession>();
        _eventCreationSemaphore = new SemaphoreSlim(MAX_CONCURRENT_EVENTS, MAX_CONCURRENT_EVENTS);

        _eventPool = new CudaEventPool(context, logger);

        // Setup maintenance timer for cleanup and optimization
        _maintenanceTimer = new Timer(PerformMaintenance, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger.LogInformation(
            "CUDA Event Manager initialized: max concurrent events={MaxEvents}, " +
            "timing pool={TimingPool}, sync pool={SyncPool}",
            MAX_CONCURRENT_EVENTS, TIMING_EVENT_POOL_SIZE, SYNC_EVENT_POOL_SIZE);
    }

    /// <summary>
    /// Creates a high-performance timing event for precise measurements
    /// </summary>
    public async Task<CudaEventHandle> CreateTimingEventAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _eventCreationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var eventHandle = await _eventPool.AcquireTimingEventAsync(cancellationToken).ConfigureAwait(false);
            var eventId = EventId.New();

            var eventInfo = new CudaEventInfo
            {
                EventId = eventId,
                Handle = eventHandle,
                Type = CudaEventType.Timing,
                CreatedAt = DateTimeOffset.UtcNow,
                IsFromPool = true
            };

            _activeEvents[eventId] = eventInfo;
            Interlocked.Increment(ref _totalEventsCreated);

            _logger.LogTrace("Created timing event {EventId} (handle={Handle})", eventId, eventHandle);

            return new CudaEventHandle(eventId, eventHandle, this, CudaEventType.Timing);
        }
        catch
        {
            _eventCreationSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Creates a synchronization event optimized for stream coordination
    /// </summary>
    public async Task<CudaEventHandle> CreateSyncEventAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _eventCreationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var eventHandle = await _eventPool.AcquireSyncEventAsync(cancellationToken).ConfigureAwait(false);
            var eventId = EventId.New();

            var eventInfo = new CudaEventInfo
            {
                EventId = eventId,
                Handle = eventHandle,
                Type = CudaEventType.Synchronization,
                CreatedAt = DateTimeOffset.UtcNow,
                IsFromPool = true
            };

            _activeEvents[eventId] = eventInfo;
            Interlocked.Increment(ref _totalEventsCreated);

            _logger.LogTrace("Created sync event {EventId} (handle={Handle})", eventId, eventHandle);

            return new CudaEventHandle(eventId, eventHandle, this, CudaEventType.Synchronization);
        }
        catch
        {
            _eventCreationSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Creates a matched pair of timing events for measuring elapsed time
    /// </summary>
    public async Task<(CudaEventHandle start, CudaEventHandle end)> CreateTimingPairAsync(
        CancellationToken cancellationToken = default)
    {
        var startEvent = await CreateTimingEventAsync(cancellationToken).ConfigureAwait(false);
        var endEvent = await CreateTimingEventAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Created timing pair: start={StartEvent}, end={EndEvent}", 
            startEvent.EventId, endEvent.EventId);

        return (startEvent, endEvent);
    }

    /// <summary>
    /// Creates a batch of events for parallel operations
    /// </summary>
    public async Task<CudaEventHandle[]> CreateEventBatchAsync(
        int count,
        CudaEventType eventType = CudaEventType.Timing,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var events = new CudaEventHandle[count];
        var tasks = new Task<CudaEventHandle>[count];

        for (int i = 0; i < count; i++)
        {
            tasks[i] = eventType == CudaEventType.Timing 
                ? CreateTimingEventAsync(cancellationToken)
                : CreateSyncEventAsync(cancellationToken);
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        Array.Copy(results, events, count);

        _logger.LogDebug("Created batch of {Count} {Type} events", count, eventType);
        return events;
    }

    /// <summary>
    /// Records an event on the specified stream
    /// </summary>
    public void RecordEvent(EventId eventId, IntPtr stream = default)
    {
        ThrowIfDisposed();

        if (!_activeEvents.TryGetValue(eventId, out var eventInfo))
            throw new ArgumentException($"Event {eventId} not found", nameof(eventId));

        _context.MakeCurrent();

        var result = Native.CudaRuntime.cudaEventRecord(eventInfo.Handle, stream);
        Native.CudaRuntime.CheckError(result, $"recording event {eventId} on stream {stream}");

        eventInfo.RecordedAt = DateTimeOffset.UtcNow;
        eventInfo.Stream = stream;

        _logger.LogTrace("Recorded event {EventId} on stream {Stream}", eventId, stream);
    }

    /// <summary>
    /// Records an event using the handle directly for performance
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordEventFast(IntPtr eventHandle, IntPtr stream = default)
    {
        _context.MakeCurrent();
        var result = Native.CudaRuntime.cudaEventRecord(eventHandle, stream);
        Native.CudaRuntime.CheckError(result, "recording event (fast path)");
    }

    /// <summary>
    /// Synchronizes with an event asynchronously
    /// </summary>
    public async Task SynchronizeEventAsync(
        EventId eventId, 
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_activeEvents.TryGetValue(eventId, out var eventInfo))
            throw new ArgumentException($"Event {eventId} not found", nameof(eventId));

        _context.MakeCurrent();

        if (timeout.HasValue)
        {
            using var timeoutCts = new CancellationTokenSource(timeout.Value);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            try
            {
                await Task.Run(() =>
                {
                    var result = Native.CudaRuntime.cudaEventSynchronize(eventInfo.Handle);
                    Native.CudaRuntime.CheckError(result, $"synchronizing event {eventId}");
                }, combinedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException($"Event {eventId} synchronization timed out after {timeout}");
            }
        }
        else
        {
            await Task.Run(() =>
            {
                var result = Native.CudaRuntime.cudaEventSynchronize(eventInfo.Handle);
                Native.CudaRuntime.CheckError(result, $"synchronizing event {eventId}");
            }, cancellationToken).ConfigureAwait(false);
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
            return false;

        _context.MakeCurrent();
        var result = Native.CudaRuntime.cudaEventQuery(eventInfo.Handle);
        
        if (result == CudaError.Success && !eventInfo.CompletedAt.HasValue)
        {
            eventInfo.CompletedAt = DateTimeOffset.UtcNow;
        }

        return result == CudaError.Success;
    }

    /// <summary>
    /// Measures elapsed time between two timing events in milliseconds
    /// </summary>
    public float MeasureElapsedTime(EventId startEvent, EventId endEvent)
    {
        ThrowIfDisposed();

        if (!_activeEvents.TryGetValue(startEvent, out var startInfo))
            throw new ArgumentException($"Start event {startEvent} not found");

        if (!_activeEvents.TryGetValue(endEvent, out var endInfo))
            throw new ArgumentException($"End event {endEvent} not found");

        if (startInfo.Type != CudaEventType.Timing || endInfo.Type != CudaEventType.Timing)
            throw new InvalidOperationException("Both events must be timing events");

        _context.MakeCurrent();

        var milliseconds = 0f;
        var result = Native.CudaRuntime.cudaEventElapsedTime(
            ref milliseconds, startInfo.Handle, endInfo.Handle);
        
        Native.CudaRuntime.CheckError(result, 
            $"measuring elapsed time between events {startEvent} and {endEvent}");

        Interlocked.Increment(ref _totalTimingMeasurements);

        _logger.LogTrace("Elapsed time between events {StartEvent} and {EndEvent}: {Time}ms",
            startEvent, endEvent, milliseconds);

        return milliseconds;
    }

    /// <summary>
    /// Performs high-precision timing measurement of an operation
    /// </summary>
    public async Task<CudaTimingResult> MeasureOperationAsync(
        Func<IntPtr, Task> operation,
        IntPtr stream = default,
        string? operationName = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var (startEvent, endEvent) = await CreateTimingPairAsync(cancellationToken).ConfigureAwait(false);
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Record start event
            RecordEvent(startEvent.EventId, stream);
            
            // Execute operation
            await operation(stream).ConfigureAwait(false);
            
            // Record end event
            RecordEvent(endEvent.EventId, stream);
            
            // Synchronize end event
            await SynchronizeEventAsync(endEvent.EventId, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            var endTime = DateTimeOffset.UtcNow;
            var gpuTime = MeasureElapsedTime(startEvent.EventId, endEvent.EventId);
            var cpuTime = (float)(endTime - startTime).TotalMilliseconds;

            var result = new CudaTimingResult
            {
                OperationName = operationName ?? "Unknown",
                GpuTimeMs = gpuTime,
                CpuTimeMs = cpuTime,
                OverheadMs = Math.Max(0, cpuTime - gpuTime),
                StartTime = startTime,
                EndTime = endTime,
                Stream = stream
            };

            _logger.LogDebug("Measured operation '{Operation}': GPU={GpuTime:F3}ms, CPU={CpuTime:F3}ms, Overhead={Overhead:F3}ms",
                result.OperationName, result.GpuTimeMs, result.CpuTimeMs, result.OverheadMs);

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
    public async Task<CudaProfilingResult> ProfileOperationAsync(
        Func<IntPtr, Task> operation,
        int iterations = 100,
        IntPtr stream = default,
        string? operationName = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var timings = new List<CudaTimingResult>();
        var sessionId = Guid.NewGuid().ToString("N")[..8];
        
        // Create timing session
        var session = new CudaTimingSession
        {
            SessionId = sessionId,
            OperationName = operationName ?? "Unknown",
            Stream = stream,
            StartTime = DateTimeOffset.UtcNow,
            PlannedIterations = iterations
        };
        
        _timingSessions[sessionId] = session;

        try
        {
            // Warmup runs
            var warmupIterations = Math.Min(10, iterations / 10);
            _logger.LogDebug("Starting profiling session {SessionId}: {Warmup} warmup + {Iterations} iterations",
                sessionId, warmupIterations, iterations);

            for (int i = 0; i < warmupIterations; i++)
            {
                await MeasureOperationAsync(operation, stream, $"{operationName}-warmup-{i}", cancellationToken)
                    .ConfigureAwait(false);
            }

            // Profile iterations
            for (int i = 0; i < iterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var timing = await MeasureOperationAsync(
                    operation, stream, $"{operationName}-{i}", cancellationToken).ConfigureAwait(false);
                
                timings.Add(timing);
                session.CompletedIterations = i + 1;

                // Log progress every 10% 
                if ((i + 1) % Math.Max(1, iterations / 10) == 0)
                {
                    var progress = (double)(i + 1) / iterations * 100;
                    _logger.LogDebug("Profiling progress {SessionId}: {Progress:F1}% ({Completed}/{Total})",
                        sessionId, progress, i + 1, iterations);
                }
            }

            session.EndTime = DateTimeOffset.UtcNow;
            session.ActualIterations = iterations;

            // Statistical analysis
            var gpuTimes = timings.Select(t => (double)t.GpuTimeMs).OrderBy(t => t).ToArray();
            var cpuTimes = timings.Select(t => (double)t.CpuTimeMs).ToArray();

            var result = new CudaProfilingResult
            {
                SessionId = sessionId,
                OperationName = operationName ?? "Unknown",
                Stream = stream,
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
                
                // Percentiles
                Percentiles = new Dictionary<int, double>
                {
                    [50] = CalculatePercentile(gpuTimes, 0.5),
                    [90] = CalculatePercentile(gpuTimes, 0.9),
                    [95] = CalculatePercentile(gpuTimes, 0.95),
                    [99] = CalculatePercentile(gpuTimes, 0.99),
                    [999] = CalculatePercentile(gpuTimes, 0.999)
                },
                
                // Quality metrics
                CoefficientOfVariation = CalculateStandardDeviation(gpuTimes) / gpuTimes.Average(),
                OutlierCount = CountOutliers(gpuTimes),
                ThroughputOpsPerSecond = 1000.0 / gpuTimes.Average(),
                
                SessionDurationMs = (session.EndTime!.Value - session.StartTime).TotalMilliseconds
            };

            _logger.LogInformation(
                "Completed profiling session {SessionId}: " +
                "avg={AvgGpu:F3}ms, min={MinGpu:F3}ms, max={MaxGpu:F3}ms, " +
                "p95={P95:F3}ms, throughput={Throughput:F1}ops/s",
                sessionId, result.AverageGpuTimeMs, result.MinGpuTimeMs, result.MaxGpuTimeMs,
                result.Percentiles[95], result.ThroughputOpsPerSecond);

            return result;
        }
        finally
        {
            _timingSessions.TryRemove(sessionId, out _);
        }
    }

    /// <summary>
    /// Adds an asynchronous callback for when an event completes
    /// </summary>
    public void AddEventCallback(EventId eventId, Func<EventId, Task> callback)
    {
        ThrowIfDisposed();

        if (!_activeEvents.TryGetValue(eventId, out var eventInfo))
            throw new ArgumentException($"Event {eventId} not found");

        _ = Task.Run(async () =>
        {
            try
            {
                await SynchronizeEventAsync(eventId).ConfigureAwait(false);
                await callback(eventId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event callback for event {EventId}", eventId);
            }
        });
    }

    /// <summary>
    /// Creates a new CUDA event with specified flags (backward compatibility)
    /// </summary>
    public IntPtr CreateEvent(CudaEventFlags flags = CudaEventFlags.Default)
    {
        var eventHandle = flags == CudaEventFlags.Default 
            ? CreateTimingEventAsync().Result
            : CreateSyncEventAsync().Result;
        return eventHandle.Handle;
    }

    /// <summary>
    /// Destroys a CUDA event (backward compatibility)
    /// </summary>
    public void DestroyEvent(IntPtr eventHandle)
    {
        // Find the event in active events and dispose it
        var eventInfo = _activeEvents.Values.FirstOrDefault(e => e.Handle == eventHandle);
        if (eventInfo != null)
        {
            _activeEvents.TryRemove(eventInfo.EventId, out _);
            _eventPool.Return(eventHandle, eventInfo.Type);
            _eventCreationSemaphore.Release();
        }
    }

    /// <summary>
    /// Calculates elapsed time between two events (backward compatibility)
    /// </summary>
    public float ElapsedTime(IntPtr startEvent, IntPtr endEvent)
    {
        var startInfo = _activeEvents.Values.FirstOrDefault(e => e.Handle == startEvent);
        var endInfo = _activeEvents.Values.FirstOrDefault(e => e.Handle == endEvent);
        
        if (startInfo != null && endInfo != null)
        {
            return MeasureElapsedTime(startInfo.EventId, endInfo.EventId);
        }
        
        throw new ArgumentException("One or both events not found");
    }

    /// <summary>
    /// Gets comprehensive event manager statistics
    /// </summary>
    public CudaEventStatistics GetStatistics()
    {
        ThrowIfDisposed();

        var activeEventCount = _activeEvents.Count;
        var completedEventCount = _activeEvents.Values.Count(e => e.CompletedAt.HasValue);
        var timingEventCount = _activeEvents.Values.Count(e => e.Type == CudaEventType.Timing);
        var syncEventCount = _activeEvents.Values.Count(e => e.Type == CudaEventType.Synchronization);

        var totalAge = 0.0;
        var now = DateTimeOffset.UtcNow;

        foreach (var eventInfo in _activeEvents.Values)
        {
            totalAge += (now - eventInfo.CreatedAt).TotalSeconds;
        }

        return new CudaEventStatistics
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
            _eventCreationSemaphore.Release();
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
        if (sortedValues.Length == 0) return 0;
        
        var index = (sortedValues.Length - 1) * percentile;
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        
        if (lower == upper) return sortedValues[lower];
        
        var weight = index - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }

    private static double CalculateStandardDeviation(double[] values)
    {
        if (values.Length <= 1) return 0;
        
        var mean = values.Average();
        var sumOfSquaredDeviations = values.Sum(val => Math.Pow(val - mean, 2));
        return Math.Sqrt(sumOfSquaredDeviations / (values.Length - 1));
    }

    private static int CountOutliers(double[] sortedValues)
    {
        if (sortedValues.Length < 4) return 0;
        
        var q1 = CalculatePercentile(sortedValues, 0.25);
        var q3 = CalculatePercentile(sortedValues, 0.75);
        var iqr = q3 - q1;
        var lowerBound = q1 - 1.5 * iqr;
        var upperBound = q3 + 1.5 * iqr;
        
        return sortedValues.Count(val => val < lowerBound || val > upperBound);
    }

    private void PerformMaintenance(object? state)
    {
        if (_disposed) return;

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
                _eventCreationSemaphore.Release();
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
            _logger.LogWarning("High event usage: {ActiveEvents}/{MaxEvents} active events",
                stats.ActiveEvents, MAX_CONCURRENT_EVENTS);
        }
        
        if (stats.ActiveTimingSessions > 10)
        {
            _logger.LogInformation("Many active timing sessions: {ActiveSessions}",
                stats.ActiveTimingSessions);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CudaEventManager));
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

            _logger.LogInformation("CUDA Event Manager disposed: created {TotalEvents} events, " +
                                 "performed {TotalMeasurements} timing measurements",
                _totalEventsCreated, _totalTimingMeasurements);
        }
    }
}

// Supporting types and classes...

/// <summary>
/// Unique identifier for CUDA events
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
/// Types of CUDA events
/// </summary>
public enum CudaEventType
{
    Timing,
    Synchronization
}

/// <summary>
/// CUDA event flags (backward compatibility)
/// </summary>
public enum CudaEventFlags
{
    Default,
    BlockingSync,
    DisableTiming,
    Interprocess
}

/// <summary>
/// Information about an active CUDA event
/// </summary>
internal sealed class CudaEventInfo
{
    public EventId EventId { get; set; }
    public IntPtr Handle { get; set; }
    public CudaEventType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public IntPtr Stream { get; set; }
    public bool IsFromPool { get; set; }
}

/// <summary>
/// Handle for managed CUDA events with automatic cleanup
/// </summary>
public sealed class CudaEventHandle : IDisposable
{
    private readonly CudaEventManager _manager;
    private volatile bool _disposed;

    internal CudaEventHandle(EventId eventId, IntPtr eventHandle, CudaEventManager manager, CudaEventType type)
    {
        EventId = eventId;
        Handle = eventHandle;
        Type = type;
        _manager = manager;
    }

    public EventId EventId { get; }
    public IntPtr Handle { get; }
    public CudaEventType Type { get; }

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
public sealed class CudaTimingResult
{
    public string OperationName { get; set; } = string.Empty;
    public float GpuTimeMs { get; set; }
    public float CpuTimeMs { get; set; }
    public float OverheadMs { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public IntPtr Stream { get; set; }
}

/// <summary>
/// Result of profiling with statistical analysis
/// </summary>
public sealed class CudaProfilingResult
{
    public string SessionId { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public IntPtr Stream { get; set; }
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
    public Dictionary<int, double> Percentiles { get; set; } = new();
    public double CoefficientOfVariation { get; set; }
    public int OutlierCount { get; set; }
    public double ThroughputOpsPerSecond { get; set; }
    public double SessionDurationMs { get; set; }
}

/// <summary>
/// Timing session for tracking long-running profiling
/// </summary>
internal sealed class CudaTimingSession
{
    public string SessionId { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public IntPtr Stream { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public int PlannedIterations { get; set; }
    public int ActualIterations { get; set; }
    public int CompletedIterations { get; set; }
}

/// <summary>
/// Statistics for the event manager
/// </summary>
public sealed class CudaEventStatistics
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
    public CudaEventPoolStatistics? PoolStatistics { get; set; }
    public int MaxConcurrentEvents { get; set; }
}