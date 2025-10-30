// <copyright file="GraphOptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Backends.CUDA.Execution.Graph.Enums;

namespace DotCompute.Backends.CUDA.Execution.Graph.Options;

/// <summary>
/// Options for graph execution behavior.
/// </summary>
public sealed class GraphExecutionOptions
{
    /// <summary>
    /// Gets or sets the execution timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets whether to enable performance profiling.
    /// </summary>
    public bool EnableProfiling { get; set; }

    /// <summary>
    /// Gets or sets the optimization level.
    /// </summary>
    public GraphOptimizationLevel OptimizationLevel { get; set; } = GraphOptimizationLevel.Balanced;

    /// <summary>
    /// Gets or sets whether to enable error recovery.
    /// </summary>
    public bool EnableErrorRecovery { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum retry attempts on failure.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}

/// <summary>
/// Options for graph compilation behavior.
/// </summary>
public sealed class GraphCompilationOptions
{
    /// <summary>
    /// Gets or sets whether to enable optimization during compilation.
    /// </summary>
    public bool EnableOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets the compilation timeout in milliseconds.
    /// </summary>
    public int CompilationTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Gets or sets whether to enable debugging information.
    /// </summary>
    public bool EnableDebugging { get; set; }

    /// <summary>
    /// Gets or sets the target GPU architecture.
    /// </summary>
    public string? TargetArchitecture { get; set; } = "compute_80";
}
