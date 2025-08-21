// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;

namespace DotCompute.Core.Compute.Memory
{
    /// <summary>
    /// High-performance memory buffer with aligned allocation.
    /// Provides SIMD-optimized memory allocation with 64-byte alignment for optimal performance.
    /// </summary>
    internal class HighPerformanceMemoryBuffer : IMemoryBuffer
    {
        private byte[] _data = null!;
        private GCHandle _handle;
        private IntPtr _alignedPtr;
        private bool _disposed;
        private long _sizeInBytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="HighPerformanceMemoryBuffer"/> class.
        /// </summary>
        /// <param name="sizeInBytes">Size of the buffer in bytes.</param>
        /// <param name="options">Memory allocation options.</param>
        public HighPerformanceMemoryBuffer(long sizeInBytes, MemoryOptions options)
        {
            Reset(sizeInBytes, options);
        }

        /// <summary>
        /// Gets the size of the buffer in bytes.
        /// </summary>
        public long SizeInBytes => _sizeInBytes;

        /// <summary>
        /// Gets the memory allocation options.
        /// </summary>
        public MemoryOptions Options { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the buffer has been disposed.
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Resets the buffer with new size and options, reusing the existing instance.
        /// </summary>
        /// <param name="sizeInBytes">New size in bytes.</param>
        /// <param name="options">New memory options.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the buffer has been disposed.</exception>
        public void Reset(long sizeInBytes, MemoryOptions options)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HighPerformanceMemoryBuffer));
            }

            if (_data != null)
            {
                if (_handle.IsAllocated)
                {
                    _handle.Free();
                }
            }

            // Align to 64-byte boundaries for optimal SIMD performance
            const int alignment = 64;
            var allocSize = (int)(sizeInBytes + alignment - 1);

            _data = new byte[allocSize];
            _handle = GCHandle.Alloc(_data, GCHandleType.Pinned);

            var addr = _handle.AddrOfPinnedObject();
            var alignedAddr = (addr.ToInt64() + alignment - 1) & ~(alignment - 1);
            _alignedPtr = new IntPtr(alignedAddr);

            _sizeInBytes = sizeInBytes;
            Options = options;
        }

        /// <summary>
        /// Copies data from host memory to this buffer.
        /// </summary>
        /// <typeparam name="T">Type of elements to copy.</typeparam>
        /// <param name="source">Source data to copy from.</param>
        /// <param name="offset">Offset into this buffer in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Completed task when copy finishes.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the buffer has been disposed.</exception>
        public ValueTask CopyFromHostAsync<T>(
            ReadOnlyMemory<T> source,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HighPerformanceMemoryBuffer));
            }

            var sourceBytes = MemoryMarshal.AsBytes(source.Span);
            var destPtr = _alignedPtr + (int)offset;

            unsafe
            {
                var destSpan = new Span<byte>((void*)destPtr, sourceBytes.Length);
                sourceBytes.CopyTo(destSpan);
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Copies data from this buffer to host memory.
        /// </summary>
        /// <typeparam name="T">Type of elements to copy.</typeparam>
        /// <param name="destination">Destination memory to copy to.</param>
        /// <param name="offset">Offset into this buffer in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Completed task when copy finishes.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the buffer has been disposed.</exception>
        public ValueTask CopyToHostAsync<T>(
            Memory<T> destination,
            long offset = 0,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HighPerformanceMemoryBuffer));
            }

            var destBytes = MemoryMarshal.AsBytes(destination.Span);
            var sourcePtr = _alignedPtr + (int)offset;

            unsafe
            {
                var sourceSpan = new Span<byte>((void*)sourcePtr, destBytes.Length);
                sourceSpan.CopyTo(destBytes);
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Asynchronously disposes the buffer.
        /// </summary>
        /// <returns>Completed task when disposal finishes.</returns>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Disposes the buffer and releases all associated resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_handle.IsAllocated)
                {
                    _handle.Free();
                }

                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}