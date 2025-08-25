// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Compute.Memory
{
    /// <summary>
    /// View over a high-performance memory buffer.
    /// Provides a window into a parent buffer without additional memory allocation.
    /// </summary>
    internal class HighPerformanceMemoryBufferView(HighPerformanceMemoryBuffer parent, long offset, long length) : IUnifiedMemoryBuffer
    {
        private readonly HighPerformanceMemoryBuffer _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        private readonly long _offset = offset;

        /// <summary>
        /// Gets the size of the view in bytes.
        /// </summary>
        public long SizeInBytes { get; } = length;

        /// <summary>
        /// Gets the memory options inherited from the parent buffer.
        /// </summary>
        public MemoryOptions Options { get; } = parent.Options;

        /// <summary>
        /// Gets a value indicating whether the parent buffer has been disposed.
        /// </summary>
        public bool IsDisposed => _parent.IsDisposed;

        /// <summary>
        /// Copies data from host memory to this buffer view.
        /// </summary>
        /// <typeparam name="T">Type of elements to copy.</typeparam>
        /// <param name="source">Source data to copy from.</param>
        /// <param name="offset">Offset into this view in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Completed task when copy finishes.</returns>
        public ValueTask CopyFromHostAsync<T>(
            ReadOnlyMemory<T> source,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
            => _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);

        /// <summary>
        /// Copies data from this buffer view to host memory.
        /// </summary>
        /// <typeparam name="T">Type of elements to copy.</typeparam>
        /// <param name="destination">Destination memory to copy to.</param>
        /// <param name="offset">Offset into this view in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Completed task when copy finishes.</returns>
        public ValueTask CopyToHostAsync<T>(
            Memory<T> destination,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
            => _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);

        /// <summary>
        /// Disposes the view. Note: This does not dispose the parent buffer.
        /// </summary>
        public void Dispose()
        {
            // Views don't dispose the parent
        }

        /// <summary>
        /// Asynchronously disposes the view. Note: This does not dispose the parent buffer.
        /// </summary>
        /// <returns>Completed task.</returns>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
