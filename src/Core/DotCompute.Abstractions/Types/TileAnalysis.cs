namespace DotCompute.Abstractions.Types
{
    /// <summary>
    /// Represents the analysis of tile efficiency for shared memory optimization.
    /// </summary>
    public class TileAnalysis
    {
        /// <summary>
        /// Gets or sets the optimal tile size for shared memory usage.
        /// </summary>
        public int OptimalTileSize { get; set; }

        /// <summary>
        /// Gets or sets the amount of shared memory required in bytes.
        /// </summary>
        public int SharedMemoryRequired { get; set; }

        /// <summary>
        /// Gets or sets the efficiency of the tiling strategy (0.0 to 1.0).
        /// </summary>
        public double Efficiency { get; set; }
    }
}