using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Profiling.Types;

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents an analysis of memory transfer patterns and performance.
    /// </summary>
    public class MemoryTransferAnalysis
    {
        /// <summary>
        /// Gets or sets the total number of memory transfers analyzed.
        /// </summary>
        public int TotalTransfers { get; set; }

        /// <summary>
        /// Gets or sets the total bytes transferred.
        /// </summary>
        public long TotalBytesTransferred { get; set; }

        /// <summary>
        /// Gets or sets the total time spent in transfers.
        /// </summary>
        public TimeSpan TotalTransferTime { get; set; }

        /// <summary>
        /// Gets or sets the overall bandwidth in bytes per second.
        /// </summary>
        public double OverallBandwidth { get; set; }

        /// <summary>
        /// Gets or sets statistics grouped by transfer type.
        /// </summary>
        public Dictionary<MemoryTransferType, TransferTypeStats> TransfersByType { get; init; } = [];

        /// <summary>
        /// Gets or sets identified bottlenecks in memory transfers.
        /// </summary>
        public IList<string> Bottlenecks { get; init; } = [];
    }
}