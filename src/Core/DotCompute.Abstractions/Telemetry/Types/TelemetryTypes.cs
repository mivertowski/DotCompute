// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Telemetry.Types;

/// <summary>
/// Telemetry export formats.
/// </summary>
public enum TelemetryExportFormat
{
    /// <summary>
    /// Prometheus metrics format.
    /// </summary>
    Prometheus,

    /// <summary>
    /// OpenTelemetry format.
    /// </summary>
    OpenTelemetry,

    /// <summary>
    /// JSON format.
    /// </summary>
    Json
}

/// <summary>
/// Trace status values.
/// </summary>
public enum TraceStatus
{
    /// <summary>
    /// Trace completed successfully.
    /// </summary>
    Ok,

    /// <summary>
    /// Trace completed with errors.
    /// </summary>
    Error,

    /// <summary>
    /// Trace was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Profile options for performance profiling.
/// </summary>
public sealed class ProfileOptions
{
    /// <summary>
    /// Gets or sets whether to include memory profiling.
    /// </summary>
    public bool IncludeMemoryProfile { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include GPU profiling.
    /// </summary>
    public bool IncludeGpuProfile { get; set; } = true;

    /// <summary>
    /// Gets or sets the profiling duration.
    /// </summary>
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// System health metrics.
/// </summary>
public sealed class SystemHealthMetrics
{
    /// <summary>
    /// Gets or sets the CPU utilization percentage.
    /// </summary>
    public double CpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the memory utilization percentage.
    /// </summary>
    public double MemoryUtilization { get; set; }

    /// <summary>
    /// Gets or sets the GPU utilization percentage.
    /// </summary>
    public double GpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the metrics.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Kernel performance metrics for telemetry.
/// </summary>
public sealed class TelemetryKernelPerformanceMetrics
{
    /// <summary>
    /// Gets or sets the average throughput.
    /// </summary>
    public double AverageThroughput { get; set; }

    /// <summary>
    /// Gets or sets the average occupancy.
    /// </summary>
    public double AverageOccupancy { get; set; }

    /// <summary>
    /// Gets or sets the memory efficiency.
    /// </summary>
    public double MemoryEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate.
    /// </summary>
    public double CacheHitRate { get; set; }
}
