// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Analysis;

/// <summary>
/// Contains statistical information about memory usage for a specific memory segment.
/// Provides insights into how different memory regions are being utilized.
/// </summary>
public sealed class MemorySegmentStats
{
    /// <summary>
    /// Gets or sets the total number of operations performed on this memory segment.
    /// Indicates the level of activity for this memory region.
    /// </summary>
    /// <value>The operation count as an integer.</value>
    public int OperationCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of bytes transferred to/from this memory segment.
    /// Indicates the volume of data processed in this memory region.
    /// </summary>
    /// <value>The total bytes as a long integer.</value>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets the average bandwidth achieved for operations on this memory segment.
    /// Provides insight into the performance characteristics of this memory region.
    /// </summary>
    /// <value>The average bandwidth in GB/s.</value>
    public double AverageBandwidth { get; set; }
}
