// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.P2P.Models
{
    /// <summary>
    /// Represents the placement of a data chunk on a specific device.
    /// </summary>
    public sealed class CudaDataPlacement
    {
        /// <summary>
        /// Gets or sets the chunk ID.
        /// </summary>
        public Guid ChunkId { get; set; }

        /// <summary>
        /// Gets or sets the device ID where the chunk is placed.
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the size of the placement in bytes.
        /// </summary>
        public ulong Size { get; set; }

        /// <summary>
        /// Gets or sets the priority of this placement.
        /// </summary>
        public int Priority { get; set; }
    }
}
