// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions
{

    /// <summary>
    /// Extended memory buffer interface with synchronous operations and additional capabilities.
    /// </summary>
    public interface ISyncMemoryBuffer : IUnifiedMemoryBuffer, IDisposable
    {
        /// <summary>
        /// Gets a pointer to the host memory, if available.
        /// </summary>
        public unsafe void* GetHostPointer();

        /// <summary>
        /// Gets a span over the buffer memory, if available.
        /// </summary>
        public unsafe Span<T> AsSpan<T>() where T : unmanaged;

        /// <summary>
        /// Creates a slice of this buffer.
        /// </summary>
        public ISyncMemoryBuffer Slice(long offset, long length);

        /// <summary>
        /// Gets whether the buffer has been disposed.
        /// </summary>
        public new bool IsDisposed { get; }
    }
}
