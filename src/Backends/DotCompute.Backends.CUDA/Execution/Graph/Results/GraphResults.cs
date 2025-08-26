// <copyright file="GraphResults.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Backends.CUDA.Execution.Graph.Enums;

namespace DotCompute.Backends.CUDA.Execution.Graph.Results;

/// <summary>
/// Results from graph execution.
/// </summary>
public sealed class GraphExecutionResult
{
    /// <summary>
    /// Gets or sets the execution state.
    /// </summary>
    public GraphExecutionState State { get; set; }

    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets any error that occurred during execution.
    /// </summary>
    public Exception? Error { get; set; }

    /// <summary>
    /// Gets or sets additional execution metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether the execution was successful.
    /// </summary>
    public bool IsSuccess => State == GraphExecutionState.Completed && Error == null;
}

/// <summary>
/// Performance metrics from graph execution.
/// </summary>
public sealed class GraphPerformanceMetrics
{
    /// <summary>
    /// Gets or sets the total GPU time in milliseconds.
    /// </summary>
    public double GpuTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the memory bandwidth utilization percentage.
    /// </summary>
    public double MemoryBandwidthUtilization { get; set; }

    /// <summary>
    /// Gets or sets the compute utilization percentage.
    /// </summary>
    public double ComputeUtilization { get; set; }

    /// <summary>
    /// Gets or sets the number of kernel launches.
    /// </summary>
    public int KernelLaunches { get; set; }

    /// <summary>
    /// Gets or sets the total memory transfers in bytes.
    /// </summary>
    public long MemoryTransferBytes { get; set; }
}