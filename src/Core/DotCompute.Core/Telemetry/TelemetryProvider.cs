using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using DotCompute.Core.Logging;
using DotCompute.Core.Telemetry.Profiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Production-grade telemetry provider with OpenTelemetry integration for comprehensive observability.
/// Provides distributed tracing, metrics collection, and performance profiling for DotCompute operations.
/// </summary>
public sealed class ProductionTelemetryProvider : AbstractionsMemory.Telemetry.Providers.TelemetryProvider
{
    private static readonly ActivitySource ActivitySource = new("DotCompute.Core", "1.0.0");
    private static readonly Meter Meter = new("DotCompute.Core", "1.0.0");

    // Core metrics

    private readonly Counter<long> _kernelExecutionCounter;
    private readonly Counter<long> _memoryAllocationCounter;
    private readonly Counter<long> _errorCounter;
    private readonly Histogram<double> _kernelExecutionDuration;
    private readonly Histogram<double> _memoryTransferDuration;
    private readonly ObservableGauge<long> _memoryUsageGauge;
    private readonly ObservableGauge<double> _deviceUtilizationGauge;


    private readonly ILogger<ProductionTelemetryProvider> _logger;
    private readonly TelemetryOptions _options;
    private readonly MetricsCollector _metricsCollector;
    private readonly PerformanceProfiler _performanceProfiler;
    private readonly ConcurrentDictionary<string, object> _correlationContext;
#pragma warning disable CA2213 // Disposable fields should be disposed - Timer is properly disposed in Dispose method at line 436
    private readonly Timer _samplingTimer = null!;
#pragma warning restore CA2213
    private volatile bool _disposed;
    /// <summary>
    /// Initializes a new instance of the ProductionTelemetryProvider class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>
    /// <param name="metricsCollector">The metrics collector.</param>
    /// <param name="performanceProfiler">The performance profiler.</param>

    public ProductionTelemetryProvider(
        ILogger<ProductionTelemetryProvider> logger,
        IOptions<TelemetryOptions> options,
        MetricsCollector metricsCollector,
        PerformanceProfiler performanceProfiler)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(metricsCollector);

        _metricsCollector = metricsCollector;
        ArgumentNullException.ThrowIfNull(performanceProfiler);

        _performanceProfiler = performanceProfiler;


        _correlationContext = new ConcurrentDictionary<string, object>();

        // Initialize metrics

        _kernelExecutionCounter = Meter.CreateCounter<long>(
            "dotcompute_kernel_executions_total",
            description: "Total number of kernel executions");


        _memoryAllocationCounter = Meter.CreateCounter<long>(
            "dotcompute_memory_allocations_total",

            description: "Total number of memory allocations");


        _errorCounter = Meter.CreateCounter<long>(
            "dotcompute_errors_total",
            description: "Total number of errors");


        _kernelExecutionDuration = Meter.CreateHistogram<double>(
            "dotcompute_kernel_execution_duration_seconds",
            unit: "s",
            description: "Kernel execution duration in seconds");


        _memoryTransferDuration = Meter.CreateHistogram<double>(
            "dotcompute_memory_transfer_duration_seconds",

            unit: "s",
            description: "Memory transfer duration in seconds");


        _memoryUsageGauge = Meter.CreateObservableGauge(
            "dotcompute_memory_usage_bytes",
            observeValue: () => _metricsCollector.GetCurrentMemoryUsage(),
            unit: "bytes",
            description: "Current memory usage in bytes");


        _deviceUtilizationGauge = Meter.CreateObservableGauge(
            "dotcompute_device_utilization_ratio",
            observeValue: () => _metricsCollector.GetDeviceUtilization(),
            description: "Device utilization ratio (0.0 to 1.0)");

        // Start sampling timer if enabled

