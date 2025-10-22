// <copyright file="GraphConfiguration.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Execution.Graph.Configuration;

/// <summary>
/// Configuration options for CUDA graph execution.
/// Provides comprehensive settings for graph optimization and execution behavior.
/// </summary>
public sealed class GraphConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent graph executions.
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets whether graph optimization is enabled.
    /// </summary>
    public bool EnableOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets the optimization timeout in milliseconds.
    /// </summary>
    public int OptimizationTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets the default graph configuration.
    /// </summary>
    public static GraphConfiguration Default { get; } = new();
}

/// <summary>
/// Configuration for graph capture operations.
/// </summary>
public sealed class GraphCaptureConfiguration
{
    /// <summary>
    /// Gets or sets the capture mode for the graph.
    /// </summary>
    public GraphCaptureMode CaptureMode { get; set; } = GraphCaptureMode.Global;

    /// <summary>
    /// Gets or sets whether to enable dependency tracking.
    /// </summary>
    public bool EnableDependencyTracking { get; set; } = true;
}

/// <summary>
/// Graph capture mode enumeration.
/// </summary>
public enum GraphCaptureMode
{
    /// <summary>Global capture mode.</summary>
    Global = 0,
    /// <summary>Thread-local capture mode.</summary>
    ThreadLocal = 1,
    /// <summary>Relaxed capture mode.</summary>
    Relaxed = 2
}