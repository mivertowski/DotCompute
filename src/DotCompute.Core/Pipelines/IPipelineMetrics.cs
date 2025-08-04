// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines;

/// <summary>
/// Interface for pipeline performance metrics.
/// </summary>
public interface IPipelineMetrics
{
    /// <summary>
    /// Gets the pipeline identifier.
    /// </summary>
    public string PipelineId { get; }

    /// <summary>
    /// Gets the total execution count.
    /// </summary>
    public long ExecutionCount { get; }

    /// <summary>
    /// Gets the successful execution count.
    /// </summary>
    public long SuccessfulExecutionCount { get; }

    /// <summary>
    /// Gets the failed execution count.
    /// </summary>
    public long FailedExecutionCount { get; }

    /// <summary>
    /// Gets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; }

    /// <summary>
    /// Gets the minimum execution time.
    /// </summary>
    public TimeSpan MinExecutionTime { get; }

    /// <summary>
    /// Gets the maximum execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; }

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Gets throughput in executions per second.
    /// </summary>
    public double Throughput { get; }

    /// <summary>
    /// Gets the success rate.
    /// </summary>
    public double SuccessRate { get; }

    /// <summary>
    /// Gets average memory usage.
    /// </summary>
    public long AverageMemoryUsage { get; }

    /// <summary>
    /// Gets peak memory usage.
    /// </summary>
    public long PeakMemoryUsage { get; }

    /// <summary>
    /// Gets metrics by stage.
    /// </summary>
    public IReadOnlyDictionary<string, IStageMetrics> StageMetrics { get; }

    /// <summary>
    /// Gets custom metrics.
    /// </summary>
    public IReadOnlyDictionary<string, double> CustomMetrics { get; }

    /// <summary>
    /// Gets performance over time.
    /// </summary>
    public IReadOnlyList<TimeSeriesMetric> TimeSeries { get; }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset();

    /// <summary>
    /// Exports metrics to a specific format.
    /// </summary>
    public string Export(MetricsExportFormat format);
}

/// <summary>
/// Pipeline execution metrics.
/// </summary>
public sealed class PipelineExecutionMetrics
{
    /// <summary>
    /// Gets the execution identifier.
    /// </summary>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Gets the start time.
    /// </summary>
    public required DateTime StartTime { get; init; }

    /// <summary>
    /// Gets the end time.
    /// </summary>
    public required DateTime EndTime { get; init; }

    /// <summary>
    /// Gets the total duration.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets memory usage statistics.
    /// </summary>
    public required MemoryUsageStats MemoryUsage { get; init; }

    /// <summary>
    /// Gets compute utilization percentage.
    /// </summary>
    public required double ComputeUtilization { get; init; }

    /// <summary>
    /// Gets memory bandwidth utilization.
    /// </summary>
    public required double MemoryBandwidthUtilization { get; init; }

    /// <summary>
    /// Gets stage execution times.
    /// </summary>
    public required IReadOnlyDictionary<string, TimeSpan> StageExecutionTimes { get; init; }

    /// <summary>
    /// Gets data transfer times.
    /// </summary>
    public required IReadOnlyDictionary<string, TimeSpan> DataTransferTimes { get; init; }

    /// <summary>
    /// Gets kernel execution statistics.
    /// </summary>
    public IReadOnlyList<KernelExecutionStats>? KernelStats { get; init; }

    /// <summary>
    /// Gets optimization impact metrics.
    /// </summary>
    public OptimizationImpactMetrics? OptimizationImpact { get; init; }
}

/// <summary>
/// Interface for pipeline profiling.
/// </summary>
public interface IPipelineProfiler
{
    /// <summary>
    /// Starts profiling a pipeline execution.
    /// </summary>
    public void StartPipelineExecution(string pipelineId, string executionId);

    /// <summary>
    /// Ends profiling a pipeline execution.
    /// </summary>
    public void EndPipelineExecution(string executionId);

    /// <summary>
    /// Starts profiling a stage execution.
    /// </summary>
    public void StartStageExecution(string executionId, string stageId);

