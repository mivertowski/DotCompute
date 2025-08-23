// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines.Metrics;
using DotCompute.Core.Pipelines.Analysis;
using DotCompute.Core.Pipelines.Recommendations;

namespace DotCompute.Core.Pipelines.Results;

/// <summary>
/// Contains comprehensive profiling results for a single pipeline execution.
/// Provides detailed analysis, timeline, and optimization recommendations.
/// </summary>
public sealed class ProfilingResults
{
    /// <summary>
    /// Gets the unique identifier for the pipeline execution these results represent.
    /// Links the results to the specific execution instance that was profiled.
    /// </summary>
    /// <value>The execution identifier as a string.</value>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Gets the unique identifier for the pipeline that was executed.
    /// Enables correlation of results across multiple executions of the same pipeline.
    /// </summary>
    /// <value>The pipeline identifier as a string.</value>
    public required string PipelineId { get; init; }

    /// <summary>
    /// Gets the comprehensive execution metrics collected during profiling.
    /// Includes timing, resource utilization, and optimization impact data.
    /// </summary>
    /// <value>The pipeline execution metrics.</value>
    public required PipelineExecutionMetrics Metrics { get; init; }

    /// <summary>
    /// Gets the detailed timeline of events that occurred during execution.
    /// Provides chronological insight into the execution flow and bottlenecks.
    /// </summary>
    /// <value>A read-only list of timeline events.</value>
    public required IReadOnlyList<TimelineEvent> Timeline { get; init; }

    /// <summary>
    /// Gets the analysis of performance bottlenecks identified during execution.
    /// Highlights the primary factors limiting pipeline performance.
    /// </summary>
    /// <value>The bottleneck analysis results, or null if analysis wasn't performed.</value>
    public BottleneckAnalysis? BottleneckAnalysis { get; init; }

    /// <summary>
    /// Gets the list of performance optimization recommendations.
    /// Provides actionable suggestions for improving pipeline performance.
    /// </summary>
    /// <value>A read-only list of performance recommendations, or null if not available.</value>
    public IReadOnlyList<PerformanceRecommendation>? Recommendations { get; init; }
}