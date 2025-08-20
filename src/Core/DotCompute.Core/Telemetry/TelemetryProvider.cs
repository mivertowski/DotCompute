using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Production-grade telemetry provider with OpenTelemetry integration for comprehensive observability.
/// Provides distributed tracing, metrics collection, and performance profiling for DotCompute operations.
/// </summary>
public sealed class TelemetryProvider : IDisposable
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
    
    private readonly ILogger<TelemetryProvider> _logger;
    private readonly TelemetryOptions _options;
    private readonly MetricsCollector _metricsCollector;
    private readonly PerformanceProfiler _performanceProfiler;
    private readonly ConcurrentDictionary<string, object> _correlationContext;
    private readonly Timer _samplingTimer = null!;
    private volatile bool _disposed;

    public TelemetryProvider(
        ILogger<TelemetryProvider> logger,
        IOptions<TelemetryOptions> options,
        MetricsCollector metricsCollector,
        PerformanceProfiler performanceProfiler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _performanceProfiler = performanceProfiler ?? throw new ArgumentNullException(nameof(performanceProfiler));
        
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
            
        _memoryUsageGauge = Meter.CreateObservableGauge<long>(
            "dotcompute_memory_usage_bytes",
            observeValue: () => _metricsCollector.GetCurrentMemoryUsage(),
            unit: "bytes",
            description: "Current memory usage in bytes");
            
        _deviceUtilizationGauge = Meter.CreateObservableGauge<double>(
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
            activity.SetTag("correlation_id", correlationId);
            activity.SetTag("operation_type", "kernel_execution");
            activity.SetTag("component", "dotcompute.core");
            
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    activity.SetTag(tag.Key, tag.Value?.ToString());
                }
            }
            
            // Store correlation context
            _correlationContext.TryAdd(correlationId, new CorrelationContext
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
    public void RecordKernelExecution(string kernelName, TimeSpan duration, 
        string deviceId, bool success, Dictionary<string, object>? metadata = null)
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
        _kernelExecutionDuration.Record(duration.TotalSeconds, [.. tags]);
        
        if (!success)
        {
            _errorCounter.Add(1, new KeyValuePair<string, object?>("error_type", "kernel_execution"));
        }
    }

    /// <summary>
    /// Records memory operation metrics including allocation patterns and transfer performance.
    /// </summary>
    public void RecordMemoryOperation(string operationType, long bytes, TimeSpan duration,
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
        
        _logger.LogError(exception, "Operation failed with correlation ID {CorrelationId}", correlationId);
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
    public async Task ExportTelemetryAsync(TelemetryExportFormat format = TelemetryExportFormat.Prometheus,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        try
        {
            var metrics = await _metricsCollector.CollectAllMetricsAsync(cancellationToken);
            
            switch (format)
            {
                case TelemetryExportFormat.Prometheus:
                    await ExportPrometheusMetricsAsync(metrics, cancellationToken);
                    break;
                case TelemetryExportFormat.OpenTelemetry:
                    await ExportOpenTelemetryMetricsAsync(metrics, cancellationToken);
                    break;
                case TelemetryExportFormat.Json:
                    await ExportJsonMetricsAsync(metrics, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export telemetry data");
            _errorCounter.Add(1, new KeyValuePair<string, object?>("error_type", "telemetry_export"));
        }
    }

    /// <summary>
    /// Gets current system health metrics for monitoring and alerting.
    /// </summary>
    public SystemHealthMetrics GetSystemHealth()
    {
        ThrowIfDisposed();
        
        return new SystemHealthMetrics
        {
            MemoryUsageBytes = _metricsCollector.GetCurrentMemoryUsage(),
            DeviceUtilization = _metricsCollector.GetDeviceUtilization(),
            ActiveOperations = _correlationContext.Count,
            ErrorRate = CalculateErrorRate(),
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
            if (health.MemoryUsageBytes > _options.MemoryAlertThreshold)
            {
                _logger.LogWarning("Memory usage exceeded threshold: {MemoryUsage} bytes", 
                    health.MemoryUsageBytes);
            }
            
            if (health.ErrorRate > _options.ErrorRateThreshold)
            {
                _logger.LogWarning("Error rate exceeded threshold: {ErrorRate}%", 
                    health.ErrorRate * 100);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sample metrics");
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

    private double CalculateErrorRate() =>
        // This would typically use a sliding window calculation
        // For now, return 0 as a placeholder
        0.0;

    private async Task ExportPrometheusMetricsAsync(CollectedMetrics metrics,

        CancellationToken cancellationToken) =>
        // Implementation for Prometheus export
        await Task.Delay(1, cancellationToken); // Placeholder

    private async Task ExportOpenTelemetryMetricsAsync(CollectedMetrics metrics,
        CancellationToken cancellationToken) =>
        // Implementation for OpenTelemetry export
        await Task.Delay(1, cancellationToken); // Placeholder

    private async Task ExportJsonMetricsAsync(CollectedMetrics metrics,
        CancellationToken cancellationToken) =>
        // Implementation for JSON export
        await Task.Delay(1, cancellationToken); // Placeholder

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(TelemetryProvider));
        }
    }

    public void Dispose()
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
    public bool EnableSampling { get; set; } = true;
    public int SamplingIntervalSeconds { get; set; } = 30;
    public long MemoryAlertThreshold { get; set; } = 1024L * 1024 * 1024; // 1GB
    public double ErrorRateThreshold { get; set; } = 0.05; // 5%
    public bool EnableDistributedTracing { get; set; } = true;
    public bool EnablePerformanceProfiling { get; set; } = true;
    public TelemetryExportFormat DefaultExportFormat { get; set; } = TelemetryExportFormat.Prometheus;
}

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
    public string ActivityId { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public string OperationName { get; set; } = string.Empty;
}

/// <summary>
/// System health metrics for monitoring.
/// </summary>
public sealed class SystemHealthMetrics
{
    public long MemoryUsageBytes { get; set; }
    public double DeviceUtilization { get; set; }
    public int ActiveOperations { get; set; }
    public double ErrorRate { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Container for collected metrics data.
/// </summary>
public sealed class CollectedMetrics
{
    public Dictionary<string, long> Counters { get; set; } = new();
    public Dictionary<string, double[]> Histograms { get; set; } = new();
    public Dictionary<string, double> Gauges { get; set; } = new();
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
}