// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory
{
    /// <summary>
    /// Specifies the type of memory allocation.
    /// </summary>
    public enum MemoryType
    {
        /// <summary>
        /// Host system memory (RAM).
        /// </summary>
        Host,

        /// <summary>
        /// Device memory (e.g., GPU VRAM).
        /// </summary>
        Device,

        /// <summary>
        /// Unified memory accessible from both host and device.
        /// </summary>
        Unified,

        /// <summary>
        /// Pinned host memory for faster transfers.
        /// </summary>
        Pinned,

        /// <summary>
        /// Shared memory within a compute unit.
        /// </summary>
        Shared,

        /// <summary>
        /// Constant memory for read-only data.
        /// </summary>
        Constant,

        /// <summary>
        /// Texture memory optimized for spatial locality.
        /// </summary>
        Texture,

        /// <summary>
        /// Managed memory with automatic migration.
        /// </summary>
        Managed
    }
}
