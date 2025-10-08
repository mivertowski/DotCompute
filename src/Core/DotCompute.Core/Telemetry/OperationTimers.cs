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
    /// <summary>
    /// Initializes a new instance of the UnifiedOperationTimer class.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="tags">The tags.</param>
    /// <param name="telemetryProvider">The telemetry provider.</param>

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
    /// <summary>
    /// Gets or sets a value indicating whether enabled.
    /// </summary>
    /// <value>The is enabled.</value>

    public bool IsEnabled => true;
    /// <summary>
    /// Gets or sets the minimum duration threshold.
    /// </summary>
    /// <value>The minimum duration threshold.</value>
    public TimeSpan MinimumDurationThreshold => TimeSpan.Zero;
    /// <summary>
    /// Occurs when operation completed.
    /// </summary>

    public event EventHandler<OperationTimingEventArgs>? OperationCompleted;
    /// <summary>
    /// Performs stop.
    /// </summary>

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
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_stopwatch.IsRunning)
        {
            Stop();
        }
        _activity?.Dispose();
    }
    /// <summary>
    /// Gets start operation.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <returns>The result of the operation.</returns>

    // Additional interface methods with minimal implementations
    public ITimerHandle StartOperation(string operationName, string? operationId = null)
        => new SimpleTimerHandle(operationName, operationId ?? Guid.NewGuid().ToString());
    /// <summary>
    /// Gets start operation scope.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <returns>The result of the operation.</returns>

    public IDisposable StartOperationScope(string operationName, string? operationId = null)
        => new SimpleTimerHandle(operationName, operationId ?? Guid.NewGuid().ToString());
    /// <summary>
    /// Gets time operation.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The result of the operation.</returns>

    public (T result, TimeSpan duration) TimeOperation<T>(string operationName, Func<T> operation)
    {
        var sw = Stopwatch.StartNew();
        var result = operation();
        sw.Stop();
        return (result, sw.Elapsed);
    }
    /// <summary>
    /// Gets time operation asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<(T result, TimeSpan duration)> TimeOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        var sw = Stopwatch.StartNew();
        var result = await operation();
        sw.Stop();
        return (result, sw.Elapsed);
    }
    /// <summary>
    /// Gets time operation.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The result of the operation.</returns>

    public TimeSpan TimeOperation(string operationName, Action operation)
    {
        var sw = Stopwatch.StartNew();
        operation();
        sw.Stop();
        return sw.Elapsed;
    }
    /// <summary>
    /// Gets time operation asynchronously.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<TimeSpan> TimeOperationAsync(string operationName, Func<Task> operation)
    {
        var sw = Stopwatch.StartNew();
        await operation();
        sw.Stop();
        return sw.Elapsed;
    }
    /// <summary>
    /// Performs record timing.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="duration">The duration.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="metadata">The metadata.</param>

    public void RecordTiming(string operationName, TimeSpan duration, string? operationId = null, IDictionary<string, object>? metadata = null) { }
    /// <summary>
    /// Gets the statistics.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <returns>The statistics.</returns>
    public OperationStatistics? GetStatistics(string operationName) => null;
    /// <summary>
    /// Gets the all statistics.
    /// </summary>
    /// <returns>The all statistics.</returns>
    public IDictionary<string, OperationStatistics> GetAllStatistics() => new Dictionary<string, OperationStatistics>();
    /// <summary>
    /// Performs clear statistics.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    public void ClearStatistics(string operationName) { }
    /// <summary>
    /// Performs clear all statistics.
    /// </summary>
    public void ClearAllStatistics() { }
    /// <summary>
    /// Gets export data.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <param name="operationFilter">The operation filter.</param>
    /// <returns>The result of the operation.</returns>
    public string ExportData(MetricsExportFormat format, Func<string, bool>? operationFilter = null) => string.Empty;
    /// <summary>
    /// Sets the enabled.
    /// </summary>
    /// <param name="enabled">The enabled.</param>
    public void SetEnabled(bool enabled) { }
    /// <summary>
    /// Sets the minimum duration threshold.
    /// </summary>
    /// <param name="threshold">The threshold.</param>
    public void SetMinimumDurationThreshold(TimeSpan threshold) { }
    /// <summary>
    /// A class that represents simple timer handle.
    /// </summary>

    private sealed class SimpleTimerHandle(string operationName, string operationId) : ITimerHandle, IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        /// <summary>
        /// Gets or sets the operation name.
        /// </summary>
        /// <value>The operation name.</value>

        public string OperationName { get; } = operationName;
        /// <summary>
        /// Gets or sets the operation identifier.
        /// </summary>
        /// <value>The operation id.</value>
        public string OperationId { get; } = operationId;
        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        /// <value>The start time.</value>
        public DateTime StartTime { get; } = DateTime.UtcNow;
        /// <summary>
        /// Gets or sets the elapsed.
        /// </summary>
        /// <value>The elapsed.</value>
        public TimeSpan Elapsed => _stopwatch.Elapsed;
        /// <summary>
        /// Gets stop.
        /// </summary>
        /// <param name="metadata">The metadata.</param>
        /// <returns>The result of the operation.</returns>

        public TimeSpan Stop(IDictionary<string, object>? metadata = null)
        {
            _stopwatch.Stop();
            return _stopwatch.Elapsed;
        }

        /// <summary>
        /// Stops the timer and records the operation duration.
        /// </summary>
        /// <param name="metadata">Optional metadata to associate with the timing record</param>
        /// <returns>The total duration of the operation</returns>
        public TimeSpan StopTimer(IDictionary<string, object>? metadata = null) => Stop(metadata);

        /// <summary>
        /// Gets add checkpoint.
        /// </summary>
        /// <param name="checkpointName">The checkpoint name.</param>
        /// <returns>The result of the operation.</returns>

        public TimeSpan AddCheckpoint(string checkpointName) => _stopwatch.Elapsed;
        /// <summary>
        /// Gets the checkpoints.
        /// </summary>
        /// <returns>The checkpoints.</returns>
        public IDictionary<string, TimeSpan> GetCheckpoints() => new Dictionary<string, TimeSpan>();
        /// <summary>
        /// Performs dispose.
        /// </summary>
        public void Dispose() => _stopwatch.Stop();
    }
}

