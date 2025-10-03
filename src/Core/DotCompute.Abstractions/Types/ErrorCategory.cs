namespace DotCompute.Backends.CUDA.ErrorHandling.Types
{
    /// <summary>
    /// Categories of CUDA errors for handling strategies.
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>
        /// Memory-related errors (allocation, access violations).
        /// </summary>
        Memory,

        /// <summary>
        /// Device-related errors (device failure, reset).
        /// </summary>
        Device,

        /// <summary>
        /// Kernel execution errors.
        /// </summary>
        Kernel,

        /// <summary>
        /// Stream synchronization errors.
        /// </summary>
        Stream,

        /// <summary>
        /// API call errors.
        /// </summary>
        Api,

        /// <summary>
        /// Driver errors.
        /// </summary>
        Driver,

        /// <summary>
        /// Resource errors (handles, contexts).
        /// </summary>
        Resource,

        /// <summary>
        /// Compilation errors.
        /// </summary>
        Compilation,

        /// <summary>
        /// Unknown or unclassified errors.
        /// </summary>
        Unknown
    }
}
