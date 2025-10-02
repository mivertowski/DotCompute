// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DotCompute.Core.Aot;
using DotCompute.Abstractions.Pipelines.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// High-performance pipeline telemetry collector with lock-free operations and minimal overhead.
/// Provides comprehensive metrics collection for pipeline execution analysis and optimization.
/// </summary>
public sealed class PipelineTelemetryCollector : IDisposable
{
    private static readonly Meter PipelineMeter = new("DotCompute.Pipelines", "1.0.0");
    private static readonly ActivitySource PipelineActivitySource = new("DotCompute.Pipelines", "1.0.0");

    // Metrics instruments
    private readonly Counter<long> _pipelineExecutionCounter;
    private readonly Counter<long> _stageExecutionCounter;
    private readonly Counter<long> _cacheAccessCounter;
    private readonly Histogram<double> _pipelineExecutionDuration;
    private readonly Histogram<double> _stageExecutionDuration;
    private readonly Histogram<long> _itemThroughputHistogram;
    private readonly ObservableGauge<long> _activePipelinesGauge;
    private readonly ObservableGauge<double> _averageCacheHitRatio;

    // Lock-free data structures for high-performance metrics collection
    private readonly ConcurrentDictionary<string, PipelineMetricsSnapshot> _pipelineSnapshots;
    private readonly ConcurrentQueue<TelemetryEvent> _eventQueue;
    private readonly ConcurrentDictionary<string, StageMetricsSnapshot> _stageSnapshots;

    // Atomic counters for lock-free operations

    private long _totalPipelineExecutions;
    private long _totalStageExecutions;
    private long _totalCacheAccesses;
    private long _cacheHits;
    private long _activePipelineCount;

    private readonly ILogger<PipelineTelemetryCollector> _logger;
    private readonly PipelineTelemetryOptions _options;
    private readonly Timer? _metricsExportTimer;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the PipelineTelemetryCollector.
    /// </summary>
    public PipelineTelemetryCollector(
        ILogger<PipelineTelemetryCollector> logger,
        IOptions<PipelineTelemetryOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new PipelineTelemetryOptions();


        _pipelineSnapshots = new ConcurrentDictionary<string, PipelineMetricsSnapshot>();
        _eventQueue = new ConcurrentQueue<TelemetryEvent>();
        _stageSnapshots = new ConcurrentDictionary<string, StageMetricsSnapshot>();
        _cancellationTokenSource = new CancellationTokenSource();

        // Initialize metrics instruments
        _pipelineExecutionCounter = PipelineMeter.CreateCounter<long>(
            "dotcompute_pipeline_executions_total",
            description: "Total number of pipeline executions");

        _stageExecutionCounter = PipelineMeter.CreateCounter<long>(
            "dotcompute_pipeline_stage_executions_total",

            description: "Total number of pipeline stage executions");

        _cacheAccessCounter = PipelineMeter.CreateCounter<long>(
            "dotcompute_pipeline_cache_accesses_total",
            description: "Total number of pipeline cache accesses");

        _pipelineExecutionDuration = PipelineMeter.CreateHistogram<double>(
            "dotcompute_pipeline_execution_duration_seconds",
            unit: "s",
            description: "Pipeline execution duration in seconds");

        _stageExecutionDuration = PipelineMeter.CreateHistogram<double>(
            "dotcompute_pipeline_stage_execution_duration_seconds",
            unit: "s",

            description: "Pipeline stage execution duration in seconds");

        _itemThroughputHistogram = PipelineMeter.CreateHistogram<long>(
            "dotcompute_pipeline_item_throughput",
            unit: "items/s",
            description: "Pipeline item throughput in items per second");

        _activePipelinesGauge = PipelineMeter.CreateObservableGauge<long>(
            "dotcompute_pipeline_active_count",
            observeValue: () => Interlocked.Read(ref _activePipelineCount),
            description: "Number of currently active pipelines");

        _averageCacheHitRatio = PipelineMeter.CreateObservableGauge<double>(
            "dotcompute_pipeline_cache_hit_ratio",
            observeValue: GetAverageCacheHitRatio,
            description: "Average cache hit ratio across all pipelines");

        // Start export timer if enabled
        if (_options.EnablePeriodicExport)
        {
            _metricsExportTimer = new Timer(
                ExportMetricsAsync,
                null,
                TimeSpan.FromSeconds(_options.ExportIntervalSeconds),
                TimeSpan.FromSeconds(_options.ExportIntervalSeconds));
        }
    }

