// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Optimization.Enums;

namespace DotCompute.Linq.Pipelines.Models;

#region Core Interface Bridge Types

/// <summary>
/// Interface for pipeline memory manager (bridges to Core interface).
/// </summary>
public interface IPipelineMemoryManager : IDisposable
{
    /// <summary>Allocates memory buffer of specified size.</summary>
    Task<IMemoryBuffer> AllocateAsync(long size);


    /// <summary>Releases allocated memory buffer.</summary>
    Task ReleaseAsync(IMemoryBuffer buffer);
}

/// <summary>
/// Interface for compute device (bridges to Core interface).
/// </summary>
public interface IComputeDevice : IDisposable
{
    /// <summary>Device name.</summary>
    string Name { get; }


    /// <summary>Device type.</summary>
    string Type { get; }


    /// <summary>Whether device is available.</summary>
    bool IsAvailable { get; }


    /// <summary>Initialize the device.</summary>
    Task InitializeAsync();
}

/// <summary>
/// Interface for memory buffer (bridges to Core interface).
/// </summary>
public interface IMemoryBuffer : IDisposable
{
    /// <summary>Buffer size in bytes.</summary>
    long Size { get; }


    /// <summary>Copy buffer to another buffer.</summary>
    Task CopyToAsync(IMemoryBuffer destination);
}

#endregion

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
    Task<object> OptimizeQueryPlanAsync(object pipeline);

    /// <summary>
    /// Applies kernel fusion optimizations to the pipeline.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <returns>Pipeline with fused kernels</returns>
    Task<object> FuseKernelsAsync(object pipeline);

    /// <summary>
    /// Optimizes memory access patterns in the pipeline.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <returns>Memory-optimized pipeline</returns>
    Task<object> OptimizeMemoryAccessAsync(object pipeline);
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

    /// <summary>Gets the peak memory usage in bytes.</summary>
    long PeakMemoryUsage { get; }
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
/// Pipeline validation result.
/// </summary>
public class PipelineValidationResult
{
    /// <summary>Gets whether the pipeline is valid.</summary>
    public bool IsValid { get; set; }


    /// <summary>Gets validation errors if any.</summary>
    public List<string> Errors { get; set; } = new();


    /// <summary>Gets validation warnings if any.</summary>
    public List<string> Warnings { get; set; } = new();
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
    public long PeakMemoryUsage { get; set; }
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

    /// <summary>Gets the peak memory usage in bytes.</summary>
    long PeakMemoryUsage { get; }

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

#region Missing Pipeline Interfaces

/// <summary>
/// Interface for kernel execution context.
/// </summary>
public interface IKernelExecutionContext
{
    /// <summary>Preferred backend for execution.</summary>
    string PreferredBackend { get; set; }

    /// <summary>Timeout in milliseconds.</summary>
    int TimeoutMs { get; set; }

    /// <summary>Whether to enable profiling.</summary>
    bool EnableProfiling { get; set; }

    /// <summary>Maximum memory usage.</summary>
    long MaxMemoryUsage { get; set; }

    /// <summary>Optimization level.</summary>
    OptimizationLevel OptimizationLevel { get; set; }
}

/// <summary>
/// Interface for pipeline configuration.
/// </summary>
public interface IPipelineConfiguration
{
    /// <summary>Preferred backend for execution.</summary>
    string PreferredBackend { get; set; }

    /// <summary>Whether to enable caching.</summary>
    bool EnableCaching { get; set; }

    /// <summary>Whether to enable profiling.</summary>
    bool EnableProfiling { get; set; }

    /// <summary>Maximum memory usage.</summary>
    long MaxMemoryUsage { get; set; }

    /// <summary>Timeout in seconds.</summary>
    int TimeoutSeconds { get; set; }

    /// <summary>Optimization level.</summary>
    OptimizationLevel OptimizationLevel { get; set; }
}

/// <summary>
/// Interface for pipeline execution context.
/// </summary>
public interface IPipelineExecutionContext
{
    /// <summary>Whether to enable detailed metrics.</summary>
    bool EnableDetailedMetrics { get; set; }

    /// <summary>Whether to enable profiling.</summary>
    bool EnableProfiling { get; set; }

    /// <summary>Whether to track memory usage.</summary>
    bool TrackMemoryUsage { get; set; }

    /// <summary>Whether to collect timing data.</summary>
    bool CollectTimingData { get; set; }
}

/// <summary>
/// Interface for pipeline execution result.
/// </summary>
public interface IPipelineExecutionResult<T>
{
    /// <summary>The execution result.</summary>
    T Result { get; }

