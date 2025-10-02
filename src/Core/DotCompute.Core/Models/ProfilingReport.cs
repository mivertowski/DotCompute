namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents a comprehensive profiling report containing kernel and memory performance data.
    /// </summary>
    public class ProfilingReport
    {
        /// <summary>
        /// Gets or initializes the timestamp when the report was generated.
        /// </summary>
        public DateTimeOffset GeneratedAt { get; init; }

        /// <summary>
        /// Gets or initializes the list of kernel profiles.
        /// </summary>
        public List<KernelProfile> KernelProfiles { get; init; } = [];

        /// <summary>
        /// Gets or initializes the list of memory profiles.
        /// </summary>
        public List<MemoryProfile> MemoryProfiles { get; init; } = [];

        /// <summary>
        /// Gets or sets the total time spent in kernel execution.
        /// </summary>
        public TimeSpan TotalKernelTime { get; set; }

        /// <summary>
        /// Gets or sets the average kernel execution time.
        /// </summary>
        public TimeSpan AverageKernelTime { get; set; }

        /// <summary>
        /// Gets or sets the total amount of memory transferred.
        /// </summary>
        public long TotalMemoryTransferred { get; set; }

        /// <summary>
        /// Gets or sets the total time spent in memory transfers.
        /// </summary>
        public TimeSpan TotalMemoryTime { get; set; }

        /// <summary>
        /// Gets or sets the top kernels by execution time.
        /// </summary>
        public List<KernelProfile> TopKernelsByTime { get; set; } = [];

        /// <summary>
        /// Gets or sets the top memory transfers by size.
        /// </summary>
        public List<MemoryProfile> TopMemoryTransfers { get; set; } = [];
    }
}