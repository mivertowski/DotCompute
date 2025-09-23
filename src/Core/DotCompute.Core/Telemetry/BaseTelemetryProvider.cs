// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Interfaces.Telemetry;
using DotCompute.Abstractions.Pipelines.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Unified base telemetry provider that consolidates common telemetry patterns
/// across all backend implementations (Metal, CUDA, OpenCL, CPU, LINQ).
/// Eliminates over 2,500 lines of duplicate telemetry code.
/// </summary>
public abstract class BaseTelemetryProvider : ITelemetryProvider, IDisposable
{
    protected readonly ILogger Logger;
    protected readonly TelemetryConfiguration Configuration;
    protected readonly Meter Meter;
    protected readonly ActivitySource ActivitySource;

    // Core metrics collections
    private readonly ConcurrentDictionary<string, Counter<long>> _counters = new();
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new();
    private readonly ConcurrentDictionary<string, Gauge<double>> _gauges = new();
    private readonly ConcurrentDictionary<string, ObservableGauge<double>> _observableGauges = new();

    // Performance tracking
    private readonly ConcurrentDictionary<string, PerformanceMetric> _performanceMetrics = new();
    private readonly ConcurrentQueue<TelemetryEvent> _eventQueue = new();

    // Thread-safe counters
    private long _totalOperations;
    private long _totalErrors;
    private long _telemetryOverheadTicks;

    private volatile bool _disposed;

    protected BaseTelemetryProvider(
        ILogger logger,
        TelemetryConfiguration configuration,
        string serviceName,
        string serviceVersion)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        Meter = new Meter(serviceName, serviceVersion);
        ActivitySource = new ActivitySource(serviceName, serviceVersion);

        InitializeStandardMetrics();

