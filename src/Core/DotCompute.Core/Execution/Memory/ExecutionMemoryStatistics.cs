// <copyright file="ExecutionMemoryStatistics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Memory
{
    /// <summary>
    /// Comprehensive memory statistics for execution planning across all managed devices.
    /// Provides aggregated memory usage information and per-device statistics.
    /// </summary>
    public sealed class ExecutionMemoryStatistics
    {
        /// <summary>
        /// Gets or sets the per-device memory statistics.
        /// The dictionary key is the device ID, and the value contains detailed statistics for that device.
        /// </summary>
        public Dictionary<string, DeviceMemoryStatistics> DeviceStatistics { get; init; } = [];

        /// <summary>
        /// Gets or sets the total number of bytes allocated across all devices.
        /// </summary>
        public long TotalAllocatedBytes { get; init; }

        /// <summary>
        /// Gets or sets the total number of bytes available for allocation across all devices.
        /// </summary>
        public long TotalAvailableBytes { get; init; }

        /// <summary>
        /// Gets or sets the number of active executions currently using allocated buffers.
        /// </summary>
        public int ActiveExecutions { get; init; }

        /// <summary>
        /// Gets the total memory utilization percentage across all devices.
        /// Calculated as the ratio of allocated bytes to total memory (allocated + available).
        /// </summary>
        public double UtilizationPercentage
        {
            get
            {
                var total = TotalAllocatedBytes + TotalAvailableBytes;
                return total > 0 ? (TotalAllocatedBytes * 100.0) / total : 0;
            }
        }
    }
}
