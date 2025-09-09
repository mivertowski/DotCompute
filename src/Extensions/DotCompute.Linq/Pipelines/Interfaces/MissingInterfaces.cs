// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Pipelines.Models;

/// <summary>
/// Temporary interface definition for IAdvancedPipelineOptimizer.
/// This will be moved to proper location once infrastructure is ready.
/// </summary>
public interface IAdvancedPipelineOptimizer
{
    /// <summary>
    /// Optimizes a pipeline using advanced query plan optimization.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <returns>Optimized pipeline</returns>
    Task<IKernelPipeline> OptimizeQueryPlanAsync(IKernelPipeline pipeline);

    /// <summary>
    /// Applies kernel fusion optimizations to the pipeline.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <returns>Pipeline with fused kernels</returns>
    Task<IKernelPipeline> FuseKernelsAsync(IKernelPipeline pipeline);

    /// <summary>
    /// Optimizes memory access patterns in the pipeline.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <returns>Memory-optimized pipeline</returns>
    Task<IKernelPipeline> OptimizeMemoryAccessAsync(IKernelPipeline pipeline);
}

/// <summary>
/// Represents pipeline diagnostic information.
/// </summary>
public interface IPipelineDiagnostics
{
    /// <summary>Gets the number of stages in the pipeline.</summary>
    int StageCount { get; }

    /// <summary>Gets the total execution time in milliseconds.</summary>
    double TotalExecutionTimeMs { get; }

    /// <summary>Gets the cache hit rate as a percentage.</summary>
    double CacheHitRate { get; }

    /// <summary>Gets detailed stage performance metrics.</summary>
    IReadOnlyList<IStagePerformanceMetrics> StageMetrics { get; }

    /// <summary>Gets memory usage statistics.</summary>
    IMemoryUsageStatistics MemoryUsage { get; }

    /// <summary>Gets identified performance bottlenecks.</summary>
    IReadOnlyList<BottleneckInfo> Bottlenecks { get; }
}

/// <summary>
/// Stage performance metrics interface.
/// </summary>
public interface IStagePerformanceMetrics
{
    /// <summary>Gets the stage name.</summary>
    string StageName { get; }

    /// <summary>Gets the execution time for this stage.</summary>
    TimeSpan ExecutionTime { get; }

    /// <summary>Gets the memory usage for this stage.</summary>
    long MemoryUsage { get; }

    /// <summary>Gets the backend used for this stage.</summary>
    string Backend { get; }

    /// <summary>Gets stage-specific performance data.</summary>
    IReadOnlyDictionary<string, object> PerformanceData { get; }
}

/// <summary>
/// Memory usage statistics interface.
/// </summary>
public interface IMemoryUsageStatistics
{
    /// <summary>Gets the peak memory usage in bytes.</summary>
    long PeakMemoryUsage { get; }

    /// <summary>Gets the average memory usage in bytes.</summary>
    long AverageMemoryUsage { get; }

    /// <summary>Gets the number of allocations.</summary>
    int AllocationCount { get; }

    /// <summary>Gets the total allocated memory in bytes.</summary>
    long TotalAllocatedMemory { get; }

    /// <summary>Gets memory usage by stage.</summary>
    IReadOnlyDictionary<string, long> MemoryByStage { get; }
}

/// <summary>
/// Pipeline error types for comprehensive error handling.
/// </summary>
[Flags]
public enum PipelineErrorType
{
    /// <summary>No errors.</summary>
    None = 0,

    /// <summary>Data validation errors.</summary>
    DataValidationError = 1 << 0,

    /// <summary>Computation errors (overflow, NaN, etc.).</summary>
    ComputationError = 1 << 1,

    /// <summary>Memory allocation errors.</summary>
    MemoryAllocationError = 1 << 2,

    /// <summary>Backend compilation errors.</summary>
    CompilationError = 1 << 3,

    /// <summary>Kernel execution errors.</summary>
    KernelExecutionError = 1 << 4,

    /// <summary>Resource unavailability errors.</summary>
    ResourceUnavailableError = 1 << 5,

    /// <summary>Timeout errors.</summary>
    TimeoutError = 1 << 6,

    /// <summary>Synchronization errors.</summary>
    SynchronizationError = 1 << 7,

    /// <summary>Data transfer errors.</summary>
    DataTransferError = 1 << 8,

    /// <summary>Configuration errors.</summary>
    ConfigurationError = 1 << 9,

    /// <summary>Network communication errors.</summary>
    NetworkError = 1 << 10,

    /// <summary>Security or permission errors.</summary>
    SecurityError = 1 << 11,

    /// <summary>Hardware errors.</summary>
    HardwareError = 1 << 12,

    /// <summary>All error types.</summary>
    All = int.MaxValue
}

