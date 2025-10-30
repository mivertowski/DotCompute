// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Core.Execution.Workload;

namespace DotCompute.Core.Execution.Models
{
    /// <summary>
    /// Work Item Status
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class WorkItemStatus<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the work item.
        /// </summary>
        /// <value>
        /// The work item.
        /// </value>
        public required WorkItem<T> WorkItem { get; set; }

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <value>
        /// The status.
        /// </value>
        public WorkStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the index of the assigned device.
        /// </summary>
        /// <value>
        /// The index of the assigned device.
        /// </value>
        public int AssignedDeviceIndex { get; set; }

        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        /// <value>
        /// The start time.
        /// </value>
        public DateTimeOffset? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        /// <value>
        /// The end time.
        /// </value>
        public DateTimeOffset? EndTime { get; set; }
    }
}
