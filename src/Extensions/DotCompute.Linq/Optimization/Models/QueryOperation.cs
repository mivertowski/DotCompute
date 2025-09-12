// <copyright file="QueryOperation.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;

namespace DotCompute.Linq.Optimization.Models;

/// <summary>
/// Represents a single operation in a query execution plan.
/// </summary>
public class QueryOperation
{
    /// <summary>Gets or sets the operation type.</summary>
    public OperationType Type { get; set; }

    /// <summary>Gets or sets the input data size.</summary>
    public long InputSize { get; set; }

    /// <summary>Gets or sets the output data size.</summary>
    public long OutputSize { get; set; }

    /// <summary>Gets or sets the data type being processed.</summary>
    public Type DataType { get; set; } = typeof(object);

    /// <summary>Gets or sets the memory access pattern.</summary>
    public AccessPattern AccessPattern { get; set; }

    /// <summary>Gets or sets the stride for strided access patterns.</summary>
    public int Stride { get; set; } = 1;

    /// <summary>Gets or sets the estimated computational complexity.</summary>
    public double ComputeComplexity { get; set; }

    /// <summary>Gets or sets the operation identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the operation name or description.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the dependencies on other operations.</summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>Gets or sets operation-specific metadata.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>Gets or sets whether this operation can be parallelized.</summary>
    public bool IsParallelizable { get; set; } = true;

    /// <summary>Gets or sets whether this operation has side effects.</summary>
    public bool HasSideEffects { get; set; }

    /// <summary>Gets or sets whether this operation is safe for side effect analysis.</summary>
    public bool IsSideEffectSafe { get; set; } = true;

    /// <summary>Gets or sets whether this operation is associative.</summary>
    public bool IsAssociative { get; set; }

    /// <summary>Gets or sets the estimated memory usage in bytes.</summary>
    public long EstimatedMemoryUsage { get; set; }

    /// <summary>Gets or sets the operation's selectivity factor (0.0 to 1.0).</summary>
    public double Selectivity { get; set; } = 1.0;

    /// <summary>Gets or sets the GPU optimization configuration for this operation.</summary>
    public GpuOptimizationConfig? GpuOptimizationConfig { get; set; }

    /// <summary>Gets or sets the CPU optimization configuration for this operation.</summary>
    public CpuOptimizationConfig? CpuOptimizationConfig { get; set; }

    /// <summary>Gets or sets the parallelization configuration for this operation.</summary>
    public ParallelizationConfig? ParallelizationConfig { get; set; }

    /// <summary>Gets or sets the generated kernel associated with this operation.</summary>
    public DotCompute.Linq.Operators.Generation.GeneratedKernel? GeneratedKernel { get; set; }

    /// <summary>Gets or sets the list of fused operations for this kernel fusion operation.</summary>
    public List<QueryOperation>? FusedOperations { get; set; }

    /// <summary>Gets or sets the load balancing configuration for this operation.</summary>
    public LoadBalancingConfig? LoadBalancingConfig { get; set; }

    /// <summary>Gets or sets the cache efficiency score for this operation.</summary>
    public double CacheEfficiency { get; set; } = 1.0;

    /// <summary>Gets or sets the memory layout configuration for this operation.</summary>
    public string MemoryLayout { get; set; } = "Auto";

    /// <summary>Gets or sets the input ID for data flow tracking.</summary>
    public string InputId { get; set; } = string.Empty;

    /// <summary>Gets or sets the output ID for data flow tracking.</summary>
    public string OutputId { get; set; } = string.Empty;

    /// <summary>Gets or sets the input data type for this operation.</summary>
    public Type InputDataType { get; set; } = typeof(object);

    /// <summary>Gets or sets the output data type for this operation.</summary>
    public Type OutputDataType { get; set; } = typeof(object);

    /// <summary>Gets or sets the data structure optimization hint.</summary>
    public string DataStructureHint { get; set; } = "Array";

    /// <summary>Gets or sets the compression strategy for data.</summary>
    public string CompressionStrategy { get; set; } = "None";

    /// <summary>Gets or sets the memory alignment requirement in bytes.</summary>
    public int MemoryAlignment { get; set; } = 16;

    /// <summary>Gets or sets the cache blocking strategy.</summary>
    public string CacheBlockingStrategy { get; set; } = "Auto";

    /// <summary>Gets or sets the preferred NUMA node for this operation.</summary>
    public int PreferredNumaNode { get; set; } = -1;

    /// <summary>Gets or sets the NUMA memory policy for this operation.</summary>
    public NumaMemoryPolicy? NumaMemoryPolicy { get; set; }

    /// <summary>Gets or sets the prefetching configuration for this operation.</summary>
    public object? PrefetchingConfig { get; set; }

    /// <summary>Gets or sets the start time for this operation.</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>Gets or sets the end time for this operation.</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>Gets or sets the optimized block size for memory operations.</summary>
    public int OptimizedBlockSize { get; set; } = 1024;

