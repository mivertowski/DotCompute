// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Unified telemetry interface for consistent metrics and monitoring across the solution.
/// Addresses the issue of 1,320+ logger usage sites and multiple telemetry providers.
/// </summary>
public interface ITelemetryProvider : IDisposable
{
    /// <summary>
    /// Records a metric value.
    /// </summary>
    void RecordMetric(string name, double value, IDictionary<string, object?>? tags = null);
    
    /// <summary>
    /// Increments a counter metric.
    /// </summary>
    void IncrementCounter(string name, long increment = 1, IDictionary<string, object?>? tags = null);
    
    /// <summary>
    /// Records a histogram value for distribution analysis.
    /// </summary>
    void RecordHistogram(string name, double value, IDictionary<string, object?>? tags = null);
    
    /// <summary>
    /// Starts a new activity span for distributed tracing.
    /// </summary>
    Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal);
    
    /// <summary>
    /// Records an event with optional attributes.
    /// </summary>
    void RecordEvent(string name, IDictionary<string, object?>? attributes = null);
    
    /// <summary>
    /// Creates a timer for measuring operation duration.
    /// </summary>
    IOperationTimer StartTimer(string operationName, IDictionary<string, object?>? tags = null);
    
    /// <summary>
    /// Records memory allocation metrics.
    /// </summary>
    void RecordMemoryAllocation(long bytes, string? allocationType = null);
    
    /// <summary>
    /// Records garbage collection metrics.
    /// </summary>
    void RecordGarbageCollection(int generation, TimeSpan duration, long memoryBefore, long memoryAfter);
    
    /// <summary>
    /// Records accelerator utilization metrics.
    /// </summary>
    void RecordAcceleratorUtilization(string acceleratorType, double utilization, long memoryUsed);
    
    /// <summary>
    /// Records kernel execution metrics.
    /// </summary>
    void RecordKernelExecution(string kernelName, TimeSpan duration, long operationCount);
    
    /// <summary>
    /// Records memory transfer metrics.
    /// </summary>
    void RecordMemoryTransfer(string direction, long bytes, TimeSpan duration);
    
    /// <summary>
    /// Gets or creates a meter for custom metrics.
    /// </summary>
    Meter GetMeter(string name, string? version = null);
}

/// <summary>
/// Timer for measuring operation duration.
/// </summary>
public interface IOperationTimer : IDisposable
{
    /// <summary>
    /// Adds a tag to the operation.
    /// </summary>
    IOperationTimer AddTag(string key, object? value);
    
    /// <summary>
    /// Marks the operation as failed.
    /// </summary>
    IOperationTimer MarkFailed(Exception? exception = null);
    
    /// <summary>
    /// Stops the timer and records the duration.
    /// </summary>
    void Stop();
}

/// <summary>
/// Telemetry configuration options.
/// </summary>
public class TelemetryConfiguration
{
    /// <summary>
    /// Gets or sets whether telemetry is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the service name for telemetry.
    /// </summary>
    public string ServiceName { get; set; } = "DotCompute";
    
    /// <summary>
    /// Gets or sets the service version.
    /// </summary>
    public string? ServiceVersion { get; set; }
    
    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    public string Environment { get; set; } = "Production";
    
