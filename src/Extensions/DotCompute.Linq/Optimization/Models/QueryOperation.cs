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

    /// <summary>Gets or sets the estimated memory usage in bytes.</summary>
    public long EstimatedMemoryUsage { get; set; }

    /// <summary>Gets or sets the operation's selectivity factor (0.0 to 1.0).</summary>
    public double Selectivity { get; set; } = 1.0;

    /// <summary>Gets or sets the GPU optimization configuration for this operation.</summary>
    public GpuOptimizationConfig? GpuOptimizationConfig { get; set; }

    /// <summary>Gets or sets the parallelization configuration for this operation.</summary>
    public ParallelizationConfig? ParallelizationConfig { get; set; }

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
            ParallelizationConfig = ParallelizationConfig
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
}