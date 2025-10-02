namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents an analysis of register usage and pressure for kernel optimization.
    /// </summary>
    public class RegisterAnalysis
    {
        /// <summary>
        /// Gets or sets the number of registers used per thread.
        /// </summary>
        public int RegistersPerThread { get; set; }

        /// <summary>
        /// Gets or sets the number of registers used per block.
        /// </summary>
        public int RegistersUsedPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the maximum registers available per block.
        /// </summary>
        public int MaxRegistersPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the maximum registers available per SM.
        /// </summary>
        public int MaxRegistersPerSM { get; set; }

        /// <summary>
        /// Gets or sets the maximum blocks per SM limited by register usage.
        /// </summary>
        public int MaxBlocksPerSmRegisters { get; set; }

        /// <summary>
        /// Gets or sets the warp occupancy (0.0 to 1.0).
        /// </summary>
        public double WarpOccupancy { get; set; }

        /// <summary>
        /// Gets or sets whether register pressure is limiting occupancy.
        /// </summary>
        public bool IsRegisterLimited { get; set; }

        /// <summary>
        /// Gets the list of optimization suggestions.
        /// </summary>
        public IList<string> Suggestions { get; } = [];
    }
}