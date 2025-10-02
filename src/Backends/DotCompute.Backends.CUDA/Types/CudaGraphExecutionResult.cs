// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Result of CUDA graph execution.
    /// </summary>
    public sealed class CudaGraphExecutionResult
    {
        /// <summary>
        /// Gets or sets the success.
        /// </summary>
        /// <value>The success.</value>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Gets or sets the execution time ms.
        /// </summary>
        /// <value>The execution time ms.</value>
        public long ExecutionTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the nodes executed.
        /// </summary>
        /// <value>The nodes executed.</value>
        public int NodesExecuted { get; set; }
        /// <summary>
        /// Gets or sets the instance identifier.
        /// </summary>
        /// <value>The instance id.</value>
        public string InstanceId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the graph identifier.
        /// </summary>
        /// <value>The graph id.</value>
        public string GraphId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the execution time.
        /// </summary>
        /// <value>The execution time.</value>
        public TimeSpan ExecutionTime { get; set; }
        /// <summary>
        /// Gets or sets the gpu time ms.
        /// </summary>
        /// <value>The gpu time ms.</value>
        public double GpuTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the execution count.
        /// </summary>
        /// <value>The execution count.</value>
        public int ExecutionCount { get; set; }
    }
}