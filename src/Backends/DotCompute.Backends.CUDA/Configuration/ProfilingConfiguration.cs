namespace DotCompute.Backends.CUDA.Profiling.Configuration
{
    /// <summary>
    /// Configuration options for CUDA performance profiling.
    /// </summary>
    public class ProfilingConfiguration
    {
        /// <summary>
        /// Gets or sets whether to profile kernel executions.
        /// </summary>
        public bool ProfileKernels { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to profile memory operations.
        /// </summary>
        public bool ProfileMemory { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to profile CUDA API calls.
        /// </summary>
        public bool ProfileApi { get; set; }


        /// <summary>
        /// Gets or sets whether to collect performance metrics.
        /// </summary>
        public bool CollectMetrics { get; set; } = true;

        /// <summary>
        /// Gets a default profiling configuration.
        /// </summary>
        public static ProfilingConfiguration Default => new();
    }
}