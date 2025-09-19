// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Telemetry.Options;

namespace DotCompute.Abstractions.Logging;

/// <summary>
/// Represents a structured log entry with metadata.
/// </summary>
public sealed class StructuredLogEntry
{
    /// <summary>
    /// Gets the timestamp of the log entry.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the log level.
    /// </summary>
    public required LogLevel LogLevel { get; init; }

    /// <summary>
    /// Gets the formatted message.
    /// </summary>
    public required string FormattedMessage { get; init; }

    /// <summary>
    /// Gets the log properties.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Properties { get; init; }

    /// <summary>
    /// Gets the correlation ID, if available.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the exception information, if available.
    /// </summary>
    public Exception? Exception { get; init; }
}

/// <summary>
/// Kernel performance metrics for logging.
/// </summary>
public sealed class KernelPerformanceMetrics
{
    /// <summary>
    /// Gets the throughput in operations per second.
    /// </summary>
    public required double ThroughputOpsPerSecond { get; init; }

    /// <summary>
    /// Gets the occupancy percentage.
    /// </summary>
    public required double OccupancyPercentage { get; init; }

    /// <summary>
    /// Gets the memory usage in bytes.
    /// </summary>
    public required long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets the cache hit rate.
    /// </summary>
    public required double CacheHitRate { get; init; }

    /// <summary>
    /// Gets the device utilization percentage.
    /// </summary>
    public required double DeviceUtilization { get; init; }
}

/// <summary>
/// Memory access metrics for logging.
/// </summary>
public sealed class MemoryAccessMetrics
{
    /// <summary>
    /// Gets the access pattern description.
    /// </summary>
    public required string AccessPattern { get; init; }

    /// <summary>
    /// Gets the coalescing efficiency.
    /// </summary>
    public required double CoalescingEfficiency { get; init; }

    /// <summary>
    /// Gets the cache hit rate.
    /// </summary>
    public required double CacheHitRate { get; init; }

    /// <summary>
    /// Gets the memory segment identifier.
    /// </summary>
    public required string MemorySegment { get; init; }

    /// <summary>
    /// Gets the transfer direction.
    /// </summary>
    public required string TransferDirection { get; init; }

    /// <summary>
    /// Gets the queue depth.
    /// </summary>
    public required int QueueDepth { get; init; }
}