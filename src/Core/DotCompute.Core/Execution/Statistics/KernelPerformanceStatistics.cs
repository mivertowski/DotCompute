// <copyright file="KernelPerformanceStatistics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Statistics;

/// <summary>
/// Performance statistics for a compiled kernel.
/// Contains comprehensive metrics about kernel execution and performance characteristics
/// including execution count, timing, throughput, and efficiency measurements.
/// </summary>
public sealed class KernelPerformanceStatistics
{
    /// <summary>
    /// Gets or initializes the kernel name.
    /// </summary>
    /// <value>The unique identifier for the kernel.</value>
    public required string KernelName { get; init; }

    /// <summary>
    /// Gets or initializes the device ID where the kernel executes.
    /// </summary>
    /// <value>The identifier of the target accelerator device.</value>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Gets or initializes the total number of kernel executions.
    /// </summary>
    /// <value>The count of times this kernel has been invoked.</value>
    public long ExecutionCount { get; init; }

    /// <summary>
    /// Gets or initializes the total execution time across all invocations.
    /// </summary>
    /// <value>The cumulative execution time in milliseconds.</value>
    public double TotalExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets or initializes the average execution time per invocation.
    /// </summary>
    /// <value>The mean execution time in milliseconds.</value>
    public double AverageExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets or initializes the kernel compilation timestamp.
    /// </summary>
    /// <value>When the kernel was compiled and became available for execution.</value>
    public DateTimeOffset CompilationTime { get; init; }

    /// <summary>
    /// Gets the execution frequency (executions per minute).
    /// </summary>
    /// <value>The rate of kernel execution since compilation.</value>
    public double ExecutionFrequency
    {
        get
        {
            var elapsed = DateTimeOffset.UtcNow - CompilationTime;
            return elapsed.TotalMinutes > 0 ? ExecutionCount / elapsed.TotalMinutes : 0;
        }
    }

    /// <summary>
    /// Gets the throughput in executions per second.
    /// </summary>
    /// <value>The effective kernel throughput based on total execution time.</value>
    public double Throughput
    {
        get
        {
            return TotalExecutionTimeMs > 0 ? (ExecutionCount * 1000.0) / TotalExecutionTimeMs : 0;
        }
    }

    /// <summary>
    /// Gets the efficiency ratio as a percentage.
    /// </summary>
    /// <value>The percentage of time spent in actual execution versus overhead.</value>
    public double EfficiencyPercentage
    {
        get
        {
            if (ExecutionCount == 0)
            {
                return 0;
            }

            var elapsed = DateTimeOffset.UtcNow - CompilationTime;
            var totalElapsed = elapsed.TotalMilliseconds;

            return totalElapsed > 0 ? (TotalExecutionTimeMs / totalElapsed) * 100.0 : 0;
        }
    }

    /// <summary>
    /// Gets a summary description of the kernel performance.
    /// </summary>
    /// <returns>A formatted string describing key performance metrics.</returns>
    public override string ToString()
    {
        return $"Kernel '{KernelName}' on {DeviceId}: {ExecutionCount} executions, " +
               $"avg {AverageExecutionTimeMs:F2}ms, throughput {Throughput:F2}/sec";
    }
}
