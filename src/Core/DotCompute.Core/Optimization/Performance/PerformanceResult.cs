// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Performance result for recording actual execution metrics.
/// </summary>
public class PerformanceResult
{
    /// <summary>
    /// Gets or sets the execution time ms.
    /// </summary>
    /// <value>The execution time ms.</value>
    public double ExecutionTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the throughput ops per second.
    /// </summary>
    /// <value>The throughput ops per second.</value>
    public double ThroughputOpsPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the memory used bytes.
    /// </summary>
    /// <value>The memory used bytes.</value>
    public long MemoryUsedBytes { get; set; }
    /// <summary>
    /// Gets or sets the cpu utilization.
    /// </summary>
    /// <value>The cpu utilization.</value>
    public double CpuUtilization { get; set; }
    /// <summary>
    /// Gets or sets the gpu utilization.
    /// </summary>
    /// <value>The gpu utilization.</value>
    public double GpuUtilization { get; set; }
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool Success { get; set; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets the additional metrics.
    /// </summary>
    /// <value>The additional metrics.</value>
    public Dictionary<string, object> AdditionalMetrics { get; } = [];

    /// <summary>
    /// Gets the elapsed time as a TimeSpan (computed from ExecutionTimeMs).
    /// </summary>
    public TimeSpan ElapsedTime => TimeSpan.FromMilliseconds(ExecutionTimeMs);
}