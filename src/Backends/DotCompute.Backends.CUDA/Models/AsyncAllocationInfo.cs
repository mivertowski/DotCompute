// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.Memory.Models
{
    /// <summary>
    /// Information about an asynchronous CUDA memory allocation.
    /// </summary>
    internal sealed class AsyncAllocationInfo
    {
        /// <summary>
        /// Gets or initializes the device pointer to the allocated memory.
        /// </summary>
        public IntPtr Pointer { get; init; }

        /// <summary>
        /// Gets or initializes the size of the allocation in bytes.
        /// </summary>
        public long Size { get; init; }

        /// <summary>
        /// Gets or initializes the stream on which the allocation was made.
        /// </summary>
        public IntPtr Stream { get; init; }

        /// <summary>
        /// Gets or initializes the memory pool from which the allocation was made.
        /// </summary>
        public IntPtr Pool { get; init; }

        /// <summary>
        /// Gets or initializes when the allocation was made.
        /// </summary>
        public DateTimeOffset AllocatedAt { get; init; }
    }
}