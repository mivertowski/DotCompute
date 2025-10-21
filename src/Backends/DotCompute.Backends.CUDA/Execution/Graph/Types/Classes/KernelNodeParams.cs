// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Parameters for a CUDA graph kernel execution node.
    /// </summary>
    /// <remarks>
    /// This class defines all parameters required to launch a CUDA kernel within a CUDA graph.
    /// CUDA graphs allow capturing and replaying sequences of GPU operations with reduced overhead.
    /// Kernel nodes represent GPU kernel launches within the graph execution flow.
    /// </remarks>
    public class KernelNodeParams
    {
        /// <summary>
        /// Gets or sets the pointer to the CUDA kernel function to execute.
        /// </summary>
        /// <value>
        /// A native pointer (CUfunction) to the compiled kernel function.
        /// Obtained via cuModuleGetFunction after loading a CUDA module.
        /// </value>
        public nint Function { get; set; }

        /// <summary>
        /// Gets or sets the grid dimensions specifying the number of thread blocks to launch.
        /// </summary>
        /// <value>
        /// Grid dimensions in 3D space (X, Y, Z). The total number of blocks is X * Y * Z.
        /// Determines the overall parallelism of the kernel launch.
        /// </value>
        public GridDimensions GridDim { get; set; }

        /// <summary>
        /// Gets or sets the block dimensions specifying the number of threads per block.
        /// </summary>
        /// <value>
        /// Block dimensions in 3D space (X, Y, Z). The total threads per block is X * Y * Z.
        /// Must not exceed the device's maxThreadsPerBlock limit (typically 1024).
        /// </value>
        public BlockDimensions BlockDim { get; set; }

        /// <summary>
        /// Gets or sets the amount of dynamic shared memory to allocate per block in bytes.
        /// </summary>
        /// <value>
        /// Size of dynamic shared memory in bytes. This is in addition to statically allocated
        /// shared memory declared in the kernel. Must not exceed device limits.
        /// </value>
        public uint SharedMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the pointer to an array of kernel parameter pointers.
        /// </summary>
        /// <value>
        /// Native pointer to an array of void* pointers, where each element points to
        /// a kernel argument. The array must match the kernel's parameter signature.
        /// </value>
        public nint KernelParams { get; set; }
    }
}