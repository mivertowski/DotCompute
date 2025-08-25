using System;

namespace DotCompute.Backends.CUDA.Execution.Types
{
    /// <summary>
    /// Flags for CUDA stream creation and behavior.
    /// </summary>
    [Flags]
    public enum StreamFlags
    {
        /// <summary>
        /// Default stream behavior.
        /// </summary>
        None = 0,

        /// <summary>
        /// Stream does not synchronize with stream 0 (default stream).
        /// </summary>
        NonBlocking = 1,

        /// <summary>
        /// Stream is created with disabled timing.
        /// </summary>
        DisableTiming = 2,

        /// <summary>
        /// Stream is created for graph capture.
        /// </summary>
        GraphCapture = 4,

        /// <summary>
        /// Stream has priority scheduling enabled.
        /// </summary>
        PriorityScheduling = 8
    }
}