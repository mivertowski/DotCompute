// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging.Types;

/// <summary>
/// Comprehensive resource utilization report for kernel execution.
/// Analyzes CPU, memory, GPU, and other resource consumption patterns.
/// </summary>
public sealed class ResourceUtilizationReport
{
    /// <summary>
    /// Gets or sets the kernel name analyzed.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the analysis time window.
    /// </summary>
    public TimeSpan AnalysisTimeWindow { get; set; }

    /// <summary>
    /// Gets or sets CPU utilization statistics.
    /// </summary>
    public CpuUtilizationStats CpuUtilization { get; set; } = new();

    /// <summary>
    /// Gets or sets memory utilization statistics.
    /// </summary>
    public MemoryUtilizationStats MemoryUtilization { get; set; } = new();

    /// <summary>
    /// Gets or sets GPU utilization statistics (if applicable).
    /// </summary>
    public GpuUtilizationStats? GpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets I/O utilization statistics.
    /// </summary>
    public IoUtilizationStats IoUtilization { get; set; } = new();

    /// <summary>
    /// Gets or sets the overall resource efficiency score (0-100).
    /// Composite score based on all resource utilization metrics.
    /// </summary>
    public double OverallEfficiencyScore { get; set; }

    /// <summary>
    /// Gets or sets identified resource bottlenecks.
    /// </summary>
    public IList<string> ResourceBottlenecks { get; init; } = [];

    /// <summary>
    /// Gets or sets resource optimization recommendations.
    /// </summary>
    public IList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets or sets the number of execution samples analyzed.
    /// </summary>
    public int SampleCount { get; set; }

    /// <summary>
    /// Gets or sets additional resource metrics.
    /// </summary>
    public Dictionary<string, double> AdditionalMetrics { get; init; } = [];
}

/// <summary>
/// CPU utilization statistics.
/// </summary>
public sealed class CpuUtilizationStats
{
    /// <summary>
    /// Gets or sets the average CPU utilization percentage (0-100).
    /// </summary>
    public double AverageCpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the peak CPU utilization percentage (0-100).
    /// </summary>
    public double PeakCpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the minimum CPU utilization percentage (0-100).
    /// </summary>
    public double MinCpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the average CPU time per execution.
    /// </summary>
    public TimeSpan AverageCpuTime { get; set; }

    /// <summary>
    /// Gets or sets the number of CPU cores utilized.
    /// </summary>
    public int CoresUtilized { get; set; }

    /// <summary>
    /// Gets or sets the parallel efficiency percentage (0-100).
    /// </summary>
    public double ParallelEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the average thread count.
    /// </summary>
    public double AverageThreadCount { get; set; }
}

/// <summary>
/// Memory utilization statistics.
/// </summary>
public sealed class MemoryUtilizationStats
{
    /// <summary>
    /// Gets or sets the average memory usage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the minimum memory usage in bytes.
    /// </summary>
    public long MinMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the average memory bandwidth in GB/s.
    /// </summary>
    public double AverageMemoryBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the peak memory bandwidth in GB/s.
    /// </summary>
    public double PeakMemoryBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the memory allocation efficiency (0-1).
    /// </summary>
    public double AllocationEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate percentage (0-100).
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the average garbage collection count.
    /// </summary>
    public double AverageGCCollections { get; set; }
}

/// <summary>
/// GPU utilization statistics.
/// </summary>
public sealed class GpuUtilizationStats
{
    /// <summary>
    /// Gets or sets the average GPU utilization percentage (0-100).
    /// </summary>
    public double AverageGpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the peak GPU utilization percentage (0-100).
    /// </summary>
    public double PeakGpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the average GPU memory usage in bytes.
    /// </summary>
    public long AverageGpuMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the peak GPU memory usage in bytes.
    /// </summary>
    public long PeakGpuMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the average GPU memory bandwidth in GB/s.
    /// </summary>
    public double AverageGpuMemoryBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the GPU occupancy percentage (0-100).
    /// </summary>
    public double OccupancyPercentage { get; set; }

    /// <summary>
    /// Gets or sets the warp efficiency percentage (0-100).
    /// </summary>
    public double WarpEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the SM (streaming multiprocessor) utilization percentage (0-100).
    /// </summary>
    public double SmUtilization { get; set; }
}

/// <summary>
/// I/O utilization statistics.
/// </summary>
public sealed class IoUtilizationStats
{
    /// <summary>
    /// Gets or sets the average I/O bandwidth in MB/s.
    /// </summary>
    public double AverageIoBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the peak I/O bandwidth in MB/s.
    /// </summary>
    public double PeakIoBandwidth { get; set; }

    /// <summary>
    /// Gets or sets the average I/O wait time.
    /// </summary>
    public TimeSpan AverageIoWaitTime { get; set; }

    /// <summary>
    /// Gets or sets the total bytes read.
    /// </summary>
    public long TotalBytesRead { get; set; }

    /// <summary>
    /// Gets or sets the total bytes written.
    /// </summary>
    public long TotalBytesWritten { get; set; }

    /// <summary>
    /// Gets or sets the I/O operations per second.
    /// </summary>
    public double IopsPerSecond { get; set; }
}
