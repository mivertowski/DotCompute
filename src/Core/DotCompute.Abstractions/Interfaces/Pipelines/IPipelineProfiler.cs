// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Results;
using DotCompute.Abstractions.Pipelines.Statistics;

namespace DotCompute.Abstractions.Interfaces.Pipelines.Profiling;

/// <summary>
/// Interface for comprehensive pipeline profiling capabilities.
/// Provides methods for tracking pipeline execution performance and resource utilization.
/// </summary>
public interface IPipelineProfiler
{
    /// <summary>
    /// Starts profiling a new pipeline execution.
    /// Initializes tracking for a complete pipeline run from start to finish.
    /// </summary>
    /// <param name="pipelineId">The unique identifier for the pipeline being executed.</param>
    /// <param name="executionId">The unique identifier for this specific execution instance.</param>
    public void StartPipelineExecution(string pipelineId, string executionId);

    /// <summary>
    /// Ends profiling for a pipeline execution.
    /// Completes the tracking and makes the execution data available for analysis.
    /// </summary>
    /// <param name="executionId">The unique identifier for the execution instance to end.</param>
    public void EndPipelineExecution(string executionId);

    /// <summary>
    /// Starts profiling a specific stage within a pipeline execution.
    /// Enables detailed performance analysis at the individual stage level.
    /// </summary>
    /// <param name="executionId">The execution instance that contains this stage.</param>
    /// <param name="stageId">The unique identifier for the stage being profiled.</param>
    public void StartStageExecution(string executionId, string stageId);

    /// <summary>
    /// Ends profiling for a specific stage within a pipeline execution.
    /// Completes the stage-level tracking and updates the execution profile.
    /// </summary>
    /// <param name="executionId">The execution instance that contains this stage.</param>
    /// <param name="stageId">The unique identifier for the stage to end profiling.</param>
    public void EndStageExecution(string executionId, string stageId);

    /// <summary>
    /// Records a memory allocation event during pipeline execution.
    /// Tracks memory usage patterns for performance analysis and optimization.
    /// </summary>
    /// <param name="executionId">The execution instance where the allocation occurred.</param>
    /// <param name="bytes">The number of bytes allocated.</param>
    /// <param name="purpose">A description of the allocation purpose for analysis.</param>
    public void RecordMemoryAllocation(string executionId, long bytes, string purpose);

    /// <summary>
    /// Records a memory deallocation event during pipeline execution.
    /// Tracks memory cleanup patterns and potential leak detection.
    /// </summary>
    /// <param name="executionId">The execution instance where the deallocation occurred.</param>
    /// <param name="bytes">The number of bytes deallocated.</param>
    public void RecordMemoryDeallocation(string executionId, long bytes);

    /// <summary>
    /// Records a data transfer operation during pipeline execution.
    /// Tracks data movement patterns and bandwidth utilization.
    /// </summary>
    /// <param name="executionId">The execution instance where the transfer occurred.</param>
    /// <param name="bytes">The number of bytes transferred.</param>
    /// <param name="duration">The time taken to complete the transfer.</param>
    /// <param name="type">The type of data transfer (host-to-device, device-to-host, etc.).</param>
    public void RecordDataTransfer(string executionId, long bytes, TimeSpan duration, DataTransferType type);

    /// <summary>
    /// Records detailed kernel execution statistics during pipeline execution.
    /// Provides comprehensive kernel performance data for analysis.
    /// </summary>
    /// <param name="executionId">The execution instance where the kernel was executed.</param>
    /// <param name="stats">Detailed statistics about the kernel execution.</param>
    public void RecordKernelExecution(string executionId, KernelExecutionStats stats);

    /// <summary>
    /// Records a custom metric value during pipeline execution.
    /// Allows tracking of domain-specific or implementation-specific metrics.
    /// </summary>
    /// <param name="executionId">The execution instance where the metric was captured.</param>
    /// <param name="name">The name of the custom metric.</param>
    /// <param name="value">The value of the custom metric.</param>
    public void RecordCustomMetric(string executionId, string name, double value);

    /// <summary>
    /// Retrieves comprehensive profiling results for a specific execution.
    /// Provides detailed analysis data for a single pipeline execution.
    /// </summary>
    /// <param name="executionId">The unique identifier for the execution to analyze.</param>
    /// <returns>Complete profiling results including metrics, timeline, and recommendations.</returns>
    public ProfilingResults GetResults(string executionId);

    /// <summary>
    /// Retrieves aggregated profiling results across multiple executions of a pipeline.
    /// Provides statistical analysis and trends across multiple pipeline runs.
    /// </summary>
    /// <param name="pipelineId">The unique identifier for the pipeline to analyze.</param>
    /// <returns>Aggregated profiling results including statistics and trend analysis.</returns>
    public AggregatedProfilingResults GetAggregatedResults(string pipelineId);
}
