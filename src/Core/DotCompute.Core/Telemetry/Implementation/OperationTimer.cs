// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Interfaces.Telemetry;
using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Core.Telemetry.Implementation;

/// <summary>
/// Operation timer implementation.
/// </summary>
internal sealed class OperationTimer(string operationName, IDictionary<string, object?>? tags) : IOperationTimer
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public bool IsEnabled => true;
    public TimeSpan MinimumDurationThreshold => TimeSpan.Zero;

#pragma warning disable CS0067 // Event is never used
    public event EventHandler<OperationTimingEventArgs>? OperationCompleted;
#pragma warning restore CS0067

    public ITimerHandle StartOperation(string operationName, string? operationId = null) => new TimerHandle();
    public IDisposable StartOperationScope(string operationName, string? operationId = null) => new TimerHandle();
    public (T result, TimeSpan duration) TimeOperation<T>(string operationName, Func<T> operation)
    {
        var result = operation();
        return (result, TimeSpan.Zero);
    }
    public async Task<(T result, TimeSpan duration)> TimeOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        var result = await operation();
        return (result, TimeSpan.Zero);
    }
    public TimeSpan TimeOperation(string operationName, Action operation)
    {
        operation();
        return TimeSpan.Zero;
    }
    public async Task<TimeSpan> TimeOperationAsync(string operationName, Func<Task> operation)
    {
        await operation();
        return TimeSpan.Zero;
    }
    public void RecordTiming(string operationName, TimeSpan duration, string? operationId = null, IDictionary<string, object>? metadata = null) { }
    public OperationStatistics? GetStatistics(string operationName) => null;
    public IDictionary<string, OperationStatistics> GetAllStatistics() => new Dictionary<string, OperationStatistics>();
    public void ClearStatistics(string operationName) { }
    public void ClearAllStatistics() { }
    public string ExportData(MetricsExportFormat format, Func<string, bool>? operationFilter = null) => string.Empty;
    public void SetEnabled(bool enabled) { }
    public void SetMinimumDurationThreshold(TimeSpan threshold) { }

    public void Stop() => _stopwatch.Stop();// TODO: Record the duration

    public void Dispose()
    {
        if (_stopwatch.IsRunning)
        {
            Stop();
        }
    }
}