    /// <summary>
    /// Records the start of a pipeline execution with minimal overhead.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PipelineExecutionContext StartPipelineExecution(string pipelineId, string? correlationId = null)
    {
        ThrowIfDisposed();


        _ = Interlocked.Increment(ref _activePipelineCount);


        var context = new PipelineExecutionContext
        {
            PipelineId = pipelineId,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            StartTime = DateTime.UtcNow,
            Activity = _options.EnableDistributedTracing

                ? PipelineActivitySource.StartActivity($"pipeline.{pipelineId}")
                : null
        };

        if (context.Activity != null)
        {
            _ = context.Activity.SetTag("pipeline.id", pipelineId);
            _ = context.Activity.SetTag("correlation.id", context.CorrelationId);
        }

        return context;
    }

    /// <summary>
    /// Records the completion of a pipeline execution with performance metrics.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompletePipelineExecution(
        PipelineExecutionContext context,

        bool success,

        long itemsProcessed = 0,
        Exception? exception = null)
    {
        ThrowIfDisposed();


        var duration = DateTime.UtcNow - context.StartTime;
        _ = Interlocked.Decrement(ref _activePipelineCount);
        _ = Interlocked.Increment(ref _totalPipelineExecutions);

        // Record metrics with tags
        var tags = new KeyValuePair<string, object?>[]
        {
            new("pipeline_id", context.PipelineId),
            new("success", success),
            new("correlation_id", context.CorrelationId)
        };

        _pipelineExecutionCounter.Add(1, tags);
        _pipelineExecutionDuration.Record(duration.TotalSeconds, tags);


        if (itemsProcessed > 0)
        {
            var throughput = itemsProcessed / Math.Max(duration.TotalSeconds, 0.001);
            _itemThroughputHistogram.Record((long)throughput, tags);
        }

        // Update pipeline snapshot (lock-free)
        _ = _pipelineSnapshots.AddOrUpdate(
            context.PipelineId,
            _ => CreateInitialPipelineSnapshot(context.PipelineId, duration, success, itemsProcessed),
            (_, existing) => existing.UpdateWith(duration, success, itemsProcessed));

        // Queue telemetry event for batch processing
        if (_options.EnableDetailedTelemetry)
        {
            _eventQueue.Enqueue(new TelemetryEvent
            {
                Name = "PipelineCompleted",
                Timestamp = new DateTimeOffset(DateTime.UtcNow),
                Attributes = [],
                Source = nameof(PipelineTelemetryCollector),
                EventType = TelemetryEventType.PipelineCompleted,
                PipelineId = context.PipelineId,
                CorrelationId = context.CorrelationId,
                Duration = duration,
                Success = success,
                ItemsProcessed = itemsProcessed,
                Exception = exception
            });
        }

        _ = (context.Activity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error));
        context.Activity?.Dispose();
    }

    /// <summary>
    /// Records stage execution with minimal performance impact.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordStageExecution(
        string pipelineId,

        string stageId,

        TimeSpan duration,

        bool success,
        long memoryUsed = 0)
    {
        ThrowIfDisposed();


        _ = Interlocked.Increment(ref _totalStageExecutions);


        var tags = new KeyValuePair<string, object?>[]
        {
            new("pipeline_id", pipelineId),
            new("stage_id", stageId),
            new("success", success)
        };

        _stageExecutionCounter.Add(1, tags);
        _stageExecutionDuration.Record(duration.TotalSeconds, tags);

        // Update stage snapshot (lock-free)
        var stageKey = $"{pipelineId}:{stageId}";
        _ = _stageSnapshots.AddOrUpdate(
            stageKey,
            _ => CreateInitialStageSnapshot(pipelineId, stageId, duration, success, memoryUsed),
            (_, existing) => existing.UpdateWith(duration, success, memoryUsed));
    }

    /// <summary>
    /// Records cache access with lock-free operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordCacheAccess(string pipelineId, bool hit)
    {
        ThrowIfDisposed();


        _ = Interlocked.Increment(ref _totalCacheAccesses);
        if (hit)
        {
            _ = Interlocked.Increment(ref _cacheHits);
        }

        var tags = new KeyValuePair<string, object?>[]
        {
            new("pipeline_id", pipelineId),
            new("hit", hit)
        };

        _cacheAccessCounter.Add(1, tags);
    }

    /// <summary>
    /// Gets comprehensive pipeline metrics for a specific pipeline.
    /// </summary>
    public PipelineMetricsSnapshot? GetPipelineMetrics(string pipelineId)
    {
        ThrowIfDisposed();
        return _pipelineSnapshots.TryGetValue(pipelineId, out var snapshot) ? snapshot : null;
    }

    /// <summary>
    /// Gets all pipeline metrics snapshots.
    /// </summary>
    public IReadOnlyDictionary<string, PipelineMetricsSnapshot> GetAllPipelineMetrics()
    {
        ThrowIfDisposed();
        return _pipelineSnapshots.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Exports metrics in the specified format with optimal performance.
    /// </summary>
    public async Task<string> ExportMetricsAsync(MetricsExportFormat format, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return format switch
        {
            MetricsExportFormat.Json => await ExportJsonAsync(cancellationToken),
            MetricsExportFormat.Prometheus => await ExportPrometheusAsync(cancellationToken),
            MetricsExportFormat.OpenTelemetry => await ExportOpenTelemetryAsync(cancellationToken),
            _ => throw new ArgumentException($"Unsupported export format: {format}", nameof(format))
        };
    }

    private static PipelineMetricsSnapshot CreateInitialPipelineSnapshot(string pipelineId, TimeSpan duration, bool success, long itemsProcessed)
    {
        return new PipelineMetricsSnapshot(
            pipelineId: pipelineId,
            executionCount: 1,
            successCount: success ? 1 : 0,
            totalDuration: duration,
            minDuration: duration,
            maxDuration: duration,
            itemsProcessed: itemsProcessed,
            lastExecution: DateTime.UtcNow);
    }

    private static StageMetricsSnapshot CreateInitialStageSnapshot(string pipelineId, string stageId, TimeSpan duration, bool success, long memoryUsed)
    {
        return new StageMetricsSnapshot(
            pipelineId: pipelineId,
            stageId: stageId,
            executionCount: 1,
            successCount: success ? 1 : 0,
            totalDuration: duration,
            minDuration: duration,
            maxDuration: duration,
            memoryUsed: memoryUsed);
    }

    private async Task<string> ExportJsonAsync(CancellationToken cancellationToken)
    {
        var data = new
        {
            timestamp = DateTime.UtcNow,
            pipeline_metrics = _pipelineSnapshots.Values.ToArray(),
            stage_metrics = _stageSnapshots.Values.ToArray(),
            global_stats = new
            {
                total_executions = Interlocked.Read(ref _totalPipelineExecutions),
                active_pipelines = Interlocked.Read(ref _activePipelineCount),
                cache_hit_ratio = GetAverageCacheHitRatio(),
                total_cache_accesses = Interlocked.Read(ref _totalCacheAccesses)
            }
        };

        return await Task.FromResult(JsonSerializer.Serialize(data, DotComputeJsonContext.Default.PipelineTelemetryData));
    }

    private async Task<string> ExportPrometheusAsync(CancellationToken cancellationToken)
    {
        var prometheus = new StringBuilder();

        // Global metrics

        _ = prometheus.AppendLine($"# TYPE dotcompute_pipeline_executions_total counter");
        _ = prometheus.AppendLine($"dotcompute_pipeline_executions_total {Interlocked.Read(ref _totalPipelineExecutions)}");


        _ = prometheus.AppendLine($"# TYPE dotcompute_pipeline_active_count gauge");
        _ = prometheus.AppendLine($"dotcompute_pipeline_active_count {Interlocked.Read(ref _activePipelineCount)}");


        _ = prometheus.AppendLine($"# TYPE dotcompute_pipeline_cache_hit_ratio gauge");
        _ = prometheus.AppendLine($"dotcompute_pipeline_cache_hit_ratio {GetAverageCacheHitRatio():F3}");

        // Per-pipeline metrics
        foreach (var snapshot in _pipelineSnapshots.Values)
        {
            var labels = $"{{pipeline_id=\"{snapshot.PipelineId}\"}}";
            _ = prometheus.AppendLine($"dotcompute_pipeline_executions_total{labels} {snapshot.ExecutionCount}");
            _ = prometheus.AppendLine($"dotcompute_pipeline_success_rate{labels} {snapshot.SuccessRate:F3}");
            _ = prometheus.AppendLine($"dotcompute_pipeline_avg_duration_seconds{labels} {snapshot.AverageDuration.TotalSeconds:F6}");
        }

        return await Task.FromResult(prometheus.ToString());
    }

    private async Task<string> ExportOpenTelemetryAsync(CancellationToken cancellationToken)
    {
        var otlpData = new
        {
            resourceMetrics = new[]
            {
                new
                {
                    resource = new
                    {
                        attributes = new[]
                        {
                            new { key = "service.name", value = new { stringValue = "dotcompute-pipelines" } },
                            new { key = "service.version", value = new { stringValue = "1.0.0" } }
                        }
                    },
                    scopeMetrics = new[]
                    {
                        new
                        {
                            scope = new
                            {
                                name = "DotCompute.Pipelines",
                                version = "1.0.0"
                            },
                            metrics = _pipelineSnapshots.Values.Select(snapshot => new
                            {
                                name = "dotcompute.pipeline.executions",
                                description = "Pipeline execution metrics",
                                unit = "1",
                                sum = new
                                {
                                    dataPoints = new[]
                                    {
                                        new
                                        {
                                            attributes = new[]
                                            {
                                                new { key = "pipeline.id", value = new { stringValue = snapshot.PipelineId } }
                                            },
                                            value = snapshot.ExecutionCount,
                                            timeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000
                                        }
                                    }
                                }
                            }).ToArray()
                        }
                    }
                }
            }
        };

        return await Task.FromResult(JsonSerializer.Serialize(otlpData, DotComputeJsonContext.Default.OpenTelemetryData));
    }

    private void ExportMetricsAsync(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var metrics = await ExportMetricsAsync(_options.DefaultExportFormat, _cancellationTokenSource.Token);


                    if (_options.ExportToConsole)
                    {
                        _logger.LogDebug("Pipeline Metrics:\n{Metrics}", metrics);
                    }

                    // Process queued events
                    await ProcessQueuedEventsAsync(_cancellationTokenSource.Token);
                }
                catch (Exception ex) when (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Failed to export pipeline metrics");
                }
            }, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule metrics export");
        }
    }

    private async Task ProcessQueuedEventsAsync(CancellationToken cancellationToken)
    {
        var processedEvents = 0;
        var batchSize = _options.EventBatchSize;

        while (_eventQueue.TryDequeue(out var telemetryEvent) && processedEvents < batchSize)
        {
            try
            {
                await ProcessTelemetryEventAsync(telemetryEvent, cancellationToken);
                processedEvents++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process telemetry event for pipeline {PipelineId}",

                    telemetryEvent.PipelineId);
            }
        }

        if (processedEvents > 0)
        {
            _logger.LogDebug("Processed {EventCount} telemetry events", processedEvents);
        }
    }

    private static async Task ProcessTelemetryEventAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken)
    {
        // Custom telemetry event processing logic
        // This could include forwarding to external systems, alerting, etc.
        await Task.CompletedTask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetAverageCacheHitRatio()
    {
        var totalAccesses = Interlocked.Read(ref _totalCacheAccesses);
        return totalAccesses > 0 ? (double)Interlocked.Read(ref _cacheHits) / totalAccesses : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(PipelineTelemetryCollector));
        }

    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;
        _cancellationTokenSource.Cancel();
        _metricsExportTimer?.Dispose();
        _cancellationTokenSource.Dispose();
        PipelineMeter.Dispose();
        PipelineActivitySource.Dispose();
    }
}

