// <copyright file="DeviceBufferPool.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

using System;
namespace DotCompute.Core.Execution.Memory
{
    /// <summary>
    /// Device-specific buffer pool for efficient memory management.
    /// Manages a pool of reusable memory buffers for a single device to reduce allocation overhead.
    /// </summary>
    public sealed class DeviceBufferPool : IAsyncDisposable
    {
        private readonly string _deviceId;
        private readonly ConcurrentQueue<AbstractionsMemory.IUnifiedMemoryBuffer> _availableBuffers;
        private readonly ConcurrentDictionary<long, int> _allocationSizes;
        private long _totalAllocated;
        private long _totalAvailable;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceBufferPool"/> class.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device this pool manages.</param>
        /// <exception cref="ArgumentNullException">Thrown when deviceId is null.</exception>
        public DeviceBufferPool(string deviceId)
        {
            _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
            _availableBuffers = new ConcurrentQueue<AbstractionsMemory.IUnifiedMemoryBuffer>();
            _allocationSizes = new ConcurrentDictionary<long, int>();
        }

        /// <summary>
        /// Gets the device ID this pool manages.
        /// </summary>
        public string DeviceId => _deviceId;

        /// <summary>
        /// Allocates a buffer from the pool or creates a new one.
        /// First attempts to reuse an existing buffer from the pool that meets the size and options requirements.
        /// If no suitable buffer is available, creates a new buffer.
        /// </summary>
        /// <param name="sizeInBytes">The minimum size required for the buffer.</param>
        /// <param name="options">Memory options for the buffer.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A memory buffer that meets the specified requirements.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
        public async ValueTask<IUnifiedMemoryBuffer<byte>> AllocateBufferAsync(
            long sizeInBytes,
            MemoryOptions options,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Try to find a suitable buffer from the pool
            while (_availableBuffers.TryDequeue(out var buffer))
            {
                if (buffer.SizeInBytes >= sizeInBytes && buffer.Options == options)
                {
                    _ = Interlocked.Add(ref _totalAvailable, -buffer.SizeInBytes);
                    // Cast to the generic interface - buffers in pool should be byte buffers
                    if (buffer is IUnifiedMemoryBuffer<byte> typedBuffer)
                    {
                        return typedBuffer;
                    }
                    // If not the right type, continue searching
                    continue;
                }

                // Buffer doesn't match, dispose it
                await buffer.DisposeAsync();
            }

            // Create new buffer - TODO
            var newBuffer = new MemoryBuffer(sizeInBytes, options);
            _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);
            _ = _allocationSizes.AddOrUpdate(sizeInBytes, 1, (k, v) => v + 1);

            return newBuffer;
        }

        /// <summary>
        /// Returns a buffer to the pool for reuse.
        /// If the pool has been disposed, the buffer is disposed immediately.
        /// </summary>
        /// <param name="buffer">The buffer to return to the pool.</param>
        public void ReturnBuffer(AbstractionsMemory.IUnifiedMemoryBuffer buffer)
        {
            if (_disposed || buffer == null)
            {
                _ = (buffer?.DisposeAsync());
                return;
            }

            _availableBuffers.Enqueue(buffer);
            _ = Interlocked.Add(ref _totalAvailable, buffer.SizeInBytes);
        }

        /// <summary>
        /// Gets statistics for this device pool.
        /// </summary>
        /// <returns>Statistics including allocated bytes, available bytes, and allocation patterns.</returns>
        public DeviceMemoryStatistics GetStatistics()
        {
            return new DeviceMemoryStatistics
            {
                DeviceId = _deviceId,
                AllocatedBytes = _totalAllocated,
                AvailableBytes = _totalAvailable,
                PooledBufferCount = _availableBuffers.Count,
                AllocationSizeDistribution = new Dictionary<long, int>(_allocationSizes)
            };
        }

        /// <summary>
        /// Disposes all pooled buffers and clears the pool.
        /// </summary>
        /// <returns>A task representing the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            // Dispose all pooled buffers
            while (_availableBuffers.TryDequeue(out var buffer))
            {
                await buffer.DisposeAsync();
            }

            _allocationSizes.Clear();
            _disposed = true;
        }
    }
}
