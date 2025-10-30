// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Metrics;

/// <summary>
/// Contains performance metrics for kernel execution on compute devices.
/// Provides detailed insights into kernel efficiency and resource utilization.
/// </summary>
public sealed class KernelPerformanceData
{
    /// <summary>
    /// Gets or sets the throughput in operations per second.
    /// Measures the rate at which the kernel processes work items.
    /// </summary>
    /// <value>The throughput in operations per second.</value>
    public double ThroughputOpsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the occupancy percentage of the compute device.
    /// Indicates how effectively the device's parallel processing units are utilized.
    /// </summary>
    /// <value>The occupancy percentage as a decimal (0.0 to 1.0).</value>
    public double OccupancyPercentage { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate.
    /// Measures the percentage of memory accesses that hit the cache.
    /// </summary>
    /// <value>The cache hit rate as a decimal (0.0 to 1.0).</value>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the instruction throughput.
    /// Measures the rate at which instructions are executed by the kernel.
    /// </summary>
    /// <value>The instruction throughput in instructions per second.</value>
    public double InstructionThroughput { get; set; }

    /// <summary>
    /// Gets or sets the memory bandwidth utilization in gigabytes per second.
    /// Measures the rate of data transfer between the kernel and memory.
    /// </summary>
    /// <value>The memory bandwidth in GB/s.</value>
    public double MemoryBandwidthGBPerSecond { get; set; }
}
