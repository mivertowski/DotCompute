// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Models;

/// <summary>
/// Defines the types of performance alerts that can be generated.
/// </summary>
public enum ExecutionPerformanceAlertType
{
    /// <summary>
    /// Performance degradation detected.
    /// </summary>
    PerformanceDegradation,

    /// <summary>
    /// High memory usage detected.
    /// </summary>
    HighMemoryUsage,

    /// <summary>
    /// Low efficiency detected.
    /// </summary>
    LowEfficiency,

    /// <summary>
    /// Execution timeout threshold exceeded.
    /// </summary>
    ExecutionTimeout,

    /// <summary>
    /// Device utilization is too low.
    /// </summary>
    LowDeviceUtilization,

    /// <summary>
    /// Memory bandwidth bottleneck detected.
    /// </summary>
    MemoryBandwidthBottleneck,

    /// <summary>
    /// Compute utilization bottleneck detected.
    /// </summary>
    ComputeBottleneck,

    /// <summary>
    /// Synchronization overhead is too high.
    /// </summary>
    HighSynchronizationOverhead,

    /// <summary>
    /// Execution failure rate is too high.
    /// </summary>
    HighFailureRate,

    /// <summary>
    /// Resource contention detected.
    /// </summary>
    ResourceContention
}
