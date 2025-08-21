// <copyright file="MemoryDefragmentationResult.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Recovery.Memory.Results;

/// <summary>
/// Result of memory defragmentation operation.
/// Contains metrics and outcome of a defragmentation attempt.
/// </summary>
public class MemoryDefragmentationResult
{
    /// <summary>
    /// Gets or sets whether the defragmentation was successful.
    /// True if defragmentation completed without errors.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the duration of the operation.
    /// Time taken to complete defragmentation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the memory freed in bytes.
    /// Amount of memory reclaimed by defragmentation.
    /// </summary>
    public long MemoryFreed { get; set; }

    /// <summary>
    /// Gets or sets the memory usage before defragmentation.
    /// Initial memory consumption in bytes.
    /// </summary>
    public long MemoryBefore { get; set; }

    /// <summary>
    /// Gets or sets the memory usage after defragmentation.
    /// Final memory consumption in bytes.
    /// </summary>
    public long MemoryAfter { get; set; }

    /// <summary>
    /// Gets or sets the error message if defragmentation failed.
    /// Description of what went wrong.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets the compression ratio achieved.
    /// Percentage of memory freed relative to initial usage.
    /// </summary>
    public double CompressionRatio => MemoryBefore > 0 ? (double)MemoryFreed / MemoryBefore : 0.0;

    /// <summary>
    /// Returns a string representation of the result.
    /// </summary>
    /// <returns>Summary of the defragmentation outcome.</returns>
    public override string ToString()
        => Success
            ? $"Success: Freed {MemoryFreed / 1024 / 1024}MB in {Duration.TotalMilliseconds}ms (ratio: {CompressionRatio:P1})"
            : $"Failed: {Error}";
}