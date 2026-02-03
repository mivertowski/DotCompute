// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Extensions
{

    /// <summary>
    /// Extension methods for IUnifiedMemoryBuffer.
    /// </summary>
    public static class MemoryBufferExtensions
    {
        /// <summary>
        /// Writes data to the memory buffer from a host array at the specified offset.
        /// </summary>
        /// <typeparam name="T">The unmanaged element type.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="data">The source array to copy from.</param>
        /// <param name="offset">The offset in bytes where to start copying.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the copy operation.</returns>
        public static ValueTask WriteAsync<T>(
            this IUnifiedMemoryBuffer<T> buffer,
            T[] data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged
            // Use the non-generic interface's offset-supporting CopyFromAsync
            => ((IUnifiedMemoryBuffer)buffer).CopyFromAsync<T>(data.AsMemory(), offset, cancellationToken);

        /// <summary>
        /// Writes data to the memory buffer from a host memory at the specified offset.
        /// </summary>
        /// <typeparam name="T">The unmanaged element type.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="data">The source memory to copy from.</param>
        /// <param name="offset">The offset in bytes where to start copying.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the copy operation.</returns>
        public static ValueTask WriteAsync<T>(
            this IUnifiedMemoryBuffer<T> buffer,
            ReadOnlyMemory<T> data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged
            // Use the non-generic interface's offset-supporting CopyFromAsync
            => ((IUnifiedMemoryBuffer)buffer).CopyFromAsync<T>(data, offset, cancellationToken);

        /// <summary>
        /// Reads data from the memory buffer to a host array from the specified offset.
        /// </summary>
        /// <typeparam name="T">The unmanaged element type.</typeparam>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="data">The destination array to copy to.</param>
        /// <param name="offset">The offset in bytes where to start reading from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the copy operation.</returns>
        public static ValueTask ReadAsync<T>(
            this IUnifiedMemoryBuffer<T> buffer,
            T[] data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged
            // Use the non-generic interface's offset-supporting CopyToAsync
            => ((IUnifiedMemoryBuffer)buffer).CopyToAsync<T>(data.AsMemory(), offset, cancellationToken);

        /// <summary>
        /// Reads data from the memory buffer to a host memory from the specified offset.
        /// </summary>
        /// <typeparam name="T">The unmanaged element type.</typeparam>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="data">The destination memory to copy to.</param>
        /// <param name="offset">The offset in bytes where to start reading from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the copy operation.</returns>
        public static ValueTask ReadAsync<T>(
            this IUnifiedMemoryBuffer<T> buffer,
            Memory<T> data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged
            // Use the non-generic interface's offset-supporting CopyToAsync
            => ((IUnifiedMemoryBuffer)buffer).CopyToAsync<T>(data, offset, cancellationToken);
    }
}
