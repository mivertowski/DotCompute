// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Memory.Models;

/// <summary>
/// Memory usage analytics and statistics returned by
/// <see cref="CudaMemoryManager.GetUsageAnalytics"/>.
/// </summary>
public readonly record struct MemoryUsageAnalytics
{
    /// <summary>Gets the total number of allocation requests recorded.</summary>
    public long TotalAllocations { get; init; }

    /// <summary>Gets the total number of deallocations recorded.</summary>
    public long TotalDeallocations { get; init; }

    /// <summary>Gets the total number of bytes allocated across all requests.</summary>
    public long TotalBytesAllocated { get; init; }

    /// <summary>Gets the total number of bytes transferred (host-to-device, device-to-host, device-to-device).</summary>
    public long TotalBytesTransferred { get; init; }

    /// <summary>Gets the number of allocation requests that failed.</summary>
    public long FailedAllocations { get; init; }

    /// <summary>Gets the hit ratio for the optional memory pool in the range [0, 1].</summary>
    public double PoolHitRatio { get; init; }

    /// <summary>Gets the average allocation size in bytes.</summary>
    public double AverageAllocationSize { get; init; }

    /// <summary>Gets the peak memory usage observed in bytes.</summary>
    public long PeakMemoryUsage { get; init; }

    /// <summary>Gets the timestamp of the last memory optimization pass.</summary>
    public DateTimeOffset LastOptimizationTime { get; init; }
}

/// <summary>
/// Summary of a comprehensive memory cleanup run performed by
/// <see cref="CudaMemoryManager.PerformCleanupAsync(System.Threading.CancellationToken)"/>.
/// </summary>
public sealed class MemoryCleanupSummary
{
    /// <summary>Gets a value indicating whether the cleanup completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the UTC timestamp when the cleanup started.</summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>Gets the UTC timestamp when the cleanup finished.</summary>
    public DateTimeOffset EndTime { get; init; }

    /// <summary>Gets the number of bytes freed by the cleanup.</summary>
    public long MemoryFreed { get; init; }

    /// <summary>Gets the number of pooled items freed.</summary>
    public int PoolItemsFreed { get; init; }

    /// <summary>Gets the list of optimization phases applied during cleanup.</summary>
    public IReadOnlyList<string> OptimizationsPerformed { get; init; } = [];

    /// <summary>Gets the error message if <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the total duration of the cleanup.</summary>
    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// Aggregated policy configuration for memory management.
/// </summary>
public sealed class MemoryManagementPolicies
{
    /// <summary>Gets the pooling policy.</summary>
    public MemoryPoolingPolicy PoolingPolicy { get; init; } = new();

    /// <summary>Gets the tracking policy.</summary>
    public MemoryTrackingPolicy TrackingPolicy { get; init; } = new();
}

/// <summary>
/// Memory pooling policy configuration.
/// </summary>
public sealed class MemoryPoolingPolicy
{
    /// <summary>Gets a value indicating whether memory pooling is enabled.</summary>
    public bool EnablePooling { get; init; } = true;

    /// <summary>Gets the maximum number of pooled buffers retained.</summary>
    public int MaxPoolSize { get; init; } = 100;

    /// <summary>Gets the maximum idle time a buffer can remain pooled before being freed.</summary>
    public TimeSpan MaxIdleTime { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets the maximum size in bytes of an individual pooled buffer.</summary>
    public long MaxItemSize { get; init; } = 1024L * 1024L * 1024L;
}

/// <summary>
/// Memory tracking policy configuration.
/// </summary>
public sealed class MemoryTrackingPolicy
{
    /// <summary>Gets a value indicating whether detailed tracking is enabled.</summary>
    public bool EnableDetailedTracking { get; init; } = true;

    /// <summary>Gets a value indicating whether allocation stack traces are captured.</summary>
    public bool TrackStackTraces { get; init; }

    /// <summary>Gets the sliding window over which analytics are aggregated.</summary>
    public TimeSpan AnalyticsWindow { get; init; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Direction classification for memory transfer events.
/// </summary>
public enum MemoryTransferDirection
{
    /// <summary>Host-to-device transfer.</summary>
    HostToDevice,
    /// <summary>Device-to-host transfer.</summary>
    DeviceToHost,
    /// <summary>Device-to-device transfer (same or different devices).</summary>
    DeviceToDevice
}
