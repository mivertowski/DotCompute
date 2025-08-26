// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// Performance metrics for kernel execution.
/// </summary>
public sealed class KernelPerformanceMetrics
{
    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the compilation time in milliseconds.
    /// </summary>
    public double CompilationTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the memory bandwidth in MB/s.
    /// </summary>
    public double MemoryBandwidthMBps { get; set; }

    /// <summary>
    /// Gets or sets the CPU utilization percentage.
    /// </summary>
    public double CpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the SIMD utilization percentage.
    /// </summary>
    public double SimdUtilization { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate percentage.
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the number of operations per second.
    /// </summary>
    public long OperationsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets additional metrics.
    /// </summary>
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}