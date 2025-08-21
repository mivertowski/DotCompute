// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Types
{
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
        /// Network bandwidth or latency limitation.
        /// The system is limited by network infrastructure capacity or latency.
        /// Occurs in distributed systems where network communication
        /// becomes the primary performance constraint.
        /// </summary>
        Network
    }
}