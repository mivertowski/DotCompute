namespace DotCompute.Abstractions.Types
{
    /// <summary>
    /// Represents information about a memory access pattern in CUDA kernels.
    /// Used to analyze coalescing efficiency and identify optimization opportunities.
    /// </summary>
    public class MemoryAccessInfo
    {
        /// <summary>
        /// Gets or initializes the name of the kernel performing the memory access.
        /// </summary>
        public required string KernelName { get; init; }

        /// <summary>
        /// Gets or sets the base memory address for the access pattern.
        /// </summary>
        public long BaseAddress { get; set; }

        /// <summary>
        /// Gets or sets the total size of data being accessed in bytes.
        /// </summary>
        public int AccessSize { get; set; }

        /// <summary>
        /// Gets or sets the size of individual elements being accessed in bytes.
        /// </summary>
        public int ElementSize { get; set; }

        /// <summary>
        /// Gets or sets the number of threads performing the memory access.
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Gets or sets the stride between consecutive thread accesses.
        /// A stride of 1 indicates coalesced access.
        /// </summary>
        public int Stride { get; set; } = 1;

        /// <summary>
        /// Gets or sets whether the access pattern is random.
        /// Random access patterns severely impact coalescing efficiency.
        /// </summary>
        public bool IsRandom { get; set; }

        /// <summary>
        /// Gets or sets the execution time for this memory access pattern.
        /// Used for bandwidth calculations.
        /// </summary>
        public TimeSpan? ExecutionTime { get; set; }


        /// <summary>
        /// Gets or sets the memory access pattern type.
        /// </summary>
        public MemoryAccessPattern AccessPattern { get; set; }
    }
}