// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Represents a single metric data point.
/// </summary>
public sealed class MetricPoint
{
    /// <summary>
    /// Gets the timestamp when this metric was recorded.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the metric value.
    /// </summary>
    public double Value { get; init; }

    /// <summary>
    /// Gets optional tags for categorization.
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = [];
}

/// <summary>
/// Represents a series of metric data points over time.
/// </summary>
public sealed class MetricsSeries
{
    /// <summary>
    /// Gets the metric name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the data points in this series.
    /// </summary>
    public Collection<MetricPoint> Points { get; init; } = [];

    /// <summary>
    /// Gets the unit of measurement for this metric.
    /// </summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>
    /// Gets additional metadata for this metric series.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Represents a statistical summary of metrics data.
/// </summary>
public sealed class MetricsSummary
{
    /// <summary>
    /// Gets the metric name.
    /// </summary>
    public string MetricName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the total number of data points.
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Gets the sum of all values.
    /// </summary>
    public double Sum { get; init; }

    /// <summary>
    /// Gets the average value.
    /// </summary>
    public double Average { get; init; }

    /// <summary>
    /// Gets the minimum value.
    /// </summary>
    public double Minimum { get; init; }

    /// <summary>
    /// Gets the maximum value.
    /// </summary>
    public double Maximum { get; init; }

    /// <summary>
    /// Gets the standard deviation.
    /// </summary>
    public double StandardDeviation { get; init; }

    /// <summary>
    /// Gets the median value.
    /// </summary>
    public double Median { get; init; }

    /// <summary>
    /// Gets the 95th percentile value.
    /// </summary>
    public double Percentile95 { get; init; }

    /// <summary>
    /// Gets the 99th percentile value.
    /// </summary>
    public double Percentile99 { get; init; }

    /// <summary>
    /// Gets the timestamp of the first recorded value.
    /// </summary>
    public DateTime FirstRecorded { get; init; }

    /// <summary>
    /// Gets the timestamp of the last recorded value.
    /// </summary>
    public DateTime LastRecorded { get; init; }
}

/// <summary>
/// Represents a comprehensive metrics report.
/// </summary>
public sealed class MetricsReport
{
    /// <summary>
    /// Gets when this report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; }

    /// <summary>
    /// Gets the time range covered by this report.
    /// </summary>
    public TimeSpan? TimeRange { get; init; }

    /// <summary>
    /// Gets metrics summaries by metric name.
    /// </summary>
    public Dictionary<string, MetricsSummary> MetricsSummaries { get; init; } = [];

    /// <summary>
    /// Gets system metrics at the time of report generation.
    /// </summary>
    public SystemMetrics? SystemMetrics { get; init; }

    /// <summary>
    /// Gets detected anomalies in the metrics.
    /// </summary>
    public IReadOnlyList<MetricAnomaly> Anomalies { get; init; } = [];

    /// <summary>
    /// Gets performance trends identified in the data.
    /// </summary>
    public IReadOnlyList<PerformanceTrend> Trends { get; init; } = [];
}

/// <summary>
/// Represents a detected anomaly in metrics data.
/// </summary>
public sealed class MetricAnomaly
{
    /// <summary>
    /// Gets the metric name where the anomaly was detected.
    /// </summary>
    public string MetricName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the anomaly occurred.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the anomalous value.
    /// </summary>
    public double Value { get; init; }

    /// <summary>
    /// Gets the expected value based on historical data.
    /// </summary>
    public double ExpectedValue { get; init; }

    /// <summary>
    /// Gets the deviation from the expected value.
    /// </summary>
    public double Deviation { get; init; }

    /// <summary>
    /// Gets the severity of the anomaly.
    /// </summary>
    public AnomalySeverity Severity { get; init; }

    /// <summary>
    /// Gets the type of anomaly.
    /// </summary>
    public AnomalyType Type { get; init; }

    /// <summary>
    /// Gets a description of the anomaly.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Represents system metrics at a point in time.
/// </summary>
public sealed class SystemMetrics
{
    /// <summary>
    /// Gets the total memory available in bytes.
    /// </summary>
    public long TotalMemory { get; init; }

    /// <summary>
    /// Gets the used memory in bytes.
    /// </summary>
    public long UsedMemory { get; init; }

    /// <summary>
    /// Gets the CPU usage percentage.
    /// </summary>
    public double CpuUsage { get; init; }

    /// <summary>
    /// Gets the number of active threads.
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Gets the number of open handles.
    /// </summary>
    public int HandleCount { get; init; }

    /// <summary>
    /// Gets the number of Generation 0 garbage collections.
    /// </summary>
    public int GCGen0Collections { get; init; }

    /// <summary>
    /// Gets the number of Generation 1 garbage collections.
    /// </summary>
    public int GCGen1Collections { get; init; }

    /// <summary>
    /// Gets the number of Generation 2 garbage collections.
    /// </summary>
    public int GCGen2Collections { get; init; }
}


// PerformanceMetrics is already defined in IKernelDebugService.cs
