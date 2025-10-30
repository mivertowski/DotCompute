// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Types
{
    /// <summary>
    /// CUDA event creation flags for backward compatibility.
    /// </summary>
    public enum CudaEventFlags
    {
        /// <summary>
        /// Default event creation flags.
        /// </summary>
        Default,

        /// <summary>
        /// Event will use blocking synchronization.
        /// </summary>
        BlockingSync,

        /// <summary>
        /// Event will not record timing data.
        /// </summary>
        DisableTiming,

        /// <summary>
        /// Event can be used for interprocess synchronization.
        /// </summary>
        Interprocess
    }
}
