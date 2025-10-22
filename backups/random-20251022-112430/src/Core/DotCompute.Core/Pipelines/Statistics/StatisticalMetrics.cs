// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Models;

namespace DotCompute.Core.Pipelines.Statistics;

/// <summary>
/// Contains comprehensive statistical metrics computed across multiple pipeline executions.
/// Provides detailed statistical analysis including central tendency, variability, and distribution data.
/// </summary>
public sealed class StatisticalMetrics
{
    /// <summary>
    /// Gets the average (mean) metrics computed across all executions.
    /// Represents the typical performance characteristics of the pipeline.
    /// </summary>
    /// <value>The average execution metrics.</value>
    public required PipelineExecutionMetrics Average { get; init; }

    /// <summary>
    /// Gets the median metrics computed across all executions.
    /// Represents the middle value and is less affected by outliers than the mean.
    /// </summary>
    /// <value>The median execution metrics.</value>
    public required PipelineExecutionMetrics Median { get; init; }

    /// <summary>
    /// Gets the standard deviation metrics showing variability across executions.
    /// Indicates how much variation exists in pipeline performance.
    /// </summary>
    /// <value>The standard deviation execution metrics.</value>
    public required PipelineExecutionMetrics StandardDeviation { get; init; }

    /// <summary>
    /// Gets metrics at various percentile levels across all executions.
    /// Enables understanding of performance distribution and outlier identification.
    /// </summary>
    /// <value>A read-only dictionary mapping percentiles (e.g., 95, 99) to their corresponding metrics.</value>
    public required IReadOnlyDictionary<int, PipelineExecutionMetrics> Percentiles { get; init; }
}