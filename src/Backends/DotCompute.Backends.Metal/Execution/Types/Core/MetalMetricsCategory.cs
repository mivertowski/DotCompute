// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Core;

/// <summary>
/// Metrics collection categories
/// </summary>
[Flags]
public enum MetalMetricsCategory
{
    /// <summary>
    /// No metrics collection
    /// </summary>
    None = 0,

    /// <summary>
    /// Timing metrics
    /// </summary>
    Timing = 1 << 0,

    /// <summary>
    /// Memory usage metrics
    /// </summary>
    Memory = 1 << 1,

    /// <summary>
    /// GPU utilization metrics
    /// </summary>
    GpuUtilization = 1 << 2,

    /// <summary>
    /// Command buffer metrics
    /// </summary>
    CommandBuffer = 1 << 3,

    /// <summary>
    /// Pipeline state metrics
    /// </summary>
    Pipeline = 1 << 4,

    /// <summary>
    /// Error and recovery metrics
    /// </summary>
    Error = 1 << 5,

    /// <summary>
    /// All metrics categories
    /// </summary>
    All = Timing | Memory | GpuUtilization | CommandBuffer | Pipeline | Error
}