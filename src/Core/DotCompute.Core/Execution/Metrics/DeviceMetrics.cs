// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
namespace DotCompute.Core.Execution.Metrics
{
    /// <summary>
    /// Represents performance metrics for a specific compute device in execution contexts.
    /// </summary>
    /// <remarks>
    /// This class tracks execution-specific performance metrics for individual compute devices,
    /// including throughput, bandwidth, utilization, and execution statistics.
    /// It provides device-specific insights for load balancing, performance optimization,
    /// and resource allocation decisions in parallel execution scenarios.
    /// </remarks>
    public class DeviceMetrics
    {
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>
        /// A string that uniquely identifies the compute device (e.g., "GPU0", "CPU", "TPU1").
        /// This identifier correlates metrics with specific hardware devices.
        /// </value>
        public required string DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the total number of executions on this device.
        /// </summary>
        /// <value>
        /// The total count of execution operations performed on this device.
        /// This includes both successful and failed executions.
        /// </value>
        public int TotalExecutions { get; set; }

        /// <summary>
        /// Gets or sets the average execution time on this device.
        /// </summary>
        /// <value>
        /// The mean execution time for operations on this device, measured in milliseconds.
        /// This metric indicates the typical performance characteristics of the device.
        /// </value>
        public double AverageExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the average throughput achieved on this device.
        /// </summary>
        /// <value>
        /// The mean computational throughput achieved on this device, measured in gigaFLOPS.
        /// This metric indicates the computational efficiency and capabilities of the device.
        /// </value>
        public double AverageThroughputGFLOPS { get; set; }

        /// <summary>
        /// Gets or sets the average memory bandwidth achieved on this device.
        /// </summary>
        /// <value>
        /// The mean memory bandwidth achieved during executions, measured in gigabytes per second.
        /// This metric indicates how effectively the device utilizes its memory subsystem.
        /// </value>
        public double AverageMemoryBandwidthGBps { get; set; }

        /// <summary>
        /// Gets or sets the utilization percentage of this device.
        /// </summary>
        /// <value>
        /// The percentage of time this device is actively used for computation (0-100).
        /// Higher values indicate better resource utilization and workload distribution.
        /// </value>
        public double UtilizationPercentage { get; set; }

        private int _successfulExecutions;
        private double _totalElementsProcessed;

        /// <summary>
        /// Gets the number of successful executions.
        /// </summary>
        /// <value>
        /// The count of executions that completed successfully on this device.
        /// </value>
        public int SuccessfulExecutions => _successfulExecutions;

        /// <summary>
        /// Gets the number of failed executions.
        /// </summary>
        /// <value>
        /// The count of executions that failed on this device.
        /// </value>
        public int FailedExecutions => TotalExecutions - _successfulExecutions;

        /// <summary>
        /// Gets the success rate percentage.
        /// </summary>
        /// <value>
        /// The percentage of successful executions on this device (0-100).
        /// </value>
        public double SuccessRatePercentage => TotalExecutions > 0 ? ((double)_successfulExecutions / TotalExecutions) * 100.0 : 0.0;

        /// <summary>
        /// Gets the total elements processed by this device.
        /// </summary>
        /// <value>
        /// The cumulative number of data elements processed across all executions.
        /// </value>
        public double TotalElementsProcessed => _totalElementsProcessed;

        /// <summary>
        /// Gets the average elements processed per execution.
        /// </summary>
        /// <value>
        /// The mean number of elements processed per execution on this device.
        /// </value>
        public double AverageElementsPerExecution => TotalExecutions > 0 ? _totalElementsProcessed / TotalExecutions : 0.0;

        /// <summary>
        /// Gets a value indicating whether this device is performing well.
        /// </summary>
        /// <value>
        /// <c>true</c> if the device has good throughput, bandwidth, and success rate; otherwise, <c>false</c>.
        /// </value>
        public bool IsPerformingWell
            => AverageThroughputGFLOPS > 1.0 && 
            AverageMemoryBandwidthGBps > 10.0 && 
            SuccessRatePercentage > 95.0;

        /// <summary>
        /// Gets a value indicating whether this device is underutilized.
        /// </summary>
        /// <value>
        /// <c>true</c> if the utilization percentage is below 50%; otherwise, <c>false</c>.
        /// </value>
        public bool IsUnderutilized => UtilizationPercentage < 50.0;

