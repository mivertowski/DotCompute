// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Pipelines.Models;

/// <summary>
/// Comprehensive metrics collected during pipeline execution.
/// Provides detailed performance, resource usage, and timing information.
/// </summary>
public sealed class PipelineExecutionMetrics
{
    /// <summary>
    /// Gets or sets the unique identifier for this execution.
    /// </summary>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Gets or sets the name of the pipeline that was executed.
    /// </summary>
    public required string PipelineName { get; init; }

    /// <summary>
    /// Gets or sets the start time of pipeline execution.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// Gets or sets the end time of pipeline execution.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets the total execution duration.
    /// </summary>
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

    /// <summary>
    /// Gets or sets whether the execution completed successfully.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets whether the execution completed successfully (legacy property for backward compatibility).
    /// </summary>
    [Obsolete("Use IsSuccessful instead")]
    public bool Success
    {
        get => IsSuccessful;
        set => IsSuccessful = value;
    }

    /// <summary>
    /// Gets or sets the error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the exception that caused failure, if any.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the total number of stages in the pipeline.
    /// </summary>
    public int TotalStages { get; set; }

    /// <summary>
    /// Gets or sets the number of stages that completed successfully.
    /// </summary>
    public int CompletedStages { get; set; }

    /// <summary>
    /// Gets or sets the number of stages that failed.
    /// </summary>
    public int FailedStages { get; set; }

    /// <summary>
    /// Gets or sets the number of stages that were skipped.
    /// </summary>
    public int SkippedStages { get; set; }

    /// <summary>
    /// Gets or sets detailed metrics for each stage.
    /// </summary>
    public List<StageExecutionMetrics> StageMetrics { get; set; } = new List<StageExecutionMetrics>();

    /// <summary>
    /// Gets or sets memory usage statistics.
    /// </summary>
    public MemoryUsageMetrics? MemoryMetrics { get; set; }

    /// <summary>
    /// Gets the peak memory usage in bytes during pipeline execution.
    /// </summary>
    public long PeakMemoryUsage => MemoryMetrics?.PeakMemoryUsage ?? 0L;

    /// <summary>
    /// Gets or sets compute resource usage statistics.
    /// </summary>
    public ComputeResourceMetrics? ComputeMetrics { get; set; }

    /// <summary>
    /// Gets or sets data transfer statistics.
    /// </summary>
    public DataTransferMetrics? TransferMetrics { get; set; }

    /// <summary>
    /// Gets or sets the backend used for execution.
    /// </summary>
    public string? Backend { get; set; }

    /// <summary>
    /// Gets or sets the device used for execution.
    /// </summary>
    public ComputeDeviceType DeviceType { get; set; }

    /// <summary>
    /// Gets or sets cache performance statistics.
    /// </summary>
    public CacheMetrics? CacheMetrics { get; set; }

    /// <summary>
    /// Gets or sets optimization metrics if optimizations were applied.
    /// </summary>
    public OptimizationMetrics? OptimizationMetrics { get; set; }

    /// <summary>
    /// Gets or sets parallelization metrics for concurrent execution.
    /// </summary>
    public ParallelizationMetrics? ParallelizationMetrics { get; set; }

    /// <summary>
    /// Gets or sets throughput metrics for the pipeline execution.
    /// </summary>
    public ThroughputMetrics? ThroughputMetrics { get; set; }

    /// <summary>
    /// Gets or sets the total execution time of the pipeline.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the number of stage executions.
    /// </summary>
    public int StageExecutions { get; set; }

    /// <summary>
    /// Gets or sets the time spent in computation (excluding transfers and overhead).
    /// </summary>
    public TimeSpan ComputationTime { get; set; }

    /// <summary>
    /// Gets or sets the time spent in data transfers.
    /// </summary>
    public TimeSpan DataTransferTime { get; set; }

    /// <summary>
    /// Gets or sets the number of parallel executions.
    /// </summary>
    public int ParallelExecutions { get; set; }

    /// <summary>
    /// Gets or sets the execution status.
    /// </summary>
    public ExecutionStatus ExecutionStatus { get; set; }

