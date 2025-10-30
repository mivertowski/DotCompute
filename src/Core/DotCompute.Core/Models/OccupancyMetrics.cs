// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Occupancy metrics for kernel execution.
    /// </summary>
    public sealed class OccupancyMetrics
    {
        /// <summary>
        /// Gets or sets the theoretical occupancy ratio (0.0 to 1.0).
        /// </summary>
        public double TheoreticalOccupancy { get; set; }

        /// <summary>
        /// Gets or sets the warp occupancy ratio (0.0 to 1.0).
        /// </summary>
        public double WarpOccupancy { get; set; }

        /// <summary>
        /// Gets or sets the number of blocks per streaming multiprocessor.
        /// </summary>
        public int BlocksPerSM { get; set; }

        /// <summary>
        /// Gets or sets the number of active warps.
        /// </summary>
        public int ActiveWarps { get; set; }

        /// <summary>
        /// Gets or sets whether the configuration is optimal for Ada generation GPUs.
        /// </summary>
        public bool IsOptimalForAda { get; set; }
    }
}
