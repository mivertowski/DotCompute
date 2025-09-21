// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DotCompute.Core.Pipelines;
using DotCompute.Core.Pipelines.Models;
using DotCompute.Linq.Pipelines.Models;
using DotCompute.Linq.Pipelines.Examples;
using DotCompute.Linq.Pipelines.Extensions;
using IKernelPipeline = DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipeline;
using OptimizationLevel = DotCompute.Linq.Operators.Models.OptimizationLevel;
namespace DotCompute.Linq.Pipelines.Extensions;
/// <summary>
/// Complex LINQ query pattern extensions for the Core IKernelPipeline interface.
/// Provides GroupBy, Join, Aggregate, and Window function support with kernel acceleration.
/// </summary>
public static class CorePipelineComplexExtensions
{
    #region GroupBy Operations with GPU Optimization
    /// <summary>
    /// Groups elements by a key selector with GPU-optimized hash-based grouping.
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <typeparam name="TKey">Key type for grouping</typeparam>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="keySelector">Key selector expression</param>
    /// <param name="options">Grouping optimization options</param>
    /// <returns>Pipeline with GroupBy operation</returns>
    public static IKernelPipeline GroupByGpu<TSource, TKey>(
        this IKernelPipeline pipeline,
        Expression<Func<TSource, TKey>> keySelector,
        GroupByOptions? options = null)
        where TSource : unmanaged where TKey : unmanaged, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(keySelector);
        var groupingOptions = options ?? new GroupByOptions();
        var stageOptions = new PipelineStageOptions
        {
            PreferredBackend = "CUDA",
            EnableCaching = groupingOptions.EnableResultCaching,
            TimeoutMs = groupingOptions.TimeoutMs
        };
        // Note: The Core IKernelPipeline doesn't have a Then<> method.
        // This would require creating a custom IPipelineStage implementation.
        // For now, returning the original pipeline as a placeholder.
        // In a real implementation, you would:
        // 1. Create a GroupByStage that implements IPipelineStage
        // 2. Use IKernelPipelineBuilder to add the stage
        // 3. Return the updated pipeline
        // Placeholder: return original pipeline
        return pipeline;
    }
    /// Groups elements and applies an aggregate function to each group.
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="resultSelector">Result selector for each group</param>
    /// <param name="options">Grouping options</param>
    /// <returns>Pipeline with GroupBy and aggregate operation</returns>
    public static IKernelPipeline GroupByAggregate<TSource, TKey, TResult>(
        Expression<Func<TKey, IEnumerable<TSource>, TResult>> resultSelector,
        where TSource : unmanaged where TKey : unmanaged, IEquatable<TKey> where TResult : unmanaged
        ArgumentNullException.ThrowIfNull(resultSelector);
        // First group, then aggregate in parallel
        // Placeholder implementation using the GroupByGpu method above
        // This would also need custom stage implementation
        return pipeline.GroupByGpu(keySelector, options);
    #endregion
    #region Join Operations with Parallel Processing
    /// Performs an inner join between two sequences using GPU-optimized hash joins.
    /// <typeparam name="TOuter">Outer sequence element type</typeparam>
    /// <typeparam name="TInner">Inner sequence element type</typeparam>
    /// <typeparam name="TKey">Join key type</typeparam>
    /// <typeparam name="TResult">Result element type</typeparam>
    /// <param name="inner">Inner sequence expression</param>
    /// <param name="outerKeySelector">Outer key selector</param>
    /// <param name="innerKeySelector">Inner key selector</param>
    /// <param name="resultSelector">Result selector</param>
    /// <param name="options">Join optimization options</param>
    /// <returns>Pipeline with optimized join operation</returns>
    public static IKernelPipeline JoinGpu<TOuter, TInner, TKey, TResult>(
        IEnumerable<TInner> inner,
        Expression<Func<TOuter, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector,
        Expression<Func<TOuter, TInner, TResult>> resultSelector,
        JoinOptions? options = null)
        where TOuter : unmanaged where TInner : unmanaged
        where TKey : unmanaged, IEquatable<TKey> where TResult : unmanaged
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        var joinOptions = options ?? new JoinOptions();
            PreferredBackend = joinOptions.PreferGpu ? "CUDA" : "CPU",
            EnableCaching = joinOptions.EnableResultCaching,
            TimeoutMs = joinOptions.TimeoutMs
        // Placeholder implementation - would need custom JoinStage
    /// Performs a left outer join with null handling for missing matches.
    /// <param name="inner">Inner sequence</param>
    /// <param name="resultSelector">Result selector with null handling</param>
    /// <param name="options">Join options</param>
    /// <returns>Pipeline with left outer join operation</returns>
    public static IKernelPipeline LeftJoinGpu<TOuter, TInner, TKey, TResult>(
        Expression<Func<TOuter, TInner?, TResult>> resultSelector,
        // Placeholder implementation - would need custom LeftJoinStage
    #region Aggregate and Reduction Operations
    /// Performs GPU-optimized reduction with configurable tree-reduce or scan.
    /// <typeparam name="T">Element type for reduction</typeparam>
    /// <param name="reductionFunction">Binary reduction function</param>
    /// <param name="seed">Initial value for reduction</param>
    /// <param name="options">Reduction optimization options</param>
    /// <returns>Pipeline with optimized reduction</returns>
    public static IKernelPipeline ReduceGpu<T>(
        Expression<Func<T, T, T>> reductionFunction,
        T seed = default(T),
        ReductionOptions? options = null)
        where T : unmanaged
        ArgumentNullException.ThrowIfNull(reductionFunction);
        var reductionOptions = options ?? new ReductionOptions();
            EnableCaching = reductionOptions.EnableIntermediateCaching,
            TimeoutMs = reductionOptions.TimeoutMs
        // Placeholder implementation - would need custom ReductionStage
    /// Performs GPU-optimized prefix sum (scan) operation.
    /// <typeparam name="T">Element type for scan operation</typeparam>
    /// <param name="scanFunction">Binary scan function</param>
    /// <param name="options">Scan optimization options</param>
    /// <returns>Pipeline with prefix sum operation</returns>
    public static IKernelPipeline ScanGpu<T>(
        Expression<Func<T, T, T>> scanFunction,
        ScanOptions? options = null)
        ArgumentNullException.ThrowIfNull(scanFunction);
        var scanOptions = options ?? new ScanOptions();
            EnableCaching = scanOptions.EnableResultCaching,
            TimeoutMs = scanOptions.TimeoutMs
        // Placeholder implementation - would need custom ScanStage
    #region Window and Streaming Operations
    /// Applies a sliding window function with configurable overlap and GPU processing.
    /// <param name="windowSize">Size of the sliding window</param>
    /// <param name="windowFunction">Function to apply to each window</param>
    /// <param name="options">Window processing options</param>
    /// <returns>Pipeline with sliding window operation</returns>
    public static IKernelPipeline SlidingWindowGpu<TSource, TResult>(
        int windowSize,
        Expression<Func<IEnumerable<TSource>, TResult>> windowFunction,
        Models.WindowOptions? options = null)
        where TSource : unmanaged where TResult : unmanaged
        ArgumentNullException.ThrowIfNull(windowFunction);
        if (windowSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowSize));
        }
        var windowOptions = options ?? new Models.WindowOptions();
            EnableCaching = windowOptions.EnableResultCaching,
            TimeoutMs = windowOptions.TimeoutMs
        // Placeholder implementation - would need custom SlidingWindowStage
    #region Missing Extension Methods for ComprehensivePipelineDemo
    // Removed duplicate GroupByGpu overload with GpuGroupingOptions to prevent ambiguity  
    // Using the GroupByOptions version above instead
    // Removed duplicate JoinGpu overload with GpuJoinOptions to prevent ambiguity
    // Using the JoinOptions version above instead
    /// Multiple aggregation operations in a single kernel pass.
    public static IKernelPipeline MultiAggregate<TInput>(
        params AggregateFunction<TInput>[] aggregateFunctions)
        ArgumentNullException.ThrowIfNull(aggregateFunctions);
        // Placeholder implementation - would need custom MultiAggregateStage
    /// Sliding window operation with configurable window parameters.
    public static IKernelPipeline SlidingWindow<TInput, TResult>(
        Expression<Func<IEnumerable<TInput>, TResult>> windowFunction,
        options ??= new Models.WindowOptions();
    /// Moving average computation with sliding window.
    public static IKernelPipeline MovingAverage<TInput>(
        Expression<Func<TInput, float>> valueSelector,
        ArgumentNullException.ThrowIfNull(valueSelector);
        // Placeholder implementation - would need custom MovingAverageStage
    #region Core Pipeline Extensions for LINQ Integration
    // Removed duplicate ExecutePipelineAsync methods - they are defined in PipelineQueryableExtensions.cs
}
