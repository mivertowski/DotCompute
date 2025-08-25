// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Models
{
    /// <summary>
    /// Grid configuration for optimal kernel execution.
    /// </summary>
    public sealed class GridConfig
    {
        /// <summary>
        /// Gets or sets the X dimension of the grid.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the Y dimension of the grid.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Gets or sets the Z dimension of the grid.
        /// </summary>
        public int Z { get; set; }

        /// <summary>
        /// Gets or sets the total number of blocks in the grid.
        /// </summary>
        public int TotalBlocks { get; set; }

        /// <summary>
        /// Gets or sets the number of elements each block processes.
        /// </summary>
        public int ElementsPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the theoretical occupancy ratio (0.0 to 1.0).
        /// </summary>
        public double Occupancy { get; set; }
    }
}