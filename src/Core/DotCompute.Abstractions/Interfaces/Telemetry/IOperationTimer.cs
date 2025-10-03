// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Abstractions.Interfaces.Telemetry;

/// <summary>
/// Interface for high-precision operation timing and performance measurement.
/// Provides methods for measuring execution times, collecting metrics, and analyzing performance patterns.
/// </summary>
public interface IOperationTimer : IDisposable
{
    /// <summary>
    /// Starts timing an operation with the specified name.
    /// </summary>
    /// <param name="operationName">Name of the operation being timed</param>
    /// <param name="operationId">Optional unique identifier for this operation instance</param>
    /// <returns>A timer handle that can be used to stop the operation</returns>
    public ITimerHandle StartOperation(string operationName, string? operationId = null);

    /// <summary>
    /// Starts timing an operation and automatically stops when the returned handle is disposed.
    /// </summary>
    /// <param name="operationName">Name of the operation being timed</param>
    /// <param name="operationId">Optional unique identifier for this operation instance</param>
    /// <returns>A disposable timer handle</returns>
    public IDisposable StartOperationScope(string operationName, string? operationId = null);

    /// <summary>
    /// Times a synchronous operation and returns the result along with timing information.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operationName">Name of the operation being timed</param>
    /// <param name="operation">The operation to time</param>
    /// <returns>Tuple containing the operation result and timing information</returns>
    public (T result, TimeSpan duration) TimeOperation<T>(string operationName, Func<T> operation);

    /// <summary>
    /// Times an asynchronous operation and returns the result along with timing information.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operationName">Name of the operation being timed</param>
    /// <param name="operation">The async operation to time</param>
    /// <returns>Task containing the operation result and timing information</returns>
    public Task<(T result, TimeSpan duration)> TimeOperationAsync<T>(string operationName, Func<Task<T>> operation);

    /// <summary>
    /// Times a void synchronous operation.
    /// </summary>
    /// <param name="operationName">Name of the operation being timed</param>
    /// <param name="operation">The operation to time</param>
    /// <returns>Duration of the operation</returns>
    public TimeSpan TimeOperation(string operationName, Action operation);

    /// <summary>
    /// Times a void asynchronous operation.
    /// </summary>
    /// <param name="operationName">Name of the operation being timed</param>
    /// <param name="operation">The async operation to time</param>
    /// <returns>Task containing the duration of the operation</returns>
    public Task<TimeSpan> TimeOperationAsync(string operationName, Func<Task> operation);

    /// <summary>
    /// Records a manual timing measurement for an operation.
    /// </summary>
    /// <param name="operationName">Name of the operation</param>
    /// <param name="duration">Duration of the operation</param>
    /// <param name="operationId">Optional unique identifier for this operation instance</param>
    /// <param name="metadata">Optional metadata associated with the operation</param>
    public void RecordTiming(string operationName, TimeSpan duration, string? operationId = null,
                     IDictionary<string, object>? metadata = null);

    /// <summary>
    /// Gets timing statistics for a specific operation.
    /// </summary>
    /// <param name="operationName">Name of the operation to get statistics for</param>
    /// <returns>Timing statistics for the operation, or null if no data exists</returns>
    public OperationStatistics? GetStatistics(string operationName);

    /// <summary>
    /// Gets timing statistics for all recorded operations.
    /// </summary>
    /// <returns>Dictionary mapping operation names to their statistics</returns>
    public IDictionary<string, OperationStatistics> GetAllStatistics();

    /// <summary>
    /// Clears all timing data for a specific operation.
    /// </summary>
    /// <param name="operationName">Name of the operation to clear data for</param>
    public void ClearStatistics(string operationName);

    /// <summary>
    /// Clears all timing data for all operations.
    /// </summary>
    public void ClearAllStatistics();

    /// <summary>
    /// Exports timing data in the specified format.
    /// </summary>
    /// <param name="format">Format for exporting the data</param>
    /// <param name="operationFilter">Optional filter for which operations to include</param>
    /// <returns>Exported timing data as a string</returns>
    public string ExportData(MetricsExportFormat format, Func<string, bool>? operationFilter = null);

    /// <summary>
    /// Enables or disables timing collection.
    /// </summary>
    /// <param name="enabled">Whether timing should be enabled</param>
    public void SetEnabled(bool enabled);

