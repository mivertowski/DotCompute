// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Abstractions.Performance;

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Historical performance statistics for a backend on a specific workload.
/// </summary>
public class BackendPerformanceStats
{
    public int SampleCount { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double ExecutionTimeStdDev { get; set; }
    public double MinExecutionTimeMs { get; set; }
    public double MaxExecutionTimeMs { get; set; }
    public double AverageThroughput { get; set; }
    public double AverageMemoryUsage { get; set; }
    public float ReliabilityScore { get; set; } // 0-1 based on success rate
    public DateTimeOffset LastUpdated { get; set; }

    /// <summary>
    /// Converts this BackendPerformanceStats to a PerformanceMetrics instance.
    /// </summary>
    /// <param name="operation">The operation name for the metrics.</param>
    /// <returns>A PerformanceMetrics instance representing these statistics.</returns>
    public PerformanceMetrics ToPerformanceMetrics(string operation = "Historical")
    {
        return new PerformanceMetrics
        {
            ExecutionTimeMs = (long)AverageExecutionTimeMs,
            TotalExecutionTimeMs = (long)AverageExecutionTimeMs,
            AverageTimeMs = AverageExecutionTimeMs,
            MinTimeMs = MinExecutionTimeMs,
            MaxTimeMs = MaxExecutionTimeMs,
            StandardDeviation = ExecutionTimeStdDev,
            ThroughputGBps = AverageThroughput,
            MemoryUsageBytes = (long)AverageMemoryUsage,
            CallCount = SampleCount,
            Operation = operation,
            Timestamp = LastUpdated,
            CustomMetrics =
            {
                ["ReliabilityScore"] = ReliabilityScore,
                ["SampleCount"] = SampleCount
            }
        };
    }

    /// <summary>
    /// Creates a BackendPerformanceStats from a PerformanceMetrics instance.
    /// </summary>
    /// <param name="metrics">The performance metrics to convert.</param>
    /// <returns>A new BackendPerformanceStats instance.</returns>
    public static BackendPerformanceStats FromPerformanceMetrics(PerformanceMetrics metrics)
    {
        return new BackendPerformanceStats
        {
            SampleCount = metrics.CallCount,
            AverageExecutionTimeMs = metrics.AverageTimeMs,
            ExecutionTimeStdDev = metrics.StandardDeviation,
            MinExecutionTimeMs = metrics.MinTimeMs,
            MaxExecutionTimeMs = metrics.MaxTimeMs,
            AverageThroughput = metrics.ThroughputGBps,
            AverageMemoryUsage = metrics.MemoryUsageBytes,
            ReliabilityScore = metrics.IsSuccessful ? 1.0f : 0.0f,
            LastUpdated = metrics.Timestamp
        };
    }
}