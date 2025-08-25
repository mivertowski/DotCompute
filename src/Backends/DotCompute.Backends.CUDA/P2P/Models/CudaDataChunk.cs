// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.P2P.Models
{
    /// <summary>
    /// Represents a chunk of data for P2P transfer operations.
    /// </summary>
    public sealed class CudaDataChunk
    {
        /// <summary>
        /// Gets or sets the unique chunk ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the size of the chunk in bytes.
        /// </summary>
        public ulong Size { get; set; }

        /// <summary>
        /// Gets or sets the priority for transfer scheduling.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the name or description of the chunk.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}