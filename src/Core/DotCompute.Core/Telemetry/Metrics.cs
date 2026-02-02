// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// In-memory metrics implementation with Prometheus/OpenMetrics export support.
/// Can be replaced with prometheus-net for full Prometheus integration.
/// </summary>
public static class DotComputeMetrics
{
    private static readonly MetricsRegistry _registry = new();

    /// <summary>
    /// Gets the default metrics registry.
    /// </summary>
    public static MetricsRegistry DefaultRegistry => _registry;

    /// <summary>
    /// Creates a new counter metric.
    /// </summary>
    public static ICounter CreateCounter(string name, string help, params string[] labelNames)
        => _registry.CreateCounter(name, help, labelNames);

    /// <summary>
    /// Creates a new histogram metric.
    /// </summary>
    public static IHistogram CreateHistogram(string name, string help, HistogramConfiguration? config = null, params string[] labelNames)
        => _registry.CreateHistogram(name, help, config, labelNames);

    /// <summary>
    /// Creates a new gauge metric.
    /// </summary>
    public static IGauge CreateGauge(string name, string help, params string[] labelNames)
        => _registry.CreateGauge(name, help, labelNames);

    /// <summary>
    /// Exports all metrics in OpenMetrics/Prometheus format.
    /// </summary>
    public static string ExportMetrics() => _registry.ExportMetrics();
}

/// <summary>
/// Registry for collecting and exporting metrics.
/// </summary>
public sealed class MetricsRegistry
{
    private readonly ConcurrentDictionary<string, MetricBase> _metrics = new();

    /// <summary>
    /// Creates a counter in this registry.
    /// </summary>
    public ICounter CreateCounter(string name, string help, params string[] labelNames)
    {
        var counter = new Counter(name, help, labelNames);
        _metrics.TryAdd(name, counter);
        return counter;
    }

    /// <summary>
    /// Creates a histogram in this registry.
    /// </summary>
    public IHistogram CreateHistogram(string name, string help, HistogramConfiguration? config = null, params string[] labelNames)
    {
        var histogram = new Histogram(name, help, config?.Buckets ?? Histogram.DefaultBuckets, labelNames);
        _metrics.TryAdd(name, histogram);
        return histogram;
    }

    /// <summary>
    /// Creates a gauge in this registry.
    /// </summary>
    public IGauge CreateGauge(string name, string help, params string[] labelNames)
    {
        var gauge = new Gauge(name, help, labelNames);
        _metrics.TryAdd(name, gauge);
        return gauge;
    }

