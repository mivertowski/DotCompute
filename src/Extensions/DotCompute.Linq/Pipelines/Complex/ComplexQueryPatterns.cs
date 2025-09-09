// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Core.Pipelines;
using DotCompute.Linq.Pipelines.Models;

namespace DotCompute.Linq.Pipelines.Complex;

/// <summary>
/// Extension methods for complex LINQ query patterns with GPU optimization.
/// Provides GroupBy, Join, Aggregate, and Window function support with kernel acceleration.
/// </summary>
public static class ComplexQueryPatterns
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
        var groupingOptions = options ?? new GroupByOptions();
        
        var stageOptions = new PipelineStageOptions
        {
            PreferredBackend = "CUDA",
            EnableCaching = groupingOptions.EnableResultCaching,
            TimeoutMs = groupingOptions.TimeoutMs
        };

        return pipeline.Then<IEnumerable<TSource>, IEnumerable<IGrouping<TKey, TSource>>>(
            "OptimizedGroupByKernel",
            input => new object[] 
            { 
                input, 
                keySelector.Compile(), 
                groupingOptions.ExpectedGroupCount,
                groupingOptions.HashTableSize 
            },
            stageOptions);
    }

    /// <summary>
    /// Groups elements and applies an aggregate function to each group.
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <typeparam name="TKey">Key type for grouping</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="keySelector">Key selector expression</param>
    /// <param name="resultSelector">Result selector for each group</param>
    /// <param name="options">Grouping options</param>
    /// <returns>Pipeline with GroupBy and aggregate operation</returns>
    public static IKernelPipeline GroupByAggregate<TSource, TKey, TResult>(
        this IKernelPipeline pipeline,
        Expression<Func<TSource, TKey>> keySelector,
        Expression<Func<TKey, IEnumerable<TSource>, TResult>> resultSelector,
        GroupByOptions? options = null)
        where TSource : unmanaged where TKey : unmanaged, IEquatable<TKey> where TResult : unmanaged
    {
        // First group, then aggregate in parallel
        return pipeline
            .GroupByGpu(keySelector, options)
            .Parallel<IEnumerable<IGrouping<TKey, TSource>>, TResult>(
                ("GroupAggregateKernel", input => new object[] { input, resultSelector.Compile() }));
    }

    #endregion

    #region Join Operations with Parallel Processing

    /// <summary>
    /// Performs an inner join between two sequences using GPU-optimized hash joins.
    /// </summary>
    /// <typeparam name="TOuter">Outer sequence element type</typeparam>
    /// <typeparam name="TInner">Inner sequence element type</typeparam>
    /// <typeparam name="TKey">Join key type</typeparam>
    /// <typeparam name="TResult">Result element type</typeparam>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="inner">Inner sequence expression</param>
    /// <param name="outerKeySelector">Outer key selector</param>
    /// <param name="innerKeySelector">Inner key selector</param>
    /// <param name="resultSelector">Result selector</param>
    /// <param name="options">Join optimization options</param>
    /// <returns>Pipeline with optimized join operation</returns>
    public static IKernelPipeline JoinGpu<TOuter, TInner, TKey, TResult>(
        this IKernelPipeline pipeline,
        IEnumerable<TInner> inner,
        Expression<Func<TOuter, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector,
        Expression<Func<TOuter, TInner, TResult>> resultSelector,
        JoinOptions? options = null)
        where TOuter : unmanaged 
        where TInner : unmanaged 
        where TKey : unmanaged, IEquatable<TKey>
        where TResult : unmanaged
    {
        var joinOptions = options ?? new JoinOptions();
        
        var stageOptions = new PipelineStageOptions
        {
            PreferredBackend = "CUDA",
            EnableCaching = joinOptions.EnableHashTableCaching,
            TimeoutMs = joinOptions.TimeoutMs
        };

        return pipeline.Then<IEnumerable<TOuter>, IEnumerable<TResult>>(
            "OptimizedHashJoinKernel",
            input => new object[]
            {
                input,
                inner,
                outerKeySelector.Compile(),
                innerKeySelector.Compile(),
                resultSelector.Compile(),
                joinOptions.EstimatedResultSize
            },
            stageOptions);
    }

    /// <summary>
    /// Performs a left outer join with GPU optimization.
    /// </summary>
    /// <typeparam name="TOuter">Outer sequence element type</typeparam>
    /// <typeparam name="TInner">Inner sequence element type</typeparam>
    /// <typeparam name="TKey">Join key type</typeparam>
    /// <typeparam name="TResult">Result element type</typeparam>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="inner">Inner sequence</param>
    /// <param name="outerKeySelector">Outer key selector</param>
    /// <param name="innerKeySelector">Inner key selector</param>
    /// <param name="resultSelector">Result selector</param>
    /// <param name="defaultInner">Default inner value for non-matching outer elements</param>
    /// <param name="options">Join options</param>
    /// <returns>Pipeline with left outer join</returns>
    public static IKernelPipeline LeftJoinGpu<TOuter, TInner, TKey, TResult>(
        this IKernelPipeline pipeline,
        IEnumerable<TInner> inner,
        Expression<Func<TOuter, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector,
        Expression<Func<TOuter, TInner?, TResult>> resultSelector,
        TInner? defaultInner = default,
        JoinOptions? options = null)
        where TOuter : unmanaged 
        where TInner : unmanaged 
        where TKey : unmanaged, IEquatable<TKey>
        where TResult : unmanaged
    {
        var joinOptions = options ?? new JoinOptions();

        return pipeline.Then<IEnumerable<TOuter>, IEnumerable<TResult>>(
            "OptimizedLeftJoinKernel",
            input => new object[]
            {
                input,
                inner,
                outerKeySelector.Compile(),
                innerKeySelector.Compile(),
                resultSelector.Compile(),
                defaultInner
            },
            new PipelineStageOptions { PreferredBackend = "CUDA" });
    }

    #endregion

    #region Advanced Aggregate Functions

    /// <summary>
    /// Performs a custom aggregate operation with GPU acceleration.
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <typeparam name="TAccumulate">Accumulator type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="seed">Initial accumulator value</param>
    /// <param name="func">Accumulator function</param>
    /// <param name="resultSelector">Result selector</param>
    /// <param name="options">Aggregation options</param>
    /// <returns>Pipeline with custom aggregate operation</returns>
    public static IKernelPipeline AggregateGpu<TSource, TAccumulate, TResult>(
        this IKernelPipeline pipeline,
        TAccumulate seed,
        Expression<Func<TAccumulate, TSource, TAccumulate>> func,
        Expression<Func<TAccumulate, TResult>> resultSelector,
        AggregateOptions? options = null)
        where TSource : unmanaged 
        where TAccumulate : unmanaged 
        where TResult : unmanaged
    {
        var aggregateOptions = options ?? new AggregateOptions();
        
        return pipeline.Then<IEnumerable<TSource>, TResult>(
            "OptimizedAggregateKernel",
            input => new object[]
            {
                input,
                seed,
                func.Compile(),
                resultSelector.Compile(),
                aggregateOptions.UseTreeReduction
            },
            new PipelineStageOptions 
            { 
                PreferredBackend = "CUDA",
                EnableCaching = aggregateOptions.EnableIntermediateResultCaching
            });
    }

    /// <summary>
    /// Computes multiple aggregates simultaneously with shared iteration.
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="aggregateFunctions">Multiple aggregate functions to compute</param>
    /// <returns>Pipeline with multi-aggregate operation</returns>
    public static IKernelPipeline MultiAggregate<TSource>(
        this IKernelPipeline pipeline,
        params AggregateFunction<TSource>[] aggregateFunctions)
        where TSource : unmanaged
    {
        return pipeline.Then<IEnumerable<TSource>, Dictionary<string, object>>(
            "MultiAggregateKernel",
            input => new object[] { input, aggregateFunctions },
            new PipelineStageOptions 
            { 
                PreferredBackend = "CUDA",
                EnableCaching = true 
            });
    }

    #endregion

    #region Window Functions

    /// <summary>
    /// Applies a sliding window function over the sequence.
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <typeparam name="TResult">Result element type</typeparam>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="windowSize">Size of the sliding window</param>
    /// <param name="windowFunction">Function to apply to each window</param>
    /// <param name="options">Window function options</param>
    /// <returns>Pipeline with sliding window operation</returns>
    public static IKernelPipeline SlidingWindow<TSource, TResult>(
        this IKernelPipeline pipeline,
        int windowSize,
        Expression<Func<IEnumerable<TSource>, TResult>> windowFunction,
        WindowOptions? options = null)
        where TSource : unmanaged where TResult : unmanaged
    {
        var windowOptions = options ?? new WindowOptions();
        
        return pipeline.Then<IEnumerable<TSource>, IEnumerable<TResult>>(
            "SlidingWindowKernel",
            input => new object[]
            {
                input,
                windowSize,
                windowFunction.Compile(),
                windowOptions.Stride,
                windowOptions.PaddingStrategy
            },
            new PipelineStageOptions { PreferredBackend = "CUDA" });
    }

    /// <summary>
    /// Applies a tumbling (non-overlapping) window function.
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <typeparam name="TResult">Result element type</typeparam>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="windowSize">Size of each window</param>
    /// <param name="windowFunction">Function to apply to each window</param>
    /// <returns>Pipeline with tumbling window operation</returns>
    public static IKernelPipeline TumblingWindow<TSource, TResult>(
        this IKernelPipeline pipeline,
        int windowSize,
        Expression<Func<IEnumerable<TSource>, TResult>> windowFunction)
        where TSource : unmanaged where TResult : unmanaged
    {
        return pipeline.SlidingWindow(windowSize, windowFunction, 
            new WindowOptions { Stride = windowSize, PaddingStrategy = PaddingStrategy.None });
    }

    /// <summary>
    /// Computes a moving average with specified window size.
    /// </summary>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="windowSize">Window size for moving average</param>
    /// <returns>Pipeline with moving average computation</returns>
    public static IKernelPipeline MovingAverage(
        this IKernelPipeline pipeline,
        int windowSize)
    {
        return pipeline.SlidingWindow<float, float>(
            windowSize,
            window => Expression.Lambda<Func<IEnumerable<float>, float>>(
                Expression.Call(typeof(Enumerable), nameof(Enumerable.Average), new[] { typeof(float) },
                    Expression.Parameter(typeof(IEnumerable<float>), "window")),
                Expression.Parameter(typeof(IEnumerable<float>), "window")));
    }

    /// <summary>
    /// Rank elements within the sequence using GPU-optimized sorting.
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <typeparam name="TKey">Key type for ranking</typeparam>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="keySelector">Key selector for ranking</param>
    /// <param name="descending">Whether to rank in descending order</param>
    /// <returns>Pipeline with ranking operation</returns>
    public static IKernelPipeline Rank<TSource, TKey>(
        this IKernelPipeline pipeline,
        Expression<Func<TSource, TKey>> keySelector,
        bool descending = false)
        where TSource : unmanaged where TKey : unmanaged, IComparable<TKey>
    {
        return pipeline.Then<IEnumerable<TSource>, IEnumerable<(TSource item, int rank)>>(
            "RankKernel",
            input => new object[] { input, keySelector.Compile(), descending },
            new PipelineStageOptions { PreferredBackend = "CUDA" });
    }

    #endregion

    #region Statistical Functions

    /// <summary>
    /// Computes descriptive statistics for the sequence.
    /// </summary>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <returns>Pipeline with statistical computation</returns>
    public static IKernelPipeline DescriptiveStatistics(this IKernelPipeline pipeline)
    {
        return pipeline.Then<IEnumerable<float>, StatisticalSummary>(
            "StatisticsKernel",
            input => new object[] { input },
            new PipelineStageOptions 
            { 
                PreferredBackend = "CUDA",
                EnableCaching = true 
            });
    }

    /// <summary>
    /// Computes percentiles for the sequence.
    /// </summary>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="percentiles">Percentiles to compute (0-100)</param>
    /// <returns>Pipeline with percentile computation</returns>
    public static IKernelPipeline Percentiles(
        this IKernelPipeline pipeline,
        params double[] percentiles)
    {
        return pipeline.Then<IEnumerable<float>, Dictionary<double, float>>(
            "PercentilesKernel",
            input => new object[] { input, percentiles },
            new PipelineStageOptions { PreferredBackend = "CUDA" });
    }

    /// <summary>
    /// Computes correlation between two numeric sequences.
    /// </summary>
    /// <param name="pipeline">The kernel pipeline</param>
    /// <param name="otherSequence">The other sequence for correlation</param>
    /// <returns>Pipeline with correlation computation</returns>
    public static IKernelPipeline Correlation(
        this IKernelPipeline pipeline,
        IEnumerable<float> otherSequence)
    {
        return pipeline.Then<IEnumerable<float>, double>(
            "CorrelationKernel",
            input => new object[] { input, otherSequence },
            new PipelineStageOptions { PreferredBackend = "CUDA" });
    }

    #endregion
}

