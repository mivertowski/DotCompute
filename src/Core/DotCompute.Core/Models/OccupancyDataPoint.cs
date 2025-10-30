namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents a single data point in an occupancy curve.
    /// </summary>
    public class OccupancyDataPoint
    {
        /// <summary>
        /// Gets or sets the block size for this data point.
        /// </summary>
        public int BlockSize { get; set; }

        /// <summary>
        /// Gets or sets the occupancy at this block size (0.0 to 1.0).
        /// </summary>
        public double Occupancy { get; set; }

        /// <summary>
        /// Gets or sets the number of active warps per SM.
        /// </summary>
        public int ActiveWarps { get; set; }

        /// <summary>
        /// Gets or sets the number of active blocks per SM.
        /// </summary>
        public int ActiveBlocks { get; set; }

        /// <summary>
        /// Gets or sets the factor limiting occupancy at this configuration.
        /// </summary>
        public string LimitingFactor { get; set; } = "";
    }
}
