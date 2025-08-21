// <copyright file="OperationMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Metrics;

/// <summary>
/// Operation-specific performance metrics.
/// Tracks detailed performance data for individual operations.
/// </summary>
public class OperationMetrics
{
    /// <summary>
    /// Gets the operation name.
    /// Identifies the specific operation being measured.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Gets the total execution count.
    /// Number of times this operation has been executed.
    /// </summary>
    public long ExecutionCount { get; init; }

    /// <summary>
    /// Gets the average execution time.
    /// Mean duration across all executions.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; init; }

    /// <summary>
    /// Gets the minimum execution time.
    /// Fastest recorded execution duration.
    /// </summary>
    public TimeSpan MinExecutionTime { get; init; }

    /// <summary>
    /// Gets the maximum execution time.
    /// Slowest recorded execution duration.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; init; }

    /// <summary>
    /// Gets the success rate.
    /// Percentage of successful executions (0.0 to 1.0).
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Gets custom metrics for this operation.
    /// Application-specific performance indicators.
    /// </summary>
    public Dictionary<string, double> CustomMetrics { get; init; } = [];
}