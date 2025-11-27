// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Observability;

/// <summary>
/// OpenTelemetry-compatible instrumentation implementation for Ring Kernels.
/// </summary>
/// <remarks>
/// Provides comprehensive observability for Ring Kernel operations including:
/// - Distributed tracing with W3C Trace Context propagation
/// - Metrics with Prometheus-compatible export
/// - Structured logging integration
/// </remarks>
public sealed partial class RingKernelInstrumentation : IRingKernelInstrumentation
{
    private readonly ILogger<RingKernelInstrumentation> _logger;
    private readonly RingKernelInstrumentationOptions _options;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private bool _disposed;

    // Counters
    private readonly Counter<long> _kernelLaunchesCounter;
    private readonly Counter<long> _kernelTerminationsCounter;
    private readonly Counter<long> _messagesProcessedCounter;
    private readonly Counter<long> _messagesDroppedCounter;
    private readonly Counter<long> _errorsCounter;

    // Gauges (using UpDownCounter for gauge-like behavior)
    private readonly UpDownCounter<long> _activeKernelsGauge;
    private readonly ConcurrentDictionary<string, int> _queueDepths = new();
    private readonly ConcurrentDictionary<string, double> _throughputs = new();
    private readonly ConcurrentDictionary<string, bool> _healthStatuses = new();

    // Histograms
    private readonly Histogram<double> _messageLatencyHistogram;

    // Event IDs: 9700-9749 for RingKernelInstrumentation
    [LoggerMessage(EventId = 9700, Level = LogLevel.Debug,
        Message = "Kernel {KernelId} launched on {Backend} with grid={GridSize} block={BlockSize}")]
    private static partial void LogKernelLaunched(
        ILogger logger, string kernelId, string backend, int gridSize, int blockSize);

    [LoggerMessage(EventId = 9701, Level = LogLevel.Debug,
        Message = "Kernel {KernelId} terminated after {Uptime} with {MessagesProcessed} messages: {Reason}")]
    private static partial void LogKernelTerminated(
        ILogger logger, string kernelId, TimeSpan uptime, long messagesProcessed, string reason);

    [LoggerMessage(EventId = 9702, Level = LogLevel.Warning,
        Message = "Message dropped for kernel {KernelId}: {Reason}")]
    private static partial void LogMessageDropped(
        ILogger logger, string kernelId, string reason);

    [LoggerMessage(EventId = 9703, Level = LogLevel.Error,
        Message = "Kernel {KernelId} error {ErrorCode}: {ErrorMessage}")]
    private static partial void LogKernelError(
        ILogger logger, string kernelId, int errorCode, string errorMessage);

    [LoggerMessage(EventId = 9704, Level = LogLevel.Warning,
        Message = "Kernel {KernelId} health status changed to {IsHealthy}")]
    private static partial void LogHealthStatusChanged(
        ILogger logger, string kernelId, bool isHealthy);

    /// <inheritdoc />
    public ActivitySource ActivitySource => _activitySource;

    /// <inheritdoc />
    public Meter Meter => _meter;

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled && !_disposed;

