// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Structs
{
    /// <summary>
    /// Represents the dimensions of a CUDA grid in 3D space.
    /// A grid is composed of thread blocks, and grid dimensions specify the number
    /// of blocks launched along each axis during kernel execution.
    /// </summary>
    /// <remarks>
    /// Each dimension must be at least 1. The total number of blocks (X * Y * Z)
    /// determines the overall parallelism of the kernel launch. Grid dimensions
    /// are typically calculated based on problem size and block dimensions.
    /// </remarks>
    public struct GridDimensions(uint x, uint y = 1, uint z = 1)
    {
        /// <summary>
        /// Gets or sets the number of thread blocks in the X dimension of the grid.
        /// </summary>
        /// <value>The block count in the X dimension. Must be at least 1.</value>
        public uint X { get; set; } = x;

        /// <summary>
        /// Gets or sets the number of thread blocks in the Y dimension of the grid.
        /// </summary>
        /// <value>The block count in the Y dimension. Defaults to 1 for 1D workloads.</value>
        public uint Y { get; set; } = y;

        /// <summary>
        /// Gets or sets the number of thread blocks in the Z dimension of the grid.
        /// </summary>
        /// <value>The block count in the Z dimension. Defaults to 1 for 1D/2D workloads.</value>
        public uint Z { get; set; } = z;
    }
}