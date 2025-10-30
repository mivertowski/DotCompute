// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Statistics;

/// <summary>
/// Contains detailed performance statistics for kernel execution within a pipeline.
/// Provides comprehensive metrics for analyzing kernel performance and optimization opportunities.
/// </summary>
public sealed class KernelExecutionStats
{
    /// <summary>
    /// Gets the name of the kernel that was executed.
    /// Identifies the specific compute operation being measured.
    /// </summary>
    /// <value>The kernel name as a string.</value>
    public required string KernelName { get; init; }

    /// <summary>
    /// Gets the total execution time for the kernel.
    /// Represents the time from kernel launch to completion.
    /// </summary>
    /// <value>The execution time as a TimeSpan.</value>
    public required TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets the number of work items processed by the kernel.
    /// Indicates the scale of computation performed.
    /// </summary>
    /// <value>The work items processed as a long integer.</value>
    public required long WorkItemsProcessed { get; init; }

    /// <summary>
    /// Gets the number of bytes read from memory during kernel execution.
    /// Used for analyzing memory access patterns and bandwidth utilization.
    /// </summary>
    /// <value>The memory reads in bytes as a long integer.</value>
    public required long MemoryReadsBytes { get; init; }

    /// <summary>
    /// Gets the number of bytes written to memory during kernel execution.
    /// Used for analyzing memory access patterns and bandwidth utilization.
    /// </summary>
    /// <value>The memory writes in bytes as a long integer.</value>
    public required long MemoryWritesBytes { get; init; }

    /// <summary>
    /// Gets the compute utilization percentage during kernel execution.
    /// Indicates how effectively the compute resources were used (0.0 to 1.0).
    /// </summary>
    /// <value>The compute utilization as a double between 0.0 and 1.0.</value>
    public required double ComputeUtilization { get; init; }

    /// <summary>
    /// Gets the occupancy percentage of the compute device during execution.
    /// Measures how many execution units were active simultaneously (0.0 to 1.0).
    /// </summary>
    /// <value>The occupancy as a double between 0.0 and 1.0.</value>
    public required double Occupancy { get; init; }

    /// <summary>
    /// Gets the cache hit rate during kernel execution.
    /// Measures the effectiveness of memory caching (0.0 to 1.0).
    /// </summary>
    /// <value>The cache hit rate as a double between 0.0 and 1.0, or null if not available.</value>
    public double? CacheHitRate { get; init; }
}
