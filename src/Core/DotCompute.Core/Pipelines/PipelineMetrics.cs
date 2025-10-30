// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using DotCompute.Abstractions.Interfaces.Pipelines.Interfaces;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Metrics;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Core.Aot;
using AbsTimeSeriesMetric = DotCompute.Abstractions.Pipelines.Metrics.TimeSeriesMetric;
using CoreTimeSeriesMetric = DotCompute.Core.Pipelines.Metrics.TimeSeriesMetric;

namespace DotCompute.Core.Pipelines
{

    /// <summary>
    /// Implementation of pipeline performance metrics.
    /// </summary>
    internal sealed class PipelineMetrics(string pipelineId) : IPipelineMetrics
    {
        private readonly ConcurrentQueue<PipelineExecutionMetrics> _executions = new();
        private readonly ConcurrentDictionary<string, StageMetrics> _stageMetrics = new();
        private readonly ConcurrentQueue<CoreTimeSeriesMetric> _timeSeries = new();
        private readonly ConcurrentDictionary<string, double> _customMetrics = new();
        private readonly Lock _lock = new();

        private long _executionCount;
        private long _successfulExecutionCount;
        private long _failedExecutionCount;
        private TimeSpan _totalExecutionTime;
        private TimeSpan _minExecutionTime = TimeSpan.MaxValue;
        private TimeSpan _maxExecutionTime = TimeSpan.MinValue;
        private long _totalMemoryUsage;
        private long _peakMemoryUsage;
        private long _totalItemsProcessed;
        private long _totalCacheRequests;
        private long _cacheHits;

        /// <inheritdoc/>
        public string PipelineId { get; } = pipelineId;

        /// <inheritdoc/>
        public long ExecutionCount => _executionCount;

        /// <inheritdoc/>
        public long SuccessfulExecutionCount => _successfulExecutionCount;

        /// <inheritdoc/>
        public long FailedExecutionCount => _failedExecutionCount;

        /// <inheritdoc/>
        public TimeSpan AverageExecutionTime
            => _executionCount > 0
            ? TimeSpan.FromTicks(_totalExecutionTime.Ticks / _executionCount)
            : TimeSpan.Zero;

        /// <inheritdoc/>
        public TimeSpan MinExecutionTime
            => _executionCount > 0 ? _minExecutionTime : TimeSpan.Zero;

        /// <inheritdoc/>
        public TimeSpan MaxExecutionTime
            => _executionCount > 0 ? _maxExecutionTime : TimeSpan.Zero;

        /// <inheritdoc/>
        public TimeSpan TotalExecutionTime => _totalExecutionTime;

        /// <inheritdoc/>
        public double Throughput
            => _totalExecutionTime.TotalSeconds > 0
            ? _executionCount / _totalExecutionTime.TotalSeconds
            : 0;

        /// <inheritdoc/>
        public double SuccessRate
            => _executionCount > 0
            ? (double)_successfulExecutionCount / _executionCount
            : 0;

        /// <inheritdoc/>
        public long AverageMemoryUsage
            => _executionCount > 0 ? _totalMemoryUsage / _executionCount : 0;

        /// <inheritdoc/>
        public long PeakMemoryUsage => _peakMemoryUsage;

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, IStageMetrics> StageMetrics
            => _stageMetrics.ToDictionary(kvp => kvp.Key, kvp => (IStageMetrics)kvp.Value);

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, double> CustomMetrics => _customMetrics;

        /// <inheritdoc/>
        public IReadOnlyList<AbsTimeSeriesMetric> TimeSeries
        {
            get
            {
                var list = new List<CoreTimeSeriesMetric>();
                while (_timeSeries.TryDequeue(out var metric))
                {
                    list.Add(metric);
                }

                // Keep only recent metrics (last 1000)
                var recent = list.OrderByDescending(m => m.Timestamp).Take(1000).ToList();

                // Re-enqueue the recent ones
                foreach (var metric in recent)
                {
                    _timeSeries.Enqueue(metric);
                }

                // Convert to Abstractions type
                var groupedMetrics = recent.GroupBy(m => m.MetricName);
                var result = new List<AbsTimeSeriesMetric>();

                foreach (var group in groupedMetrics)
                {
                    var values = group.OrderBy(m => m.Timestamp)
                        .Select(m => new TimestampedValue<double>(m.Timestamp, m.Value))
                        .ToList();

                    result.Add(new AbsTimeSeriesMetric
                    {
                        Name = group.Key,
                        Values = values,
                        Unit = "count" // Default unit
                    });
                }

                return result;
            }
        }

        /// <inheritdoc/>
        public int StageCount => _stageMetrics.Count;

