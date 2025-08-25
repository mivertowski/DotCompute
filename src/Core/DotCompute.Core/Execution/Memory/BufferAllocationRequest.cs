// <copyright file="BufferAllocationRequest.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Execution.Memory
{
    /// <summary>
    /// Represents a request for buffer allocation on a specific device.
    /// Contains all parameters needed to allocate a memory buffer including size, device, and options.
    /// </summary>
    public sealed class BufferAllocationRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the device where the buffer should be allocated.
        /// </summary>
        public required string DeviceId { get; init; }

        /// <summary>
        /// Gets or sets the size of the buffer in bytes.
        /// </summary>
        public required long SizeInBytes { get; init; }

        /// <summary>
        /// Gets or sets the memory options for the buffer allocation.
        /// Defaults to <see cref="AbstractionsMemory.MemoryOptions.None"/>.
        /// </summary>
        public AbstractionsMemory.MemoryOptions Options { get; init; } = AbstractionsMemory.MemoryOptions.None;
    }
}
