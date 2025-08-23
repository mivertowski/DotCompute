// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines.Statistics;

namespace DotCompute.Core.Pipelines.Metrics;

/// <summary>
/// Contains comprehensive performance metrics for a single pipeline execution.
/// Captures detailed information about resource utilization, timing, and optimization impact.
/// </summary>
public sealed class PipelineExecutionMetrics
{
    /// <summary>
    /// Gets the unique identifier for this pipeline execution.
    /// Used to correlate metrics across different analysis and monitoring systems.
    /// </summary>
    /// <value>The execution identifier as a string.</value>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Gets the timestamp when the pipeline execution started.
    /// Used for timeline analysis and performance trend identification.
    /// </summary>
    /// <value>The start time as a DateTime.</value>
    public required DateTime StartTime { get; init; }

    /// <summary>
    /// Gets the timestamp when the pipeline execution completed.
    /// Used for calculating execution duration and throughput metrics.
    /// </summary>
    /// <value>The end time as a DateTime.</value>
    public required DateTime EndTime { get; init; }

    /// <summary>
    /// Gets the total duration of the pipeline execution.
    /// Represents the wall-clock time from start to completion.
    /// </summary>
    /// <value>The execution duration as a TimeSpan.</value>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets comprehensive memory usage statistics for this execution.
    /// Includes peak usage, average usage, and allocation patterns.
    /// </summary>
    /// <value>The memory usage statistics.</value>
    public required MemoryUsageStats MemoryUsage { get; init; }

    /// <summary>
    /// Gets the compute utilization percentage during this execution.
    /// Indicates how effectively compute resources were used (0.0 to 1.0).
    /// </summary>
    /// <value>The compute utilization as a double between 0.0 and 1.0.</value>
    public required double ComputeUtilization { get; init; }

    /// <summary>
    /// Gets the memory bandwidth utilization percentage during this execution.
    /// Indicates how effectively memory bandwidth was used (0.0 to 1.0).
    /// </summary>
    /// <value>The memory bandwidth utilization as a double between 0.0 and 1.0.</value>
    public required double MemoryBandwidthUtilization { get; init; }

    /// <summary>
    /// Gets the execution times for individual stages within the pipeline.
    /// Enables identification of bottlenecks and optimization opportunities.
    /// </summary>
    /// <value>A read-only dictionary mapping stage names to their execution times.</value>
    public required IReadOnlyDictionary<string, TimeSpan> StageExecutionTimes { get; init; }

    /// <summary>
    /// Gets the data transfer times for different types of transfers.
    /// Includes host-to-device, device-to-host, and device-to-device transfers.
    /// </summary>
    /// <value>A read-only dictionary mapping transfer types to their durations.</value>
    public required IReadOnlyDictionary<string, TimeSpan> DataTransferTimes { get; init; }

    /// <summary>
    /// Gets detailed statistics for kernel executions during this pipeline run.
    /// Provides insights into kernel performance and optimization effectiveness.
    /// </summary>
    /// <value>A read-only list of kernel execution statistics, or null if not available.</value>
    public IReadOnlyList<KernelExecutionStats>? KernelStats { get; init; }

    /// <summary>
    /// Gets metrics showing the impact of various optimizations applied.
    /// Quantifies the benefit of optimization techniques like fusion and caching.
    /// </summary>
    /// <value>The optimization impact metrics, or null if not available.</value>
    public OptimizationImpactMetrics? OptimizationImpact { get; init; }
}