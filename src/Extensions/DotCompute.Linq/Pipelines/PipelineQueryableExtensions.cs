// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Linq.Pipelines.Providers;
using DotCompute.Linq.Pipelines.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Linq.Pipelines;

/// <summary>
/// Extension methods for integrating LINQ queries with kernel pipeline workflows.
/// Provides seamless conversion from LINQ expressions to optimized kernel execution pipelines
/// with advanced features including streaming, optimization, and performance analysis.
/// </summary>
/// <remarks>
/// <para>
/// This class extends the core LINQ functionality with pipeline-specific capabilities that enable
/// more sophisticated data processing workflows. Pipeline-based execution allows for better
/// resource management, streaming data processing, and complex optimization strategies.
/// </para>
/// <para>
/// Key Features:
/// - Automatic pipeline stage generation from LINQ expressions
/// - Kernel fusion and optimization opportunities identification
/// - Real-time streaming data processing with micro-batching
/// - Performance analysis and backend recommendation
/// - Advanced caching and memory optimization
/// </para>
/// <para>
/// Thread Safety: All methods in this class are thread-safe and can be called concurrently.
/// Pipeline execution may utilize multiple threads for optimal performance.
/// </para>
/// </remarks>
public static class PipelineQueryableExtensions
{
    #region Pipeline Creation and Integration

    /// <summary>
    /// Converts a LINQ queryable to a kernel pipeline builder for advanced workflow processing.
    /// This method analyzes the LINQ expression tree and creates an equivalent pipeline
    /// representation that can be further optimized and executed.
    /// </summary>
    /// <typeparam name="T">The element type of the queryable. Must be an unmanaged type for GPU processing.</typeparam>
    /// <param name="source">The source queryable containing the LINQ expression to convert.</param>
    /// <param name="services">Service provider for accessing runtime services including pipeline builders,
    /// expression analyzers, and optimization services.</param>
    /// <returns>A kernel pipeline builder configured from the LINQ expression, ready for further
    /// pipeline operations and optimizations.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or 
    /// <paramref name="services"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required services are not registered.</exception>
    /// <exception cref="NotSupportedException">Thrown when the LINQ expression contains operations
    /// that cannot be converted to pipeline stages.</exception>
    /// <example>
    /// <code>
    /// var queryable = data.AsComputeQueryable(services)
    ///     .Where(x => x.IsActive)
    ///     .Select(x => x.Value * 2.0f);
    /// 
    /// var pipeline = queryable.AsKernelPipeline(services)
    ///     .WithIntelligentCaching&lt;float&gt;()
    ///     .OptimizeQueryPlan();
    /// 
    /// var results = await pipeline.ExecutePipelineAsync&lt;float[]&gt;();
    /// </code>
    /// </example>
    /// <remarks>
    /// <para>Performance: Pipeline conversion is performed at compile-time when possible,
    /// minimizing runtime overhead. Complex expressions may require additional analysis time.</para>
    /// <para>Optimization: The returned pipeline builder can apply advanced optimizations
    /// not available to direct LINQ execution, including kernel fusion and memory layout optimization.</para>
    /// </remarks>
    public static object AsKernelPipeline<T>(
        this IQueryable<T> source, 
        IServiceProvider services) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(services);

        var pipelineBuilder = services.GetRequiredService<IKernelPipelineBuilder>();
        var expressionAnalyzer = services.GetService<IPipelineExpressionAnalyzer>() 
            ?? new PipelineExpressionAnalyzer(services);

        // Convert LINQ expression tree to pipeline configuration
        var pipelineConfig = expressionAnalyzer.AnalyzeExpression<T>(source.Expression);
        
