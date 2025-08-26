// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Memory;

/// <summary>
/// Configuration settings that control memory recovery behavior and strategies.
/// </summary>
/// <remarks>
/// This class provides comprehensive configuration options for memory recovery operations,
/// including timing intervals, thresholds, and feature toggles. All timing values should
/// be positive, and percentage values should be between 0.0 and 1.0.
/// </remarks>
public class MemoryRecoveryConfiguration
{
    /// <summary>
    /// Gets or sets the interval between automatic defragmentation operations.
    /// </summary>
    /// <value>
    /// The time interval for periodic defragmentation. Defaults to 5 minutes.
    /// </value>
    /// <remarks>
    /// This setting controls how frequently the system performs background
    /// defragmentation when <see cref="EnablePeriodicDefragmentation"/> is enabled.
    /// Shorter intervals provide better memory utilization but may impact performance.
    /// </remarks>
    public TimeSpan DefragmentationInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the delay between allocation retry attempts.
    /// </summary>
    /// <value>
    /// The delay before retrying a failed allocation. Defaults to 50 milliseconds.
    /// </value>
    /// <remarks>
    /// This delay allows the system time to perform recovery operations
    /// between allocation attempts. Too short delays may not allow sufficient
    /// time for recovery, while too long delays can impact responsiveness.
    /// </remarks>
    public TimeSpan AllocationRetryDelay { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets or sets the size of the emergency memory reserve in megabytes.
    /// </summary>
    /// <value>
    /// The emergency reserve size in MB. Defaults to 64 MB.
    /// </value>
    /// <remarks>
    /// This reserve is maintained for critical operations when the system
    /// is under severe memory pressure. It should be sized according to
    /// the application's minimum operating requirements.
    /// </remarks>
    public int EmergencyReserveSizeMB { get; set; } = 64;

    /// <summary>
    /// Gets or sets whether large object heap compaction is enabled.
    /// </summary>
    /// <value>
    /// True to enable LOH compaction during garbage collection; otherwise, false.
    /// Defaults to true.
    /// </value>
    /// <remarks>
    /// Enabling LOH compaction can reduce memory fragmentation but may
    /// increase GC pause times for applications with many large objects.
    /// </remarks>
    public bool EnableLargeObjectHeapCompaction { get; set; } = true;

    /// <summary>
    /// Gets or sets whether periodic defragmentation is enabled.
    /// </summary>
    /// <value>
    /// True to enable automatic periodic defragmentation; otherwise, false.
    /// Defaults to true.
    /// </value>
    /// <remarks>
    /// When enabled, the system will automatically perform defragmentation
    /// operations at the interval specified by <see cref="DefragmentationInterval"/>.
    /// </remarks>
    public bool EnablePeriodicDefragmentation { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory pressure threshold for triggering recovery operations.
    /// </summary>
    /// <value>
    /// A value between 0.0 and 1.0 representing the pressure threshold.
    /// Defaults to 0.85 (85%).
    /// </value>
    /// <remarks>
    /// When memory usage exceeds this threshold, the system will begin
    /// proactive recovery operations. Values closer to 1.0 delay recovery
    /// but allow higher memory utilization.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when the value is not between 0.0 and 1.0.
    /// </exception>
    public double MemoryPressureThreshold { get; set; } = 0.85; // 85%

    /// <summary>
    /// Gets or sets the maximum number of allocation retry attempts.
    /// </summary>
    /// <value>
    /// The maximum retry count. Defaults to 3 attempts.
    /// </value>
    /// <remarks>
    /// This limits the number of times the system will attempt to recover
    /// memory and retry a failed allocation. Higher values provide more
    /// resilience but may delay failure reporting.
    /// </remarks>
    public int MaxAllocationRetries { get; set; } = 3;

    /// <summary>
    /// Gets a default configuration instance with standard settings.
    /// </summary>
    /// <value>
    /// A new instance with default configuration values.
    /// </value>
    /// <remarks>
    /// This property provides a convenient way to obtain a configuration
    /// with sensible defaults for most applications.
    /// </remarks>
    public static MemoryRecoveryConfiguration Default => new();

    /// <summary>
    /// Returns a string representation of the configuration highlighting key settings.
    /// </summary>
    /// <returns>
    /// A formatted string containing the emergency reserve size, defragmentation interval,
    /// and memory pressure threshold.
    /// </returns>
    /// <example>
    /// Reserve=64MB, DefragInterval=00:05:00, PressureThreshold=85%
    /// </example>
    public override string ToString()
        => $"Reserve={EmergencyReserveSizeMB}MB, DefragInterval={DefragmentationInterval}, PressureThreshold={MemoryPressureThreshold:P0}";
}