// <copyright file="TransferOptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Memory.Types;

/// <summary>
/// Configuration options for memory transfer operations.
/// </summary>
/// <remarks>
/// This class provides comprehensive configuration for memory transfers,
/// including performance optimizations, data integrity options, and resource constraints.
/// </remarks>
public class TransferOptions
{
    /// <summary>
    /// Gets or sets the chunk size for streaming transfers.
    /// </summary>
    /// <value>The size of each chunk in bytes. Default is 64MB.</value>
    public int ChunkSize { get; set; } = 64 * 1024 * 1024; // 64MB default

    /// <summary>
    /// Gets or sets a value indicating whether to enable compression.
    /// </summary>
    /// <value>True to compress data during transfer; otherwise, false.</value>
    public bool EnableCompression { get; set; }

    /// <summary>
    /// Gets or sets the compression level when compression is enabled.
    /// </summary>
    /// <value>The compression level (0 = no compression, 9 = maximum compression). Default is 6.</value>
    public int CompressionLevel { get; set; } = 6;

    /// <summary>
    /// Gets or sets a value indicating whether to use memory mapping for large transfers.
    /// </summary>
    /// <value>True to use memory-mapped files for large datasets; otherwise, false.</value>
    public bool EnableMemoryMapping { get; set; }

    /// <summary>
    /// Gets or sets the threshold for using memory mapping.
    /// </summary>
    /// <value>The minimum size in bytes for which memory mapping is considered. Default is 256MB.</value>
    public long MemoryMappingThreshold { get; set; } = 256 * 1024 * 1024; // 256MB

    /// <summary>
    /// Gets or sets a value indicating whether to verify data integrity after transfer.
    /// </summary>
    /// <value>True to perform integrity verification; otherwise, false.</value>
    public bool VerifyIntegrity { get; set; }

    /// <summary>
    /// Gets or sets the sample rate for integrity verification.
    /// </summary>
    /// <value>The fraction of data to sample for verification (0.0 to 1.0). Default is 0.01 (1%).</value>
    public double IntegritySampleRate { get; set; } = 0.01; // Sample 1% by default

    /// <summary>
    /// Gets or sets the memory options for allocation.
    /// </summary>
    /// <value>The memory allocation options.</value>
    public MemoryOptions MemoryOptions { get; set; } = MemoryOptions.None;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed transfers.
    /// </summary>
    /// <value>The maximum number of retries. Default is 3.</value>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    /// <value>The time to wait between retries. Default is 1 second.</value>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets a value indicating whether to use exponential backoff for retries.
    /// </summary>
    /// <value>True to use exponential backoff; otherwise, false.</value>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for individual transfer operations.
    /// </summary>
    /// <value>The timeout duration. Default is 5 minutes.</value>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether to pin memory during transfer.
    /// </summary>
    /// <value>True to pin memory to prevent GC movement; otherwise, false.</value>
    public bool PinMemory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use asynchronous I/O.
    /// </summary>
    /// <value>True to use async I/O operations; otherwise, false. Default is true.</value>
    public bool UseAsyncIO { get; set; } = true;

    /// <summary>
    /// Gets or sets the buffer pool size for reusable buffers.
    /// </summary>
    /// <value>The maximum number of buffers to pool. Default is 10.</value>
    public int BufferPoolSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether to enable progress reporting.
    /// </summary>
    /// <value>True to report transfer progress; otherwise, false.</value>
    public bool EnableProgressReporting { get; set; }

    /// <summary>
    /// Gets or sets the progress reporting interval.
    /// </summary>
    /// <value>The interval between progress reports. Default is 100ms.</value>
    public TimeSpan ProgressReportInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the priority of the transfer operation.
    /// </summary>
    /// <value>The transfer priority (0 = lowest, 10 = highest). Default is 5.</value>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether to optimize for throughput.
    /// </summary>
    /// <value>True to optimize for maximum throughput; false to optimize for latency.</value>
    public bool OptimizeForThroughput { get; set; } = true;

    /// <summary>
    /// Gets a default set of transfer options optimized for general use.
    /// </summary>
    /// <value>Default transfer options.</value>
    public static TransferOptions Default => new();

    /// <summary>
    /// Gets transfer options optimized for small transfers.
    /// </summary>
    /// <value>Options optimized for transfers under 1MB.</value>
    public static TransferOptions SmallTransfer => new()
    {
        ChunkSize = 64 * 1024, // 64KB chunks
        EnableCompression = false,
        EnableMemoryMapping = false,
        PinMemory = true,
        OptimizeForThroughput = false
    };

    /// <summary>
    /// Gets transfer options optimized for large transfers.
    /// </summary>
    /// <value>Options optimized for transfers over 100MB.</value>
    public static TransferOptions LargeTransfer => new()
    {
        ChunkSize = 256 * 1024 * 1024, // 256MB chunks
        EnableCompression = true,
        EnableMemoryMapping = true,
        VerifyIntegrity = true,
        OptimizeForThroughput = true
    };

    /// <summary>
    /// Gets transfer options optimized for streaming.
    /// </summary>
    /// <value>Options optimized for continuous streaming transfers.</value>
    public static TransferOptions Streaming => new()
    {
        ChunkSize = 4 * 1024 * 1024, // 4MB chunks
        EnableCompression = false,
        UseAsyncIO = true,
        BufferPoolSize = 20,
        OptimizeForThroughput = true
    };

    /// <summary>
    /// Creates a copy of these transfer options.
    /// </summary>
    /// <returns>A new TransferOptions instance with the same values.</returns>
    public TransferOptions Clone()
    {
        return new TransferOptions
        {
            ChunkSize = ChunkSize,
            EnableCompression = EnableCompression,
            CompressionLevel = CompressionLevel,
            EnableMemoryMapping = EnableMemoryMapping,
            MemoryMappingThreshold = MemoryMappingThreshold,
            VerifyIntegrity = VerifyIntegrity,
            IntegritySampleRate = IntegritySampleRate,
            MemoryOptions = MemoryOptions,
            MaxRetryAttempts = MaxRetryAttempts,
            RetryDelay = RetryDelay,
            UseExponentialBackoff = UseExponentialBackoff,
            Timeout = Timeout,
            PinMemory = PinMemory,
            UseAsyncIO = UseAsyncIO,
            BufferPoolSize = BufferPoolSize,
            EnableProgressReporting = EnableProgressReporting,
            ProgressReportInterval = ProgressReportInterval,
            Priority = Priority,
            OptimizeForThroughput = OptimizeForThroughput
        };
    }
}
