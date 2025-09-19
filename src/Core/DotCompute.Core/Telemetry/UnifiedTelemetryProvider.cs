// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using DotCompute.Abstractions.Interfaces.Telemetry;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Core.Telemetry.Implementation;
using TelemetryConfiguration = DotCompute.Abstractions.Interfaces.Telemetry.TelemetryConfiguration;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Unified telemetry provider implementation.
/// This consolidates all telemetry functionality.
/// </summary>
public class UnifiedTelemetryProvider : ITelemetryProvider
{
    private readonly TelemetryConfiguration _configuration;
    private readonly Meter _meter;
    private readonly ActivitySource _activitySource;
    private readonly ConcurrentDictionary<string, Counter<long>> _counters = new();
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new();
    private readonly ConcurrentDictionary<string, Gauge<double>> _gauges = new();
    private readonly ConcurrentDictionary<string, ObservableGauge<double>> _observableGauges = new();
    private bool _disposed;


    public UnifiedTelemetryProvider(TelemetryConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _meter = new Meter(_configuration.ServiceName, _configuration.ServiceVersion);
        _activitySource = new ActivitySource(_configuration.ServiceName, _configuration.ServiceVersion);

        // Initialize core metrics
        InitializeStandardMetrics();
    }


    public void RecordMetric(string name, double value, IDictionary<string, object?>? tags = null)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var histogram = _histograms.GetOrAdd(name, _ => _meter.CreateHistogram<double>(name, "units", "Custom metric"));