#region Supporting Types and Options

/// <summary>
/// Options for GroupBy operations.
/// </summary>
public class GroupByOptions
{
    /// <summary>Expected number of groups for hash table optimization.</summary>
    public int ExpectedGroupCount { get; set; } = 1000;

    /// <summary>Hash table size for grouping operations.</summary>
    public int HashTableSize { get; set; } = 4096;

    /// <summary>Whether to enable result caching.</summary>
    public bool EnableResultCaching { get; set; } = true;

    /// <summary>Timeout in milliseconds.</summary>
    public int TimeoutMs { get; set; } = 30000;
}

/// <summary>
/// Options for Join operations.
/// </summary>
public class JoinOptions
{
    /// <summary>Estimated size of the join result.</summary>
    public int EstimatedResultSize { get; set; } = 10000;

    /// <summary>Whether to enable hash table caching.</summary>
    public bool EnableHashTableCaching { get; set; } = true;

    /// <summary>Timeout in milliseconds.</summary>
    public int TimeoutMs { get; set; } = 60000;

    /// <summary>Join algorithm to use.</summary>
    public JoinAlgorithm Algorithm { get; set; } = JoinAlgorithm.Hash;
}

/// <summary>
/// Join algorithms.
/// </summary>
public enum JoinAlgorithm
{
    /// <summary>Hash-based join (good for small-medium datasets).</summary>
    Hash,
    
