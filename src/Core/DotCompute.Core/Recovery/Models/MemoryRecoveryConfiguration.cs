// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Configuration settings for memory recovery operations
/// </summary>
public class MemoryRecoveryConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of recovery attempts
    /// </summary>
    public int MaxRecoveryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the memory threshold percentage that triggers recovery
    /// </summary>
    public double MemoryThresholdPercentage { get; set; } = 85.0;

    /// <summary>
    /// Gets or sets the timeout for memory recovery operations
    /// </summary>
    public TimeSpan RecoveryTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether aggressive memory cleanup is enabled
    /// </summary>
    public bool EnableAggressiveCleanup { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay between recovery attempts
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the minimum free memory required after recovery (in bytes)
    /// </summary>
    public long MinimumFreeMemory { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Gets or sets a value indicating whether memory recovery monitoring is enabled
    /// </summary>
    public bool EnableMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the monitoring interval
    /// </summary>
    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the size of emergency memory reserve in MB
    /// </summary>
    public int EmergencyReserveSizeMB { get; set; } = 64;

    /// <summary>
    /// Gets or sets the defragmentation interval
    /// </summary>
    public TimeSpan DefragmentationInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether large object heap compaction is enabled
    /// </summary>
    public bool EnableLargeObjectHeapCompaction { get; set; } = true;

    /// <summary>
    /// Gets or sets the allocation retry delay
    /// </summary>
    public TimeSpan AllocationRetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the default configuration
    /// </summary>
    public static MemoryRecoveryConfiguration Default => new();
}

/// <summary>
/// Context information for memory recovery operations
/// </summary>
public class MemoryRecoveryContext
{
    /// <summary>
    /// Gets or sets the current memory usage (in bytes)
    /// </summary>
    public long CurrentMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the total available memory (in bytes)
    /// </summary>
    public long TotalAvailableMemory { get; set; }

    /// <summary>
    /// Gets or sets the memory usage percentage
    /// </summary>
    public double MemoryUsagePercentage { get; set; }

    /// <summary>
    /// Gets or sets the recovery attempt count
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the list of memory consumers to potentially clean up
    /// </summary>
    public List<MemoryConsumer> MemoryConsumers { get; set; } = new();

    /// <summary>
    /// Gets or sets additional context data
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when recovery was initiated
    /// </summary>
    public DateTimeOffset RecoveryStartTime { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents a memory consumer that can be cleaned up during recovery
/// </summary>
public class MemoryConsumer
{
    /// <summary>
    /// Gets or sets the name of the memory consumer
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the memory usage of this consumer (in bytes)
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the priority for cleanup (higher values are cleaned up first)
    /// </summary>
    public int CleanupPriority { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this consumer can be safely cleaned up
    /// </summary>
    public bool CanCleanup { get; set; } = true;

    /// <summary>
    /// Gets or sets the estimated memory that would be freed by cleaning up this consumer
    /// </summary>
    public long EstimatedRecoveredMemory { get; set; }
}