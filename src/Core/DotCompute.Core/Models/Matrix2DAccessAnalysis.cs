using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents the analysis results for 2D matrix memory access patterns.
    /// </summary>
    public class Matrix2DAccessAnalysis
    {
        /// <summary>
        /// Gets or sets the number of rows in the matrix.
        /// </summary>
        public int Rows { get; set; }

        /// <summary>
        /// Gets or sets the number of columns in the matrix.
        /// </summary>
        public int Columns { get; set; }

        /// <summary>
        /// Gets or sets the memory access order pattern.
        /// </summary>
        public AccessOrder AccessOrder { get; set; }

        /// <summary>
        /// Gets or sets the size of each element in bytes.
        /// </summary>
        public int ElementSize { get; set; }

        /// <summary>
        /// Gets or sets the thread block X dimension.
        /// </summary>
        public int BlockDimX { get; set; }

        /// <summary>
        /// Gets or sets the thread block Y dimension.
        /// </summary>
        public int BlockDimY { get; set; }

        /// <summary>
        /// Gets or sets whether the access pattern is optimal.
        /// </summary>
        public bool IsOptimal { get; set; }

        /// <summary>
        /// Gets or sets the coalescing factor (0.0 to 1.0).
        /// </summary>
        public double CoalescingFactor { get; set; }

        /// <summary>
        /// Gets or sets the bandwidth efficiency (0.0 to 1.0).
        /// </summary>
        public double BandwidthEfficiency { get; set; }

        /// <summary>
        /// Gets or sets the number of memory transactions per block.
        /// </summary>
        public int TransactionsPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the tile analysis for shared memory optimization.
        /// </summary>
        public TileAnalysis TileAnalysis { get; set; } = new();

        /// <summary>
        /// Gets the list of optimization recommendations.
        /// </summary>
        public IList<string> Optimizations { get; init; } = [];
    }
}
