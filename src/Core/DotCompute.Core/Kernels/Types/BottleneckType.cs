// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Kernels;

/// <summary>
/// Defines types of performance bottlenecks that can occur in computational workloads.
/// These bottleneck types help identify the primary limiting factors in system performance
/// and guide optimization strategies for improving overall throughput.
/// </summary>
public enum BottleneckType
{
    /// <summary>
    /// Memory bandwidth limitation.
    /// The system is constrained by the rate at which data can be transferred
    /// to and from memory. Common in memory-intensive workloads where
    /// computation speed exceeds memory access speed.
    /// </summary>
    MemoryBandwidth,

    /// <summary>
    /// Computational processing limitation.
    /// The system is limited by the computational capacity of processing units.
    /// Occurs when arithmetic operations or algorithmic complexity
    /// exceed the available processing power.
    /// </summary>
    Compute,

    /// <summary>
    /// Memory latency limitation.
    /// Performance is constrained by memory access latency rather than bandwidth.
    /// Common when accessing scattered memory locations or cache misses.
    /// </summary>
    MemoryLatency,

    /// <summary>
    /// Instruction issue limitation.
    /// The system is limited by instruction throughput and scheduling.
    /// Occurs when the processor cannot issue instructions fast enough.
    /// </summary>
    InstructionIssue,

    /// <summary>
    /// Synchronization overhead limitation.
    /// Performance is constrained by synchronization operations between
    /// parallel execution units. Common in highly parallel workloads
    /// with frequent coordination requirements.
    /// </summary>
    Synchronization,

    /// <summary>
    /// Communication bandwidth limitation.
    /// The system is limited by inter-device or inter-node communication capacity.
    /// Typical in distributed computing scenarios where data exchange
    /// between computing units becomes the limiting factor.
    /// </summary>
    Communication,

    /// <summary>
    /// Storage I/O limitation.
    /// Performance is constrained by storage system read/write operations.
    /// Common in data-intensive applications where persistent storage
    /// access patterns limit overall throughput.
    /// </summary>
    Storage,

    /// <summary>
    /// Register pressure limitation.
    /// The processor has insufficient registers for the workload.
    /// </summary>
    RegisterPressure,

    /// <summary>
    /// Shared memory bank conflicts.
    /// Performance is limited by conflicts in shared memory access patterns.
    /// </summary>
    SharedMemoryBankConflicts,

    /// <summary>
    /// Cache misses limitation.
    /// Performance is limited by cache miss rate.
    /// </summary>
    CacheMisses,

    /// <summary>
    /// Warp divergence limitation.
    /// Performance is limited by divergent execution paths in SIMD units.
    /// </summary>
    WarpDivergence,
    
    /// <summary>
    /// Thread divergence limitation.
    /// Performance is limited by divergent execution paths between threads.
    /// </summary>
    Divergence,
    
    /// <summary>
    /// Thermal or power throttling.
    /// Performance is limited by thermal or power constraints causing frequency reduction.
    /// </summary>
    Throttling,

    /// <summary>
    /// Unknown or unidentified bottleneck.
    /// The bottleneck type could not be determined or doesn't fit standard categories.
    /// </summary>
    Unknown,

    /// <summary>
    /// No significant bottleneck detected.
    /// The system is well-balanced with no single limiting factor.
    /// </summary>
    None
}