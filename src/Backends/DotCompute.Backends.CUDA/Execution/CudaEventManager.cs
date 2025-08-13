// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution;

/// <summary>
/// Advanced CUDA event manager for timing, profiling, and synchronization
/// </summary>
public sealed class CudaEventManager : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<IntPtr, CudaEventInfo> _events;
    private readonly ConcurrentQueue<IntPtr> _eventPool;
    private readonly SemaphoreSlim _eventSemaphore;
    private readonly Timer _cleanupTimer;
    private readonly object _lockObject = new();
    private bool _disposed;

    // Event configuration
    private const int MaxConcurrentEvents = 1000;
    private const int InitialEventPoolSize = 50;
    private const uint CudaEventDefault = 0x00;
    private const uint CudaEventBlockingSync = 0x01;
    private const uint CudaEventDisableTiming = 0x02;
    private const uint CudaEventInterprocess = 0x04;

    public CudaEventManager(CudaContext context, ILogger<CudaEventManager> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _events = new ConcurrentDictionary<IntPtr, CudaEventInfo>();
        _eventPool = new ConcurrentQueue<IntPtr>();
        _eventSemaphore = new SemaphoreSlim(MaxConcurrentEvents, MaxConcurrentEvents);

        Initialize();

        // Set up cleanup timer
        _cleanupTimer = new Timer(PerformCleanup, null, 
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));

        _logger.LogInformation("CUDA Event Manager initialized with {MaxEvents} max concurrent events", 
            MaxConcurrentEvents);
    }

    /// <summary>
    /// Creates a new CUDA event with timing enabled
    /// </summary>
    public IntPtr CreateEvent(CudaEventFlags flags = CudaEventFlags.Default)
    {
        ThrowIfDisposed();
        
        _context.MakeCurrent();
        
        // Try to get from pool first
        if (_eventPool.TryDequeue(out var pooledEvent))
        {
            var pooledInfo = new CudaEventInfo
            {
                Handle = pooledEvent,
                Flags = flags,
                CreatedAt = DateTimeOffset.UtcNow,
                IsFromPool = true
            };
            
            _events[pooledEvent] = pooledInfo;
            _logger.LogTrace("Reused pooled event {Event}", pooledEvent);
            return pooledEvent;
        }

        // Create new event
        var eventHandle = IntPtr.Zero;
        var cudaFlags = ConvertToCudaFlags(flags);
        var result = Native.CudaRuntime.cudaEventCreateWithFlags(ref eventHandle, cudaFlags);
        Native.CudaRuntime.CheckError(result, "creating CUDA event");

        var eventInfo = new CudaEventInfo
        {
            Handle = eventHandle,
            Flags = flags,
            CreatedAt = DateTimeOffset.UtcNow,
            IsFromPool = false
        };

        _events[eventHandle] = eventInfo;
        
        _logger.LogTrace("Created new CUDA event {Event} with flags {Flags}", eventHandle, flags);
        return eventHandle;
    }

    /// <summary>
    /// Creates a timing event pair for measuring execution time
    /// </summary>
    public (IntPtr start, IntPtr end) CreateTimingEventPair()
    {
        var startEvent = CreateEvent(CudaEventFlags.Default);
        var endEvent = CreateEvent(CudaEventFlags.Default);
        
        return (startEvent, endEvent);
    }

    /// <summary>
    /// Destroys a CUDA event and returns it to pool if applicable
    /// </summary>
    public void DestroyEvent(IntPtr eventHandle)
    {
        if (eventHandle == IntPtr.Zero)
            return;

        if (!_events.TryRemove(eventHandle, out var eventInfo))
        {
            _logger.LogWarning("Attempted to destroy unknown event {Event}", eventHandle);
            return;
        }

        // Return to pool if it's a timing event and pool isn't full
        if (eventInfo.IsFromPool || 
            (eventInfo.Flags == CudaEventFlags.Default && _eventPool.Count < InitialEventPoolSize * 2))
        {
            _eventPool.Enqueue(eventHandle);
            _logger.LogTrace("Returned event {Event} to pool", eventHandle);
            return;
        }

        // Actually destroy the event
        _context.MakeCurrent();
        var result = Native.CudaRuntime.cudaEventDestroy(eventHandle);
        if (result != CudaError.Success)
        {
            _logger.LogWarning("Failed to destroy CUDA event {Event}: {Error}", 
                eventHandle, Native.CudaRuntime.GetErrorString(result));
        }
        else
        {
            _logger.LogTrace("Destroyed CUDA event {Event}", eventHandle);
        }
    }

    /// <summary>
    /// Records an event on the specified stream
    /// </summary>
    public void RecordEvent(IntPtr eventHandle, IntPtr stream = default)
    {
        ThrowIfDisposed();
        _context.MakeCurrent();

        var result = Native.CudaRuntime.cudaEventRecord(eventHandle, stream);
        Native.CudaRuntime.CheckError(result, $"recording event {eventHandle} on stream {stream}");

        if (_events.TryGetValue(eventHandle, out var eventInfo))
        {
            eventInfo.RecordedAt = DateTimeOffset.UtcNow;
            eventInfo.Stream = stream;
        }

        _logger.LogTrace("Recorded event {Event} on stream {Stream}", eventHandle, stream);
    }

    /// <summary>
    /// Synchronizes with an event (waits for completion)
    /// </summary>
    public async Task SynchronizeEventAsync(IntPtr eventHandle, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _context.MakeCurrent();

        await Task.Run(() =>
        {
            var result = Native.CudaRuntime.cudaEventSynchronize(eventHandle);
            Native.CudaRuntime.CheckError(result, $"synchronizing event {eventHandle}");
        }, cancellationToken).ConfigureAwait(false);

        if (_events.TryGetValue(eventHandle, out var eventInfo))
        {
            eventInfo.CompletedAt = DateTimeOffset.UtcNow;
        }

        _logger.LogTrace("Synchronized with event {Event}", eventHandle);
    }

    /// <summary>
    /// Checks if an event has completed
    /// </summary>
    public bool IsEventComplete(IntPtr eventHandle)
    {
        ThrowIfDisposed();
        _context.MakeCurrent();

        var result = Native.CudaRuntime.cudaEventQuery(eventHandle);
        return result == CudaError.Success;
    }

    /// <summary>
    /// Waits for event completion with timeout
    /// </summary>
    public async Task<bool> WaitForEventAsync(IntPtr eventHandle, TimeSpan timeout, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            while (!IsEventComplete(eventHandle))
            {
                await Task.Delay(1, combinedCts.Token).ConfigureAwait(false);
            }

            if (_events.TryGetValue(eventHandle, out var eventInfo))
            {
                eventInfo.CompletedAt = DateTimeOffset.UtcNow;
            }

            return true;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return false; // Timeout
        }
    }

    /// <summary>
    /// Calculates elapsed time between two events in milliseconds
    /// </summary>
    public float ElapsedTime(IntPtr startEvent, IntPtr endEvent)
    {
        ThrowIfDisposed();
        _context.MakeCurrent();

        var milliseconds = 0f;
        var result = Native.CudaRuntime.cudaEventElapsedTime(ref milliseconds, startEvent, endEvent);
        Native.CudaRuntime.CheckError(result, $"calculating elapsed time between events {startEvent} and {endEvent}");

        _logger.LogTrace("Elapsed time between events {Start} and {End}: {Time}ms", 
            startEvent, endEvent, milliseconds);

        return milliseconds;
    }

    /// <summary>
    /// Measures execution time of an operation
    /// </summary>
    public async Task<CudaTimingResult> MeasureAsync(
        Func<IntPtr, Task> operation, 
        IntPtr stream = default,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var (startEvent, endEvent) = CreateTimingEventPair();
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            RecordEvent(startEvent, stream);
            await operation(stream).ConfigureAwait(false);
            RecordEvent(endEvent, stream);
            
            await SynchronizeEventAsync(endEvent, cancellationToken).ConfigureAwait(false);
            
            var endTime = DateTimeOffset.UtcNow;
            var gpuTime = ElapsedTime(startEvent, endEvent);
            var cpuTime = (float)(endTime - startTime).TotalMilliseconds;

            return new CudaTimingResult
            {
                GpuTimeMs = gpuTime,
                CpuTimeMs = cpuTime,
                OverheadMs = Math.Max(0, cpuTime - gpuTime),
                StartTime = startTime,
                EndTime = endTime
            };
        }
        finally
        {
            DestroyEvent(startEvent);
            DestroyEvent(endEvent);
        }
    }

    /// <summary>
    /// Profiles an operation with multiple iterations
    /// </summary>
    public async Task<CudaProfilingResult> ProfileAsync(
        Func<IntPtr, Task> operation,
        int iterations = 100,
        IntPtr stream = default,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var timings = new List<CudaTimingResult>();
        
        // Warm up
        for (int i = 0; i < Math.Min(10, iterations / 10); i++)
        {
            await MeasureAsync(operation, stream, cancellationToken).ConfigureAwait(false);
        }

        // Profile iterations
        for (int i = 0; i < iterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var timing = await MeasureAsync(operation, stream, cancellationToken).ConfigureAwait(false);
            timings.Add(timing);
        }

        var gpuTimes = timings.Select(t => t.GpuTimeMs).ToArray();
        var cpuTimes = timings.Select(t => t.CpuTimeMs).ToArray();

        Array.Sort(gpuTimes);
        Array.Sort(cpuTimes);

        var avgGpuTime = gpuTimes.Average();
        var avgCpuTime = cpuTimes.Average();

        return new CudaProfilingResult
        {
            Iterations = iterations,
            AverageGpuTimeMs = avgGpuTime,
            MinGpuTimeMs = gpuTimes.Min(),
            MaxGpuTimeMs = gpuTimes.Max(),
            MedianGpuTimeMs = gpuTimes[gpuTimes.Length / 2],
            StdDevGpuTimeMs = CalculateStandardDeviation(gpuTimes, avgGpuTime),
            AverageCpuTimeMs = avgCpuTime,
            AverageOverheadMs = timings.Average(t => t.OverheadMs),
            Percentiles = new Dictionary<int, double>
            {
                [50] = gpuTimes[gpuTimes.Length / 2],
                [90] = gpuTimes[(int)(gpuTimes.Length * 0.9)],
                [95] = gpuTimes[(int)(gpuTimes.Length * 0.95)],
                [99] = gpuTimes[(int)(gpuTimes.Length * 0.99)]
            }
        };
    }

    /// <summary>
    /// Creates events for batch timing operations
    /// </summary>
    public IntPtr[] CreateEventBatch(int count, CudaEventFlags flags = CudaEventFlags.Default)
    {
        ThrowIfDisposed();

        var events = new IntPtr[count];
        for (int i = 0; i < count; i++)
        {
            events[i] = CreateEvent(flags);
        }

        _logger.LogDebug("Created batch of {Count} events", count);
        return events;
    }

    /// <summary>
    /// Destroys a batch of events
    /// </summary>
    public void DestroyEventBatch(IntPtr[] events)
    {
        if (events == null)
            return;

        foreach (var eventHandle in events)
        {
            DestroyEvent(eventHandle);
        }

        _logger.LogDebug("Destroyed batch of {Count} events", events.Length);
    }

    /// <summary>
    /// Gets event manager statistics
    /// </summary>
    public CudaEventStatistics GetStatistics()
    {
        ThrowIfDisposed();

        var activeEvents = _events.Count;
        var pooledEvents = _eventPool.Count;
        var completedEvents = _events.Values.Count(e => e.CompletedAt.HasValue);

        return new CudaEventStatistics
        {
            ActiveEvents = activeEvents,
            PooledEvents = pooledEvents,
            CompletedEvents = completedEvents,
            TotalEventsCreated = activeEvents + pooledEvents,
            MaxConcurrentEvents = MaxConcurrentEvents,
            AverageEventAge = _events.Values
                .Where(e => e.CreatedAt != default)
                .Select(e => (DateTimeOffset.UtcNow - e.CreatedAt).TotalSeconds)
                .DefaultIfEmpty(0)
                .Average()
        };
    }

    /// <summary>
    /// Adds callback to be executed when event completes
    /// </summary>
    public void AddEventCallback(IntPtr eventHandle, Action<IntPtr> callback)
    {
        ThrowIfDisposed();

        _ = Task.Run(async () =>
        {
            try
            {
                await SynchronizeEventAsync(eventHandle).ConfigureAwait(false);
                callback(eventHandle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event callback for event {Event}", eventHandle);
            }
        });
    }

    private void Initialize()
    {
        _context.MakeCurrent();

        // Pre-allocate event pool
        for (int i = 0; i < InitialEventPoolSize; i++)
        {
            var eventHandle = IntPtr.Zero;
            var result = Native.CudaRuntime.cudaEventCreateWithFlags(ref eventHandle, CudaEventDefault);
            if (result == CudaError.Success)
            {
                _eventPool.Enqueue(eventHandle);
            }
            else
            {
                _logger.LogWarning("Failed to pre-allocate event {Index}: {Error}", 
                    i, Native.CudaRuntime.GetErrorString(result));
                break;
            }
        }

        _logger.LogDebug("Pre-allocated {Count} events in pool", _eventPool.Count);
    }

    private static uint ConvertToCudaFlags(CudaEventFlags flags)
    {
        return flags switch
        {
            CudaEventFlags.Default => CudaEventDefault,
            CudaEventFlags.BlockingSync => CudaEventBlockingSync,
            CudaEventFlags.DisableTiming => CudaEventDisableTiming,
            CudaEventFlags.Interprocess => CudaEventInterprocess,
            _ => CudaEventDefault
        };
    }

    private static double CalculateStandardDeviation(float[] values, double mean)
    {
        var sumOfSquaredDiffs = values.Sum(val => Math.Pow(val - mean, 2));
        return Math.Sqrt(sumOfSquaredDiffs / values.Length);
    }

    private void PerformCleanup(object? state)
    {
        if (_disposed)
            return;

        try
        {
            lock (_lockObject)
            {
                var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-5);
                var oldEvents = _events
                    .Where(kvp => kvp.Value.CompletedAt.HasValue && kvp.Value.CompletedAt < cutoffTime)
                    .Take(50) // Limit cleanup batch size
                    .ToList();

                foreach (var (eventHandle, _) in oldEvents)
                {
                    DestroyEvent(eventHandle);
                }

                if (oldEvents.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} old events", oldEvents.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during event cleanup");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaEventManager));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer?.Dispose();

            // Clean up all events
            foreach (var eventHandle in _events.Keys)
            {
                try
                {
                    _context.MakeCurrent();
                    Native.CudaRuntime.cudaEventDestroy(eventHandle);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error destroying event {Event} during disposal", eventHandle);
                }
            }

            // Clean up pooled events
            while (_eventPool.TryDequeue(out var eventHandle))
            {
                try
                {
                    _context.MakeCurrent();
                    Native.CudaRuntime.cudaEventDestroy(eventHandle);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error destroying pooled event {Event} during disposal", eventHandle);
                }
            }

            _eventSemaphore?.Dispose();
            _disposed = true;

            _logger.LogInformation("CUDA Event Manager disposed");
        }
    }
}