    /// <summary>Pipeline metrics.</summary>
    IPipelineMetrics Metrics { get; }

    /// <summary>Execution success status.</summary>
    bool IsSuccess { get; }

    /// <summary>Any exception that occurred.</summary>
    Exception? Exception { get; }
}

/// <summary>
/// Interface for adaptive backend selector.
/// </summary>
public interface IAdaptiveBackendSelector
{
    /// <summary>
    /// Selects the optimal backend for given workload characteristics.
    /// </summary>
    /// <param name="characteristics">Workload characteristics</param>
    /// <returns>Selected backend name</returns>
    Task<string> SelectBackendAsync(WorkloadCharacteristics characteristics);
}

/// <summary>
/// Pipeline stage options for execution configuration.
/// </summary>
public class PipelineStageOptions
{
    /// <summary>Preferred backend for this stage.</summary>
    public string? PreferredBackend { get; set; }

    /// <summary>Whether to enable profiling.</summary>
    public bool EnableProfiling { get; set; }

    /// <summary>Whether to enable caching.</summary>
    public bool EnableCaching { get; set; }
    
    /// <summary>Whether to enable optimization.</summary>
    public bool EnableOptimization { get; set; }
    
    /// <summary>Whether to enable memory optimization.</summary>
    public bool EnableMemoryOptimization { get; set; }

    /// <summary>Timeout in milliseconds.</summary>
    public int TimeoutMs { get; set; } = 30000;
}

/// <summary>
/// Workload characteristics for backend selection.
/// </summary>
public class WorkloadCharacteristics
{
    /// <summary>Compute intensity (0.0 to 1.0).</summary>
    public double ComputeIntensity { get; set; }

    /// <summary>Memory intensity (0.0 to 1.0).</summary>
    public double MemoryIntensity { get; set; }

    /// <summary>Parallelism degree.</summary>
    public int ParallelismDegree { get; set; }

    /// <summary>Parallelism level (0.0 to 1.0).</summary>
    public double ParallelismLevel { get; set; }

    /// <summary>Data size in bytes.</summary>
    public long DataSize { get; set; }

    /// <summary>Expected number of operations.</summary>
    public long OperationCount { get; set; }

    /// <summary>Memory access pattern classification.</summary>
    public MemoryAccessPattern AccessPattern { get; set; }

    /// <summary>Additional custom optimization hints.</summary>
    public List<string> OptimizationHints { get; set; } = new();

    /// <summary>Secondary data size in bytes.</summary>
    public long SecondaryDataSize { get; set; }

    /// <summary>Workload variance (0.0 to 1.0) indicating variability in work per element.</summary>
    public double WorkloadVariance { get; set; }

    /// <summary>Workload type description.</summary>
    public string WorkloadType { get; set; } = string.Empty;

    /// <summary>Whether this workload is suitable for GPU execution.</summary>
    public bool IsGpuSuitable { get; set; }

    /// <summary>Parallelism potential (0.0 to 1.0).</summary>
    public double ParallelismPotential { get; set; }

    /// <summary>Hardware information for this workload.</summary>
    public object? Hardware { get; set; }
}

/// <summary>
/// Backend recommendation result.
/// </summary>
public class BackendRecommendation
{
    /// <summary>Recommended backend name.</summary>
    public string RecommendedBackend { get; set; } = "CPU";

    /// <summary>Confidence in recommendation (0.0 to 1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Reasoning for the backend recommendation.</summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>Backend performance estimates.</summary>
    public Dictionary<string, BackendEstimate> BackendEstimates { get; set; } = new();
}

/// <summary>
/// Backend performance estimate.
/// </summary>
public class BackendEstimate
{
    /// <summary>Estimated execution time.</summary>
    public TimeSpan EstimatedExecutionTime { get; set; }

    /// <summary>Estimated memory usage.</summary>
    public long EstimatedMemory { get; set; }

    /// <summary>Performance score (higher is better).</summary>
    public double PerformanceScore { get; set; }
}

// OptimizationLevel is now imported from DotCompute.Abstractions.Types

/// <summary>
/// Pipeline metrics interface.
/// </summary>
public interface IPipelineMetrics
{
    /// <summary>Total execution time.</summary>
    TimeSpan TotalExecutionTime { get; }

    /// <summary>Peak memory usage in bytes.</summary>
    long PeakMemoryUsage { get; }

    /// <summary>Number of stages.</summary>
    int StageCount { get; }
}

/// <summary>
/// Cache policy for pipeline caching.
/// </summary>
public enum CachePolicy
{
    /// <summary>Default cache policy.</summary>
    Default,

