// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Comprehensive logging and telemetry integration for Metal execution components.
/// Provides structured logging, performance tracking, and diagnostic information.
/// </summary>
public static partial class MetalExecutionLogger
{
    #region Command Stream Logging

    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Information,
        Message = "Metal Command Stream initialized for {Architecture}: device={Device}, optimal_streams={OptimalStreams}, max_concurrent={MaxConcurrent}")]
    public static partial void CommandStreamInitialized(this ILogger logger, string architecture, IntPtr device, int optimalStreams, int maxConcurrent);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Debug,
        Message = "Created Metal stream {StreamId} with priority={Priority}, flags={Flags}")]
    public static partial void StreamCreated(this ILogger logger, string streamId, MetalStreamPriority priority, MetalStreamFlags flags);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Debug,
        Message = "Created optimized stream group '{GroupName}' with {StreamCount} streams")]
    public static partial void StreamGroupCreated(this ILogger logger, string groupName, int streamCount);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Trace,
        Message = "Executed command '{Operation}' on stream {StreamId} in {Duration:F3}ms")]
    public static partial void CommandExecuted(this ILogger logger, string operation, string streamId, double duration);

    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Debug,
        Message = "Completed execution graph with {NodeCount} nodes in {LevelCount} levels")]
    public static partial void ExecutionGraphCompleted(this ILogger logger, int nodeCount, int levelCount);

    [LoggerMessage(
        EventId = 5005,
        Level = LogLevel.Trace,
        Message = "Cleaned up {Count} idle streams")]
    public static partial void IdleStreamsCleanedUp(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 5006,
        Level = LogLevel.Information,
        Message = "Metal Command Stream disposed: created {TotalStreams} streams, executed {TotalCommands} commands")]
    public static partial void CommandStreamDisposed(this ILogger logger, long totalStreams, long totalCommands);

    #endregion

    #region Event Manager Logging

    [LoggerMessage(
        EventId = 5100,
        Level = LogLevel.Information,
        Message = "Metal Event Manager initialized: max_concurrent_events={MaxEvents}, timing_pool={TimingPool}, sync_pool={SyncPool}")]
    public static partial void EventManagerInitialized(this ILogger logger, int maxEvents, int timingPool, int syncPool);

    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Trace,
        Message = "Created timing event {EventId} (handle={Handle})")]
    public static partial void TimingEventCreated(this ILogger logger, string eventId, IntPtr handle);

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Trace,
        Message = "Created sync event {EventId} (handle={Handle})")]
    public static partial void SyncEventCreated(this ILogger logger, string eventId, IntPtr handle);

    [LoggerMessage(
        EventId = 5103,
        Level = LogLevel.Debug,
        Message = "Created timing pair: start={StartEventId}, end={EndEventId}")]
    public static partial void TimingPairCreated(this ILogger logger, string startEventId, string endEventId);

    [LoggerMessage(
        EventId = 5104,
        Level = LogLevel.Trace,
        Message = "Recorded event {EventId} on command queue {CommandQueue}")]
    public static partial void EventRecorded(this ILogger logger, string eventId, IntPtr commandQueue);

    [LoggerMessage(
        EventId = 5105,
        Level = LogLevel.Trace,
        Message = "Elapsed time between events {StartEvent} and {EndEvent}: {Time:F3}ms")]
    public static partial void ElapsedTimeMeasured(this ILogger logger, string startEvent, string endEvent, double time);

    [LoggerMessage(
        EventId = 5106,
        Level = LogLevel.Debug,
        Message = "Measured operation '{Operation}': GPU={GpuTime:F3}ms, CPU={CpuTime:F3}ms, Overhead={Overhead:F3}ms")]
    public static partial void OperationMeasured(this ILogger logger, string operation, double gpuTime, double cpuTime, double overhead);

    [LoggerMessage(
        EventId = 5107,
        Level = LogLevel.Information,
        Message = "Completed Metal profiling session {SessionId}: avg={AvgGpu:F3}ms, min={MinGpu:F3}ms, max={MaxGpu:F3}ms, p95={P95:F3}ms, throughput={Throughput:F1}ops/s")]
    public static partial void ProfilingSessionCompleted(this ILogger logger, string sessionId, double avgGpu, double minGpu, double maxGpu, double p95, double throughput);

    [LoggerMessage(
        EventId = 5108,
        Level = LogLevel.Warning,
        Message = "High event usage: {ActiveEvents}/{MaxEvents} active events")]
    public static partial void HighEventUsage(this ILogger logger, int activeEvents, int maxEvents);

    [LoggerMessage(
        EventId = 5109,
        Level = LogLevel.Information,
        Message = "Metal Event Manager disposed: created {TotalEvents} events")]
    public static partial void EventManagerDisposed(this ILogger logger, long totalEvents);

    #endregion

    #region Error Handler Logging

    [LoggerMessage(
        EventId = 5200,
        Level = LogLevel.Information,
        Message = "Metal Error Handler initialized for {Architecture}")]
    public static partial void ErrorHandlerInitialized(this ILogger logger, string architecture);

    [LoggerMessage(
        EventId = 5201,
        Level = LogLevel.Warning,
        Message = "Retry {RetryCount}/{MaxRetries} after {Delay:F0}ms for Metal error: {Error}")]
    public static partial void ErrorRetryAttempt(this ILogger logger, int retryCount, int maxRetries, double delay, MetalError error);

    [LoggerMessage(
        EventId = 5202,
        Level = LogLevel.Warning,
        Message = "Memory allocation retry {RetryCount}, attempting cleanup...")]
    public static partial void MemoryRetryAttempt(this ILogger logger, int retryCount);

    [LoggerMessage(
        EventId = 5203,
        Level = LogLevel.Error,
        Message = "Circuit breaker opened for {Duration:F0}s due to repeated Metal failures")]
    public static partial void CircuitBreakerOpened(this ILogger logger, double duration);

    [LoggerMessage(
        EventId = 5204,
        Level = LogLevel.Information,
        Message = "Circuit breaker reset, Metal GPU operations resuming")]
    public static partial void CircuitBreakerReset(this ILogger logger);

    [LoggerMessage(
        EventId = 5205,
        Level = LogLevel.Warning,
        Message = "Attempting memory error recovery for Metal operation {OperationName}")]
    public static partial void MemoryErrorRecovery(this ILogger logger, string operationName);

    [LoggerMessage(
        EventId = 5206,
        Level = LogLevel.Warning,
        Message = "Attempting device error recovery for Metal operation {OperationName}")]
    public static partial void DeviceErrorRecovery(this ILogger logger, string operationName);

    [LoggerMessage(
        EventId = 5207,
        Level = LogLevel.Information,
        Message = "Falling back to CPU for Metal operation {OperationName}")]
    public static partial void CpuFallback(this ILogger logger, string operationName);

    [LoggerMessage(
        EventId = 5208,
        Level = LogLevel.Information,
        Message = "Performing unified memory cleanup on Apple Silicon")]
    public static partial void UnifiedMemoryCleanup(this ILogger logger);

    [LoggerMessage(
        EventId = 5209,
        Level = LogLevel.Information,
        Message = "Performing discrete GPU memory cleanup on Intel Mac")]
    public static partial void DiscreteMemoryCleanup(this ILogger logger);

    [LoggerMessage(
        EventId = 5210,
        Level = LogLevel.Debug,
        Message = "Metal operation {OperationName} completed in {ElapsedMs}ms")]
    public static partial void OperationCompleted(this ILogger logger, string operationName, long elapsedMs);

    #endregion

    #region Execution Context Logging

    [LoggerMessage(
        EventId = 5300,
        Level = LogLevel.Information,
        Message = "Metal Execution Context initialized for {Architecture}: device={Device}")]
    public static partial void ExecutionContextInitialized(this ILogger logger, string architecture, IntPtr device);

    [LoggerMessage(
        EventId = 5301,
        Level = LogLevel.Debug,
        Message = "Metal operation {OperationId} completed successfully in {Duration:F3}ms")]
    public static partial void ExecutionOperationCompleted(this ILogger logger, string operationId, double duration);

    [LoggerMessage(
        EventId = 5302,
        Level = LogLevel.Error,
        Message = "Metal operation {OperationId} failed after {Duration:F3}ms")]
    public static partial void ExecutionOperationFailed(this ILogger logger, string operationId, double duration);

    [LoggerMessage(
        EventId = 5303,
        Level = LogLevel.Trace,
        Message = "Tracking Metal resource {ResourceId} of type {Type}, size {Size} bytes")]
    public static partial void ResourceTracked(this ILogger logger, string resourceId, MetalResourceType type, long size);

    [LoggerMessage(
        EventId = 5304,
        Level = LogLevel.Trace,
        Message = "Untracked Metal resource {ResourceId}")]
    public static partial void ResourceUntracked(this ILogger logger, string resourceId);

    [LoggerMessage(
        EventId = 5305,
        Level = LogLevel.Information,
        Message = "Metal execution context paused")]
    public static partial void ExecutionPaused(this ILogger logger);

    [LoggerMessage(
        EventId = 5306,
        Level = LogLevel.Information,
        Message = "Metal execution context resumed")]
    public static partial void ExecutionResumed(this ILogger logger);

    [LoggerMessage(
        EventId = 5307,
        Level = LogLevel.Debug,
        Message = "Metal Execution Context: {Operations} ops executed, {Resources} resources tracked, {ActiveOps} active operations, success rate {SuccessRate:P2}")]
    public static partial void ExecutionContextStats(this ILogger logger, long operations, long resources, int activeOps, double successRate);

    [LoggerMessage(
        EventId = 5308,
        Level = LogLevel.Information,
        Message = "Metal Execution Context disposed: executed {Operations} operations, tracked {Resources} resources, uptime {Uptime}")]
    public static partial void ExecutionContextDisposed(this ILogger logger, long operations, long resources, TimeSpan uptime);

    #endregion

    #region Event Pool Logging

    [LoggerMessage(
        EventId = 5400,
        Level = LogLevel.Information,
        Message = "Metal Event Pool initialized: timing pool={TimingSize}, sync pool={SyncSize}")]
    public static partial void EventPoolInitialized(this ILogger logger, int timingSize, int syncSize);

    [LoggerMessage(
        EventId = 5401,
        Level = LogLevel.Trace,
        Message = "Reused timing event from pool: {EventHandle}")]
    public static partial void TimingEventReused(this ILogger logger, IntPtr eventHandle);

    [LoggerMessage(
        EventId = 5402,
        Level = LogLevel.Trace,
        Message = "Created new timing event: {EventHandle}")]
    public static partial void TimingEventCreatedNew(this ILogger logger, IntPtr eventHandle);

    [LoggerMessage(
        EventId = 5403,
        Level = LogLevel.Trace,
        Message = "Returned timing event to pool: {EventHandle}")]
    public static partial void TimingEventReturned(this ILogger logger, IntPtr eventHandle);

    [LoggerMessage(
        EventId = 5404,
        Level = LogLevel.Debug,
        Message = "Pool maintenance cleaned {TotalCleaned} events (timing: {TimingCleaned}, sync: {SyncCleaned})")]
    public static partial void PoolMaintenanceCleaned(this ILogger logger, int totalCleaned, int timingCleaned, int syncCleaned);

    [LoggerMessage(
        EventId = 5405,
        Level = LogLevel.Information,
        Message = "Metal Event Pool disposed: created={Created}, reused={Reused}, reuse ratio={ReuseRatio:P2}")]
    public static partial void EventPoolDisposed(this ILogger logger, int created, int reused, double reuseRatio);

    #endregion

    #region Command Encoder Logging

    [LoggerMessage(
        EventId = 5500,
        Level = LogLevel.Trace,
        Message = "Created Metal command encoder for command buffer {CommandBuffer}")]
    public static partial void CommandEncoderCreated(this ILogger logger, IntPtr commandBuffer);

    [LoggerMessage(
        EventId = 5501,
        Level = LogLevel.Trace,
        Message = "Set compute pipeline state {PipelineState} on encoder {Encoder}")]
    public static partial void PipelineStateSet(this ILogger logger, IntPtr pipelineState, IntPtr encoder);

    [LoggerMessage(
        EventId = 5502,
        Level = LogLevel.Trace,
        Message = "Bound buffer {Buffer} at offset {Offset} to index {Index} on encoder {Encoder}")]
    public static partial void BufferBound(this ILogger logger, IntPtr buffer, nuint offset, int index, IntPtr encoder);

    [LoggerMessage(
        EventId = 5503,
        Level = LogLevel.Trace,
        Message = "Set {Size} bytes of constant data at index {Index} on encoder {Encoder}")]
    public static partial void ConstantDataSet(this ILogger logger, nuint size, int index, IntPtr encoder);

    [LoggerMessage(
        EventId = 5504,
        Level = LogLevel.Debug,
        Message = "Dispatched threadgroups: grid=({GridW}, {GridH}, {GridD}), threadgroup=({TgW}, {TgH}, {TgD})")]
    public static partial void ThreadgroupsDispatched(this ILogger logger, int gridW, int gridH, int gridD, int tgW, int tgH, int tgD);

    [LoggerMessage(
        EventId = 5505,
        Level = LogLevel.Trace,
        Message = "Ended encoding for encoder {Encoder} with {CommandCount} commands")]
    public static partial void EncodingEnded(this ILogger logger, IntPtr encoder, int commandCount);

    [LoggerMessage(
        EventId = 5506,
        Level = LogLevel.Trace,
        Message = "Disposed Metal command encoder: {CommandCount} commands encoded, duration: {Duration:F3}ms")]
    public static partial void CommandEncoderDisposed(this ILogger logger, int commandCount, double duration);

    #endregion

    #region Performance and Diagnostics

    [LoggerMessage(
        EventId = 5600,
        Level = LogLevel.Information,
        Message = "Metal performance metrics - Operations: {Operations}, Success Rate: {SuccessRate:P2}, Avg Duration: {AvgDuration:F3}ms")]
    public static partial void PerformanceMetrics(this ILogger logger, long operations, double successRate, double avgDuration);

    [LoggerMessage(
        EventId = 5601,
        Level = LogLevel.Information,
        Message = "Metal resource usage - Buffers: {Buffers}, Textures: {Textures}, Pipeline States: {Pipelines}, Total Memory: {Memory:N0} MB")]
    public static partial void ResourceUsage(this ILogger logger, int buffers, int textures, int pipelines, double memory);

    [LoggerMessage(
        EventId = 5602,
        Level = LogLevel.Warning,
        Message = "Metal health check failed: {IssueCount} issues detected")]
    public static partial void HealthCheckFailed(this ILogger logger, int issueCount);

    [LoggerMessage(
        EventId = 5603,
        Level = LogLevel.Information,
        Message = "Metal diagnostic report generated: Health={Health}, Architecture={Architecture}, Active Operations={ActiveOps}")]
    public static partial void DiagnosticReportGenerated(this ILogger logger, MetalExecutionHealth health, string architecture, int activeOps);

    #endregion
}