    /// <summary>
    /// Ends profiling a stage execution.
    /// </summary>
    public void EndStageExecution(string executionId, string stageId);

    /// <summary>
    /// Records a memory allocation.
    /// </summary>
    public void RecordMemoryAllocation(string executionId, long bytes, string purpose);

    /// <summary>
    /// Records a memory deallocation.
    /// </summary>
    public void RecordMemoryDeallocation(string executionId, long bytes);

    /// <summary>
    /// Records a data transfer.
    /// </summary>
    public void RecordDataTransfer(string executionId, long bytes, TimeSpan duration, DataTransferType type);

    /// <summary>
    /// Records kernel execution statistics.
    /// </summary>
    public void RecordKernelExecution(string executionId, KernelExecutionStats stats);

    /// <summary>
    /// Records a custom metric.
    /// </summary>
    public void RecordCustomMetric(string executionId, string name, double value);

    /// <summary>
    /// Gets profiling results for an execution.
    /// </summary>
    public ProfilingResults GetResults(string executionId);

    /// <summary>
    /// Gets aggregated profiling results.
    /// </summary>
    public AggregatedProfilingResults GetAggregatedResults(string pipelineId);
}

/// <summary>
/// Time series metric data point.
/// </summary>
public sealed class TimeSeriesMetric
{
    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the metric value.
    /// </summary>
    public required double Value { get; init; }

    /// <summary>
    /// Gets the metric name.
    /// </summary>
    public required string MetricName { get; init; }

    /// <summary>
    /// Gets additional labels.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Labels { get; init; }
}

/// <summary>
/// Kernel execution statistics.
/// </summary>
public sealed class KernelExecutionStats
{
    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public required string KernelName { get; init; }

    /// <summary>
    /// Gets the execution time.
    /// </summary>
    public required TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets the work items processed.
    /// </summary>
    public required long WorkItemsProcessed { get; init; }

    /// <summary>
    /// Gets the memory reads in bytes.
    /// </summary>
    public required long MemoryReadsBytes { get; init; }

    /// <summary>
    /// Gets the memory writes in bytes.
    /// </summary>
    public required long MemoryWritesBytes { get; init; }

    /// <summary>
    /// Gets the compute utilization percentage.
    /// </summary>
    public required double ComputeUtilization { get; init; }

    /// <summary>
    /// Gets the occupancy percentage.
    /// </summary>
    public required double Occupancy { get; init; }

    /// <summary>
    /// Gets cache hit rate.
    /// </summary>
    public double? CacheHitRate { get; init; }
}

/// <summary>
/// Optimization impact metrics.
/// </summary>
public sealed class OptimizationImpactMetrics
{
    /// <summary>
    /// Gets the speedup factor.
    /// </summary>
    public required double SpeedupFactor { get; init; }

    /// <summary>
    /// Gets memory savings in bytes.
    /// </summary>
    public required long MemorySavingsBytes { get; init; }

    /// <summary>
    /// Gets reduced kernel launches.
    /// </summary>
    public required int ReducedKernelLaunches { get; init; }

    /// <summary>
    /// Gets reduced memory transfers.
    /// </summary>
    public required int ReducedMemoryTransfers { get; init; }

    /// <summary>
    /// Gets optimization breakdown.
    /// </summary>
    public required IReadOnlyDictionary<OptimizationType, double> OptimizationBreakdown { get; init; }
}

/// <summary>
/// Data transfer types.
/// </summary>
public enum DataTransferType
{
    /// <summary>
    /// Host to device transfer.
    /// </summary>
    HostToDevice,

    /// <summary>
    /// Device to host transfer.
    /// </summary>
    DeviceToHost,

    /// <summary>
    /// Device to device transfer.
    /// </summary>
    DeviceToDevice,

    /// <summary>
    /// Peer to peer transfer.
    /// </summary>
    PeerToPeer
}