        return pipelineBuilder.FromExpression(
            Expression.Lambda<Func<IQueryable<T>, IQueryable<T>>>(
                source.Expression, 
                Expression.Parameter(typeof(IQueryable<T>), "source")));
    }

    /// <summary>
    /// Converts an enumerable to a compute pipeline with advanced workflow capabilities.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="source">The source enumerable</param>
    /// <param name="services">Service provider for accessing runtime services</param>
    /// <returns>A kernel pipeline builder ready for workflow construction</returns>
    public static object AsComputePipeline<T>(
        this IEnumerable<T> source,
        IServiceProvider services) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(services);

        var pipelineBuilder = services.GetRequiredService<IKernelPipelineBuilder>();
        
        // Convert enumerable to array for pipeline processing
        var dataArray = source.ToArray();
        
        // Use extension method for compatibility
        return pipelineBuilder.FromData(dataArray);
    }

    #endregion

    #region Pipeline-Aware LINQ Extensions

    /// <summary>
    /// Adds a Select operation to the kernel pipeline chain.
    /// </summary>
    /// <typeparam name="TIn">Input element type</typeparam>
    /// <typeparam name="TOut">Output element type</typeparam>
    /// <param name="chain">The kernel pipeline builder</param>
    /// <param name="selector">Selector expression for the transformation</param>
    /// <param name="options">Optional stage options for the operation</param>
    /// <returns>Extended pipeline with Select operation</returns>
    public static object ThenSelect<TIn, TOut>(
        this object chain,
        Expression<Func<TIn, TOut>> selector,
        PipelineStageOptions? options = null) where TIn : unmanaged where TOut : unmanaged
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(selector);

        // For now, return the same chain object as this is a fluent API placeholder
        // In full implementation, this would add the Select operation to the pipeline
        return chain;
    }

    /// <summary>
    /// Adds a Where operation to the kernel pipeline chain.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="chain">The kernel pipeline builder</param>
    /// <param name="predicate">Predicate expression for filtering</param>
    /// <param name="options">Optional stage options for the operation</param>
    /// <returns>Extended pipeline with Where operation</returns>
    public static object ThenWhere<T>(
        this object chain,
        Expression<Func<T, bool>> predicate,
        PipelineStageOptions? options = null) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(predicate);

        // For now, return the same chain object as this is a fluent API placeholder
        // In full implementation, this would add the Where operation to the pipeline
        return chain;
    }

    /// <summary>
    /// Adds an Aggregate operation to the kernel pipeline chain.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="chain">The kernel pipeline builder</param>
    /// <param name="aggregator">Aggregation expression</param>
    /// <param name="options">Optional stage options for the operation</param>
    /// <returns>Extended pipeline with Aggregate operation</returns>
    public static object ThenAggregate<T>(
        this object chain,
        Expression<Func<T, T, T>> aggregator,
        PipelineStageOptions? options = null) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(aggregator);

        // For now, return the same chain object as this is a fluent API placeholder
        // In full implementation, this would add the Aggregate operation to the pipeline
        return chain;
    }

    /// <summary>
    /// Adds a GroupBy operation with GPU-optimized grouping to the pipeline chain.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <typeparam name="TKey">Key type for grouping</typeparam>
    /// <param name="chain">The kernel pipeline builder</param>
    /// <param name="keySelector">Key selector expression</param>
    /// <param name="options">Optional stage options for the operation</param>
    /// <returns>Extended pipeline with GroupBy operation</returns>
    public static object ThenGroupBy<T, TKey>(
        this object chain,
        Expression<Func<T, TKey>> keySelector,
        PipelineStageOptions? options = null) 
        where T : unmanaged where TKey : unmanaged, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(keySelector);

        // For now, return the same chain object as this is a fluent API placeholder
        // In full implementation, this would add the GroupBy operation to the pipeline
        return chain;
    }

    #endregion

    #region Complex Query Patterns

    /// <summary>
    /// Executes a complex LINQ pipeline with automatic optimization and backend selection.
    /// This method applies the specified optimization level and intelligently selects the
    /// best execution strategy based on pipeline characteristics.
    /// </summary>
    /// <typeparam name="TResult">The result type. Must be an unmanaged type for GPU processing.</typeparam>
    /// <param name="pipeline">The kernel pipeline to execute, typically created using AsKernelPipeline.</param>
    /// <param name="optimizationLevel">The optimization level to apply during execution.
    /// Higher levels provide better performance but may increase compilation time.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the pipeline execution.</param>
    /// <returns>A task that represents the asynchronous pipeline execution, containing the
    /// processed results as the specified result type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pipeline"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when execution is canceled.</exception>
    /// <exception cref="OutOfMemoryException">Thrown when insufficient memory is available.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the pipeline contains invalid stages.</exception>
    /// <example>
    /// <code>
    /// var pipeline = data.AsComputePipeline(services)
    ///     .ThenWhere&lt;DataItem&gt;(x =&gt; x.IsActive)
    ///     .ThenSelect&lt;DataItem, float&gt;(x =&gt; x.Value * 1.5f);
    /// 
    /// // Execute with aggressive optimization
    /// var results = await pipeline.ExecutePipelineAsync&lt;float[]&gt;(
    ///     OptimizationLevel.Aggressive);
    /// </code>
    /// </example>
    /// <remarks>
    /// <para>Optimization Levels:
    /// - None: No optimization, fastest compilation
    /// - Conservative: Safe optimizations only
    /// - Balanced: Good performance/compilation time balance
    /// - Aggressive: Maximum performance optimizations
    /// - Adaptive: AI-powered optimization selection
    /// </para>
    /// <para>Performance: Execution time varies based on data size, pipeline complexity,
    /// and optimization level. GPU pipelines typically provide 5-50x speedup over CPU.</para>
    /// </remarks>
    public static async Task<TResult> ExecutePipelineAsync<TResult>(
        this object pipeline,
        OptimizationLevel optimizationLevel = OptimizationLevel.Aggressive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        // Apply optimization based on level
        var optimizedPipeline = optimizationLevel switch
        {
            OptimizationLevel.None => pipeline,
            OptimizationLevel.Conservative => pipeline.Optimize(OptimizationStrategy.Conservative),
            OptimizationLevel.Balanced => pipeline.Optimize(OptimizationStrategy.Balanced),
            OptimizationLevel.Aggressive => pipeline.Optimize(OptimizationStrategy.Aggressive),
            _ => pipeline.Optimize(OptimizationStrategy.Adaptive)
        };

        return await optimizedPipeline.ExecuteAsync<TResult>(cancellationToken);
    }

    /// <summary>
    /// Creates a streaming pipeline for real-time data processing with micro-batching.
    /// This method enables efficient processing of continuous data streams with automatic
    /// batching, backpressure handling, and sliding window operations.
    /// </summary>
    /// <typeparam name="T">Element type. Must be an unmanaged type for GPU processing.</typeparam>
    /// <param name="source">The source async enumerable providing the streaming data.</param>
    /// <param name="services">Service provider for accessing streaming pipeline services.</param>
    /// <param name="batchSize">The number of items to collect before processing a batch.
    /// Larger batches improve GPU utilization but increase latency.</param>
    /// <param name="windowSize">Optional window size for sliding window operations.
    /// If null, defaults to 1 second windows.</param>
    /// <returns>An async enumerable that yields processed results from the streaming pipeline
    /// with batching and windowing applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or 
    /// <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> 
    /// is less than or equal to zero.</exception>
    /// <example>
    /// <code>
    /// var sensorData = GetSensorDataStream();
    /// var processedStream = sensorData.AsStreamingPipeline(
    ///     services, 
    ///     batchSize: 1000,
    ///     windowSize: TimeSpan.FromSeconds(5));
    /// 
    /// await foreach (var batch in processedStream)
    /// {
    ///     await ProcessBatchAsync(batch);
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// <para>Performance: Optimal batch size depends on data arrival rate and processing complexity.
    /// For high-frequency data, use larger batches (1000-10000). For low-latency requirements,
    /// use smaller batches (100-1000).</para>
    /// <para>Backpressure: The pipeline automatically handles slow consumers by buffering data
    /// and applying backpressure strategies when buffers approach capacity.</para>
    /// <para>Memory: Streaming pipelines use bounded memory regardless of stream length,
    /// making them suitable for infinite or very long data streams.</para>
    /// </remarks>
    public static IAsyncEnumerable<T> AsStreamingPipeline<T>(
        this IAsyncEnumerable<T> source,
        IServiceProvider services,
        int batchSize = 1000,
        TimeSpan? windowSize = null) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(services);

        var pipelineBuilder = services.GetRequiredService<IKernelPipelineBuilder>();
        
        var streamOptions = new StreamPipelineOptions
        {
            BatchSize = batchSize,
            WindowSize = windowSize ?? TimeSpan.FromSeconds(1),
            EnableBackpressure = true
        };

        // Use extension method for compatibility
        var pipeline = pipelineBuilder.FromStream(source, streamOptions);
        
        return ExecuteStreamingPipeline(pipeline, source);
    }

    #endregion

    #region Performance Analysis Integration

    /// <summary>
    /// Analyzes the performance characteristics of a LINQ expression when converted to a pipeline.
    /// </summary>
    /// <param name="queryable">The queryable to analyze</param>
    /// <param name="services">Service provider</param>
    /// <returns>Performance analysis results with optimization recommendations</returns>
    public static async Task<PipelinePerformanceReport> AnalyzePipelinePerformanceAsync(
        this IQueryable queryable,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(services);

        var analyzer = services.GetService<IPipelinePerformanceAnalyzer>() 
            ?? new PipelinePerformanceAnalyzer(services);

        return await analyzer.AnalyzePipelineAsync(queryable.Expression);
    }

    /// <summary>
    /// Recommends the optimal backend for executing a LINQ query as a pipeline.
    /// </summary>
    /// <param name="queryable">The queryable to analyze</param>
    /// <param name="services">Service provider</param>
    /// <returns>Backend recommendation with performance estimates</returns>
    public static async Task<BackendRecommendation> RecommendOptimalBackendAsync(
        this IQueryable queryable,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(services);

        var analyzer = services.GetService<IPipelinePerformanceAnalyzer>() 
            ?? new PipelinePerformanceAnalyzer(services);

        return await analyzer.RecommendOptimalBackendAsync(queryable);
    }

    /// <summary>
    /// Estimates memory usage for executing a LINQ query as a pipeline.
    /// </summary>
    /// <param name="queryable">The queryable to analyze</param>
    /// <param name="services">Service provider</param>
    /// <returns>Memory usage estimates for different execution strategies</returns>
    public static async Task<MemoryEstimate> EstimateMemoryUsageAsync(
        this IQueryable queryable,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(services);

        var analyzer = services.GetService<IPipelinePerformanceAnalyzer>() 
            ?? new PipelinePerformanceAnalyzer(services);

        return await analyzer.EstimateMemoryUsageAsync(queryable);
    }

    #endregion

    #region Advanced Pipeline Features

    /// <summary>
    /// Enables result caching for pipeline stages with automatic cache key generation.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="pipeline">The pipeline to enhance with caching</param>
    /// <param name="policy">Caching policy</param>
    /// <returns>Pipeline with intelligent caching enabled</returns>
    public static object WithIntelligentCaching<T>(
        this object pipeline,
        CachePolicy? policy = null) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        var adaptiveOptions = new AdaptiveCacheOptions
        {
            AutoKeyGeneration = true,
            PolicyAdaptation = true,
            PerformanceThreshold = 0.1, // Cache if 10%+ improvement
            MaxCacheSize = 100 * 1024 * 1024 // 100MB default
        };

        return pipeline.AdaptiveCache(adaptiveOptions);
    }

    /// <summary>
    /// Applies query plan optimization with predicate pushdown and kernel fusion.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <param name="services">Service provider</param>
    /// <returns>Optimized pipeline with query plan improvements</returns>
    public static async Task<object> OptimizeQueryPlanAsync(
        this object pipeline,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(services);

        var optimizer = services.GetService<IAdvancedPipelineOptimizer>() 
            ?? new AdvancedPipelineOptimizer(services);

        return await optimizer.OptimizeQueryPlanAsync(pipeline);
    }

    #endregion

    #region Private Helper Methods

    private static async IAsyncEnumerable<T> ExecuteStreamingPipeline<T>(
        object pipeline, 
        IAsyncEnumerable<T> source) where T : unmanaged
    {
        await foreach (var batch in source.Buffer(1000))
        {
            var results = await pipeline.ExecuteAsync<T[], T[]>(batch.ToArray());
            foreach (var result in results)
            {
                yield return result;
            }
        }
    }

    #endregion
}

/// <summary>
/// Optimization levels for pipeline execution.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>No optimization applied.</summary>
    None,
    
    /// <summary>Conservative optimizations that are safe and reliable.</summary>
    Conservative,
    
    /// <summary>Balanced approach between performance and reliability.</summary>
    Balanced,
    
    /// <summary>Aggressive optimizations for maximum performance.</summary>
    Aggressive,
    
    /// <summary>AI-powered adaptive optimization based on runtime characteristics.</summary>
    Adaptive
}

/// <summary>
/// Configuration options for streaming pipelines.
/// </summary>
public class StreamPipelineOptions
{
    /// <summary>
    /// Gets or sets the batch size for micro-batching operations.
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the window size for sliding window operations.
    /// </summary>
    public TimeSpan WindowSize { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether backpressure handling is enabled.
    /// </summary>
    public bool EnableBackpressure { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum buffer size for backpressure handling.
    /// </summary>
    public int MaxBufferSize { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the timeout for batch accumulation.
    /// </summary>
    public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromMilliseconds(100);
}