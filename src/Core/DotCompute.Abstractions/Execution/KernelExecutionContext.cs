// <copyright file="KernelExecutionContext.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using DotCompute.Abstractions.Enums;

namespace DotCompute.Abstractions.Execution;

/// <summary>
/// Represents the execution context for a kernel.
/// </summary>
public class KernelExecutionContext
{
    /// <summary>
    /// Gets or sets the unique identifier for this execution context.
    /// </summary>
    public Guid ExecutionId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the target accelerator for execution.
    /// </summary>
    public string? AcceleratorId { get; set; }

    /// <summary>
    /// Gets or sets the optimization level for this execution.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;

    /// <summary>
    /// Gets or sets the memory optimization level for this execution.
    /// </summary>
    public MemoryOptimizationLevel MemoryOptimizationLevel { get; set; } = MemoryOptimizationLevel.Balanced;

    /// <summary>
    /// Gets or sets the maximum execution time in milliseconds.
    /// </summary>
    public int? TimeoutMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets additional execution parameters.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to enable profiling for this execution.
    /// </summary>
    public bool EnableProfiling { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable debug mode for this execution.
    /// </summary>
    public bool EnableDebugMode { get; set; }

    /// <summary>
    /// Creates a default execution context.
    /// </summary>
    /// <returns>A new instance of <see cref="KernelExecutionContext"/> with default settings.</returns>
    public static KernelExecutionContext CreateDefault() => new();
}