/// <summary>
/// Configuration options for pipeline telemetry collection.
/// </summary>
public sealed class PipelineTelemetryOptions
{
    /// <summary>
    /// Whether to enable distributed tracing for pipeline operations.
    /// </summary>
    public bool EnableDistributedTracing { get; set; } = true;

    /// <summary>
    /// Whether to enable detailed telemetry event collection.
    /// </summary>
    public bool EnableDetailedTelemetry { get; set; } = true;

    /// <summary>
    /// Whether to enable periodic metrics export.
    /// </summary>
    public bool EnablePeriodicExport { get; set; } = true;

    /// <summary>
    /// Interval in seconds between periodic metrics exports.
    /// </summary>
    public int ExportIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Default format for metrics export.
    /// </summary>
    public MetricsExportFormat DefaultExportFormat { get; set; } = MetricsExportFormat.Json;

    /// <summary>
    /// Whether to export metrics to console for debugging.
    /// </summary>
    public bool ExportToConsole { get; set; }


    /// <summary>
    /// Maximum number of events to process in each batch.
    /// </summary>
    public int EventBatchSize { get; set; } = 100;
}

/// <summary>
/// Simple telemetry context for pipeline execution tracking.
/// </summary>
public sealed class PipelineExecutionContext : IDisposable
{
    public string PipelineId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public Activity? Activity { get; set; }