/// <summary>
/// Null implementation for when telemetry is disabled.
/// </summary>
internal sealed class NullOperationTimer : IOperationTimer
{
    /// <summary>
    /// Gets or sets a value indicating whether enabled.
    /// </summary>
    /// <value>The is enabled.</value>
    public bool IsEnabled => false;
    /// <summary>
    /// Gets or sets the minimum duration threshold.
    /// </summary>
    /// <value>The minimum duration threshold.</value>
    public TimeSpan MinimumDurationThreshold => TimeSpan.Zero;
    /// <summary>
    /// Occurs when operation completed.
    /// </summary>
#pragma warning disable CS0067 // Event is never used
    public event EventHandler<OperationTimingEventArgs>? OperationCompleted;
    /// <summary>
    /// Performs stop.
    /// </summary>
#pragma warning restore CS0067
#pragma warning disable CA1822 // Mark members as static - implements interface member
    public void Stop() { }
#pragma warning restore CA1822
    /// <summary>
    /// Performs dispose.
    /// </summary>
    public void Dispose() { }
    /// <summary>
    /// Gets start operation.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <returns>The result of the operation.</returns>
    public ITimerHandle StartOperation(string operationName, string? operationId = null) => new NullTimerHandle();
    /// <summary>
    /// Gets start operation scope.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <returns>The result of the operation.</returns>
    public IDisposable StartOperationScope(string operationName, string? operationId = null) => new NullTimerHandle();
    /// <summary>
    /// Gets time operation.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The result of the operation.</returns>
    public (T result, TimeSpan duration) TimeOperation<T>(string operationName, Func<T> operation) => (operation(), TimeSpan.Zero);
    /// <summary>
    /// Gets time operation asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<(T result, TimeSpan duration)> TimeOperationAsync<T>(string operationName, Func<Task<T>> operation) => (await operation(), TimeSpan.Zero);
    /// <summary>
    /// Gets time operation.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The result of the operation.</returns>
    public TimeSpan TimeOperation(string operationName, Action operation)
    {
        operation();
        return TimeSpan.Zero;
    }
    /// <summary>
    /// Gets time operation asynchronously.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<TimeSpan> TimeOperationAsync(string operationName, Func<Task> operation)
    {
        await operation();
        return TimeSpan.Zero;
    }
    /// <summary>
    /// Performs record timing.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="duration">The duration.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="metadata">The metadata.</param>
    public void RecordTiming(string operationName, TimeSpan duration, string? operationId = null, IDictionary<string, object>? metadata = null) { }
    /// <summary>
    /// Gets the statistics.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <returns>The statistics.</returns>
    public OperationStatistics? GetStatistics(string operationName) => null;
    /// <summary>
    /// Gets the all statistics.
    /// </summary>
    /// <returns>The all statistics.</returns>
    public IDictionary<string, OperationStatistics> GetAllStatistics() => new Dictionary<string, OperationStatistics>();
    /// <summary>
    /// Performs clear statistics.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    public void ClearStatistics(string operationName) { }
    /// <summary>
    /// Performs clear all statistics.
    /// </summary>
    public void ClearAllStatistics() { }
    /// <summary>
    /// Gets export data.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <param name="operationFilter">The operation filter.</param>
    /// <returns>The result of the operation.</returns>
    public string ExportData(MetricsExportFormat format, Func<string, bool>? operationFilter = null) => string.Empty;
    /// <summary>
    /// Sets the enabled.
    /// </summary>
    /// <param name="enabled">The enabled.</param>
    public void SetEnabled(bool enabled) { }
    /// <summary>
    /// Sets the minimum duration threshold.
    /// </summary>
    /// <param name="threshold">The threshold.</param>
    public void SetMinimumDurationThreshold(TimeSpan threshold) { }

