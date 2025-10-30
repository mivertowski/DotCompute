// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces.Device
{
    /// <summary>
    /// Provides detailed statistics about memory transfer operations between
    /// host and device memory, including transfer volumes, rates, and timing information.
    /// </summary>
    public interface IMemoryTransferStats
    {
        /// <summary>
        /// Gets the total number of bytes transferred from host to device memory.
        /// </summary>
        /// <value>The cumulative bytes transferred to the device.</value>
        /// <remarks>
        /// This includes all write operations and host-to-device copy operations
        /// performed since the device was initialized.
        /// </remarks>
        public long BytesToDevice { get; }

        /// <summary>
        /// Gets the total number of bytes transferred from device to host memory.
        /// </summary>
        /// <value>The cumulative bytes transferred from the device.</value>
        /// <remarks>
        /// This includes all read operations and device-to-host copy operations
        /// performed since the device was initialized.
        /// </remarks>
        public long BytesFromDevice { get; }

        /// <summary>
        /// Gets the average data transfer rate for host-to-device operations.
        /// </summary>
        /// <value>The average transfer rate in gigabytes per second.</value>
        /// <remarks>
        /// This rate is calculated across all host-to-device transfer operations.
        /// Returns 0.0 if no transfers have occurred.
        /// </remarks>
        public double AverageRateToDevice { get; }

        /// <summary>
        /// Gets the average data transfer rate for device-to-host operations.
        /// </summary>
        /// <value>The average transfer rate in gigabytes per second.</value>
        /// <remarks>
        /// This rate is calculated across all device-to-host transfer operations.
        /// Returns 0.0 if no transfers have occurred.
        /// </remarks>
        public double AverageRateFromDevice { get; }

        /// <summary>
        /// Gets the total time spent performing memory transfer operations.
        /// </summary>
        /// <value>The cumulative time spent on all memory transfers.</value>
        /// <remarks>
        /// This includes time for both host-to-device and device-to-host transfers,
        /// but excludes any overhead from operation setup or teardown.
        /// </remarks>
        public TimeSpan TotalTransferTime { get; }
    }
}
