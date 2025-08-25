using System;

namespace DotCompute.Backends.CUDA.Execution.Models
{
    /// <summary>
    /// Statistics for CUDA stream management.
    /// </summary>
    public class StreamStatistics
    {
        /// <summary>
        /// Gets or sets the total number of streams created.
        /// </summary>
        public int TotalStreamsCreated { get; set; }

        /// <summary>
        /// Gets or sets the number of currently active streams.
        /// </summary>
        public int ActiveStreams { get; set; }

        /// <summary>
        /// Gets or sets the number of streams in the pool.
        /// </summary>
        public int PooledStreams { get; set; }

        /// <summary>
        /// Gets or sets the peak number of concurrent streams.
        /// </summary>
        public int PeakConcurrentStreams { get; set; }

        /// <summary>
        /// Gets or sets the total number of stream operations.
        /// </summary>
        public long TotalOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of stream synchronizations.
        /// </summary>
        public long SynchronizationCount { get; set; }

        /// <summary>
        /// Gets or sets the average stream utilization percentage.
        /// </summary>
        public double AverageUtilization { get; set; }

        /// <summary>
        /// Gets or sets the total time spent in stream operations.
        /// </summary>
        public TimeSpan TotalExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets when statistics collection started.
        /// </summary>
        public DateTimeOffset CollectionStarted { get; set; }
    }
}