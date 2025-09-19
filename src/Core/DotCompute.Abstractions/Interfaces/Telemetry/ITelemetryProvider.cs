// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Abstractions.Interfaces.Telemetry;

/// <summary>
/// Unified telemetry interface for consistent metrics and monitoring across the solution.
/// Addresses the issue of 1,320+ logger usage sites and multiple telemetry providers.
/// </summary>
public interface ITelemetryProvider : IDisposable
{
    /// <summary>
    /// Records a metric value.
    /// </summary>
    public void RecordMetric(string name, double value, IDictionary<string, object?>? tags = null);

    /// <summary>
    /// Increments a counter metric.
    /// </summary>
    public void IncrementCounter(string name, long increment = 1, IDictionary<string, object?>? tags = null);

    /// <summary>
    /// Records a histogram value for distribution analysis.
    /// </summary>
    public void RecordHistogram(string name, double value, IDictionary<string, object?>? tags = null);

    /// <summary>
    /// Starts a new activity span for distributed tracing.
    /// </summary>
    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal);

    /// <summary>
    /// Records an event with optional attributes.
    /// </summary>
    public void RecordEvent(string name, IDictionary<string, object?>? attributes = null);

    /// <summary>
    /// Creates a timer for measuring operation duration.
    /// </summary>
    public Interfaces.Telemetry.IOperationTimer StartTimer(string operationName, IDictionary<string, object?>? tags = null);

    /// <summary>
    /// Records memory allocation metrics.
    /// </summary>
    public void RecordMemoryAllocation(long bytes, string? allocationType = null);

    /// <summary>
    /// Records garbage collection metrics.
    /// </summary>
    public void RecordGarbageCollection(int generation, TimeSpan duration, long memoryBefore, long memoryAfter);

    /// <summary>
    /// Records accelerator utilization metrics.
    /// </summary>
    public void RecordAcceleratorUtilization(string acceleratorType, double utilization, long memoryUsed);

    /// <summary>
    /// Records kernel execution metrics.
    /// </summary>
    public void RecordKernelExecution(string kernelName, TimeSpan duration, long operationCount);

    /// <summary>
    /// Records memory transfer metrics.
    /// </summary>
    public void RecordMemoryTransfer(string direction, long bytes, TimeSpan duration);

    /// <summary>
    /// Gets or creates a meter for custom metrics.
    /// </summary>
    public Meter GetMeter(string name, string? version = null);
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

    /// <summary>
    /// Gets or sets whether to throw exceptions when telemetry errors occur.
    /// </summary>
    public bool ThrowOnTelemetryErrors { get; set; }

    /// <summary>
    /// Gets or sets whether telemetry is enabled (alias for Enabled).
    /// </summary>
    public bool IsEnabled { get => Enabled; set => Enabled = value; }

    /// <summary>
    /// Gets or sets whether asynchronous processing is enabled for telemetry data.
    /// </summary>
    public bool EnableAsyncProcessing { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log telemetry errors to the configured logger.
    /// </summary>
    public bool LogTelemetryErrors { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to track telemetry collection overhead.
    /// </summary>
    public bool TrackOverhead { get; set; }
}

/// <summary>
/// Interface for custom telemetry exporters.
/// </summary>
public interface ITelemetryExporter
{
    /// <summary>
    /// Exports metrics to the target system.
    /// </summary>
    public ValueTask ExportMetricsAsync(IEnumerable<MetricData> metrics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports traces to the target system.
    /// </summary>
    public ValueTask ExportTracesAsync(IEnumerable<Activity> activities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any buffered data.
    /// </summary>
    public ValueTask FlushAsync(CancellationToken cancellationToken = default);
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
    /// TODO: UnifiedTelemetryProvider should be implemented in DotCompute.Core
    /// </summary>
    public static void Initialize(TelemetryConfiguration configuration)
    {
        // TODO: Replace with actual implementation from DotCompute.Core
        Provider = new NullTelemetryProvider();
    }
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
    public Interfaces.Telemetry.IOperationTimer StartTimer(string operationName, IDictionary<string, object?>? tags = null) => new NullTimer();
    public void RecordMemoryAllocation(long bytes, string? allocationType = null) { }
    public void RecordGarbageCollection(int generation, TimeSpan duration, long memoryBefore, long memoryAfter) { }
    public void RecordAcceleratorUtilization(string acceleratorType, double utilization, long memoryUsed) { }
    public void RecordKernelExecution(string kernelName, TimeSpan duration, long operationCount) { }
    public void RecordMemoryTransfer(string direction, long bytes, TimeSpan duration) { }
    public Meter GetMeter(string name, string? version = null) => new(name, version);
    public void Dispose() { }


    private sealed class NullTimer : Interfaces.Telemetry.IOperationTimer
    {
        public string OperationName => string.Empty;
        public string OperationId => string.Empty;
        public DateTime StartTime => DateTime.UtcNow;
        public TimeSpan Elapsed => TimeSpan.Zero;
        public TimeSpan MinimumDurationThreshold => TimeSpan.Zero;
        public bool IsEnabled => false;

#pragma warning disable CS0067 // Event is never used - this is a null implementation
        public event EventHandler<OperationTimingEventArgs>? OperationCompleted;
#pragma warning restore CS0067

        public ITimerHandle StartOperation(string operationName, string? operationId = null) => new NullHandle();
        public IDisposable StartOperationScope(string operationName, string? operationId = null) => new NullHandle();
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
        public void Dispose() { }

        private sealed class NullHandle : ITimerHandle
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
}