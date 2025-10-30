// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Types
{
    /// <summary>
    /// Types of CUDA events used for different purposes.
    /// </summary>
    public enum CudaEventType
    {
        /// <summary>
        /// Event capable of timing GPU operations.
        /// </summary>
        Timing,

        /// <summary>
        /// Event optimized for synchronization between streams.
        /// </summary>
        Synchronization
    }
}