    public void Dispose()
    {
        Activity?.Dispose();
    }
}

/// <summary>
/// Thread-safe snapshot of pipeline metrics using lock-free operations.
/// </summary>
public sealed class PipelineMetricsSnapshot
{
    private long _executionCount;
    private long _successCount;
    private long _totalDurationTicks;
    private long _minDurationTicks;
    private long _maxDurationTicks;
    private long _itemsProcessed;

    public string PipelineId { get; }
    public DateTime LastExecution { get; set; }

    public PipelineMetricsSnapshot(
        string pipelineId,
        long executionCount,
        long successCount,
        TimeSpan totalDuration,
        TimeSpan minDuration,
        TimeSpan maxDuration,
        long itemsProcessed,
        DateTime lastExecution)
    {
        PipelineId = pipelineId;
        _executionCount = executionCount;
        _successCount = successCount;
        _totalDurationTicks = totalDuration.Ticks;
        _minDurationTicks = minDuration.Ticks;
        _maxDurationTicks = maxDuration.Ticks;
        _itemsProcessed = itemsProcessed;
        LastExecution = lastExecution;
    }

    public long ExecutionCount
    {

        get => Interlocked.Read(ref _executionCount);
        private set => Interlocked.Exchange(ref _executionCount, value);
    }

    public long SuccessCount
    {

        get => Interlocked.Read(ref _successCount);
        private set => Interlocked.Exchange(ref _successCount, value);
    }

