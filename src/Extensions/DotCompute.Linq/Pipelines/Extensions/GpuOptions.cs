// <copyright file="GpuOptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Linq.Pipelines.Extensions;

/// <summary>
/// Configuration options for GPU grouping operations.
/// </summary>
public class GpuGroupingOptions
{
    public string PreferredBackend { get; set; } = "CUDA";
    public int BatchSize { get; set; } = 1000;
    public bool EnableLocalReduction { get; set; } = true;
}

/// <summary>
/// Configuration options for GPU join operations.
/// </summary>
public class GpuJoinOptions
{
    public JoinAlgorithm JoinAlgorithm { get; set; } = JoinAlgorithm.HashJoin;
    public bool EnableHashOptimization { get; set; } = true;
}

/// <summary>
/// Join algorithms for GPU operations.
/// </summary>
public enum JoinAlgorithm
{
    HashJoin,
    SortMergeJoin,
    NestedLoopJoin
}

/// <summary>
/// Configuration options for GroupBy operations with GPU acceleration.
/// </summary>
public class GroupByOptions
{
    /// <summary>
    /// Expected number of groups for hash table sizing optimization.
    /// </summary>
    public int ExpectedGroupCount { get; set; } = 1000;

    /// <summary>
    /// Size of the hash table for grouping operations.
    /// </summary>
    public int HashTableSize { get; set; } = 8192;

    /// <summary>
    /// Whether to enable result caching for repeated operations.
    /// </summary>
    public bool EnableResultCaching { get; set; } = true;

    /// <summary>
    /// Operation timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Preferred backend for grouping operations.
    /// </summary>
    public string PreferredBackend { get; set; } = "CUDA";
}

/// <summary>
/// Defines an aggregate function for multi-aggregate operations.
/// </summary>
/// <typeparam name="T">The input data type for aggregation.</typeparam>
public class AggregateFunction<T>
{
    /// <summary>
    /// Name of the aggregate function for identification.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The aggregation function to apply to the data.
    /// </summary>
    public Func<IEnumerable<T>, object> Function { get; set; } = _ => new object();

    /// <summary>
    /// The expected result type of the aggregation.
    /// </summary>
    public Type ResultType { get; set; } = typeof(object);

    /// <summary>
    /// Whether this function supports GPU acceleration.
    /// </summary>
    public bool SupportsGpu { get; set; } = true;

    /// <summary>
    /// Memory usage estimate for the function.
    /// </summary>
    public long EstimatedMemoryUsage { get; set; } = 1024;
}

/// <summary>
/// Configuration options for join operations.
/// </summary>
public class JoinOptions
{
    /// <summary>
    /// Whether to prefer GPU execution for join operations.
    /// </summary>
    public bool PreferGpu { get; set; } = true;

    /// <summary>
    /// Expected size of the result set for memory allocation.
    /// </summary>
    public int ExpectedResultSize { get; set; } = 10000;

    /// <summary>
    /// Size of the hash table for hash join operations.
    /// </summary>
    public int HashTableSize { get; set; } = 16384;

    /// <summary>
    /// Whether to enable result caching.
    /// </summary>
    public bool EnableResultCaching { get; set; } = true;

    /// <summary>
    /// Operation timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 60000;
}

/// <summary>
/// Configuration options for reduction operations.
/// </summary>
public class ReductionOptions
{
    /// <summary>
    /// Whether to use tree reduction algorithm for better parallelization.
    /// </summary>
    public bool UseTreeReduction { get; set; } = true;

    /// <summary>
    /// Size of data chunks for processing.
    /// </summary>
    public int ChunkSize { get; set; } = 1024;

    /// <summary>
    /// Whether to enable intermediate result caching.
    /// </summary>
    public bool EnableIntermediateCaching { get; set; } = true;

    /// <summary>
    /// Operation timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;
}

/// <summary>
/// Configuration options for scan (prefix sum) operations.
/// </summary>
public class ScanOptions
{
    /// <summary>
    /// Whether to perform inclusive scan (includes current element).
    /// </summary>
    public bool InclusiveScan { get; set; } = true;

    /// <summary>
    /// Size of data chunks for processing.
    /// </summary>
    public int ChunkSize { get; set; } = 1024;

    /// <summary>
    /// Whether to enable result caching.
    /// </summary>
    public bool EnableResultCaching { get; set; } = true;

    /// <summary>
    /// Operation timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;
}