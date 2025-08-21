// <copyright file="KernelExecutionRecord.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Results;

/// <summary>
/// Historical record of a kernel execution.
/// Captures execution details for analysis and auditing.
/// </summary>
public class KernelExecutionRecord
{
    /// <summary>
    /// Gets the kernel name.
    /// Identifier of the executed kernel.
    /// </summary>
    public required string KernelName { get; init; }

    /// <summary>
    /// Gets the execution timestamp.
    /// When the kernel was executed.
    /// </summary>
    public DateTime ExecutionTime { get; init; }

    /// <summary>
    /// Gets the execution duration.
    /// Time taken to complete the kernel execution.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets whether the execution was successful.
    /// Indicates if the kernel completed without errors.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the execution parameters.
    /// Input parameters used for this kernel execution.
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = [];
}