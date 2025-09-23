// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file is kept for backward compatibility and will be removed in future versions.
// Use DotCompute.Abstractions.Types.BottleneckType instead.

using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Legacy alias for BottleneckType. Use DotCompute.Abstractions.Types.BottleneckType instead.
/// </summary>
[System.Obsolete("Use DotCompute.Abstractions.Types.BottleneckType instead. This alias will be removed in a future version.")]
public enum BottleneckType
{
    /// <summary>
    /// Memory bandwidth limitation.
    /// </summary>
    MemoryBandwidth = Abstractions.Types.BottleneckType.MemoryBandwidth,

    /// <summary>
    /// Computational processing limitation.
    /// </summary>
    Compute = Abstractions.Types.BottleneckType.Compute,

    /// <summary>
    /// Memory latency limitation.
    /// </summary>
    MemoryLatency = Abstractions.Types.BottleneckType.MemoryLatency,

    /// <summary>
    /// Instruction issue limitation.
    /// </summary>
    InstructionIssue = Abstractions.Types.BottleneckType.InstructionIssue,

    /// <summary>
    /// Synchronization overhead limitation.
    /// </summary>
    Synchronization = Abstractions.Types.BottleneckType.Synchronization,

    /// <summary>
    /// Communication bandwidth limitation.
    /// </summary>
    Communication = Abstractions.Types.BottleneckType.Communication,

    /// <summary>
    /// Storage I/O limitation.
    /// </summary>
    Storage = Abstractions.Types.BottleneckType.Storage,

    /// <summary>
    /// Register pressure limitation.
    /// </summary>
    RegisterPressure = Abstractions.Types.BottleneckType.RegisterPressure,

    /// <summary>
    /// Shared memory bank conflicts.
    /// </summary>
    SharedMemoryBankConflicts = Abstractions.Types.BottleneckType.SharedMemoryBankConflicts,

    /// <summary>
    /// Cache misses limitation.
    /// </summary>
    CacheMisses = Abstractions.Types.BottleneckType.CacheMisses,

    /// <summary>
    /// Warp divergence limitation.
    /// </summary>
    WarpDivergence = Abstractions.Types.BottleneckType.WarpDivergence,

    /// <summary>
    /// Thread divergence limitation.
    /// </summary>
    Divergence = Abstractions.Types.BottleneckType.ThreadDivergence,

    /// <summary>
    /// Thermal or power throttling.
    /// </summary>
    Throttling = Abstractions.Types.BottleneckType.Throttling,

    /// <summary>
    /// Unknown or unidentified bottleneck.
    /// </summary>
    Unknown = Abstractions.Types.BottleneckType.Unknown,

    /// <summary>
    /// No significant bottleneck detected.
    /// </summary>
    None = Abstractions.Types.BottleneckType.None
}