/// <summary>
/// Profiling results for a single execution.
/// </summary>
public sealed class ProfilingResults
{
    /// <summary>
    /// Gets the execution identifier.
    /// </summary>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Gets the pipeline identifier.
    /// </summary>
    public required string PipelineId { get; init; }

    /// <summary>
    /// Gets execution metrics.
    /// </summary>
    public required PipelineExecutionMetrics Metrics { get; init; }

    /// <summary>
    /// Gets the execution timeline.
    /// </summary>
    public required IReadOnlyList<TimelineEvent> Timeline { get; init; }

    /// <summary>
    /// Gets bottleneck analysis.
    /// </summary>
    public BottleneckAnalysis? BottleneckAnalysis { get; init; }

    /// <summary>
    /// Gets recommendations.
    /// </summary>
    public IReadOnlyList<PerformanceRecommendation>? Recommendations { get; init; }
}

/// <summary>
/// Aggregated profiling results across multiple executions.
/// </summary>
public sealed class AggregatedProfilingResults
{
    /// <summary>
    /// Gets the pipeline identifier.
    /// </summary>
    public required string PipelineId { get; init; }

    /// <summary>
    /// Gets the number of executions analyzed.
    /// </summary>
    public required int ExecutionCount { get; init; }

    /// <summary>
    /// Gets statistical metrics.
    /// </summary>
    public required StatisticalMetrics Statistics { get; init; }

    /// <summary>
    /// Gets performance trends.
    /// </summary>
    public required IReadOnlyList<PerformanceTrend> Trends { get; init; }

    /// <summary>
    /// Gets common bottlenecks.
    /// </summary>
    public required IReadOnlyList<BottleneckInfo> CommonBottlenecks { get; init; }
}

/// <summary>
/// Timeline event in execution.
/// </summary>
public sealed class TimelineEvent
{
    /// <summary>
    /// Gets the event timestamp.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the event type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the event description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the duration if applicable.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Gets associated stage ID.
    /// </summary>
    public string? StageId { get; init; }

    /// <summary>
    /// Gets event metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Bottleneck analysis results.
/// </summary>
public sealed class BottleneckAnalysis
{
    /// <summary>
    /// Gets the primary bottleneck.
    /// </summary>
    public required BottleneckInfo PrimaryBottleneck { get; init; }

    /// <summary>
    /// Gets secondary bottlenecks.
    /// </summary>
    public IReadOnlyList<BottleneckInfo>? SecondaryBottlenecks { get; init; }

    /// <summary>
    /// Gets the estimated performance impact.
    /// </summary>
    public required double EstimatedImpactPercentage { get; init; }
}

/// <summary>
/// Information about a performance bottleneck.
/// </summary>
public sealed class BottleneckInfo
{
    /// <summary>
    /// Gets the bottleneck type.
    /// </summary>
    public required BottleneckType Type { get; init; }

    /// <summary>
    /// Gets the bottleneck location.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Gets the bottleneck description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the severity score (0-1).
    /// </summary>
    public required double Severity { get; init; }

    /// <summary>
    /// Gets the frequency of occurrence.
    /// </summary>
    public required double Frequency { get; init; }
}

/// <summary>
/// Types of performance bottlenecks.
/// </summary>
public enum BottleneckType
{
    /// <summary>
    /// Memory bandwidth limitation.
    /// </summary>
    MemoryBandwidth,

    /// <summary>
    /// Compute throughput limitation.
    /// </summary>
    ComputeThroughput,

    /// <summary>
    /// Data transfer overhead.
    /// </summary>
    DataTransfer,

    /// <summary>
    /// Kernel launch overhead.
    /// </summary>
    KernelLaunch,

    /// <summary>
    /// Synchronization overhead.
    /// </summary>
    Synchronization,

    /// <summary>
    /// Memory allocation overhead.
    /// </summary>
    MemoryAllocation,

    /// <summary>
    /// Pipeline orchestration overhead.
    /// </summary>
    Orchestration
}

/// <summary>
/// Performance recommendation.
/// </summary>
public sealed class PerformanceRecommendation
{
    /// <summary>
    /// Gets the recommendation type.
    /// </summary>
    public required RecommendationType Type { get; init; }

