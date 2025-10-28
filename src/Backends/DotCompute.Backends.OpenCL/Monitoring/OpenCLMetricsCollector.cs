// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Monitoring
{
    /// <summary>
    /// Defines metric types for classification.
    /// </summary>
    public enum MetricType
    {
        /// <summary>
        /// Monotonically increasing counter (e.g., total allocations).
        /// </summary>
        Counter,

        /// <summary>
        /// Point-in-time value (e.g., current memory usage).
        /// </summary>
        Gauge,

        /// <summary>
        /// Distribution of values (e.g., execution times).
        /// </summary>
        Histogram,

        /// <summary>
        /// Events per second (e.g., throughput).
        /// </summary>
        Rate
    }

    /// <summary>
    /// Defines metric categories for organization.
    /// </summary>
    public enum MetricCategory
    {
        /// <summary>
        /// Memory-related metrics.
        /// </summary>
        Memory,

        /// <summary>
        /// Execution-related metrics.
        /// </summary>
        Execution,

        /// <summary>
        /// Compilation-related metrics.
        /// </summary>
        Compilation,

        /// <summary>
        /// Throughput-related metrics.
        /// </summary>
        Throughput,

        /// <summary>
        /// Latency-related metrics.
        /// </summary>
        Latency,

        /// <summary>
        /// Error-related metrics.
        /// </summary>
        Errors
    }

    /// <summary>
    /// Represents a single metric data point.
    /// </summary>
    public sealed class Metric
    {
        /// <summary>
        /// Gets the metric name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the metric type.
        /// </summary>
        public required MetricType Type { get; init; }

        /// <summary>
        /// Gets the metric category.
        /// </summary>
        public required MetricCategory Category { get; init; }

        /// <summary>
        /// Gets or sets the metric value.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the metric was recorded.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the metric labels for dimensional data.
        /// </summary>
        public Dictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Circular buffer for efficient memory-bounded storage.
    /// </summary>
    /// <typeparam name="T">The type of elements in the buffer.</typeparam>
    internal sealed class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private readonly object _lock = new object();
        private int _head;
        private int _tail;
        private int _count;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularBuffer{T}"/> class.
        /// </summary>
        /// <param name="capacity">The buffer capacity.</param>
        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentException("Capacity must be positive.", nameof(capacity));
            }

            _buffer = new T[capacity];
        }

        /// <summary>
        /// Gets the number of elements in the buffer.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        /// <summary>
        /// Adds an item to the buffer, overwriting oldest if full.
        /// </summary>
        /// <param name="item">The item to add.</param>
        public void Add(T item)
        {
            lock (_lock)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _buffer.Length;

                if (_count == _buffer.Length)
                {
                    _tail = (_tail + 1) % _buffer.Length;
                }
                else
                {
                    _count++;
                }
            }
        }

        /// <summary>
        /// Converts the buffer to an array.
        /// </summary>
        /// <returns>An array containing all elements in the buffer.</returns>
        public T[] ToArray()
        {
            lock (_lock)
            {
                var result = new T[_count];
                for (int i = 0; i < _count; i++)
                {
                    result[i] = _buffer[(_tail + i) % _buffer.Length];
                }
                return result;
            }
        }

        /// <summary>
        /// Clears all elements from the buffer.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _head = 0;
                _tail = 0;
                _count = 0;
                Array.Clear(_buffer, 0, _buffer.Length);
            }
        }
    }

    /// <summary>
    /// Represents a timestamped value for sliding window calculations.
    /// </summary>
    internal readonly struct TimestampedValue
    {
        public DateTime Timestamp { get; init; }
        public double Value { get; init; }
    }

    /// <summary>
    /// Sliding window for time-based metric aggregations.
    /// </summary>
    public sealed class SlidingWindow
    {
        private readonly CircularBuffer<TimestampedValue> _buffer;
        private readonly TimeSpan _windowSize;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="SlidingWindow"/> class.
        /// </summary>
        /// <param name="windowSize">The time window size.</param>
        /// <param name="maxSamples">Maximum number of samples to store.</param>
        public SlidingWindow(TimeSpan windowSize, int maxSamples = 10000)
        {
            _windowSize = windowSize;
            _buffer = new CircularBuffer<TimestampedValue>(maxSamples);
        }

        /// <summary>
        /// Adds a value to the window.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <param name="timestamp">The timestamp of the value.</param>
        public void Add(double value, DateTime timestamp)
        {
            lock (_lock)
            {
                _buffer.Add(new TimestampedValue { Value = value, Timestamp = timestamp });
            }
        }

        /// <summary>
        /// Gets the average value within the window.
        /// </summary>
        /// <returns>The average value.</returns>
        public double GetAverage()
        {
            var values = GetValidValues();
            return values.Length > 0 ? values.Average(v => v.Value) : 0.0;
        }

        /// <summary>
        /// Gets the minimum value within the window.
        /// </summary>
        /// <returns>The minimum value.</returns>
        public double GetMin()
        {
            var values = GetValidValues();
            return values.Length > 0 ? values.Min(v => v.Value) : 0.0;
        }

        /// <summary>
        /// Gets the maximum value within the window.
        /// </summary>
        /// <returns>The maximum value.</returns>
        public double GetMax()
        {
            var values = GetValidValues();
            return values.Length > 0 ? values.Max(v => v.Value) : 0.0;
        }

        /// <summary>
        /// Gets the sum of values within the window.
        /// </summary>
        /// <returns>The sum of values.</returns>
        public double GetSum()
        {
            var values = GetValidValues();
            return values.Sum(v => v.Value);
        }

        /// <summary>
        /// Gets the specified percentile value within the window.
        /// </summary>
        /// <param name="percentile">The percentile (0.0 to 1.0).</param>
        /// <returns>The percentile value.</returns>
        public double GetPercentile(double percentile)
        {
            if (percentile < 0.0 || percentile > 1.0)
            {
                throw new ArgumentException("Percentile must be between 0.0 and 1.0.", nameof(percentile));
            }

            var values = GetValidValues();
            if (values.Length == 0)
            {
                return 0.0;
            }

            var sorted = values.Select(v => v.Value).OrderBy(v => v).ToArray();
            int index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
            return sorted[Math.Max(0, index)];
        }

        /// <summary>
        /// Gets the count of values within the window.
        /// </summary>
        /// <returns>The count of valid values.</returns>
        public int GetCount()
        {
            return GetValidValues().Length;
        }

        private TimestampedValue[] GetValidValues()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var cutoff = now - _windowSize;
                return _buffer.ToArray()
                    .Where(v => v.Timestamp >= cutoff)
                    .ToArray();
            }
        }
    }

    /// <summary>
    /// Windowed metrics with multiple time windows.
    /// </summary>
    public sealed class WindowedMetrics
    {
        /// <summary>
        /// Gets the 1-second sliding window.
        /// </summary>
        public SlidingWindow OneSecond { get; }

        /// <summary>
        /// Gets the 5-second sliding window.
        /// </summary>
        public SlidingWindow FiveSeconds { get; }

        /// <summary>
        /// Gets the 1-minute sliding window.
        /// </summary>
        public SlidingWindow OneMinute { get; }

        /// <summary>
        /// Gets the 5-minute sliding window.
        /// </summary>
        public SlidingWindow FiveMinutes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowedMetrics"/> class.
        /// </summary>
        public WindowedMetrics()
        {
            OneSecond = new SlidingWindow(TimeSpan.FromSeconds(1));
            FiveSeconds = new SlidingWindow(TimeSpan.FromSeconds(5));
            OneMinute = new SlidingWindow(TimeSpan.FromMinutes(1));
            FiveMinutes = new SlidingWindow(TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Adds a value to all windows.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <param name="timestamp">The timestamp of the value.</param>
        public void Add(double value, DateTime timestamp)
        {
            OneSecond.Add(value, timestamp);
            FiveSeconds.Add(value, timestamp);
            OneMinute.Add(value, timestamp);
            FiveMinutes.Add(value, timestamp);
        }
    }

    /// <summary>
    /// Metrics snapshot representing point-in-time state.
    /// </summary>
    public sealed class MetricsSnapshot
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// Gets the snapshot timestamp.
        /// </summary>
        public required DateTime Timestamp { get; init; }

        /// <summary>
        /// Gets the current metrics.
        /// </summary>
        public required Dictionary<string, Metric> CurrentMetrics { get; init; }

        /// <summary>
        /// Gets the windowed metrics.
        /// </summary>
        public required Dictionary<string, WindowedMetrics> WindowedMetrics { get; init; }

        /// <summary>
        /// Exports the snapshot in Prometheus text format.
        /// </summary>
        /// <returns>Prometheus-formatted metrics.</returns>
        public string ToPrometheusFormat()
        {
            var sb = new StringBuilder();

            foreach (var kvp in CurrentMetrics)
            {
                var metric = kvp.Value;
                var metricName = metric.Name.Replace('.', '_');

                // Add HELP and TYPE comments
                sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP {metricName} {metric.Category} metric");
                sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE {metricName} {GetPrometheusType(metric.Type)}");

                // Add labels
                var labels = string.Join(",", metric.Labels.Select(l => $"{l.Key}=\"{l.Value}\""));
                var labelsStr = labels.Length > 0 ? $"{{{labels}}}" : "";

                sb.AppendLine(CultureInfo.InvariantCulture, $"{metricName}{labelsStr} {metric.Value}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports the snapshot in JSON format.
        /// </summary>
        /// <returns>JSON-formatted metrics.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Anonymous types are preserved in metrics export")]
        [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Metrics export is not used in AOT scenarios")]
        public string ToJsonFormat()
        {
            var data = new
            {
                timestamp = Timestamp,
                metrics = CurrentMetrics.Select(kvp => new
                {
                    name = kvp.Key,
                    type = kvp.Value.Type.ToString(),
                    category = kvp.Value.Category.ToString(),
                    value = kvp.Value.Value,
                    labels = kvp.Value.Labels
                }),
                windows = WindowedMetrics.Select(kvp => new
                {
                    name = kvp.Key,
                    oneSecond = new
                    {
                        avg = kvp.Value.OneSecond.GetAverage(),
                        min = kvp.Value.OneSecond.GetMin(),
                        max = kvp.Value.OneSecond.GetMax(),
                        count = kvp.Value.OneSecond.GetCount()
                    },
                    fiveSeconds = new
                    {
                        avg = kvp.Value.FiveSeconds.GetAverage(),
                        min = kvp.Value.FiveSeconds.GetMin(),
                        max = kvp.Value.FiveSeconds.GetMax(),
                        count = kvp.Value.FiveSeconds.GetCount()
                    },
                    oneMinute = new
                    {
                        avg = kvp.Value.OneMinute.GetAverage(),
                        min = kvp.Value.OneMinute.GetMin(),
                        max = kvp.Value.OneMinute.GetMax(),
                        p50 = kvp.Value.OneMinute.GetPercentile(0.5),
                        p95 = kvp.Value.OneMinute.GetPercentile(0.95),
                        p99 = kvp.Value.OneMinute.GetPercentile(0.99),
                        count = kvp.Value.OneMinute.GetCount()
                    },
                    fiveMinutes = new
                    {
                        avg = kvp.Value.FiveMinutes.GetAverage(),
                        min = kvp.Value.FiveMinutes.GetMin(),
                        max = kvp.Value.FiveMinutes.GetMax(),
                        count = kvp.Value.FiveMinutes.GetCount()
                    }
                })
            };

            return JsonSerializer.Serialize(data, s_jsonOptions);
        }

        private static string GetPrometheusType(MetricType type)
        {
            return type switch
            {
                MetricType.Counter => "counter",
                MetricType.Gauge => "gauge",
                MetricType.Histogram => "histogram",
                MetricType.Rate => "gauge",
                _ => "untyped"
            };
        }
    }

    /// <summary>
    /// Real-time metrics collector for OpenCL backend with circular buffers and sliding window aggregations.
    /// </summary>
    public sealed class OpenCLMetricsCollector : IDisposable
    {
        private readonly ConcurrentDictionary<string, Metric> _currentMetrics = new();
        private readonly ConcurrentDictionary<string, WindowedMetrics> _windowedMetrics = new();
        private readonly ILogger _logger;
        private readonly Timer? _autoCollectionTimer;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenCLMetricsCollector"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="autoCollectionInterval">Auto-collection interval (null to disable).</param>
        public OpenCLMetricsCollector(ILogger logger, TimeSpan? autoCollectionInterval = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (autoCollectionInterval.HasValue)
            {
                _autoCollectionTimer = new Timer(
                    AutoCollectionCallback,
                    null,
                    autoCollectionInterval.Value,
                    autoCollectionInterval.Value);
            }

            _logger.LogInformation("OpenCLMetricsCollector initialized with auto-collection: {Enabled}",
                autoCollectionInterval.HasValue);
        }

        /// <summary>
        /// Records a counter metric.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="value">The counter value.</param>
        /// <param name="labels">Optional labels for dimensional data.</param>
        public void RecordCounter(string name, long value, Dictionary<string, string>? labels = null)
        {
            var metricKey = GetMetricKey(name, labels);
            var metric = _currentMetrics.GetOrAdd(metricKey, _ => new Metric
            {
                Name = name,
                Type = MetricType.Counter,
                Category = GetCategoryFromName(name),
                Labels = labels ?? new Dictionary<string, string>()
            });

            metric.Value += value;
            metric.Timestamp = DateTime.UtcNow;

            _logger.LogTrace("Counter metric recorded: {Name} = {Value}", name, metric.Value);
        }

        /// <summary>
        /// Records a gauge metric.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="value">The gauge value.</param>
        /// <param name="labels">Optional labels for dimensional data.</param>
        public void RecordGauge(string name, double value, Dictionary<string, string>? labels = null)
        {
            var metricKey = GetMetricKey(name, labels);
            var metric = _currentMetrics.GetOrAdd(metricKey, _ => new Metric
            {
                Name = name,
                Type = MetricType.Gauge,
                Category = GetCategoryFromName(name),
                Labels = labels ?? new Dictionary<string, string>()
            });

            metric.Value = value;
            metric.Timestamp = DateTime.UtcNow;

            _logger.LogTrace("Gauge metric recorded: {Name} = {Value}", name, value);
        }

        /// <summary>
        /// Records a histogram metric.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="value">The value to add to the histogram.</param>
        /// <param name="labels">Optional labels for dimensional data.</param>
        public void RecordHistogram(string name, double value, Dictionary<string, string>? labels = null)
        {
            var metricKey = GetMetricKey(name, labels);

            // Record current metric
            var metric = _currentMetrics.GetOrAdd(metricKey, _ => new Metric
            {
                Name = name,
                Type = MetricType.Histogram,
                Category = GetCategoryFromName(name),
                Labels = labels ?? new Dictionary<string, string>()
            });

            metric.Value = value;
            metric.Timestamp = DateTime.UtcNow;

            // Add to windowed metrics
            var windows = _windowedMetrics.GetOrAdd(metricKey, _ => new WindowedMetrics());
            windows.Add(value, DateTime.UtcNow);

            _logger.LogTrace("Histogram metric recorded: {Name} = {Value}", name, value);
        }

        /// <summary>
        /// Records a rate metric.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="events">Number of events.</param>
        /// <param name="window">Time window for rate calculation.</param>
        /// <param name="labels">Optional labels for dimensional data.</param>
        public void RecordRate(string name, long events, TimeSpan window, Dictionary<string, string>? labels = null)
        {
            var rate = events / window.TotalSeconds;
            var metricKey = GetMetricKey(name, labels);

            var metric = _currentMetrics.GetOrAdd(metricKey, _ => new Metric
            {
                Name = name,
                Type = MetricType.Rate,
                Category = GetCategoryFromName(name),
                Labels = labels ?? new Dictionary<string, string>()
            });

            metric.Value = rate;
            metric.Timestamp = DateTime.UtcNow;

            _logger.LogTrace("Rate metric recorded: {Name} = {Rate:F2} events/sec", name, rate);
        }

        /// <summary>
        /// Gets a snapshot of all current metrics.
        /// </summary>
        /// <returns>A point-in-time metrics snapshot.</returns>
        /// <remarks>
        /// This method performs allocation and should not be converted to a property.
        /// </remarks>
        [SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Method creates new instances and has non-trivial cost")]
        public MetricsSnapshot GetSnapshot()
        {
            return new MetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                CurrentMetrics = new Dictionary<string, Metric>(_currentMetrics),
                WindowedMetrics = new Dictionary<string, WindowedMetrics>(_windowedMetrics)
            };
        }

        /// <summary>
        /// Resets all metrics.
        /// </summary>
        public void Reset()
        {
            _currentMetrics.Clear();
            _windowedMetrics.Clear();
            _logger.LogInformation("All metrics reset");
        }

        /// <summary>
        /// Gets the metric count.
        /// </summary>
        /// <returns>The total number of tracked metrics.</returns>
        /// <remarks>
        /// This method accesses a concurrent dictionary which could change between calls.
        /// </remarks>
        [SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Accessing concurrent dictionary state that can change")]
        public int GetMetricCount()
        {
            return _currentMetrics.Count;
        }

        private void AutoCollectionCallback(object? state)
        {
            try
            {
                // This method is called periodically for background metric collection
                // Currently, metrics are updated on-demand via Record methods
                // This can be extended for automatic system metric collection

                _logger.LogTrace("Auto-collection heartbeat");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-collection");
            }
        }

        private static string GetMetricKey(string name, Dictionary<string, string>? labels)
        {
            if (labels == null || labels.Count == 0)
            {
                return name;
            }

            var labelStr = string.Join(",", labels.OrderBy(l => l.Key).Select(l => $"{l.Key}={l.Value}"));
            return $"{name}{{{labelStr}}}";
        }

        private static MetricCategory GetCategoryFromName(string name)
        {
            if (name.Contains("memory", StringComparison.OrdinalIgnoreCase))
            {
                return MetricCategory.Memory;
            }

            if (name.Contains("execution", StringComparison.OrdinalIgnoreCase))
            {
                return MetricCategory.Execution;
            }

            if (name.Contains("compilation", StringComparison.OrdinalIgnoreCase))
            {
                return MetricCategory.Compilation;
            }

            if (name.Contains("throughput", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("rate", StringComparison.OrdinalIgnoreCase))
            {
                return MetricCategory.Throughput;
            }

            if (name.Contains("latency", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("duration", StringComparison.OrdinalIgnoreCase))
            {
                return MetricCategory.Latency;
            }

            if (name.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                return MetricCategory.Errors;
            }

            return MetricCategory.Execution;
        }

        /// <summary>
        /// Disposes the metrics collector and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _autoCollectionTimer?.Dispose();
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            _disposed = true;
            _logger.LogInformation("OpenCLMetricsCollector disposed");
        }
    }
}
