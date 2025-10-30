// <copyright file="CudaGraphStatistics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Execution.Statistics;

/// <summary>
/// Aggregated statistics for a CUDA graph over its lifetime.
/// Used for performance monitoring and optimization decisions.
/// </summary>
public sealed class CudaGraphStatistics
{
    /// <summary>
    /// Gets or sets the unique identifier of the graph.
    /// </summary>
    public string GraphId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of instantiated versions of this graph.
    /// Multiple instances may exist for different parameter sets.
    /// </summary>
    public int InstanceCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of executions across all instances.
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the cumulative GPU time in milliseconds.
    /// Sum of all GPU execution times for performance analysis.
    /// </summary>
    public float TotalGpuTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in milliseconds.
    /// Calculated as TotalGpuTimeMs / TotalExecutions.
    /// </summary>
    public float AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last execution.
    /// Null if the graph has never been executed.
    /// </summary>
    public DateTimeOffset? LastExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets whether this graph has been optimized.
    /// Optimized graphs have undergone fusion or other transformations.
    /// </summary>
    public bool IsOptimized { get; set; }
}
