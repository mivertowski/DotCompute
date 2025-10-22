// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Structs
{
    /// <summary>
    /// Represents the dimensions of a CUDA thread block in 3D space.
    /// Thread blocks are the fundamental unit of parallel execution in CUDA,
    /// containing threads that can cooperate through shared memory and synchronization.
    /// </summary>
    /// <remarks>
    /// Each dimension must be at least 1. The total number of threads (X * Y * Z)
    /// is limited by the device's maxThreadsPerBlock attribute, typically 1024.
    /// </remarks>
    public struct BlockDimensions(uint x, uint y = 1, uint z = 1)
    {
        /// <summary>
        /// Gets or sets the number of threads in the X dimension of the block.
        /// </summary>
        /// <value>The thread count in the X dimension. Must be at least 1.</value>
        public uint X { get; set; } = x;

        /// <summary>
        /// Gets or sets the number of threads in the Y dimension of the block.
        /// </summary>
        /// <value>The thread count in the Y dimension. Defaults to 1 for 1D/2D workloads.</value>
        public uint Y { get; set; } = y;

        /// <summary>
        /// Gets or sets the number of threads in the Z dimension of the block.
        /// </summary>
        /// <value>The thread count in the Z dimension. Defaults to 1 for 1D/2D workloads.</value>
        public uint Z { get; set; } = z;
    }
}