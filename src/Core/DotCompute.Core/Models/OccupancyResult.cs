namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents the result of an occupancy calculation.
    /// </summary>
    public class OccupancyResult
    {
        /// <summary>
        /// Gets or sets the occupancy percentage (0.0 to 1.0).
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Gets or sets the number of active warps per SM.
        /// </summary>
        public int ActiveWarps { get; set; }

        /// <summary>
        /// Gets or sets the number of active blocks per SM.
        /// </summary>
        public int ActiveBlocks { get; set; }

        /// <summary>
        /// Gets or sets the factor limiting occupancy (e.g., "Registers", "Shared Memory").
        /// </summary>
        public string LimitingFactor { get; set; } = "";
    }
}