    /// <summary>Sort-merge join (good for large datasets).</summary>
    SortMerge,
    
    /// <summary>Nested loop join (fallback for complex conditions).</summary>
    NestedLoop
}

/// <summary>
/// Options for Aggregate operations.
/// </summary>
public class AggregateOptions
{
    /// <summary>Whether to use tree reduction for better parallelism.</summary>
    public bool UseTreeReduction { get; set; } = true;

    /// <summary>Whether to enable caching of intermediate results.</summary>
    public bool EnableIntermediateResultCaching { get; set; } = false;

    /// <summary>Chunk size for processing large datasets.</summary>
    public int ChunkSize { get; set; } = 1000;
}

/// <summary>
/// Aggregate function definition.
/// </summary>
/// <typeparam name="TSource">Source element type</typeparam>
public class AggregateFunction<TSource> where TSource : unmanaged
{
    /// <summary>Function name for identification.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The aggregate function to apply.</summary>
    public Func<IEnumerable<TSource>, object> Function { get; set; } = _ => 0;

    /// <summary>Result type of the aggregate function.</summary>
    public Type ResultType { get; set; } = typeof(object);
}

/// <summary>
/// Options for Window functions.
/// </summary>
public class WindowOptions
{
    /// <summary>Stride between windows (1 = sliding, window size = tumbling).</summary>
    public int Stride { get; set; } = 1;