    private sealed class NullTimerHandle : ITimerHandle, IDisposable
    {
        /// <summary>
        /// Gets or sets the operation name.
        /// </summary>
        /// <value>The operation name.</value>
        public string OperationName => string.Empty;
        /// <summary>
        /// Gets or sets the operation identifier.
        /// </summary>
        /// <value>The operation id.</value>
        public string OperationId => string.Empty;
        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        /// <value>The start time.</value>
        public DateTime StartTime => DateTime.UtcNow;
        /// <summary>
        /// Gets or sets the elapsed.
        /// </summary>
        /// <value>The elapsed.</value>
        public TimeSpan Elapsed => TimeSpan.Zero;
        /// <summary>
        /// Gets stop.
        /// </summary>
        /// <param name="metadata">The metadata.</param>
        /// <returns>The result of the operation.</returns>
#pragma warning disable CA1822 // Mark members as static - implements interface member
        public TimeSpan Stop(IDictionary<string, object>? metadata = null) => TimeSpan.Zero;
#pragma warning restore CA1822

        /// <summary>
        /// Stops the timer and records the operation duration.
        /// </summary>
        /// <param name="metadata">Optional metadata to associate with the timing record</param>
        /// <returns>The total duration of the operation</returns>
        public TimeSpan StopTimer(IDictionary<string, object>? metadata = null) => Stop(metadata);

        /// <summary>
        /// Gets add checkpoint.
        /// </summary>
        /// <param name="checkpointName">The checkpoint name.</param>
        /// <returns>The result of the operation.</returns>
        public TimeSpan AddCheckpoint(string checkpointName) => TimeSpan.Zero;
        /// <summary>
        /// Gets the checkpoints.
        /// </summary>
        /// <returns>The checkpoints.</returns>
        public IDictionary<string, TimeSpan> GetCheckpoints() => new Dictionary<string, TimeSpan>();
        /// <summary>
        /// Performs dispose.
        /// </summary>
        public void Dispose() { }
    }
}
