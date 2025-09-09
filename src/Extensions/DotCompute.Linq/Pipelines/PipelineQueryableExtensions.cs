// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Linq.Pipelines.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Linq.Pipelines;

/// <summary>
/// Extension methods for integrating LINQ queries with kernel pipeline workflows.
/// Provides seamless conversion from LINQ expressions to optimized kernel execution pipelines.
/// </summary>
public static class PipelineQueryableExtensions
{
    #region Pipeline Creation and Integration

    /// <summary>
    /// Converts a LINQ queryable to a kernel pipeline builder for advanced workflow processing.
    /// </summary>
    /// <typeparam name="T">The element type of the queryable</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="services">Service provider for accessing runtime services</param>
    /// <returns>A kernel pipeline builder configured from the LINQ expression</returns>
    public static IKernelPipelineBuilder AsKernelPipeline<T>(
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
    public static IKernelPipelineBuilder AsComputePipeline<T>(
        this IEnumerable<T> source,
        IServiceProvider services) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(services);

        var pipelineBuilder = services.GetRequiredService<IKernelPipelineBuilder>();
        
        // Convert enumerable to array for pipeline processing
        var dataArray = source.ToArray();
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
    public static IKernelPipeline ThenSelect<TIn, TOut>(
        this IKernelPipelineBuilder chain,
        Expression<Func<TIn, TOut>> selector,
        PipelineStageOptions? options = null) where TIn : unmanaged where TOut : unmanaged
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(selector);

        // Create a kernel stage for the select operation
        var pipeline = chain.Create();
        return pipeline.Then<TIn, TOut>(
            "SelectKernel",
            input => new object[] { input, selector.Compile() },
            options);
    }

    /// <summary>
    /// Adds a Where operation to the kernel pipeline chain.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="chain">The kernel pipeline builder</param>
    /// <param name="predicate">Predicate expression for filtering</param>
    /// <param name="options">Optional stage options for the operation</param>
    /// <returns>Extended pipeline with Where operation</returns>
    public static IKernelPipeline ThenWhere<T>(
        this IKernelPipelineBuilder chain,
        Expression<Func<T, bool>> predicate,
        PipelineStageOptions? options = null) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(predicate);

        var pipeline = chain.Create();
        return pipeline.Then<T, T>(
            "WhereKernel",
            input => new object[] { input, predicate.Compile() },
            options);
    }

    /// <summary>
    /// Adds an Aggregate operation to the kernel pipeline chain.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="chain">The kernel pipeline builder</param>
    /// <param name="aggregator">Aggregation expression</param>
    /// <param name="options">Optional stage options for the operation</param>
    /// <returns>Extended pipeline with Aggregate operation</returns>
    public static IKernelPipeline ThenAggregate<T>(
        this IKernelPipelineBuilder chain,
        Expression<Func<T, T, T>> aggregator,
        PipelineStageOptions? options = null) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(aggregator);

        var pipeline = chain.Create();
        return pipeline.Then<IEnumerable<T>, T>(
            "AggregateKernel",
            input => new object[] { input, aggregator.Compile() },
            options);
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
    public static IKernelPipeline ThenGroupBy<T, TKey>(
        this IKernelPipelineBuilder chain,
        Expression<Func<T, TKey>> keySelector,
        PipelineStageOptions? options = null) 
        where T : unmanaged where TKey : unmanaged, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(keySelector);

        var pipeline = chain.Create();
        return pipeline.Then<IEnumerable<T>, IEnumerable<IGrouping<TKey, T>>>(
            "GroupByKernel",
            input => new object[] { input, keySelector.Compile() },
            options);
    }

    #endregion

    #region Complex Query Patterns

    /// <summary>
    /// Executes a complex LINQ pipeline with automatic optimization and backend selection.
    /// </summary>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="pipeline">The kernel pipeline to execute</param>
    /// <param name="optimizationLevel">Optimization level to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The pipeline execution results</returns>
    public static async Task<TResult> ExecutePipelineAsync<TResult>(
        this IKernelPipeline pipeline,
        OptimizationLevel optimizationLevel = OptimizationLevel.Aggressive,
        CancellationToken cancellationToken = default) where TResult : unmanaged
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
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="source">The source async enumerable</param>
    /// <param name="services">Service provider</param>
    /// <param name="batchSize">Batch size for micro-batching</param>
    /// <param name="windowSize">Window size for sliding window operations</param>
    /// <returns>A streaming pipeline with batching and windowing</returns>
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
    public static IKernelPipeline WithIntelligentCaching<T>(
        this IKernelPipeline pipeline,
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
    public static async Task<IKernelPipeline> OptimizeQueryPlanAsync(
        this IKernelPipeline pipeline,
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
        IKernelPipeline pipeline, 
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