    /// <summary>
    /// Gets whether timing collection is currently enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Sets the minimum duration threshold for recording operations.
    /// Operations faster than this threshold will be ignored.
    /// </summary>
    /// <param name="threshold">Minimum duration threshold</param>
    public void SetMinimumDurationThreshold(TimeSpan threshold);

    /// <summary>
    /// Gets the current minimum duration threshold.
    /// </summary>
    public TimeSpan MinimumDurationThreshold { get; }

    /// <summary>
    /// Event fired when an operation completes and timing data is recorded.
    /// </summary>
    public event EventHandler<OperationTimingEventArgs> OperationCompleted;
}

/// <summary>
/// Handle for controlling an active timing operation.
/// </summary>
public interface ITimerHandle : IDisposable
{
    /// <summary>
    /// Gets the name of the operation being timed.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets the unique identifier for this operation instance.
    /// </summary>
    public string OperationId { get; }

    /// <summary>
    /// Gets the time when the operation started.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// Gets the elapsed time since the operation started.
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Stops the timer and records the operation duration.
    /// </summary>
    /// <param name="metadata">Optional metadata to associate with the timing record</param>
    /// <returns>The total duration of the operation</returns>
    public TimeSpan StopTimer(IDictionary<string, object>? metadata = null);

    /// <summary>
    /// Adds a checkpoint with a name and the current elapsed time.
    /// </summary>
    /// <param name="checkpointName">Name of the checkpoint</param>
    /// <returns>Elapsed time at the checkpoint</returns>
    public TimeSpan AddCheckpoint(string checkpointName);

    /// <summary>
    /// Gets all checkpoints recorded for this operation.
    /// </summary>
    /// <returns>Dictionary mapping checkpoint names to their elapsed times</returns>
    public IDictionary<string, TimeSpan> GetCheckpoints();
}

/// <summary>
/// Statistical information about operation timings.
/// </summary>
public sealed class OperationStatistics
{
    /// <summary>
    /// Gets the name of the operation.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Gets the total number of recorded executions.
    /// </summary>
    public required long ExecutionCount { get; init; }

    /// <summary>
    /// Gets the total time spent in all executions.
    /// </summary>
    public required TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Gets the average execution time.
    /// </summary>
    public required TimeSpan AverageDuration { get; init; }

    /// <summary>
    /// Gets the minimum execution time recorded.
    /// </summary>
    public required TimeSpan MinimumDuration { get; init; }

    /// <summary>
    /// Gets the maximum execution time recorded.
    /// </summary>
    public required TimeSpan MaximumDuration { get; init; }

    /// <summary>
    /// Gets the standard deviation of execution times.
    /// </summary>
    public required TimeSpan StandardDeviation { get; init; }

    /// <summary>
    /// Gets the median execution time.
    /// </summary>
    public required TimeSpan MedianDuration { get; init; }

    /// <summary>
    /// Gets the 95th percentile execution time.
    /// </summary>
    public required TimeSpan P95Duration { get; init; }

    /// <summary>
    /// Gets the 99th percentile execution time.
    /// </summary>
    public required TimeSpan P99Duration { get; init; }

    /// <summary>
    /// Gets the time when the first execution was recorded.
    /// </summary>
    public required DateTime FirstExecution { get; init; }

    /// <summary>
    /// Gets the time when the most recent execution was recorded.
    /// </summary>
    public required DateTime LastExecution { get; init; }
}

/// <summary>
/// Event arguments for operation timing events.
/// </summary>
public sealed class OperationTimingEventArgs : EventArgs
{
    /// <summary>
    /// Gets the name of the completed operation.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Gets the unique identifier for the operation instance.
    /// </summary>
    public required string OperationId { get; init; }

    /// <summary>
    /// Gets the duration of the operation.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the start time of the operation.
    /// </summary>
    public required DateTime StartTime { get; init; }

    /// <summary>
    /// Gets the end time of the operation.
    /// </summary>
    public required DateTime EndTime { get; init; }

    /// <summary>
    /// Gets any metadata associated with the operation.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Gets any checkpoints recorded during the operation.
    /// </summary>
    public IDictionary<string, TimeSpan>? Checkpoints { get; init; }
}