/// <summary>
/// CUDA event flags
/// </summary>
public enum CudaEventFlags
{
    Default,
    BlockingSync,
    DisableTiming,
    Interprocess
}

/// <summary>
/// Information about a CUDA event
/// </summary>
internal sealed class CudaEventInfo
{
    public IntPtr Handle { get; set; }
    public CudaEventFlags Flags { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public IntPtr Stream { get; set; }
    public bool IsFromPool { get; set; }
}

/// <summary>
/// Result of a timing measurement
/// </summary>
public sealed class CudaTimingResult
{
    public float GpuTimeMs { get; set; }
    public float CpuTimeMs { get; set; }
    public float OverheadMs { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
}

/// <summary>
/// Result of profiling with multiple iterations
/// </summary>
public sealed class CudaProfilingResult
{
    public int Iterations { get; set; }
    public double AverageGpuTimeMs { get; set; }
    public double MinGpuTimeMs { get; set; }
    public double MaxGpuTimeMs { get; set; }
    public double MedianGpuTimeMs { get; set; }
    public double StdDevGpuTimeMs { get; set; }
    public double AverageCpuTimeMs { get; set; }
    public double AverageOverheadMs { get; set; }
    public Dictionary<int, double> Percentiles { get; set; } = [];
}

/// <summary>
/// Statistics for the event manager
/// </summary>
public sealed class CudaEventStatistics
{
    public int ActiveEvents { get; set; }
    public int PooledEvents { get; set; }
    public int CompletedEvents { get; set; }
    public int TotalEventsCreated { get; set; }
    public int MaxConcurrentEvents { get; set; }
    public double AverageEventAge { get; set; }
}