// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.Memory.Models
{
    /// <summary>
    /// Information about a CUDA memory pool.
    /// </summary>
    internal sealed class MemoryPoolInfo
    {
        /// <summary>
        /// Gets or initializes the handle to the memory pool.
        /// </summary>
        public IntPtr Pool { get; init; }

        /// <summary>
        /// Gets or initializes whether this is the default memory pool.
        /// </summary>
        public bool IsDefault { get; init; }

        /// <summary>
        /// Gets or initializes the release threshold in bytes.
        /// Memory is returned to the OS when this threshold is exceeded.
        /// </summary>
        public ulong ReleaseThreshold { get; init; }

        /// <summary>
        /// Gets or initializes when the pool was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }
    }
}