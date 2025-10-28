// <copyright file="AdvancedTransferResult.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;

namespace DotCompute.Memory.Types;

/// <summary>
/// Represents the result of an advanced memory transfer operation.
/// </summary>
/// <remarks>
/// This class provides detailed information about a memory transfer operation,
/// including performance metrics, optimization strategies used, and data integrity verification results.
/// </remarks>
public class AdvancedTransferResult
{
    /// <summary>
    /// Gets or sets the start time of the transfer operation.
    /// </summary>
    /// <value>The UTC timestamp when the transfer began.</value>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of the transfer operation.
    /// </summary>
    /// <value>The UTC timestamp when the transfer completed.</value>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Gets or sets the duration of the transfer operation.
    /// </summary>
    /// <value>The time taken to complete the transfer.</value>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the total number of bytes transferred.
    /// </summary>
    /// <value>The size of the data transferred in bytes.</value>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of chunks used in the transfer.
    /// </summary>
    /// <value>The number of chunks if the transfer was chunked, otherwise 1.</value>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether streaming was used.
    /// </summary>
    /// <value>True if streaming transfer was used; otherwise, false.</value>
    public bool UsedStreaming { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether compression was used.
    /// </summary>
    /// <value>True if data compression was applied; otherwise, false.</value>
    public bool UsedCompression { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether memory mapping was used.
    /// </summary>
    /// <value>True if memory-mapped files were used; otherwise, false.</value>
    public bool UsedMemoryMapping { get; set; }

    /// <summary>
    /// Gets or sets the compression ratio achieved.
    /// </summary>
    /// <value>The compression ratio (1.0 means no compression, >1.0 means data was compressed).</value>
    public double CompressionRatio { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the transfer throughput in bytes per second.
    /// </summary>
    /// <value>The average transfer rate in bytes per second.</value>
    public double ThroughputBytesPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the transfer throughput in megabytes per second.
    /// </summary>
    /// <value>The average transfer rate in megabytes per second.</value>
    public double ThroughputMBps { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether data integrity was verified.
    /// </summary>
    /// <value>True if data integrity verification was performed; otherwise, false.</value>
    public bool IntegrityVerified { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the integrity check passed.
    /// </summary>
    /// <value>True if the integrity verification passed; null if not performed.</value>
    public bool? IntegrityCheckPassed { get; set; }

    /// <summary>
    /// Gets or sets the resulting memory buffer from the transfer.
    /// </summary>
    /// <value>The memory buffer containing the transferred data, or null if the transfer failed.</value>
    public object? ResultBuffer { get; set; }

    /// <summary>
    /// Gets or sets the efficiency ratio of the transfer operation.
    /// </summary>
    /// <value>A ratio indicating the efficiency of the transfer (higher values indicate better efficiency).</value>
    public double EfficiencyRatio { get; set; }

    /// <summary>
    /// Gets or sets the index of the transfer in a batch operation.
    /// </summary>
    /// <value>The zero-based index of the transfer operation in a batch.</value>
    public int TransferIndex { get; set; }

    /// <summary>
    /// Gets or sets the transferred unified memory buffer.
    /// </summary>
    /// <value>The unified memory buffer that was transferred, or null if the transfer failed.</value>
    public IUnifiedMemoryBuffer? TransferredBuffer { get; set; }

    /// <summary>
    /// Gets or sets any error that occurred during the transfer.
    /// </summary>
    /// <value>The exception that occurred, or null if the transfer succeeded.</value>
    public Exception? Error { get; set; }

    /// <summary>
    /// Gets or sets any error message that occurred during the transfer.
    /// </summary>
    /// <value>The error message, or null if the transfer succeeded.</value>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the transfer succeeded.
    /// </summary>
    /// <value>True if the transfer completed without errors; otherwise, false.</value>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the memory pressure at the time of transfer.
    /// </summary>
    /// <value>A value between 0 and 1 indicating memory pressure (1 = maximum pressure).</value>
    public double MemoryPressure { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts made.
    /// </summary>
    /// <value>The number of times the transfer was retried due to failures.</value>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the transfer.
    /// </summary>
    /// <value>A dictionary containing additional transfer metadata.</value>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets a formatted summary of the transfer result.
    /// </summary>
    /// <returns>A human-readable summary of the transfer operation.</returns>
    public override string ToString()
    {
        var throughputMBps = ThroughputBytesPerSecond / (1024 * 1024);
        var sizeMB = TotalBytes / (1024.0 * 1024.0);


        return $"Transfer Result: {sizeMB:F2} MB in {Duration.TotalSeconds:F2}s " +
               $"({throughputMBps:F2} MB/s) - " +
               $"Chunks: {ChunkCount}, " +
               $"Streaming: {UsedStreaming}, " +
               $"Compression: {(UsedCompression ? $"{CompressionRatio:F2}x" : "No")}, " +
               $"Success: {Success}";
    }
}