    /// <summary>
    /// Gets the recommendation description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the estimated improvement.
    /// </summary>
    public required double EstimatedImprovement { get; init; }

    /// <summary>
    /// Gets the implementation difficulty.
    /// </summary>
    public required ImplementationDifficulty Difficulty { get; init; }

    /// <summary>
    /// Gets specific actions to take.
    /// </summary>
    public IReadOnlyList<string>? Actions { get; init; }
}

/// <summary>
/// Types of performance recommendations.
/// </summary>
public enum RecommendationType
{
    /// <summary>
    /// Enable kernel fusion.
    /// </summary>
    KernelFusion,

    /// <summary>
    /// Adjust work group size.
    /// </summary>
    WorkGroupSize,

    /// <summary>
    /// Optimize memory layout.
    /// </summary>
    MemoryLayout,

    /// <summary>
    /// Reduce synchronization.
    /// </summary>
    ReduceSynchronization,

    /// <summary>
    /// Use memory pooling.
    /// </summary>
    MemoryPooling,

    /// <summary>
    /// Parallelize sequential stages.
    /// </summary>
    Parallelization,

    /// <summary>
    /// Cache intermediate results.
    /// </summary>
    Caching
}

/// <summary>
/// Implementation difficulty levels.
/// </summary>
public enum ImplementationDifficulty
{
    /// <summary>
    /// Trivial change.
    /// </summary>
    Trivial,

    /// <summary>
    /// Easy to implement.
    /// </summary>
    Easy,

    /// <summary>
    /// Moderate difficulty.
    /// </summary>
    Moderate,

    /// <summary>
    /// Difficult to implement.
    /// </summary>
    Difficult,

    /// <summary>
    /// Very complex change.
    /// </summary>
    Complex
}

/// <summary>
/// Statistical metrics for aggregated results.
/// </summary>
public sealed class StatisticalMetrics
{
    /// <summary>
    /// Gets average metrics.
    /// </summary>
    public required PipelineExecutionMetrics Average { get; init; }

    /// <summary>
    /// Gets median metrics.
    /// </summary>
    public required PipelineExecutionMetrics Median { get; init; }

    /// <summary>
    /// Gets standard deviation.
    /// </summary>
    public required PipelineExecutionMetrics StandardDeviation { get; init; }

    /// <summary>
    /// Gets percentile metrics.
    /// </summary>
    public required IReadOnlyDictionary<int, PipelineExecutionMetrics> Percentiles { get; init; }
}

/// <summary>
/// Performance trend information.
/// </summary>
public sealed class PerformanceTrend
{
    /// <summary>
    /// Gets the metric name.
    /// </summary>
    public required string MetricName { get; init; }

    /// <summary>
    /// Gets the trend direction.
    /// </summary>
    public required TrendDirection Direction { get; init; }

    /// <summary>
    /// Gets the trend magnitude.
    /// </summary>
    public required double Magnitude { get; init; }

    /// <summary>
    /// Gets the confidence level.
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Gets the time period.
    /// </summary>
    public required TimeSpan Period { get; init; }
}

/// <summary>
/// Trend directions.
/// </summary>
public enum TrendDirection
{
    /// <summary>
    /// Improving performance.
    /// </summary>
    Improving,

    /// <summary>
    /// Stable performance.
    /// </summary>
    Stable,

    /// <summary>
    /// Degrading performance.
    /// </summary>
    Degrading
}

/// <summary>
/// Metrics export formats.
/// </summary>
public enum MetricsExportFormat
{
    /// <summary>
    /// JSON format.
    /// </summary>
    Json,

    /// <summary>
    /// CSV format.
    /// </summary>
    Csv,

    /// <summary>
    /// Prometheus format.
    /// </summary>
    Prometheus,

    /// <summary>
    /// OpenTelemetry format.
    /// </summary>
    OpenTelemetry
}
