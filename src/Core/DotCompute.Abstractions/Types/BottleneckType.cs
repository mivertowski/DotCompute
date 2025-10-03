// <copyright file="BottleneckType.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Defines types of performance bottlenecks that can occur in computational workloads.
/// These bottleneck types help identify the primary limiting factors in system performance
/// and guide optimization strategies for improving overall throughput.
/// </summary>
public enum BottleneckType
{
    /// <summary>
    /// No significant bottleneck detected.
    /// The system is well-balanced with no single limiting factor.
    /// </summary>
    None,

    /// <summary>
    /// CPU processing capacity limitation.
    /// The system is limited by computational capacity of CPU cores.
    /// </summary>
    CPU,

    /// <summary>
    /// GPU processing capacity limitation.
    /// The system is limited by computational capacity of GPU cores.
    /// </summary>
    GPU,

    /// <summary>
    /// Memory bandwidth limitation.
    /// The system is constrained by the rate at which data can be transferred
    /// to and from memory. Common in memory-intensive workloads.
    /// </summary>
    Memory,

    /// <summary>
    /// Memory bandwidth limitation (alias for Memory).
    /// </summary>
    MemoryBandwidth,

    /// <summary>
    /// Memory latency limitation.
    /// Performance is constrained by memory access latency rather than bandwidth.
    /// Common when accessing scattered memory locations or cache misses.
    /// </summary>
    MemoryLatency,

    /// <summary>
    /// I/O operations limitation.
    /// Performance is constrained by input/output operations.
    /// </summary>
    IO,

    /// <summary>
    /// Storage I/O limitation.
    /// Performance is constrained by storage system read/write operations.
    /// </summary>
    Storage,

    /// <summary>
    /// Network communication limitation.
    /// The system is limited by network bandwidth or latency.
    /// </summary>
    Network,

    /// <summary>
    /// Communication bandwidth limitation.
    /// The system is limited by inter-device or inter-node communication capacity.
    /// </summary>
    Communication,

    /// <summary>
    /// Synchronization overhead limitation.
    /// Performance is constrained by synchronization operations between
    /// parallel execution units.
    /// </summary>
    Synchronization,

    /// <summary>
    /// Thread divergence limitation.
    /// Performance is limited by divergent execution paths between threads.
    /// </summary>
    ThreadDivergence,

    /// <summary>
    /// Warp divergence limitation.
    /// Performance is limited by divergent execution paths in SIMD units.
    /// </summary>
    WarpDivergence,

    /// <summary>
    /// Low occupancy limiting performance.
    /// GPU occupancy is too low to hide memory latency.
    /// </summary>
    Occupancy,

    /// <summary>
    /// Computational processing limitation.
    /// The system is limited by the computational capacity of processing units.
    /// </summary>
    Compute,

    /// <summary>
    /// Instruction issue limitation.
    /// The system is limited by instruction throughput and scheduling.
    /// </summary>
    InstructionIssue,

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
    /// Thermal or power throttling.
    /// Performance is limited by thermal or power constraints causing frequency reduction.
    /// </summary>
    Throttling,

    /// <summary>
    /// Memory utilization bottleneck.
    /// Performance is limited by high memory utilization.
    /// </summary>
    MemoryUtilization,

    /// <summary>
    /// Kernel failures bottleneck.
    /// Performance is limited by kernel execution failures.
    /// </summary>
    KernelFailures,

    /// <summary>
    /// Unknown or unidentified bottleneck.
    /// The bottleneck type could not be determined or doesn't fit standard categories.
    /// </summary>
    Unknown
}
