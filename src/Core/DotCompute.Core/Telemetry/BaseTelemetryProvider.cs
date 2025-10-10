// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Interfaces.Telemetry;
using DotCompute.Abstractions.Telemetry;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Unified base telemetry provider that consolidates common telemetry patterns
/// across all backend implementations (Metal, CUDA, OpenCL, CPU, LINQ).
/// Eliminates over 2,500 lines of duplicate telemetry code.
/// </summary>
public abstract partial class BaseTelemetryProvider : ITelemetryProvider, IDisposable
{
    // LoggerMessage delegates - Event ID range 9200-9299 for BaseTelemetryProvider (Telemetry module)
    private static readonly Action<ILogger, string, string, Exception?> _logProviderInitialized =
        LoggerMessage.Define<string, string>(
            MsLogLevel.Debug,
            new EventId(9200, nameof(LogProviderInitialized)),
            "Base telemetry provider initialized for {ServiceName} v{ServiceVersion}");

    private static readonly Action<ILogger, Exception, string, string, Exception?> _logTelemetryError =
        LoggerMessage.Define<Exception, string, string>(
            MsLogLevel.Warning,
            new EventId(9201, nameof(LogTelemetryError)),
            "Telemetry error {Exception} in {Operation} for {Context}");

    private static readonly Action<ILogger, long, long, double, Exception?> _logProviderDisposed =
        LoggerMessage.Define<long, long, double>(
            MsLogLevel.Information,
            new EventId(9202, nameof(LogProviderDisposed)),
            "Telemetry provider disposed - Operations: {Operations}, Errors: {Errors}, Overhead: {Overhead:F3}%");

    // Wrapper methods
    private static void LogProviderInitialized(ILogger logger, string serviceName, string serviceVersion)
        => _logProviderInitialized(logger, serviceName, serviceVersion, null);

    private static void LogTelemetryError(ILogger logger, Exception ex, string operation, string context)
        => _logTelemetryError(logger, ex, operation, context, ex);

    private static void LogProviderDisposed(ILogger logger, long operations, long errors, double overhead)
        => _logProviderDisposed(logger, operations, errors, overhead, null);

#pragma warning disable CA1051 // Do not declare visible instance fields - Protected fields intended for use by derived classes
    protected readonly ILogger Logger;
    protected readonly TelemetryConfiguration Configuration;
    protected readonly Meter Meter;
    protected readonly ActivitySource ActivitySource;
#pragma warning restore CA1051

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

