// <copyright file="IKernelProfiler.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Runtime.Services.Performance.Results;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Service for profiling individual kernel executions.
/// Provides detailed performance analysis for kernel operations.
/// </summary>
public interface IKernelProfiler
{
    /// <summary>
    /// Profiles a kernel execution.
    /// Measures and analyzes the performance of a single kernel execution.
    /// </summary>
    /// <param name="kernel">The compiled kernel to profile.</param>
    /// <param name="arguments">The kernel arguments for execution.</param>
    /// <param name="accelerator">The target accelerator device.</param>
    /// <returns>Detailed kernel profiling results.</returns>
    Task<KernelProfilingResult> ProfileAsync(
        ICompiledKernel kernel,
        KernelArguments arguments,
        IAccelerator accelerator);

    /// <summary>
    /// Starts continuous profiling for all kernel executions.
    /// Enables automatic profiling of all kernel operations system-wide.
    /// </summary>
    /// <returns>A task representing the start operation.</returns>
    Task StartContinuousProfilingAsync();

    /// <summary>
    /// Stops continuous profiling.
    /// Disables automatic profiling of kernel operations.
    /// </summary>
    /// <returns>A task representing the stop operation.</returns>
    Task StopContinuousProfilingAsync();

    /// <summary>
    /// Gets kernel execution history.
    /// Retrieves historical execution data for analysis.
    /// </summary>
    /// <param name="kernelName">Optional filter by kernel name.</param>
    /// <returns>Collection of kernel execution records.</returns>
    Task<IEnumerable<KernelExecutionRecord>> GetExecutionHistoryAsync(string? kernelName = null);
}