// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA graph statistics.
    /// </summary>
    public sealed class CudaGraphStatistics
    {
        /// <summary>
        /// Gets or sets the node count.
        /// </summary>
        /// <value>The node count.</value>
        public int NodeCount { get; set; }
        /// <summary>
        /// Gets or sets the edge count.
        /// </summary>
        /// <value>The edge count.</value>
        public int EdgeCount { get; set; }
        /// <summary>
        /// Gets or sets the estimated memory usage.
        /// </summary>
        /// <value>The estimated memory usage.</value>
        public long EstimatedMemoryUsage { get; set; }
        /// <summary>
        /// Gets or sets the average node execution time.
        /// </summary>
        /// <value>The average node execution time.</value>
        public double AverageNodeExecutionTime { get; set; }
        /// <summary>
        /// Gets or sets the graph identifier.
        /// </summary>
        /// <value>The graph id.</value>
        public string GraphId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the instance count.
        /// </summary>
        /// <value>The instance count.</value>
        public int InstanceCount { get; set; }
        /// <summary>
        /// Gets or sets the total executions.
        /// </summary>
        /// <value>The total executions.</value>
        public int TotalExecutions { get; set; }
        /// <summary>
        /// Gets or sets the total gpu time ms.
        /// </summary>
        /// <value>The total gpu time ms.</value>
        public double TotalGpuTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the average execution time ms.
        /// </summary>
        /// <value>The average execution time ms.</value>
        public double AverageExecutionTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the last executed at.
        /// </summary>
        /// <value>The last executed at.</value>
        public DateTimeOffset? LastExecutedAt { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether optimized.
        /// </summary>
        /// <value>The is optimized.</value>
        public bool IsOptimized { get; set; }
    }
}