// <copyright file="MemoryRecoveryConfiguration.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Recovery.Memory.Configuration;

/// <summary>
/// Configuration for memory recovery behavior.
/// Controls how the system responds to memory pressure and allocation failures.
/// </summary>
public class MemoryRecoveryConfiguration
{
    /// <summary>
    /// Gets or sets the defragmentation interval.
    /// How often to perform automatic memory defragmentation.
    /// </summary>
    public TimeSpan DefragmentationInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the allocation retry delay.
    /// Time to wait between allocation retry attempts.
    /// </summary>
    public TimeSpan AllocationRetryDelay { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets or sets the emergency reserve size in MB.
    /// Memory kept in reserve for critical operations.
    /// </summary>
    public int EmergencyReserveSizeMB { get; set; } = 64;

    /// <summary>
    /// Gets or sets whether to enable large object heap compaction.
    /// Allows compaction of the LOH during garbage collection.
    /// </summary>
    public bool EnableLargeObjectHeapCompaction { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable periodic defragmentation.
    /// Automatically defragments memory at regular intervals.
    /// </summary>
    public bool EnablePeriodicDefragmentation { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory pressure threshold.
    /// Percentage of memory usage that triggers recovery actions (0.0 to 1.0).
    /// </summary>
    public double MemoryPressureThreshold { get; set; } = 0.85; // 85%

    /// <summary>
    /// Gets or sets the maximum allocation retries.
    /// Number of times to retry a failed allocation.
    /// </summary>
    public int MaxAllocationRetries { get; set; } = 3;

    /// <summary>
    /// Gets the default configuration.
    /// Standard settings for memory recovery.
    /// </summary>
    public static MemoryRecoveryConfiguration Default => new();

    /// <summary>
    /// Returns a string representation of the configuration.
    /// </summary>
    /// <returns>Configuration summary string.</returns>
    public override string ToString()
        => $"Reserve={EmergencyReserveSizeMB}MB, DefragInterval={DefragmentationInterval}, PressureThreshold={MemoryPressureThreshold:P0}";
}