    /// <summary>
    /// Gets or sets whether to export to OpenTelemetry.
    /// </summary>
    public bool EnableOpenTelemetry { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to export to Prometheus.
    /// </summary>
    public bool EnablePrometheus { get; set; }
    
    /// <summary>
    /// Gets or sets whether to export to Application Insights.
    /// </summary>
    public bool EnableApplicationInsights { get; set; }
    
    /// <summary>
    /// Gets or sets the sampling rate (0.0 to 1.0).
    /// </summary>
    public double SamplingRate { get; set; } = 1.0;
    
    /// <summary>
    /// Gets or sets custom exporters.
    /// </summary>
    public List<ITelemetryExporter> CustomExporters { get; set; } = [];
    
    /// <summary>
    /// Gets or sets metric collection interval.
    /// </summary>
    public TimeSpan MetricCollectionInterval { get; set; } = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// Gets or sets whether to include detailed metrics.
    /// </summary>
    public bool IncludeDetailedMetrics { get; set; }
}

/// <summary>
/// Interface for custom telemetry exporters.
/// </summary>
public interface ITelemetryExporter
{
    /// <summary>
    /// Exports metrics to the target system.
    /// </summary>
    ValueTask ExportMetricsAsync(IEnumerable<MetricData> metrics, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Exports traces to the target system.
    /// </summary>
    ValueTask ExportTracesAsync(IEnumerable<Activity> activities, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Flushes any buffered data.
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a metric data point.
/// </summary>
public class MetricData
{
    /// <summary>
    /// Gets or sets the metric name.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Gets or sets the metric value.
    /// </summary>
    public required double Value { get; init; }
    
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Gets or sets the metric type.
    /// </summary>
    public MetricType Type { get; init; } = MetricType.Gauge;
    
    /// <summary>
    /// Gets or sets the tags/dimensions.
    /// </summary>
    public IDictionary<string, object?> Tags { get; init; } = new Dictionary<string, object?>();
    
    /// <summary>
    /// Gets or sets the unit of measurement.
    /// </summary>
    public string? Unit { get; init; }
}

/// <summary>
/// Types of metrics.
/// </summary>
public enum MetricType
{
    /// <summary>Counter metric that only increases.</summary>
    Counter,
    
    /// <summary>Gauge metric that can go up or down.</summary>
    Gauge,
    
    /// <summary>Histogram for distribution analysis.</summary>
    Histogram,
    
    /// <summary>Summary statistics.</summary>
    Summary
}

/// <summary>
/// Static helper for accessing the global telemetry provider.
/// </summary>
public static class Telemetry
{
    private static ITelemetryProvider? _provider;
    private static readonly object _lock = new();
    
    /// <summary>
    /// Gets or sets the global telemetry provider.
    /// </summary>
    public static ITelemetryProvider Provider
    {
        get
        {
            if (_provider == null)
            {
                lock (_lock)
                {
                    _provider ??= new NullTelemetryProvider();
                }
            }
            return _provider;
        }
        set
        {
            lock (_lock)
            {
                _provider?.Dispose();
                _provider = value;
            }
        }
    }


    /// <summary>
    /// Initializes the telemetry system with the specified configuration.
    /// </summary>
    public static void Initialize(TelemetryConfiguration configuration) => Provider = new UnifiedTelemetryProvider(configuration);
}

/// <summary>
/// Null telemetry provider for when telemetry is disabled.
/// </summary>
internal sealed class NullTelemetryProvider : ITelemetryProvider
{
    public void RecordMetric(string name, double value, IDictionary<string, object?>? tags = null) { }
    public void IncrementCounter(string name, long increment = 1, IDictionary<string, object?>? tags = null) { }
    public void RecordHistogram(string name, double value, IDictionary<string, object?>? tags = null) { }
    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal) => null;
    public void RecordEvent(string name, IDictionary<string, object?>? attributes = null) { }
    public IOperationTimer StartTimer(string operationName, IDictionary<string, object?>? tags = null) => new NullTimer();
    public void RecordMemoryAllocation(long bytes, string? allocationType = null) { }
    public void RecordGarbageCollection(int generation, TimeSpan duration, long memoryBefore, long memoryAfter) { }
    public void RecordAcceleratorUtilization(string acceleratorType, double utilization, long memoryUsed) { }
    public void RecordKernelExecution(string kernelName, TimeSpan duration, long operationCount) { }
    public void RecordMemoryTransfer(string direction, long bytes, TimeSpan duration) { }
    public Meter GetMeter(string name, string? version = null) => new(name, version);
    public void Dispose() { }
    
    private sealed class NullTimer : IOperationTimer
    {
        public IOperationTimer AddTag(string key, object? value) => this;
        public IOperationTimer MarkFailed(Exception? exception = null) => this;
        public void Stop() { }
        public void Dispose() { }
    }
}