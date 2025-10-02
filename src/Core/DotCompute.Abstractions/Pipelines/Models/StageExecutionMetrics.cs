// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Abstractions.Pipelines.Models;

/// <summary>
/// Detailed execution metrics for individual pipeline stages.
/// Provides comprehensive information about stage performance, resource usage, and behavior.
/// </summary>
public sealed class StageExecutionMetrics
{
    /// <summary>
    /// Gets or sets the unique identifier for this stage execution.
    /// </summary>
    public required string StageId { get; init; }

    /// <summary>
    /// Gets or sets the name of the stage.
    /// </summary>
    public required string StageName { get; init; }

    /// <summary>
    /// Gets or sets the type of the pipeline stage.
    /// </summary>
    public PipelineStageType StageType { get; set; }

    /// <summary>
    /// Gets or sets the index of this stage in the pipeline.
    /// </summary>
    public int StageIndex { get; set; }

    /// <summary>
    /// Gets or sets the start time of stage execution.
    /// </summary>
    public required DateTime StartTime { get; init; }

    /// <summary>
    /// Gets or sets the end time of stage execution.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets the execution duration of the stage.
    /// </summary>
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

    /// <summary>
    /// Gets or sets whether the stage executed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the stage failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the exception that caused failure, if any.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the backend used for this stage execution.
    /// </summary>
    public string? Backend { get; set; }

