namespace DotCompute.Abstractions.Types
{
    /// <summary>
    /// Provides hints for kernel launch optimization based on workload characteristics.
    /// </summary>
    public enum OptimizationHint
    {
        /// <summary>
        /// No specific optimization preference.
        /// </summary>
        None,

        /// <summary>
        /// Balance between occupancy and other factors.
        /// </summary>
        Balanced,

        /// <summary>
        /// Optimize for memory-bound workloads.
        /// </summary>
        MemoryBound,

        /// <summary>
        /// Optimize for compute-bound workloads.
        /// </summary>
        ComputeBound,

        /// <summary>
        /// Minimize kernel launch latency.
        /// </summary>
        Latency
    }
}