            if (tags != null && tags.Count > 0)
            {
                var tagList = new TagList();
                foreach (var tag in tags)
                {
                    tagList.Add(tag.Key, tag.Value);
                }
                histogram.Record(value, tagList);
            }
            else
            {
                histogram.Record(value);
            }
        }
        catch (Exception ex) when (!_configuration.ThrowOnTelemetryErrors)
        {
            // Log error silently if configured to not throw
            Debug.WriteLine($"Failed to record metric {name}: {ex.Message}");
        }
    }


    public void IncrementCounter(string name, long increment = 1, IDictionary<string, object?>? tags = null)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var counter = _counters.GetOrAdd(name, _ => _meter.CreateCounter<long>(name, "units", "Counter metric"));

            if (tags != null && tags.Count > 0)
            {
                var tagList = new TagList();
                foreach (var tag in tags)
                {
                    tagList.Add(tag.Key, tag.Value);
                }
                counter.Add(increment, tagList);
            }
            else
            {
                counter.Add(increment);
            }
        }
        catch (Exception ex) when (!_configuration.ThrowOnTelemetryErrors)
        {
            Debug.WriteLine($"Failed to increment counter {name}: {ex.Message}");
        }
    }


    public void RecordHistogram(string name, double value, IDictionary<string, object?>? tags = null)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var histogram = _histograms.GetOrAdd(name, _ => _meter.CreateHistogram<double>(name, "units", "Histogram metric"));

            if (tags != null && tags.Count > 0)
            {
                var tagList = new TagList();
                foreach (var tag in tags)
                {
                    tagList.Add(tag.Key, tag.Value);
                }
                histogram.Record(value, tagList);
            }
            else
            {
                histogram.Record(value);
            }
        }
        catch (Exception ex) when (!_configuration.ThrowOnTelemetryErrors)
        {
            Debug.WriteLine($"Failed to record histogram {name}: {ex.Message}");
        }
    }


    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        if (_disposed)
        {
            return null;
        }


        try
        {
            var activity = _activitySource.StartActivity(name, kind);

            // Add standard tags
            activity?.SetTag("service.name", _configuration.ServiceName);
            activity?.SetTag("service.version", _configuration.ServiceVersion);
            activity?.SetTag("dotcompute.component", "telemetry");

            return activity;
        }
        catch (Exception ex) when (!_configuration.ThrowOnTelemetryErrors)
        {
            Debug.WriteLine($"Failed to start activity {name}: {ex.Message}");
            return null;
        }
    }


    public void RecordEvent(string name, IDictionary<string, object?>? attributes = null)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            using var activity = _activitySource.StartActivity($"event.{name}");

            if (activity != null)
            {
                activity.SetTag("event.name", name);
                activity.SetTag("event.timestamp", DateTimeOffset.UtcNow.ToString("O"));

                if (attributes != null)
                {
                    foreach (var attr in attributes)
                    {
                        activity.SetTag($"event.{attr.Key}", attr.Value?.ToString());
                    }
                }

                // Add event to activity
                activity.AddEvent(new ActivityEvent(name, DateTimeOffset.UtcNow, new ActivityTagsCollection(
                    attributes?.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value)) ?? Enumerable.Empty<KeyValuePair<string, object?>>())
                ));
            }
        }
        catch (Exception ex) when (!_configuration.ThrowOnTelemetryErrors)
        {
            Debug.WriteLine($"Failed to record event {name}: {ex.Message}");
        }
    }


    public Abstractions.Interfaces.Telemetry.IOperationTimer StartTimer(string operationName, IDictionary<string, object?>? tags = null)
    {
        if (_disposed)
        {
            return new OperationTimer(operationName, tags);
        }


        try
        {
            // Record timer start as an event
            IncrementCounter("operation.timer.started", 1, new Dictionary<string, object?> { ["operation"] = operationName });

            var timer = new ProductionOperationTimer(operationName, tags, this);
            return timer;
        }
        catch (Exception ex) when (!_configuration.ThrowOnTelemetryErrors)
        {
            Debug.WriteLine($"Failed to start timer for {operationName}: {ex.Message}");
            return new OperationTimer(operationName, tags);
        }
    }


    public void RecordMemoryAllocation(long bytes, string? allocationType = null)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var tags = new Dictionary<string, object?>();
            if (!string.IsNullOrEmpty(allocationType))
            {
                tags["allocation.type"] = allocationType;
            }

            // Record allocation size as histogram
            RecordHistogram("memory.allocation.bytes", bytes, tags);

            // Increment allocation counter
            IncrementCounter("memory.allocation.count", 1, tags);

            // Track total allocated memory
            RecordMetric("memory.total_allocated.bytes", bytes, tags);
        }
        catch (Exception ex) when (!_configuration.ThrowOnTelemetryErrors)
        {
            Debug.WriteLine($"Failed to record memory allocation: {ex.Message}");
        }
    }


    public void RecordGarbageCollection(int generation, TimeSpan duration, long memoryBefore, long memoryAfter)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var tags = new Dictionary<string, object?>
            {
                ["gc.generation"] = generation,
                ["gc.type"] = generation switch
                {
                    0 => "gen0",
                    1 => "gen1",
                    2 => "gen2",
                    _ => "unknown"
                }
            };

            // Record GC duration
            RecordHistogram("gc.duration.ms", duration.TotalMilliseconds, tags);

            // Record memory changes
            RecordHistogram("gc.memory.before.bytes", memoryBefore, tags);
            RecordHistogram("gc.memory.after.bytes", memoryAfter, tags);
            RecordHistogram("gc.memory.freed.bytes", Math.Max(0, memoryBefore - memoryAfter), tags);

            // Increment GC counter
            IncrementCounter("gc.count", 1, tags);
        }
        catch (Exception ex) when (!_configuration.ThrowOnTelemetryErrors)
        {
            Debug.WriteLine($"Failed to record garbage collection: {ex.Message}");
        }
    }


    public void RecordAcceleratorUtilization(string acceleratorType, double utilization, long memoryUsed)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var tags = new Dictionary<string, object?>
            {
                ["accelerator.type"] = acceleratorType,
                ["accelerator.category"] = acceleratorType.ToLowerInvariant() switch
                {
                    "cuda" or "nvidia" => "gpu",
                    "metal" => "gpu",
                    "opencl" => "gpu",
                    "cpu" => "cpu",
                    _ => "unknown"
                }
            };

            // Record utilization as a gauge (percentage)
            RecordMetric("accelerator.utilization.percent", Math.Clamp(utilization * 100, 0, 100), tags);

            // Record memory usage
            RecordMetric("accelerator.memory.used.bytes", memoryUsed, tags);

            // Calculate utilization level for counter
            var utilizationLevel = utilization switch
            {
                >= 0.9 => "high",
                >= 0.5 => "medium",
                >= 0.1 => "low",
                _ => "idle"
            };

            var utilizationTags = new Dictionary<string, object?>(tags)
            {
                ["utilization.level"] = utilizationLevel
            };

            IncrementCounter("accelerator.utilization.samples", 1, utilizationTags);
        }
        catch (Exception ex) when (!_configuration.ThrowOnTelemetryErrors)
        {
            Debug.WriteLine($"Failed to record accelerator utilization: {ex.Message}");
        }
    }


    public void RecordKernelExecution(string kernelName, TimeSpan duration, long operationCount)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var tags = new Dictionary<string, object?>
            {
                ["kernel.name"] = kernelName,
                ["kernel.category"] = ExtractKernelCategory(kernelName)
            };

            // Record execution duration
            RecordHistogram("kernel.execution.duration.ms", duration.TotalMilliseconds, tags);

            // Record operation count
            RecordHistogram("kernel.execution.operations", operationCount, tags);

            // Calculate operations per second
            if (duration.TotalSeconds > 0)
            {
                var opsPerSecond = operationCount / duration.TotalSeconds;
                RecordMetric("kernel.execution.ops_per_second", opsPerSecond, tags);
            }

            // Increment execution counter
            IncrementCounter("kernel.execution.count", 1, tags);

            // Performance classification
            var performanceLevel = duration.TotalMilliseconds switch
            {
                < 1.0 => "very_fast",
                < 10.0 => "fast",
                < 100.0 => "normal",
                < 1000.0 => "slow",
                _ => "very_slow"
            };

            var perfTags = new Dictionary<string, object?>(tags)
            {
                ["performance.level"] = performanceLevel
            };

            IncrementCounter("kernel.performance.classification", 1, perfTags);
        }
        catch (Exception ex) when (!_configuration.ThrowOnTelemetryErrors)
        {
            Debug.WriteLine($"Failed to record kernel execution: {ex.Message}");
        }
    }


    public void RecordMemoryTransfer(string direction, long bytes, TimeSpan duration)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var tags = new Dictionary<string, object?>
            {
                ["transfer.direction"] = direction,
                ["transfer.type"] = direction.ToLowerInvariant() switch
                {
                    "host_to_device" or "h2d" => "upload",
                    "device_to_host" or "d2h" => "download",
                    "device_to_device" or "d2d" => "peer",
                    _ => "unknown"
                }
            };

            // Record transfer size
            RecordHistogram("memory.transfer.bytes", bytes, tags);

            // Record transfer duration
            RecordHistogram("memory.transfer.duration.ms", duration.TotalMilliseconds, tags);

            // Calculate bandwidth (MB/s)
            if (duration.TotalSeconds > 0)
            {
                var bandwidthMBps = (bytes / (1024.0 * 1024.0)) / duration.TotalSeconds;
                RecordMetric("memory.transfer.bandwidth.mbps", bandwidthMBps, tags);

                // Bandwidth classification
                var bandwidthLevel = bandwidthMBps switch
                {
                    >= 10000 => "very_high", // > 10 GB/s
                    >= 1000 => "high",       // > 1 GB/s
                    >= 100 => "medium",      // > 100 MB/s
                    >= 10 => "low",          // > 10 MB/s
                    _ => "very_low"
                };

                var bwTags = new Dictionary<string, object?>(tags)
                {
                    ["bandwidth.level"] = bandwidthLevel
                };

                IncrementCounter("memory.transfer.bandwidth.classification", 1, bwTags);
            }

            // Increment transfer counter
            IncrementCounter("memory.transfer.count", 1, tags);
        }
        catch (Exception ex) when (!_configuration.ThrowOnTelemetryErrors)
        {
            Debug.WriteLine($"Failed to record memory transfer: {ex.Message}");
        }
    }


    public Meter GetMeter(string name, string? version = null) => new(name, version);

    private void InitializeStandardMetrics()
    {
        // Initialize commonly used metrics to avoid creation overhead
        _counters.TryAdd("operation.timer.started", _meter.CreateCounter<long>("operation.timer.started", "count", "Number of operation timers started"));
        _counters.TryAdd("memory.allocation.count", _meter.CreateCounter<long>("memory.allocation.count", "count", "Number of memory allocations"));
        _counters.TryAdd("gc.count", _meter.CreateCounter<long>("gc.count", "count", "Number of garbage collections"));
        _counters.TryAdd("accelerator.utilization.samples", _meter.CreateCounter<long>("accelerator.utilization.samples", "count", "Number of accelerator utilization samples"));
        _counters.TryAdd("kernel.execution.count", _meter.CreateCounter<long>("kernel.execution.count", "count", "Number of kernel executions"));
        _counters.TryAdd("memory.transfer.count", _meter.CreateCounter<long>("memory.transfer.count", "count", "Number of memory transfers"));

        _histograms.TryAdd("memory.allocation.bytes", _meter.CreateHistogram<double>("memory.allocation.bytes", "bytes", "Memory allocation size"));
        _histograms.TryAdd("kernel.execution.duration.ms", _meter.CreateHistogram<double>("kernel.execution.duration.ms", "ms", "Kernel execution duration"));
        _histograms.TryAdd("memory.transfer.bytes", _meter.CreateHistogram<double>("memory.transfer.bytes", "bytes", "Memory transfer size"));
        _histograms.TryAdd("memory.transfer.duration.ms", _meter.CreateHistogram<double>("memory.transfer.duration.ms", "ms", "Memory transfer duration"));
    }

    private static string ExtractKernelCategory(string kernelName)
    {
        var name = kernelName.ToLowerInvariant();
        return name switch
        {
            var n when n.Contains("add") || n.Contains("sum") => "arithmetic",
            var n when n.Contains("mul") || n.Contains("multiply") => "arithmetic",
            var n when n.Contains("matrix") || n.Contains("gemm") => "linear_algebra",
            var n when n.Contains("reduce") || n.Contains("scan") => "reduction",
            var n when n.Contains("sort") => "sorting",
            var n when n.Contains("fft") => "transform",
            var n when n.Contains("conv") || n.Contains("filter") => "convolution",
            var n when n.Contains("copy") || n.Contains("memcpy") => "memory",
            _ => "general"
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Dispose activity source
            _activitySource?.Dispose();

            // Dispose meter
            _meter?.Dispose();

            // Clear collections
            _counters.Clear();
            _histograms.Clear();
            _gauges.Clear();
            _observableGauges.Clear();
        }
    }
}

