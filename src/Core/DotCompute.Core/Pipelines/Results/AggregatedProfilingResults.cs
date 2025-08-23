// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines.Statistics;
using DotCompute.Core.Pipelines.Analysis;

namespace DotCompute.Core.Pipelines.Results;

/// <summary>
/// Contains aggregated profiling results across multiple executions of a pipeline.
/// Provides statistical analysis, trends, and patterns observed across executions.
/// </summary>
public sealed class AggregatedProfilingResults
{
    /// <summary>
    /// Gets the unique identifier for the pipeline these aggregated results represent.
    /// Links the results to all executions of this specific pipeline.
    /// </summary>
    /// <value>The pipeline identifier as a string.</value>
    public required string PipelineId { get; init; }

    /// <summary>
    /// Gets the total number of pipeline executions included in this analysis.
    /// Provides context for the statistical significance of the aggregated results.
    /// </summary>
    /// <value>The execution count as an integer.</value>
    public required int ExecutionCount { get; init; }

    /// <summary>
    /// Gets the statistical metrics computed across all analyzed executions.
    /// Includes mean, median, standard deviation, and percentile data.
    /// </summary>
    /// <value>The statistical metrics summary.</value>
    public required StatisticalMetrics Statistics { get; init; }

    /// <summary>
    /// Gets the performance trends observed over the analysis period.
    /// Identifies whether performance is improving, stable, or degrading over time.
    /// </summary>
    /// <value>A read-only list of performance trends.</value>
    public required IReadOnlyList<PerformanceTrend> Trends { get; init; }

    /// <summary>
    /// Gets the most common performance bottlenecks identified across executions.
    /// Highlights recurring performance issues that should be prioritized for optimization.
    /// </summary>
    /// <value>A read-only list of common bottleneck information.</value>
    public required IReadOnlyList<BottleneckInfo> CommonBottlenecks { get; init; }
}