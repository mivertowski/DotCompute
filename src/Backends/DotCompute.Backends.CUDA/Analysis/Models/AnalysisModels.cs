// <copyright file="AnalysisModels.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Backends.CUDA.Advanced.Types;
using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.CUDA.Analysis.Models;

/// <summary>
/// Model for memory access analysis results.
/// </summary>
public sealed class MemoryAccessAnalysis
{
    /// <summary>
    /// Gets or sets the memory access pattern type.
    /// </summary>
    public MemoryAccessPattern Pattern { get; set; }

    /// <summary>
    /// Gets or sets the coalescing efficiency percentage.
    /// </summary>
    public double CoalescingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the bank conflicts count.
    /// </summary>
    public int BankConflicts { get; set; }

    /// <summary>
    /// Gets or sets the stride information.
    /// </summary>
    public int Stride { get; set; }

    /// <summary>
    /// Gets or sets additional analysis metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Model for kernel performance analysis.
/// </summary>
public sealed class KernelPerformanceModel
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string? KernelName { get; set; }

    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the occupancy percentage.
    /// </summary>
    public double Occupancy { get; set; }

    /// <summary>
    /// Gets or sets the throughput in operations per second.
    /// </summary>
    public double Throughput { get; set; }

    /// <summary>
    /// Gets or sets the memory bandwidth utilization.
    /// </summary>
    public double MemoryBandwidthUtilization { get; set; }

    /// <summary>
    /// Gets or sets identified performance bottlenecks.
    /// </summary>
    public List<DotCompute.Backends.CUDA.Advanced.Profiling.Types.BottleneckType> Bottlenecks { get; set; } = [];
}

/// <summary>
/// Model for workload characteristics analysis.
/// </summary>
public sealed class WorkloadCharacteristics
{
    /// <summary>
    /// Gets or sets the workload type.
    /// </summary>
    public WorkloadType Type { get; set; }

    /// <summary>
    /// Gets or sets the compute intensity ratio.
    /// </summary>
    public double ComputeIntensity { get; set; }

    /// <summary>
    /// Gets or sets the memory intensity ratio.
    /// </summary>
    public double MemoryIntensity { get; set; }

    /// <summary>
    /// Gets or sets the parallelism level.
    /// </summary>
    public int ParallelismLevel { get; set; }

    /// <summary>
    /// Gets or sets the data size in bytes.
    /// </summary>
    public long DataSizeBytes { get; set; }
}