    public double SuccessRate => ExecutionCount > 0 ? (double)SuccessCount / ExecutionCount : 0.0;

    public TimeSpan TotalDuration
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _totalDurationTicks));
        private set => Interlocked.Exchange(ref _totalDurationTicks, value.Ticks);
    }

    public TimeSpan AverageDuration => ExecutionCount > 0

        ? TimeSpan.FromTicks(TotalDuration.Ticks / ExecutionCount)

        : TimeSpan.Zero;

    public TimeSpan MinDuration
    {

        get => TimeSpan.FromTicks(Interlocked.Read(ref _minDurationTicks));
        private set => Interlocked.Exchange(ref _minDurationTicks, value.Ticks);
    }


    public TimeSpan MaxDuration
    {

        get => TimeSpan.FromTicks(Interlocked.Read(ref _maxDurationTicks));
        private set => Interlocked.Exchange(ref _maxDurationTicks, value.Ticks);
    }

    public long ItemsProcessed
    {

        get => Interlocked.Read(ref _itemsProcessed);
        private set => Interlocked.Exchange(ref _itemsProcessed, value);
    }

    public double ItemThroughputPerSecond => TotalDuration.TotalSeconds > 0

        ? ItemsProcessed / TotalDuration.TotalSeconds

        : 0.0;

    public PipelineMetricsSnapshot UpdateWith(TimeSpan duration, bool success, long items)
    {
        _ = Interlocked.Increment(ref _executionCount);
        if (success)
        {
            _ = Interlocked.Increment(ref _successCount);
        }


        _ = Interlocked.Add(ref _totalDurationTicks, duration.Ticks);
        _ = Interlocked.Add(ref _itemsProcessed, items);

        // Update min/max using compare-and-swap for thread safety
        var durationTicks = duration.Ticks;

        // Update minimum

        long currentMin;
        do
        {
            currentMin = Interlocked.Read(ref _minDurationTicks);
            if (durationTicks >= currentMin && currentMin != 0)
            {
                break;
            }

        } while (Interlocked.CompareExchange(ref _minDurationTicks, durationTicks, currentMin) != currentMin);

        // Update maximum

        long currentMax;
        do
        {
            currentMax = Interlocked.Read(ref _maxDurationTicks);
            if (durationTicks <= currentMax)
            {
                break;
            }

        } while (Interlocked.CompareExchange(ref _maxDurationTicks, durationTicks, currentMax) != currentMax);


        LastExecution = DateTime.UtcNow;
        return this;
    }
}

