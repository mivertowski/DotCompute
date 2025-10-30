// <copyright file="CudaGraphExecutionResult.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Execution.Results;

/// <summary>
/// Contains the results of a CUDA graph execution.
/// Provides performance metrics and execution status.
/// </summary>
public sealed class CudaGraphExecutionResult
{
    /// <summary>
    /// Gets or sets whether the graph execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the graph instance that was executed.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the graph template.
    /// </summary>
    public string GraphId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total wall-clock time for the execution.
    /// Includes launch overhead and synchronization time.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the GPU execution time in milliseconds.
    /// Measured using CUDA events for precise GPU timing.
    /// </summary>
    public float GpuTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the number of times this graph has been executed.
    /// Used for performance tracking and optimization decisions.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the error message if execution failed.
    /// Null when execution is successful.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
