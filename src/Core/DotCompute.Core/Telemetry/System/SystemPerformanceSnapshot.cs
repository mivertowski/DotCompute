// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.System;

/// <summary>
/// Represents a comprehensive snapshot of system performance metrics at a specific point in time.
/// Captures detailed system-wide resource utilization including hardware counters and garbage collection statistics.
/// </summary>
public sealed class SystemPerformanceSnapshot
{
    /// <summary>
    /// Gets or sets the timestamp when this snapshot was captured.
    /// Provides the exact moment when these system metrics were recorded.
    /// </summary>
    /// <value>The timestamp as a DateTimeOffset.</value>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the number of active profiling sessions at the time of the snapshot.
    /// Indicates the current profiling workload on the system.
    /// </summary>
    /// <value>The active profile count as an integer.</value>
    public int ActiveProfiles { get; set; }

    /// <summary>
    /// Gets or sets the CPU usage percentage at the time of the snapshot.
    /// Indicates how much of the system's CPU capacity was being utilized.
    /// </summary>
    /// <value>The CPU usage as a decimal (0.0 to 1.0).</value>
    public double ProcessorUsage { get; set; }

    /// <summary>
    /// Gets or sets the memory usage in bytes at the time of the snapshot.
    /// Indicates how much system memory was being consumed.
    /// </summary>
    /// <value>The memory usage in bytes.</value>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the number of active threads at the time of the snapshot.
    /// Provides insight into the system's concurrency level.
    /// </summary>
    /// <value>The thread count as an integer.</value>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Gets or sets the number of work items queued in the thread pool.
    /// Indicates the current workload and potential threading bottlenecks.
    /// </summary>
    /// <value>The thread pool work item count as an integer.</value>
    public int ThreadPoolWorkItems { get; set; }

    /// <summary>
    /// Gets or sets the number of Generation 0 garbage collections that have occurred.
    /// Provides insight into short-lived object allocation patterns.
    /// </summary>
    /// <value>The Generation 0 collection count as an integer.</value>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Gets or sets the number of Generation 1 garbage collections that have occurred.
    /// Provides insight into medium-lived object allocation patterns.
    /// </summary>
    /// <value>The Generation 1 collection count as an integer.</value>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Gets or sets the number of Generation 2 garbage collections that have occurred.
    /// Provides insight into long-lived object allocation patterns and potential memory pressure.
    /// </summary>
    /// <value>The Generation 2 collection count as an integer.</value>
    public int Gen2Collections { get; set; }

    /// <summary>
    /// Gets or sets the hardware performance counters captured at the time of the snapshot.
    /// Maps counter names to their values for detailed hardware performance analysis.
    /// </summary>
    /// <value>A dictionary mapping hardware counter names to their values.</value>
    public Dictionary<string, double> HardwareCounters { get; init; } = [];

    /// <summary>
    /// Gets or sets the CPU usage percentage (alias for ProcessorUsage).
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// Gets or sets the available memory in bytes.
    /// </summary>
    public long MemoryAvailable { get; set; }

    /// <summary>
    /// Gets or sets the GPU usage percentage.
    /// </summary>
    public double GpuUsage { get; set; }

    /// <summary>
    /// Gets or sets the number of active threads.
    /// </summary>
    public int ActiveThreads { get; set; }
}