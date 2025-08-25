// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Backends.CUDA.Memory.Models
{
    /// <summary>
    /// CUDA async memory buffer implementation with stream-ordered allocation.
    /// </summary>
    internal sealed class CudaAsyncMemoryBuffer : IUnifiedMemoryBuffer
    {
        private readonly IntPtr _devicePtr;
        private readonly long _sizeInBytes;
        private readonly IntPtr _stream;
        private readonly CudaAsyncMemoryManager _manager;
        private readonly MemoryOptions _options;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the CudaAsyncMemoryBuffer class.
        /// </summary>
        /// <param name="devicePtr">The device pointer to the allocated memory.</param>
        /// <param name="sizeInBytes">The size of the allocation in bytes.</param>
        /// <param name="stream">The stream on which the allocation was made.</param>
        /// <param name="manager">The memory manager that owns this buffer.</param>
        /// <param name="options">The memory allocation options.</param>
        public CudaAsyncMemoryBuffer(
            IntPtr devicePtr,
            long sizeInBytes,
            IntPtr stream,
            CudaAsyncMemoryManager manager,
            MemoryOptions options)
        {
            _devicePtr = devicePtr;
            _sizeInBytes = sizeInBytes;
            _stream = stream;
            _manager = manager;
            _options = options;
        }

        /// <summary>
        /// Gets the device pointer to the allocated memory.
        /// </summary>
        public IntPtr DevicePointer => _devicePtr;

        /// <summary>
        /// Gets the size of the buffer in bytes.
        /// </summary>
        public long SizeInBytes => _sizeInBytes;

        /// <summary>
        /// Gets the memory allocation options.
        /// </summary>
        public MemoryOptions Options => _options;

        /// <summary>
        /// Gets whether the buffer has been disposed.
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Copies data from host memory to this buffer asynchronously.
        /// </summary>
        public async ValueTask CopyFromHostAsync<T>(
            ReadOnlyMemory<T> source,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            
            var destPtr = _devicePtr + (nint)offset;
            await _manager.CopyFromAsyncOnStream(source, destPtr, _stream, cancellationToken);
        }

        /// <summary>
        /// Copies data from this buffer to host memory asynchronously.
        /// </summary>
        public async ValueTask CopyToHostAsync<T>(
            Memory<T> destination,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            
            var srcPtr = _devicePtr + (nint)offset;
            await _manager.CopyToAsyncOnStream(srcPtr, destination, _stream, cancellationToken);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, GetType());
        }

        /// <summary>
        /// Disposes the buffer and frees the allocated memory.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _manager.FreeAsyncOnStream(_devicePtr, _stream);
            }
        }

        /// <summary>
        /// Disposes the buffer asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                await Task.Run(() => _manager.FreeAsyncOnStream(_devicePtr, _stream));
            }
        }
    }
}