    /// <summary>
    /// Creates a deep copy of this operation.
    /// </summary>
    /// <returns>A new QueryOperation instance with the same properties.</returns>
    public QueryOperation Clone()
    {
        return new QueryOperation
        {
            Type = Type,
            InputSize = InputSize,
            OutputSize = OutputSize,
            DataType = DataType,
            AccessPattern = AccessPattern,
            Stride = Stride,
            ComputeComplexity = ComputeComplexity,
            Id = Id,
            Name = Name,
            Dependencies = new List<string>(Dependencies),
            Metadata = new Dictionary<string, object>(Metadata),
            IsParallelizable = IsParallelizable,
            EstimatedMemoryUsage = EstimatedMemoryUsage,
            Selectivity = Selectivity,
            GpuOptimizationConfig = GpuOptimizationConfig,
            CpuOptimizationConfig = CpuOptimizationConfig,
            ParallelizationConfig = ParallelizationConfig,
            GeneratedKernel = GeneratedKernel,
            LoadBalancingConfig = LoadBalancingConfig,
            CacheEfficiency = CacheEfficiency,
            MemoryLayout = MemoryLayout,
            InputId = InputId,
            OutputId = OutputId,
            InputDataType = InputDataType,
            OutputDataType = OutputDataType,
            DataStructureHint = DataStructureHint,
            CompressionStrategy = CompressionStrategy,
            MemoryAlignment = MemoryAlignment,
            CacheBlockingStrategy = CacheBlockingStrategy,
            PreferredNumaNode = PreferredNumaNode,
            NumaMemoryPolicy = NumaMemoryPolicy,
            PrefetchingConfig = PrefetchingConfig,
            StartTime = StartTime,
            EndTime = EndTime,
            OptimizedBlockSize = OptimizedBlockSize
        };
    }
}

/// <summary>
/// Defines the types of operations that can be performed in a query.
/// </summary>
public enum OperationType
{
    /// <summary>Map operation (transform each element).</summary>
    Map,

    /// <summary>Filter operation (select elements based on predicate).</summary>
    Filter,

    /// <summary>Reduce operation (aggregate elements).</summary>
    Reduce,

    /// <summary>Aggregate operation (alias for Reduce for compatibility).</summary>
    Aggregate,

    /// <summary>Group by operation.</summary>
    GroupBy,

    /// <summary>Join operation.</summary>
    Join,

    /// <summary>Sort operation.</summary>
    Sort,

    /// <summary>Scan operation (prefix sum).</summary>
    Scan,

    /// <summary>Custom kernel operation.</summary>
    CustomKernel,

    /// <summary>Fused kernel operation combining multiple operations.</summary>
    FusedKernel,

    /// <summary>Memory transfer operation.</summary>
    MemoryTransfer,

    /// <summary>Synchronization barrier.</summary>
    Barrier
}

/// <summary>
/// Defines memory access patterns for operations.
/// </summary>
public enum AccessPattern
{
    /// <summary>Sequential access pattern.</summary>
    Sequential,

    /// <summary>Random access pattern.</summary>
    Random,

    /// <summary>Strided access pattern.</summary>
    Strided,

    /// <summary>Broadcast access pattern.</summary>
    Broadcast,

    /// <summary>Gather access pattern.</summary>
    Gather,

    /// <summary>Scatter access pattern.</summary>
    Scatter
}

/// <summary>
/// Configuration for GPU-specific optimizations.
/// </summary>
public class GpuOptimizationConfig
{
    /// <summary>Gets or sets the preferred block size for GPU kernels.</summary>
    public int PreferredBlockSize { get; set; } = 256;

    /// <summary>Gets or sets the shared memory size in bytes.</summary>
    public int SharedMemorySize { get; set; }

    /// <summary>Gets or sets the maximum registers per thread.</summary>
    public int MaxRegistersPerThread { get; set; }

    /// <summary>Gets or sets whether to use texture memory.</summary>
    public bool UseTextureMemory { get; set; }

    /// <summary>Gets or sets whether to use constant memory.</summary>
    public bool UseConstantMemory { get; set; }

    /// <summary>Gets or sets the occupancy target (0.0 to 1.0).</summary>
    public double OccupancyTarget { get; set; } = 1.0;

    /// <summary>Gets or sets the block size for GPU kernels.</summary>
    public int BlockSize { get; set; } = 256;

    /// <summary>Gets or sets the shared memory usage in bytes.</summary>
    public int SharedMemoryUsage { get; set; } = 0;
}

/// <summary>
/// Configuration for load balancing strategies.
/// </summary>
public class LoadBalancingConfig
{
    /// <summary>Gets or sets the load balancing strategy.</summary>
    public string Strategy { get; set; } = "Balanced";

    /// <summary>Gets or sets the maximum number of threads to use.</summary>
    public int MaxThreads { get; set; } = Environment.ProcessorCount;

    /// <summary>Gets or sets the chunk size for work distribution.</summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>Gets or sets whether to use dynamic scheduling.</summary>
    public bool UseDynamicScheduling { get; set; } = true;

    /// <summary>Gets or sets the grid size for GPU kernels.</summary>
    public int GridSize { get; set; } = 1;
}

