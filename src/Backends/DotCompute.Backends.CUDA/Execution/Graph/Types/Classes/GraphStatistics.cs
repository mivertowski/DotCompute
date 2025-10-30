// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Graph statistics for performance tracking
    /// </summary>
    public class GraphStatistics
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the created at.
        /// </summary>
        /// <value>The created at.</value>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// Gets or sets the last executed at.
        /// </summary>
        /// <value>The last executed at.</value>
        public DateTime? LastExecutedAt { get; set; }
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
        /// Gets or sets the execution count.
        /// </summary>
        /// <value>The execution count.</value>
        public int ExecutionCount { get; set; }
        /// <summary>
        /// Gets or sets the instantiation count.
        /// </summary>
        /// <value>The instantiation count.</value>
        public int InstantiationCount { get; set; }
        /// <summary>
        /// Gets or sets the update count.
        /// </summary>
        /// <value>The update count.</value>
        public int UpdateCount { get; set; }
        /// <summary>
        /// Gets or sets the optimization count.
        /// </summary>
        /// <value>The optimization count.</value>
        public int OptimizationCount { get; set; }
        /// <summary>
        /// Gets or sets the error count.
        /// </summary>
        /// <value>The error count.</value>
        public int ErrorCount { get; set; }
        /// <summary>
        /// Gets or sets the total execution time ms.
        /// </summary>
        /// <value>The total execution time ms.</value>
        public float TotalExecutionTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the last execution time ms.
        /// </summary>
        /// <value>The last execution time ms.</value>
        public float LastExecutionTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the cloned from.
        /// </summary>
        /// <value>The cloned from.</value>
        public string? ClonedFrom { get; set; }
        /// <summary>
        /// Gets or sets the capture mode.
        /// </summary>
        /// <value>The capture mode.</value>
        public CudaGraphCaptureMode? CaptureMode { get; set; }
    }
}
