// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Native;

namespace DotCompute.Backends.CUDA.Execution.Graph
{
    /// <summary>
    /// Represents a CUDA computational graph that can be compiled, optimized, and executed.
    /// Provides high-performance execution through kernel fusion and optimization for RTX 2000 Ada architecture.
    /// </summary>
    /// <remarks>
    /// CUDA graphs enable efficient execution of repetitive kernel sequences by reducing CPU-GPU
    /// synchronization overhead and enabling advanced optimizations like kernel fusion.
    /// </remarks>
    public sealed class CudaGraph : IDisposable
    {
        /// <summary>
        /// Gets or sets the unique identifier for this CUDA graph.
        /// </summary>
        /// <value>A string that uniquely identifies this graph instance.</value>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the native CUDA graph handle.
        /// </summary>
        /// <value>An <see cref="IntPtr"/> representing the native CUDA graph handle.</value>
        public IntPtr Handle { get; set; }

        /// <summary>
        /// Gets or sets the list of kernel operations that comprise this graph.
        /// </summary>
        /// <value>A list of <see cref="CudaKernelOperation"/> instances representing the computational sequence.</value>
        public List<CudaKernelOperation> Operations { get; set; } = [];

        /// <summary>
        /// Gets or sets the number of nodes in the CUDA graph.
        /// </summary>
        /// <value>The total number of computational nodes in the graph.</value>
        public int NodeCount { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this graph was created.
        /// </summary>
        /// <value>A <see cref="DateTimeOffset"/> indicating when the graph was instantiated.</value>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the graph has been built and is ready for instantiation.
        /// </summary>
        /// <value><c>true</c> if the graph has been successfully built; otherwise, <c>false</c>.</value>
        public bool IsBuilt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the graph has been optimized for execution.
        /// </summary>
        /// <value><c>true</c> if optimization passes have been applied; otherwise, <c>false</c>.</value>
        public bool IsOptimized { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this graph was created through stream capture.
        /// </summary>
        /// <value><c>true</c> if the graph was captured from a CUDA stream; otherwise, <c>false</c>.</value>
        public bool IsCaptured { get; set; }

        /// <summary>
        /// Gets or sets the optimization options for this graph.
        /// </summary>
        /// <value>A <see cref="CudaGraphOptimizationOptions"/> instance specifying optimization behavior.</value>
        public CudaGraphOptimizationOptions Options { get; set; } = new();

        /// <summary>
        /// Releases the native CUDA graph resources.
        /// </summary>
        /// <remarks>
        /// This method is called automatically when the graph is disposed. It safely destroys
        /// the native CUDA graph handle and prevents further use of this graph instance.
        /// </remarks>
        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                try
                {
                    _ = CudaRuntime.cuGraphDestroy(Handle);
                }
                catch (Exception)
                {
                    // Ignore disposal errors - the native resource may have already been released
                }
                Handle = IntPtr.Zero;
            }
        }
    }
}