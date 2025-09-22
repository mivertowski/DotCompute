// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Optimization.Enums;
namespace DotCompute.Linq.Pipelines.Models;
{
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
/// Interface for compute device (bridges to Core interface).
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
/// Interface for memory buffer (bridges to Core interface).
public interface IMemoryBuffer : IDisposable
    {
    /// <summary>Buffer size in bytes.</summary>
    long Size { get; }
    /// <summary>Copy buffer to another buffer.</summary>
    Task CopyToAsync(IMemoryBuffer destination);
#endregion
/// Temporary interface definition for IAdvancedPipelineOptimizer.
/// This will be moved to proper location once infrastructure is ready.
public interface IAdvancedPipelineOptimizer
    {
    /// <summary>
    /// Optimizes a pipeline using advanced query plan optimization.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <returns>Optimized pipeline</returns>
    Task<object> OptimizeQueryPlanAsync(object pipeline);
    /// Applies kernel fusion optimizations to the pipeline.
    /// <returns>Pipeline with fused kernels</returns>
    Task<object> FuseKernelsAsync(object pipeline);
    /// Optimizes memory access patterns in the pipeline.
    /// <returns>Memory-optimized pipeline</returns>
    Task<object> OptimizeMemoryAccessAsync(object pipeline);
/// Represents pipeline diagnostic information.
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
/// Stage performance metrics interface.
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
/// Memory usage statistics interface.
public interface IMemoryUsageStatistics
    {
    /// <summary>Gets the average memory usage in bytes.</summary>
    long AverageMemoryUsage { get; }
    /// <summary>Gets the number of allocations.</summary>
    int AllocationCount { get; }
    /// <summary>Gets the total allocated memory in bytes.</summary>
    long TotalAllocatedMemory { get; }
    /// <summary>Gets memory usage by stage.</summary>
    IReadOnlyDictionary<string, long> MemoryByStage { get; }
/// Pipeline validation result.
public class PipelineValidationResult
    {
    /// <summary>Gets whether the pipeline is valid.</summary>
    public bool IsValid { get; set; }
    /// <summary>Gets validation errors if any.</summary>
    public List<string> Errors { get; set; } = [];
    /// <summary>Gets validation warnings if any.</summary>
    public List<string> Warnings { get; set; } = [];
/// Pipeline error types for comprehensive error handling.
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
/// Pipeline error recovery result.
public class PipelineErrorRecoveryResult<T>
    {
    /// Gets whether the recovery was successful.
    public bool IsSuccess { get; set; }
    /// Gets the recovered value if successful.
    public T? Value { get; set; }
    /// Gets whether a value was recovered.
    public bool HasValue => IsSuccess && Value != null;
    /// Gets the error that caused the recovery attempt.
    public Exception? OriginalError { get; set; }
    /// Gets recovery actions that were attempted.
    public List<string> RecoveryActions { get; set; } = [];
    /// Gets any warnings from the recovery process.
/// Simple implementation of pipeline diagnostics for testing.
public class SimplePipelineDiagnostics : IPipelineDiagnostics
    {
    public int StageCount { get; set; }
    public double TotalExecutionTimeMs { get; set; }
    public double CacheHitRate { get; set; }
    public IReadOnlyList<IStagePerformanceMetrics> StageMetrics { get; set; } = new List<IStagePerformanceMetrics>();
    public IMemoryUsageStatistics MemoryUsage { get; set; } = new SimpleMemoryUsageStatistics();
    public IReadOnlyList<BottleneckInfo> Bottlenecks { get; set; } = new List<BottleneckInfo>();
    public long PeakMemoryUsage { get; set; }
/// Simple implementation of stage performance metrics.
public class SimpleStagePerformanceMetrics : IStagePerformanceMetrics
    {
    public string StageName { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
    public long MemoryUsage { get; set; }
    public string Backend { get; set; } = "CPU";
    public IReadOnlyDictionary<string, object> PerformanceData { get; set; } = new Dictionary<string, object>();
/// Simple implementation of memory usage statistics.
public class SimpleMemoryUsageStatistics : IMemoryUsageStatistics
    {
    public long AverageMemoryUsage { get; set; }
    public int AllocationCount { get; set; }
    public long TotalAllocatedMemory { get; set; }
    public IReadOnlyDictionary<string, long> MemoryByStage { get; set; } = new Dictionary<string, long>();
/// Resource utilization metrics interface.
public interface IResourceUtilizationMetrics
    {
    /// <summary>Gets CPU utilization as a percentage.</summary>
    double CpuUtilization { get; }
    /// <summary>Gets memory utilization as a percentage.</summary>
    double MemoryUtilization { get; }
    /// <summary>Gets GPU utilization as a percentage.</summary>
    double GpuUtilization { get; }
    /// <summary>Gets peak memory usage in bytes.</summary>
    /// <summary>Gets average memory usage in bytes.</summary>
/// Cache utilization metrics interface.
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
/// Performance insights interface.
public interface IPerformanceInsights
    {
    /// <summary>Gets optimization recommendations.</summary>
    IReadOnlyList<string> Recommendations { get; }
    /// <summary>Gets performance improvement opportunities.</summary>
    IReadOnlyList<string> ImprovementOpportunities { get; }
    /// <summary>Gets performance rating (0.0 to 1.0).</summary>
    double PerformanceRating { get; }
/// Stage execution metrics interface.
public interface IStageExecutionMetrics
    {
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
#region Missing Pipeline Interfaces
/// Interface for kernel execution context.
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
/// Interface for pipeline configuration.
public interface IPipelineConfiguration
    {
    /// <summary>Whether to enable caching.</summary>
    bool EnableCaching { get; set; }
    /// <summary>Timeout in seconds.</summary>
    int TimeoutSeconds { get; set; }
/// Interface for pipeline execution context.
public interface IPipelineExecutionContext
    {
    /// <summary>Whether to enable detailed metrics.</summary>
    bool EnableDetailedMetrics { get; set; }
    /// <summary>Whether to track memory usage.</summary>
    bool TrackMemoryUsage { get; set; }
    /// <summary>Whether to collect timing data.</summary>
    bool CollectTimingData { get; set; }
/// Interface for pipeline execution result.
public interface IPipelineExecutionResult<T>
    {
    /// <summary>The execution result.</summary>
    T Result { get; }
    /// <summary>Pipeline metrics.</summary>
    IPipelineMetrics Metrics { get; }
    /// <summary>Execution success status.</summary>
    /// <summary>Any exception that occurred.</summary>
/// Interface for adaptive backend selector.
public interface IAdaptiveBackendSelector
    {
    /// Selects the optimal backend for given workload characteristics.
    /// <param name="characteristics">Workload characteristics</param>
    /// <returns>Selected backend name</returns>
    Task<string> SelectBackendAsync(WorkloadCharacteristics characteristics);
/// Pipeline stage options for execution configuration.
public class PipelineStageOptions
    {
    /// <summary>Preferred backend for this stage.</summary>
    public string? PreferredBackend { get; set; }
    public bool EnableProfiling { get; set; }
    public bool EnableCaching { get; set; }
    
    /// <summary>Whether to enable optimization.</summary>
    public bool EnableOptimization { get; set; }
    /// <summary>Whether to enable memory optimization.</summary>
    public bool EnableMemoryOptimization { get; set; }
    public int TimeoutMs { get; set; } = 30000;
/// Workload characteristics for backend selection.
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
    public List<string> OptimizationHints { get; set; } = [];
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
/// Backend recommendation result.
public class BackendRecommendation
    {
    /// <summary>Recommended backend name.</summary>
    public string RecommendedBackend { get; set; } = "CPU";
    /// <summary>Confidence in recommendation (0.0 to 1.0).</summary>
    public double Confidence { get; set; }
    /// <summary>Reasoning for the backend recommendation.</summary>
    public string Reasoning { get; set; } = string.Empty;
    /// <summary>Backend performance estimates.</summary>
    public Dictionary<string, BackendEstimate> BackendEstimates { get; set; } = [];
/// Backend performance estimate.
public class BackendEstimate
    {
    /// <summary>Estimated execution time.</summary>
    public TimeSpan EstimatedExecutionTime { get; set; }
    /// <summary>Estimated memory usage.</summary>
    public long EstimatedMemory { get; set; }
    /// <summary>Performance score (higher is better).</summary>
    public double PerformanceScore { get; set; }
// OptimizationLevel is now imported from DotCompute.Abstractions.Types
/// Pipeline metrics interface.
public interface IPipelineMetrics
    {
    /// <summary>Total execution time.</summary>
    TimeSpan TotalExecutionTime { get; }
    /// <summary>Peak memory usage in bytes.</summary>
    /// <summary>Number of stages.</summary>
/// Cache policy for pipeline caching.
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
/// Execution priority for pipeline tasks.
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
/// Interface for pipeline resource manager.
public interface IPipelineResourceManager
    {
    /// <summary>Gets available resources.</summary>
    Task<object> GetAvailableResourcesAsync();
    /// <summary>Releases resources.</summary>
    Task ReleaseResourcesAsync();
/// Interface for pipeline cache manager.
public interface IPipelineCacheManager
    {
    /// <summary>Clears the cache.</summary>
    Task ClearCacheAsync();
    /// <summary>Gets cache statistics.</summary>
    Task<object> GetCacheStatsAsync();
/// Interface for telemetry collector.
public interface ITelemetryCollector
    {
    /// <summary>Collects telemetry data.</summary>
    Task CollectAsync(string eventName, object data);
/// Interface for pipeline execution graph.
public interface IPipelineExecutionGraph
    {
    /// <summary>Gets the graph nodes.</summary>
    IEnumerable<object> Nodes { get; }
    /// <summary>Gets the graph edges.</summary>
    IEnumerable<object> Edges { get; }
// IUnifiedMemoryManager is defined in DotCompute.Abstractions
// Options classes moved to GpuOptions.cs to avoid duplication
/// Extension methods to bridge LINQ IKernelPipelineBuilder to Core IKernelPipelineBuilder.
/// Provides backward compatibility while unifying on the Core interface.
public static class KernelPipelineBuilderExtensions
    {
    /// Creates a new empty pipeline for manual construction.
    /// Bridges LINQ interface to Core interface.
    /// <param name="builder">The pipeline builder</param>
    /// <returns>A new empty Core pipeline</returns>
    public static object Create(this DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipelineBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // Create empty Core pipeline
        return builder.WithName("EmptyPipeline").Build();
    }
    /// Creates a pipeline from a LINQ expression tree.
    /// <param name="expression">The expression to convert to a pipeline</param>
    /// <returns>A Core pipeline based on the expression</returns>
    public static object FromExpression(this DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipelineBuilder builder, System.Linq.Expressions.Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        // Create Core pipeline from expression
        return builder
            .WithName($"ExpressionPipeline_{expression.GetHashCode()}")
            .WithMetadata("SourceExpression", expression.ToString())
            .Build();
    /// Creates a pipeline from data array.
    /// Extension method for data-based pipeline construction.
    /// <typeparam name="T">Data element type</typeparam>
    /// <param name="data">Source data array</param>
    /// <returns>A Core pipeline configured for the data</returns>
    public static object FromData<T>(this DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipelineBuilder builder, T[] data)
        ArgumentNullException.ThrowIfNull(data);
        // Create Core pipeline with data input
            .WithName($"DataPipeline_{typeof(T).Name}")
            .WithMetadata("InputDataSize", data.Length)
            .WithMetadata("InputDataType", typeof(T).Name)
    /// Creates a streaming pipeline from async enumerable.
    /// Extension method for streaming data pipeline construction.
    /// <typeparam name="T">Stream element type</typeparam>
    /// <param name="source">Source async enumerable</param>
    /// <param name="options">Stream pipeline options</param>
    /// <returns>A Core pipeline configured for streaming</returns>
    public static object FromStream<T>(
        this DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipelineBuilder builder,
        IAsyncEnumerable<T> source,
        StreamPipelineOptions options)
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        // Create Core streaming pipeline
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
/// Extension methods for IAsyncEnumerable to provide missing LINQ operations.
public static class AsyncEnumerableExtensions
    {
    /// Projects each element of an async sequence into a new form.
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <typeparam name="TResult">Result element type</typeparam>
    /// <param name="selector">Transform function</param>
    /// <returns>Transformed async enumerable</returns>
    public static async IAsyncEnumerable<TResult> Select<TSource, TResult>(
        this IAsyncEnumerable<TSource> source,
        Func<TSource, TResult> selector)
        ArgumentNullException.ThrowIfNull(selector);
        }
        await foreach (var item in source)
        {
            yield return selector(item);
        }
    /// Returns a specified number of contiguous elements from the start of an async sequence.
    /// <param name="count">Number of elements to take</param>
    /// <returns>Async enumerable with taken elements</returns>
    public static async IAsyncEnumerable<TSource> Take<TSource>(
        int count)
        if (count <= 0)
            yield break;
        var taken = 0;
            if (taken >= count)
                yield break;
            }
            yield return item;
            taken++;
    /// Buffers elements from an async sequence into batches.
    /// <param name="batchSize">Size of each batch</param>
    /// <returns>Async enumerable of batched elements</returns>
    public static async IAsyncEnumerable<TSource[]> Buffer<TSource>(
        int batchSize)
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive");
        var buffer = new List<TSource>(batchSize);
            buffer.Add(item);
            if (buffer.Count >= batchSize)
                yield return buffer.ToArray();
                buffer.Clear();
        // Yield remaining items if any
        if (buffer.Count > 0)
            yield return buffer.ToArray();
#region Missing Types for ComprehensivePipelineDemo
/// Represents an aggregate function for pipeline operations.
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
        Name = "Sum",
        IsAssociative = true,
        IsCommutative = true
    };
    /// <summary>Creates an average aggregate function.</summary>
    public static AggregateFunction<T> Average() => new()
        Name = "Average",
        IsAssociative = false,
    /// <summary>Creates a min aggregate function.</summary>
    public static AggregateFunction<T> Min() => new()
        Name = "Min",
    /// <summary>Creates a max aggregate function.</summary>
    public static AggregateFunction<T> Max() => new()
        Name = "Max",
// StreamPipelineOptions is defined in DotCompute.Linq.Pipelines.Models.PipelineExecutionPlan
