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
/// Configuration options for GPU join operations.
public class GpuJoinOptions
    public JoinAlgorithm JoinAlgorithm { get; set; } = JoinAlgorithm.HashJoin;
    public bool EnableHashOptimization { get; set; } = true;
/// Join algorithms for GPU operations.
public enum JoinAlgorithm
    HashJoin,
    SortMergeJoin,
    NestedLoopJoin
/// Configuration options for GroupBy operations with GPU acceleration.
public class GroupByOptions
    /// <summary>
    /// Expected number of groups for hash table sizing optimization.
    /// </summary>
    public int ExpectedGroupCount { get; set; } = 1000;
    /// Size of the hash table for grouping operations.
    public int HashTableSize { get; set; } = 8192;
    /// Whether to enable result caching for repeated operations.
    public bool EnableResultCaching { get; set; } = true;
    /// Operation timeout in milliseconds.
    public int TimeoutMs { get; set; } = 30000;
    /// Preferred backend for grouping operations.
/// Defines an aggregate function for multi-aggregate operations.
/// <typeparam name="T">The input data type for aggregation.</typeparam>
public class AggregateFunction<T>
    /// Name of the aggregate function for identification.
    public string Name { get; set; } = string.Empty;
    /// The aggregation function to apply to the data.
    public Func<IEnumerable<T>, object> Function { get; set; } = _ => new object();
    /// The expected result type of the aggregation.
    public Type ResultType { get; set; } = typeof(object);
    /// Whether this function supports GPU acceleration.
    public bool SupportsGpu { get; set; } = true;
    /// Memory usage estimate for the function.
    public long EstimatedMemoryUsage { get; set; } = 1024;
/// Configuration options for join operations.
public class JoinOptions
    /// Whether to prefer GPU execution for join operations.
    public bool PreferGpu { get; set; } = true;
    /// Expected size of the result set for memory allocation.
    public int ExpectedResultSize { get; set; } = 10000;
    /// Size of the hash table for hash join operations.
    public int HashTableSize { get; set; } = 16384;
    /// Whether to enable result caching.
    public int TimeoutMs { get; set; } = 60000;
/// Configuration options for reduction operations.
public class ReductionOptions
    /// Whether to use tree reduction algorithm for better parallelization.
    public bool UseTreeReduction { get; set; } = true;
    /// Size of data chunks for processing.
    public int ChunkSize { get; set; } = 1024;
    /// Whether to enable intermediate result caching.
    public bool EnableIntermediateCaching { get; set; } = true;
/// Configuration options for scan (prefix sum) operations.
public class ScanOptions
    /// Whether to perform inclusive scan (includes current element).
    public bool InclusiveScan { get; set; } = true;
