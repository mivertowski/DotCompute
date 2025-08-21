// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Memory;

/// <summary>
/// Represents the result of a memory defragmentation operation, including
/// performance metrics and outcome information.
/// </summary>
/// <remarks>
/// This class provides detailed information about the success or failure
/// of memory defragmentation operations, including timing data and
/// the amount of memory recovered. The compression ratio can be used
/// to evaluate the effectiveness of the defragmentation process.
/// </remarks>
public class MemoryDefragmentationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the defragmentation operation succeeded.
    /// </summary>
    /// <value>
    /// True if the operation completed successfully; otherwise, false.
    /// </value>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the total time taken for the defragmentation operation.
    /// </summary>
    /// <value>
    /// The elapsed time for the operation.
    /// </value>
    /// <remarks>
    /// This includes the time spent on garbage collection, memory compaction,
    /// and any other cleanup activities performed during defragmentation.
    /// </remarks>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the amount of memory freed during the operation, in bytes.
    /// </summary>
    /// <value>
    /// The number of bytes freed by the defragmentation process.
    /// </value>
    /// <remarks>
    /// This represents the net reduction in memory usage achieved by
    /// the defragmentation operation.
    /// </remarks>
    public long MemoryFreed { get; set; }

    /// <summary>
    /// Gets or sets the memory usage before the defragmentation operation, in bytes.
    /// </summary>
    /// <value>
    /// The memory usage snapshot taken before the operation began.
    /// </value>
    public long MemoryBefore { get; set; }

    /// <summary>
    /// Gets or sets the memory usage after the defragmentation operation, in bytes.
    /// </summary>
    /// <value>
    /// The memory usage snapshot taken after the operation completed.
    /// </value>
    public long MemoryAfter { get; set; }

    /// <summary>
    /// Gets or sets the error message if the operation failed.
    /// </summary>
    /// <value>
    /// A description of the error that occurred, or null if the operation succeeded.
    /// </value>
    public string? Error { get; set; }

    /// <summary>
    /// Gets the compression ratio achieved by the defragmentation operation.
    /// </summary>
    /// <value>
    /// A value between 0.0 and 1.0 representing the fraction of memory freed
    /// relative to the initial memory usage.
    /// </value>
    /// <remarks>
    /// This calculated property provides a normalized measure of defragmentation
    /// effectiveness. A value of 0.2 indicates that 20% of the original memory
    /// was freed. Returns 0.0 if the initial memory usage was 0 or negative.
    /// </remarks>
    /// <example>
    /// If MemoryBefore was 1000 bytes and MemoryFreed was 200 bytes,
    /// the CompressionRatio would be 0.2 (20%).
    /// </example>
    public double CompressionRatio => MemoryBefore > 0 ? (double)MemoryFreed / MemoryBefore : 0.0;

    /// <summary>
    /// Returns a string representation of the defragmentation result.
    /// </summary>
    /// <returns>
    /// A formatted string describing the operation outcome, including memory freed,
    /// duration, and compression ratio for successful operations, or error details for failures.
    /// </returns>
    /// <example>
    /// Success: "Success: Freed 256MB in 150.5ms (ratio: 15.2%)"
    /// Failure: "Failed: Out of memory during compaction"
    /// </example>
    public override string ToString()
        => Success
            ? $"Success: Freed {MemoryFreed / 1024 / 1024}MB in {Duration.TotalMilliseconds}ms (ratio: {CompressionRatio:P1})"
            : $"Failed: {Error}";
}