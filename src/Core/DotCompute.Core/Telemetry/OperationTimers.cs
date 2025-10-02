// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Interfaces.Telemetry;
using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Unified operation timer implementation.
/// </summary>
internal sealed class UnifiedOperationTimer : IOperationTimer
{
    private readonly string _operationName;
    private readonly IDictionary<string, object?>? _tags;
    private readonly BaseTelemetryProvider _telemetryProvider;
    private readonly Stopwatch _stopwatch;
    private readonly Activity? _activity;

    public UnifiedOperationTimer(string operationName, IDictionary<string, object?>? tags, BaseTelemetryProvider telemetryProvider)
    {
        _operationName = operationName;
        _tags = tags;
        _telemetryProvider = telemetryProvider;
        _stopwatch = Stopwatch.StartNew();
        _activity = telemetryProvider.StartActivity($"timer.{operationName}");

        if (_activity != null && tags != null)
        {
            foreach (var tag in tags)
            {
                _ = _activity.SetTag(tag.Key, tag.Value?.ToString());
            }
        }
    }

    public bool IsEnabled => true;
    public TimeSpan MinimumDurationThreshold => TimeSpan.Zero;

    public event EventHandler<OperationTimingEventArgs>? OperationCompleted;

    public void Stop()
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();

            var tags = new Dictionary<string, object?> { ["operation"] = _operationName };
            if (_tags != null)
            {
                foreach (var tag in _tags)
                {
                    tags[tag.Key] = tag.Value;
                }
            }

            _telemetryProvider.RecordHistogram("operation.duration.ms", _stopwatch.Elapsed.TotalMilliseconds, tags);

            _ = (_activity?.SetTag("duration.ms", _stopwatch.Elapsed.TotalMilliseconds));
            _ = (_activity?.SetStatus(ActivityStatusCode.Ok));

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

    // Additional interface methods with minimal implementations
    public ITimerHandle StartOperation(string operationName, string? operationId = null)
        => new SimpleTimerHandle(operationName, operationId ?? Guid.NewGuid().ToString());

    public IDisposable StartOperationScope(string operationName, string? operationId = null)
        => new SimpleTimerHandle(operationName, operationId ?? Guid.NewGuid().ToString());

    public (T result, TimeSpan duration) TimeOperation<T>(string operationName, Func<T> operation)
    {
        var sw = Stopwatch.StartNew();
        var result = operation();
        sw.Stop();
        return (result, sw.Elapsed);
    }

    public async Task<(T result, TimeSpan duration)> TimeOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        var sw = Stopwatch.StartNew();
        var result = await operation();
        sw.Stop();
        return (result, sw.Elapsed);
    }

    public TimeSpan TimeOperation(string operationName, Action operation)
    {
        var sw = Stopwatch.StartNew();
        operation();
        sw.Stop();
        return sw.Elapsed;
    }

    public async Task<TimeSpan> TimeOperationAsync(string operationName, Func<Task> operation)
    {
        var sw = Stopwatch.StartNew();
        await operation();
        sw.Stop();
        return sw.Elapsed;
    }

    public void RecordTiming(string operationName, TimeSpan duration, string? operationId = null, IDictionary<string, object>? metadata = null) { }
    public OperationStatistics? GetStatistics(string operationName) => null;
    public IDictionary<string, OperationStatistics> GetAllStatistics() => new Dictionary<string, OperationStatistics>();
    public void ClearStatistics(string operationName) { }
    public void ClearAllStatistics() { }
    public string ExportData(MetricsExportFormat format, Func<string, bool>? operationFilter = null) => string.Empty;
    public void SetEnabled(bool enabled) { }
    public void SetMinimumDurationThreshold(TimeSpan threshold) { }

    private sealed class SimpleTimerHandle(string operationName, string operationId) : ITimerHandle, IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public string OperationName { get; } = operationName;
        public string OperationId { get; } = operationId;
        public DateTime StartTime { get; } = DateTime.UtcNow;
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public TimeSpan Stop(IDictionary<string, object>? metadata = null)
        {
            _stopwatch.Stop();
            return _stopwatch.Elapsed;
        }

        public TimeSpan AddCheckpoint(string checkpointName) => _stopwatch.Elapsed;
        public IDictionary<string, TimeSpan> GetCheckpoints() => new Dictionary<string, TimeSpan>();
        public void Dispose() => _stopwatch.Stop();
    }
}

/// <summary>
/// Null implementation for when telemetry is disabled.
/// </summary>
internal sealed class NullOperationTimer : IOperationTimer
{
    public bool IsEnabled => false;
    public TimeSpan MinimumDurationThreshold => TimeSpan.Zero;
#pragma warning disable CS0067 // Event is never used
    public event EventHandler<OperationTimingEventArgs>? OperationCompleted;
#pragma warning restore CS0067

    public static void Stop() { }
    public void Dispose() { }
    public ITimerHandle StartOperation(string operationName, string? operationId = null) => new NullTimerHandle();
    public IDisposable StartOperationScope(string operationName, string? operationId = null) => new NullTimerHandle();
    public (T result, TimeSpan duration) TimeOperation<T>(string operationName, Func<T> operation) => (operation(), TimeSpan.Zero);
    public async Task<(T result, TimeSpan duration)> TimeOperationAsync<T>(string operationName, Func<Task<T>> operation) => (await operation(), TimeSpan.Zero);
    public TimeSpan TimeOperation(string operationName, Action operation) { operation(); return TimeSpan.Zero; }
    public async Task<TimeSpan> TimeOperationAsync(string operationName, Func<Task> operation) { await operation(); return TimeSpan.Zero; }
    public void RecordTiming(string operationName, TimeSpan duration, string? operationId = null, IDictionary<string, object>? metadata = null) { }
    public OperationStatistics? GetStatistics(string operationName) => null;
    public IDictionary<string, OperationStatistics> GetAllStatistics() => new Dictionary<string, OperationStatistics>();
    public void ClearStatistics(string operationName) { }
    public void ClearAllStatistics() { }
    public string ExportData(MetricsExportFormat format, Func<string, bool>? operationFilter = null) => string.Empty;
    public void SetEnabled(bool enabled) { }
    public void SetMinimumDurationThreshold(TimeSpan threshold) { }

    private sealed class NullTimerHandle : ITimerHandle, IDisposable
    {
        public string OperationName => string.Empty;
        public string OperationId => string.Empty;
        public DateTime StartTime => DateTime.UtcNow;
        public TimeSpan Elapsed => TimeSpan.Zero;
        public TimeSpan Stop(IDictionary<string, object>? metadata = null) => TimeSpan.Zero;
        public TimeSpan AddCheckpoint(string checkpointName) => TimeSpan.Zero;
        public IDictionary<string, TimeSpan> GetCheckpoints() => new Dictionary<string, TimeSpan>();
        public void Dispose() { }
    }
}
