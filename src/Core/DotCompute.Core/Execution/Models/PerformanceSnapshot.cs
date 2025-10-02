// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Models;

/// <summary>
/// Represents a point-in-time performance snapshot.
/// </summary>
public class PerformanceSnapshot
{
    /// <summary>
    /// Gets or sets the timestamp of this snapshot.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the execution ID this snapshot belongs to.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the total execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the throughput in GFLOPS.
    /// </summary>
    public double ThroughputGFLOPS { get; set; }

    /// <summary>
    /// Gets or sets the memory bandwidth in GB/s.
    /// </summary>
    public double MemoryBandwidthGBps { get; set; }

    /// <summary>
    /// Gets or sets the efficiency percentage.
    /// </summary>
    public double EfficiencyPercentage { get; set; }

    /// <summary>
    /// Gets or sets the device-specific metrics.
    /// </summary>
    public Dictionary<string, double> DeviceMetrics { get; } = [];

    /// <summary>
    /// Gets or sets additional performance counters.
    /// </summary>
    public Dictionary<string, long> Counters { get; } = [];

    /// <summary>
    /// Gets or sets memory usage statistics.
    /// </summary>
    public MemoryUsageSnapshot? MemoryUsage { get; set; }
}

/// <summary>
/// Represents memory usage at a point in time.
/// </summary>
public class MemoryUsageSnapshot
{
    /// <summary>
    /// Gets or sets total memory usage in bytes.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets peak memory usage in bytes.
    /// </summary>
    public long PeakBytes { get; set; }

    /// <summary>
    /// Gets or sets allocated memory in bytes.
    /// </summary>
    public long AllocatedBytes { get; set; }

    /// <summary>
    /// Gets or sets free memory in bytes.
    /// </summary>
    public long FreeBytes { get; set; }

    /// <summary>
    /// Gets or sets memory utilization percentage.
    /// </summary>
    public double UtilizationPercentage { get; set; }
}