        /// <inheritdoc/>
        public double ItemThroughputPerSecond
            => _totalExecutionTime.TotalSeconds > 0

                ? _totalItemsProcessed / _totalExecutionTime.TotalSeconds

                : 0;

        /// <inheritdoc/>
        public double CacheHitRatio
            => _totalCacheRequests > 0

                ? (double)_cacheHits / _totalCacheRequests

                : 0;

        /// <summary>
        /// Records a pipeline execution.
        /// </summary>
        public void RecordExecution(PipelineExecutionMetrics metrics, bool success)
        {
            lock (_lock)
            {
                _executions.Enqueue(metrics);
                _executionCount++;

                if (success)
                {
                    _successfulExecutionCount++;
                }
                else
                {
                    _failedExecutionCount++;
                }

                _totalExecutionTime = _totalExecutionTime.Add(metrics.Duration);

                if (metrics.Duration < _minExecutionTime)
                {
                    _minExecutionTime = metrics.Duration;
                }

                if (metrics.Duration > _maxExecutionTime)
                {
                    _maxExecutionTime = metrics.Duration;
                }

                _totalMemoryUsage += metrics.MemoryMetrics?.TotalMemoryAllocated ?? metrics.MemoryUsage;

                if ((metrics.MemoryMetrics?.PeakMemoryUsage ?? metrics.MemoryUsage) > _peakMemoryUsage)
                {
                    _peakMemoryUsage = metrics.MemoryMetrics?.PeakMemoryUsage ?? metrics.MemoryUsage;
                }

                // Record stage metrics
                foreach (var (stageId, duration) in metrics.StageExecutionTimes)
                {
                    var stageMetrics = _stageMetrics.GetOrAdd(stageId, _ => new StageMetrics(stageId));
                    stageMetrics.RecordExecution(TimeSpan.FromMilliseconds(duration), success);
                }

                // Record time series metrics
                RecordTimeSeriesMetric("ExecutionTime", metrics.Duration.TotalMilliseconds, metrics.StartTime.DateTime);
                RecordTimeSeriesMetric("MemoryUsage", metrics.MemoryMetrics?.TotalMemoryAllocated ?? metrics.MemoryUsage, metrics.StartTime.DateTime);
                RecordTimeSeriesMetric("ComputeUtilization", metrics.ComputeUtilization, metrics.StartTime.DateTime);
                RecordTimeSeriesMetric("MemoryBandwidthUtilization", metrics.MemoryBandwidthUtilization, metrics.StartTime.DateTime);
            }
        }

        /// <summary>
        /// Records a custom metric.
        /// </summary>
        public void RecordCustomMetric(string name, double value)
        {
            _ = _customMetrics.AddOrUpdate(name, value, (_, _) => value);
            RecordTimeSeriesMetric(name, value, DateTime.UtcNow);
        }

        /// <summary>
        /// Records cache access statistics.
        /// </summary>
        public void RecordCacheAccess(bool hit)
        {
            lock (_lock)
            {
                _totalCacheRequests++;
                if (hit)
                {
                    _cacheHits++;
                }
            }


            RecordTimeSeriesMetric("CacheHitRate", CacheHitRatio, DateTime.UtcNow);
        }

        /// <summary>
        /// Records items processed for throughput calculation.
        /// </summary>
        public void RecordItemsProcessed(long itemCount)
        {
            lock (_lock)
            {
                _totalItemsProcessed += itemCount;
            }


            RecordTimeSeriesMetric("ItemThroughput", ItemThroughputPerSecond, DateTime.UtcNow);
        }

        /// <inheritdoc/>
        public void Reset()
        {
            lock (_lock)
            {
                while (_executions.TryDequeue(out _))
                {
                }
                _stageMetrics.Clear();
                while (_timeSeries.TryDequeue(out _))
                {
                }
                _customMetrics.Clear();

                _executionCount = 0;
                _successfulExecutionCount = 0;
                _failedExecutionCount = 0;
                _totalExecutionTime = TimeSpan.Zero;
                _minExecutionTime = TimeSpan.MaxValue;
                _maxExecutionTime = TimeSpan.MinValue;
                _totalMemoryUsage = 0;
                _peakMemoryUsage = 0;
                _totalItemsProcessed = 0;
                _totalCacheRequests = 0;
                _cacheHits = 0;
            }
        }

        /// <inheritdoc/>
        public string Export(MetricsExportFormat format)
        {
            return format switch
            {
                MetricsExportFormat.Json => ExportJson(),
                MetricsExportFormat.Csv => ExportCsv(),
                MetricsExportFormat.Prometheus => ExportPrometheus(),
                MetricsExportFormat.OpenTelemetry => ExportOpenTelemetry(),
                _ => throw new ArgumentException($"Unsupported export format: {format}")
            };
        }

