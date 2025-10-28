// <copyright file="DeviceMemoryStatistics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Memory
{
    /// <summary>
    /// Memory statistics for a specific device's buffer pool.
    /// Contains detailed information about memory allocation, availability, and usage patterns.
    /// </summary>
    public sealed class DeviceMemoryStatistics
    {
        /// <summary>
        /// Gets or sets the unique identifier of the device these statistics represent.
        /// </summary>
        public required string DeviceId { get; init; }

        /// <summary>
        /// Gets or sets the total number of bytes allocated on this device.
        /// </summary>
        public required long AllocatedBytes { get; init; }

        /// <summary>
        /// Gets or sets the total number of bytes available for reuse in the buffer pool.
        /// </summary>
        public required long AvailableBytes { get; init; }

        /// <summary>
        /// Gets or sets the number of buffers currently pooled and available for reuse.
        /// </summary>
        public required int PooledBufferCount { get; init; }

        /// <summary>
        /// Gets or sets the distribution of allocation sizes on this device.
        /// The dictionary key represents the buffer size in bytes, and the value represents
        /// the number of allocations of that size.
        /// </summary>
        public required Dictionary<long, int> AllocationSizeDistribution { get; init; }

        /// <summary>
        /// Gets the memory utilization percentage for this device.
        /// Calculated as the ratio of allocated bytes to total memory (allocated + available).
        /// </summary>
        public double UtilizationPercentage
        {
            get
            {
                var total = AllocatedBytes + AvailableBytes;
                return total > 0 ? (AllocatedBytes * 100.0) / total : 0;
            }
        }
    }
}