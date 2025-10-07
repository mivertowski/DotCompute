// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution.Scheduling
{
    /// <summary>
    /// Represents the distribution of workload across multiple devices,
    /// including the strategy used and assignments for each device.
    /// </summary>
    public sealed class WorkloadDistribution
    {
        /// <summary>
        /// Gets or sets the list of work assignments for each device.
        /// Each assignment specifies what portion of the workload a device should handle.
        /// </summary>
        public required List<DeviceWorkAssignment> DeviceAssignments { get; init; }

        /// <summary>
        /// Gets or sets the load balancing strategy used for this distribution.
        /// This determines how the workload was divided among the available devices.
        /// </summary>
        public required LoadBalancingStrategy Strategy { get; set; }

        /// <summary>
        /// Gets or sets the total number of elements in the workload.
        /// This represents the complete size of the work to be distributed.
        /// </summary>
        public required int TotalElements { get; set; }
    }
}