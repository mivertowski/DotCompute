// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text.Json.Serialization;

namespace DotCompute.Core.Aot
{

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
    [JsonSerializable(typeof(PipelineTelemetryData))]
    [JsonSerializable(typeof(OpenTelemetryData))]
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
        /// <summary>
        /// Gets or sets the pipeline identifier.
        /// </summary>
        /// <value>
        /// The pipeline identifier.
        /// </value>
        public string PipelineId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the execution count.
        /// </summary>
        /// <value>
        /// The execution count.
        /// </value>
        public long ExecutionCount { get; set; }

        /// <summary>
        /// Gets or sets the successful execution count.
        /// </summary>
        /// <value>
        /// The successful execution count.
        /// </value>
        public long SuccessfulExecutionCount { get; set; }

        /// <summary>
        /// Gets or sets the failed execution count.
        /// </summary>
        /// <value>
        /// The failed execution count.
        /// </value>
        public long FailedExecutionCount { get; set; }

        /// <summary>
        /// Gets or sets the average execution time.
        /// </summary>
        /// <value>
        /// The average execution time.
        /// </value>
        public double AverageExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the minimum execution time.
        /// </summary>
        /// <value>
        /// The minimum execution time.
        /// </value>
        public double MinExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the maximum execution time.
        /// </summary>
        /// <value>
        /// The maximum execution time.
        /// </value>
        public double MaxExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the total execution time.
        /// </summary>
        /// <value>
        /// The total execution time.
        /// </value>
        public double TotalExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the throughput.
        /// </summary>
        /// <value>
        /// The throughput.
        /// </value>
        public double Throughput { get; set; }

        /// <summary>
        /// Gets or sets the success rate.
        /// </summary>
        /// <value>
        /// The success rate.
        /// </value>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the average memory usage.
        /// </summary>
        /// <value>
        /// The average memory usage.
        /// </value>
        public long AverageMemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the peak memory usage.
        /// </summary>
        /// <value>
        /// The peak memory usage.
        /// </value>
        public long PeakMemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the stage metrics.
        /// </summary>
        /// <value>
        /// The stage metrics.
        /// </value>
        public Dictionary<string, StageMetricsData> StageMetrics { get; init; } = [];

        /// <summary>
        /// Gets or sets the custom metrics.
        /// </summary>
        /// <value>
        /// The custom metrics.
        /// </value>
        public Dictionary<string, double> CustomMetrics { get; init; } = [];