/// <summary>
/// Configuration for parallelization strategies.
/// </summary>
public class ParallelizationConfig
{
    /// <summary>Gets or sets the preferred parallelization degree.</summary>
    public int PreferredParallelism { get; set; }

    /// <summary>Gets or sets the minimum work size per thread.</summary>
    public long MinWorkSizePerThread { get; set; } = 1;

    /// <summary>Gets or sets the maximum work size per thread.</summary>
    public long MaxWorkSizePerThread { get; set; } = long.MaxValue;

    /// <summary>Gets or sets whether to enable work stealing.</summary>
    public bool EnableWorkStealing { get; set; } = true;

    /// <summary>Gets or sets the load balancing strategy.</summary>
    public string LoadBalancingStrategy { get; set; } = "Dynamic";

    /// <summary>Gets or sets thread affinity preferences.</summary>
    public Dictionary<int, int> ThreadAffinity { get; set; } = new();

    /// <summary>Gets or sets the parallelization degree (alias for PreferredParallelism).</summary>
    public int Degree
    {

        get => PreferredParallelism;

        set => PreferredParallelism = value;

    }

    /// <summary>Gets or sets the chunk size for work distribution.</summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>Gets or sets whether load balancing is enabled.</summary>
    public bool LoadBalancingEnabled { get; set; } = true;
}

/// <summary>
/// Configuration for CPU-specific optimizations.
/// </summary>
public class CpuOptimizationConfig
{
    /// <summary>Gets or sets whether to use SIMD instructions.</summary>
    public bool UseSIMD { get; set; } = true;

    /// <summary>Gets or sets the preferred vector width.</summary>
    public int PreferredVectorWidth { get; set; } = 256;

    /// <summary>Gets or sets whether to enable cache blocking.</summary>
    public bool EnableCacheBlocking { get; set; } = true;

    /// <summary>Gets or sets the cache block size in bytes.</summary>
    public int CacheBlockSize { get; set; } = 64 * 1024; // 64KB

    /// <summary>Gets or sets whether to enable prefetching.</summary>
    public bool EnablePrefetching { get; set; } = true;

    /// <summary>Gets or sets the prefetch distance.</summary>
    public int PrefetchDistance { get; set; } = 8;

    /// <summary>Gets or sets whether to enable loop unrolling.</summary>
    public bool EnableLoopUnrolling { get; set; } = true;

    /// <summary>Gets or sets the unroll factor.</summary>
    public int UnrollFactor { get; set; } = 4;

    /// <summary>Gets or sets the NUMA affinity settings.</summary>
    public Dictionary<int, int> NumaAffinity { get; set; } = new();

    /// <summary>Gets or sets the start time for this operation.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Gets or sets the end time for this operation.</summary>
    public DateTime EndTime { get; set; }

    /// <summary>Gets or sets the NUMA configuration for CPU optimization.</summary>
    public NumaConfiguration? NumaConfiguration { get; set; }
}

/// <summary>
/// NUMA memory policy configuration.
/// </summary>
public class NumaMemoryPolicy
{
    /// <summary>Gets or sets the preferred NUMA node.</summary>
    public int PreferredNode { get; set; }

    /// <summary>Gets or sets the NUMA allocation policy.</summary>
    public NumaAllocationPolicy AllocationPolicy { get; set; }

    /// <summary>Gets or sets whether memory migration is enabled.</summary>
    public bool MigrationEnabled { get; set; }
}

/// <summary>
/// NUMA configuration for CPU optimization.
/// </summary>
public class NumaConfiguration
{
    /// <summary>Gets or sets the preferred NUMA node.</summary>
    public int PreferredNode { get; set; }

    /// <summary>Gets or sets the thread distribution strategy.</summary>
    public NumaThreadDistribution ThreadDistribution { get; set; }

    /// <summary>Gets or sets the memory binding policy.</summary>
    public NumaMemoryBinding MemoryBinding { get; set; }
}

/// <summary>
/// NUMA allocation policy.
/// </summary>
public enum NumaAllocationPolicy
{
    /// <summary>Default system allocation policy.</summary>
    Default,

    /// <summary>Allocate memory locally to the accessing thread.</summary>
    Local,

    /// <summary>Interleave memory across all NUMA nodes.</summary>
    Interleaved,

    /// <summary>Prefer specific NUMA node but fallback if needed.</summary>
    Preferred
}

/// <summary>
/// NUMA thread distribution strategy.
/// </summary>
public enum NumaThreadDistribution
{
    /// <summary>No specific NUMA thread placement.</summary>
    None,

    /// <summary>Distribute threads evenly across NUMA nodes.</summary>
    Balanced,

    /// <summary>Interleave threads across NUMA nodes.</summary>
    Interleaved,

    /// <summary>Pack threads onto specific NUMA nodes.</summary>
    Packed
}

/// <summary>
/// NUMA memory binding policy.
/// </summary>
public enum NumaMemoryBinding
{
    /// <summary>No specific memory binding.</summary>
    None,

    /// <summary>Bind memory to local NUMA node.</summary>
    Local,

    /// <summary>Bind memory to specific NUMA node.</summary>
    Specific,

    /// <summary>Interleave memory across NUMA nodes.</summary>
    Interleaved
}