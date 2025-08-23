// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.Execution.Graph.Statistics
{
    /// <summary>
    /// Provides comprehensive performance statistics and metrics for a CUDA graph.
    /// Aggregates execution data across all instances of a graph for performance analysis.
    /// </summary>
    /// <remarks>
    /// These statistics enable performance monitoring, optimization decisions, and resource
    /// management by providing insights into graph usage patterns and execution characteristics.
    /// </remarks>
    public sealed class CudaGraphStatistics
    {
        /// <summary>
        /// Gets or sets the identifier of the graph for which statistics are reported.
        /// </summary>
        /// <value>A string identifying the graph template.</value>
        public string GraphId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of active instances created from this graph.
        /// </summary>
        /// <value>An integer representing the count of graph instances currently in memory.</value>
        /// <remarks>
        /// This count includes all instances that have been created from the graph template
        /// and have not yet been disposed, regardless of their execution status.
        /// </remarks>
        public int InstanceCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of executions across all instances of this graph.
        /// </summary>
        /// <value>An integer representing the cumulative execution count across all instances.</value>
        public int TotalExecutions { get; set; }

        /// <summary>
        /// Gets or sets the cumulative GPU execution time across all executions.
        /// </summary>
        /// <value>A float representing the total GPU time in milliseconds for all executions.</value>
        /// <remarks>
        /// This value aggregates the pure GPU execution time measured using CUDA events
        /// across all instances and executions of this graph.
        /// </remarks>
        public float TotalGpuTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the average GPU execution time per execution.
        /// </summary>
        /// <value>A float representing the mean GPU execution time in milliseconds.</value>
        /// <remarks>
        /// This value is calculated as <see cref="TotalGpuTimeMs"/> divided by <see cref="TotalExecutions"/>.
        /// It provides insight into the typical performance characteristics of this graph.
        /// </remarks>
        public float AverageExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the most recent execution across all instances.
        /// </summary>
        /// <value>A nullable <see cref="DateTimeOffset"/> indicating the last execution time, or <c>null</c> if never executed.</value>
        public DateTimeOffset? LastExecutedAt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this graph has been optimized.
        /// </summary>
        /// <value><c>true</c> if optimization passes have been applied to this graph; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Optimized graphs typically show better performance characteristics due to
        /// kernel fusion, memory access optimization, and architecture-specific tuning.
        /// </remarks>
        public bool IsOptimized { get; set; }
    }
}