/// <summary>
/// Production-grade operation timer implementation with full telemetry integration.
/// </summary>
internal sealed class ProductionOperationTimer : Abstractions.Interfaces.Telemetry.IOperationTimer
{
    private readonly string _operationName;
    private readonly IDictionary<string, object?>? _tags;
    private readonly UnifiedTelemetryProvider _telemetryProvider;
    private readonly Stopwatch _stopwatch;
    private readonly Activity? _activity;

    public ProductionOperationTimer(string operationName, IDictionary<string, object?>? tags, UnifiedTelemetryProvider telemetryProvider)
    {
        _operationName = operationName;
        _tags = tags;
        _telemetryProvider = telemetryProvider;
        _stopwatch = Stopwatch.StartNew();
        _activity = _telemetryProvider.StartActivity($"timer.{operationName}");

        // Add tags to activity
        if (_activity != null && tags != null)
        {
            foreach (var tag in tags)
            {
                _activity.SetTag(tag.Key, tag.Value?.ToString());
            }
        }
    }

    public bool IsEnabled => true;
    public TimeSpan MinimumDurationThreshold => TimeSpan.Zero;

    public event EventHandler<OperationTimingEventArgs>? OperationCompleted;

    public ITimerHandle StartOperation(string operationName, string? operationId = null) => new TimerHandle(operationName, operationId ?? Guid.NewGuid().ToString());
    public IDisposable StartOperationScope(string operationName, string? operationId = null) => new TimerHandle(operationName, operationId ?? Guid.NewGuid().ToString());

