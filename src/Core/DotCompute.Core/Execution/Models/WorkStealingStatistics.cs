// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Models
{
    /// <summary>
    /// Work Stealing Statistics
    /// </summary>
    public class WorkStealingStatistics
    {
        /// <summary>
        /// Gets or sets the total work items.
        /// </summary>
        /// <value>
        /// The total work items.
        /// </value>
        public int TotalWorkItems { get; set; }

        /// <summary>
        /// Gets or sets the completed work items.
        /// </summary>
        /// <value>
        /// The completed work items.
        /// </value>
        public int CompletedWorkItems { get; set; }

        /// <summary>
        /// Gets or sets the in progress work items.
        /// </summary>
        /// <value>
        /// The in progress work items.
        /// </value>
        public int InProgressWorkItems { get; set; }

        /// <summary>
        /// Gets or sets the pending work items.
        /// </summary>
        /// <value>
        /// The pending work items.
        /// </value>
        public int PendingWorkItems { get; set; }

        /// <summary>
        /// Gets or sets the device statistics.
        /// </summary>
        /// <value>
        /// The device statistics.
        /// </value>
        public DeviceQueueStatistics[] DeviceStatistics { get; set; } = [];

        /// <summary>
        /// Gets or sets the stealing statistics.
        /// </summary>
        /// <value>
        /// The stealing statistics.
        /// </value>
        public StealingStatistics StealingStatistics { get; set; } = new();
    }
}