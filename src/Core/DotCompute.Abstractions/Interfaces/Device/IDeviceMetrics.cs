// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces.Device
{
    /// <summary>
    /// Provides comprehensive performance metrics and statistics for a compute device,
    /// including utilization, memory usage, thermal information, and execution statistics.
    /// </summary>
    public interface IDeviceMetrics
    {
        /// <summary>
        /// Gets the current utilization percentage of the device.
        /// </summary>
        /// <value>A value between 0.0 and 1.0 representing the utilization percentage.</value>
        /// <remarks>
        /// This represents the overall computational load on the device,
        /// where 0.0 indicates idle and 1.0 indicates fully utilized.
        /// </remarks>
        public double Utilization { get; }

        /// <summary>
        /// Gets the current memory usage percentage of the device.
        /// </summary>
        /// <value>A value between 0.0 and 1.0 representing the memory usage percentage.</value>
        /// <remarks>
        /// This represents the proportion of device memory currently allocated,
        /// where 0.0 indicates no memory used and 1.0 indicates memory fully allocated.
        /// </remarks>
        public double MemoryUsage { get; }

        /// <summary>
        /// Gets the current operating temperature of the device.
        /// </summary>
        /// <value>The temperature in Celsius, or null if temperature monitoring is not available.</value>
        /// <remarks>
        /// Temperature monitoring may not be available on all device types or platforms.
        /// </remarks>
        public double? Temperature { get; }

        /// <summary>
        /// Gets the current power consumption of the device.
        /// </summary>
        /// <value>The power consumption in watts, or null if power monitoring is not available.</value>
        /// <remarks>
        /// Power monitoring may not be available on all device types or platforms.
        /// </remarks>
        public double? PowerConsumption { get; }

        /// <summary>
        /// Gets the total number of kernels executed on this device since initialization.
        /// </summary>
        /// <value>The cumulative count of kernel executions.</value>
        public long KernelExecutionCount { get; }

        /// <summary>
        /// Gets the total time spent executing kernels on this device.
        /// </summary>
        /// <value>The cumulative compute time across all kernel executions.</value>
        /// <remarks>
        /// This includes only active computation time and excludes idle periods.
        /// </remarks>
        public TimeSpan TotalComputeTime { get; }

        /// <summary>
        /// Gets the average execution time per kernel on this device.
        /// </summary>
        /// <value>The mean kernel execution time.</value>
        /// <remarks>
        /// This is calculated as TotalComputeTime divided by KernelExecutionCount.
        /// Returns TimeSpan.Zero if no kernels have been executed.
        /// </remarks>
        public TimeSpan AverageKernelTime { get; }

        /// <summary>
        /// Gets detailed statistics about memory transfer operations.
        /// </summary>
        /// <value>An object providing access to memory transfer metrics.</value>
        public IMemoryTransferStats TransferStats { get; }
    }
}