    /// <summary>
    /// Gets or sets additional metrics for the pipeline.
    /// </summary>
    public IDictionary<string, object> AdditionalMetrics { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets memory usage statistics.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets compute utilization percentage.
    /// </summary>
    public double ComputeUtilization { get; set; }

    /// <summary>
    /// Gets or sets memory bandwidth utilization percentage.
    /// </summary>
    public double MemoryBandwidthUtilization { get; set; }

    /// <summary>
    /// Gets or sets execution times for individual stages.
    /// </summary>
    public IDictionary<string, double> StageExecutionTimes { get; set; } = new Dictionary<string, double>();

    /// <summary>
    /// Gets or sets custom user-defined metrics.
    /// </summary>
    public IDictionary<string, object> CustomMetrics { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the throughput in operations per second.
    /// </summary>
    public double Throughput { get; set; }

    /// <summary>
    /// Gets or sets the latency in milliseconds.
    /// </summary>
    public double LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the energy consumption in joules.
    /// </summary>
    public double EnergyConsumptionJ { get; set; }

    /// <summary>
    /// Gets or sets quality metrics for the execution results.
    /// </summary>
    public QualityMetrics? QualityMetrics { get; set; }

    /// <summary>
    /// Adds a custom metric to the collection.
    /// </summary>
    /// <param name="name">Name of the metric</param>
    /// <param name="value">Value of the metric</param>
    public void AddCustomMetric(string name, object value)
    {
        CustomMetrics[name] = value;
    }

    /// <summary>
    /// Gets a custom metric value.
    /// </summary>
    /// <typeparam name="T">Type of the metric value</typeparam>
    /// <param name="name">Name of the metric</param>
    /// <returns>The metric value, or default if not found</returns>
    public T? GetCustomMetric<T>(string name)
    {
        return CustomMetrics.TryGetValue(name, out var value) && value is T typedValue ? typedValue : default;
    }

    /// <summary>
    /// Calculates the average stage execution time.
    /// </summary>
    /// <returns>Average stage execution time</returns>
    public TimeSpan GetAverageStageTime()
    {
        if (StageMetrics.Count == 0) return TimeSpan.Zero;

        var totalTicks = 0L;
        foreach (var stage in StageMetrics)
        {
            totalTicks += stage.Duration.Ticks;
        }

        return new TimeSpan(totalTicks / StageMetrics.Count);
    }

    /// <summary>
    /// Gets the longest running stage.
    /// </summary>
    /// <returns>The stage with the longest execution time, or null if no stages</returns>
    public StageExecutionMetrics? GetLongestStage()
    {
        StageExecutionMetrics? longest = null;
        var maxDuration = TimeSpan.Zero;

        foreach (var stage in StageMetrics)
        {
            if (stage.Duration > maxDuration)
            {
                maxDuration = stage.Duration;
                longest = stage;
            }
        }

        return longest;
    }

    /// <summary>
    /// Calculates the efficiency ratio (successful stages / total stages).
    /// </summary>
    /// <returns>Efficiency ratio between 0 and 1</returns>
    public double GetEfficiencyRatio()
    {
        return TotalStages > 0 ? (double)CompletedStages / TotalStages : 0.0;
    }
}

/// <summary>
/// Memory usage metrics during pipeline execution.
/// </summary>
public sealed class MemoryUsageMetrics
{
    /// <summary>
    /// Gets or sets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the total memory allocated in bytes.
    /// </summary>
    public long TotalMemoryAllocated { get; set; }

    /// <summary>
    /// Gets or sets the number of memory allocations.
    /// </summary>
    public int AllocationCount { get; set; }

    /// <summary>
    /// Gets or sets the number of garbage collection cycles.
    /// </summary>
    public int GarbageCollectionCount { get; set; }

    /// <summary>
    /// Gets or sets whether memory pooling was used.
    /// </summary>
    public bool MemoryPoolingUsed { get; set; }

    /// <summary>
    /// Gets or sets the memory pool hit rate.
    /// </summary>
    public double MemoryPoolHitRate { get; set; }
}

/// <summary>
/// Compute resource usage metrics.
/// </summary>
public sealed class ComputeResourceMetrics
{
    /// <summary>
    /// Gets or sets the average CPU utilization percentage.
    /// </summary>
    public double AverageCpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the peak CPU utilization percentage.
    /// </summary>
    public double PeakCpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the average GPU utilization percentage.
    /// </summary>
    public double AverageGpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the peak GPU utilization percentage.
    /// </summary>
    public double PeakGpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the number of compute units used.
    /// </summary>
    public int ComputeUnitsUsed { get; set; }

    /// <summary>
    /// Gets or sets the thread count during execution.
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Gets or sets the degree of parallelism achieved.
    /// </summary>
    public int DegreeOfParallelism { get; set; }
}

/// <summary>
/// Data transfer metrics for memory and device operations.
/// </summary>
public sealed class DataTransferMetrics
{
    /// <summary>
    /// Gets or sets the total bytes transferred.
    /// </summary>
    public long TotalBytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets the number of transfer operations.
    /// </summary>
    public int TransferCount { get; set; }

    /// <summary>
    /// Gets or sets the average transfer rate in bytes per second.
    /// </summary>
    public double AverageTransferRate { get; set; }

    /// <summary>
    /// Gets or sets the peak transfer rate in bytes per second.
    /// </summary>
    public double PeakTransferRate { get; set; }

    /// <summary>
    /// Gets or sets the total time spent in data transfers.
    /// </summary>
    public TimeSpan TotalTransferTime { get; set; }

    /// <summary>
    /// Gets or sets transfer statistics by type.
    /// </summary>
    public IDictionary<DataTransferType, TransferTypeMetrics> TransfersByType { get; set; } =
        new Dictionary<DataTransferType, TransferTypeMetrics>();
}

/// <summary>
/// Types of data transfers in the pipeline.
/// </summary>
public enum DataTransferType
{
    /// <summary>
    /// Transfer from host memory to device memory.
    /// </summary>
    HostToDevice,

    /// <summary>
    /// Transfer from device memory to host memory.
    /// </summary>
    DeviceToHost,

    /// <summary>
    /// Transfer between different device memories.
    /// </summary>
    DeviceToDevice,

    /// <summary>
    /// Transfer within host memory.
    /// </summary>
    HostToHost,

    /// <summary>
    /// Peer-to-peer transfer between devices.
    /// </summary>
    PeerToPeer
}

/// <summary>
/// Metrics for a specific transfer type.
/// </summary>
public sealed class TransferTypeMetrics
{
    /// <summary>
    /// Gets or sets the number of transfers of this type.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the total bytes transferred of this type.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets the total time spent in transfers of this type.
    /// </summary>
    public TimeSpan TotalTime { get; set; }

    /// <summary>
    /// Gets or sets the average transfer rate for this type.
    /// </summary>
    public double AverageRate { get; set; }
}

/// <summary>
/// Cache performance metrics.
/// </summary>
public sealed class CacheMetrics
{
    /// <summary>
    /// Gets or sets the total number of cache accesses.
    /// </summary>
    public int TotalAccesses { get; set; }

    /// <summary>
    /// Gets or sets the number of cache hits.
    /// </summary>
    public int CacheHits { get; set; }

    /// <summary>
    /// Gets or sets the number of cache misses.
    /// </summary>
    public int CacheMisses { get; set; }

    /// <summary>
    /// Gets the cache hit rate as a percentage.
    /// </summary>
    public double HitRate => TotalAccesses > 0 ? (double)CacheHits / TotalAccesses * 100 : 0;

    /// <summary>
    /// Gets or sets the average cache access time.
    /// </summary>
    public TimeSpan AverageAccessTime { get; set; }
}

/// <summary>
/// Optimization metrics when optimizations are applied.
/// </summary>
public sealed class OptimizationMetrics
{
    /// <summary>
    /// Gets or sets the time spent on optimization.
    /// </summary>
    public TimeSpan OptimizationTime { get; set; }

    /// <summary>
    /// Gets or sets the performance improvement factor.
    /// </summary>
    public double PerformanceImprovement { get; set; }

    /// <summary>
    /// Gets or sets the memory usage improvement factor.
    /// </summary>
    public double MemoryImprovement { get; set; }

    /// <summary>
    /// Gets or sets the optimizations that were applied.
    /// </summary>
    public OptimizationType AppliedOptimizations { get; set; }

    /// <summary>
    /// Gets or sets whether optimization validation passed.
    /// </summary>
    public bool ValidationPassed { get; set; }
}

/// <summary>
/// Parallelization metrics for concurrent execution.
/// </summary>
public sealed class ParallelizationMetrics
{
    /// <summary>
    /// Gets or sets the maximum degree of parallelism used.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; }

    /// <summary>
    /// Gets or sets the average degree of parallelism during execution.
    /// </summary>
    public double AverageDegreeOfParallelism { get; set; }

    /// <summary>
    /// Gets or sets the parallel efficiency (0-1).
    /// </summary>
    public double ParallelEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the load balancing efficiency (0-1).
    /// </summary>
    public double LoadBalancingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the number of synchronization points.
    /// </summary>
    public int SynchronizationPoints { get; set; }

    /// <summary>
    /// Gets or sets the total time spent in synchronization.
    /// </summary>
    public TimeSpan SynchronizationTime { get; set; }
}

/// <summary>
/// Quality metrics for execution results.
/// </summary>
public sealed class QualityMetrics
{
    /// <summary>
    /// Gets or sets the numerical accuracy compared to reference implementation.
    /// </summary>
    public double NumericalAccuracy { get; set; }

    /// <summary>
    /// Gets or sets the determinism score (how consistent results are across runs).
    /// </summary>
    public double DeterminismScore { get; set; }

    /// <summary>
    /// Gets or sets the correctness verification result.
    /// </summary>
    public bool CorrectnessVerified { get; set; }

    /// <summary>
    /// Gets or sets any quality warnings or issues.
    /// </summary>
    public IList<string> QualityWarnings { get; set; } = new List<string>();
}