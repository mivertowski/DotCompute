// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Telemetry;

/// <summary>
/// Configuration settings for telemetry collection and reporting.
/// Defines what metrics are collected and how they are processed.
/// </summary>
public sealed class TelemetryConfiguration
{
    /// <summary>
    /// Gets or sets the service name for telemetry identification.
    /// </summary>
    public string ServiceName { get; set; } = "DotCompute";

    /// <summary>
    /// Gets or sets the service version for telemetry identification.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets whether to throw exceptions when telemetry errors occur.
    /// </summary>
    public bool ThrowOnTelemetryErrors { get; set; }

    /// <summary>
    /// Gets or sets whether telemetry collection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum severity level for telemetry events.
    /// </summary>
    public TelemetrySeverity MinimumSeverity { get; set; } = TelemetrySeverity.Information;

    /// <summary>
    /// Gets or sets whether to collect performance metrics.
    /// </summary>
    public bool CollectPerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to collect memory metrics.
    /// </summary>
    public bool CollectMemoryMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to collect execution traces.
    /// </summary>
    public bool CollectExecutionTraces { get; set; }

    /// <summary>
    /// Gets or sets whether to collect hardware metrics (GPU, CPU utilization).
    /// </summary>
    public bool CollectHardwareMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the sampling rate for metrics (0-1).
    /// 1.0 means collect all metrics, 0.5 means sample 50%, etc.
    /// </summary>
    public double SamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the metrics collection interval.
    /// </summary>
    public TimeSpan CollectionInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum buffer size for telemetry data.
    /// When exceeded, oldest data is discarded.
    /// </summary>
    public int MaxBufferSize { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the batch size for telemetry exports.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the export interval for telemetry data.
    /// </summary>
    public TimeSpan ExportInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets whether to include stack traces in telemetry.
    /// </summary>
    public bool IncludeStackTraces { get; set; }

    /// <summary>
    /// Gets or sets whether to include detailed diagnostics.
    /// Warning: This can significantly impact performance.
    /// </summary>
    public bool EnableDetailedDiagnostics { get; set; }

    /// <summary>
    /// Gets or sets the maximum trace depth for execution traces.
    /// </summary>
    public int MaxTraceDepth { get; set; } = 10;

    /// <summary>
    /// Gets or sets custom tags to include with all telemetry data.
    /// </summary>
    public Dictionary<string, string> CustomTags { get; init; } = [];

    /// <summary>
    /// Gets or sets the telemetry export targets.
    /// </summary>
    public IList<TelemetryExportTarget> ExportTargets { get; init; } = [];

    /// <summary>
    /// Gets or sets whether to aggregate metrics before export.
    /// </summary>
    public bool AggregateMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the aggregation window size.
    /// </summary>
    public TimeSpan AggregationWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets whether to compress telemetry data before export.
    /// </summary>
    public bool CompressExports { get; set; }

    /// <summary>
    /// Gets or sets the maximum retention period for telemetry data.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets additional configuration options.
    /// </summary>
    public Dictionary<string, object> AdditionalOptions { get; init; } = [];
}

/// <summary>
/// Severity levels for telemetry events.
/// </summary>
public enum TelemetrySeverity
{
    /// <summary>
    /// Verbose/trace level telemetry.
    /// </summary>
    Verbose = 0,

    /// <summary>
    /// Debug level telemetry.
    /// </summary>
    Debug,

    /// <summary>
    /// Informational telemetry.
    /// </summary>
    Information,

    /// <summary>
    /// Warning level telemetry.
    /// </summary>
    Warning,

    /// <summary>
    /// Error level telemetry.
    /// </summary>
    Error,

    /// <summary>
    /// Critical error telemetry.
    /// </summary>
    Critical
}

/// <summary>
/// Defines a target for telemetry export.
/// </summary>
public sealed class TelemetryExportTarget
{
    /// <summary>
    /// Gets or sets the target type (e.g., "Console", "File", "Network").
    /// </summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target endpoint or path.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this target is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the export format (e.g., "JSON", "CSV", "Binary").
    /// </summary>
    public string Format { get; set; } = "JSON";

    /// <summary>
    /// Gets or sets target-specific configuration options.
    /// </summary>
    public Dictionary<string, object> Options { get; init; } = [];
}
