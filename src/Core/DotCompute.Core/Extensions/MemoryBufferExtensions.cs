// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Extensions
{

    /// <summary>
    /// Extension methods for IMemoryBuffer.
    /// </summary>
    public static class MemoryBufferExtensions
    {
        /// <summary>
        /// Writes data to the memory buffer from a host array.
        /// </summary>
        public static ValueTask WriteAsync<T>(
            this IMemoryBuffer buffer,
            T[] data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged => buffer.CopyFromHostAsync<T>(data.AsMemory(), offset, cancellationToken);

        /// <summary>
        /// Writes data to the memory buffer from a host memory.
        /// </summary>
        public static ValueTask WriteAsync<T>(
            this IMemoryBuffer buffer,
            ReadOnlyMemory<T> data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged => buffer.CopyFromHostAsync<T>(data, offset, cancellationToken);

        /// <summary>
        /// Reads data from the memory buffer to a host array.
        /// </summary>
        public static ValueTask ReadAsync<T>(
            this IMemoryBuffer buffer,
            T[] data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged => buffer.CopyToHostAsync<T>(data.AsMemory(), offset, cancellationToken);

        /// <summary>
        /// Reads data from the memory buffer to a host memory.
        /// </summary>
        public static ValueTask ReadAsync<T>(
            this IMemoryBuffer buffer,
            Memory<T> data,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged => buffer.CopyToHostAsync<T>(data, offset, cancellationToken);
    }
}
