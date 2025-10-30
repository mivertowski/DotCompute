// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;

namespace DotCompute.Abstractions.Performance;

/// <summary>
/// Comprehensive performance metrics collection for DotCompute operations.
/// This is the canonical PerformanceMetrics type used across all projects.
/// </summary>
public sealed class PerformanceMetrics
{
    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the kernel execution time in milliseconds (GPU-specific).
    /// </summary>
    public long KernelExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the memory transfer time in milliseconds.
    /// </summary>
    public long MemoryTransferTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the total execution time including all operations.
    /// </summary>
    public long TotalExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in milliseconds.
    /// </summary>
    public double AverageTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the minimum execution time in milliseconds.
    /// </summary>
    public double MinTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the maximum execution time in milliseconds.
    /// </summary>
    public double MaxTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the standard deviation of execution times.
    /// </summary>
    public double StandardDeviation { get; set; }

    /// <summary>
    /// Gets or sets the throughput in gigabytes per second.
    /// </summary>
    public double ThroughputGBps { get; set; }

    /// <summary>
    /// Gets or sets the compute utilization percentage (0.0 to 100.0).
    /// </summary>
    public double ComputeUtilization { get; set; }

    /// <summary>
    /// Gets or sets the memory utilization percentage (0.0 to 100.0).
    /// </summary>
    public double MemoryUtilization { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate percentage (0.0 to 100.0).
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the number of operations per second.
    /// </summary>
    public long OperationsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the total floating point operations performed.
    /// </summary>
    public long TotalFlops { get; set; }

    /// <summary>
    /// Gets or sets the number of times this operation was called.
    /// </summary>
    public int CallCount { get; set; }

    /// <summary>
    /// Gets or sets the name or description of the operation being measured.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when these metrics were collected.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets additional custom metrics specific to the backend or operation.
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; } = [];

    /// <summary>
    /// Gets or sets the error information if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets a value indicating whether the operation completed successfully.
    /// </summary>
    public bool IsSuccessful => string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Creates a new PerformanceMetrics instance with basic timing information.
    /// </summary>
    /// <param name="stopwatch">The stopwatch used to measure execution time.</param>
    /// <param name="operation">The name of the operation.</param>
    /// <returns>A new PerformanceMetrics instance.</returns>
    public static PerformanceMetrics FromStopwatch(Stopwatch stopwatch, string operation = "")
    {
        return new PerformanceMetrics
        {
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            TotalExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            AverageTimeMs = stopwatch.Elapsed.TotalMilliseconds,
            MinTimeMs = stopwatch.Elapsed.TotalMilliseconds,
            MaxTimeMs = stopwatch.Elapsed.TotalMilliseconds,
            Operation = operation,
            CallCount = 1
        };
    }

    /// <summary>
    /// Creates a new PerformanceMetrics instance with timing information from a TimeSpan.
    /// </summary>
    /// <param name="elapsed">The elapsed time.</param>
    /// <param name="operation">The name of the operation.</param>
    /// <returns>A new PerformanceMetrics instance.</returns>
    public static PerformanceMetrics FromTimeSpan(TimeSpan elapsed, string operation = "")
    {
        return new PerformanceMetrics
        {
            ExecutionTimeMs = (long)elapsed.TotalMilliseconds,
            TotalExecutionTimeMs = (long)elapsed.TotalMilliseconds,
            AverageTimeMs = elapsed.TotalMilliseconds,
            MinTimeMs = elapsed.TotalMilliseconds,
            MaxTimeMs = elapsed.TotalMilliseconds,
            Operation = operation,
            CallCount = 1
        };
    }

    /// <summary>
    /// Merges multiple performance metrics into a single aggregated result.
    /// </summary>
    /// <param name="metrics">The metrics to merge.</param>
    /// <returns>An aggregated PerformanceMetrics instance.</returns>
    public static PerformanceMetrics Aggregate(IEnumerable<PerformanceMetrics> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var metricsList = metrics.ToList();
        if (metricsList.Count == 0)
        {

            return new PerformanceMetrics();
        }


        var result = new PerformanceMetrics
        {
            TotalExecutionTimeMs = metricsList.Sum(m => m.TotalExecutionTimeMs),
            KernelExecutionTimeMs = metricsList.Sum(m => m.KernelExecutionTimeMs),
            MemoryTransferTimeMs = metricsList.Sum(m => m.MemoryTransferTimeMs),
            TotalFlops = metricsList.Sum(m => m.TotalFlops),
            CallCount = metricsList.Sum(m => m.CallCount),
            MemoryUsageBytes = metricsList.Sum(m => m.MemoryUsageBytes),
            PeakMemoryUsageBytes = metricsList.Max(m => m.PeakMemoryUsageBytes),

            AverageTimeMs = metricsList.Average(m => m.AverageTimeMs),
            MinTimeMs = metricsList.Min(m => m.MinTimeMs),
            MaxTimeMs = metricsList.Max(m => m.MaxTimeMs),

            ComputeUtilization = metricsList.Average(m => m.ComputeUtilization),
            MemoryUtilization = metricsList.Average(m => m.MemoryUtilization),
            CacheHitRate = metricsList.Average(m => m.CacheHitRate),
            ThroughputGBps = metricsList.Average(m => m.ThroughputGBps),

            Operation = "Aggregated",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Calculate standard deviation
        var times = metricsList.Select(m => m.AverageTimeMs).ToList();
        var mean = times.Average();
        var sumOfSquares = times.Sum(t => Math.Pow(t - mean, 2));
        result.StandardDeviation = Math.Sqrt(sumOfSquares / times.Count);

        return result;
    }

    /// <summary>
    /// Returns a string representation of the performance metrics.
    /// </summary>
    public override string ToString()
    {
        return $"PerformanceMetrics[Operation={Operation}, " +
               $"ExecutionTime={ExecutionTimeMs}ms, " +
               $"Throughput={ThroughputGBps:F2} GB/s, " +
               $"CallCount={CallCount}]";
    }
}
