// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Native;

namespace DotCompute.Backends.CUDA.Execution.Graph
{
    /// <summary>
    /// Represents an instantiated CUDA graph that can be executed multiple times with high performance.
    /// Provides tracking of execution statistics and resource management for graph instances.
    /// </summary>
    /// <remarks>
    /// Graph instances are created from graph templates and can be executed repeatedly with minimal
    /// overhead. Each instance maintains its own execution statistics and can be updated dynamically.
    /// </remarks>
    public sealed class CudaGraphInstance : IDisposable
    {
        /// <summary>
        /// Gets or sets the unique identifier for this graph instance.
        /// </summary>
        /// <value>A string that uniquely identifies this graph instance.</value>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the identifier of the parent graph from which this instance was created.
        /// </summary>
        /// <value>A string identifying the parent graph template.</value>
        public string GraphId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the native CUDA graph execution handle.
        /// </summary>
        /// <value>An <see cref="IntPtr"/> representing the native CUDA graph execution handle.</value>
        public IntPtr Handle { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this instance was created.
        /// </summary>
        /// <value>A <see cref="DateTimeOffset"/> indicating when the instance was instantiated.</value>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the most recent execution.
        /// </summary>
        /// <value>A nullable <see cref="DateTimeOffset"/> indicating the last execution time, or <c>null</c> if never executed.</value>
        public DateTimeOffset? LastExecutedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the most recent update.
        /// </summary>
        /// <value>A nullable <see cref="DateTimeOffset"/> indicating the last update time, or <c>null</c> if never updated.</value>
        public DateTimeOffset? LastUpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the total number of times this instance has been executed.
        /// </summary>
        /// <value>An integer representing the cumulative execution count.</value>
        public int ExecutionCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of times this instance has been updated.
        /// </summary>
        /// <value>An integer representing the cumulative update count.</value>
        public int UpdateCount { get; set; }

        /// <summary>
        /// Gets or sets the cumulative GPU execution time for all executions of this instance.
        /// </summary>
        /// <value>A float representing the total GPU time in milliseconds.</value>
        public float TotalGpuTime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is valid and can be executed.
        /// </summary>
        /// <value><c>true</c> if the instance is valid for execution; otherwise, <c>false</c>.</value>
        public bool IsValid { get; set; }

        /// <summary>
        /// Releases the native CUDA graph execution resources.
        /// </summary>
        /// <remarks>
        /// This method is called automatically when the instance is disposed. It safely destroys
        /// the native CUDA graph execution handle and marks the instance as invalid.
        /// </remarks>
        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                try
                {
                    _ = CudaRuntime.cuGraphExecDestroy(Handle);
                }
                catch (Exception)
                {
                    // Ignore disposal errors - the native resource may have already been released
                }
                Handle = IntPtr.Zero;
                IsValid = false;
            }
        }
    }
}