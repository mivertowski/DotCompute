// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace DotCompute.Abstractions.Observability;

/// <summary>
/// Defines health check capabilities for Ring Kernels.
/// </summary>
/// <remarks>
/// Integrates with Microsoft.Extensions.Diagnostics.HealthChecks
/// for standardized health monitoring across the DotCompute ecosystem.
/// </remarks>
public interface IRingKernelHealthCheck
{
    /// <summary>
    /// Performs a health check on all registered Ring Kernels.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result containing status and diagnostics.</returns>
    public Task<RingKernelHealthReport> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a health check on a specific Ring Kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result for the specified kernel.</returns>
    public Task<RingKernelHealthEntry> CheckKernelHealthAsync(
        string kernelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last known health status for a kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <returns>Cached health entry, or null if not available.</returns>
    public RingKernelHealthEntry? GetCachedHealth(string kernelId);

    /// <summary>
    /// Registers a kernel for health monitoring.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="options">Health check options for this kernel.</param>
    public void RegisterKernel(string kernelId, RingKernelHealthOptions? options = null);

    /// <summary>
    /// Unregisters a kernel from health monitoring.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    public void UnregisterKernel(string kernelId);

    /// <summary>
    /// Gets all registered kernel IDs.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredKernels { get; }

    /// <summary>
    /// Occurs when a kernel health status changes.
    /// </summary>
    public event EventHandler<RingKernelHealthChangedEventArgs>? HealthChanged;
}

/// <summary>
/// Overall health status for Ring Kernels.
/// </summary>
public enum RingKernelHealthStatus
{
    /// <summary>
    /// All kernels are healthy.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Some kernels have warnings but are operational.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// One or more kernels are unhealthy.
    /// </summary>
    Unhealthy = 2
}

/// <summary>
/// Health report for all Ring Kernels.
/// </summary>
public sealed class RingKernelHealthReport
{
    /// <summary>
    /// Gets the overall health status.
    /// </summary>
    public RingKernelHealthStatus Status { get; init; }

    /// <summary>
    /// Gets the total duration of the health check.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Gets individual kernel health entries.
    /// </summary>
    public IReadOnlyDictionary<string, RingKernelHealthEntry> Entries { get; init; }
        = new Dictionary<string, RingKernelHealthEntry>();

    /// <summary>
    /// Gets the timestamp when this report was generated.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the count of healthy kernels.
    /// </summary>
    public int HealthyCount => Entries.Count(e => e.Value.Status == RingKernelHealthStatus.Healthy);

    /// <summary>
    /// Gets the count of degraded kernels.
    /// </summary>
    public int DegradedCount => Entries.Count(e => e.Value.Status == RingKernelHealthStatus.Degraded);

    /// <summary>
    /// Gets the count of unhealthy kernels.
    /// </summary>
    public int UnhealthyCount => Entries.Count(e => e.Value.Status == RingKernelHealthStatus.Unhealthy);
}

/// <summary>
/// Health check entry for a single Ring Kernel.
/// </summary>
public sealed record RingKernelHealthEntry
{
    /// <summary>
    /// Gets the kernel identifier.
    /// </summary>
    public required string KernelId { get; init; }

    /// <summary>
    /// Gets the health status.
    /// </summary>
    public RingKernelHealthStatus Status { get; init; }

    /// <summary>
    /// Gets a description of the health status.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the duration of this health check.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets additional data about the kernel's health.
    /// </summary>
    public IReadOnlyDictionary<string, object> Data { get; init; }
        = new Dictionary<string, object>();

    /// <summary>
    /// Gets the exception if health check failed.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets diagnostic tags for the health entry.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the timestamp of this health check.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets whether the kernel is currently active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets the kernel uptime.
    /// </summary>
    public TimeSpan? Uptime { get; init; }

    /// <summary>
    /// Gets the last message processed timestamp.
    /// </summary>
    public DateTimeOffset? LastMessageProcessed { get; init; }

    /// <summary>
    /// Gets the current queue depth.
    /// </summary>
    public int? QueueDepth { get; init; }

    /// <summary>
    /// Gets the current throughput (messages per second).
    /// </summary>
    public double? Throughput { get; init; }

    /// <summary>
    /// Gets the average latency in nanoseconds.
    /// </summary>
    public ulong? AverageLatencyNanos { get; init; }

    /// <summary>
    /// Gets the error code if kernel has an error.
    /// </summary>
    public int? ErrorCode { get; init; }
}

/// <summary>
/// Options for Ring Kernel health checks.
/// </summary>
public sealed class RingKernelHealthOptions
{
    /// <summary>
    /// Gets or sets the timeout for health checks.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the threshold for considering a kernel stuck.
    /// </summary>
    public TimeSpan StuckThreshold { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the queue depth warning threshold (percentage).
    /// </summary>
    public double QueueDepthWarningThreshold { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets the queue depth critical threshold (percentage).
    /// </summary>
    public double QueueDepthCriticalThreshold { get; set; } = 0.95;

    /// <summary>
    /// Gets or sets the latency warning threshold in milliseconds.
    /// </summary>
    public double LatencyWarningThresholdMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets the latency critical threshold in milliseconds.
    /// </summary>
    public double LatencyCriticalThresholdMs { get; set; } = 500;

    /// <summary>
    /// Gets or sets the minimum throughput threshold (messages per second).
    /// </summary>
    public double MinimumThroughput { get; set; }

    /// <summary>
    /// Gets or sets whether to include detailed diagnostics.
    /// </summary>
    public bool IncludeDetailedDiagnostics { get; set; } = true;

    /// <summary>
    /// Gets or sets custom tags for health entries.
    /// </summary>
    public IList<string> Tags { get; } = [];

    /// <summary>
    /// Gets or sets failure threshold before reporting unhealthy.
    /// </summary>
    public int FailureThreshold { get; set; } = 1;

    /// <summary>
    /// Gets or sets the cache duration for health results.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Event arguments for health status changes.
/// </summary>
public sealed class RingKernelHealthChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the kernel identifier.
    /// </summary>
    public required string KernelId { get; init; }

    /// <summary>
    /// Gets the previous health status.
    /// </summary>
    public RingKernelHealthStatus PreviousStatus { get; init; }

    /// <summary>
    /// Gets the current health status.
    /// </summary>
    public RingKernelHealthStatus CurrentStatus { get; init; }

    /// <summary>
    /// Gets the health entry with details.
    /// </summary>
    public required RingKernelHealthEntry Entry { get; init; }

    /// <summary>
    /// Gets the timestamp of the change.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Factory for creating health check instances.
/// </summary>
public interface IRingKernelHealthCheckFactory
{
    /// <summary>
    /// Creates a health check for Ring Kernels.
    /// </summary>
    /// <param name="options">Global health check options.</param>
    /// <returns>A configured health check instance.</returns>
    public IRingKernelHealthCheck CreateHealthCheck(RingKernelHealthOptions? options = null);
}
