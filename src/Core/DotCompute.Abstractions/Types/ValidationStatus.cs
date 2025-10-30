namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Status of a validation test.
    /// </summary>
    public enum ValidationStatus
    {
        /// <summary>
        /// Status has not been set.
        /// </summary>
        NotSet,

        /// <summary>
        /// Validation passed successfully.
        /// </summary>
        Passed,

        /// <summary>
        /// Validation failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Validation passed with warnings.
        /// </summary>
        Warning,

        /// <summary>
        /// Validation was skipped.
        /// </summary>
        Skipped,

        /// <summary>
        /// Validation partially passed.
        /// </summary>
        Partial
    }
}
