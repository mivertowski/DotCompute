// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Statistics;

/// <summary>
/// Contains comprehensive memory usage statistics for pipeline execution analysis.
/// Provides detailed insights into memory allocation patterns and utilization.
/// </summary>
public sealed class MemoryUsageStats
{
    /// <summary>
    /// Gets or sets the peak memory usage observed during execution.
    /// Represents the maximum amount of memory used at any point.
    /// </summary>
    /// <value>The peak memory usage in bytes.</value>
    public long PeakUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage throughout the execution.
    /// Provides insight into the typical memory footprint.
    /// </summary>
    /// <value>The average memory usage in bytes.</value>
    public long AverageUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the initial memory usage at the start of execution.
    /// Used for calculating the net memory impact of the operation.
    /// </summary>
    /// <value>The initial memory usage in bytes.</value>
    public long InitialUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the final memory usage at the end of execution.
    /// Used for detecting memory leaks and cleanup efficiency.
    /// </summary>
    /// <value>The final memory usage in bytes.</value>
    public long FinalUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the total number of memory allocations performed.
    /// Indicates the allocation frequency and potential GC pressure.
    /// </summary>
    /// <value>The allocation count as a long integer.</value>
    public long AllocationCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of memory deallocations performed.
    /// Used for analyzing memory management patterns.
    /// </summary>
    /// <value>The deallocation count as a long integer.</value>
    public long DeallocationCount { get; set; }

    /// <summary>
    /// Gets or sets the total bytes allocated during execution.
    /// Represents the cumulative allocation activity.
    /// </summary>
    /// <value>The total allocated bytes as a long integer.</value>
    public long TotalAllocatedBytes { get; set; }

    /// <summary>
    /// Gets or sets the total bytes deallocated during execution.
    /// Used for calculating net memory usage and leak detection.
    /// </summary>
    /// <value>The total deallocated bytes as a long integer.</value>
    public long TotalDeallocatedBytes { get; set; }
}