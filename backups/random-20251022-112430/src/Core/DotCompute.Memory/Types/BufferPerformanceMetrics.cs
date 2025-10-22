// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Memory.Types;

/// <summary>
/// Performance metrics for unified buffer operations.
/// </summary>
public record BufferPerformanceMetrics
{
    /// <summary>
    /// Gets or sets the transfer count.
    /// </summary>
    /// <value>The transfer count.</value>
    public long TransferCount { get; init; }
    /// <summary>
    /// Gets or sets the average transfer time.
    /// </summary>
    /// <value>The average transfer time.</value>
    public TimeSpan AverageTransferTime { get; init; }
    /// <summary>
    /// Gets or sets the last access time.
    /// </summary>
    /// <value>The last access time.</value>
    public DateTimeOffset LastAccessTime { get; init; }
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; init; }
    /// <summary>
    /// Gets or sets the allocation source.
    /// </summary>
    /// <value>The allocation source.</value>
    public string AllocationSource { get; init; } = "Unknown";
    /// <summary>
    /// Gets or sets the transfers per second.
    /// </summary>
    /// <value>The transfers per second.</value>
    public double TransfersPerSecond => TransferCount > 0 && AverageTransferTime > TimeSpan.Zero
        ? 1.0 / AverageTransferTime.TotalSeconds : 0;
}