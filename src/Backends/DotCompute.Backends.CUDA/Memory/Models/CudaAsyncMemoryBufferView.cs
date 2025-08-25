// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Backends.CUDA.Memory.Models
{
    /// <summary>
    /// View over a CUDA async memory buffer.
    /// </summary>
    internal sealed class CudaAsyncMemoryBufferView : IMemoryBuffer
    {
        private readonly CudaAsyncMemoryBuffer _parent;
        private readonly long _offset;
        private readonly long _length;

        /// <summary>
        /// Initializes a new instance of the CudaAsyncMemoryBufferView class.
        /// </summary>
        /// <param name="parent">The parent buffer to create a view over.</param>
        /// <param name="offset">The offset in bytes from the start of the parent buffer.</param>
        /// <param name="length">The length of the view in bytes.</param>
        public CudaAsyncMemoryBufferView(CudaAsyncMemoryBuffer parent, long offset, long length)
        {
            _parent = parent;
            _offset = offset;
            _length = length;
        }

        /// <summary>
        /// Gets the size of the view in bytes.
        /// </summary>
        public long SizeInBytes => _length;

        /// <summary>
        /// Gets the memory options from the parent buffer.
        /// </summary>
        public MemoryOptions Options => _parent.Options;

        /// <summary>
        /// Gets whether the parent buffer has been disposed.
        /// </summary>
        public bool IsDisposed => _parent.IsDisposed;

        /// <summary>
        /// Copies data from host memory to this view asynchronously.
        /// </summary>
        public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
            => _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);

        /// <summary>
        /// Copies data from this view to host memory asynchronously.
        /// </summary>
        public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
            => _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);

        /// <summary>
        /// Disposes the view. Note: The view doesn't own memory, so this is a no-op.
        /// </summary>
        public void Dispose() 
        { 
            // View doesn't own memory
        }

        /// <summary>
        /// Disposes the view asynchronously. Note: The view doesn't own memory, so this is a no-op.
        /// </summary>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}