// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file is kept for backward compatibility and will be removed in future versions.
// Use DotCompute.Abstractions.Types.BottleneckType instead.

using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Pipelines.Enums;

/// <summary>
/// Legacy alias for BottleneckType. Use DotCompute.Abstractions.Types.BottleneckType instead.
/// </summary>
[System.Obsolete("Use DotCompute.Abstractions.Types.BottleneckType instead. This alias will be removed in a future version.")]
public enum BottleneckType
{
    /// <summary>
    /// Memory bandwidth limitation - data transfer rate is the limiting factor.
    /// </summary>
    MemoryBandwidth = Abstractions.Types.BottleneckType.MemoryBandwidth,

    /// <summary>
    /// Compute throughput limitation - processing capacity is the limiting factor.
    /// </summary>
    ComputeThroughput = Abstractions.Types.BottleneckType.Compute,

    /// <summary>
    /// Data transfer overhead - time spent moving data between memory locations.
    /// </summary>
    DataTransfer = Abstractions.Types.BottleneckType.IO,

    /// <summary>
    /// Kernel launch overhead - time spent setting up and launching compute kernels.
    /// </summary>
    KernelLaunch = Abstractions.Types.BottleneckType.Synchronization,

    /// <summary>
    /// Synchronization overhead - time spent waiting for operations to complete.
    /// </summary>
    Synchronization = Abstractions.Types.BottleneckType.Synchronization,

    /// <summary>
    /// Memory allocation overhead - time spent allocating and deallocating memory.
    /// </summary>
    MemoryAllocation = Abstractions.Types.BottleneckType.Memory,

    /// <summary>
    /// Pipeline orchestration overhead - time spent coordinating pipeline stages.
    /// </summary>
    Orchestration = Abstractions.Types.BottleneckType.Synchronization
}