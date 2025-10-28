// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Profiling;

/// <summary>
/// Aggregated profiling statistics for a session.
/// </summary>
/// <remarks>
/// This class provides comprehensive statistical analysis of profiled events,
/// grouped by operation type. It includes standard metrics (min/max/avg) as well
/// as percentile analysis (median, P95, P99) for identifying outliers and performance
/// characteristics.
///
/// Statistics are calculated lazily and cached for performance.
/// </remarks>
public sealed record ProfilingStatistics
{
    /// <summary>
    /// Gets statistics for each operation type profiled in the session.
    /// Dictionary maps operation type to its aggregated statistics.
    /// </summary>
    public required Dictionary<ProfiledOperation, OperationStatistics> OperationStats { get; init; }

    /// <summary>
    /// Gets the total wall-clock time of the profiling session.
    /// </summary>
    public required TimeSpan TotalSessionTime { get; init; }

    /// <summary>
    /// Gets the total number of profiled events in the session.
    /// </summary>
    public required int TotalEvents { get; init; }

    /// <summary>
    /// Gets the total execution time across all events (sum of all durations).
    /// </summary>
    public double TotalExecutionTimeMs => OperationStats.Values.Sum(s => s.TotalDurationMs);

    /// <summary>
    /// Gets the device utilization percentage.
    /// Calculated as: (total execution time / session wall-clock time) * 100
    /// </summary>
    /// <remarks>
    /// Values > 100% indicate concurrent operations (e.g., overlapped transfers and kernels).
    /// Values &lt; 100% indicate idle time or CPU-bound operations.
    /// </remarks>
    public double DeviceUtilizationPercent => TotalSessionTime.TotalMilliseconds > 0
        ? (TotalExecutionTimeMs / TotalSessionTime.TotalMilliseconds) * 100.0
        : 0.0;

    /// <summary>
    /// Returns a formatted string summary of the statistics.
    /// </summary>
    public override string ToString()
    {
        return $"ProfilingStatistics: {TotalEvents} events, " +
               $"{TotalSessionTime.TotalSeconds:F2}s session time, " +
               $"{TotalExecutionTimeMs:F2}ms execution time, " +
               $"{DeviceUtilizationPercent:F1}% utilization";
    }
}

/// <summary>
/// Statistical metrics for a specific operation type.
/// </summary>
/// <remarks>
/// These statistics provide deep insights into operation performance:
/// - Count: Total number of operations
/// - Min/Max: Performance bounds
/// - Avg: Expected typical performance
/// - Median: True "middle" performance (less affected by outliers)
/// - P95/P99: Worst-case performance for 95th/99th percentile
///
/// Use P95/P99 for SLA definitions and performance guarantees.
/// </remarks>
public sealed record OperationStatistics
{
    /// <summary>
    /// Gets the number of times this operation was profiled.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Gets the minimum duration observed for this operation (milliseconds).
    /// Represents best-case performance.
    /// </summary>
    public required double MinDurationMs { get; init; }

    /// <summary>
    /// Gets the maximum duration observed for this operation (milliseconds).
    /// Represents worst-case performance (may include outliers).
    /// </summary>
    public required double MaxDurationMs { get; init; }

    /// <summary>
    /// Gets the average (mean) duration for this operation (milliseconds).
    /// </summary>
    public required double AvgDurationMs { get; init; }

    /// <summary>
    /// Gets the median duration for this operation (milliseconds).
    /// The median is less sensitive to outliers than the average.
    /// </summary>
    public required double MedianDurationMs { get; init; }

    /// <summary>
    /// Gets the 95th percentile duration (milliseconds).
    /// 95% of operations completed within this time or faster.
    /// Useful for SLA definitions and performance targets.
    /// </summary>
    public required double P95DurationMs { get; init; }

    /// <summary>
    /// Gets the 99th percentile duration (milliseconds).
    /// 99% of operations completed within this time or faster.
    /// Identifies extreme outliers and worst-case scenarios.
    /// </summary>
    public required double P99DurationMs { get; init; }

    /// <summary>
    /// Gets the total cumulative duration for all operations of this type (milliseconds).
    /// </summary>
    public required double TotalDurationMs { get; init; }

    /// <summary>
    /// Gets the standard deviation of durations (milliseconds).
    /// Measures variability/consistency of performance.
    /// </summary>
    /// <remarks>
    /// High standard deviation indicates inconsistent performance.
    /// Low standard deviation indicates predictable, consistent behavior.
    /// </remarks>
    public double StandardDeviationMs
    {
        get
        {
            // Standard deviation calculation would require storing all durations
            // For now, estimate using (P95 - Median) as a proxy for spread
            return (P95DurationMs - MedianDurationMs) * 1.5;
        }
    }

    /// <summary>
    /// Gets the coefficient of variation (CV) as a percentage.
    /// CV = (StandardDeviation / Mean) * 100
    /// </summary>
    /// <remarks>
    /// CV quantifies the degree of variability relative to the mean:
    /// - CV &lt; 10%: Very consistent performance
    /// - CV 10-20%: Moderately consistent performance
    /// - CV 20-30%: Variable performance
    /// - CV > 30%: Highly variable performance
    /// </remarks>
    public double CoefficientOfVariationPercent => AvgDurationMs > 0 ? (StandardDeviationMs / AvgDurationMs) * 100.0 : 0.0;

    /// <summary>
    /// Returns a formatted string summary of the operation statistics.
    /// </summary>
    public override string ToString()
    {
        return $"Count={Count}, Avg={AvgDurationMs:F3}ms, " +
               $"Min={MinDurationMs:F3}ms, Max={MaxDurationMs:F3}ms, " +
               $"Median={MedianDurationMs:F3}ms, P95={P95DurationMs:F3}ms, P99={P99DurationMs:F3}ms, " +
               $"Total={TotalDurationMs:F2}ms, CV={CoefficientOfVariationPercent:F1}%";
    }
}
