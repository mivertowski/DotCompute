// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// Configuration options for Metal telemetry system
/// </summary>
public sealed class MetalTelemetryOptions
{
    /// <summary>
    /// Gets or sets the reporting interval for periodic telemetry reports
    /// </summary>
    public TimeSpan ReportingInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the cleanup interval for old telemetry data
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the retention period for metrics data
    /// </summary>
    public TimeSpan MetricsRetentionPeriod { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets whether to automatically export metrics
    /// </summary>
    public bool AutoExportMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the slow operation threshold in milliseconds
    /// </summary>
    public double SlowOperationThresholdMs { get; set; } = 100.0;

    /// <summary>
    /// Gets or sets the high GPU utilization threshold percentage
    /// </summary>
    public double HighGpuUtilizationThreshold { get; set; } = 85.0;

    /// <summary>
    /// Gets or sets the high memory utilization threshold percentage
    /// </summary>
    public double HighMemoryUtilizationThreshold { get; set; } = 80.0;

    /// <summary>
    /// Gets or sets the high resource utilization threshold percentage
    /// </summary>
    public double HighResourceUtilizationThreshold { get; set; } = 85.0;

    /// <summary>
    /// Gets or sets the performance counters options
    /// </summary>
    public MetalPerformanceCountersOptions PerformanceCountersOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the health monitor options
    /// </summary>
    public MetalHealthMonitorOptions HealthMonitorOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the logging options
    /// </summary>
    public MetalLoggingOptions LoggingOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the export options
    /// </summary>
    public MetalExportOptions ExportOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the alerts options
    /// </summary>
    public MetalAlertsOptions AlertsOptions { get; set; } = new();
}