    /// <summary>No caching.</summary>
    None,

    /// <summary>Memory-based caching.</summary>
    Memory,

    /// <summary>Disk-based caching.</summary>
    Disk,

    /// <summary>Distributed caching.</summary>
    Distributed
}

/// <summary>
/// Execution priority for pipeline tasks.
/// </summary>
public enum ExecutionPriority
{
    /// <summary>Low priority.</summary>
    Low,

    /// <summary>Normal priority.</summary>
    Normal,

    /// <summary>High priority.</summary>
    High,

    /// <summary>Critical priority.</summary>
    Critical
}

/// <summary>
/// Interface for pipeline resource manager.
/// </summary>
public interface IPipelineResourceManager
{
    /// <summary>Gets available resources.</summary>
    Task<object> GetAvailableResourcesAsync();


    /// <summary>Releases resources.</summary>
    Task ReleaseResourcesAsync();
}

/// <summary>
/// Interface for pipeline cache manager.
/// </summary>
public interface IPipelineCacheManager
{
    /// <summary>Clears the cache.</summary>
    Task ClearCacheAsync();


    /// <summary>Gets cache statistics.</summary>
    Task<object> GetCacheStatsAsync();
}

/// <summary>
/// Interface for telemetry collector.
/// </summary>
public interface ITelemetryCollector
{
    /// <summary>Collects telemetry data.</summary>
    Task CollectAsync(string eventName, object data);
}

/// <summary>
/// Interface for pipeline execution graph.
/// </summary>
public interface IPipelineExecutionGraph
{
    /// <summary>Gets the graph nodes.</summary>
    IEnumerable<object> Nodes { get; }


    /// <summary>Gets the graph edges.</summary>
    IEnumerable<object> Edges { get; }
}

// IUnifiedMemoryManager is defined in DotCompute.Abstractions

#endregion

// Options classes moved to GpuOptions.cs to avoid duplication


/// <summary>
/// Extension methods to bridge LINQ IKernelPipelineBuilder to Core IKernelPipelineBuilder.
/// Provides backward compatibility while unifying on the Core interface.
/// </summary>
public static class KernelPipelineBuilderExtensions
{
    /// <summary>
    /// Creates a new empty pipeline for manual construction.
    /// Bridges LINQ interface to Core interface.
    /// </summary>
    /// <param name="builder">The pipeline builder</param>
    /// <returns>A new empty Core pipeline</returns>
    public static object Create(this DotCompute.Core.Pipelines.IKernelPipelineBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Create empty Core pipeline

        return builder.WithName("EmptyPipeline").Build();
    }

    /// <summary>
    /// Creates a pipeline from a LINQ expression tree.
    /// Bridges LINQ interface to Core interface.
    /// </summary>
    /// <param name="builder">The pipeline builder</param>
    /// <param name="expression">The expression to convert to a pipeline</param>
    /// <returns>A Core pipeline based on the expression</returns>
    public static object FromExpression(this DotCompute.Core.Pipelines.IKernelPipelineBuilder builder, System.Linq.Expressions.Expression expression)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(expression);

        // Create Core pipeline from expression

        return builder
            .WithName($"ExpressionPipeline_{expression.GetHashCode()}")
            .WithMetadata("SourceExpression", expression.ToString())
            .Build();
    }

    /// <summary>
    /// Creates a pipeline from data array.
    /// Extension method for data-based pipeline construction.
    /// </summary>
    /// <typeparam name="T">Data element type</typeparam>
    /// <param name="builder">The pipeline builder</param>
    /// <param name="data">Source data array</param>
    /// <returns>A Core pipeline configured for the data</returns>
    public static object FromData<T>(this DotCompute.Core.Pipelines.IKernelPipelineBuilder builder, T[] data)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(data);

        // Create Core pipeline with data input

        return builder
            .WithName($"DataPipeline_{typeof(T).Name}")
            .WithMetadata("InputDataSize", data.Length)
            .WithMetadata("InputDataType", typeof(T).Name)
            .Build();
    }

    /// <summary>
    /// Creates a streaming pipeline from async enumerable.
    /// Extension method for streaming data pipeline construction.
    /// </summary>
    /// <typeparam name="T">Stream element type</typeparam>
    /// <param name="builder">The pipeline builder</param>
    /// <param name="source">Source async enumerable</param>
    /// <param name="options">Stream pipeline options</param>
    /// <returns>A Core pipeline configured for streaming</returns>
    public static object FromStream<T>(
        this DotCompute.Core.Pipelines.IKernelPipelineBuilder builder,

        IAsyncEnumerable<T> source,

        StreamPipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        // Create Core streaming pipeline

        return builder
            .WithName($"StreamPipeline_{typeof(T).Name}")
            .WithMetadata("BatchSize", options.BatchSize)
            .WithMetadata("WindowSize", options.WindowSize.TotalMilliseconds)
            .WithMetadata("EnableBackpressure", options.EnableBackpressure)
            .WithOptimization(settings =>
            {
                settings.EnableStreaming = true;
                settings.EnableMemoryOptimization = true;
                settings.Level = DotCompute.Core.Pipelines.PipelineOptimizationLevel.Balanced;
            })
            .Build();
    }
}