/// <summary>
/// Pipeline error recovery result.
/// </summary>
public class PipelineErrorRecoveryResult<T>
{
    /// <summary>
    /// Gets whether the recovery was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets the recovered value if successful.
    /// </summary>
    public T? Value { get; set; }

    /// <summary>
    /// Gets whether a value was recovered.
    /// </summary>
    public bool HasValue => IsSuccess && Value != null;

    /// <summary>
    /// Gets the error that caused the recovery attempt.
    /// </summary>
    public Exception? OriginalError { get; set; }

    /// <summary>
    /// Gets recovery actions that were attempted.
    /// </summary>
    public List<string> RecoveryActions { get; set; } = new();

    /// <summary>
    /// Gets any warnings from the recovery process.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Simple implementation of pipeline diagnostics for testing.
/// </summary>
public class SimplePipelineDiagnostics : IPipelineDiagnostics
{
    public int StageCount { get; set; }
    public double TotalExecutionTimeMs { get; set; }
    public double CacheHitRate { get; set; }
    public IReadOnlyList<IStagePerformanceMetrics> StageMetrics { get; set; } = new List<IStagePerformanceMetrics>();
    public IMemoryUsageStatistics MemoryUsage { get; set; } = new SimpleMemoryUsageStatistics();
    public IReadOnlyList<BottleneckInfo> Bottlenecks { get; set; } = new List<BottleneckInfo>();
}

/// <summary>
/// Simple implementation of stage performance metrics.
/// </summary>
public class SimpleStagePerformanceMetrics : IStagePerformanceMetrics
{
    public string StageName { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
    public long MemoryUsage { get; set; }
    public string Backend { get; set; } = "CPU";
    public IReadOnlyDictionary<string, object> PerformanceData { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Simple implementation of memory usage statistics.
/// </summary>
public class SimpleMemoryUsageStatistics : IMemoryUsageStatistics
{
    public long PeakMemoryUsage { get; set; }
    public long AverageMemoryUsage { get; set; }
    public int AllocationCount { get; set; }
    public long TotalAllocatedMemory { get; set; }
    public IReadOnlyDictionary<string, long> MemoryByStage { get; set; } = new Dictionary<string, long>();
}

/// <summary>
/// Resource utilization metrics interface.
/// </summary>
public interface IResourceUtilizationMetrics
{
    /// <summary>Gets CPU utilization as a percentage.</summary>
    double CpuUtilization { get; }

    /// <summary>Gets memory utilization as a percentage.</summary>
    double MemoryUtilization { get; }

    /// <summary>Gets GPU utilization as a percentage.</summary>
    double GpuUtilization { get; }

    /// <summary>Gets peak memory usage in bytes.</summary>
    long PeakMemoryUsage { get; }

    /// <summary>Gets average memory usage in bytes.</summary>
    long AverageMemoryUsage { get; }
}

/// <summary>
/// Cache utilization metrics interface.
/// </summary>
public interface ICacheUtilizationMetrics
{
    /// <summary>Gets cache hit rate as a percentage.</summary>
    double HitRate { get; }

    /// <summary>Gets cache miss rate as a percentage.</summary>
    double MissRate { get; }

    /// <summary>Gets total cache requests.</summary>
    long TotalRequests { get; }

    /// <summary>Gets cache size in bytes.</summary>
    long CacheSize { get; }

    /// <summary>Gets eviction count.</summary>
    int EvictionCount { get; }
}

/// <summary>
/// Performance insights interface.
/// </summary>
public interface IPerformanceInsights
{
    /// <summary>Gets optimization recommendations.</summary>
    IReadOnlyList<string> Recommendations { get; }

    /// <summary>Gets identified performance bottlenecks.</summary>
    IReadOnlyList<BottleneckInfo> Bottlenecks { get; }

    /// <summary>Gets performance improvement opportunities.</summary>
    IReadOnlyList<string> ImprovementOpportunities { get; }

    /// <summary>Gets performance rating (0.0 to 1.0).</summary>
    double PerformanceRating { get; }
}

/// <summary>
/// Stage execution metrics interface.
/// </summary>
public interface IStageExecutionMetrics
{
    /// <summary>Gets the stage name.</summary>
    string StageName { get; }

    /// <summary>Gets execution start time.</summary>
    DateTimeOffset StartTime { get; }

    /// <summary>Gets execution end time.</summary>
    DateTimeOffset EndTime { get; }

    /// <summary>Gets execution duration.</summary>
    TimeSpan Duration { get; }

    /// <summary>Gets whether execution was successful.</summary>
    bool IsSuccess { get; }

    /// <summary>Gets any exception that occurred.</summary>
    Exception? Exception { get; }
}