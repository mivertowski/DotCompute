namespace DotCompute.Backends.CUDA.Execution.Types
{
    /// <summary>
    /// Defines priority levels for CUDA streams.
    /// </summary>
    public enum StreamPriority
    {
        /// <summary>
        /// Lowest priority for background operations.
        /// </summary>
        Lowest = -2,

        /// <summary>
        /// Low priority for non-critical operations.
        /// </summary>
        Low = -1,

        /// <summary>
        /// Normal/default priority.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// High priority for important operations.
        /// </summary>
        High = 1,

        /// <summary>
        /// Highest priority for critical operations.
        /// </summary>
        Highest = 2
    }
}
