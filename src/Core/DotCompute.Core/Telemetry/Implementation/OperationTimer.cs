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
#pragma warning disable CA1823 // Avoid unused private fields - Fields reserved for future duration recording (see TODO at line 146)
    private readonly string _operationName = operationName;
    private readonly IDictionary<string, object?>? _tags = tags;
#pragma warning restore CA1823
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
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

#pragma warning disable CS0067 // Event is never used
    public event EventHandler<OperationTimingEventArgs>? OperationCompleted;
    /// <summary>
    /// Gets start operation.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <returns>The result of the operation.</returns>
#pragma warning restore CS0067

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
        var result = operation();
        return (result, TimeSpan.Zero);
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
        var result = await operation();
        return (result, TimeSpan.Zero);
    }
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
    /// <summary>
    /// Performs stop.
    /// </summary>

    public void Stop() => _stopwatch.Stop();// TODO: Record the duration
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