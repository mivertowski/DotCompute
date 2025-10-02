// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Graph node parameters for kernel execution
    /// </summary>
    public class KernelNodeParams
    {
        /// <summary>
        /// Gets or sets the function.
        /// </summary>
        /// <value>The function.</value>
        public nint Function { get; set; }
        /// <summary>
        /// Gets or sets the grid dim.
        /// </summary>
        /// <value>The grid dim.</value>
        public GridDimensions GridDim { get; set; }
        /// <summary>
        /// Gets or sets the block dim.
        /// </summary>
        /// <value>The block dim.</value>

        public BlockDimensions BlockDim { get; set; }
        /// <summary>
        /// Gets or sets the shared memory bytes.
        /// </summary>
        /// <value>The shared memory bytes.</value>

        public uint SharedMemoryBytes { get; set; }
        /// <summary>
        /// Gets or sets the kernel params.
        /// </summary>
        /// <value>The kernel params.</value>
        public nint KernelParams { get; set; }
    }
}