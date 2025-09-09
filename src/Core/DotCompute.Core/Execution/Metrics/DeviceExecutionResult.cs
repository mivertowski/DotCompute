// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Metrics
{
    /// <summary>
    /// Represents the result of execution on a single device.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the execution results from a single compute device,
    /// including performance metrics, execution status, and error information.
    /// It provides detailed information about device-specific execution performance
    /// for analysis and optimization purposes.
    /// </remarks>
    public class DeviceExecutionResult
    {
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>
        /// A string that uniquely identifies the compute device (e.g., "GPU0", "CPU", "TPU1").
        /// This identifier is used to correlate results with specific hardware devices.
        /// </value>
        public required string DeviceId { get; set; }

        /// <summary>
        /// Gets or sets whether the execution was successful.
        /// </summary>
        /// <value>
        /// <c>true</c> if the execution completed successfully; otherwise, <c>false</c>.
        /// This indicates whether the kernel execution finished without errors.
        /// </value>
        public required bool Success { get; set; }

        /// <summary>
        /// Gets or sets the execution time in milliseconds.
        /// </summary>
        /// <value>
        /// The total time taken to execute the kernel on this device, measured in milliseconds.
        /// This includes kernel execution time but excludes data transfer overhead.
        /// </value>
        public double ExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the number of elements processed.
        /// </summary>
        /// <value>
        /// The total number of data elements processed by this device during execution.
        /// This is used to calculate throughput and efficiency metrics.
        /// </value>
        public int ElementsProcessed { get; set; }

        /// <summary>
        /// Gets or sets the memory bandwidth achieved in GB/s.
        /// </summary>
        /// <value>
        /// The effective memory bandwidth achieved during execution, measured in gigabytes per second.
        /// This metric indicates how efficiently the device utilized its memory subsystem.
        /// </value>
        public double MemoryBandwidthGBps { get; set; }

        /// <summary>
        /// Gets or sets the throughput achieved in GFLOPS.
        /// </summary>
        /// <value>
        /// The computational throughput achieved during execution, measured in gigaFLOPS (billion floating-point operations per second).
        /// This metric indicates the computational efficiency of the device.
        /// </value>
        public double ThroughputGFLOPS { get; set; }

        /// <summary>
        /// Gets or sets any error message.
        /// </summary>
        /// <value>
        /// A string containing error details if the execution failed; otherwise, <c>null</c>.
        /// This provides diagnostic information for troubleshooting execution failures.
        /// </value>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets a value indicating whether the device achieved good performance.
        /// </summary>
        /// <value>
        /// <c>true</c> if both memory bandwidth and throughput are above reasonable thresholds; otherwise, <c>false</c>.
        /// </value>
        public bool HasGoodPerformance => Success && MemoryBandwidthGBps > 10.0 && ThroughputGFLOPS > 1.0;

        /// <summary>
        /// Gets the elements processed per millisecond.
        /// </summary>
        /// <value>
        /// The processing rate as elements per millisecond, or 0 if execution time is zero.
        /// </value>
        public double ElementsPerMs => ExecutionTimeMs > 0 ? ElementsProcessed / ExecutionTimeMs : 0.0;

        /// <summary>
        /// Returns a string representation of the device execution result.
        /// </summary>
        /// <returns>
        /// A string containing the device ID, success status, execution time, and performance metrics.
        /// </returns>
        public override string ToString()
        {
            if (!Success)
            {
                return $"Device {DeviceId}: Failed - {ErrorMessage}";
            }


            return $"Device {DeviceId}: {ExecutionTimeMs:F2}ms, {ThroughputGFLOPS:F2} GFLOPS, {MemoryBandwidthGBps:F2} GB/s";
        }
    }
}