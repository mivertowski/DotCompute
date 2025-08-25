using System;

namespace DotCompute.Backends.CUDA.Analysis.Models
{
    /// <summary>
    /// Represents runtime profiling results for memory coalescing analysis.
    /// </summary>
    public class RuntimeCoalescingProfile
    {
        /// <summary>
        /// Gets or initializes the name of the kernel being profiled.
        /// </summary>
        public required string KernelName { get; init; }

        /// <summary>
        /// Gets or initializes the timestamp when profiling started.
        /// </summary>
        public DateTimeOffset ProfileStartTime { get; init; }

        /// <summary>
        /// Gets or sets the average execution time across all profiling runs.
        /// </summary>
        public TimeSpan AverageExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the minimum execution time observed.
        /// </summary>
        public TimeSpan MinExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the maximum execution time observed.
        /// </summary>
        public TimeSpan MaxExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the estimated memory bandwidth in bytes per second.
        /// </summary>
        public double EstimatedBandwidth { get; set; }

        /// <summary>
        /// Gets or sets the estimated coalescing efficiency (0.0 to 1.0).
        /// </summary>
        public double EstimatedCoalescingEfficiency { get; set; }
    }
}