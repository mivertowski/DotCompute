// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA graph update parameters.
    /// </summary>
    public sealed class CudaGraphUpdateParameters
    {
        /// <summary>
        /// Gets or sets the update node params.
        /// </summary>
        /// <value>The update node params.</value>
        public bool UpdateNodeParams { get; set; } = true;
        /// <summary>
        /// Gets or sets the update kernel params.
        /// </summary>
        /// <value>The update kernel params.</value>
        public bool UpdateKernelParams { get; set; } = true;
        /// <summary>
        /// Gets or sets the preserve topology.
        /// </summary>
        /// <value>The preserve topology.</value>
        public bool PreserveTopology { get; set; } = true;
        /// <summary>
        /// Gets or sets the source graph.
        /// </summary>
        /// <value>The source graph.</value>
        public IntPtr SourceGraph { get; set; }
    }
}