    /// <summary>
    /// Exports all metrics in OpenMetrics format.
    /// </summary>
    public string ExportMetrics()
    {
        var sb = new StringBuilder();
        foreach (var metric in _metrics.Values)
        {
            metric.Export(sb);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gets all registered metrics.
    /// </summary>
    public IReadOnlyDictionary<string, MetricBase> AllMetrics => _metrics;
}

/// <summary>
/// Base class for all metric types.
/// </summary>
public abstract class MetricBase
{
    /// <summary>
    /// Gets the metric name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the metric help text.
    /// </summary>
    public string Help { get; }

    /// <summary>
    /// Gets the label names for this metric.
    /// </summary>
    public string[] LabelNames { get; }

    /// <summary>
    /// Initializes a new metric.
    /// </summary>
    protected MetricBase(string name, string help, string[] labelNames)
    {
        Name = name;
        Help = help;
        LabelNames = labelNames;
    }

    /// <summary>
    /// Exports this metric in OpenMetrics format.
    /// </summary>
    public abstract void Export(StringBuilder sb);

    /// <summary>
    /// Formats labels for export.
    /// </summary>
    protected static string FormatLabels(string[] labelNames, string[] labelValues)
    {
        if (labelNames.Length == 0) return "";
        var pairs = new string[labelNames.Length];
        for (var i = 0; i < labelNames.Length; i++)
        {
            pairs[i] = $"{labelNames[i]}=\"{EscapeLabel(labelValues[i])}\"";
        }
        return "{" + string.Join(",", pairs) + "}";
    }

    private static string EscapeLabel(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
}

/// <summary>
/// Counter metric interface.
/// </summary>
public interface ICounter
{
    /// <summary>
    /// Returns a counter with the specified labels.
    /// </summary>
    public ICounter WithLabels(params string[] labels);

    /// <summary>
    /// Increments the counter.
    /// </summary>
    public void Inc(double increment = 1);

    /// <summary>
    /// Gets the current value.
    /// </summary>
    public double Value { get; }
}

/// <summary>
/// Histogram metric interface.
/// </summary>
public interface IHistogram
{
    /// <summary>
    /// Returns a histogram with the specified labels.
    /// </summary>
    public IHistogram WithLabels(params string[] labels);

    /// <summary>
    /// Observes a value.
    /// </summary>
    public void Observe(double value);

    /// <summary>
    /// Gets the total sum of observed values.
    /// </summary>
    public double Sum { get; }

    /// <summary>
    /// Gets the count of observations.
    /// </summary>
    public long Count { get; }
}

/// <summary>
/// Gauge metric interface.
/// </summary>
public interface IGauge
{
    /// <summary>
    /// Returns a gauge with the specified labels.
    /// </summary>
    public IGauge WithLabels(params string[] labels);

    /// <summary>
    /// Sets the gauge value.
    /// </summary>
    public void Set(double value);

    /// <summary>
    /// Increments the gauge.
    /// </summary>
    public void Inc(double increment = 1);

    /// <summary>
    /// Decrements the gauge.
    /// </summary>
    public void Dec(double decrement = 1);

    /// <summary>
    /// Gets the current value.
    /// </summary>
    public double Value { get; }
}

/// <summary>
/// Counter metric implementation.
/// </summary>
internal sealed class Counter : MetricBase, ICounter
{
    private readonly ConcurrentDictionary<string, double> _values = new();
    private double _value;

    public Counter(string name, string help, string[] labelNames)
        : base(name, help, labelNames) { }

    public double Value => _value;

    public ICounter WithLabels(params string[] labels)
    {
        var key = string.Join("|", labels);
        _values.TryAdd(key, 0);
        return new LabeledCounter(this, labels);
    }

    public void Inc(double increment = 1)
    {
        Interlocked.Exchange(ref _value, _value + increment);
    }

    internal void Inc(string[] labels, double increment = 1)
    {
        var key = string.Join("|", labels);
        _values.AddOrUpdate(key, increment, (_, v) => v + increment);
    }

    public override void Export(StringBuilder sb)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP {Name} {Help}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE {Name} counter");

        if (LabelNames.Length == 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{Name} {_value}");
        }
        else
        {
            foreach (var kvp in _values)
            {
                var labels = kvp.Key.Split('|');
                sb.AppendLine(CultureInfo.InvariantCulture, $"{Name}{FormatLabels(LabelNames, labels)} {kvp.Value}");
            }
        }
    }

    private sealed class LabeledCounter(Counter parent, string[] labels) : ICounter
    {
        public double Value => parent._values.GetValueOrDefault(string.Join("|", labels));
        public ICounter WithLabels(params string[] newLabels) => parent.WithLabels(newLabels);
        public void Inc(double increment = 1) => parent.Inc(labels, increment);
    }
}

/// <summary>
/// Histogram metric implementation.
/// </summary>
internal sealed class Histogram : MetricBase, IHistogram
{
    public static readonly double[] DefaultBuckets = [.005, .01, .025, .05, .075, .1, .25, .5, .75, 1, 2.5, 5, 7.5, 10];

    private readonly double[] _buckets;
    private readonly ConcurrentDictionary<string, HistogramData> _data = new();
    private long _count;
    private double _sum;

    public Histogram(string name, string help, double[] buckets, string[] labelNames)
        : base(name, help, labelNames)
    {
        _buckets = buckets.OrderBy(b => b).ToArray();
    }

    public double Sum => _sum;
    public long Count => _count;

    public IHistogram WithLabels(params string[] labels)
    {
        var key = string.Join("|", labels);
        _data.TryAdd(key, new HistogramData(_buckets.Length));
        return new LabeledHistogram(this, labels);
    }

    public void Observe(double value)
    {
        Interlocked.Increment(ref _count);
        Interlocked.Exchange(ref _sum, _sum + value);
    }

    internal void Observe(string[] labels, double value)
    {
        var key = string.Join("|", labels);
        var data = _data.GetOrAdd(key, _ => new HistogramData(_buckets.Length));
        data.Observe(value, _buckets);
    }

    public override void Export(StringBuilder sb)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP {Name} {Help}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE {Name} histogram");

        if (LabelNames.Length == 0)
        {
            for (var i = 0; i < _buckets.Length; i++)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"{Name}_bucket{{le=\"{_buckets[i]}\"}} 0");
            }
            sb.AppendLine(CultureInfo.InvariantCulture, $"{Name}_bucket{{le=\"+Inf\"}} {_count}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"{Name}_sum {_sum}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"{Name}_count {_count}");
        }
        else
        {
            foreach (var kvp in _data)
            {
                var labels = kvp.Key.Split('|');
                var labelStr = FormatLabels(LabelNames, labels);
                var data = kvp.Value;

                for (var i = 0; i < _buckets.Length; i++)
                {
                    var leLabel = labelStr.TrimEnd('}') + (labelStr.Length > 2 ? "," : "") + $"le=\"{_buckets[i]}\"}}";
                    sb.AppendLine(CultureInfo.InvariantCulture, $"{Name}_bucket{leLabel} {data.BucketCounts[i]}");
                }
                var infLabel = labelStr.TrimEnd('}') + (labelStr.Length > 2 ? "," : "") + "le=\"+Inf\"}";
                sb.AppendLine(CultureInfo.InvariantCulture, $"{Name}_bucket{infLabel} {data.Count}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"{Name}_sum{labelStr} {data.Sum}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"{Name}_count{labelStr} {data.Count}");
            }
        }
    }

    private sealed class HistogramData(int bucketCount)
    {
        public long[] BucketCounts { get; } = new long[bucketCount];
        public long Count;
        public double Sum;

        public void Observe(double value, double[] buckets)
        {
            Interlocked.Increment(ref Count);
            Interlocked.Exchange(ref Sum, Sum + value);
            for (var i = 0; i < buckets.Length; i++)
            {
                if (value <= buckets[i])
                {
                    Interlocked.Increment(ref BucketCounts[i]);
                }
            }
        }
    }

    private sealed class LabeledHistogram(Histogram parent, string[] labels) : IHistogram
    {
        public double Sum => parent._data.GetValueOrDefault(string.Join("|", labels))?.Sum ?? 0;
        public long Count => parent._data.GetValueOrDefault(string.Join("|", labels))?.Count ?? 0;
        public IHistogram WithLabels(params string[] newLabels) => parent.WithLabels(newLabels);
        public void Observe(double value) => parent.Observe(labels, value);
    }
}

