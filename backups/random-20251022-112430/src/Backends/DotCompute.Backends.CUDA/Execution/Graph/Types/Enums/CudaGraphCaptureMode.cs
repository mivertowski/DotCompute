// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Enums
{
    /// <summary>
    /// Specifies the capture mode for CUDA graph recording operations.
    /// </summary>
    /// <remarks>
    /// CUDA graphs allow capturing sequences of operations for efficient replay.
    /// The capture mode determines how the graph interacts with other CUDA operations
    /// during the capture process, affecting thread safety and operation restrictions.
    /// </remarks>
    public enum CudaGraphCaptureMode : uint
    {
        /// <summary>
        /// Global capture mode where all CUDA operations across all threads in the process
        /// must be part of the same graph capture. Provides strictest isolation.
        /// </summary>
        Global = 0,

        /// <summary>
        /// Thread-local capture mode where only operations in the capturing thread
        /// must be part of the graph. Other threads can perform independent CUDA operations.
        /// </summary>
        ThreadLocal = 1,

        /// <summary>
        /// Relaxed capture mode allowing more flexibility in concurrent operations.
        /// Permits certain CUDA operations to occur outside the graph capture on the same stream.
        /// </summary>
        Relaxed = 2
    }
}