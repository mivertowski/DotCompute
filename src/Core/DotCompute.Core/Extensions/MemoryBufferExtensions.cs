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
        /// Writes data to the memory buffer from a host array.
        /// </summary>
        public static ValueTask WriteAsync<T>(
            this IUnifiedMemoryBuffer<T> buffer,
            T[] data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged =>
            // For now, just copy to the buffer 
            // TODO: handle offset properly
            buffer.CopyFromAsync(data.AsMemory(), cancellationToken);

        /// <summary>
        /// Writes data to the memory buffer from a host memory.
        /// </summary>
        public static ValueTask WriteAsync<T>(
            this IUnifiedMemoryBuffer<T> buffer,
            ReadOnlyMemory<T> data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged =>
            // For now, just copy to the buffer 
            // TODO: handle offset properly
            buffer.CopyFromAsync(data, cancellationToken);

        /// <summary>
        /// Reads data from the memory buffer to a host array.
        /// </summary>
        public static ValueTask ReadAsync<T>(
            this IUnifiedMemoryBuffer<T> buffer,
            T[] data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged =>
            // For now, just copy from the buffer 
            // TODO: handle offset properly
            buffer.CopyToAsync(data.AsMemory(), cancellationToken);

        /// <summary>
        /// Reads data from the memory buffer to a host memory.
        /// </summary>
        public static ValueTask ReadAsync<T>(
            this IUnifiedMemoryBuffer<T> buffer,
            Memory<T> data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged =>
            // For now, just copy from the buffer 
            // TODO: handle offset properly
            buffer.CopyToAsync(data, cancellationToken);
    }
}