        /// <summary>
        /// Gets or sets the time series.
        /// </summary>
        /// <value>
        /// The time series.
        /// </value>
        public IList<TimeSeriesData> TimeSeries { get; init; } = [];
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class StageMetricsData
    {
        /// <summary>
        /// Gets or sets the execution count.
        /// </summary>
        /// <value>
        /// The execution count.
        /// </value>
        public long ExecutionCount { get; set; }

        /// <summary>
        /// Gets or sets the error count.
        /// </summary>
        /// <value>
        /// The error count.
        /// </value>
        public long ErrorCount { get; set; }

        /// <summary>
        /// Gets or sets the success rate.
        /// </summary>
        /// <value>
        /// The success rate.
        /// </value>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the average execution time.
        /// </summary>
        /// <value>
        /// The average execution time.
        /// </value>
        public double AverageExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the minimum execution time.
        /// </summary>
        /// <value>
        /// The minimum execution time.
        /// </value>
        public double MinExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the maximum execution time.
        /// </summary>
        /// <value>
        /// The maximum execution time.
        /// </value>
        public double MaxExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the total execution time.
        /// </summary>
        /// <value>
        /// The total execution time.
        /// </value>
        public double TotalExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the average memory usage.
        /// </summary>
        /// <value>
        /// The average memory usage.
        /// </value>
        public long AverageMemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the custom metrics.
        /// </summary>
        /// <value>
        /// The custom metrics.
        /// </value>
        public Dictionary<string, double> CustomMetrics { get; init; } = [];
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class TimeSeriesData
    {
        /// <summary>
        /// Gets or sets the name of the metric.
        /// </summary>
        /// <value>
        /// The name of the metric.
        /// </value>
        public string MetricName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        public double Value { get; set; }

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        /// <value>
        /// The timestamp.
        /// </value>
        public string Timestamp { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the labels.
        /// </summary>
        /// <value>
        /// The labels.
        /// </value>
        public Dictionary<string, string> Labels { get; init; } = [];
    }

    /// <summary>
    /// AOT-compatible OpenTelemetry metrics data structures.
    /// </summary>
    public sealed class OpenTelemetryMetricsData
    {
        /// <summary>
        /// Gets or sets the resource.
        /// </summary>
        /// <value>
        /// The resource.
        /// </value>
        public OpenTelemetryResource Resource { get; set; } = new();

        /// <summary>
        /// Gets or sets the metrics.
        /// </summary>
        /// <value>
        /// The metrics.
        /// </value>
        public IList<OpenTelemetryMetric> Metrics { get; init; } = [];
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class OpenTelemetryResource
    {
        /// <summary>
        /// Gets or sets the attributes.
        /// </summary>
        /// <value>
        /// The attributes.
        /// </value>
        public Dictionary<string, object> Attributes { get; init; } = [];
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class OpenTelemetryMetric
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>
        /// The description.
        /// </value>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unit.
        /// </summary>
        /// <value>
        /// The unit.
        /// </value>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the gauge.
        /// </summary>
        /// <value>
        /// The gauge.
        /// </value>
        public OpenTelemetryGauge? Gauge { get; set; }

        /// <summary>
        /// Gets or sets the histogram.
        /// </summary>
        /// <value>
        /// The histogram.
        /// </value>
        public OpenTelemetryHistogram? Histogram { get; set; }
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class OpenTelemetryGauge
    {
        /// <summary>
        /// Gets or sets the data points.
        /// </summary>
        /// <value>
        /// The data points.
        /// </value>
        public IList<OpenTelemetryDataPoint> DataPoints { get; init; } = [];
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class OpenTelemetryHistogram
    {
        /// <summary>
        /// Gets or sets the data points.
        /// </summary>
        /// <value>
        /// The data points.
        /// </value>
        public IList<OpenTelemetryDataPoint> DataPoints { get; init; } = [];
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class OpenTelemetryDataPoint
    {
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        public object Value { get; set; } = new();

        /// <summary>
        /// Gets or sets the time unix nano.
        /// </summary>
        /// <value>
        /// The time unix nano.
        /// </value>
        public long TimeUnixNano { get; set; }

        /// <summary>
        /// Gets or sets the attributes.
        /// </summary>
        /// <value>
        /// The attributes.
        /// </value>
        public Dictionary<string, object> Attributes { get; init; } = [];

        /// <summary>
        /// Gets or sets the count.
        /// </summary>
        /// <value>
        /// The count.
        /// </value>
        public long? Count { get; set; }

        /// <summary>
        /// Gets or sets the sum.
        /// </summary>
        /// <value>
        /// The sum.
        /// </value>
        public double? Sum { get; set; }
    }

    /// <summary>
    /// AOT-compatible pipeline telemetry data structure.
    /// </summary>
    public sealed class PipelineTelemetryData
    {
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the pipeline metrics.
        /// </summary>
        public object[] PipelineMetrics { get; set; } = [];

        /// <summary>
        /// Gets or sets the stage metrics.
        /// </summary>
        public object[] StageMetrics { get; set; } = [];

        /// <summary>
        /// Gets or sets the global stats.
        /// </summary>
        public object GlobalStats { get; set; } = new();
    }

    /// <summary>
    /// AOT-compatible OpenTelemetry data structure.
    /// </summary>
    public sealed class OpenTelemetryData
    {
        /// <summary>
        /// Gets or sets the resource metrics.
        /// </summary>
        public object[] ResourceMetrics { get; set; } = [];
    }
}
