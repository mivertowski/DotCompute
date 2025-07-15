// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DotCompute.Core.Pipelines;

/// <summary>
/// Implementation of pipeline performance metrics.
/// </summary>
internal sealed class PipelineMetrics : IPipelineMetrics
{
    private readonly ConcurrentQueue<PipelineExecutionMetrics> _executions;
    private readonly ConcurrentDictionary<string, StageMetrics> _stageMetrics;
    private readonly ConcurrentQueue<TimeSeriesMetric> _timeSeries;
    private readonly ConcurrentDictionary<string, double> _customMetrics;
    private readonly object _lock = new();

    private long _executionCount;
    private long _successfulExecutionCount;
    private long _failedExecutionCount;
    private TimeSpan _totalExecutionTime;
    private TimeSpan _minExecutionTime = TimeSpan.MaxValue;
    private TimeSpan _maxExecutionTime = TimeSpan.MinValue;
    private long _totalMemoryUsage;
    private long _peakMemoryUsage;

    public PipelineMetrics(string pipelineId)
    {
        PipelineId = pipelineId;
        _executions = new ConcurrentQueue<PipelineExecutionMetrics>();
        _stageMetrics = new ConcurrentDictionary<string, StageMetrics>();
        _timeSeries = new ConcurrentQueue<TimeSeriesMetric>();
        _customMetrics = new ConcurrentDictionary<string, double>();
    }

    /// <inheritdoc/>
    public string PipelineId { get; }

    /// <inheritdoc/>
    public long ExecutionCount => _executionCount;

    /// <inheritdoc/>
    public long SuccessfulExecutionCount => _successfulExecutionCount;

    /// <inheritdoc/>
    public long FailedExecutionCount => _failedExecutionCount;

    /// <inheritdoc/>
    public TimeSpan AverageExecutionTime => 
        _executionCount > 0 ? 
        TimeSpan.FromTicks(_totalExecutionTime.Ticks / _executionCount) : 
        TimeSpan.Zero;

    /// <inheritdoc/>
    public TimeSpan MinExecutionTime => 
        _executionCount > 0 ? _minExecutionTime : TimeSpan.Zero;

    /// <inheritdoc/>
    public TimeSpan MaxExecutionTime => 
        _executionCount > 0 ? _maxExecutionTime : TimeSpan.Zero;

    /// <inheritdoc/>
    public TimeSpan TotalExecutionTime => _totalExecutionTime;

    /// <inheritdoc/>
    public double Throughput => 
        _totalExecutionTime.TotalSeconds > 0 ? 
        _executionCount / _totalExecutionTime.TotalSeconds : 
        0;

    /// <inheritdoc/>
    public double SuccessRate => 
        _executionCount > 0 ? 
        (double)_successfulExecutionCount / _executionCount : 
        0;

    /// <inheritdoc/>
    public long AverageMemoryUsage => 
        _executionCount > 0 ? _totalMemoryUsage / _executionCount : 0;

    /// <inheritdoc/>
    public long PeakMemoryUsage => _peakMemoryUsage;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IStageMetrics> StageMetrics => 
        _stageMetrics.ToDictionary(kvp => kvp.Key, kvp => (IStageMetrics)kvp.Value);

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, double> CustomMetrics => _customMetrics;

    /// <inheritdoc/>
    public IReadOnlyList<TimeSeriesMetric> TimeSeries
    {
        get
        {
            var list = new List<TimeSeriesMetric>();
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

            return recent.OrderBy(m => m.Timestamp).ToList();
        }
    }

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

            _totalMemoryUsage += metrics.MemoryUsage.AllocatedBytes;
            
            if (metrics.MemoryUsage.PeakBytes > _peakMemoryUsage)
            {
                _peakMemoryUsage = metrics.MemoryUsage.PeakBytes;
            }

            // Record stage metrics
            foreach (var (stageId, duration) in metrics.StageExecutionTimes)
            {
                var stageMetrics = _stageMetrics.GetOrAdd(stageId, _ => new StageMetrics(stageId));
                stageMetrics.RecordExecution(duration, success);
            }