/// <summary>
/// Extension methods for IAsyncEnumerable to provide missing LINQ operations.
/// </summary>
public static class AsyncEnumerableExtensions
{
    /// <summary>
    /// Projects each element of an async sequence into a new form.
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <typeparam name="TResult">Result element type</typeparam>
    /// <param name="source">Source async enumerable</param>
    /// <param name="selector">Transform function</param>
    /// <returns>Transformed async enumerable</returns>
    public static async IAsyncEnumerable<TResult> Select<TSource, TResult>(
        this IAsyncEnumerable<TSource> source,
        Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        await foreach (var item in source)
        {
            yield return selector(item);
        }
    }

    /// <summary>
    /// Returns a specified number of contiguous elements from the start of an async sequence.
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <param name="source">Source async enumerable</param>
    /// <param name="count">Number of elements to take</param>
    /// <returns>Async enumerable with taken elements</returns>
    public static async IAsyncEnumerable<TSource> Take<TSource>(
        this IAsyncEnumerable<TSource> source,
        int count)
    {
        ArgumentNullException.ThrowIfNull(source);


        if (count <= 0)
        {
            yield break;
        }

        var taken = 0;
        await foreach (var item in source)
        {
            if (taken >= count)
            {
                yield break;
            }

            yield return item;
            taken++;
        }
    }

    /// <summary>
    /// Buffers elements from an async sequence into batches.
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <param name="source">Source async enumerable</param>
    /// <param name="batchSize">Size of each batch</param>
    /// <returns>Async enumerable of batched elements</returns>
    public static async IAsyncEnumerable<TSource[]> Buffer<TSource>(
        this IAsyncEnumerable<TSource> source,
        int batchSize)
    {
        ArgumentNullException.ThrowIfNull(source);


        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive");
        }

        var buffer = new List<TSource>(batchSize);


        await foreach (var item in source)
        {
            buffer.Add(item);


            if (buffer.Count >= batchSize)
            {
                yield return buffer.ToArray();
                buffer.Clear();
            }
        }

        // Yield remaining items if any

        if (buffer.Count > 0)
        {
            yield return buffer.ToArray();
        }
    }
}

#region Missing Types for ComprehensivePipelineDemo

/// <summary>
/// Represents an aggregate function for pipeline operations.
/// </summary>
/// <typeparam name="T">The type being aggregated</typeparam>
public class AggregateFunction<T>
{
    /// <summary>Gets or sets the aggregation function.</summary>
    public Func<IEnumerable<T>, T>? Function { get; set; }


    /// <summary>Gets or sets the function name for kernel generation.</summary>
    public string Name { get; set; } = string.Empty;


    /// <summary>Gets or sets whether the function is associative.</summary>
    public bool IsAssociative { get; set; }


    /// <summary>Gets or sets whether the function is commutative.</summary>
    public bool IsCommutative { get; set; }


    /// <summary>Gets or sets the initial/seed value for reduction.</summary>
    public T? InitialValue { get; set; }


    /// <summary>Creates a sum aggregate function.</summary>
    public static AggregateFunction<T> Sum() => new()
    {
        Name = "Sum",
        IsAssociative = true,
        IsCommutative = true
    };


    /// <summary>Creates an average aggregate function.</summary>
    public static AggregateFunction<T> Average() => new()
    {
        Name = "Average",
        IsAssociative = false,
        IsCommutative = true
    };


    /// <summary>Creates a min aggregate function.</summary>
    public static AggregateFunction<T> Min() => new()
    {
        Name = "Min",
        IsAssociative = true,
        IsCommutative = true
    };


    /// <summary>Creates a max aggregate function.</summary>
    public static AggregateFunction<T> Max() => new()
    {
        Name = "Max",
        IsAssociative = true,
        IsCommutative = true
    };
}

// StreamPipelineOptions is defined in DotCompute.Linq.Pipelines.Models.PipelineExecutionPlan


#endregion