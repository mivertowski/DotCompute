// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Metrics;

/// <summary>
/// Contains detailed performance metrics for kernel execution operations.
/// Provides comprehensive data about kernel efficiency and resource utilization.
/// </summary>
public sealed class KernelExecutionMetrics
{
    /// <summary>
    /// Gets or sets the timestamp when the kernel execution started.
    /// Marks the beginning of the kernel operation.
    /// </summary>
    /// <value>The start time as a DateTimeOffset.</value>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the kernel execution completed.
    /// Marks the end of the kernel operation.
    /// </summary>
    /// <value>The end time as a DateTimeOffset.</value>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Gets or sets the total execution time for the kernel.
    /// Calculated as the difference between end time and start time.
    /// </summary>
    /// <value>The execution time as a TimeSpan.</value>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the throughput in operations per second.
    /// Measures the rate at which the kernel processed work items.
    /// </summary>
    /// <value>The throughput in operations per second.</value>
    public double ThroughputOpsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the occupancy percentage of the compute device.
    /// Indicates how effectively the device's parallel processing units were utilized.
    /// </summary>
    /// <value>The occupancy percentage as a decimal (0.0 to 1.0).</value>
    public double OccupancyPercentage { get; set; }

    /// <summary>
    /// Gets or sets the instruction throughput.
    /// Measures the rate at which instructions were executed by the kernel.
    /// </summary>
    /// <value>The instruction throughput in instructions per second.</value>
    public double InstructionThroughput { get; set; }

    /// <summary>
    /// Gets or sets the memory bandwidth utilization in gigabytes per second.
    /// Measures the rate of data transfer between the kernel and memory.
    /// </summary>
    /// <value>The memory bandwidth in GB/s.</value>
    public double MemoryBandwidthGBPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate.
    /// Measures the percentage of memory accesses that hit the cache.
    /// </summary>
    /// <value>The cache hit rate as a decimal (0.0 to 1.0).</value>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the memory coalescing efficiency.
    /// Measures how well memory accesses were combined for optimal bandwidth utilization.
    /// </summary>
    /// <value>The memory coalescing efficiency as a decimal (0.0 to 1.0).</value>
    public double MemoryCoalescingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the number of compute units used during execution.
    /// Indicates the parallel processing resources utilized by the kernel.
    /// </summary>
    /// <value>The number of compute units used.</value>
    public int ComputeUnitsUsed { get; set; }

    /// <summary>
    /// Gets or sets the number of registers used per thread.
    /// Affects occupancy and the number of threads that can run concurrently.
    /// </summary>
    /// <value>The number of registers per thread.</value>
    public int RegistersPerThread { get; set; }

    /// <summary>
    /// Gets or sets the amount of shared memory used in bytes.
    /// Shared memory usage affects occupancy and kernel performance.
    /// </summary>
    /// <value>The shared memory usage in bytes.</value>
    public long SharedMemoryUsed { get; set; }

    /// <summary>
    /// Gets or sets the warp efficiency percentage.
    /// Measures how effectively SIMD execution units were utilized (GPU-specific).
    /// </summary>
    /// <value>The warp efficiency as a decimal (0.0 to 1.0).</value>
    public double WarpEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the branch divergence percentage.
    /// Measures the impact of conditional branches on SIMD efficiency.
    /// </summary>
    /// <value>The branch divergence as a decimal (0.0 to 1.0).</value>
    public double BranchDivergence { get; set; }

    /// <summary>
    /// Gets or sets the memory latency in milliseconds.
    /// Measures the average time for memory operations to complete.
    /// </summary>
    /// <value>The memory latency in milliseconds.</value>
    public double MemoryLatency { get; set; }

    /// <summary>
    /// Gets or sets the power consumption in watts during kernel execution.
    /// Provides insights into energy efficiency of the computation.
    /// </summary>
    /// <value>The power consumption in watts.</value>
    public double PowerConsumption { get; set; }
}
