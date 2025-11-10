// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;

namespace DotCompute.Backends.CUDA.Profiling.Types;

/// <summary>
/// Internal configuration for CUDA performance profiling behavior.
/// </summary>
internal class ProfilingConfiguration
{
    public bool EnableKernelProfiling { get; set; } = true;
    public bool EnableMemoryProfiling { get; set; } = true;
    public bool EnableGpuMetrics { get; set; } = true;
    public bool EnableCuptiCallbacks { get; set; } = true;
    public int MetricsCollectionIntervalMs { get; set; } = 1000;
    public int MaxEventQueueSize { get; set; } = 10000;
    public bool AutoFlushReports { get; set; } = true;
    public string? ReportOutputDirectory { get; set; }
}

/// <summary>
/// Internal profile data for a CUDA kernel execution.
/// </summary>
internal class KernelProfile
{
    public required string KernelName { get; init; }
    public long TotalInvocations { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public TimeSpan MinExecutionTime { get; set; } = TimeSpan.MaxValue;
    public TimeSpan MaxExecutionTime { get; set; }
    public TimeSpan AverageExecutionTime => 
        TotalInvocations > 0 
            ? TimeSpan.FromTicks(TotalExecutionTime.Ticks / TotalInvocations) 
            : TimeSpan.Zero;
    public long TotalRegistersUsed { get; set; }
    public long SharedMemoryBytes { get; set; }
    public long LocalMemoryBytes { get; set; }
    public double OccupancyPercent { get; set; }
    public ConcurrentQueue<TimeSpan> RecentExecutionTimes { get; } = new();
    public DateTimeOffset FirstInvocation { get; set; }
    public DateTimeOffset LastInvocation { get; set; }
}

/// <summary>
/// Internal profile data for memory operations.
/// </summary>
internal class MemoryProfile
{
    public required string OperationName { get; init; }
    public MemoryTransferType TransferType { get; init; }
    public long TotalOperations { get; set; }
    public long TotalBytesTransferred { get; set; }
    public TimeSpan TotalTransferTime { get; set; }
    public TimeSpan MinTransferTime { get; set; } = TimeSpan.MaxValue;
    public TimeSpan MaxTransferTime { get; set; }
    public double AverageBandwidthGBps => 
        TotalTransferTime.TotalSeconds > 0
            ? (TotalBytesTransferred / 1_000_000_000.0) / TotalTransferTime.TotalSeconds
            : 0.0;
    public DateTimeOffset FirstOperation { get; set; }
    public DateTimeOffset LastOperation { get; set; }
}

/// <summary>
/// Internal GPU hardware metrics snapshot.
/// </summary>
internal class GpuMetrics
{
    public DateTimeOffset Timestamp { get; init; }
    public int GpuUtilizationPercent { get; set; }
    public int MemoryUtilizationPercent { get; set; }
    public long UsedMemoryBytes { get; set; }
    public long TotalMemoryBytes { get; set; }
    public int TemperatureCelsius { get; set; }
    public int PowerUsageWatts { get; set; }
    public int SmClockMhz { get; set; }
    public int MemoryClockMhz { get; set; }
    public int PcieThroughputMBps { get; set; }
}

/// <summary>
/// Internal comprehensive profiling report.
/// </summary>
internal class ProfilingReport
{
    public required string ReportId { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public TimeSpan ProfilingDuration { get; init; }
    public required Dictionary<string, KernelProfile> KernelProfiles { get; init; }
    public required Dictionary<string, MemoryProfile> MemoryProfiles { get; init; }
    public required List<GpuMetrics> GpuMetricsHistory { get; init; }
    public required MemoryTransferAnalysis TransferAnalysis { get; init; }
    public long TotalKernelInvocations { get; set; }
    public long TotalMemoryOperations { get; set; }
    public long TotalBytesTransferred { get; set; }
    public double AverageGpuUtilization { get; set; }
}

/// <summary>
/// Internal analysis of memory transfer patterns.
/// </summary>
internal class MemoryTransferAnalysis
{
    public required Dictionary<MemoryTransferType, TransferTypeStats> StatsByType { get; init; }
    public long TotalTransfers { get; set; }
    public long TotalBytesTransferred { get; set; }
    public TimeSpan TotalTransferTime { get; set; }
    public double OverallBandwidthGBps => 
        TotalTransferTime.TotalSeconds > 0
            ? (TotalBytesTransferred / 1_000_000_000.0) / TotalTransferTime.TotalSeconds
            : 0.0;
}

/// <summary>
/// Internal statistics for a specific transfer type.
/// </summary>
internal class TransferTypeStats
{
    public MemoryTransferType TransferType { get; init; }
    public long TransferCount { get; set; }
    public long TotalBytes { get; set; }
    public TimeSpan TotalTime { get; set; }
    public TimeSpan MinTime { get; set; } = TimeSpan.MaxValue;
    public TimeSpan MaxTime { get; set; }
    public TimeSpan AverageTime => 
        TransferCount > 0 
            ? TimeSpan.FromTicks(TotalTime.Ticks / TransferCount) 
            : TimeSpan.Zero;
    public double AverageBandwidthGBps => 
        TotalTime.TotalSeconds > 0
            ? (TotalBytes / 1_000_000_000.0) / TotalTime.TotalSeconds
            : 0.0;
}
