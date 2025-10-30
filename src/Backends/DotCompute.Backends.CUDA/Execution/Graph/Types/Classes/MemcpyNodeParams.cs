// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Parameters for a CUDA graph memory copy node.
    /// </summary>
    /// <remarks>
    /// This class defines parameters for memory copy operations within a CUDA graph.
    /// Memory copy nodes transfer data between host and device memory, or between
    /// different device memory locations. These operations are asynchronous when
    /// executed as part of a graph.
    /// </remarks>
    public class MemcpyNodeParams
    {
        /// <summary>
        /// Gets or sets the source memory pointer for the copy operation.
        /// </summary>
        /// <value>
        /// Native pointer to the source memory location. Can be host or device memory
        /// depending on the Kind parameter. Must be properly aligned.
        /// </value>
        public nint Source { get; set; }

        /// <summary>
        /// Gets or sets the destination memory pointer for the copy operation.
        /// </summary>
        /// <value>
        /// Native pointer to the destination memory location. Can be host or device memory
        /// depending on the Kind parameter. Must be properly aligned and have sufficient space.
        /// </value>
        public nint Destination { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes to copy.
        /// </summary>
        /// <value>
        /// Size of the memory transfer in bytes. Must not exceed the allocated size
        /// of either source or destination buffers. Zero is valid for a no-op.
        /// </value>
        public nuint ByteCount { get; set; }

        /// <summary>
        /// Gets or sets the memory copy direction/kind.
        /// </summary>
        /// <value>
        /// Direction of the memory copy operation (Host-to-Device, Device-to-Host,
        /// Device-to-Device, or Host-to-Host). Affects transfer performance and
        /// determines which memory buses are utilized.
        /// </value>
        public MemcpyKind Kind { get; set; }
    }
}
