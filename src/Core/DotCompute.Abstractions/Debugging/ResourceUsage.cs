// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Represents resource usage information during kernel execution.
/// </summary>
public sealed class ResourceUsage
{
    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; init; }

    /// <summary>
    /// Gets the total number of memory allocations.
    /// </summary>
    public long MemoryAllocationCount { get; init; }

    /// <summary>
    /// Gets the CPU time used in milliseconds.
    /// </summary>
    public double CpuTimeMs { get; init; }

    /// <summary>
    /// Gets the GPU time used in milliseconds (if applicable).
    /// </summary>
    public double GpuTimeMs { get; init; }

    /// <summary>
    /// Gets the number of threads used.
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Gets the CPU utilization percentage (0-100).
    /// </summary>
    public double CpuUtilization { get; init; }

    /// <summary>
    /// Gets the GPU utilization percentage (0-100).
    /// </summary>
    public double GpuUtilization { get; init; }

    /// <summary>
    /// Gets the memory bandwidth utilization in bytes per second.
    /// </summary>
    public long MemoryBandwidth { get; init; }

    /// <summary>
    /// Gets additional resource metrics.
    /// </summary>
    public Dictionary<string, object> AdditionalMetrics { get; init; } = [];
}