// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Types
{
    /// <summary>
    /// Defines priority levels for command queues in compute devices.
    /// </summary>
    /// <remarks>
    /// Queue priorities allow the system to prioritize certain operations over others
    /// when multiple command queues are competing for device resources. The actual
    /// behavior depends on the underlying compute platform and device capabilities.
    /// Not all devices support priority-based scheduling.
    /// </remarks>
    public enum QueuePriority
    {
        /// <summary>
        /// Low priority queue.
        /// </summary>
        /// <remarks>
        /// Operations in low priority queues may be scheduled after normal and high
        /// priority operations. This is suitable for background or non-time-critical tasks.
        /// </remarks>
        Low,

        /// <summary>
        /// Normal priority queue.
        /// </summary>
        /// <remarks>
        /// The default priority level for command queues. Provides balanced scheduling
        /// behavior suitable for most general-purpose compute operations.
        /// </remarks>
        Normal,

        /// <summary>
        /// High priority queue.
        /// </summary>
        /// <remarks>
        /// Operations in high priority queues may receive preferential scheduling
        /// over normal and low priority operations. Use for time-critical or
        /// performance-sensitive tasks.
        /// </remarks>
        High
    }
}