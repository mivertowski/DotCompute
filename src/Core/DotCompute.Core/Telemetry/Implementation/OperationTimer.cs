// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions.Interfaces.Telemetry;
using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Core.Telemetry.Implementation;

/// <summary>
/// Operation timer implementation.
/// </summary>
internal sealed class OperationTimer(string operationName, IDictionary<string, object?>? tags) : IOperationTimer
{
    private readonly string _operationName = operationName;
    private readonly IDictionary<string, object?>? _tags = tags;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private static readonly ConcurrentDictionary<string, OperationTimingAccumulator> _statistics = new();
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
    /// Gets start operation.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <returns>The result of the operation.</returns>


    public ITimerHandle StartOperation(string operationName, string? operationId = null) => new TimerHandle();
    /// <summary>
    /// Gets start operation scope.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <returns>The result of the operation.</returns>
    public IDisposable StartOperationScope(string operationName, string? operationId = null) => new TimerHandle();
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
        RecordTiming(operationName, sw.Elapsed);
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
        RecordTiming(operationName, sw.Elapsed);
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
        RecordTiming(operationName, sw.Elapsed);
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
        RecordTiming(operationName, sw.Elapsed);
        return sw.Elapsed;
    }
    /// <summary>
    /// Performs record timing.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="duration">The duration.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="metadata">The metadata.</param>
    public void RecordTiming(string operationName, TimeSpan duration, string? operationId = null, IDictionary<string, object>? metadata = null)
    {
        var accumulator = _statistics.GetOrAdd(operationName, _ => new OperationTimingAccumulator());
        accumulator.Record(duration);
    }
    /// <summary>
    /// Gets the statistics.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <returns>The statistics.</returns>
    public OperationStatistics? GetStatistics(string operationName)
    {
        return _statistics.TryGetValue(operationName, out var accumulator) ? accumulator.ToStatistics(operationName) : null;
    }
    /// <summary>
    /// Gets the all statistics.
    /// </summary>
    /// <returns>The all statistics.</returns>
    public IDictionary<string, OperationStatistics> GetAllStatistics()
    {
        var result = new Dictionary<string, OperationStatistics>();
        foreach (var (name, accumulator) in _statistics)
        {
            result[name] = accumulator.ToStatistics(name);
        }
        return result;
    }
    /// <summary>
    /// Performs clear statistics.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    public void ClearStatistics(string operationName) => _statistics.TryRemove(operationName, out _);
    /// <summary>
    /// Performs clear all statistics.
    /// </summary>
    public void ClearAllStatistics() => _statistics.Clear();
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
    /// Performs stop.
    /// </summary>

    public void Stop()
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
            var accumulator = _statistics.GetOrAdd(_operationName, _ => new OperationTimingAccumulator());
            accumulator.Record(_stopwatch.Elapsed);

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
    }
}
