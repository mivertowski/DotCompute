// <copyright file="BasicPipelineProfiler.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Interfaces.Pipelines.Profiling;
using DotCompute.Abstractions.Pipelines.Statistics;

// Type aliases to resolve ambiguous references
using AbstractionsDataTransferType = DotCompute.Abstractions.Pipelines.Enums.DataTransferType;
using AbstractionsProfilingResults = DotCompute.Abstractions.Pipelines.Results.ProfilingResults;
using AbstractionsAggregatedProfilingResults = DotCompute.Abstractions.Pipelines.Results.AggregatedProfilingResults;

namespace DotCompute.Core.Pipelines.Profiling;

/// <summary>
/// Basic pipeline profiler implementation for monitoring pipeline execution.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BasicPipelineProfiler"/> class.
/// </remarks>
/// <param name="logger">Optional logger for profiler events.</param>
internal sealed class BasicPipelineProfiler(ILogger<BasicPipelineProfiler>? logger = null) : IPipelineProfiler
{
    private readonly Dictionary<string, DateTime> _executionStarts = [];
    private readonly Dictionary<string, DateTime> _stageStarts = [];
    private readonly ILogger<BasicPipelineProfiler>? _logger = logger;

    /// <summary>
    /// Starts profiling a pipeline execution.
    /// </summary>
    /// <param name="pipelineId">The pipeline identifier.</param>
    /// <param name="executionId">The execution identifier.</param>
    public void StartPipelineExecution(string pipelineId, string executionId)
    {
        _executionStarts[executionId] = DateTime.UtcNow;
        _logger?.LogInformation("[PROFILER] Pipeline {PipelineId} started with execution ID: {ExecutionId}",

            pipelineId, executionId);
    }

    /// <summary>
    /// Ends profiling a pipeline execution.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    public void EndPipelineExecution(string executionId)
    {
        if (_executionStarts.TryGetValue(executionId, out var startTime))
        {
            var duration = DateTime.UtcNow - startTime;
            _logger?.LogInformation("[PROFILER] Pipeline {ExecutionId} completed in {Duration:F2}ms",

                executionId, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Starts profiling a stage execution.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="stageId">The stage identifier.</param>
    public void StartStageExecution(string executionId, string stageId)
    {
        _stageStarts[$"{executionId}_{stageId}"] = DateTime.UtcNow;
        _logger?.LogInformation("[PROFILER] Stage {StageId} started for execution {ExecutionId}",

            stageId, executionId);
    }

    /// <summary>
    /// Ends profiling a stage execution.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="stageId">The stage identifier.</param>
    public void EndStageExecution(string executionId, string stageId)
    {
        var key = $"{executionId}_{stageId}";
        if (_stageStarts.TryGetValue(key, out var startTime))
        {
            var duration = DateTime.UtcNow - startTime;
            _logger?.LogInformation("[PROFILER] Stage {StageId} completed in {Duration:F2}ms for execution {ExecutionId}",

                stageId, duration.TotalMilliseconds, executionId);
        }
    }

    /// <summary>
    /// Records a memory allocation event.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="bytes">The number of bytes allocated.</param>
    /// <param name="purpose">The purpose of the allocation.</param>
    public void RecordMemoryAllocation(string executionId, long bytes, string purpose)
    {
        _logger?.LogInformation("[PROFILER] Memory allocated: {MemoryMB:F2}MB for {Purpose} (Execution: {ExecutionId})",
            bytes / 1024.0 / 1024.0, purpose, executionId);
    }

    /// <summary>
    /// Records a memory deallocation event.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="bytes">The number of bytes deallocated.</param>
    public void RecordMemoryDeallocation(string executionId, long bytes)
    {
        _logger?.LogInformation("[PROFILER] Memory released: {MemoryMB:F2}MB (Execution: {ExecutionId})",
            bytes / 1024.0 / 1024.0, executionId);
    }

    /// <summary>
    /// Records a data transfer event.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="bytes">The number of bytes transferred.</param>
    /// <param name="duration">The duration of the transfer.</param>
    /// <param name="type">The type of data transfer.</param>
    public void RecordDataTransfer(string executionId, long bytes, TimeSpan duration, AbstractionsDataTransferType type)
    {
        var rate = bytes / duration.TotalSeconds / 1024.0 / 1024.0;
        _logger?.LogInformation(
            "[PROFILER] Data transfer - Type: {TransferType}, Size: {SizeMB:F2}MB, Duration: {Duration:F2}ms, Rate: {RateMBps:F2}MB/s (Execution: {ExecutionId})",
            type, bytes / 1024.0 / 1024.0, duration.TotalMilliseconds, rate, executionId);
    }

    /// <summary>
    /// Records kernel execution statistics.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="stats">The kernel execution statistics.</param>
    public void RecordKernelExecution(string executionId, KernelExecutionStats stats)
    {
        _logger?.LogInformation(
            "[PROFILER] Kernel {KernelName}: {Duration:F2}ms, {WorkItems} items, {Utilization:P} utilization (Execution: {ExecutionId})",
            stats.KernelName, stats.Timings.TotalMilliseconds, stats.WorkItemsProcessed,

            stats.ComputeUtilization, executionId);
    }

    /// <summary>
    /// Records a custom metric.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="name">The metric name.</param>
    /// <param name="value">The metric value.</param>
    public void RecordCustomMetric(string executionId, string name, double value)
    {
        _logger?.LogInformation("[PROFILER] Custom metric - {MetricName}: {Value} (Execution: {ExecutionId})",

            name, value, executionId);
    }

    /// <summary>
    /// Gets the profiling results for a specific execution.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <returns>The profiling results.</returns>
    public AbstractionsProfilingResults GetResults(string executionId)
    {
        // Return simplified results for this example
        return new AbstractionsProfilingResults
        {
            ExecutionId = executionId,
            PipelineId = "example",
            TotalExecutionTime = TimeSpan.FromMinutes(1),
            KernelStats = [],
            MemoryStats = new AbstractionsMemory.Pipelines.Results.MemoryUsageStats
            {
                PeakMemoryUsageBytes = 2 * 1024 * 1024,
                TotalAllocatedBytes = 1024 * 1024,
                AllocationCount = 10
            },
            Timeline = [],
            Recommendations = []
        };
    }

    /// <summary>
    /// Gets aggregated profiling results for a pipeline.
    /// </summary>
    /// <param name="pipelineId">The pipeline identifier.</param>
    /// <returns>The aggregated profiling results.</returns>
    public AbstractionsAggregatedProfilingResults GetAggregatedResults(string pipelineId)
    {
        // Return simplified aggregated results for this example
        return new AbstractionsAggregatedProfilingResults
        {
            PipelineId = pipelineId,
            ExecutionCount = 1,
            AverageExecutionTime = TimeSpan.FromMinutes(1),
            MinExecutionTime = TimeSpan.FromSeconds(45),
            MaxExecutionTime = TimeSpan.FromSeconds(75),
            Trends = []
        };
    }
}