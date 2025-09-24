// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Statistics;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Pipelines.Results;

/// <summary>
/// Contains comprehensive profiling results for a pipeline execution.
/// Provides detailed analysis and metrics for performance evaluation.
/// </summary>
public sealed class ProfilingResults
{
    /// <summary>
    /// Gets the unique execution identifier.
    /// </summary>
    /// <value>The execution ID as a string.</value>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Gets the pipeline identifier.
    /// </summary>
    /// <value>The pipeline ID as a string.</value>
    public required string PipelineId { get; init; }

    /// <summary>
    /// Gets the total execution time for the entire pipeline.
    /// </summary>
    /// <value>The total execution time as a TimeSpan.</value>
    public required TimeSpan TotalExecutionTime { get; init; }

    /// <summary>
    /// Gets the kernel execution statistics.
    /// </summary>
    /// <value>A collection of kernel execution statistics.</value>
    public required IReadOnlyList<KernelExecutionStats> KernelStats { get; init; }

    /// <summary>
    /// Gets the memory usage statistics.
    /// </summary>
    /// <value>Memory usage statistics for the execution.</value>
    public required MemoryUsageStats MemoryStats { get; init; }

    /// <summary>
    /// Gets the timeline of events during execution.
    /// </summary>
    /// <value>A collection of execution timeline events.</value>
    public required IReadOnlyList<ExecutionTimelineEvent> Timeline { get; init; }

    /// <summary>
    /// Gets performance recommendations based on the profiling data.
    /// </summary>
    /// <value>A collection of performance recommendations.</value>
    public required IReadOnlyList<PerformanceRecommendation> Recommendations { get; init; }
}

/// <summary>
/// Aggregated profiling results across multiple pipeline executions.
/// </summary>
public sealed class AggregatedProfilingResults
{
    /// <summary>
    /// Gets the pipeline identifier.
    /// </summary>
    /// <value>The pipeline ID as a string.</value>
    public required string PipelineId { get; init; }

    /// <summary>
    /// Gets the number of executions included in the aggregation.
    /// </summary>
    /// <value>The execution count as an integer.</value>
    public required int ExecutionCount { get; init; }

    /// <summary>
    /// Gets the average execution time across all executions.
    /// </summary>
    /// <value>The average execution time as a TimeSpan.</value>
    public required TimeSpan AverageExecutionTime { get; init; }

    /// <summary>
    /// Gets the minimum execution time observed.
    /// </summary>
    /// <value>The minimum execution time as a TimeSpan.</value>
    public required TimeSpan MinExecutionTime { get; init; }

    /// <summary>
    /// Gets the maximum execution time observed.
    /// </summary>
    /// <value>The maximum execution time as a TimeSpan.</value>
    public required TimeSpan MaxExecutionTime { get; init; }

    /// <summary>
    /// Gets the aggregated performance trends.
    /// </summary>
    /// <value>A collection of performance trend data.</value>
    public required IReadOnlyList<PerformanceTrend> Trends { get; init; }
}

/// <summary>
/// Memory usage statistics for pipeline execution.
/// </summary>
public sealed class MemoryUsageStats
{
    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// </summary>
    public required long PeakMemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets the total memory allocated in bytes.
    /// </summary>
    public required long TotalAllocatedBytes { get; init; }

    /// <summary>
    /// Gets the number of memory allocations.
    /// </summary>
    public required int AllocationCount { get; init; }
}

/// <summary>
/// Represents an event in the execution timeline.
/// </summary>
public sealed class ExecutionTimelineEvent
{
    /// <summary>
    /// Gets the timestamp of the event.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the event description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the event duration, if applicable.
    /// </summary>
    public TimeSpan? Duration { get; init; }
}

/// <summary>
/// Performance recommendation based on profiling analysis.
/// </summary>
public sealed class PerformanceRecommendation
{
    /// <summary>
    /// Gets the recommendation type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the recommendation description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the potential performance impact.
    /// </summary>
    public required string Impact { get; init; }
}

// PerformanceTrend class removed - using unified version from DotCompute.Abstractions.Types