        private void RecordTimeSeriesMetric(string name, double value, DateTime timestamp)
        {
            _timeSeries.Enqueue(new CoreTimeSeriesMetric
            {
                MetricName = name,
                Value = value,
                Timestamp = timestamp
            });
        }

        private string ExportJson()
        {
            var data = new
            {
                PipelineId,
                ExecutionCount,
                SuccessfulExecutionCount,
                FailedExecutionCount,
                AverageTimeMs = AverageExecutionTime.TotalMilliseconds,
                MinTimeMs = MinExecutionTime.TotalMilliseconds,
                MaxTimeMs = MaxExecutionTime.TotalMilliseconds,
                TotalExecutionTime = TotalExecutionTime.TotalMilliseconds,
                Throughput,
                SuccessRate,
                AverageMemoryUsage,
                PeakMemoryUsage,
                StageMetrics = StageMetrics.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        kvp.Value.ExecutionCount,
                        kvp.Value.ErrorCount,
                        kvp.Value.SuccessRate,
                        AverageTimeMs = kvp.Value.AverageExecutionTime.TotalMilliseconds,
                        MinTimeMs = kvp.Value.MinExecutionTime.TotalMilliseconds,
                        MaxTimeMs = kvp.Value.MaxExecutionTime.TotalMilliseconds,
                        TotalExecutionTime = kvp.Value.TotalExecutionTime.TotalMilliseconds,
                        kvp.Value.AverageMemoryUsage,
                        kvp.Value.CustomMetrics
                    }),
                CustomMetrics,
                TimeSeries = TimeSeries.Select(ts => new
                {
                    Name = ts.Name,
                    Values = ts.Values.Select(v => new { v.Timestamp, v.Value }),
                    ts.Unit
                }).ToList()
            };