    /// <summary>
    /// Creates a new Ring Kernel instrumentation instance.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="options">Instrumentation configuration options.</param>
    public RingKernelInstrumentation(
        ILogger<RingKernelInstrumentation> logger,
        IOptions<RingKernelInstrumentationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new RingKernelInstrumentationOptions();

        // Create ActivitySource for distributed tracing
        _activitySource = new ActivitySource(
            _options.ServiceName,
            _options.ServiceVersion);

        // Create Meter for metrics
        _meter = new Meter(
            _options.ServiceName,
            _options.ServiceVersion);

        // Initialize counters
        _kernelLaunchesCounter = _meter.CreateCounter<long>(
            RingKernelSemanticConventions.MetricKernelLaunches,
            unit: "{kernels}",
            description: "Total number of Ring Kernel launches");

        _kernelTerminationsCounter = _meter.CreateCounter<long>(
            RingKernelSemanticConventions.MetricKernelTerminations,
            unit: "{kernels}",
            description: "Total number of Ring Kernel terminations");

        _messagesProcessedCounter = _meter.CreateCounter<long>(
            RingKernelSemanticConventions.MetricMessagesProcessed,
            unit: "{messages}",
            description: "Total messages processed by Ring Kernels");

        _messagesDroppedCounter = _meter.CreateCounter<long>(
            RingKernelSemanticConventions.MetricMessagesDropped,
            unit: "{messages}",
            description: "Total messages dropped by Ring Kernels");

        _errorsCounter = _meter.CreateCounter<long>(
            RingKernelSemanticConventions.MetricErrors,
            unit: "{errors}",
            description: "Total Ring Kernel errors");

        // Initialize gauges
        _activeKernelsGauge = _meter.CreateUpDownCounter<long>(
            RingKernelSemanticConventions.MetricActiveKernels,
            unit: "{kernels}",
            description: "Current number of active Ring Kernels");

        // Create observable gauges for queue depth, throughput, and health
        _ = _meter.CreateObservableGauge(
            RingKernelSemanticConventions.MetricQueueDepth,
            ObserveQueueDepths,
            unit: "{messages}",
            description: "Current Ring Kernel queue depth");

        _ = _meter.CreateObservableGauge(
            RingKernelSemanticConventions.MetricThroughput,
            ObserveThroughputs,
            unit: "{messages}/s",
            description: "Current Ring Kernel message throughput");

        _ = _meter.CreateObservableGauge(
            RingKernelSemanticConventions.MetricHealthStatus,
            ObserveHealthStatuses,
            unit: "{status}",
            description: "Ring Kernel health status (0=unhealthy, 1=healthy)");

        // Initialize histograms
        _messageLatencyHistogram = _meter.CreateHistogram<double>(
            RingKernelSemanticConventions.MetricMessageLatency,
            unit: "ms",
            description: "Ring Kernel message processing latency");
    }

    /// <inheritdoc />
    public Activity? StartKernelActivity(
        string kernelId,
        string operationName,
        ActivityKind kind = ActivityKind.Internal)
    {
        ThrowIfDisposed();

        if (!IsEnabled)
        {
            return null;
        }

        var activity = _activitySource.StartActivity(
            operationName,
            kind);

        if (activity != null)
        {
            activity.SetTag(RingKernelSemanticConventions.KernelId, kernelId);
            AddGlobalTags(activity);
        }

        return activity;
    }

    /// <inheritdoc />
    public Activity? StartMessageActivity(
        string kernelId,
        string messageType,
        string messageId,
        ActivityContext? parentContext = null)
    {
        ThrowIfDisposed();

        if (!IsEnabled || !_options.RecordMessageActivities)
        {
            return null;
        }

        Activity? activity;
        if (parentContext.HasValue)
        {
            activity = _activitySource.StartActivity(
                RingKernelSemanticConventions.SpanMessageProcess,
                ActivityKind.Consumer,
                parentContext.Value);
        }
        else
        {
            activity = _activitySource.StartActivity(
                RingKernelSemanticConventions.SpanMessageProcess,
                ActivityKind.Consumer);
        }

        if (activity != null)
        {
            activity.SetTag(RingKernelSemanticConventions.KernelId, kernelId);
            activity.SetTag(RingKernelSemanticConventions.MessageType, messageType);
            activity.SetTag(RingKernelSemanticConventions.MessageId, messageId);
            AddGlobalTags(activity);
        }

        return activity;
    }

    /// <inheritdoc />
    public void RecordKernelLaunch(
        string kernelId,
        int gridSize,
        int blockSize,
        string backend)
    {
        ThrowIfDisposed();

        if (!IsEnabled)
        {
            return;
        }

        var tags = new TagList
        {
            { RingKernelSemanticConventions.KernelId, kernelId },
            { RingKernelSemanticConventions.Backend, backend },
            { RingKernelSemanticConventions.GridSize, gridSize },
            { RingKernelSemanticConventions.BlockSize, blockSize }
        };

        _kernelLaunchesCounter.Add(1, tags);
        _activeKernelsGauge.Add(1, tags);
        _healthStatuses[kernelId] = true;

        LogKernelLaunched(_logger, kernelId, backend, gridSize, blockSize);
    }

