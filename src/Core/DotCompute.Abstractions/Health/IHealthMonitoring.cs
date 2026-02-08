// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Health;

/// <summary>
/// Interface for components that support health checking.
/// </summary>
public interface IHealthCheckable
{
    /// <summary>
    /// Performs a health check and returns the current health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The health check result.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for components that expose memory usage monitoring.
/// </summary>
public interface IMemoryMonitorable
{
    /// <summary>
    /// Gets the current memory usage information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Memory usage details.</returns>
    public Task<MemoryUsageResult> GetMemoryUsageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the memory high watermark (peak usage).
    /// </summary>
    public long PeakMemoryBytes { get; }

    /// <summary>
    /// Gets the current allocated memory.
    /// </summary>
    public long CurrentMemoryBytes { get; }
}

/// <summary>
/// Interface for components that expose performance monitoring.
/// </summary>
public interface IPerformanceMonitorable
{
    /// <summary>
    /// Gets performance metrics for the component.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Performance metrics.</returns>
    public Task<PerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an operation execution time.
    /// </summary>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    public void RecordExecutionTime(string operationName, double durationMs);
}

/// <summary>
/// Interface for components that expose error tracking.
/// </summary>
public interface IErrorMonitorable
{
    /// <summary>
    /// Gets error statistics for the component.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Error statistics.</returns>
    public Task<ErrorStatistics> GetErrorStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an error occurrence.
    /// </summary>
    /// <param name="error">The error that occurred.</param>
    public void RecordError(Exception error);

    /// <summary>
    /// Gets the current error rate (errors per operation).
    /// </summary>
    public double ErrorRate { get; }
}

/// <summary>
/// Interface for components that expose resource leak detection.
/// </summary>
public interface IResourceMonitorable
{
    /// <summary>
    /// Checks for potential resource leaks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resource leak detection result.</returns>
    public Task<ResourceLeakResult> CheckForLeaksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of currently allocated resources.
    /// </summary>
    public int AllocatedResourceCount { get; }

    /// <summary>
    /// Gets the count of resources that have been released.
    /// </summary>
    public int ReleasedResourceCount { get; }
}

/// <summary>
/// Result of a health check operation.
/// </summary>
public sealed class HealthCheckResult
{
    /// <summary>
    /// Gets the health status.
    /// </summary>
    public required HealthStatus Status { get; init; }

    /// <summary>
    /// Gets an optional description of the health status.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets additional data about the health check.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }

    /// <summary>
    /// Gets the timestamp of the health check.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the duration of the health check.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    public static HealthCheckResult Healthy(string? description = null) => new()
    {
        Status = HealthStatus.Healthy,
        Description = description
    };

    /// <summary>
    /// Creates a degraded result.
    /// </summary>
    public static HealthCheckResult Degraded(string? description = null) => new()
    {
        Status = HealthStatus.Degraded,
        Description = description
    };

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    public static HealthCheckResult Unhealthy(string? description = null) => new()
    {
        Status = HealthStatus.Critical,
        Description = description
    };
}

/// <summary>
/// Result of memory usage monitoring.
/// </summary>
public sealed class MemoryUsageResult
{
    /// <summary>
    /// Gets the current allocated memory in bytes.
    /// </summary>
    public long CurrentBytes { get; init; }

    /// <summary>
    /// Gets the peak allocated memory in bytes.
    /// </summary>
    public long PeakBytes { get; init; }

    /// <summary>
    /// Gets the available memory in bytes.
    /// </summary>
    public long AvailableBytes { get; init; }

    /// <summary>
    /// Gets the memory utilization as a percentage (0.0 to 1.0).
    /// </summary>
    public double Utilization { get; init; }

    /// <summary>
    /// Gets the timestamp of the measurement.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Performance metrics result.
/// </summary>
public sealed class PerformanceMetrics
{
    /// <summary>
    /// Gets the total number of operations executed.
    /// </summary>
    public long TotalOperations { get; init; }

    /// <summary>
    /// Gets the average operation duration in milliseconds.
    /// </summary>
    public double AverageDurationMs { get; init; }

    /// <summary>
    /// Gets the 50th percentile (median) duration in milliseconds.
    /// </summary>
    public double P50DurationMs { get; init; }

    /// <summary>
    /// Gets the 95th percentile duration in milliseconds.
    /// </summary>
    public double P95DurationMs { get; init; }

    /// <summary>
    /// Gets the 99th percentile duration in milliseconds.
    /// </summary>
    public double P99DurationMs { get; init; }

    /// <summary>
    /// Gets the minimum duration in milliseconds.
    /// </summary>
    public double MinDurationMs { get; init; }

    /// <summary>
    /// Gets the maximum duration in milliseconds.
    /// </summary>
    public double MaxDurationMs { get; init; }

    /// <summary>
    /// Gets the operations per second throughput.
    /// </summary>
    public double OperationsPerSecond { get; init; }

    /// <summary>
    /// Gets the timestamp of the measurement.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Error statistics result.
/// </summary>
public sealed class ErrorStatistics
{
    /// <summary>
    /// Gets the total number of errors.
    /// </summary>
    public long TotalErrors { get; init; }

    /// <summary>
    /// Gets the total number of operations (including errors).
    /// </summary>
    public long TotalOperations { get; init; }

    /// <summary>
    /// Gets the error rate (errors / operations).
    /// </summary>
    public double ErrorRate => TotalOperations > 0 ? (double)TotalErrors / TotalOperations : 0;

    /// <summary>
    /// Gets the most recent error, if any.
    /// </summary>
    public Exception? LastError { get; init; }

    /// <summary>
    /// Gets the timestamp of the last error, if any.
    /// </summary>
    public DateTimeOffset? LastErrorTime { get; init; }

    /// <summary>
    /// Gets error counts grouped by exception type.
    /// </summary>
    public IReadOnlyDictionary<string, long>? ErrorsByType { get; init; }

    /// <summary>
    /// Gets the timestamp of the measurement.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Resource leak detection result.
/// </summary>
public sealed class ResourceLeakResult
{
    /// <summary>
    /// Gets whether potential leaks were detected.
    /// </summary>
    public bool HasPotentialLeaks { get; init; }

    /// <summary>
    /// Gets the number of potentially leaked resources.
    /// </summary>
    public int LeakedResourceCount { get; init; }

    /// <summary>
    /// Gets descriptions of detected leaks.
    /// </summary>
    public IReadOnlyList<string>? LeakDescriptions { get; init; }

    /// <summary>
    /// Gets the total allocated resources.
    /// </summary>
    public int TotalAllocated { get; init; }

    /// <summary>
    /// Gets the total released resources.
    /// </summary>
    public int TotalReleased { get; init; }

    /// <summary>
    /// Gets the timestamp of the detection.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
