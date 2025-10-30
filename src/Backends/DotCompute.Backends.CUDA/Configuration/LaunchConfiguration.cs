using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.CUDA.Configuration
{
    /// <summary>
    /// Represents the launch configuration for a CUDA kernel execution.
    /// </summary>
    public class LaunchConfiguration
    {
        /// <summary>
        /// Gets or sets the thread block dimensions.
        /// </summary>
        public Dim3 BlockSize { get; set; }

        /// <summary>
        /// Gets or sets the grid dimensions.
        /// </summary>
        public Dim3 GridSize { get; set; }

        /// <summary>
        /// Gets or sets the amount of shared memory required in bytes.
        /// </summary>
        public ulong SharedMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the theoretical occupancy as a percentage (0.0 to 1.0).
        /// </summary>
        public double TheoreticalOccupancy { get; set; }

        /// <summary>
        /// Gets or sets the number of active warps.
        /// </summary>
        public int ActiveWarps { get; set; }

        /// <summary>
        /// Gets or sets the number of active blocks per SM.
        /// </summary>
        public int ActiveBlocks { get; set; }

        /// <summary>
        /// Gets or sets the number of registers used per thread.
        /// </summary>
        public int RegistersPerThread { get; set; }

        /// <summary>
        /// Gets or sets the device ID for this configuration.
        /// </summary>
        public int DeviceId { get; set; }
    }
}
