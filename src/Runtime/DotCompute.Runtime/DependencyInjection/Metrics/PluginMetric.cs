// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.DependencyInjection.Metrics;

/// <summary>
/// Individual plugin metric.
/// </summary>
public sealed record PluginMetric
{
    /// <summary>
    /// Gets the activation count.
    /// </summary>
    public int ActivationCount { get; init; }

    /// <summary>
    /// Gets the execution count.
    /// </summary>
    public int ExecutionCount { get; init; }

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; init; }

    /// <summary>
    /// Gets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime
        => ExecutionCount > 0 ? TimeSpan.FromTicks(TotalExecutionTime.Ticks / ExecutionCount) : TimeSpan.Zero;
}