    public (T result, TimeSpan duration) TimeOperation<T>(string operationName, Func<T> operation)
    {
        var sw = Stopwatch.StartNew();
        var result = operation();
        sw.Stop();
        RecordTiming(operationName, sw.Elapsed);
        return (result, sw.Elapsed);
    }

    public async Task<(T result, TimeSpan duration)> TimeOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        var sw = Stopwatch.StartNew();
        var result = await operation();
        sw.Stop();
        RecordTiming(operationName, sw.Elapsed);
        return (result, sw.Elapsed);
    }

    public TimeSpan TimeOperation(string operationName, Action operation)
    {
        var sw = Stopwatch.StartNew();
        operation();
        sw.Stop();
        RecordTiming(operationName, sw.Elapsed);
        return sw.Elapsed;
    }

    public async Task<TimeSpan> TimeOperationAsync(string operationName, Func<Task> operation)
    {
        var sw = Stopwatch.StartNew();
        await operation();
        sw.Stop();
        RecordTiming(operationName, sw.Elapsed);
        return sw.Elapsed;
    }

    public void RecordTiming(string operationName, TimeSpan duration, string? operationId = null, IDictionary<string, object>? metadata = null)
    {
        var tags = new Dictionary<string, object?> { ["operation"] = operationName };

        if (!string.IsNullOrEmpty(operationId))
        {
            tags["operation.id"] = operationId;
        }

        if (_tags != null)
        {
            foreach (var tag in _tags)
            {
                tags[tag.Key] = tag.Value;
            }
        }

        if (metadata != null)
        {
            foreach (var meta in metadata)
            {
                tags[meta.Key] = meta.Value;
            }
        }

        _telemetryProvider.RecordHistogram("operation.duration.ms", duration.TotalMilliseconds, tags);
    }

    public OperationStatistics? GetStatistics(string operationName) => null;
    public IDictionary<string, OperationStatistics> GetAllStatistics() => new Dictionary<string, OperationStatistics>();
    public void ClearStatistics(string operationName) { }
    public void ClearAllStatistics() { }
    public string ExportData(MetricsExportFormat format, Func<string, bool>? operationFilter = null) => string.Empty;
    public void SetEnabled(bool enabled) { }
    public void SetMinimumDurationThreshold(TimeSpan threshold) { }

    public void Stop()
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();

            // Record the operation timing
            RecordTiming(_operationName, _stopwatch.Elapsed);

            // Complete the activity
            _activity?.SetTag("duration.ms", _stopwatch.Elapsed.TotalMilliseconds);
            _activity?.SetStatus(ActivityStatusCode.Ok);

            // Fire completion event
            OperationCompleted?.Invoke(this, new OperationTimingEventArgs
            {
                OperationName = _operationName,
                OperationId = Guid.NewGuid().ToString(),
                Duration = _stopwatch.Elapsed,
                StartTime = DateTime.UtcNow.Subtract(_stopwatch.Elapsed),
                EndTime = DateTime.UtcNow,
                Metadata = _tags?.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value!) ?? []
            });
        }
    }

    public void Dispose()
    {
        if (_stopwatch.IsRunning)
        {
            Stop();
        }

        _activity?.Dispose();
    }

    // Simple timer handle implementation
    private sealed class TimerHandle : ITimerHandle, IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private readonly Dictionary<string, TimeSpan> _checkpoints;

        public string OperationName { get; }
        public string OperationId { get; }
        public DateTime StartTime { get; }
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public TimerHandle(string operationName, string operationId)
        {
            OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            OperationId = operationId ?? Guid.NewGuid().ToString();
            StartTime = DateTime.UtcNow;
            _stopwatch = Stopwatch.StartNew();
            _checkpoints = [];
        }

        public TimeSpan Stop(IDictionary<string, object>? metadata = null)
        {
            _stopwatch.Stop();
            return _stopwatch.Elapsed;
        }

        public TimeSpan AddCheckpoint(string checkpointName)
        {
            var elapsed = _stopwatch.Elapsed;
            _checkpoints[checkpointName] = elapsed;
            return elapsed;
        }

        public IDictionary<string, TimeSpan> GetCheckpoints()
        {
            return new Dictionary<string, TimeSpan>(_checkpoints);
        }

        public void Dispose()
        {
            if (_stopwatch.IsRunning)
            {
                _stopwatch.Stop();
            }
        }
    }
}