// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Performance result for recording actual execution metrics.
/// </summary>
public class PerformanceResult
{
    public double ExecutionTimeMs { get; set; }
    public double ThroughputOpsPerSecond { get; set; }
    public long MemoryUsedBytes { get; set; }
    public double CpuUtilization { get; set; }
    public double GpuUtilization { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> AdditionalMetrics { get; set; } = [];

    /// <summary>
    /// Gets the elapsed time as a TimeSpan (computed from ExecutionTimeMs).
    /// </summary>
    public TimeSpan ElapsedTime => TimeSpan.FromMilliseconds(ExecutionTimeMs);
}