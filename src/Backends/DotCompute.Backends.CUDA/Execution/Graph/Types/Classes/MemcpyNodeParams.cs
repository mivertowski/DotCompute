// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Classes
{
    /// <summary>
    /// Memory copy node parameters
    /// </summary>
    public class MemcpyNodeParams
    {
        /// <summary>
        /// Gets or sets the source.
        /// </summary>
        /// <value>The source.</value>
        public nint Source { get; set; }
        /// <summary>
        /// Gets or sets the destination.
        /// </summary>
        /// <value>The destination.</value>
        public nint Destination { get; set; }
        /// <summary>
        /// Gets or sets the byte count.
        /// </summary>
        /// <value>The byte count.</value>
        public nuint ByteCount { get; set; }
        /// <summary>
        /// Gets or sets the kind.
        /// </summary>
        /// <value>The kind.</value>
        public MemcpyKind Kind { get; set; }
    }
}