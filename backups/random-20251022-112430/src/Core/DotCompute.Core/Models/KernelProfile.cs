namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents performance profiling data for a CUDA kernel.
    /// </summary>
    public class KernelProfile
    {
        /// <summary>
        /// Gets or initializes the name of the profiled kernel.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or initializes the timestamp when profiling started.
        /// </summary>
        public DateTimeOffset StartTime { get; init; }

        /// <summary>
        /// Gets or sets the number of times the kernel was executed.
        /// </summary>
        public int ExecutionCount { get; set; }

        /// <summary>
        /// Gets or sets the total execution time across all runs.
        /// </summary>
        public TimeSpan TotalTime { get; set; }

        /// <summary>
        /// Gets or sets the average execution time per run.
        /// </summary>
        public TimeSpan AverageTime { get; set; }

        /// <summary>
        /// Gets or sets the minimum execution time observed.
        /// </summary>
        public TimeSpan MinTime { get; set; }

        /// <summary>
        /// Gets or sets the maximum execution time observed.
        /// </summary>
        public TimeSpan MaxTime { get; set; }

        /// <summary>
        /// Gets or sets the standard deviation of execution times.
        /// </summary>
        public TimeSpan StandardDeviation { get; set; }

        /// <summary>
        /// Gets or sets the amount of shared memory used by the kernel in bytes.
        /// </summary>
        public long SharedMemoryUsed { get; set; }

        /// <summary>
        /// Gets or sets the number of registers used per thread.
        /// </summary>
        public long RegistersPerThread { get; set; }

        /// <summary>
        /// Gets or sets the thread block size.
        /// </summary>
        public int BlockSize { get; set; }

        /// <summary>
        /// Gets or sets the grid size.
        /// </summary>
        public int GridSize { get; set; }
    }
}