    /// <summary>Padding strategy for incomplete windows.</summary>
    public PaddingStrategy PaddingStrategy { get; set; } = PaddingStrategy.None;

    /// <summary>Value to use for padding when strategy is Constant.</summary>
    public object? PaddingValue { get; set; }
}

/// <summary>
/// Padding strategies for window functions.
/// </summary>
public enum PaddingStrategy
{
    /// <summary>No padding - skip incomplete windows.</summary>
    None,
    
    /// <summary>Zero padding for incomplete windows.</summary>
    Zero,
    
    /// <summary>Repeat the last value for padding.</summary>
    Repeat,
    
    /// <summary>Use a constant value for padding.</summary>
    Constant,
    
    /// <summary>Reflect values for padding.</summary>
    Reflect
}

/// <summary>
/// Statistical summary result.
/// </summary>
public struct StatisticalSummary
{
    /// <summary>Number of elements.</summary>
    public int Count { get; set; }

    /// <summary>Sum of all elements.</summary>
    public double Sum { get; set; }

    /// <summary>Arithmetic mean.</summary>
    public double Mean { get; set; }

    /// <summary>Sample variance.</summary>
    public double Variance { get; set; }

    /// <summary>Sample standard deviation.</summary>
    public double StandardDeviation { get; set; }

    /// <summary>Minimum value.</summary>
    public double Min { get; set; }

    /// <summary>Maximum value.</summary>
    public double Max { get; set; }

    /// <summary>Median value.</summary>
    public double Median { get; set; }

    /// <summary>Mode (most frequent value).</summary>
    public double Mode { get; set; }

    /// <summary>Skewness measure.</summary>
    public double Skewness { get; set; }

    /// <summary>Kurtosis measure.</summary>
    public double Kurtosis { get; set; }
}

#endregion