            return JsonSerializer.Serialize(data, DotComputeJsonContext.Default.PipelineMetricsData);
        }

        private string ExportCsv()
        {
            var lines = new List<string>
        {
            "Metric,Value,Unit",
            $"Pipeline ID,{PipelineId},",
            $"Execution Count,{ExecutionCount},count",
            $"Successful Executions,{SuccessfulExecutionCount},count",
            $"Failed Executions,{FailedExecutionCount},count",
            $"Average Execution Time,{AverageExecutionTime.TotalMilliseconds},ms",
            $"Min Execution Time,{MinExecutionTime.TotalMilliseconds},ms",
            $"Max Execution Time,{MaxExecutionTime.TotalMilliseconds},ms",
            $"Total Execution Time,{TotalExecutionTime.TotalMilliseconds},ms",
            $"Throughput,{Throughput},executions/sec",
            $"Success Rate,{SuccessRate:P},percentage",
            $"Average Memory Usage,{AverageMemoryUsage},bytes",
            $"Peak Memory Usage,{PeakMemoryUsage},bytes"
        };

            foreach (var customMetric in CustomMetrics)
            {
                lines.Add($"{customMetric.Key},{customMetric.Value},");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private string ExportPrometheus()
        {
            var lines = new List<string>
        {
            $"# TYPE dotcompute_pipeline_executions_total counter",
            $"dotcompute_pipeline_executions_total{{pipeline_id=\"{PipelineId}\"}} {ExecutionCount}",
            $"# TYPE dotcompute_pipeline_execution_duration_seconds histogram",
            $"dotcompute_pipeline_execution_duration_seconds_sum{{pipeline_id=\"{PipelineId}\"}} {TotalExecutionTime.TotalSeconds}",
            $"dotcompute_pipeline_execution_duration_seconds_count{{pipeline_id=\"{PipelineId}\"}} {ExecutionCount}",
            $"# TYPE dotcompute_pipeline_success_rate gauge",
            $"dotcompute_pipeline_success_rate{{pipeline_id=\"{PipelineId}\"}} {SuccessRate}",
            $"# TYPE dotcompute_pipeline_memory_usage_bytes gauge",
            $"dotcompute_pipeline_memory_usage_bytes{{pipeline_id=\"{PipelineId}\",type=\"average\"}} {AverageMemoryUsage}",
            $"dotcompute_pipeline_memory_usage_bytes{{pipeline_id=\"{PipelineId}\",type=\"peak\"}} {PeakMemoryUsage}"
        };

            foreach (var customMetric in CustomMetrics)
            {
                var metricName = customMetric.Key.ToUpper(CultureInfo.InvariantCulture).Replace(' ', '_');
                lines.Add($"# TYPE dotcompute_pipeline_custom_{metricName} gauge");
                lines.Add($"dotcompute_pipeline_custom_{metricName}{{pipeline_id=\"{PipelineId}\"}} {customMetric.Value}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private string ExportOpenTelemetry()
        {
            // Export metrics in OpenTelemetry-compatible JSON format
            // This structured format can be directly consumed by OpenTelemetry collectors
            var metrics = new
            {
                resource = new
                {
                    attributes = new
                    {
                        service_name = "dotcompute",
                        pipeline_id = PipelineId
                    }
                },
                metrics = new object[]
                {
                new
                {
                    name = "dotcompute.pipeline.executions",
                    description = "Total number of pipeline executions",
                    unit = "1",
                    gauge = new
                    {
                        data_points = new[]
                        {
                            new
                            {
                                value = ExecutionCount,
                                time_unix_nano = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000,
                                attributes = new { pipeline_id = PipelineId }
                            }
                        }
                    }
                },
                new
                {
                    name = "dotcompute.pipeline.execution_duration",
                    description = "Pipeline execution duration",
                    unit = "ms",
                    histogram = new
                    {
                        data_points = new[]
                        {
                            new
                            {
                                count = ExecutionCount,
                                sum = TotalExecutionTime.TotalMilliseconds,
                                time_unix_nano = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000,
                                attributes = new { pipeline_id = PipelineId }
                            }
                        }
                    }
                }
                }
            };

            return JsonSerializer.Serialize(metrics, DotComputeJsonContext.Default.OpenTelemetryMetricsData);
        }
    }

    /// <summary>
    /// Implementation of stage metrics.
    /// </summary>
    public sealed class StageMetrics(string stageId, string? stageName = null) : IStageMetrics
    {
        private readonly Lock _lock = new();
        private readonly ConcurrentDictionary<string, double> _customMetrics = new();

        private long _executionCount;
        private long _errorCount;
        private TimeSpan _totalExecutionTime;
        private TimeSpan _minExecutionTime = TimeSpan.MaxValue;
        private TimeSpan _maxExecutionTime = TimeSpan.MinValue;
        private long _totalMemoryUsage;
        /// <summary>
        /// Gets or sets the stage identifier.
        /// </summary>
        /// <value>The stage id.</value>

        public string StageId { get; } = stageId;

        /// <inheritdoc/>
        public string StageName { get; } = stageName ?? stageId;

        /// <inheritdoc/>
        public long ExecutionCount => _executionCount;

        /// <inheritdoc/>
        public TimeSpan AverageExecutionTime
            => _executionCount > 0
            ? TimeSpan.FromTicks(_totalExecutionTime.Ticks / _executionCount)
            : TimeSpan.Zero;

        /// <inheritdoc/>
        public TimeSpan MinExecutionTime
            => _executionCount > 0 ? _minExecutionTime : TimeSpan.Zero;

        /// <inheritdoc/>
        public TimeSpan MaxExecutionTime
            => _executionCount > 0 ? _maxExecutionTime : TimeSpan.Zero;

        /// <inheritdoc/>
        public TimeSpan TotalExecutionTime => _totalExecutionTime;

        /// <inheritdoc/>
        public long ErrorCount => _errorCount;

        /// <inheritdoc/>
        public double SuccessRate
            => _executionCount > 0
            ? (double)(_executionCount - _errorCount) / _executionCount
            : 0;

        /// <inheritdoc/>
        public long AverageMemoryUsage
            => _executionCount > 0 ? _totalMemoryUsage / _executionCount : 0;

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, double> CustomMetrics => _customMetrics;
        /// <summary>
        /// Performs record execution.
        /// </summary>
        /// <param name="duration">The duration.</param>
        /// <param name="success">The success.</param>

        public void RecordExecution(TimeSpan duration, bool success)
        {
            lock (_lock)
            {
                _executionCount++;
                _totalExecutionTime = _totalExecutionTime.Add(duration);

                if (duration < _minExecutionTime)
                {
                    _minExecutionTime = duration;
                }

                if (duration > _maxExecutionTime)
                {
                    _maxExecutionTime = duration;
                }

                if (!success)
                {
                    _errorCount++;
                }
            }
        }
        /// <summary>
        /// Performs record memory usage.
        /// </summary>
        /// <param name="bytes">The bytes.</param>

        public void RecordMemoryUsage(long bytes)
        {
            lock (_lock)
            {
                _totalMemoryUsage += bytes;
            }
        }
        /// <summary>
        /// Performs record custom metric.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>

        public void RecordCustomMetric(string name, double value) => _customMetrics.AddOrUpdate(name, value, (_, _) => value);
    }
}
