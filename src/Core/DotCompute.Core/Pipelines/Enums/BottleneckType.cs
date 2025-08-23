// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Enums;

/// <summary>
/// Defines the different types of performance bottlenecks that can occur during pipeline execution.
/// Each type represents a specific category of performance limitation with distinct characteristics.
/// </summary>
public enum BottleneckType
{
    /// <summary>
    /// Memory bandwidth limitation - data transfer rate is the limiting factor.
    /// Often occurs when kernels are memory-bound rather than compute-bound.
    /// </summary>
    MemoryBandwidth,

    /// <summary>
    /// Compute throughput limitation - processing capacity is the limiting factor.
    /// Occurs when kernels are compute-bound and need more processing power.
    /// </summary>
    ComputeThroughput,

    /// <summary>
    /// Data transfer overhead - time spent moving data between memory locations.
    /// Common between host-device transfers or inefficient memory access patterns.
    /// </summary>
    DataTransfer,

    /// <summary>
    /// Kernel launch overhead - time spent setting up and launching compute kernels.
    /// Often indicates too many small kernel launches that could be fused.
    /// </summary>
    KernelLaunch,

    /// <summary>
    /// Synchronization overhead - time spent waiting for operations to complete.
    /// Common in pipelines with excessive synchronization points or dependencies.
    /// </summary>
    Synchronization,

    /// <summary>
    /// Memory allocation overhead - time spent allocating and deallocating memory.
    /// Often indicates inefficient memory management or lack of memory pooling.
    /// </summary>
    MemoryAllocation,

    /// <summary>
    /// Pipeline orchestration overhead - time spent coordinating pipeline stages.
    /// Occurs when the pipeline management itself becomes a performance bottleneck.
    /// </summary>
    Orchestration
}