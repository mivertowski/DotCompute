// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Profiles kernel execution for performance monitoring and optimization.
/// </summary>
public interface IKernelProfiler
{
    /// <summary>
    /// Starts a profiling session for kernel execution.
    /// </summary>
    /// <param name="sessionName">Name for the profiling session</param>
    /// <returns>A disposable profiling session</returns>
    public IProfilingSession StartProfiling(string sessionName);

    /// <summary>
    /// Gets profiling results for a completed session.
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>Profiling results or null if session not found</returns>
    public Task<ProfilingResults?> GetResultsAsync(Guid sessionId);

    /// <summary>
    /// Gets aggregated statistics for a kernel across all executions.
    /// </summary>
    /// <param name="kernelName">The kernel name</param>
    /// <param name="timeRange">Optional time range filter</param>
    /// <returns>Aggregated profiling statistics</returns>
    public Task<KernelStatistics> GetKernelStatisticsAsync(
        string kernelName,

        TimeRange? timeRange = null);

    /// <summary>
    /// Exports profiling data for external analysis.
    /// </summary>
    /// <param name="format">Export format (JSON, CSV, etc.)</param>
    /// <param name="outputPath">Path to write the export</param>
    /// <param name="timeRange">Optional time range filter</param>
    /// <returns>Path to the exported file</returns>
    public Task<string> ExportDataAsync(
        ExportFormat format,

        string outputPath,
        TimeRange? timeRange = null);

    /// <summary>
    /// Clears profiling data older than the specified age.
    /// </summary>
    /// <param name="olderThan">Age threshold for data cleanup</param>
    /// <returns>Number of sessions cleaned up</returns>
    public Task<int> CleanupOldDataAsync(TimeSpan olderThan);
}

/// <summary>
/// Results from a profiling session.
/// </summary>
public class ProfilingResults
{
    /// <summary>
    /// Gets or sets the session identifier.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Gets or sets the session name.
    /// </summary>
    public required string SessionName { get; init; }

    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; init; }

    /// <summary>
    /// Gets or sets the kernel compilation time.
    /// </summary>
    public TimeSpan CompilationTime { get; init; }

    /// <summary>
    /// Gets or sets the kernel execution time.
    /// </summary>
    public TimeSpan KernelExecutionTime { get; init; }

    /// <summary>
    /// Gets or sets the memory transfer time.
    /// </summary>
    public TimeSpan MemoryTransferTime { get; init; }

    /// <summary>
    /// Gets or sets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryBytes { get; init; }

    /// <summary>
    /// Gets or sets the custom metrics recorded.
    /// </summary>
    public Dictionary<string, double> CustomMetrics { get; init; } = [];

    /// <summary>
    /// Gets or sets the timing checkpoints.
    /// </summary>
    public List<TimingCheckpoint> Checkpoints { get; init; } = [];

    /// <summary>
    /// Gets or sets the memory usage snapshots.
    /// </summary>
    public List<MemorySnapshot> MemorySnapshots { get; init; } = [];

    /// <summary>
    /// Gets or sets the session context information.
    /// </summary>
    public Dictionary<string, object> Context { get; init; } = [];
}

/// <summary>
/// Timing checkpoint in profiling session.
/// </summary>
public class TimingCheckpoint
{
    /// <summary>
    /// Gets or sets the checkpoint name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the elapsed time since session start.
    /// </summary>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// Gets or sets the time since last checkpoint.
    /// </summary>
    public TimeSpan DeltaTime { get; init; }
}

/// <summary>
/// Memory usage snapshot.
/// </summary>
public class MemorySnapshot
{
    /// <summary>
    /// Gets or sets the snapshot label.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public TimeSpan Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the managed memory in bytes.
    /// </summary>
    public long ManagedMemoryBytes { get; init; }

    /// <summary>
    /// Gets or sets the device memory in bytes.
    /// </summary>
    public long DeviceMemoryBytes { get; init; }

    /// <summary>
    /// Gets or sets the pinned memory in bytes.
    /// </summary>
    public long PinnedMemoryBytes { get; init; }
}

/// <summary>
/// Aggregated statistics for a kernel.
/// </summary>
public class KernelStatistics
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public required string KernelName { get; init; }

    /// <summary>
    /// Gets or sets the total execution count.
    /// </summary>
    public int ExecutionCount { get; init; }

    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; init; }

    /// <summary>
    /// Gets or sets the minimum execution time.
    /// </summary>
    public TimeSpan MinExecutionTime { get; init; }

    /// <summary>
    /// Gets or sets the maximum execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; init; }

    /// <summary>
    /// Gets or sets the 95th percentile execution time.
    /// </summary>
    public TimeSpan P95ExecutionTime { get; init; }

    /// <summary>
    /// Gets or sets the 99th percentile execution time.
    /// </summary>
    public TimeSpan P99ExecutionTime { get; init; }

    /// <summary>
    /// Gets or sets the standard deviation of execution times.
    /// </summary>
    public TimeSpan StandardDeviation { get; init; }

    /// <summary>
    /// Gets or sets the average memory usage.
    /// </summary>
    public long AverageMemoryBytes { get; init; }

    /// <summary>
    /// Gets or sets the success rate (0-1).
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Gets or sets the most common accelerator used.
    /// </summary>
    public string? MostCommonAccelerator { get; init; }
}

/// <summary>
/// Time range for filtering profiling data.
/// </summary>
public class TimeRange
{
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime Start { get; init; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTime End { get; init; }

    /// <summary>
    /// Creates a time range for the last N hours.
    /// </summary>
    public static TimeRange LastHours(int hours) => new()
    {
        Start = DateTime.UtcNow.AddHours(-hours),
        End = DateTime.UtcNow
    };

    /// <summary>
    /// Creates a time range for the last N days.
    /// </summary>
    public static TimeRange LastDays(int days) => new()
    {
        Start = DateTime.UtcNow.AddDays(-days),
        End = DateTime.UtcNow
    };
}

/// <summary>
/// Export format for profiling data.
/// </summary>
public enum ExportFormat
{
    /// <summary>
    /// JSON format.
    /// </summary>
    Json,

    /// <summary>
    /// CSV format.
    /// </summary>
    Csv,

    /// <summary>
    /// Chrome tracing format.
    /// </summary>
    ChromeTracing,

    /// <summary>
    /// OpenTelemetry format.
    /// </summary>
    OpenTelemetry
}