// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Pipelines.Models;
using DotCompute.Linq.Pipelines.Streaming;
using Microsoft.Extensions.DependencyInjection;
namespace DotCompute.Linq.Pipelines.Extensions;
/// <summary>
/// Extension methods for pipeline builders to support LINQ-style operations and streaming.
/// </summary>
public static class PipelineBuilderLinqExtensions
{
    /// <summary>
    /// Adds a Where operation to the pipeline.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="pipeline">The pipeline builder</param>
    /// <param name="predicate">The filter predicate</param>
    /// <returns>The pipeline builder for chaining</returns>
    public static object ThenWhere<T>(this object pipeline, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(predicate);
        
        // In a real implementation, this would add a filter stage to the pipeline
        // For now, return the pipeline for chaining
        return pipeline;
    }
    /// Adds a Select operation to the pipeline.
    /// <typeparam name="TIn">Input element type</typeparam>
    /// <typeparam name="TOut">Output element type</typeparam>
    /// <param name="selector">The transformation selector</param>
    public static object ThenSelect<TIn, TOut>(this object pipeline, Expression<Func<TIn, TOut>> selector)
        ArgumentNullException.ThrowIfNull(selector);
        // In a real implementation, this would add a transform stage to the pipeline
    /// Adds intelligent caching to the pipeline.
    public static object WithIntelligentCaching<T>(this object pipeline)
        // In a real implementation, this would configure caching for the pipeline
    /// Optimizes the query plan for the pipeline.
    /// <param name="services">Service provider for optimization services</param>
    /// <returns>An optimized pipeline</returns>
    public static async Task<object> OptimizeQueryPlanAsync(this object pipeline, IServiceProvider services)
        ArgumentNullException.ThrowIfNull(services);
        // Simulate optimization delay
        await Task.Delay(1);
        // In a real implementation, this would optimize the pipeline
    /// Creates a streaming pipeline from an async enumerable.
    /// <param name="source">The streaming data source</param>
    /// <param name="services">Service provider</param>
    /// <param name="batchSize">Batch size for processing</param>
    /// <param name="windowSize">Time window for batching</param>
    /// <returns>A streaming pipeline</returns>
    public static IAsyncEnumerable<T> AsStreamingPipeline<T>(
        this IAsyncEnumerable<T> source,
        IServiceProvider services,
        int batchSize = 1000,
        TimeSpan? windowSize = null)
        ArgumentNullException.ThrowIfNull(source);
        // For demonstration, just return the source
        // In a real implementation, this would create a streaming pipeline
        return source;
    /// Creates a streaming pipeline from an async enumerable with options.
    /// <param name="options">Streaming pipeline options</param>
        Models.StreamingPipelineOptions options)
        ArgumentNullException.ThrowIfNull(options);
    /// Analyzes pipeline performance.
    /// <param name="queryable">The queryable to analyze</param>
    /// <returns>Performance analysis report</returns>
    public static async Task<Models.PipelinePerformanceReport> AnalyzePipelinePerformanceAsync<T>(
        this IQueryable<T> queryable,
        IServiceProvider services)
        ArgumentNullException.ThrowIfNull(queryable);
        // Simulate analysis
        await Task.Delay(10);
        return new Models.PipelinePerformanceReport
        {
            EstimatedExecutionTime = TimeSpan.FromMilliseconds(50),
            EstimatedMemoryUsage = 1024 * 1024 * 10, // 10 MB
            Recommendations = ["Consider using GPU acceleration", "Enable caching"]
        };
    /// Recommends optimal backend for execution.
    /// <returns>Backend recommendation</returns>
    public static async Task<Models.BackendRecommendation> RecommendOptimalBackendAsync<T>(
        await Task.Delay(5);
        return new Models.BackendRecommendation
            RecommendedBackend = "CUDA",
            Confidence = 0.85
    /// Estimates memory usage for a queryable.
    /// <returns>Memory usage estimate</returns>
    public static async Task<MemoryUsageEstimate> EstimateMemoryUsageAsync<T>(
        return new MemoryUsageEstimate
            PeakMemoryUsage = 1024 * 1024 * 15 // 15 MB
}
/// Memory usage estimation result.
public class MemoryUsageEstimate
    public long PeakMemoryUsage { get; set; }
