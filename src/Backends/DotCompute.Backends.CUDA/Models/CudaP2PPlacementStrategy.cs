// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.P2P.Models
{
    /// <summary>
    /// Strategy for placing and transferring data across multiple devices.
    /// </summary>
    public sealed class CudaP2PPlacementStrategy
    {
        /// <summary>
        /// Gets or sets the list of data placements.
        /// </summary>
        public IList<CudaDataPlacement> Placements { get; } = [];

        /// <summary>
        /// Gets or sets the order in which transfers should occur.
        /// </summary>
        public IList<Guid> TransferOrder { get; } = [];

        /// <summary>
        /// Gets or sets the estimated total time for all transfers in seconds.
        /// </summary>
        public double EstimatedTotalTime { get; set; }

        /// <summary>
        /// Gets or sets the device utilization percentages.
        /// Key is device ID, value is utilization (0.0 to 1.0).
        /// </summary>
        public Dictionary<int, double> DeviceUtilization { get; } = [];
    }
}