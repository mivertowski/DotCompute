// <copyright file="SynchronizationStrategy.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines synchronization strategies for coordinating parallel execution across multiple devices.
    /// These strategies determine how devices wait for and coordinate with each other during computation.
    /// </summary>
    public enum SynchronizationStrategy
    {
        /// <summary>
        /// Use CUDA events or OpenCL events for synchronization.
        /// Provides hardware-accelerated, low-overhead synchronization using device-native event objects.
        /// Recommended for GPU-heavy workloads where fine-grained synchronization is required.
        /// </summary>
        EventBased,

        /// <summary>
        /// Use barrier synchronization.
        /// All devices wait at synchronization points until all devices reach the same point.
        /// Suitable for lockstep execution patterns where all devices must complete a phase before proceeding.
        /// </summary>
        Barrier,

        /// <summary>
        /// Use lock-free atomic operations.
        /// Synchronization through atomic memory operations without explicit locks or barriers.
        /// Optimal for high-performance scenarios where lock contention would be problematic.
        /// </summary>
        LockFree,

        /// <summary>
        /// Use host-based synchronization.
        /// Synchronization is managed by the host CPU rather than device-specific mechanisms.
        /// Provides maximum compatibility across different device types at the cost of some performance.
        /// </summary>
        HostBased
    }
}