        /// <summary>
        /// Gets a value indicating whether this device is overutilized.
        /// </summary>
        /// <value>
        /// <c>true</c> if the utilization percentage is above 90%; otherwise, <c>false</c>.
        /// </value>
        public bool IsOverutilized => UtilizationPercentage > 90.0;

        /// <summary>
        /// Adds the results of a device execution to these metrics.
        /// </summary>
        /// <param name="result">The device execution result to incorporate.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is <c>null</c>.</exception>
        public void AddExecution(DeviceExecutionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            var previousCount = TotalExecutions;
            TotalExecutions++;

            // Update average execution time
            var totalTime = (AverageExecutionTimeMs * previousCount) + result.ExecutionTimeMs;
            AverageExecutionTimeMs = totalTime / TotalExecutions;

            // Update average throughput
            var totalThroughput = (AverageThroughputGFLOPS * previousCount) + result.ThroughputGFLOPS;
            AverageThroughputGFLOPS = totalThroughput / TotalExecutions;

            // Update average memory bandwidth
            var totalBandwidth = (AverageMemoryBandwidthGBps * previousCount) + result.MemoryBandwidthGBps;
            AverageMemoryBandwidthGBps = totalBandwidth / TotalExecutions;

            // Update success count
            if (result.Success)
            {
                _successfulExecutions++;
            }

            // Update total elements processed
            _totalElementsProcessed += result.ElementsProcessed;
        }

        /// <summary>
        /// Updates the utilization percentage based on current workload.
        /// </summary>
        /// <param name="utilizationPercentage">The current utilization percentage (0-100).</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="utilizationPercentage"/> is not between 0 and 100.
        /// </exception>
        public void UpdateUtilization(double utilizationPercentage)
        {
            if (utilizationPercentage is < 0.0 or > 100.0)
            {
                throw new ArgumentOutOfRangeException(nameof(utilizationPercentage), 
                    "Utilization percentage must be between 0 and 100.");
            }

            UtilizationPercentage = utilizationPercentage;
        }

        /// <summary>
        /// Resets all metrics to their initial state.
        /// </summary>
        public void Reset()
        {
            TotalExecutions = 0;
            AverageExecutionTimeMs = 0.0;
            AverageThroughputGFLOPS = 0.0;
            AverageMemoryBandwidthGBps = 0.0;
            UtilizationPercentage = 0.0;
            _successfulExecutions = 0;
            _totalElementsProcessed = 0.0;
        }

        /// <summary>
        /// Compares this device's performance with another device.
        /// </summary>
        /// <param name="other">The device metrics to compare against.</param>
        /// <returns>
        /// A positive value if this device performs better, negative if worse, or zero if equivalent.
        /// The comparison considers throughput, bandwidth, success rate, and execution time.
        /// </returns>
        public double CompareTo(DeviceMetrics other)
        {
            ArgumentNullException.ThrowIfNull(other);

            // Weighted comparison: throughput (30%), bandwidth (30%), reliability (25%), speed (15%)
            var throughputScore = (AverageThroughputGFLOPS - other.AverageThroughputGFLOPS) * 0.3;
            var bandwidthScore = (AverageMemoryBandwidthGBps - other.AverageMemoryBandwidthGBps) * 0.3;
            var reliabilityScore = (SuccessRatePercentage - other.SuccessRatePercentage) * 0.25;
            var speedScore = (other.AverageExecutionTimeMs - AverageExecutionTimeMs) / 
                           Math.Max(AverageExecutionTimeMs, other.AverageExecutionTimeMs) * 15.0;

            return throughputScore + bandwidthScore + reliabilityScore + speedScore;
        }

        /// <summary>
        /// Returns a string representation of the device metrics.
        /// </summary>
        /// <returns>
        /// A string containing device ID, execution count, performance metrics, and utilization.
        /// </returns>
        public override string ToString()
        {
            return $"Device {DeviceId}: {TotalExecutions} executions, {AverageExecutionTimeMs:F2}ms avg, " +
                   $"{AverageThroughputGFLOPS:F2} GFLOPS, {AverageMemoryBandwidthGBps:F2} GB/s, " +
                   $"{UtilizationPercentage:F1}% util, {SuccessRatePercentage:F1}% success";
        }
    }
}