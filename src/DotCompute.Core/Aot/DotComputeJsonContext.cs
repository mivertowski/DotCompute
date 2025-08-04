// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotCompute.Core.Pipelines;

/// <summary>
/// AOT-compatible JSON serialization context for DotCompute.
/// This replaces runtime JsonSerializer calls with source-generated serialization.
/// </summary>
[JsonSerializable(typeof(PipelineMetricsData))]
[JsonSerializable(typeof(StageMetricsData))]
[JsonSerializable(typeof(TimeSeriesData))]
[JsonSerializable(typeof(OpenTelemetryMetricsData))]
[JsonSerializable(typeof(OpenTelemetryResource))]
[JsonSerializable(typeof(OpenTelemetryMetric))]
[JsonSerializable(typeof(OpenTelemetryGauge))]
[JsonSerializable(typeof(OpenTelemetryHistogram))]
[JsonSerializable(typeof(OpenTelemetryDataPoint))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, double>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Serialization | JsonSourceGenerationMode.Metadata)]
public partial class DotComputeJsonContext : JsonSerializerContext
{
}

/// <summary>
/// AOT-compatible data structures for pipeline metrics JSON serialization.
/// </summary>
public sealed class PipelineMetricsData
{
    public string PipelineId { get; set; } = string.Empty;
    public long ExecutionCount { get; set; }
    public long SuccessfulExecutionCount { get; set; }
    public long FailedExecutionCount { get; set; }
    public double AverageExecutionTime { get; set; }
    public double MinExecutionTime { get; set; }
    public double MaxExecutionTime { get; set; }
    public double TotalExecutionTime { get; set; }
    public double Throughput { get; set; }
    public double SuccessRate { get; set; }
    public long AverageMemoryUsage { get; set; }
    public long PeakMemoryUsage { get; set; }
    public Dictionary<string, StageMetricsData> StageMetrics { get; set; } = new();
    public Dictionary<string, double> CustomMetrics { get; set; } = new();
    public List<TimeSeriesData> TimeSeries { get; set; } = new();
}

public sealed class StageMetricsData
{
    public long ExecutionCount { get; set; }
    public long ErrorCount { get; set; }
    public double SuccessRate { get; set; }
    public double AverageExecutionTime { get; set; }
    public double MinExecutionTime { get; set; }
    public double MaxExecutionTime { get; set; }
    public double TotalExecutionTime { get; set; }
    public long AverageMemoryUsage { get; set; }
    public Dictionary<string, double> CustomMetrics { get; set; } = new();
}

public sealed class TimeSeriesData
{
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public Dictionary<string, string> Labels { get; set; } = new();
}

/// <summary>
/// AOT-compatible OpenTelemetry metrics data structures.
/// </summary>
public sealed class OpenTelemetryMetricsData
{
    public OpenTelemetryResource Resource { get; set; } = new();
    public List<OpenTelemetryMetric> Metrics { get; set; } = new();
}

public sealed class OpenTelemetryResource
{
    public Dictionary<string, object> Attributes { get; set; } = new();
}

public sealed class OpenTelemetryMetric
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public OpenTelemetryGauge? Gauge { get; set; }
    public OpenTelemetryHistogram? Histogram { get; set; }
}

public sealed class OpenTelemetryGauge
{
    public List<OpenTelemetryDataPoint> DataPoints { get; set; } = new();
}

public sealed class OpenTelemetryHistogram
{
    public List<OpenTelemetryDataPoint> DataPoints { get; set; } = new();
}

public sealed class OpenTelemetryDataPoint
{
    public object Value { get; set; } = new();
    public long TimeUnixNano { get; set; }
    public Dictionary<string, object> Attributes { get; set; } = new();
    public long? Count { get; set; }
    public double? Sum { get; set; }
}