    /// <inheritdoc />
    public void RecordKernelTermination(
        string kernelId,
        TimeSpan uptime,
        long messagesProcessed,
        string terminationReason)
    {
        ThrowIfDisposed();

        if (!IsEnabled)
        {
            return;
        }

        var tags = new TagList
        {
            { RingKernelSemanticConventions.KernelId, kernelId },
            { RingKernelSemanticConventions.TerminationReason, terminationReason }
        };

        _kernelTerminationsCounter.Add(1, tags);
        _activeKernelsGauge.Add(-1, new TagList { { RingKernelSemanticConventions.KernelId, kernelId } });

        // Clean up state
        _queueDepths.TryRemove(kernelId, out _);
        _throughputs.TryRemove(kernelId, out _);
        _healthStatuses.TryRemove(kernelId, out _);

        LogKernelTerminated(_logger, kernelId, uptime, messagesProcessed, terminationReason);
    }

    /// <inheritdoc />
    public void RecordThroughput(
        string kernelId,
        double messagesPerSecond,
        int queueDepth)
    {
        ThrowIfDisposed();

        if (!IsEnabled)
        {
            return;
        }

        _queueDepths[kernelId] = queueDepth;
        _throughputs[kernelId] = messagesPerSecond;
    }

    /// <inheritdoc />
    public void RecordMessageLatency(
        string kernelId,
        long latencyNanos)
    {
        ThrowIfDisposed();

        if (!IsEnabled || !_options.RecordLatencyHistograms)
        {
            return;
        }

        // Convert nanoseconds to milliseconds for the histogram
        var latencyMs = latencyNanos / 1_000_000.0;

        var tags = new TagList
        {
            { RingKernelSemanticConventions.KernelId, kernelId }
        };

        _messageLatencyHistogram.Record(latencyMs, tags);
        _messagesProcessedCounter.Add(1, tags);
    }

    /// <inheritdoc />
    public void RecordMessageDropped(
        string kernelId,
        string reason)
    {
        ThrowIfDisposed();

        if (!IsEnabled)
        {
            return;
        }

        var tags = new TagList
        {
            { RingKernelSemanticConventions.KernelId, kernelId },
            { RingKernelSemanticConventions.DropReason, reason }
        };

        _messagesDroppedCounter.Add(1, tags);
        LogMessageDropped(_logger, kernelId, reason);
    }

    /// <inheritdoc />
    public void RecordKernelError(
        string kernelId,
        int errorCode,
        string errorMessage)
    {
        ThrowIfDisposed();

        if (!IsEnabled)
        {
            return;
        }

        var tags = new TagList
        {
            { RingKernelSemanticConventions.KernelId, kernelId },
            { RingKernelSemanticConventions.ErrorCode, errorCode }
        };

        _errorsCounter.Add(1, tags);
        _healthStatuses[kernelId] = false;

        LogKernelError(_logger, kernelId, errorCode, errorMessage);
    }

    /// <inheritdoc />
    public void RecordHealthStatus(
        string kernelId,
        bool isHealthy,
        DateTimeOffset lastProcessedTimestamp)
    {
        ThrowIfDisposed();

        if (!IsEnabled)
        {
            return;
        }

        var previousHealth = _healthStatuses.GetValueOrDefault(kernelId, true);
        _healthStatuses[kernelId] = isHealthy;

        if (previousHealth != isHealthy)
        {
            LogHealthStatusChanged(_logger, kernelId, isHealthy);
        }
    }

    /// <inheritdoc />
    public void AddEvent(
        Activity? activity,
        string eventName,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        if (activity == null || !IsEnabled)
        {
            return;
        }

        if (attributes != null)
        {
            var tags = new ActivityTagsCollection(
                attributes.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value)));
            activity.AddEvent(new ActivityEvent(eventName, DateTimeOffset.UtcNow, tags));
        }
        else
        {
            activity.AddEvent(new ActivityEvent(eventName));
        }
    }

    /// <inheritdoc />
    public void SetActivityStatus(
        Activity? activity,
        bool success,
        string? description = null)
    {
        if (activity == null)
        {
            return;
        }

        activity.SetStatus(
            success ? ActivityStatusCode.Ok : ActivityStatusCode.Error,
            description);
    }

    private void AddGlobalTags(Activity activity)
    {
        foreach (var tag in _options.GlobalTags)
        {
            activity.SetTag(tag.Key, tag.Value);
        }
    }

    private IEnumerable<Measurement<int>> ObserveQueueDepths()
    {
        foreach (var kvp in _queueDepths)
        {
            yield return new Measurement<int>(
                kvp.Value,
                new KeyValuePair<string, object?>(RingKernelSemanticConventions.KernelId, kvp.Key));
        }
    }

    private IEnumerable<Measurement<double>> ObserveThroughputs()
    {
        foreach (var kvp in _throughputs)
        {
            yield return new Measurement<double>(
                kvp.Value,
                new KeyValuePair<string, object?>(RingKernelSemanticConventions.KernelId, kvp.Key));
        }
    }

    private IEnumerable<Measurement<int>> ObserveHealthStatuses()
    {
        foreach (var kvp in _healthStatuses)
        {
            yield return new Measurement<int>(
                kvp.Value ? 1 : 0,
                new KeyValuePair<string, object?>(RingKernelSemanticConventions.KernelId, kvp.Key));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _activitySource.Dispose();
        _meter.Dispose();

        _queueDepths.Clear();
        _throughputs.Clear();
        _healthStatuses.Clear();
    }
}

