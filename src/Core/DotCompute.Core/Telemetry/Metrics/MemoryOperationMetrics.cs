// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Metrics;

/// <summary>
/// Contains detailed performance metrics for memory operations.
/// Provides comprehensive data about memory transfer efficiency and access patterns.
/// </summary>
public sealed class MemoryOperationMetrics
{
    /// <summary>
    /// Gets or sets the timestamp when the memory operation started.
    /// Marks the beginning of the memory operation.
    /// </summary>
    /// <value>The start time as a DateTimeOffset.</value>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the duration of the memory operation.
    /// Represents the total time taken to complete the operation.
    /// </summary>
    /// <value>The operation duration as a TimeSpan.</value>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the number of bytes transferred during the operation.
    /// Indicates the size of the memory operation.
    /// </summary>
    /// <value>The bytes transferred as a long integer.</value>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets the memory bandwidth achieved in gigabytes per second.
    /// Measures the effective data transfer rate for this operation.
    /// </summary>
    /// <value>The bandwidth in GB/s.</value>
    public double BandwidthGBPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the memory access pattern observed during the operation.
    /// Describes how memory was accessed (e.g., "Sequential", "Random", "Strided").
    /// </summary>
    /// <value>The access pattern as a string.</value>
    public string AccessPattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the memory coalescing efficiency.
    /// Measures how well memory accesses were combined for optimal bandwidth utilization.
    /// </summary>
    /// <value>The coalescing efficiency as a decimal (0.0 to 1.0).</value>
    public double CoalescingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate during the memory operation.
    /// Measures the percentage of memory accesses that hit the cache.
    /// </summary>
    /// <value>The cache hit rate as a decimal (0.0 to 1.0).</value>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the memory segment where the operation occurred.
    /// Identifies the type of memory involved (e.g., "Global", "Local", "Shared").
    /// </summary>
    /// <value>The memory segment as a string.</value>
    public string MemorySegment { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the direction of the memory transfer.
    /// Indicates the flow of data (e.g., "HostToDevice", "DeviceToHost", "DeviceToDevice").
    /// </summary>
    /// <value>The transfer direction as a string.</value>
    public string TransferDirection { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the queue depth during the memory operation.
    /// Indicates the number of outstanding memory operations at the time.
    /// </summary>
    /// <value>The queue depth as an integer.</value>
    public int QueueDepth { get; set; }
}
