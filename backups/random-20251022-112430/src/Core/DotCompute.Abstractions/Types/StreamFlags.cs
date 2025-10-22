namespace DotCompute.Backends.CUDA.Execution.Types
{
    /// <summary>
    /// Flags for CUDA stream creation and behavior.
    /// </summary>
    [Flags]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - "Flags" is appropriate for flag enum
    public enum StreamCreationFlags
#pragma warning restore CA1711
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