        LogProviderInitialized(Logger, serviceName, serviceVersion);
    }

    #region Core Telemetry Methods

    /// <summary>
    /// Records a metric value with comprehensive error handling and performance tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void RecordMetric(string name, double value, IDictionary<string, object?>? tags = null)
    {
        if (_disposed || !Configuration.Enabled)
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

            _ = Interlocked.Increment(ref _totalOperations);
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
        if (_disposed || !Configuration.Enabled)
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

            _ = Interlocked.Increment(ref _totalOperations);
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
        if (_disposed || !Configuration.Enabled)
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

            _ = Interlocked.Increment(ref _totalOperations);
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
        if (_disposed || !Configuration.Enabled)
        {
            return null;
        }


        try
        {
            var activity = ActivitySource.StartActivity(name, kind);

            if (activity != null)
            {
                // Add standard context
                _ = activity.SetTag("service.name", Configuration.ServiceName);
                _ = activity.SetTag("service.version", Configuration.ServiceVersion);
                _ = activity.SetTag("telemetry.provider", GetType().Name);

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
        if (_disposed || !Configuration.Enabled)
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
            // Note: Always process synchronously unless async processing is explicitly enabled via AdditionalOptions
            var enableAsync = Configuration.AdditionalOptions.TryGetValue("EnableAsyncProcessing", out var asyncValue)
                && asyncValue is bool enableAsyncBool && enableAsyncBool;

            if (enableAsync)
            {
                _eventQueue.Enqueue(telemetryEvent);
            }
            else
            {
                ProcessEventSync(telemetryEvent);
            }

            _ = Interlocked.Increment(ref _totalOperations);
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
        if (_disposed || !Configuration.Enabled)
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
        if (_disposed || !Configuration.Enabled)
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
        if (_disposed || !Configuration.Enabled)
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
        if (_disposed || !Configuration.Enabled)
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
        if (_disposed || !Configuration.Enabled)
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
        if (_disposed || !Configuration.Enabled)
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
    public virtual Meter GetMeter(string name, string? version = null) => new(name, version);

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
        _ = Interlocked.Increment(ref _totalErrors);

        // Log errors unless explicitly disabled via AdditionalOptions
        var suppressLogging = Configuration.AdditionalOptions.TryGetValue("SuppressTelemetryErrorLogging", out var suppressValue)
            && suppressValue is bool suppressBool && suppressBool;

        if (!suppressLogging)
        {
            LogTelemetryError(Logger, ex, operation, context);
        }
    }

    /// <summary>
    /// Updates telemetry overhead tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void UpdateTelemetryOverhead(long startTicks)
    {
        // Track overhead if explicitly enabled via AdditionalOptions
        var trackOverhead = Configuration.AdditionalOptions.TryGetValue("TrackOverhead", out var trackValue)
            && trackValue is bool trackBool && trackBool;

        if (trackOverhead)
        {
            var overheadTicks = Stopwatch.GetTimestamp() - startTicks;
            _ = Interlocked.Add(ref _telemetryOverheadTicks, overheadTicks);
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
            _ = activity.SetTag("event.name", telemetryEvent.Name);
            _ = activity.SetTag("event.timestamp", telemetryEvent.Timestamp.ToString("O", CultureInfo.InvariantCulture));
            _ = activity.SetTag("event.source", telemetryEvent.Source);

            foreach (var attr in telemetryEvent.Attributes)
            {
                _ = activity.SetTag($"event.{attr.Key}", attr.Value?.ToString());
            }
        }
    }

    /// <summary>
    /// Calculates telemetry overhead percentage.
    /// </summary>
    protected virtual double CalculateOverheadPercentage()
    {
        // Check if overhead tracking is enabled via AdditionalOptions
        var trackOverhead = Configuration.AdditionalOptions.TryGetValue("TrackOverhead", out var trackValue)
            && trackValue is bool trackBool && trackBool;

        if (!trackOverhead)
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
    protected void InitializeStandardMetrics()
    {
        // Pre-create commonly used metrics to reduce creation overhead
        _ = _counters.TryAdd("operation.timer.started",
            Meter.CreateCounter<long>("operation.timer.started", "count", "Operation timers started"));
        _ = _counters.TryAdd("memory.allocation.count",
            Meter.CreateCounter<long>("memory.allocation.count", "count", "Memory allocations"));
        _ = _counters.TryAdd("kernel.execution.count",
            Meter.CreateCounter<long>("kernel.execution.count", "count", "Kernel executions"));
        _ = _counters.TryAdd("memory.transfer.count",
            Meter.CreateCounter<long>("memory.transfer.count", "count", "Memory transfers"));

        _ = _histograms.TryAdd("memory.allocation.bytes",
            Meter.CreateHistogram<double>("memory.allocation.bytes", "bytes", "Memory allocation size"));
        _ = _histograms.TryAdd("kernel.execution.duration.ms",
            Meter.CreateHistogram<double>("kernel.execution.duration.ms", "ms", "Kernel execution duration"));
        _ = _histograms.TryAdd("memory.transfer.bytes",
            Meter.CreateHistogram<double>("memory.transfer.bytes", "bytes", "Memory transfer size"));
    }

    /// <summary>
    /// Extracts kernel category from name for classification.
    /// </summary>
    protected static string ExtractKernelCategory(string kernelName)
    {
        var name = kernelName.ToUpper(CultureInfo.InvariantCulture);
        return name switch
        {
            var n when n.Contains("add", StringComparison.OrdinalIgnoreCase) || n.Contains("sum", StringComparison.OrdinalIgnoreCase) => "arithmetic",
            var n when n.Contains("mul", StringComparison.Ordinal) || n.Contains("multiply", StringComparison.Ordinal) => "arithmetic",
            var n when n.Contains("matrix", StringComparison.Ordinal) || n.Contains("gemm", StringComparison.Ordinal) => "linear_algebra",
            var n when n.Contains("reduce", StringComparison.Ordinal) || n.Contains("scan", StringComparison.Ordinal) => "reduction",
            var n when n.Contains("sort", StringComparison.Ordinal) => "sorting",
            var n when n.Contains("fft", StringComparison.Ordinal) => "transform",
            var n when n.Contains("conv", StringComparison.Ordinal) || n.Contains("filter", StringComparison.Ordinal) => "convolution",
            var n when n.Contains("copy", StringComparison.Ordinal) || n.Contains("memcpy", StringComparison.Ordinal) => "memory",
            _ => "general"
        };
    }

    /// <summary>
    /// Gets accelerator category from type.
    /// </summary>
    protected static string GetAcceleratorCategory(string acceleratorType)
    {
        return acceleratorType.ToUpper(CultureInfo.InvariantCulture) switch
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
        return direction.ToUpper(CultureInfo.InvariantCulture) switch
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
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Log final metrics
            var perfMetrics = GetPerformanceMetrics();
            LogProviderDisposed(Logger, perfMetrics.TotalOperations, perfMetrics.TotalErrors, perfMetrics.OverheadPercentage);

            // Dispose OpenTelemetry resources
            ActivitySource?.Dispose();
            Meter?.Dispose();

            // Clear collections
            _counters.Clear();
            _histograms.Clear();
            _gauges.Clear();
            _observableGauges.Clear();
            _performanceMetrics.Clear();
        }

        _disposed = true;
    }

    #endregion
}
