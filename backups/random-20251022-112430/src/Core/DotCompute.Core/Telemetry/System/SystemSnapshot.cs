// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.System;

/// <summary>
/// Represents a snapshot of system performance metrics at a specific point in time.
/// Captures system-wide resource utilization for profiling analysis.
/// </summary>
public sealed class SystemSnapshot
{
    /// <summary>
    /// Gets or sets the timestamp when this snapshot was captured.
    /// Provides the exact moment when these system metrics were recorded.
    /// </summary>
    /// <value>The timestamp as a DateTimeOffset.</value>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the CPU usage percentage at the time of the snapshot.
    /// Indicates how much of the system's CPU capacity was being utilized.
    /// </summary>
    /// <value>The CPU usage as a decimal (0.0 to 1.0).</value>
    public double CpuUsage { get; set; }

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
}