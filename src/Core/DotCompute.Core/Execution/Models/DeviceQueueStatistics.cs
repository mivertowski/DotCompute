// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Models
{
    /// <summary>
    /// Device Queue Statistics
    /// </summary>
    public class DeviceQueueStatistics
    {
        /// <summary>
        /// Gets or sets the index of the device.
        /// </summary>
        /// <value>
        /// The index of the device.
        /// </value>
        public int DeviceIndex { get; set; }

        /// <summary>
        /// Gets or sets the size of the current queue.
        /// </summary>
        /// <value>
        /// The size of the current queue.
        /// </value>
        public int CurrentQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the total enqueued.
        /// </summary>
        /// <value>
        /// The total enqueued.
        /// </value>
        public long TotalEnqueued { get; set; }

        /// <summary>
        /// Gets or sets the total dequeued.
        /// </summary>
        /// <value>
        /// The total dequeued.
        /// </value>
        public long TotalDequeued { get; set; }

        /// <summary>
        /// Gets or sets the total stolen.
        /// </summary>
        /// <value>
        /// The total stolen.
        /// </value>
        public long TotalStolen { get; set; }
    }
}