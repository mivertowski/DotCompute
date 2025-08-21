// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Device.Interfaces
{
    /// <summary>
    /// Provides comprehensive information about a device's memory hierarchy,
    /// including capacity, availability, bandwidth, and cache configurations.
    /// </summary>
    public interface IDeviceMemoryInfo
    {
        /// <summary>
        /// Gets the total amount of global memory available on the device.
        /// </summary>
        /// <value>The total global memory capacity in bytes.</value>
        public long TotalGlobalMemory { get; }

        /// <summary>
        /// Gets the amount of global memory currently available for allocation.
        /// </summary>
        /// <value>The available global memory in bytes.</value>
        public long AvailableGlobalMemory { get; }

        /// <summary>
        /// Gets the amount of local memory available per work group.
        /// </summary>
        /// <value>The local memory per work group in bytes.</value>
        public long LocalMemoryPerWorkGroup { get; }

        /// <summary>
        /// Gets the theoretical peak memory bandwidth of the device.
        /// </summary>
        /// <value>The memory bandwidth in gigabytes per second.</value>
        public double MemoryBandwidth { get; }

        /// <summary>
        /// Gets detailed information about the device's cache hierarchy.
        /// </summary>
        /// <value>An object providing access to cache size information.</value>
        public ICacheSizes CacheSizes { get; }

        /// <summary>
        /// Gets the minimum granularity for memory allocations.
        /// </summary>
        /// <value>The allocation granularity in bytes.</value>
        /// <remarks>
        /// Memory allocations are typically aligned to this boundary for optimal performance.
        /// </remarks>
        public long AllocationGranularity { get; }
    }
}