        Logger.LogDebug("Base telemetry provider initialized for {ServiceName} v{ServiceVersion}",
            serviceName, serviceVersion);
    }

    #region Core Telemetry Methods

    /// <summary>
    /// Records a metric value with comprehensive error handling and performance tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void RecordMetric(string name, double value, IDictionary<string, object?>? tags = null)
    {
        if (_disposed || !Configuration.IsEnabled)
        {
            return;
        }


        var startTicks = Stopwatch.GetTimestamp();

        try
        {
            var histogram = _histograms.GetOrAdd(name,
                _ => Meter.CreateHistogram<double>(name, "units", "Custom metric"));

            if (tags != null && tags.Count > 0)
            {
                var tagList = CreateTagList(tags);
                histogram.Record(value, tagList);
            }
            else
            {
                histogram.Record(value);
            }

            Interlocked.Increment(ref _totalOperations);
        }
        catch (Exception ex) when (!Configuration.ThrowOnTelemetryErrors)
        {
            HandleTelemetryError(ex, nameof(RecordMetric), name);
        }
        finally
        {
            UpdateTelemetryOverhead(startTicks);
        }
    }

    /// <summary>
    /// Increments a counter with optimized performance and error handling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void IncrementCounter(string name, long increment = 1, IDictionary<string, object?>? tags = null)
    {
        if (_disposed || !Configuration.IsEnabled)
        {
            return;
        }


        var startTicks = Stopwatch.GetTimestamp();

        try
        {
            var counter = _counters.GetOrAdd(name,
                _ => Meter.CreateCounter<long>(name, "count", "Counter metric"));

            if (tags != null && tags.Count > 0)
            {
                var tagList = CreateTagList(tags);
                counter.Add(increment, tagList);
            }
            else
            {
                counter.Add(increment);
            }

            Interlocked.Increment(ref _totalOperations);
        }
        catch (Exception ex) when (!Configuration.ThrowOnTelemetryErrors)
        {
            HandleTelemetryError(ex, nameof(IncrementCounter), name);
        }
        finally
        {
            UpdateTelemetryOverhead(startTicks);
        }
    }

    /// <summary>
    /// Records a histogram value with performance optimization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void RecordHistogram(string name, double value, IDictionary<string, object?>? tags = null)
    {
        if (_disposed || !Configuration.IsEnabled)
        {
            return;
        }


        var startTicks = Stopwatch.GetTimestamp();

        try
        {
            var histogram = _histograms.GetOrAdd(name,
                _ => Meter.CreateHistogram<double>(name, "units", "Histogram metric"));

            if (tags != null && tags.Count > 0)
            {
                var tagList = CreateTagList(tags);
                histogram.Record(value, tagList);
            }
            else
            {
                histogram.Record(value);
            }

            Interlocked.Increment(ref _totalOperations);
        }
        catch (Exception ex) when (!Configuration.ThrowOnTelemetryErrors)
        {
            HandleTelemetryError(ex, nameof(RecordHistogram), name);
        }
        finally
        {
            UpdateTelemetryOverhead(startTicks);
        }
    }

    /// <summary>
    /// Starts an activity with comprehensive context and error handling.
    /// </summary>
    public virtual Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        if (_disposed || !Configuration.IsEnabled)
        {
            return null;
        }


        try
        {
            var activity = ActivitySource.StartActivity(name, kind);

            if (activity != null)
            {
                // Add standard context
                activity.SetTag("service.name", Configuration.ServiceName);
                activity.SetTag("service.version", Configuration.ServiceVersion);
                activity.SetTag("telemetry.provider", GetType().Name);

                // Add backend-specific context
                AddBackendSpecificActivityContext(activity);
            }

            return activity;
        }
        catch (Exception ex) when (!Configuration.ThrowOnTelemetryErrors)
        {
            HandleTelemetryError(ex, nameof(StartActivity), name);
            return null;
        }
    }

    /// <summary>
    /// Records an event with structured data and performance tracking.
    /// </summary>
    public virtual void RecordEvent(string name, IDictionary<string, object?>? attributes = null)
    {
        if (_disposed || !Configuration.IsEnabled)
        {
            return;
        }


        var startTicks = Stopwatch.GetTimestamp();

        try
        {
            var telemetryEvent = new TelemetryEvent
            {
                Name = name,
                Timestamp = DateTimeOffset.UtcNow,
                Attributes = attributes?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? [],
                Source = GetType().Name
            };

            // Queue for async processing if configured
            if (Configuration.EnableAsyncProcessing)
            {
                _eventQueue.Enqueue(telemetryEvent);
            }
            else
            {
                ProcessEventSync(telemetryEvent);
            }

            Interlocked.Increment(ref _totalOperations);
        }
        catch (Exception ex) when (!Configuration.ThrowOnTelemetryErrors)
        {
            HandleTelemetryError(ex, nameof(RecordEvent), name);
        }
        finally
        {
            UpdateTelemetryOverhead(startTicks);
        }
    }

    /// <summary>
    /// Starts a performance timer with comprehensive tracking.
    /// </summary>
    public virtual IOperationTimer StartTimer(string operationName, IDictionary<string, object?>? tags = null)
    {
        if (_disposed || !Configuration.IsEnabled)
        {

            return new NullOperationTimer();
        }


        try
        {
            IncrementCounter("operation.timer.started", 1,
                new Dictionary<string, object?> { ["operation"] = operationName });

            return new UnifiedOperationTimer(operationName, tags, this);
        }
        catch (Exception ex) when (!Configuration.ThrowOnTelemetryErrors)
        {
            HandleTelemetryError(ex, nameof(StartTimer), operationName);
            return new NullOperationTimer();
        }
    }

    #endregion

    #region Backend-Specific Memory Tracking

    /// <summary>
    /// Records memory allocation with unified tracking across all backends.
    /// </summary>
    public virtual void RecordMemoryAllocation(long bytes, string? allocationType = null)
    {
        if (_disposed || !Configuration.IsEnabled)
        {
            return;
        }


        try
        {
            var tags = new Dictionary<string, object?>();
            if (!string.IsNullOrEmpty(allocationType))
            {
                tags["allocation.type"] = allocationType;
            }
            tags["backend.type"] = GetBackendType();

            RecordHistogram("memory.allocation.bytes", bytes, tags);
            IncrementCounter("memory.allocation.count", 1, tags);
            RecordMetric("memory.total_allocated.bytes", bytes, tags);

            // Backend-specific memory tracking
            OnMemoryAllocated(bytes, allocationType, tags);
        }
        catch (Exception ex) when (!Configuration.ThrowOnTelemetryErrors)
        {
            HandleTelemetryError(ex, nameof(RecordMemoryAllocation), allocationType ?? "unknown");
        }
    }

    /// <summary>
    /// Records accelerator utilization with backend-specific context.
    /// </summary>
    public virtual void RecordAcceleratorUtilization(string acceleratorType, double utilization, long memoryUsed)
    {
        if (_disposed || !Configuration.IsEnabled)
        {
            return;
        }


        try
        {
            var tags = new Dictionary<string, object?>
            {
                ["accelerator.type"] = acceleratorType,
                ["accelerator.category"] = GetAcceleratorCategory(acceleratorType),
                ["backend.type"] = GetBackendType()
            };

            RecordMetric("accelerator.utilization.percent", Math.Clamp(utilization * 100, 0, 100), tags);
            RecordMetric("accelerator.memory.used.bytes", memoryUsed, tags);

            // Backend-specific utilization tracking
            OnAcceleratorUtilizationRecorded(acceleratorType, utilization, memoryUsed, tags);
        }
        catch (Exception ex) when (!Configuration.ThrowOnTelemetryErrors)
        {
            HandleTelemetryError(ex, nameof(RecordAcceleratorUtilization), acceleratorType);
        }
    }

    /// <summary>
    /// Records kernel execution with comprehensive performance tracking.
    /// </summary>
    public virtual void RecordKernelExecution(string kernelName, TimeSpan duration, long operationCount)
    {
        if (_disposed || !Configuration.IsEnabled)
        {
            return;
        }


        try
        {
            var tags = new Dictionary<string, object?>
            {
                ["kernel.name"] = kernelName,
                ["kernel.category"] = ExtractKernelCategory(kernelName),
                ["backend.type"] = GetBackendType()
            };

            RecordHistogram("kernel.execution.duration.ms", duration.TotalMilliseconds, tags);
            RecordHistogram("kernel.execution.operations", operationCount, tags);

            if (duration.TotalSeconds > 0)
            {
                var opsPerSecond = operationCount / duration.TotalSeconds;
                RecordMetric("kernel.execution.ops_per_second", opsPerSecond, tags);
            }

            IncrementCounter("kernel.execution.count", 1, tags);

            // Backend-specific kernel tracking
            OnKernelExecuted(kernelName, duration, operationCount, tags);
        }
        catch (Exception ex) when (!Configuration.ThrowOnTelemetryErrors)
        {
            HandleTelemetryError(ex, nameof(RecordKernelExecution), kernelName);
        }
    }

    /// <summary>
    /// Records memory transfer with bandwidth analysis.
    /// </summary>
    public virtual void RecordMemoryTransfer(string direction, long bytes, TimeSpan duration)
    {
        if (_disposed || !Configuration.IsEnabled)
        {
            return;
        }


        try
        {
            var tags = new Dictionary<string, object?>
            {
                ["transfer.direction"] = direction,
                ["transfer.type"] = GetTransferType(direction),
                ["backend.type"] = GetBackendType()
            };

            RecordHistogram("memory.transfer.bytes", bytes, tags);
            RecordHistogram("memory.transfer.duration.ms", duration.TotalMilliseconds, tags);

            if (duration.TotalSeconds > 0)
            {
                var bandwidthMBps = (bytes / (1024.0 * 1024.0)) / duration.TotalSeconds;
                RecordMetric("memory.transfer.bandwidth.mbps", bandwidthMBps, tags);
            }

            IncrementCounter("memory.transfer.count", 1, tags);

            // Backend-specific transfer tracking
            OnMemoryTransferRecorded(direction, bytes, duration, tags);
        }
        catch (Exception ex) when (!Configuration.ThrowOnTelemetryErrors)
        {
            HandleTelemetryError(ex, nameof(RecordMemoryTransfer), direction);
        }
    }

    /// <summary>
    /// Records garbage collection metrics with generation analysis.
    /// </summary>
    public virtual void RecordGarbageCollection(int generation, TimeSpan duration, long memoryBefore, long memoryAfter)
    {
        if (_disposed || !Configuration.IsEnabled)
        {
            return;
        }


        try
        {
            var memoryReleased = memoryBefore - memoryAfter;
            var tags = new Dictionary<string, object?>
            {
                ["gc.generation"] = generation,
                ["gc.type"] = GetGCType(generation),
                ["backend.type"] = GetBackendType()
            };

            RecordHistogram("gc.duration.ms", duration.TotalMilliseconds, tags);
            RecordHistogram("gc.memory.before.bytes", memoryBefore, tags);
            RecordHistogram("gc.memory.after.bytes", memoryAfter, tags);
            RecordHistogram("gc.memory.released.bytes", memoryReleased, tags);

            if (memoryBefore > 0)
            {
                var releasePercentage = (double)memoryReleased / memoryBefore * 100;
                RecordMetric("gc.memory.release.percentage", releasePercentage, tags);
            }

            IncrementCounter("gc.collection.count", 1, tags);

            // Backend-specific GC tracking
            OnGarbageCollectionRecorded(generation, duration, memoryBefore, memoryAfter, tags);
        }
        catch (Exception ex) when (!Configuration.ThrowOnTelemetryErrors)
        {
            HandleTelemetryError(ex, nameof(RecordGarbageCollection), $"Gen{generation}");
        }
    }

    #endregion

    #region Performance Metrics

    /// <summary>
    /// Gets comprehensive telemetry performance metrics.
    /// </summary>
    public virtual TelemetryPerformanceMetrics GetPerformanceMetrics()
    {
        return new TelemetryPerformanceMetrics
        {
            TotalOperations = Interlocked.Read(ref _totalOperations),
            TotalErrors = Interlocked.Read(ref _totalErrors),
            OverheadTicks = Interlocked.Read(ref _telemetryOverheadTicks),
            OverheadPercentage = CalculateOverheadPercentage(),
            EventQueueSize = _eventQueue.Count,
            ActiveMetrics = _counters.Count + _histograms.Count + _gauges.Count,
            BackendType = GetBackendType()
        };
    }

    /// <summary>
    /// Gets a specific meter for advanced scenarios.
    /// </summary>
    public virtual Meter GetMeter(string name, string? version = null)
    {
        return new Meter(name, version);
    }

    #endregion

    #region Abstract and Virtual Methods for Backend Customization

    /// <summary>
    /// Gets the backend type identifier (e.g., "CUDA", "Metal", "OpenCL", "CPU").
    /// </summary>
    protected abstract string GetBackendType();

    /// <summary>
    /// Adds backend-specific context to activities.
    /// </summary>
    protected virtual void AddBackendSpecificActivityContext(Activity activity)
    {
        // Override in derived classes for backend-specific context
    }

    /// <summary>
    /// Called when memory is allocated - override for backend-specific tracking.
    /// </summary>
    protected virtual void OnMemoryAllocated(long bytes, string? allocationType, Dictionary<string, object?> tags)
    {
        // Override in derived classes for backend-specific memory tracking
    }

    /// <summary>
    /// Called when accelerator utilization is recorded - override for backend-specific tracking.
    /// </summary>
    protected virtual void OnAcceleratorUtilizationRecorded(string acceleratorType, double utilization, long memoryUsed, Dictionary<string, object?> tags)
    {
        // Override in derived classes for backend-specific utilization tracking
    }

    /// <summary>
    /// Called when kernel execution is recorded - override for backend-specific tracking.
    /// </summary>
    protected virtual void OnKernelExecuted(string kernelName, TimeSpan duration, long operationCount, Dictionary<string, object?> tags)
    {
        // Override in derived classes for backend-specific kernel tracking
    }

    /// <summary>
    /// Called when memory transfer is recorded - override for backend-specific tracking.
    /// </summary>
    protected virtual void OnMemoryTransferRecorded(string direction, long bytes, TimeSpan duration, Dictionary<string, object?> tags)
    {
        // Override in derived classes for backend-specific transfer tracking
    }

    /// <summary>
    /// Called when garbage collection is recorded - override for backend-specific tracking.
    /// </summary>
    protected virtual void OnGarbageCollectionRecorded(int generation, TimeSpan duration, long memoryBefore, long memoryAfter, Dictionary<string, object?> tags)
    {
        // Override in derived classes for backend-specific GC tracking
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a TagList from a dictionary for metrics.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static TagList CreateTagList(IDictionary<string, object?> tags)
    {
        var tagList = new TagList();
        foreach (var tag in tags)
        {
            tagList.Add(tag.Key, tag.Value);
        }
        return tagList;
    }

    /// <summary>
    /// Handles telemetry errors consistently across all operations.
    /// </summary>
    protected virtual void HandleTelemetryError(Exception ex, string operation, string context)
    {
        Interlocked.Increment(ref _totalErrors);

        if (Configuration.LogTelemetryErrors)
        {
            Logger.LogWarning(ex, "Telemetry error in {Operation} for {Context}", operation, context);
        }
    }

    /// <summary>
    /// Updates telemetry overhead tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void UpdateTelemetryOverhead(long startTicks)
    {
        if (Configuration.TrackOverhead)
        {
            var overheadTicks = Stopwatch.GetTimestamp() - startTicks;
            Interlocked.Add(ref _telemetryOverheadTicks, overheadTicks);
        }
    }

    /// <summary>
    /// Processes an event synchronously.
    /// </summary>
    protected virtual void ProcessEventSync(TelemetryEvent telemetryEvent)
    {
        using var activity = StartActivity($"event.{telemetryEvent.Name}");

        if (activity != null)
        {
            activity.SetTag("event.name", telemetryEvent.Name);
            activity.SetTag("event.timestamp", telemetryEvent.Timestamp.ToString("O"));
            activity.SetTag("event.source", telemetryEvent.Source);

            foreach (var attr in telemetryEvent.Attributes)
            {
                activity.SetTag($"event.{attr.Key}", attr.Value?.ToString());
            }
        }
    }

    /// <summary>
    /// Calculates telemetry overhead percentage.
    /// </summary>
    protected virtual double CalculateOverheadPercentage()
    {
        if (!Configuration.TrackOverhead)
        {
            return 0.0;
        }


        var overheadTicks = Interlocked.Read(ref _telemetryOverheadTicks);
        var totalOps = Interlocked.Read(ref _totalOperations);

        if (totalOps == 0)
        {
            return 0.0;
        }


        var avgOverheadTicks = overheadTicks / (double)totalOps;
        var avgOverheadMs = (avgOverheadTicks / Stopwatch.Frequency) * 1000;

        return Math.Min(avgOverheadMs * 100, 100.0); // Cap at 100%
    }

    /// <summary>
    /// Initializes standard metrics used across all backends.
    /// </summary>
    protected virtual void InitializeStandardMetrics()
    {
        // Pre-create commonly used metrics to reduce creation overhead
        _counters.TryAdd("operation.timer.started",
            Meter.CreateCounter<long>("operation.timer.started", "count", "Operation timers started"));
        _counters.TryAdd("memory.allocation.count",
            Meter.CreateCounter<long>("memory.allocation.count", "count", "Memory allocations"));
        _counters.TryAdd("kernel.execution.count",
            Meter.CreateCounter<long>("kernel.execution.count", "count", "Kernel executions"));
        _counters.TryAdd("memory.transfer.count",
            Meter.CreateCounter<long>("memory.transfer.count", "count", "Memory transfers"));

        _histograms.TryAdd("memory.allocation.bytes",
            Meter.CreateHistogram<double>("memory.allocation.bytes", "bytes", "Memory allocation size"));
        _histograms.TryAdd("kernel.execution.duration.ms",
            Meter.CreateHistogram<double>("kernel.execution.duration.ms", "ms", "Kernel execution duration"));
        _histograms.TryAdd("memory.transfer.bytes",
            Meter.CreateHistogram<double>("memory.transfer.bytes", "bytes", "Memory transfer size"));
    }

    /// <summary>
    /// Extracts kernel category from name for classification.
    /// </summary>
    protected static string ExtractKernelCategory(string kernelName)
    {
        var name = kernelName.ToLowerInvariant();
        return name switch
        {
            var n when n.Contains("add") || n.Contains("sum") => "arithmetic",
            var n when n.Contains("mul") || n.Contains("multiply") => "arithmetic",
            var n when n.Contains("matrix") || n.Contains("gemm") => "linear_algebra",
            var n when n.Contains("reduce") || n.Contains("scan") => "reduction",
            var n when n.Contains("sort") => "sorting",
            var n when n.Contains("fft") => "transform",
            var n when n.Contains("conv") || n.Contains("filter") => "convolution",
            var n when n.Contains("copy") || n.Contains("memcpy") => "memory",
            _ => "general"
        };
    }

    /// <summary>
    /// Gets accelerator category from type.
    /// </summary>
    protected static string GetAcceleratorCategory(string acceleratorType)
    {
        return acceleratorType.ToLowerInvariant() switch
        {
            "cuda" or "nvidia" => "gpu",
            "metal" => "gpu",
            "opencl" => "gpu",
            "cpu" => "cpu",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Gets transfer type from direction.
    /// </summary>
    protected static string GetTransferType(string direction)
    {
        return direction.ToLowerInvariant() switch
        {
            "host_to_device" or "h2d" => "upload",
            "device_to_host" or "d2h" => "download",
            "device_to_device" or "d2d" => "peer",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Gets garbage collection type from generation.
    /// </summary>
    protected static string GetGCType(int generation)
    {
        return generation switch
        {
            0 => "gen0",
            1 => "gen1",
            2 => "gen2",
            -1 => "large_object_heap",
            _ => "unknown"
        };
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the telemetry provider and releases all resources.
    /// </summary>
    public virtual void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Log final metrics
            var perfMetrics = GetPerformanceMetrics();
            Logger.LogInformation(
                "Telemetry provider disposed - Operations: {Operations}, Errors: {Errors}, Overhead: {Overhead:F3}%",
                perfMetrics.TotalOperations, perfMetrics.TotalErrors, perfMetrics.OverheadPercentage);

            // Dispose OpenTelemetry resources
            ActivitySource?.Dispose();
            Meter?.Dispose();

            // Clear collections
            _counters.Clear();
            _histograms.Clear();
            _gauges.Clear();
            _observableGauges.Clear();
            _performanceMetrics.Clear();

            GC.SuppressFinalize(this);
        }
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Telemetry event type enumeration for pipeline events.
/// </summary>
public enum TelemetryEventType
{
    PipelineStarted,
    PipelineCompleted,
    PipelineStageStarted,
    PipelineStageCompleted,
    KernelExecuted,
    MemoryAllocated,
    CacheAccess,
    Error,
    Performance
}

/// <summary>
/// Represents a telemetry event with structured data.
/// Supports both generic event structure and pipeline-specific properties.
/// </summary>
public sealed class TelemetryEvent
{
    // Generic event properties (existing)
    public required string Name { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required Dictionary<string, object?> Attributes { get; init; }
    public required string Source { get; init; }

    // Pipeline-specific properties (new)
    public TelemetryEventType? EventType { get; init; }
    public string? PipelineId { get; init; }
    public string? StageId { get; init; }
    public string? CorrelationId { get; init; }
    public TimeSpan? Duration { get; init; }
    public bool? Success { get; init; }
    public long? ItemsProcessed { get; init; }
    public Exception? Exception { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Performance metrics for telemetry operations.
/// </summary>
public sealed class TelemetryPerformanceMetrics
{
    public long TotalOperations { get; init; }
    public long TotalErrors { get; init; }
    public long OverheadTicks { get; init; }
    public double OverheadPercentage { get; init; }
    public int EventQueueSize { get; init; }
    public int ActiveMetrics { get; init; }
    public string BackendType { get; init; } = string.Empty;
}

/// <summary>
/// Performance metric tracking for specific operations.
/// </summary>
internal sealed class PerformanceMetric
{
    public long Count { get; set; }
    public double TotalDuration { get; set; }
    public double MinDuration { get; set; } = double.MaxValue;
    public double MaxDuration { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public double AverageDuration => Count > 0 ? TotalDuration / Count : 0.0;
}

/// <summary>
/// Unified operation timer implementation.
/// </summary>
internal sealed class UnifiedOperationTimer : IOperationTimer
{
    private readonly string _operationName;
    private readonly IDictionary<string, object?>? _tags;
    private readonly BaseTelemetryProvider _telemetryProvider;
    private readonly Stopwatch _stopwatch;
    private readonly Activity? _activity;

    public UnifiedOperationTimer(string operationName, IDictionary<string, object?>? tags, BaseTelemetryProvider telemetryProvider)
    {
        _operationName = operationName;
        _tags = tags;
        _telemetryProvider = telemetryProvider;
        _stopwatch = Stopwatch.StartNew();
        _activity = telemetryProvider.StartActivity($"timer.{operationName}");

        if (_activity != null && tags != null)
        {
            foreach (var tag in tags)
            {
                _activity.SetTag(tag.Key, tag.Value?.ToString());
            }
        }
    }

    public bool IsEnabled => true;
    public TimeSpan MinimumDurationThreshold => TimeSpan.Zero;

    public event EventHandler<OperationTimingEventArgs>? OperationCompleted;

    public void Stop()
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();

            var tags = new Dictionary<string, object?> { ["operation"] = _operationName };
            if (_tags != null)
            {
                foreach (var tag in _tags)
                {
                    tags[tag.Key] = tag.Value;
                }
            }

            _telemetryProvider.RecordHistogram("operation.duration.ms", _stopwatch.Elapsed.TotalMilliseconds, tags);

            _activity?.SetTag("duration.ms", _stopwatch.Elapsed.TotalMilliseconds);
            _activity?.SetStatus(ActivityStatusCode.Ok);

            OperationCompleted?.Invoke(this, new OperationTimingEventArgs
            {
                OperationName = _operationName,
                OperationId = Guid.NewGuid().ToString(),
                Duration = _stopwatch.Elapsed,
                StartTime = DateTime.UtcNow.Subtract(_stopwatch.Elapsed),
                EndTime = DateTime.UtcNow,
                Metadata = _tags?.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value!) ?? []
            });
        }
    }

    public void Dispose()
    {
        if (_stopwatch.IsRunning)
        {
            Stop();
        }
        _activity?.Dispose();
    }

    // Additional interface methods with minimal implementations
    public ITimerHandle StartOperation(string operationName, string? operationId = null)
        => new SimpleTimerHandle(operationName, operationId ?? Guid.NewGuid().ToString());

    public IDisposable StartOperationScope(string operationName, string? operationId = null)
        => new SimpleTimerHandle(operationName, operationId ?? Guid.NewGuid().ToString());

    public (T result, TimeSpan duration) TimeOperation<T>(string operationName, Func<T> operation)
    {
        var sw = Stopwatch.StartNew();
        var result = operation();
        sw.Stop();
        return (result, sw.Elapsed);
    }

    public async Task<(T result, TimeSpan duration)> TimeOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        var sw = Stopwatch.StartNew();
        var result = await operation();
        sw.Stop();
        return (result, sw.Elapsed);
    }

    public TimeSpan TimeOperation(string operationName, Action operation)
    {
        var sw = Stopwatch.StartNew();
        operation();
        sw.Stop();
        return sw.Elapsed;
    }

    public async Task<TimeSpan> TimeOperationAsync(string operationName, Func<Task> operation)
    {
        var sw = Stopwatch.StartNew();
        await operation();
        sw.Stop();
        return sw.Elapsed;
    }

    public void RecordTiming(string operationName, TimeSpan duration, string? operationId = null, IDictionary<string, object>? metadata = null) { }
    public OperationStatistics? GetStatistics(string operationName) => null;
    public IDictionary<string, OperationStatistics> GetAllStatistics() => new Dictionary<string, OperationStatistics>();
    public void ClearStatistics(string operationName) { }
    public void ClearAllStatistics() { }
    public string ExportData(MetricsExportFormat format, Func<string, bool>? operationFilter = null) => string.Empty;
    public void SetEnabled(bool enabled) { }
    public void SetMinimumDurationThreshold(TimeSpan threshold) { }

    private sealed class SimpleTimerHandle : ITimerHandle, IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public string OperationName { get; }
        public string OperationId { get; }
        public DateTime StartTime { get; } = DateTime.UtcNow;
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public SimpleTimerHandle(string operationName, string operationId)
        {
            OperationName = operationName;
            OperationId = operationId;
        }

        public TimeSpan Stop(IDictionary<string, object>? metadata = null)
        {
            _stopwatch.Stop();
            return _stopwatch.Elapsed;
        }

        public TimeSpan AddCheckpoint(string checkpointName) => _stopwatch.Elapsed;
        public IDictionary<string, TimeSpan> GetCheckpoints() => new Dictionary<string, TimeSpan>();
        public void Dispose() => _stopwatch.Stop();
    }
}

/// <summary>
/// Null implementation for when telemetry is disabled.
/// </summary>
internal sealed class NullOperationTimer : IOperationTimer
{
    public bool IsEnabled => false;
    public TimeSpan MinimumDurationThreshold => TimeSpan.Zero;
#pragma warning disable CS0067 // Event is never used
    public event EventHandler<OperationTimingEventArgs>? OperationCompleted;
#pragma warning restore CS0067

    public static void Stop() { }
    public void Dispose() { }
    public ITimerHandle StartOperation(string operationName, string? operationId = null) => new NullTimerHandle();
    public IDisposable StartOperationScope(string operationName, string? operationId = null) => new NullTimerHandle();
    public (T result, TimeSpan duration) TimeOperation<T>(string operationName, Func<T> operation) => (operation(), TimeSpan.Zero);
    public async Task<(T result, TimeSpan duration)> TimeOperationAsync<T>(string operationName, Func<Task<T>> operation) => (await operation(), TimeSpan.Zero);
    public TimeSpan TimeOperation(string operationName, Action operation) { operation(); return TimeSpan.Zero; }
    public async Task<TimeSpan> TimeOperationAsync(string operationName, Func<Task> operation) { await operation(); return TimeSpan.Zero; }
    public void RecordTiming(string operationName, TimeSpan duration, string? operationId = null, IDictionary<string, object>? metadata = null) { }
    public OperationStatistics? GetStatistics(string operationName) => null;
    public IDictionary<string, OperationStatistics> GetAllStatistics() => new Dictionary<string, OperationStatistics>();
    public void ClearStatistics(string operationName) { }
    public void ClearAllStatistics() { }
    public string ExportData(MetricsExportFormat format, Func<string, bool>? operationFilter = null) => string.Empty;
    public void SetEnabled(bool enabled) { }
    public void SetMinimumDurationThreshold(TimeSpan threshold) { }

    private sealed class NullTimerHandle : ITimerHandle, IDisposable
    {
        public string OperationName => string.Empty;
        public string OperationId => string.Empty;
        public DateTime StartTime => DateTime.UtcNow;
        public TimeSpan Elapsed => TimeSpan.Zero;
        public TimeSpan Stop(IDictionary<string, object>? metadata = null) => TimeSpan.Zero;
        public TimeSpan AddCheckpoint(string checkpointName) => TimeSpan.Zero;
        public IDictionary<string, TimeSpan> GetCheckpoints() => new Dictionary<string, TimeSpan>();
        public void Dispose() { }
    }
}

#endregion