            // Record time series metrics
            RecordTimeSeriesMetric("ExecutionTime", metrics.Duration.TotalMilliseconds, metrics.StartTime);
            RecordTimeSeriesMetric("MemoryUsage", metrics.MemoryUsage.AllocatedBytes, metrics.StartTime);
            RecordTimeSeriesMetric("ComputeUtilization", metrics.ComputeUtilization, metrics.StartTime);
            RecordTimeSeriesMetric("MemoryBandwidthUtilization", metrics.MemoryBandwidthUtilization, metrics.StartTime);
        }
    }

    /// <summary>
    /// Records a custom metric.
    /// </summary>
    public void RecordCustomMetric(string name, double value)
    {
        _customMetrics.AddOrUpdate(name, value, (_, _) => value);
        RecordTimeSeriesMetric(name, value, DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        lock (_lock)
        {
            while (_executions.TryDequeue(out _)) { }
            _stageMetrics.Clear();
            while (_timeSeries.TryDequeue(out _)) { }
            _customMetrics.Clear();

            _executionCount = 0;
            _successfulExecutionCount = 0;
            _failedExecutionCount = 0;
            _totalExecutionTime = TimeSpan.Zero;
            _minExecutionTime = TimeSpan.MaxValue;
            _maxExecutionTime = TimeSpan.MinValue;
            _totalMemoryUsage = 0;
            _peakMemoryUsage = 0;
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
        _timeSeries.Enqueue(new TimeSeriesMetric
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
            AverageExecutionTime = AverageExecutionTime.TotalMilliseconds,
            MinExecutionTime = MinExecutionTime.TotalMilliseconds,
            MaxExecutionTime = MaxExecutionTime.TotalMilliseconds,
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
                    AverageExecutionTime = kvp.Value.AverageExecutionTime.TotalMilliseconds,
                    MinExecutionTime = kvp.Value.MinExecutionTime.TotalMilliseconds,
                    MaxExecutionTime = kvp.Value.MaxExecutionTime.TotalMilliseconds,
                    TotalExecutionTime = kvp.Value.TotalExecutionTime.TotalMilliseconds,
                    kvp.Value.AverageMemoryUsage,
                    kvp.Value.CustomMetrics
                }),
            CustomMetrics,
            TimeSeries = TimeSeries.Select(ts => new
            {
                ts.MetricName,
                ts.Value,
                Timestamp = ts.Timestamp.ToString("O"),
                ts.Labels
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
        var lines = new List<string>();

        lines.Add($"# TYPE dotcompute_pipeline_executions_total counter");
        lines.Add($"dotcompute_pipeline_executions_total{{pipeline_id=\"{PipelineId}\"}} {ExecutionCount}");

        lines.Add($"# TYPE dotcompute_pipeline_execution_duration_seconds histogram");
        lines.Add($"dotcompute_pipeline_execution_duration_seconds_sum{{pipeline_id=\"{PipelineId}\"}} {TotalExecutionTime.TotalSeconds}");
        lines.Add($"dotcompute_pipeline_execution_duration_seconds_count{{pipeline_id=\"{PipelineId}\"}} {ExecutionCount}");

        lines.Add($"# TYPE dotcompute_pipeline_success_rate gauge");
        lines.Add($"dotcompute_pipeline_success_rate{{pipeline_id=\"{PipelineId}\"}} {SuccessRate}");

        lines.Add($"# TYPE dotcompute_pipeline_memory_usage_bytes gauge");
        lines.Add($"dotcompute_pipeline_memory_usage_bytes{{pipeline_id=\"{PipelineId}\",type=\"average\"}} {AverageMemoryUsage}");
        lines.Add($"dotcompute_pipeline_memory_usage_bytes{{pipeline_id=\"{PipelineId}\",type=\"peak\"}} {PeakMemoryUsage}");

        foreach (var customMetric in CustomMetrics)
        {
            var metricName = customMetric.Key.ToLowerInvariant().Replace(' ', '_');
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
internal sealed class StageMetrics : IStageMetrics
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, double> _customMetrics;

    private long _executionCount;
    private long _errorCount;
    private TimeSpan _totalExecutionTime;
    private TimeSpan _minExecutionTime = TimeSpan.MaxValue;
    private TimeSpan _maxExecutionTime = TimeSpan.MinValue;
    private long _totalMemoryUsage;

    public StageMetrics(string stageId)
    {
        StageId = stageId;
        _customMetrics = new ConcurrentDictionary<string, double>();
    }

    public string StageId { get; }

    /// <inheritdoc/>
    public long ExecutionCount => _executionCount;

    /// <inheritdoc/>
    public TimeSpan AverageExecutionTime => 
        _executionCount > 0 ? 
        TimeSpan.FromTicks(_totalExecutionTime.Ticks / _executionCount) : 
        TimeSpan.Zero;

    /// <inheritdoc/>
    public TimeSpan MinExecutionTime => 
        _executionCount > 0 ? _minExecutionTime : TimeSpan.Zero;

    /// <inheritdoc/>
    public TimeSpan MaxExecutionTime => 
        _executionCount > 0 ? _maxExecutionTime : TimeSpan.Zero;

    /// <inheritdoc/>
    public TimeSpan TotalExecutionTime => _totalExecutionTime;

    /// <inheritdoc/>
    public long ErrorCount => _errorCount;

    /// <inheritdoc/>
    public double SuccessRate => 
        _executionCount > 0 ? 
        (double)(_executionCount - _errorCount) / _executionCount : 
        0;

    /// <inheritdoc/>
    public long AverageMemoryUsage => 
        _executionCount > 0 ? _totalMemoryUsage / _executionCount : 0;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, double> CustomMetrics => _customMetrics;

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

    public void RecordMemoryUsage(long bytes)
    {
        lock (_lock)
        {
            _totalMemoryUsage += bytes;
        }
    }

    public void RecordCustomMetric(string name, double value)
    {
        _customMetrics.AddOrUpdate(name, value, (_, _) => value);
    }
}