/// <summary>
/// Telemetry collector for Metal execution components
/// </summary>
public sealed class MetalExecutionTelemetry : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, object> _metrics;
    private readonly ConcurrentQueue<MetalTelemetryEvent> _events;
    private readonly Timer? _reportingTimer;
    private volatile bool _disposed;

    private const int MAX_EVENTS = 1000;

    public MetalExecutionTelemetry(ILogger logger, TimeSpan? reportingInterval = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _metrics = new ConcurrentDictionary<string, object>();
        _events = new ConcurrentQueue<MetalTelemetryEvent>();

        if (reportingInterval.HasValue)
        {
            _reportingTimer = new Timer(GeneratePeriodicReport, null, reportingInterval.Value, reportingInterval.Value);
        }

        _logger.LogInformation("Metal execution telemetry initialized with reporting interval: {Interval}",
            reportingInterval?.ToString() ?? "disabled");
    }

    /// <summary>
    /// Records a telemetry event
    /// </summary>
    public void RecordEvent(string category, string eventName, Dictionary<string, object>? properties = null)
    {
        if (_disposed)
        {
            return;
        }


        var telemetryEvent = new MetalTelemetryEvent
        {
            Category = category,
            EventName = eventName,
            Properties = properties ?? [],
            Timestamp = DateTimeOffset.UtcNow
        };

        _events.Enqueue(telemetryEvent);

        // Maintain event queue size
        while (_events.Count > MAX_EVENTS)
        {
            _ = _events.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Records a performance metric
    /// </summary>
    public void RecordMetric(string name, object value)
    {
        if (_disposed)
        {
            return;
        }


        _ = _metrics.AddOrUpdate(name, value, (_, _) => value);
    }

    /// <summary>
    /// Records timing information
    /// </summary>
    public void RecordTiming(string operation, TimeSpan duration, bool success = true)
    {
        if (_disposed)
        {
            return;
        }


        RecordEvent("Performance", "OperationTiming", new Dictionary<string, object>
        {
            ["Operation"] = operation,
            ["Duration"] = duration.TotalMilliseconds,
            ["Success"] = success
        });

        RecordMetric($"timing_{operation}_last", duration.TotalMilliseconds);
        RecordMetric($"timing_{operation}_success", success);
    }

    /// <summary>
    /// Records resource allocation information
    /// </summary>
    public void RecordResourceAllocation(MetalResourceType type, long sizeInBytes, bool success = true)
    {
        if (_disposed)
        {
            return;
        }


        RecordEvent("Resource", "Allocation", new Dictionary<string, object>
        {
            ["Type"] = type.ToString(),
            ["Size"] = sizeInBytes,
            ["Success"] = success
        });

        var key = $"resource_{type}_allocated";
        RecordMetric(key, _metrics.TryGetValue(key, out var existing) ? (long)existing + sizeInBytes : sizeInBytes);
    }

    /// <summary>
    /// Records error information
    /// </summary>
    public void RecordError(string component, MetalError errorCode, string? message = null)
    {
        if (_disposed)
        {
            return;
        }


        RecordEvent("Error", "ErrorOccurred", new Dictionary<string, object>
        {
            ["Component"] = component,
            ["ErrorCode"] = errorCode.ToString(),
            ["Message"] = message ?? string.Empty
        });

        var errorKey = $"error_{component}_{errorCode}";
        RecordMetric(errorKey, _metrics.TryGetValue(errorKey, out var existing) ? (int)existing + 1 : 1);
    }

    /// <summary>
    /// Gets current metrics snapshot
    /// </summary>
    public Dictionary<string, object> GetMetrics() => _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    /// <summary>
    /// Gets recent events
    /// </summary>
    public List<MetalTelemetryEvent> GetRecentEvents(int count = 100) => [.. _events.ToArray().TakeLast(count)];

    /// <summary>
    /// Generates a comprehensive telemetry report
    /// </summary>
    public MetalTelemetryReport GenerateReport()
    {
        var events = _events.ToArray();
        var metrics = GetMetrics();

        return new MetalTelemetryReport
        {
            Timestamp = DateTimeOffset.UtcNow,
            TotalEvents = events.Length,
            Metrics = metrics,
            RecentEvents = [.. events.TakeLast(50)],
            EventSummary = events
                .GroupBy(e => $"{e.Category}.{e.EventName}")
                .ToDictionary(g => g.Key, g => g.Count()),
            MetricSummary = GenerateMetricSummary(metrics)
        };
    }

    private void GeneratePeriodicReport(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var report = GenerateReport();
            _logger.LogInformation("Metal telemetry report: {TotalEvents} events, {MetricCount} metrics tracked",
                report.TotalEvents, report.Metrics.Count);

            // Log top events
            var topEvents = report.EventSummary
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .ToList();

            if (topEvents.Count > 0)
            {
                _logger.LogDebug("Top events: {Events}",
                    string.Join(", ", topEvents.Select(kvp => $"{kvp.Key}({kvp.Value})")));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generating periodic telemetry report");
        }
    }

    private static Dictionary<string, object> GenerateMetricSummary(Dictionary<string, object> metrics)
    {
        var summary = new Dictionary<string, object>();

        // Count different metric types
        var timingMetrics = metrics.Keys.Count(k => k.StartsWith("timing_", StringComparison.Ordinal));
        var resourceMetrics = metrics.Keys.Count(k => k.StartsWith("resource_", StringComparison.Ordinal));
        var errorMetrics = metrics.Keys.Count(k => k.StartsWith("error_", StringComparison.Ordinal));

        summary["TimingMetrics"] = timingMetrics;
        summary["ResourceMetrics"] = resourceMetrics;
        summary["ErrorMetrics"] = errorMetrics;

        return summary;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _reportingTimer?.Dispose();

            var finalReport = GenerateReport();
            _logger.LogInformation("Metal execution telemetry disposed: {TotalEvents} total events, {MetricCount} metrics",
                finalReport.TotalEvents, finalReport.Metrics.Count);
        }
    }
}

#region Telemetry Types

/// <summary>
/// Represents a telemetry event
/// </summary>
public sealed class MetalTelemetryEvent
{
    public string Category { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; } = [];
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Comprehensive telemetry report
/// </summary>
public sealed class MetalTelemetryReport
{
    public DateTimeOffset Timestamp { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<string, object> Metrics { get; } = [];
    public IList<MetalTelemetryEvent> RecentEvents { get; } = [];
    public Dictionary<string, int> EventSummary { get; } = [];
    public Dictionary<string, object> MetricSummary { get; } = [];
}


#endregion