// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Compute.Memory
{
    /// <summary>
    /// High-performance memory manager with basic optimizations.
    /// Provides memory allocation, pooling, and buffer management for high-performance computing scenarios.
    /// </summary>
    internal class HighPerformanceMemoryManager(IAccelerator accelerator, ILogger logger) : IMemoryManager, IDisposable
    {
        private readonly IAccelerator _accelerator = accelerator;
        private readonly ILogger _logger = logger;
        private readonly ConcurrentBag<HighPerformanceMemoryBuffer> _allocatedBuffers = [];
        private readonly MemoryPool _memoryPool = new();
        private long _totalAllocated;

        /// <summary>
        /// Gets the total amount of memory allocated by this manager.
        /// </summary>
        public long TotalAllocated => Interlocked.Read(ref _totalAllocated);

        /// <summary>
        /// Allocates a memory buffer with the specified size and options.
        /// </summary>
        /// <param name="sizeInBytes">Size of the buffer in bytes.</param>
        /// <param name="options">Memory allocation options.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Allocated memory buffer.</returns>
        public ValueTask<IMemoryBuffer> AllocateAsync(
            long sizeInBytes,
            MemoryOptions options = MemoryOptions.None,
            CancellationToken cancellationToken = default)
        {
            var buffer = _memoryPool.Rent(sizeInBytes, options);
            _allocatedBuffers.Add(buffer);
            _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);
            return ValueTask.FromResult<IMemoryBuffer>(buffer);
        }

        /// <summary>
        /// Allocates a buffer and copies data from the host memory.
        /// </summary>
        /// <typeparam name="T">Type of elements to copy.</typeparam>
        /// <param name="source">Source data to copy.</param>
        /// <param name="options">Memory allocation options.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Allocated buffer with copied data.</returns>
        public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
            ReadOnlyMemory<T> source,
            MemoryOptions options = MemoryOptions.None,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            var sizeInBytes = source.Length * Unsafe.SizeOf<T>();
            var buffer = _memoryPool.Rent(sizeInBytes, options);
            await buffer.CopyFromHostAsync(source, cancellationToken: cancellationToken).ConfigureAwait(false);
            _allocatedBuffers.Add(buffer);
            _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);
            return buffer;
        }

        /// <summary>
        /// Creates a view over an existing memory buffer.
        /// </summary>
        /// <param name="buffer">Parent buffer to create view from.</param>
        /// <param name="offset">Offset into the parent buffer.</param>
        /// <param name="length">Length of the view in bytes.</param>
        /// <returns>Memory buffer view.</returns>
        /// <exception cref="ArgumentException">Thrown when buffer is not a high-performance buffer.</exception>
        public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
        {
            if (buffer is not HighPerformanceMemoryBuffer hpBuffer)
            {
                throw new ArgumentException("Buffer must be a high-performance buffer", nameof(buffer));
            }
            return new HighPerformanceMemoryBufferView(hpBuffer, offset, length);
        }

        /// <summary>
        /// Allocates a typed buffer for the specified number of elements.
        /// </summary>
        /// <typeparam name="T">Type of elements to allocate for.</typeparam>
        /// <param name="count">Number of elements to allocate.</param>
        /// <returns>Allocated memory buffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when count is negative or zero.</exception>
        public async ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
            var sizeInBytes = count * Unsafe.SizeOf<T>();
            return await AllocateAsync(sizeInBytes).ConfigureAwait(false);
        }

        /// <summary>
        /// Copies data from host memory to device buffer.
        /// </summary>
        /// <typeparam name="T">Type of elements to copy.</typeparam>
        /// <param name="buffer">Destination buffer.</param>
        /// <param name="data">Source data to copy.</param>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);
            var memory = new ReadOnlyMemory<T>(data.ToArray());
            buffer.CopyFromHostAsync(memory).AsTask().Wait();
        }

        /// <summary>
        /// Copies data from device buffer to host memory.
        /// </summary>
        /// <typeparam name="T">Type of elements to copy.</typeparam>
        /// <param name="data">Destination span for copied data.</param>
        /// <param name="buffer">Source buffer to copy from.</param>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(buffer);
            var memory = new Memory<T>(new T[data.Length]);
            buffer.CopyToHostAsync(memory).AsTask().Wait();
            memory.Span.CopyTo(data);
        }

        /// <summary>
        /// Frees a memory buffer and returns it to the pool if possible.
        /// </summary>
        /// <param name="buffer">Buffer to free.</param>
        public void Free(IMemoryBuffer buffer)
        {
            if (buffer is HighPerformanceMemoryBuffer hpBuffer)
            {
                hpBuffer.Dispose();
            }
            else
            {
                buffer?.Dispose();
            }
        }

        /// <summary>
        /// Disposes the memory manager and all allocated buffers.
        /// </summary>
        public void Dispose()
        {
            _memoryPool.Dispose();
            foreach (var buffer in _allocatedBuffers)
            {
                buffer.Dispose();
            }
        }
    }
}