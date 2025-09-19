// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

using System;
namespace DotCompute.Abstractions.Interfaces.Device
{
    /// <summary>
    /// Represents a memory allocation on a compute device, providing methods
    /// for data transfer between host and device memory, as well as device-to-device operations.
    /// </summary>
    public interface IDeviceMemory : IAsyncDisposable
    {
        /// <summary>
        /// Gets the total size of this memory allocation.
        /// </summary>
        /// <value>The memory allocation size in bytes.</value>
        public long SizeInBytes { get; }

        /// <summary>
        /// Gets the device that owns this memory allocation.
        /// </summary>
        /// <value>The associated compute device.</value>
        public IComputeDevice Device { get; }

        /// <summary>
        /// Gets the access mode specified when this memory was allocated.
        /// </summary>
        /// <value>The memory access mode (read-only, write-only, or read-write).</value>
        public MemoryAccess AccessMode { get; }

        /// <summary>
        /// Asynchronously writes data from host memory to device memory.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data to write.</typeparam>
        /// <param name="source">The source data to copy from host memory.</param>
        /// <param name="offset">The byte offset in device memory where writing should begin.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous write operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when offset is negative or would cause buffer overflow.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the memory access mode doesn't allow writing.</exception>
        public ValueTask WriteAsync<T>(
            ReadOnlyMemory<T> source,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged;

        /// <summary>
        /// Asynchronously reads data from device memory to host memory.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data to read.</typeparam>
        /// <param name="destination">The destination buffer in host memory.</param>
        /// <param name="offset">The byte offset in device memory where reading should begin.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous read operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when offset is negative or would cause buffer overflow.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the memory access mode doesn't allow reading.</exception>
        public ValueTask ReadAsync<T>(
            Memory<T> destination,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged;

        /// <summary>
        /// Asynchronously fills a region of device memory with a repeated pattern.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of the pattern data.</typeparam>
        /// <param name="pattern">The pattern value to fill memory with.</param>
        /// <param name="offset">The byte offset where filling should begin.</param>
        /// <param name="count">The number of pattern instances to write, or null to fill remaining memory.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous fill operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when offset or count is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the memory access mode doesn't allow writing.</exception>
        public ValueTask FillAsync<T>(
            T pattern,
            long offset = 0,
            long? count = null,
            CancellationToken cancellationToken = default) where T : unmanaged;

        /// <summary>
        /// Asynchronously copies data from this device memory to another device memory allocation.
        /// </summary>
        /// <param name="destination">The destination device memory allocation.</param>
        /// <param name="sourceOffset">The byte offset in source memory where copying should begin.</param>
        /// <param name="destinationOffset">The byte offset in destination memory where copying should begin.</param>
        /// <param name="sizeInBytes">The number of bytes to copy, or null to copy all remaining data.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous copy operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when destination is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when offsets or size are invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when access modes don't allow the operation.</exception>
        public ValueTask CopyToAsync(
            IDeviceMemory destination,
            long sourceOffset = 0,
            long destinationOffset = 0,
            long? sizeInBytes = null,
            CancellationToken cancellationToken = default);
    }
}
