// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Models;

/// <summary>
/// Comprehensive performance statistics for execution analysis.
/// </summary>
public class PerformanceStatistics
{
    /// <summary>
    /// Gets or sets the total number of executions analyzed.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in milliseconds.
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the minimum execution time in milliseconds.
    /// </summary>
    public double MinExecutionTimeMs { get; set; } = double.MaxValue;

    /// <summary>
    /// Gets or sets the maximum execution time in milliseconds.
    /// </summary>
    public double MaxExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the standard deviation of execution times.
    /// </summary>
    public double ExecutionTimeStdDev { get; set; }

    /// <summary>
    /// Gets or sets the average efficiency percentage.
    /// </summary>
    public double AverageEfficiencyPercentage { get; set; }

    /// <summary>
    /// Gets or sets the success rate as a percentage.
    /// </summary>
    public double SuccessRatePercentage { get; set; }

    /// <summary>
    /// Gets or sets the total GFLOPS-hours computed.
    /// </summary>
    public double TotalGFLOPSHours { get; set; }

    /// <summary>
    /// Gets or sets the average throughput in GFLOPS.
    /// </summary>
    public double AverageThroughputGFLOPS { get; set; }

    /// <summary>
    /// Gets or sets the average memory bandwidth in GB/s.
    /// </summary>
    public double AverageMemoryBandwidthGBps { get; set; }

    /// <summary>
    /// Gets or sets performance percentiles (P50, P95, P99, etc.).
    /// </summary>
    public Dictionary<string, double> Percentiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the time window for these statistics.
    /// </summary>
    public TimeRange TimeWindow { get; set; } = new();

    /// <summary>
    /// Gets or sets when these statistics were last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Calculates and updates percentiles from a collection of execution times.
    /// </summary>
    public void UpdatePercentiles(IEnumerable<double> executionTimes)
    {
        var sortedTimes = executionTimes.OrderBy(t => t).ToArray();
        if (sortedTimes.Length == 0) return;

        Percentiles["P50"] = GetPercentile(sortedTimes, 0.50);
        Percentiles["P90"] = GetPercentile(sortedTimes, 0.90);
        Percentiles["P95"] = GetPercentile(sortedTimes, 0.95);
        Percentiles["P99"] = GetPercentile(sortedTimes, 0.99);
    }

    private static double GetPercentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0) return 0;

        var index = percentile * (sortedValues.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper) return sortedValues[lower];

        var weight = index - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }
}