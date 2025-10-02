// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Performance;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Trace of kernel execution showing intermediate values.
/// </summary>
public class KernelExecutionTrace
{
    public string KernelName { get; init; } = string.Empty;
    public string BackendType { get; init; } = string.Empty;
    public List<TracePoint> TracePoints { get; init; } = [];
    public TimeSpan TotalExecutionTime { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Execution result if successful.
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Memory profiling information.
    /// </summary>
    public MemoryProfile? MemoryProfile { get; init; }

    /// <summary>
    /// Performance metrics for this execution.
    /// </summary>
    public PerformanceMetrics? PerformanceMetrics { get; init; }
}

/// <summary>
/// A specific point in kernel execution trace.
/// </summary>
public class TracePoint
{
    public string Name { get; init; } = string.Empty;
    public int ExecutionOrder { get; init; }
    public Dictionary<string, object> Values { get; init; } = [];
    public TimeSpan TimestampFromStart { get; init; }

    /// <summary>
    /// Timestamp when this trace point was captured.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Memory usage at this trace point in bytes.
    /// </summary>
    public long MemoryUsage { get; init; }

    /// <summary>
    /// Additional data for this trace point.
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = [];
}

/// <summary>
/// Memory profiling information.
/// </summary>
public class MemoryProfile
{
    /// <summary>
    /// Peak memory usage during execution in bytes.
    /// </summary>
    public long PeakMemory { get; init; }

    /// <summary>
    /// Memory allocations during execution.
    /// </summary>
    public long Allocations { get; init; }

    /// <summary>
    /// Memory efficiency score (0-1).
    /// </summary>
    public float Efficiency { get; init; }
}
