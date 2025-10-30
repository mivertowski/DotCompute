// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Parameters for a CUDA graph memory set node.
    /// </summary>
    /// <remarks>
    /// This class defines parameters for memory initialization operations within a CUDA graph.
    /// Memory set nodes fill device memory with a specified value, useful for initializing
    /// buffers, clearing arrays, or setting up data structures. Supports both 1D and 2D
    /// pitched memory operations for efficient initialization of contiguous or strided memory regions.
    /// </remarks>
    public class MemsetNodeParams
    {
        /// <summary>
        /// Gets or sets the destination memory pointer to initialize.
        /// </summary>
        /// <value>
        /// Native pointer to device memory where the value will be written.
        /// Must point to valid, allocated device memory with sufficient size.
        /// </value>
        public nint Destination { get; set; }

        /// <summary>
        /// Gets or sets the value to write to each element.
        /// </summary>
        /// <value>
        /// The value to set for each element in the destination memory.
        /// Interpretation depends on ElementSize (1, 2, or 4 bytes per element).
        /// For 1-byte elements, only the low byte is used.
        /// </value>
        public uint Value { get; set; }

        /// <summary>
        /// Gets or sets the size of each element in bytes.
        /// </summary>
        /// <value>
        /// Element size in bytes. Must be 1, 2, or 4 bytes.
        /// - 1 byte: Sets individual bytes (memset8)
        /// - 2 bytes: Sets 16-bit values (memset16)
        /// - 4 bytes: Sets 32-bit values (memset32)
        /// </value>
        public uint ElementSize { get; set; }

        /// <summary>
        /// Gets or sets the width of the memory region in elements.
        /// </summary>
        /// <value>
        /// Number of elements per row to set. For 1D operations, this is the total count.
        /// For 2D operations, this is the number of elements in each row.
        /// Total elements = Width * Height.
        /// </value>
        public nuint Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the memory region in rows.
        /// </summary>
        /// <value>
        /// Number of rows for 2D memory operations. Set to 1 for 1D memset operations.
        /// Used with pitched memory to handle non-contiguous 2D arrays efficiently.
        /// </value>
        public nuint Height { get; set; }

        /// <summary>
        /// Gets or sets the pitch (stride) of the destination memory in bytes.
        /// </summary>
        /// <value>
        /// The byte offset between consecutive rows in pitched 2D memory.
        /// Must be >= Width * ElementSize. For 1D operations, typically equals Width * ElementSize.
        /// Pitch allows efficient handling of padded or aligned 2D memory layouts.
        /// </value>
        public nuint Pitch { get; set; }
    }
}
