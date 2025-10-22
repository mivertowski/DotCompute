// <copyright file="RuntimeCoalescingProfile.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Analysis.Types;

/// <summary>
/// Represents runtime coalescing profile data.
/// </summary>
public sealed class RuntimeCoalescingProfile
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string? KernelName { get; set; }

    /// <summary>
    /// Gets or sets the profile start time.
    /// </summary>
    public DateTimeOffset ProfileStartTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the minimum execution time.
    /// </summary>
    public TimeSpan MinExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the maximum execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the estimated bandwidth in bytes per second.
    /// </summary>
    public double EstimatedBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the estimated coalescing efficiency (0-1).
    /// </summary>
    public double EstimatedCoalescingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the memory throughput in GB/s.
    /// </summary>
    public double MemoryThroughputGBs { get; set; }

    /// <summary>
    /// Gets or sets the coalescing analysis results.
    /// </summary>
    public CoalescingAnalysis? CoalescingResults { get; set; }

    /// <summary>
    /// Gets or sets performance counters for detailed profiling metrics.
    /// </summary>
    public Dictionary<string, long> PerformanceCounters { get; } = [];

    /// <summary>
    /// Gets or sets the number of profiling runs executed.
    /// </summary>
    public int ProfileRuns { get; set; }

    /// <summary>
    /// Gets or sets the total bytes transferred during profiling.
    /// </summary>
    public long TotalBytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets the coalescing efficiency variance across runs.
    /// </summary>
    public double CoalescingVariance { get; set; }

    /// <summary>
    /// Gets or sets additional profiling metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];
}