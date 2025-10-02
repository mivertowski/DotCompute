namespace DotCompute.Backends.CUDA.Profiling.Types
{
    /// <summary>
    /// Statistics for a specific type of memory transfer.
    /// </summary>
    public class TransferTypeStats
    {
        /// <summary>
        /// Gets or sets the number of transfers of this type.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the total bytes transferred.
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// Gets or sets the average bytes per transfer.
        /// </summary>
        public double AverageBytes { get; set; }

        /// <summary>
        /// Gets or sets the total time spent in transfers.
        /// </summary>
        public TimeSpan TotalTime { get; set; }

        /// <summary>
        /// Gets or sets the average bandwidth in bytes per second.
        /// </summary>
        public double AverageBandwidth { get; set; }
    }
}