// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;

namespace DotCompute.Core.Compute.Memory
{
    /// <summary>
    /// Memory pool for efficient buffer reuse and reduced GC pressure.
    /// Manages pooled memory buffers organized by size for optimal reuse patterns.
    /// </summary>
    internal class MemoryPool : IDisposable
    {
        private readonly ConcurrentDictionary<long, ConcurrentBag<HighPerformanceMemoryBuffer>> _pools = new();

        /// <summary>
        /// Rents a memory buffer from the pool or creates a new one if none are available.
        /// </summary>
        /// <param name="sizeInBytes">Requested size in bytes.</param>
        /// <param name="options">Memory allocation options.</param>
        /// <returns>Memory buffer ready for use.</returns>
        public HighPerformanceMemoryBuffer Rent(long sizeInBytes, MemoryOptions options)
        {
            // Round up to nearest power of 2 for better pooling
            var poolSize = RoundUpToPowerOfTwo(sizeInBytes);

            var pool = _pools.GetOrAdd(poolSize, _ => []);

            if (pool.TryTake(out var buffer))
            {
                buffer.Reset(sizeInBytes, options);
                return buffer;
            }

            return new HighPerformanceMemoryBuffer(sizeInBytes, options);
        }

        /// <summary>
        /// Rounds up a value to the nearest power of two for optimal pooling.
        /// </summary>
        /// <param name="value">Value to round up.</param>
        /// <returns>Next power of two greater than or equal to the input value.</returns>
        private static long RoundUpToPowerOfTwo(long value)
        {
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value |= value >> 32;
            return value + 1;
        }

        /// <summary>
        /// Disposes the memory pool and all contained buffers.
        /// </summary>
        public void Dispose()
        {
            foreach (var pool in _pools.Values)
            {
                while (pool.TryTake(out var buffer))
                {
                    buffer.Dispose();
                }
            }
            _pools.Clear();
        }
    }
}