    /// <summary>
    /// Gets or sets the compute device type used.
    /// </summary>
    public ComputeDeviceType DeviceType { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts for this stage.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets whether the stage result was cached.
    /// </summary>
    public bool WasCached { get; set; }

    /// <summary>
    /// Gets or sets whether the stage was skipped due to conditions.
    /// </summary>
    public bool WasSkipped { get; set; }

    /// <summary>
    /// Gets or sets the memory usage statistics for this stage.
    /// </summary>
    public StageMemoryMetrics? MemoryMetrics { get; set; }

    /// <summary>
    /// Gets or sets the compute resource utilization for this stage.
    /// </summary>
    public StageComputeMetrics? ComputeMetrics { get; set; }

    /// <summary>
    /// Gets or sets data transfer statistics for this stage.
    /// </summary>
    public StageDataTransferMetrics? DataTransferMetrics { get; set; }

    /// <summary>
    /// Gets or sets performance characteristics of the stage.
    /// </summary>
    public StagePerformanceMetrics? PerformanceMetrics { get; set; }

    /// <summary>
    /// Gets or sets synchronization metrics for parallel stages.
    /// </summary>
    public StageSynchronizationMetrics? SynchronizationMetrics { get; set; }

    /// <summary>
    /// Gets or sets optimization metrics if optimizations were applied.
    /// </summary>
    public StageOptimizationMetrics? OptimizationMetrics { get; set; }

    /// <summary>
    /// Gets or sets quality metrics for the stage execution.
    /// </summary>
    public StageQualityMetrics? QualityMetrics { get; set; }

    /// <summary>
    /// Gets or sets custom user-defined metrics for this stage.
    /// </summary>
    public IDictionary<string, object> CustomMetrics { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets warnings generated during stage execution.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets or sets debug information collected during execution.
    /// </summary>
    public IDictionary<string, object> DebugInfo { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the input data size processed by this stage in bytes.
    /// </summary>
    public long InputDataSize { get; set; }

    /// <summary>
    /// Gets or sets the output data size produced by this stage in bytes.
    /// </summary>
    public long OutputDataSize { get; set; }

    /// <summary>
    /// Gets or sets the processing rate in bytes per second.
    /// </summary>
    public double ProcessingRate => Duration.TotalSeconds > 0 ? InputDataSize / Duration.TotalSeconds : 0;

    /// <summary>
    /// Adds a custom metric to the stage metrics.
    /// </summary>
    /// <param name="name">Name of the metric</param>
    /// <param name="value">Value of the metric</param>
    public void AddCustomMetric(string name, object value) => CustomMetrics[name] = value;

    /// <summary>
    /// Gets a custom metric value.
    /// </summary>
    /// <typeparam name="T">Type of the metric value</typeparam>
    /// <param name="name">Name of the metric</param>
    /// <returns>The metric value, or default if not found</returns>
    public T? GetCustomMetric<T>(string name) => CustomMetrics.TryGetValue(name, out var value) && value is T typedValue ? typedValue : default;

    /// <summary>
    /// Adds a warning message to the stage metrics.
    /// </summary>
    /// <param name="warning">Warning message to add</param>
    public void AddWarning(string warning) => Warnings.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {warning}");

    /// <summary>
    /// Adds debug information to the stage metrics.
    /// </summary>
    /// <param name="key">Debug information key</param>
    /// <param name="value">Debug information value</param>
    public void AddDebugInfo(string key, object value) => DebugInfo[key] = value;

    /// <summary>
    /// Calculates the efficiency ratio for this stage.
    /// </summary>
    /// <returns>Efficiency ratio between 0 and 1</returns>
    public double GetEfficiencyRatio()
    {
        if (WasSkipped || !Success)
        {
            return 0.0;
        }


        if (WasCached)
        {
            return 1.0;
        }

        // Simple efficiency based on processing rate vs. theoretical maximum

        var theoreticalMaxRate = ComputeMetrics?.TheoreticalMaxThroughput ?? double.MaxValue;
        return theoreticalMaxRate > 0 ? Math.Min(ProcessingRate / theoreticalMaxRate, 1.0) : 0.0;
    }
}

/// <summary>
/// Memory usage metrics specific to a pipeline stage.
/// </summary>
public sealed class StageMemoryMetrics
{
    /// <summary>
    /// Gets or sets the peak memory usage during this stage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage during this stage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the memory allocated for this stage in bytes.
    /// </summary>
    public long AllocatedMemory { get; set; }

    /// <summary>
    /// Gets or sets the number of memory allocations performed.
    /// </summary>
    public int AllocationCount { get; set; }

    /// <summary>
    /// Gets or sets the number of memory deallocations performed.
    /// </summary>
    public int DeallocationCount { get; set; }

    /// <summary>
    /// Gets or sets whether memory pooling was used for this stage.
    /// </summary>
    public bool UsedMemoryPool { get; set; }

    /// <summary>
    /// Gets or sets the memory pool hit rate for this stage.
    /// </summary>
    public double MemoryPoolHitRate { get; set; }

    /// <summary>
    /// Gets or sets the memory fragmentation level.
    /// </summary>
    public double FragmentationLevel { get; set; }
}

/// <summary>
/// Compute resource utilization metrics for a pipeline stage.
/// </summary>
public sealed class StageComputeMetrics
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
    /// Gets or sets the number of compute units utilized.
    /// </summary>
    public int ComputeUnitsUsed { get; set; }

    /// <summary>
    /// Gets or sets the thread count used for this stage.
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Gets or sets the actual degree of parallelism achieved.
    /// </summary>
    public int ActualParallelism { get; set; }

    /// <summary>
    /// Gets or sets the theoretical maximum throughput for comparison.
    /// </summary>
    public double TheoreticalMaxThroughput { get; set; }

    /// <summary>
    /// Gets or sets the achieved throughput.
    /// </summary>
    public double AchievedThroughput { get; set; }

    /// <summary>
    /// Gets the compute efficiency as a ratio of achieved to theoretical throughput.
    /// </summary>
    public double ComputeEfficiency => TheoreticalMaxThroughput > 0 ? AchievedThroughput / TheoreticalMaxThroughput : 0.0;
}

/// <summary>
/// Data transfer metrics for a pipeline stage.
/// </summary>
public sealed class StageDataTransferMetrics
{
    /// <summary>
    /// Gets or sets the total bytes transferred during this stage.
    /// </summary>
    public long TotalBytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets the number of transfer operations.
    /// </summary>
    public int TransferOperationCount { get; set; }

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
    /// Gets or sets transfer statistics by direction.
    /// </summary>
    public IDictionary<DataTransferType, long> TransfersByType { get; } =
        new Dictionary<DataTransferType, long>();
}

/// <summary>
/// Performance characteristics for a pipeline stage.
/// </summary>
public sealed class StagePerformanceMetrics
{
    /// <summary>
    /// Gets or sets the operations per second achieved.
    /// </summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the latency in milliseconds.
    /// </summary>
    public double LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate for this stage.
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the instruction count for this stage.
    /// </summary>
    public long InstructionCount { get; set; }

    /// <summary>
    /// Gets or sets the instructions per clock cycle.
    /// </summary>
    public double InstructionsPerCycle { get; set; }

    /// <summary>
    /// Gets or sets the branch prediction accuracy.
    /// </summary>
    public double BranchPredictionAccuracy { get; set; }

    /// <summary>
    /// Gets or sets the energy consumption in joules.
    /// </summary>
    public double EnergyConsumptionJ { get; set; }
}

/// <summary>
/// Synchronization metrics for parallel pipeline stages.
/// </summary>
public sealed class StageSynchronizationMetrics
{
    /// <summary>
    /// Gets or sets the number of synchronization points in this stage.
    /// </summary>
    public int SynchronizationPointCount { get; set; }

    /// <summary>
    /// Gets or sets the total time spent in synchronization.
    /// </summary>
    public TimeSpan TotalSynchronizationTime { get; set; }

    /// <summary>
    /// Gets or sets the average synchronization wait time.
    /// </summary>
    public TimeSpan AverageSynchronizationWaitTime { get; set; }

    /// <summary>
    /// Gets or sets the maximum synchronization wait time.
    /// </summary>
    public TimeSpan MaxSynchronizationWaitTime { get; set; }

    /// <summary>
    /// Gets or sets the load balancing efficiency (0-1).
    /// </summary>
    public double LoadBalancingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the parallel efficiency achieved (0-1).
    /// </summary>
    public double ParallelEfficiency { get; set; }
}

/// <summary>
/// Optimization metrics for a pipeline stage.
/// </summary>
public sealed class StageOptimizationMetrics
{
    /// <summary>
    /// Gets or sets the optimizations applied to this stage.
    /// </summary>
    public OptimizationType AppliedOptimizations { get; set; }

    /// <summary>
    /// Gets or sets the time spent on optimization for this stage.
    /// </summary>
    public TimeSpan OptimizationTime { get; set; }

    /// <summary>
    /// Gets or sets the performance improvement factor from optimization.
    /// </summary>
    public double PerformanceImprovementFactor { get; set; }

    /// <summary>
    /// Gets or sets the memory usage improvement factor from optimization.
    /// </summary>
    public double MemoryImprovementFactor { get; set; }

    /// <summary>
    /// Gets or sets whether optimization validation passed for this stage.
    /// </summary>
    public bool OptimizationValidationPassed { get; set; }
}

/// <summary>
/// Quality metrics for a pipeline stage execution.
/// </summary>
public sealed class StageQualityMetrics
{
    /// <summary>
    /// Gets or sets the numerical accuracy of stage results.
    /// </summary>
    public double NumericalAccuracy { get; set; }

    /// <summary>
    /// Gets or sets the determinism score for this stage.
    /// </summary>
    public double DeterminismScore { get; set; }

    /// <summary>
    /// Gets or sets whether correctness validation passed.
    /// </summary>
    public bool CorrectnessValidationPassed { get; set; }

    /// <summary>
    /// Gets or sets quality issues detected in this stage.
    /// </summary>
    public IList<string> QualityIssues { get; } = [];

    /// <summary>
    /// Gets or sets the confidence level in the stage results (0-1).
    /// </summary>
    public double ConfidenceLevel { get; set; } = 1.0;
}