/// <summary>
/// Gauge metric implementation.
/// </summary>
internal sealed class Gauge : MetricBase, IGauge
{
    private readonly ConcurrentDictionary<string, double> _values = new();
    private double _value;

    public Gauge(string name, string help, string[] labelNames)
        : base(name, help, labelNames) { }

    public double Value => _value;

    public IGauge WithLabels(params string[] labels)
    {
        var key = string.Join("|", labels);
        _values.TryAdd(key, 0);
        return new LabeledGauge(this, labels);
    }

    public void Set(double value) => Interlocked.Exchange(ref _value, value);
    public void Inc(double increment = 1) => Interlocked.Exchange(ref _value, _value + increment);
    public void Dec(double decrement = 1) => Interlocked.Exchange(ref _value, _value - decrement);

    internal void Set(string[] labels, double value)
    {
        var key = string.Join("|", labels);
        _values[key] = value;
    }

    internal void Inc(string[] labels, double increment = 1)
    {
        var key = string.Join("|", labels);
        _values.AddOrUpdate(key, increment, (_, v) => v + increment);
    }

    internal void Dec(string[] labels, double decrement = 1)
    {
        var key = string.Join("|", labels);
        _values.AddOrUpdate(key, -decrement, (_, v) => v - decrement);
    }

    public override void Export(StringBuilder sb)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP {Name} {Help}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE {Name} gauge");

        if (LabelNames.Length == 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{Name} {_value}");
        }
        else
        {
            foreach (var kvp in _values)
            {
                var labels = kvp.Key.Split('|');
                sb.AppendLine(CultureInfo.InvariantCulture, $"{Name}{FormatLabels(LabelNames, labels)} {kvp.Value}");
            }
        }
    }

    private sealed class LabeledGauge(Gauge parent, string[] labels) : IGauge
    {
        public double Value => parent._values.GetValueOrDefault(string.Join("|", labels));
        public IGauge WithLabels(params string[] newLabels) => parent.WithLabels(newLabels);
        public void Set(double value) => parent.Set(labels, value);
        public void Inc(double increment = 1) => parent.Inc(labels, increment);
        public void Dec(double decrement = 1) => parent.Dec(labels, decrement);
    }
}

/// <summary>
/// Configuration for histogram metrics.
/// </summary>
public sealed class HistogramConfiguration
{
    /// <summary>
    /// Gets or sets the histogram bucket boundaries.
    /// </summary>
    public double[]? Buckets { get; set; }
}

// Keep backward compatibility alias
internal static class PrometheusMetricsStub
{
    public static readonly MetricsRegistry DefaultRegistry = DotComputeMetrics.DefaultRegistry;
    public static ICounter CreateCounter(string name, string help, params string[] labelNames) => DotComputeMetrics.CreateCounter(name, help, labelNames);
    public static IHistogram CreateHistogram(string name, string help, HistogramConfiguration? config = null, params string[] labelNames) => DotComputeMetrics.CreateHistogram(name, help, config, labelNames);
    public static IGauge CreateGauge(string name, string help, params string[] labelNames) => DotComputeMetrics.CreateGauge(name, help, labelNames);
}
