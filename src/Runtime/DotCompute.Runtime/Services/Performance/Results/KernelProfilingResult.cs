// <copyright file="KernelProfilingResult.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Results;

/// <summary>
/// Results from profiling a kernel execution.
/// Contains detailed performance metrics for a single kernel run.
/// </summary>
public class KernelProfilingResult
{
    /// <summary>
    /// Gets the kernel name.
    /// Identifier of the profiled kernel.
    /// </summary>
    public required string KernelName { get; init; }

    /// <summary>
    /// Gets the execution time.
    /// Duration of the kernel execution.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets the memory used in bytes.
    /// Total memory consumed during kernel execution.
    /// </summary>
    public long MemoryUsed { get; init; }

    /// <summary>
    /// Gets additional performance metrics.
    /// Custom metrics specific to the kernel operation.
    /// </summary>
    public Dictionary<string, double> Metrics { get; init; } = [];
}