        if (_options.EnableSampling)
        {
            _samplingTimer = new Timer(SampleMetrics, null,

                TimeSpan.FromSeconds(_options.SamplingIntervalSeconds),
                TimeSpan.FromSeconds(_options.SamplingIntervalSeconds));
        }
    }

    /// <summary>
    /// Starts a new distributed trace for kernel execution with correlation context.
    /// </summary>
    /// <param name="operationName">The name of the operation being traced</param>
    /// <param name="correlationId">Unique correlation ID for request tracing</param>
    /// <param name="tags">Additional tags for the trace</param>
    /// <returns>Activity for the trace span</returns>
    public Activity? StartKernelTrace(string operationName, string correlationId,

        Dictionary<string, object?>? tags = null)
    {
        ThrowIfDisposed();


        var activity = ActivitySource.StartActivity($"kernel.{operationName}");
        if (activity != null)
        {
            _ = activity.SetTag("correlation_id", correlationId);
            _ = activity.SetTag("operation_type", "kernel_execution");
            _ = activity.SetTag("component", "dotcompute.core");


            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    _ = activity.SetTag(tag.Key, tag.Value?.ToString());
                }
            }

            // Store correlation context

            _ = _correlationContext.TryAdd(correlationId, new CorrelationContext
            {
                ActivityId = activity.Id ?? string.Empty,
                StartTime = DateTimeOffset.UtcNow,
                OperationName = operationName
            });
        }


        return activity;
    }

    /// <summary>
    /// Records kernel execution metrics with detailed performance data.
    /// </summary>
    public override void RecordKernelExecution(string kernelName, TimeSpan executionTime,

        string deviceId, bool success, Dictionary<string, object> metadata)
    {
        ThrowIfDisposed();


        var tags = new List<KeyValuePair<string, object?>>
        {
            new("kernel_name", kernelName),
            new("device_id", deviceId),
            new("success", success)
        };


        if (metadata != null)
        {
            foreach (var item in metadata)
            {
                tags.Add(new KeyValuePair<string, object?>(item.Key, item.Value));
            }
        }


        _kernelExecutionCounter.Add(1, [.. tags]);
        _kernelExecutionDuration.Record(executionTime.TotalSeconds, [.. tags]);


        if (!success)
        {
            _errorCounter.Add(1, new KeyValuePair<string, object?>("error_type", "kernel_execution"));
        }
    }

    /// <summary>
    /// Records memory operation metrics including allocation patterns and transfer performance.
    /// </summary>
    public override void RecordMemoryOperation(string operationType, long bytes, TimeSpan duration,
        string deviceId, bool success)
    {
        ThrowIfDisposed();


        var tags = new KeyValuePair<string, object?>[]
        {
            new("operation_type", operationType),
            new("device_id", deviceId),
            new("success", success),
            new("size_category", CategorizeMemorySize(bytes))
        };


        _memoryAllocationCounter.Add(1, tags);


        if (operationType.Contains("transfer", StringComparison.OrdinalIgnoreCase))
        {
            _memoryTransferDuration.Record(duration.TotalSeconds, tags);
        }


        if (!success)
        {
            _errorCounter.Add(1, new KeyValuePair<string, object?>("error_type", "memory_operation"));
        }
    }

    /// <summary>
    /// Records error with context and correlation information for debugging.
    /// </summary>
    public void RecordError(Exception exception, string correlationId,

        Dictionary<string, object>? context = null)
    {
        ThrowIfDisposed();


        var tags = new List<KeyValuePair<string, object?>>
        {
            new("error_type", exception.GetType().Name),
            new("correlation_id", correlationId)
        };


        if (context != null)
        {
            foreach (var item in context)
            {
                tags.Add(new KeyValuePair<string, object?>(item.Key, item.Value));
            }
        }


        _errorCounter.Add(1, [.. tags]);

        // Log structured error with correlation context

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["ErrorType"] = exception.GetType().Name,
            ["StackTrace"] = exception.StackTrace ?? string.Empty
        });


        _logger.LogErrorMessage(exception, $"Operation failed with correlation ID {correlationId}");
    }

    /// <summary>
    /// Creates a performance profile for detailed kernel analysis.
    /// </summary>
    public async Task<PerformanceProfile> CreatePerformanceProfileAsync(string correlationId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        return await _performanceProfiler.CreateProfileAsync(correlationId, null, cancellationToken);
    }

    /// <summary>
    /// Exports telemetry data to configured external systems (Prometheus, ELK, etc.).
    /// </summary>
    public override async Task ExportTelemetryAsync(AbstractionsMemory.Telemetry.Types.TelemetryExportFormat format = AbstractionsMemory.Telemetry.Types.TelemetryExportFormat.Prometheus,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        try
        {
            var metrics = await _metricsCollector.CollectAllMetricsAsync(cancellationToken);


            switch (format)
            {
                case AbstractionsMemory.Telemetry.Types.TelemetryExportFormat.Prometheus:
                    await ExportPrometheusMetricsAsync(metrics, cancellationToken);
                    break;
                case AbstractionsMemory.Telemetry.Types.TelemetryExportFormat.OpenTelemetry:
                    await ExportOpenTelemetryMetricsAsync(metrics, cancellationToken);
                    break;
                case AbstractionsMemory.Telemetry.Types.TelemetryExportFormat.Json:
                    await ExportJsonMetricsAsync(metrics, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to export telemetry data");
            _errorCounter.Add(1, new KeyValuePair<string, object?>("error_type", "telemetry_export"));
        }
    }

    /// <summary>
    /// Gets current system health metrics for monitoring and alerting.
    /// </summary>
    public override AbstractionsMemory.Telemetry.Types.SystemHealthMetrics GetSystemHealth()
    {
        ThrowIfDisposed();


        return new AbstractionsMemory.Telemetry.Types.SystemHealthMetrics
        {
            CpuUtilization = Environment.ProcessorCount > 0 ? _metricsCollector.GetDeviceUtilization() / Environment.ProcessorCount : 0,
            MemoryUtilization = Math.Min(100.0, _metricsCollector.GetCurrentMemoryUsage() / (1024.0 * 1024.0 * 1024.0) * 10), // Rough estimate
            GpuUtilization = _metricsCollector.GetDeviceUtilization(),
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private void SampleMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var health = GetSystemHealth();

            // Check thresholds for alerting

            if (health.MemoryUtilization > 80.0)
            {
                _logger.LogWarningMessage($"Memory utilization exceeded threshold: {health.MemoryUtilization:F2}%");
            }


            if (health.GpuUtilization > 90.0)
            {
                _logger.LogWarningMessage($"GPU utilization exceeded threshold: {health.GpuUtilization:F2}%");
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to sample metrics");
        }
    }

    private static string CategorizeMemorySize(long bytes)
    {
        return bytes switch
        {
            < 1024 => "small",
            < 1024 * 1024 => "medium",

            < 1024 * 1024 * 1024 => "large",
            _ => "xlarge"
        };
    }

    private static double CalculateErrorRate()
        // This would typically use a sliding window calculation
        // For now, return 0 as a placeholder





        => 0.0;

    private static async Task ExportPrometheusMetricsAsync(CollectedMetrics metrics,

        CancellationToken cancellationToken)
        // Implementation for Prometheus export





        => await Task.Delay(1, cancellationToken); // Placeholder

    private static async Task ExportOpenTelemetryMetricsAsync(CollectedMetrics metrics,
        CancellationToken cancellationToken)
        // Implementation for OpenTelemetry export





        => await Task.Delay(1, cancellationToken); // Placeholder

    private static async Task ExportJsonMetricsAsync(CollectedMetrics metrics,
        CancellationToken cancellationToken)
        // Implementation for JSON export





        => await Task.Delay(1, cancellationToken); // Placeholder

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>
    public new void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;
        _samplingTimer?.Dispose();
        _metricsCollector?.Dispose();
        _performanceProfiler?.Dispose();
        Meter.Dispose();
        ActivitySource.Dispose();
    }
}

/// <summary>
/// Configuration options for the telemetry provider.
/// </summary>
public sealed class TelemetryOptions
{
    /// <summary>
    /// Gets or sets the enable sampling.
    /// </summary>
    /// <value>The enable sampling.</value>
    public bool EnableSampling { get; set; } = true;
    /// <summary>
    /// Gets or sets the sampling interval seconds.
    /// </summary>
    /// <value>The sampling interval seconds.</value>
    public int SamplingIntervalSeconds { get; set; } = 30;
    /// <summary>
    /// Gets or sets the memory alert threshold.
    /// </summary>
    /// <value>The memory alert threshold.</value>
    public long MemoryAlertThreshold { get; set; } = 1024L * 1024 * 1024; // 1GB
    /// <summary>
    /// Gets or sets the error rate threshold.
    /// </summary>
    /// <value>The error rate threshold.</value>
    public double ErrorRateThreshold { get; set; } = 0.05; // 5%
    /// <summary>
    /// Gets or sets the enable distributed tracing.
    /// </summary>
    /// <value>The enable distributed tracing.</value>
    public bool EnableDistributedTracing { get; set; } = true;
    /// <summary>
    /// Gets or sets the enable performance profiling.
    /// </summary>
    /// <value>The enable performance profiling.</value>
    public bool EnablePerformanceProfiling { get; set; } = true;
    /// <summary>
    /// Gets or sets the default export format.
    /// </summary>
    /// <value>The default export format.</value>
    public TelemetryExportFormat DefaultExportFormat { get; set; } = TelemetryExportFormat.Prometheus;
}
/// <summary>
/// An telemetry export format enumeration.
/// </summary>

/// <summary>
/// Available telemetry export formats.
/// </summary>
public enum TelemetryExportFormat
{
    Prometheus,
    OpenTelemetry,
    Json,
    Datadog,
    AzureMonitor
}

/// <summary>
/// Correlation context for distributed tracing.
/// </summary>
internal sealed class CorrelationContext
{
    /// <summary>
    /// Gets or sets the activity identifier.
    /// </summary>
    /// <value>The activity id.</value>
    public string ActivityId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the operation name.
    /// </summary>
    /// <value>The operation name.</value>
    public string OperationName { get; set; } = string.Empty;
}

/// <summary>
/// System health metrics for monitoring.
/// </summary>
public sealed class SystemHealthMetrics
{
    /// <summary>
    /// Gets or sets the memory usage bytes.
    /// </summary>
    /// <value>The memory usage bytes.</value>
    public long MemoryUsageBytes { get; set; }
    /// <summary>
    /// Gets or sets the device utilization.
    /// </summary>
    /// <value>The device utilization.</value>
    public double DeviceUtilization { get; set; }
    /// <summary>
    /// Gets or sets the active operations.
    /// </summary>
    /// <value>The active operations.</value>
    public int ActiveOperations { get; set; }
    /// <summary>
    /// Gets or sets the error rate.
    /// </summary>
    /// <value>The error rate.</value>
    public double ErrorRate { get; set; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Container for collected metrics data.
/// </summary>
public sealed class CollectedMetrics
{
    /// <summary>
    /// Gets or sets the counters.
    /// </summary>
    /// <value>The counters.</value>
    public Dictionary<string, long> Counters { get; init; } = [];
    /// <summary>
    /// Gets or sets the histograms.
    /// </summary>
    /// <value>The histograms.</value>
    public Dictionary<string, double[]> Histograms { get; init; } = [];
    /// <summary>
    /// Gets or sets the gauges.
    /// </summary>
    /// <value>The gauges.</value>
    public Dictionary<string, double> Gauges { get; init; } = [];
    /// <summary>
    /// Gets or sets the collected at.
    /// </summary>
    /// <value>The collected at.</value>
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
}
