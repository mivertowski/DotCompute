namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents an occupancy curve showing how occupancy varies with block size.
    /// </summary>
    public class OccupancyCurve
    {
        /// <summary>
        /// Gets or sets the name of the kernel being analyzed.
        /// </summary>
        public string KernelName { get; set; } = "";

        /// <summary>
        /// Gets or sets the device ID for this analysis.
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// Gets the list of occupancy data points at different block sizes.
        /// </summary>
        public List<OccupancyDataPoint> DataPoints { get; } = [];

        /// <summary>
        /// Gets or sets the block size that yields optimal occupancy.
        /// </summary>
        public int OptimalBlockSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum occupancy achieved.
        /// </summary>
        public double MaxOccupancy { get; set; }
    }
}