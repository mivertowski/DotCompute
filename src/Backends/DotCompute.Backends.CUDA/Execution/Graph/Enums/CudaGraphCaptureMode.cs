// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Enums
{
    /// <summary>
    /// Specifies the capture mode for CUDA stream capture operations.
    /// Controls the scope and behavior of graph capture from CUDA streams.
    /// </summary>
    /// <remarks>
    /// Stream capture allows creating graphs by recording operations executed on a stream.
    /// The capture mode determines which operations are captured and how dependencies
    /// between streams are handled during the capture process.
    /// </remarks>
    public enum CudaGraphCaptureMode
    {
        /// <summary>
        /// Global capture mode captures all operations across all streams.
        /// </summary>
        /// <remarks>
        /// In global mode, all CUDA operations on any stream will be captured into the graph.
        /// This is the most comprehensive capture mode but may include unintended operations
        /// from other parts of the application. Value corresponds to cuStreamCaptureMode_Global.
        /// </remarks>
        Global = 0,

        /// <summary>
        /// Thread-local capture mode captures operations only from the current thread.
        /// </summary>
        /// <remarks>
        /// This mode captures operations only from streams used by the current thread,
        /// providing better isolation when multiple threads are performing CUDA operations
        /// concurrently. Value corresponds to cuStreamCaptureMode_ThreadLocal.
        /// </remarks>
        ThreadLocal = 1,

        /// <summary>
        /// Relaxed capture mode provides flexible capture with reduced restrictions.
        /// </summary>
        /// <remarks>
        /// Relaxed mode allows more flexibility in what operations can be captured,
        /// including some operations that would normally be prohibited in stricter modes.
        /// Use with caution as it may capture operations with complex dependencies.
        /// Value corresponds to cuStreamCaptureMode_Relaxed.
        /// </remarks>
        Relaxed = 2
    }
}