namespace DotCompute.Backends.CUDA.Analysis.Enums
{
    /// <summary>
    /// Defines the types of memory coalescing issues that can be identified.
    /// </summary>
    public enum IssueType
    {
        /// <summary>
        /// Memory access is not properly aligned to cache line boundaries.
        /// </summary>
        Misalignment,

        /// <summary>
        /// Memory access pattern has a stride greater than 1.
        /// </summary>
        StridedAccess,

        /// <summary>
        /// Element size is too small for efficient memory transactions.
        /// </summary>
        SmallElements,

        /// <summary>
        /// Memory access pattern is random or unpredictable.
        /// </summary>
        RandomAccess,

        /// <summary>
        /// Shared memory bank conflicts are occurring.
        /// </summary>
        BankConflict,

        /// <summary>
        /// Thread divergence is causing inefficient memory access.
        /// </summary>
        Divergence
    }
}