/// <summary>
/// Extension methods for registering Ring Kernel instrumentation.
/// </summary>
public static class RingKernelInstrumentationExtensions
{
    /// <summary>
    /// Creates a child activity for a kernel operation.
    /// </summary>
    /// <param name="instrumentation">The instrumentation instance.</param>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="operationName">Operation name.</param>
    /// <param name="parentActivity">Optional parent activity.</param>
    /// <returns>The started activity, or null if sampling is disabled.</returns>
    public static Activity? StartChildActivity(
        this IRingKernelInstrumentation instrumentation,
        string kernelId,
        string operationName,
        Activity? parentActivity)
    {
        if (parentActivity == null)
        {
            return instrumentation.StartKernelActivity(kernelId, operationName);
        }

        return instrumentation.ActivitySource.StartActivity(
            operationName,
            ActivityKind.Internal,
            parentActivity.Context);
    }

    /// <summary>
    /// Records a batch of message latencies.
    /// </summary>
    /// <param name="instrumentation">The instrumentation instance.</param>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="latenciesNanos">Collection of latencies in nanoseconds.</param>
    public static void RecordMessageLatencyBatch(
        this IRingKernelInstrumentation instrumentation,
        string kernelId,
        IEnumerable<long> latenciesNanos)
    {
        foreach (var latency in latenciesNanos)
        {
            instrumentation.RecordMessageLatency(kernelId, latency);
        }
    }

    /// <summary>
    /// Wraps an async operation with activity tracing.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="instrumentation">The instrumentation instance.</param>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="operationName">Operation name.</param>
    /// <param name="operation">The async operation to trace.</param>
    /// <returns>The operation result.</returns>
    public static async Task<T> TraceOperationAsync<T>(
        this IRingKernelInstrumentation instrumentation,
        string kernelId,
        string operationName,
        Func<Activity?, Task<T>> operation)
    {
        using var activity = instrumentation.StartKernelActivity(kernelId, operationName);

        try
        {
            var result = await operation(activity).ConfigureAwait(false);
            instrumentation.SetActivityStatus(activity, true);
            return result;
        }
        catch (Exception ex)
        {
            instrumentation.SetActivityStatus(activity, false, ex.Message);
            instrumentation.AddEvent(activity, "exception", new Dictionary<string, object?>
            {
                ["exception.type"] = ex.GetType().FullName,
                ["exception.message"] = ex.Message,
                ["exception.stacktrace"] = ex.StackTrace
            });
            throw;
        }
    }

    /// <summary>
    /// Wraps an async operation with activity tracing (no return value).
    /// </summary>
    /// <param name="instrumentation">The instrumentation instance.</param>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="operationName">Operation name.</param>
    /// <param name="operation">The async operation to trace.</param>
    public static async Task TraceOperationAsync(
        this IRingKernelInstrumentation instrumentation,
        string kernelId,
        string operationName,
        Func<Activity?, Task> operation)
    {
        using var activity = instrumentation.StartKernelActivity(kernelId, operationName);

        try
        {
            await operation(activity).ConfigureAwait(false);
            instrumentation.SetActivityStatus(activity, true);
        }
        catch (Exception ex)
        {
            instrumentation.SetActivityStatus(activity, false, ex.Message);
            instrumentation.AddEvent(activity, "exception", new Dictionary<string, object?>
            {
                ["exception.type"] = ex.GetType().FullName,
                ["exception.message"] = ex.Message,
                ["exception.stacktrace"] = ex.StackTrace
            });
            throw;
        }
    }
}