/// <summary>
/// Thread-safe snapshot of stage metrics using lock-free operations.
/// </summary>
public sealed class StageMetricsSnapshot
{
    private long _executionCount;
    private long _successCount;
    private long _totalDurationTicks;
    private long _memoryUsed;
    private long _minDurationTicks;
    private long _maxDurationTicks;

    public string PipelineId { get; }
    public string StageId { get; }

    public StageMetricsSnapshot(
        string pipelineId,
        string stageId,
        long executionCount,
        long successCount,
        TimeSpan totalDuration,
        TimeSpan minDuration,
        TimeSpan maxDuration,
        long memoryUsed)
    {
        PipelineId = pipelineId;
        StageId = stageId;
        _executionCount = executionCount;
        _successCount = successCount;
        _totalDurationTicks = totalDuration.Ticks;
        _minDurationTicks = minDuration.Ticks;
        _maxDurationTicks = maxDuration.Ticks;
        _memoryUsed = memoryUsed;
    }

    public long ExecutionCount => Interlocked.Read(ref _executionCount);
    public long SuccessCount => Interlocked.Read(ref _successCount);
    public double SuccessRate => ExecutionCount > 0 ? (double)SuccessCount / ExecutionCount : 0.0;

    public TimeSpan TotalDuration => TimeSpan.FromTicks(Interlocked.Read(ref _totalDurationTicks));
    public TimeSpan AverageDuration => ExecutionCount > 0

        ? TimeSpan.FromTicks(TotalDuration.Ticks / ExecutionCount)

        : TimeSpan.Zero;

    public TimeSpan MinDuration => TimeSpan.FromTicks(Interlocked.Read(ref _minDurationTicks));
    public TimeSpan MaxDuration => TimeSpan.FromTicks(Interlocked.Read(ref _maxDurationTicks));
    public long MemoryUsed => Interlocked.Read(ref _memoryUsed);

    public StageMetricsSnapshot UpdateWith(TimeSpan duration, bool success, long memory)
    {
        _ = Interlocked.Increment(ref _executionCount);
        if (success)
        {
            _ = Interlocked.Increment(ref _successCount);
        }


        _ = Interlocked.Add(ref _totalDurationTicks, duration.Ticks);
        _ = Interlocked.Add(ref _memoryUsed, memory);

        // Update min/max using compare-and-swap for thread safety
        var durationTicks = duration.Ticks;

        // Update minimum

        long currentMin;
        do
        {
            currentMin = Interlocked.Read(ref _minDurationTicks);
            if (durationTicks >= currentMin && currentMin != 0)
            {
                break;
            }

        } while (Interlocked.CompareExchange(ref _minDurationTicks, durationTicks, currentMin) != currentMin);

        // Update maximum

        long currentMax;
        do
        {
            currentMax = Interlocked.Read(ref _maxDurationTicks);
            if (durationTicks <= currentMax)
            {
                break;
            }

        } while (Interlocked.CompareExchange(ref _maxDurationTicks, durationTicks, currentMax) != currentMax);


        return this;
    }
}


// TelemetryEvent and TelemetryEventType are defined in BaseTelemetryProvider.cs to avoid duplication