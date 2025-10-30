// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines the types of synchronization mechanisms available for coordinating execution across devices.
    /// These synchronization types ensure proper ordering and coordination of operations in parallel computing environments.
    /// </summary>
    public enum SynchronizationType
    {
        /// <summary>
        /// Barrier synchronization - wait for all devices.
        /// All participating devices must reach the synchronization point before any can proceed.
        /// Ensures global synchronization across all devices in the execution group.
        /// </summary>
        Barrier,

        /// <summary>
        /// Event-based synchronization.
        /// Uses events to coordinate execution between devices, allowing for more fine-grained control
        /// over when specific operations can proceed based on completion of dependent operations.
        /// </summary>
        Event,

        /// <summary>
        /// Memory fence synchronization.
        /// Ensures memory operations are completed and visible across devices before proceeding.
        /// Critical for maintaining memory consistency in distributed memory systems.
        /// </summary>
        MemoryFence,

        /// <summary>
        /// Stream synchronization.
        /// Synchronizes execution streams within and across devices, ensuring proper ordering
        /// of operations within command queues or execution streams.
        /// </summary>
        Stream
    }
}
