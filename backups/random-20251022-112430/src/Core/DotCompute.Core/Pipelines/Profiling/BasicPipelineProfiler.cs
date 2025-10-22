// <copyright file="BasicPipelineProfiler.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Interfaces.Pipelines.Profiling;
using DotCompute.Abstractions.Pipelines.Statistics;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

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
internal sealed partial class BasicPipelineProfiler(ILogger<BasicPipelineProfiler>? logger = null) : IPipelineProfiler
{
    // LoggerMessage delegates - Event ID range 9000-9099 for BasicPipelineProfiler (Telemetry module)
    private static readonly Action<ILogger, string, string, Exception?> _logPipelineStarted =
        LoggerMessage.Define<string, string>(
            MsLogLevel.Information,
            new EventId(9000, nameof(LogPipelineStarted)),
            "[PROFILER] Pipeline {PipelineId} started with execution ID: {ExecutionId}");

    private static readonly Action<ILogger, string, double, Exception?> _logPipelineCompleted =
        LoggerMessage.Define<string, double>(
            MsLogLevel.Information,
            new EventId(9001, nameof(LogPipelineCompleted)),
            "[PROFILER] Pipeline {ExecutionId} completed in {Duration:F2}ms");

    private static readonly Action<ILogger, string, string, Exception?> _logStageStarted =
        LoggerMessage.Define<string, string>(
            MsLogLevel.Information,
            new EventId(9002, nameof(LogStageStarted)),
            "[PROFILER] Stage {StageId} started for execution {ExecutionId}");

    private static readonly Action<ILogger, string, double, string, Exception?> _logStageCompleted =
        LoggerMessage.Define<string, double, string>(
            MsLogLevel.Information,
            new EventId(9003, nameof(LogStageCompleted)),
            "[PROFILER] Stage {StageId} completed in {Duration:F2}ms for execution {ExecutionId}");

    private static readonly Action<ILogger, double, string, string, Exception?> _logMemoryAllocation =
        LoggerMessage.Define<double, string, string>(
            MsLogLevel.Information,
            new EventId(9004, nameof(LogMemoryAllocation)),
            "[PROFILER] Memory allocated: {MemoryMB:F2}MB for {Purpose} (Execution: {ExecutionId})");

    private static readonly Action<ILogger, double, string, Exception?> _logMemoryDeallocation =
        LoggerMessage.Define<double, string>(
            MsLogLevel.Information,
            new EventId(9005, nameof(LogMemoryDeallocation)),
            "[PROFILER] Memory released: {MemoryMB:F2}MB (Execution: {ExecutionId})");

    private static readonly Action<ILogger, AbstractionsDataTransferType, double, double, double, string, Exception?> _logDataTransfer =
        LoggerMessage.Define<AbstractionsDataTransferType, double, double, double, string>(
            MsLogLevel.Information,
            new EventId(9006, nameof(LogDataTransfer)),
            "[PROFILER] Data transfer - Type: {TransferType}, Size: {SizeMB:F2}MB, Duration: {Duration:F2}ms, Rate: {RateMBps:F2}MB/s (Execution: {ExecutionId})");

    private static readonly Action<ILogger, string, double, long, double, string, Exception?> _logKernelExecution =
        LoggerMessage.Define<string, double, long, double, string>(
            MsLogLevel.Information,
            new EventId(9007, nameof(LogKernelExecution)),
            "[PROFILER] Kernel {KernelName}: {Duration:F2}ms, {WorkItems} items, {Utilization:P} utilization (Execution: {ExecutionId})");

    private static readonly Action<ILogger, string, double, string, Exception?> _logCustomMetric =
        LoggerMessage.Define<string, double, string>(
            MsLogLevel.Information,
            new EventId(9008, nameof(LogCustomMetric)),
            "[PROFILER] Custom metric - {MetricName}: {Value} (Execution: {ExecutionId})");

    // Wrapper methods
    private static void LogPipelineStarted(ILogger logger, string pipelineId, string executionId)
        => _logPipelineStarted(logger, pipelineId, executionId, null);

    private static void LogPipelineCompleted(ILogger logger, string executionId, double duration)
        => _logPipelineCompleted(logger, executionId, duration, null);

    private static void LogStageStarted(ILogger logger, string stageId, string executionId)
        => _logStageStarted(logger, stageId, executionId, null);

    private static void LogStageCompleted(ILogger logger, string stageId, double duration, string executionId)
        => _logStageCompleted(logger, stageId, duration, executionId, null);

    private static void LogMemoryAllocation(ILogger logger, double memoryMB, string purpose, string executionId)
        => _logMemoryAllocation(logger, memoryMB, purpose, executionId, null);

    private static void LogMemoryDeallocation(ILogger logger, double memoryMB, string executionId)
        => _logMemoryDeallocation(logger, memoryMB, executionId, null);

    private static void LogDataTransfer(ILogger logger, AbstractionsDataTransferType type, double sizeMB, double duration, double rate, string executionId)
        => _logDataTransfer(logger, type, sizeMB, duration, rate, executionId, null);

    private static void LogKernelExecution(ILogger logger, string kernelName, double duration, long workItems, double utilization, string executionId)
        => _logKernelExecution(logger, kernelName, duration, workItems, utilization, executionId, null);

    private static void LogCustomMetric(ILogger logger, string metricName, double value, string executionId)
        => _logCustomMetric(logger, metricName, value, executionId, null);

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
        if (_logger != null)
        {
            LogPipelineStarted(_logger, pipelineId, executionId);
        }
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
            if (_logger != null)
            {
                LogPipelineCompleted(_logger, executionId, duration.TotalMilliseconds);
            }
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
        if (_logger != null)
        {
            LogStageStarted(_logger, stageId, executionId);
        }
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
            if (_logger != null)
            {
                LogStageCompleted(_logger, stageId, duration.TotalMilliseconds, executionId);
            }
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
        if (_logger != null)
        {
            LogMemoryAllocation(_logger, bytes / 1024.0 / 1024.0, purpose, executionId);
        }
    }

    /// <summary>
    /// Records a memory deallocation event.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="bytes">The number of bytes deallocated.</param>
    public void RecordMemoryDeallocation(string executionId, long bytes)
    {
        if (_logger != null)
        {
            LogMemoryDeallocation(_logger, bytes / 1024.0 / 1024.0, executionId);
        }
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
        if (_logger != null)
        {
            LogDataTransfer(_logger, type, bytes / 1024.0 / 1024.0, duration.TotalMilliseconds, rate, executionId);
        }
    }

    /// <summary>
    /// Records kernel execution statistics.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="stats">The kernel execution statistics.</param>
    public void RecordKernelExecution(string executionId, KernelExecutionStats stats)
    {
        if (_logger != null)
        {
            LogKernelExecution(_logger, stats.KernelName, stats.ExecutionTime.TotalMilliseconds,
                stats.WorkItemsProcessed, stats.ComputeUtilization, executionId);
        }
    }

    /// <summary>
    /// Records a custom metric.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="name">The metric name.</param>
    /// <param name="value">The metric value.</param>
    public void RecordCustomMetric(string executionId, string name, double value)
    {
        if (_logger != null)
        {
            LogCustomMetric(_logger, metricName: name, value, executionId);
        }
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