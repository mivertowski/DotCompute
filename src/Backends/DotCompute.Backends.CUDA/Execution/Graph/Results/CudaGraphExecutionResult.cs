// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.Execution.Graph.Results
{
    /// <summary>
    /// Contains the results and performance metrics from executing a CUDA graph instance.
    /// Provides comprehensive information about execution success, timing, and error details.
    /// </summary>
    /// <remarks>
    /// This class captures both successful execution metrics and error information,
    /// enabling comprehensive performance analysis and debugging of graph execution.
    /// </remarks>
    public sealed class CudaGraphExecutionResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the graph execution completed successfully.
        /// </summary>
        /// <value><c>true</c> if execution completed without errors; otherwise, <c>false</c>.</value>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the graph instance that was executed.
        /// </summary>
        /// <value>A string identifying the specific graph instance that was executed.</value>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the identifier of the parent graph template.
        /// </summary>
        /// <value>A string identifying the graph template from which the instance was created.</value>
        public string GraphId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total wall-clock execution time including CPU overhead.
        /// </summary>
        /// <value>A <see cref="TimeSpan"/> representing the total execution time from start to completion.</value>
        /// <remarks>
        /// This includes all CPU-side overhead such as kernel launches, synchronization,
        /// and any host-device communication. For pure GPU execution time, use <see cref="GpuTimeMs"/>.
        /// </remarks>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the GPU execution time in milliseconds.
        /// </summary>
        /// <value>A float representing the actual GPU execution time in milliseconds.</value>
        /// <remarks>
        /// This measurement represents the actual time spent by the GPU executing the graph,
        /// measured using CUDA events for high precision timing.
        /// </remarks>
        public float GpuTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the total number of times this graph instance has been executed.
        /// </summary>
        /// <value>An integer representing the cumulative execution count for this instance.</value>
        public int ExecutionCount { get; set; }

        /// <summary>
        /// Gets or sets the error message if execution failed.
        /// </summary>
        /// <value>A string containing the error description, or <c>null</c> if execution succeeded.</value>
        public string? ErrorMessage { get; set; }
    }
}