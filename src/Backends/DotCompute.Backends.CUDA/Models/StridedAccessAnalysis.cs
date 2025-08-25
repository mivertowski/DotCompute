using System.Collections.Generic;

namespace DotCompute.Backends.CUDA.Analysis.Models
{
    /// <summary>
    /// Represents the analysis results for strided memory access patterns.
    /// </summary>
    public class StridedAccessAnalysis
    {
        /// <summary>
        /// Gets or sets the stride between consecutive thread accesses.
        /// </summary>
        public int Stride { get; set; }

        /// <summary>
        /// Gets or sets the size of each element in bytes.
        /// </summary>
        public int ElementSize { get; set; }

        /// <summary>
        /// Gets or sets the number of threads accessing memory.
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Gets or sets the number of memory transactions required per warp.
        /// </summary>
        public int TransactionsPerWarp { get; set; }

        /// <summary>
        /// Gets or sets the efficiency of the access pattern (0.0 to 1.0).
        /// </summary>
        public double Efficiency { get; set; }

        /// <summary>
        /// Gets or sets the bandwidth utilization (0.0 to 1.0).
        /// </summary>
        public double BandwidthUtilization { get; set; }

        /// <summary>
        /// Gets or sets whether the access pattern is coalesced.
        /// </summary>
        public bool IsCoalesced { get; set; }

        /// <summary>
        /// Gets the list of optimization recommendations.
        /// </summary>
        public List<string> Recommendations { get; } = new();
    }
}