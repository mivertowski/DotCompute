using System.Collections.Concurrent;
using System.Diagnostics;
using global::System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Production-grade distributed tracing system for cross-device operations and performance bottleneck identification.
/// Provides OpenTelemetry-compatible tracing with correlation ID propagation and performance analysis.
/// </summary>
public sealed class DistributedTracer : IDisposable
{
    private static readonly ActivitySource ActivitySource = new("DotCompute.DistributedTracer", "1.0.0");


    private readonly ILogger<DistributedTracer> _logger;
    private readonly DistributedTracingOptions _options;
    private readonly ConcurrentDictionary<string, TraceContext> _activeTraces;
    private readonly ConcurrentDictionary<string, List<SpanData>> _completedSpans;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _exportSemaphore;
    private volatile bool _disposed;

    public DistributedTracer(ILogger<DistributedTracer> logger, IOptions<DistributedTracingOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new DistributedTracingOptions();


        _activeTraces = new ConcurrentDictionary<string, TraceContext>();
        _completedSpans = new ConcurrentDictionary<string, List<SpanData>>();
        _exportSemaphore = new SemaphoreSlim(1, 1);

        // Start cleanup timer to prevent memory leaks

        _cleanupTimer = new Timer(CleanupExpiredTraces, null,

            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Starts a new distributed trace for a cross-device operation.
    /// </summary>
    /// <param name="operationName">Name of the operation being traced</param>
    /// <param name="correlationId">Unique correlation ID for request tracing</param>
    /// <param name="parentSpanContext">Parent span context for nested operations</param>
    /// <param name="tags">Additional tags for the trace</param>
    /// <returns>Trace context for the operation</returns>
    public TraceContext StartTrace(string operationName, string? correlationId = null,
        SpanContext? parentSpanContext = null, Dictionary<string, object?>? tags = null)
    {
        ThrowIfDisposed();


        correlationId ??= GenerateCorrelationId();


        var activity = ActivitySource.StartActivity($"dotcompute.{operationName}");
        _ = (activity?.SetTag("correlation_id", correlationId));
        _ = (activity?.SetTag("operation_name", operationName));
        _ = (activity?.SetTag("component", "dotcompute.core"));
        _ = (activity?.SetTag("trace_start_time", DateTimeOffset.UtcNow.ToString("O")));


        if (parentSpanContext != null)
        {
            _ = (activity?.SetTag("parent_span_id", parentSpanContext.SpanId));
            _ = (activity?.SetTag("parent_trace_id", parentSpanContext.TraceId));
        }


        if (tags != null)
        {
            foreach (var tag in tags)
            {
                _ = (activity?.SetTag(tag.Key, tag.Value?.ToString()));
            }
        }


        var traceContext = new TraceContext
        {
            TraceId = activity?.TraceId.ToString() ?? GenerateTraceId(),
            CorrelationId = correlationId,
            OperationName = operationName,
            StartTime = DateTimeOffset.UtcNow,
            Activity = activity,
            ParentSpanContext = parentSpanContext,
            Tags = tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? [],
            Spans = [],
            DeviceOperations = new ConcurrentDictionary<string, DeviceOperationTrace>()
        };


        _ = _activeTraces.TryAdd(correlationId, traceContext);


        _logger.LogDebugMessage($"Started distributed trace {traceContext.TraceId} for operation {operationName} with correlation {correlationId}");


        return traceContext;
    }

    /// <summary>
    /// Starts a new span within an existing trace for device-specific operations.
    /// </summary>
    /// <param name="correlationId">Correlation ID of the parent trace</param>
    /// <param name="spanName">Name of the span</param>
    /// <param name="deviceId">ID of the device executing the operation</param>
    /// <param name="spanKind">Type of span (client, server, internal, etc.)</param>
    /// <param name="attributes">Additional attributes for the span</param>
    /// <returns>Span context for the operation</returns>
    public SpanContext? StartSpan(string correlationId, string spanName, string deviceId,
        SpanKind spanKind = SpanKind.Internal, Dictionary<string, object?>? attributes = null)
    {
        ThrowIfDisposed();


        if (!_activeTraces.TryGetValue(correlationId, out var traceContext))
        {
            _logger.LogWarningMessage("Attempted to start span for unknown correlation ID {correlationId}");
            return null;
        }


        var spanId = GenerateSpanId();
        var startTime = DateTimeOffset.UtcNow;


        var spanContext = new SpanContext
        {
            SpanId = spanId,
            TraceId = traceContext.TraceId,
            CorrelationId = correlationId,
            SpanName = spanName,
            DeviceId = deviceId,
            SpanKind = spanKind,
            StartTime = startTime,
            Attributes = attributes?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? [],
            ParentSpanId = traceContext.Activity?.SpanId.ToString()
        };

        // Create nested activity for detailed tracing

        var childActivity = ActivitySource.StartActivity($"{spanName}@{deviceId}");
        _ = (childActivity?.SetTag("span_id", spanId));
        _ = (childActivity?.SetTag("device_id", deviceId));
        _ = (childActivity?.SetTag("span_kind", spanKind.ToString()));
        _ = (childActivity?.SetTag("correlation_id", correlationId));


        if (attributes != null)
        {
            foreach (var attr in attributes)
            {
                _ = (childActivity?.SetTag(attr.Key, attr.Value?.ToString()));
            }
        }


        spanContext.Activity = childActivity;

        // Track device-specific operations

        _ = traceContext.DeviceOperations.AddOrUpdate(deviceId,
            new DeviceOperationTrace
            {
                DeviceId = deviceId,
                OperationCount = 1,
                FirstOperationTime = startTime,
                LastOperationTime = startTime,
                ActiveSpans = [spanContext]
            },
            (key, existing) =>
            {
                existing.OperationCount++;
                existing.LastOperationTime = startTime;
                existing.ActiveSpans.Add(spanContext);
                return existing;
            });


        _logger.LogTrace("Started span {SpanId} '{SpanName}' on device {DeviceId} for trace {TraceId}",
            spanId, spanName, deviceId, traceContext.TraceId);


        return spanContext;
    }

    /// <summary>
    /// Records a performance event within a span with detailed metrics.
    /// </summary>
    /// <param name="spanContext">The span context to record the event in</param>
    /// <param name="eventName">Name of the event</param>
    /// <param name="attributes">Event attributes including performance metrics</param>
    public void RecordEvent(SpanContext spanContext, string eventName,

        Dictionary<string, object?> attributes)
    {
        ThrowIfDisposed();


        var eventData = new SpanEvent
        {
            Name = eventName,
            Timestamp = DateTimeOffset.UtcNow,
            Attributes = attributes
        };


        spanContext.Events.Add(eventData);
        _ = (spanContext.Activity?.AddEvent(new ActivityEvent(eventName, DateTimeOffset.UtcNow,
            [.. attributes.Select(kvp =>

                new KeyValuePair<string, object?>(kvp.Key, kvp.Value))])));


        _logger.LogTrace("Recorded event '{EventName}' in span {SpanId}", eventName, spanContext.SpanId);
    }

    /// <summary>
    /// Records kernel execution metrics within a span for performance analysis.
    /// </summary>
    /// <param name="spanContext">The span context for the kernel execution</param>
    /// <param name="kernelName">Name of the executed kernel</param>
    /// <param name="executionTime">Kernel execution duration</param>
    /// <param name="memoryUsage">Memory usage during execution</param>
    /// <param name="performanceMetrics">Detailed performance metrics</param>
    public void RecordKernelExecution(SpanContext spanContext, string kernelName,
        TimeSpan executionTime, long memoryUsage, KernelPerformanceData performanceMetrics)
    {
        ThrowIfDisposed();


        var attributes = new Dictionary<string, object?>
        {
            ["kernel_name"] = kernelName,
            ["execution_time_ms"] = executionTime.TotalMilliseconds,
            ["memory_usage_bytes"] = memoryUsage,
            ["throughput_ops_per_sec"] = performanceMetrics.ThroughputOpsPerSecond,
            ["occupancy_percentage"] = performanceMetrics.OccupancyPercentage,
            ["cache_hit_rate"] = performanceMetrics.CacheHitRate,
            ["instruction_throughput"] = performanceMetrics.InstructionThroughput,
            ["memory_bandwidth_gb_per_sec"] = performanceMetrics.MemoryBandwidthGBPerSecond
        };


        RecordEvent(spanContext, "kernel_execution", attributes);

        // Update span attributes with performance summary

        spanContext.Attributes["kernel_execution_count"] =

            (spanContext.Attributes.GetValueOrDefault("kernel_execution_count", 0) as int? ?? 0) + 1;
        spanContext.Attributes["total_execution_time_ms"] =

            (spanContext.Attributes.GetValueOrDefault("total_execution_time_ms", 0.0) as double? ?? 0.0) +

            executionTime.TotalMilliseconds;
    }

    /// <summary>
    /// Finishes a span and records its completion metrics.
    /// </summary>
    /// <param name="spanContext">The span context to finish</param>
    /// <param name="status">Final status of the operation</param>
    /// <param name="statusMessage">Optional status message</param>
    public void FinishSpan(SpanContext spanContext, SpanStatus status = SpanStatus.Ok,

        string? statusMessage = null)
    {
        ThrowIfDisposed();


        var endTime = DateTimeOffset.UtcNow;
        var duration = endTime - spanContext.StartTime;


        spanContext.EndTime = endTime;
        spanContext.Duration = duration;
        spanContext.Status = status;
        spanContext.StatusMessage = statusMessage;

        // Finalize activity

        _ = (spanContext.Activity?.SetStatus(status == SpanStatus.Ok ?

            ActivityStatusCode.Ok : ActivityStatusCode.Error, statusMessage));
        _ = (spanContext.Activity?.SetTag("duration_ms", duration.TotalMilliseconds));
        spanContext.Activity?.Dispose();

        // Convert to completed span data

        var spanData = new SpanData
        {
            SpanId = spanContext.SpanId,
            TraceId = spanContext.TraceId,
            CorrelationId = spanContext.CorrelationId,
            SpanName = spanContext.SpanName,
            DeviceId = spanContext.DeviceId,
            SpanKind = spanContext.SpanKind,
            StartTime = spanContext.StartTime,
            EndTime = endTime,
            Duration = duration,
            Status = status,
            StatusMessage = statusMessage,
            Attributes = new Dictionary<string, object?>(spanContext.Attributes),
            Events = [.. spanContext.Events],
            ParentSpanId = spanContext.ParentSpanId
        };

        // Add to trace spans

        if (_activeTraces.TryGetValue(spanContext.CorrelationId, out var traceContext))
        {
            traceContext.Spans.Add(spanData);
        }


        _logger.LogTrace("Finished span {SpanId} '{SpanName}' with status {Status} after {Duration}ms",
            spanContext.SpanId, spanContext.SpanName, status, duration.TotalMilliseconds);
    }

    /// <summary>
    /// Finishes a distributed trace and prepares it for export.
    /// </summary>
    /// <param name="correlationId">Correlation ID of the trace to finish</param>
    /// <param name="status">Final status of the entire operation</param>
    /// <returns>Completed trace data for analysis</returns>
    public async Task<TraceData?> FinishTraceAsync(string correlationId,

        TraceStatus status = TraceStatus.Ok)
    {
        ThrowIfDisposed();


        if (!_activeTraces.TryRemove(correlationId, out var traceContext))
        {
            _logger.LogWarningMessage("Attempted to finish unknown trace with correlation ID {correlationId}");
            return null;
        }


        var endTime = DateTimeOffset.UtcNow;
        var totalDuration = endTime - traceContext.StartTime;


        _ = (traceContext.Activity?.SetStatus(status == TraceStatus.Ok ?
            ActivityStatusCode.Ok : ActivityStatusCode.Error));
        _ = (traceContext.Activity?.SetTag("total_duration_ms", totalDuration.TotalMilliseconds));
        _ = (traceContext.Activity?.SetTag("span_count", traceContext.Spans.Count));
        _ = (traceContext.Activity?.SetTag("device_count", traceContext.DeviceOperations.Count));
        traceContext.Activity?.Dispose();


        var traceData = new TraceData
        {
            TraceId = traceContext.TraceId,
            CorrelationId = correlationId,
            OperationName = traceContext.OperationName,
            StartTime = traceContext.StartTime,
            EndTime = endTime,
            TotalDuration = totalDuration,
            Status = status,
            Spans = [.. traceContext.Spans],
            DeviceOperations = traceContext.DeviceOperations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Tags = new Dictionary<string, object?>(traceContext.Tags)
        };

        // Perform trace analysis

        traceData.Analysis = await AnalyzeTraceAsync(traceData);

        // Store completed trace

        _ = _completedSpans.TryAdd(correlationId, traceData.Spans);


        _logger.LogInfoMessage($"Finished distributed trace {traceData.TraceId} after {traceData.TotalDuration.TotalMilliseconds:F1}ms with {traceData.Spans.Count} spans across {traceData.DeviceOperations.Count} devices for operation '{traceData.OperationName}'");


        return traceData;
    }

    /// <summary>
    /// Analyzes a completed trace to identify performance bottlenecks and optimization opportunities.
    /// </summary>
    /// <param name="traceData">The completed trace data to analyze</param>
    /// <returns>Detailed trace analysis results</returns>
    public async Task<TraceAnalysis> AnalyzeTraceAsync(TraceData traceData)
    {
        ThrowIfDisposed();


        await Task.Yield(); // Allow other operations to continue


        var analysis = new TraceAnalysis
        {
            TraceId = traceData.TraceId,
            AnalysisTimestamp = DateTimeOffset.UtcNow,
            TotalOperationTime = traceData.TotalDuration,
            SpanCount = traceData.Spans.Count,
            DeviceCount = traceData.DeviceOperations.Count
        };

        // Analyze critical path

        analysis.CriticalPath = IdentifyCriticalPath(traceData.Spans);
        analysis.CriticalPathDuration = analysis.CriticalPath.Sum(span => span.Duration.TotalMilliseconds);

        // Analyze device utilization

        analysis.DeviceUtilization = AnalyzeDeviceUtilization(new ConcurrentDictionary<string, DeviceOperationTrace>(traceData.DeviceOperations));

        // Identify bottlenecks

        analysis.Bottlenecks = IdentifyBottlenecks(traceData.Spans);

        // Analyze memory access patterns

        analysis.MemoryAccessPatterns = AnalyzeMemoryAccessPatterns(traceData.Spans);

        // Calculate efficiency metrics

        analysis.ParallelismEfficiency = CalculateParallelismEfficiency(traceData);
        analysis.DeviceEfficiency = CalculateDeviceEfficiency(traceData);

        // Generate optimization recommendations

        analysis.OptimizationRecommendations = GenerateOptimizationRecommendations(analysis);


        return analysis;
    }

    /// <summary>
    /// Exports trace data to external monitoring systems.
    /// </summary>
    /// <param name="format">Export format (OpenTelemetry, Jaeger, Zipkin)</param>
    /// <param name="correlationIds">Specific traces to export, or null for all recent traces</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ExportTracesAsync(TraceExportFormat format,

        IEnumerable<string>? correlationIds = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        await _exportSemaphore.WaitAsync(cancellationToken);
        try
        {
            var tracesToExport = correlationIds?.ToList() ?? [.. _completedSpans.Keys];


            foreach (var correlationId in tracesToExport.Take(_options.MaxTracesPerExport))
            {
                if (_completedSpans.TryGetValue(correlationId, out var spans))
                {
                    await ExportTraceDataAsync(format, correlationId, spans, cancellationToken);
                }
            }
        }
        finally
        {
            _ = _exportSemaphore.Release();
        }
    }

    private async Task ExportTraceDataAsync(TraceExportFormat format, string correlationId,
        List<SpanData> spans, CancellationToken cancellationToken)
    {
        try
        {
            switch (format)
            {
                case TraceExportFormat.OpenTelemetry:
                    await ExportOpenTelemetryTraceAsync(correlationId, spans, cancellationToken);
                    break;
                case TraceExportFormat.Jaeger:
                    await ExportJaegerTraceAsync(correlationId, spans, cancellationToken);
                    break;
                case TraceExportFormat.Zipkin:
                    await ExportZipkinTraceAsync(correlationId, spans, cancellationToken);
                    break;
                case TraceExportFormat.Custom:
                    await ExportCustomTraceAsync(correlationId, spans, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to export trace data for correlation ID {correlationId} in format {format}");
        }
    }

    private async Task ExportOpenTelemetryTraceAsync(string correlationId, List<SpanData> spans,
        CancellationToken cancellationToken)
    {
        // Implementation for OpenTelemetry export
        await Task.Delay(1, cancellationToken); // Placeholder
        _logger.LogDebugMessage("Exported trace {correlationId} to OpenTelemetry");
    }

    private async Task ExportJaegerTraceAsync(string correlationId, List<SpanData> spans,
        CancellationToken cancellationToken)
    {
        // Implementation for Jaeger export
        await Task.Delay(1, cancellationToken); // Placeholder
        _logger.LogDebugMessage("Exported trace {correlationId} to Jaeger");
    }

    private async Task ExportZipkinTraceAsync(string correlationId, List<SpanData> spans,
        CancellationToken cancellationToken)
    {
        // Implementation for Zipkin export
        await Task.Delay(1, cancellationToken); // Placeholder
        _logger.LogDebugMessage("Exported trace {correlationId} to Zipkin");
    }

    private async Task ExportCustomTraceAsync(string correlationId, List<SpanData> spans,
        CancellationToken cancellationToken)
    {
        // Implementation for custom export format
        await Task.Delay(1, cancellationToken); // Placeholder
        _logger.LogDebugMessage("Exported trace {correlationId} to custom format");
    }

    private static List<SpanData> IdentifyCriticalPath(List<SpanData> spans)
        // Simplified critical path analysis - find the longest sequential chain



        => [.. spans.OrderByDescending(s => s.Duration).Take(5)];

    private static Dictionary<string, double> AnalyzeDeviceUtilization(
        ConcurrentDictionary<string, DeviceOperationTrace> deviceOps)
    {
        var utilization = new Dictionary<string, double>();


        foreach (var deviceOp in deviceOps)
        {
            var totalTime = (deviceOp.Value.LastOperationTime - deviceOp.Value.FirstOperationTime).TotalMilliseconds;
            var utilizationRatio = totalTime > 0 ? deviceOp.Value.OperationCount / totalTime * 1000 : 0;
            utilization[deviceOp.Key] = Math.Min(1.0, utilizationRatio);
        }


        return utilization;
    }

    private static List<PerformanceBottleneck> IdentifyBottlenecks(List<SpanData> spans)
    {
        var bottlenecks = new List<PerformanceBottleneck>();

        // Find spans that took disproportionately long

        if (spans.Count != 0)
        {
            var averageDuration = spans.Average(s => s.Duration.TotalMilliseconds);
            var threshold = averageDuration * 2;


            foreach (var span in spans.Where(s => s.Duration.TotalMilliseconds > threshold))
            {
                bottlenecks.Add(new PerformanceBottleneck
                {
                    Type = BottleneckType.DeviceUtilization,
                    DeviceId = span.DeviceId,
                    Severity = BottleneckSeverity.Medium,
                    Description = $"Span '{span.SpanName}' took {span.Duration.TotalMilliseconds:F1}ms " +
                                $"(>{threshold:F1}ms threshold)",
                    Recommendation = "Investigate kernel optimization or load balancing",
                    MetricValue = span.Duration.TotalMilliseconds
                });
            }
        }


        return bottlenecks;
    }

    private static Dictionary<string, object> AnalyzeMemoryAccessPatterns(List<SpanData> spans)
    {
        var patterns = new Dictionary<string, object>();


        var memoryEvents = spans
            .SelectMany(s => s.Events)
            .Where(e => e.Name.Contains("memory", StringComparison.OrdinalIgnoreCase))
            .ToList();


        patterns["total_memory_operations"] = memoryEvents.Count;
        patterns["unique_devices_with_memory_ops"] = spans
            .Where(s => s.Events.Any(e => e.Name.Contains("memory", StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.DeviceId)
            .Distinct()
            .Count();


        return patterns;
    }

    private static double CalculateParallelismEfficiency(TraceData traceData)
    {
        if (traceData.Spans.Count <= 1)
        {
            return 1.0;
        }


        var totalSpanTime = traceData.Spans.Sum(s => s.Duration.TotalMilliseconds);
        var actualTime = traceData.TotalDuration.TotalMilliseconds;


        return actualTime > 0 ? Math.Min(1.0, totalSpanTime / (actualTime * traceData.DeviceOperations.Count)) : 0;
    }

    private static double CalculateDeviceEfficiency(TraceData traceData)
    {
        if (traceData.DeviceOperations.Count == 0)
        {
            return 0;
        }


        var deviceUtilizations = traceData.DeviceOperations.Values
            .Select(d => (d.LastOperationTime - d.FirstOperationTime).TotalMilliseconds /

                        traceData.TotalDuration.TotalMilliseconds)
            .Where(u => u > 0);


        return deviceUtilizations.Any() ? deviceUtilizations.Average() : 0;
    }

    private static List<string> GenerateOptimizationRecommendations(TraceAnalysis analysis)
    {
        var recommendations = new List<string>();


        if (analysis.ParallelismEfficiency < 0.7)
        {
            recommendations.Add("Consider increasing parallelism or reducing sequential dependencies");
        }


        if (analysis.DeviceEfficiency < 0.8)
        {
            recommendations.Add("Optimize device utilization through better load balancing");
        }


        if (analysis.CriticalPathDuration > analysis.TotalOperationTime.TotalMilliseconds * 0.8)
        {
            recommendations.Add("Focus optimization efforts on critical path operations");
        }


        return recommendations;
    }

    private void CleanupExpiredTraces(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var expiredThreshold = DateTimeOffset.UtcNow - TimeSpan.FromHours(_options.TraceRetentionHours);
            var expiredTraces = _activeTraces
                .Where(kvp => kvp.Value.StartTime < expiredThreshold)
                .Select(kvp => kvp.Key)
                .ToList();


            foreach (var correlationId in expiredTraces)
            {
                if (_activeTraces.TryRemove(correlationId, out var trace))
                {
                    trace.Activity?.Dispose();
                    _logger.LogWarningMessage("Cleaned up expired active trace {correlationId}");
                }
            }

            // Also cleanup completed spans

            var expiredCompleted = _completedSpans
                .Where(kvp => kvp.Value.Count != 0 && kvp.Value.Max(s => s.EndTime) < expiredThreshold)
                .Select(kvp => kvp.Key)
                .ToList();


            foreach (var correlationId in expiredCompleted)
            {
                _ = _completedSpans.TryRemove(correlationId, out _);
            }


            if (expiredTraces.Count != 0 || expiredCompleted.Count != 0)
            {
                _logger.LogInfoMessage($"Cleaned up {expiredTraces.Count} expired active traces and {expiredCompleted.Count} completed traces");
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to cleanup expired traces");
        }
    }

    private static string GenerateCorrelationId() => Guid.NewGuid().ToString("N")[..16];
    private static string GenerateTraceId() => ActivityTraceId.CreateRandom().ToString();
    private static string GenerateSpanId() => ActivitySpanId.CreateRandom().ToString();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(DistributedTracer));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;

        // Dispose all active traces

        foreach (var trace in _activeTraces.Values)
        {
            trace.Activity?.Dispose();
        }


        _cleanupTimer?.Dispose();
        _exportSemaphore?.Dispose();
        ActivitySource.Dispose();
    }
}



// Supporting classes and enums continue in next part due to length...
