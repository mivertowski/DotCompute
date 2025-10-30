// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines the types of communication operations available in distributed computing scenarios.
    /// These operations facilitate data exchange between multiple computing devices or nodes.
    /// </summary>
    public enum CommunicationOperationType
    {
        /// <summary>
        /// Point-to-point transfer between two devices.
        /// Used for direct communication between specific source and destination devices.
        /// </summary>
        PointToPoint,

        /// <summary>
        /// Broadcast from one device to multiple devices.
        /// One source device sends the same data to all other devices in the communication group.
        /// </summary>
        Broadcast,

        /// <summary>
        /// Gather data from multiple devices to one device.
        /// Collects data from all devices in the group and aggregates it on a single destination device.
        /// </summary>
        Gather,

        /// <summary>
        /// Scatter data from one device to multiple devices.
        /// Distributes different portions of data from one source device to multiple destination devices.
        /// </summary>
        Scatter,

        /// <summary>
        /// All-reduce operation across all devices.
        /// Performs a reduction operation (e.g., sum, max, min) across all devices and makes the result available on all devices.
        /// </summary>
        AllReduce,

        /// <summary>
        /// All-gather operation across all devices.
        /// Gathers data from all devices and makes the complete dataset available on every device.
        /// </summary>
        AllGather
    }
}
