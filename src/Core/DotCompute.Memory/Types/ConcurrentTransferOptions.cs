// <copyright file="ConcurrentTransferOptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Memory.Types;

/// <summary>
/// Configuration options for concurrent memory transfer operations.
/// </summary>
/// <remarks>
/// This class extends TransferOptions with additional settings specific to concurrent transfers,
/// including concurrency limits, load balancing strategies, and resource allocation policies.
/// </remarks>
public class ConcurrentTransferOptions : TransferOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent transfers.
    /// </summary>
    /// <value>The maximum concurrency level. Default is 2x the processor count.</value>
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount * 2;

    /// <summary>
    /// Gets or sets the minimum number of concurrent transfers to maintain.
    /// </summary>
    /// <value>The minimum concurrency level. Default is 1.</value>
    public int MinConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether to use dynamic concurrency adjustment.
    /// </summary>
    /// <value>True to automatically adjust concurrency based on system load; otherwise, false.</value>
    public bool EnableDynamicConcurrency { get; set; } = true;

    /// <summary>
    /// Gets or sets the load balancing strategy.
    /// </summary>
    /// <value>The strategy for distributing work across concurrent operations.</value>
    public LoadBalancingStrategy LoadBalancing { get; set; } = LoadBalancingStrategy.RoundRobin;

    /// <summary>
    /// Gets or sets a value indicating whether to enable work stealing.
    /// </summary>
    /// <value>True to allow idle workers to steal work from busy ones; otherwise, false.</value>
    public bool EnableWorkStealing { get; set; } = true;

    /// <summary>
    /// Gets or sets the batch size for processing multiple transfers.
    /// </summary>
    /// <value>The number of transfers to process in each batch. Default is 10.</value>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether to maintain transfer order.
    /// </summary>
    /// <value>True to preserve the order of transfers; false for best performance.</value>
    public bool PreserveOrder { get; set; }

    /// <summary>
    /// Gets or sets the memory limit for all concurrent transfers.
    /// </summary>
    /// <value>The maximum total memory in bytes that can be used. Default is 1GB.</value>
    public long TotalMemoryLimit { get; set; } = 1024L * 1024 * 1024; // 1GB

    /// <summary>
    /// Gets or sets the per-transfer memory limit.
    /// </summary>
    /// <value>The maximum memory in bytes per individual transfer. Default is 256MB.</value>
    public long PerTransferMemoryLimit { get; set; } = 256L * 1024 * 1024; // 256MB

    /// <summary>
    /// Gets or sets a value indicating whether to enable transfer pipelining.
    /// </summary>
    /// <value>True to pipeline transfers for better throughput; otherwise, false.</value>
    public bool EnablePipelining { get; set; } = true;

    /// <summary>
    /// Gets or sets the pipeline depth when pipelining is enabled.
    /// </summary>
    /// <value>The number of pipeline stages. Default is 3.</value>
    public int PipelineDepth { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether to enable transfer aggregation.
    /// </summary>
    /// <value>True to combine small transfers for efficiency; otherwise, false.</value>
    public bool EnableAggregation { get; set; } = true;

    /// <summary>
    /// Gets or sets the aggregation threshold.
    /// </summary>
    /// <value>The minimum size in bytes below which transfers are aggregated. Default is 1MB.</value>
    public long AggregationThreshold { get; set; } = 1024 * 1024; // 1MB

    /// <summary>
    /// Gets or sets a value indicating whether to enable parallel compression.
    /// </summary>
    /// <value>True to compress data in parallel threads; otherwise, false.</value>
    public bool EnableParallelCompression { get; set; } = true;

    /// <summary>
    /// Gets or sets the scheduling policy for concurrent transfers.
    /// </summary>
    /// <value>The policy for scheduling transfer operations.</value>
    public SchedulingPolicy SchedulingPolicy { get; set; } = SchedulingPolicy.Fair;

    /// <summary>
    /// Gets or sets a value indicating whether to enable memory pressure monitoring.
    /// </summary>
    /// <value>True to monitor and respond to memory pressure; otherwise, false.</value>
    public bool EnableMemoryPressureMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory pressure threshold.
    /// </summary>
    /// <value>The memory pressure level (0.0 to 1.0) at which to throttle transfers. Default is 0.85.</value>
    public double MemoryPressureThreshold { get; set; } = 0.85;

    /// <summary>
    /// Gets or sets a value indicating whether to enable adaptive chunk sizing.
    /// </summary>
    /// <value>True to adjust chunk sizes based on performance; otherwise, false.</value>
    public bool EnableAdaptiveChunkSizing { get; set; } = true;

    /// <summary>
    /// Gets concurrent transfer options optimized for high throughput.
    /// </summary>
    /// <value>Options optimized for maximum aggregate throughput.</value>
    public static new ConcurrentTransferOptions Default => new();

    /// <summary>
    /// Gets concurrent transfer options optimized for many small transfers.
    /// </summary>
    /// <value>Options optimized for handling many small transfers efficiently.</value>
    public static ConcurrentTransferOptions ManySmallTransfers => new()
    {
        MaxConcurrency = Environment.ProcessorCount * 4,
        BatchSize = 50,
        EnableAggregation = true,
        AggregationThreshold = 10 * 1024 * 1024, // 10MB
        ChunkSize = 256 * 1024, // 256KB
        EnableCompression = false,
        OptimizeForThroughput = false
    };

    /// <summary>
    /// Gets concurrent transfer options optimized for few large transfers.
    /// </summary>
    /// <value>Options optimized for handling a few large transfers.</value>
    public static ConcurrentTransferOptions FewLargeTransfers => new()
    {
        MaxConcurrency = Environment.ProcessorCount,
        EnableMemoryMapping = true,
        EnableParallelCompression = true,
        ChunkSize = 128 * 1024 * 1024, // 128MB
        EnablePipelining = true,
        PipelineDepth = 5,
        OptimizeForThroughput = true
    };

    /// <summary>
    /// Creates a copy of these concurrent transfer options.
    /// </summary>
    /// <returns>A new ConcurrentTransferOptions instance with the same values.</returns>
    public new ConcurrentTransferOptions Clone()
    {
        var clone = new ConcurrentTransferOptions
        {
            MaxConcurrency = MaxConcurrency,
            MinConcurrency = MinConcurrency,
            EnableDynamicConcurrency = EnableDynamicConcurrency,
            LoadBalancing = LoadBalancing,
            EnableWorkStealing = EnableWorkStealing,
            BatchSize = BatchSize,
            PreserveOrder = PreserveOrder,
            TotalMemoryLimit = TotalMemoryLimit,
            PerTransferMemoryLimit = PerTransferMemoryLimit,
            EnablePipelining = EnablePipelining,
            PipelineDepth = PipelineDepth,
            EnableAggregation = EnableAggregation,
            AggregationThreshold = AggregationThreshold,
            EnableParallelCompression = EnableParallelCompression,
            SchedulingPolicy = SchedulingPolicy,
            EnableMemoryPressureMonitoring = EnableMemoryPressureMonitoring,
            MemoryPressureThreshold = MemoryPressureThreshold,
            EnableAdaptiveChunkSizing = EnableAdaptiveChunkSizing
        };

        // Copy base properties
        var baseClone = base.Clone();
        foreach (var prop in typeof(TransferOptions).GetProperties())
        {
            prop.SetValue(clone, prop.GetValue(baseClone));
        }

        return clone;
    }
}

/// <summary>
/// Specifies the load balancing strategy for concurrent transfers.
/// </summary>
public enum LoadBalancingStrategy
{
    /// <summary>
    /// Distribute transfers in round-robin fashion.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Assign transfers to the least loaded worker.
    /// </summary>
    LeastLoaded,

    /// <summary>
    /// Use weighted distribution based on transfer size.
    /// </summary>
    WeightedSize,

    /// <summary>
    /// Random assignment of transfers.
    /// </summary>
    Random,

    /// <summary>
    /// Use work stealing for dynamic balancing.
    /// </summary>
    WorkStealing
}

/// <summary>
/// Specifies the scheduling policy for concurrent operations.
/// </summary>
public enum SchedulingPolicy
{
    /// <summary>
    /// Fair scheduling with equal priority.
    /// </summary>
    Fair,

    /// <summary>
    /// Priority-based scheduling.
    /// </summary>
    Priority,

    /// <summary>
    /// First-in-first-out scheduling.
    /// </summary>
    FIFO,

    /// <summary>
    /// Last-in-first-out scheduling.
    /// </summary>
    LIFO,

    /// <summary>
    /// Shortest job first scheduling.
    /// </summary>
    ShortestFirst
}
