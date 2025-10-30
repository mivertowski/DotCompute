// <copyright file="ExecutionMemoryManager.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Concurrent;

namespace DotCompute.Core.Execution.Memory
{
    /// <summary>
    /// Global memory allocator and buffer manager for execution plans.
    /// Provides centralized management of device buffer pools and execution-scoped buffers.
    /// </summary>
    public static class ExecutionMemoryManager
    {
        private static readonly ConcurrentDictionary<string, DeviceBufferPool> _devicePools = new();
        private static readonly ConcurrentDictionary<Guid, List<AbstractionsMemory.IUnifiedMemoryBuffer>> _executionBuffers = new();

        /// <summary>
        /// Gets or creates a buffer pool for the specified device.
        /// </summary>
        /// <param name="deviceId">The unique identifier of the device.</param>
        /// <returns>A <see cref="DeviceBufferPool"/> instance for the specified device.</returns>
        public static DeviceBufferPool GetDevicePool(string deviceId) => _devicePools.GetOrAdd(deviceId, id => new DeviceBufferPool(id));

        /// <summary>
        /// Allocates buffers for an execution plan.
        /// </summary>
        /// <param name="executionId">The unique identifier for the execution.</param>
        /// <param name="requests">The collection of buffer allocation requests.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A list of allocated memory buffers.</returns>
        public static async ValueTask<List<AbstractionsMemory.IUnifiedMemoryBuffer>> AllocateExecutionBuffersAsync(
            Guid executionId,
            IEnumerable<BufferAllocationRequest> requests,
            CancellationToken cancellationToken = default)
        {
            var buffers = new List<AbstractionsMemory.IUnifiedMemoryBuffer>();

            foreach (var request in requests)
            {
                var pool = GetDevicePool(request.DeviceId);
                var buffer = await pool.AllocateBufferAsync(request.SizeInBytes, request.Options, cancellationToken);
                // IUnifiedMemoryBuffer<T> inherits from IUnifiedMemoryBuffer
                // So we can safely cast and add to the non-generic list
                buffers.Add(buffer as AbstractionsMemory.IUnifiedMemoryBuffer ?? throw new InvalidCastException("Buffer must implement IUnifiedMemoryBuffer"));
            }

            _executionBuffers[executionId] = buffers;
            return buffers;
        }

        /// <summary>
        /// Releases all buffers associated with an execution.
        /// </summary>
        /// <param name="executionId">The unique identifier for the execution.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async ValueTask ReleaseExecutionBuffersAsync(Guid executionId)
        {
            if (_executionBuffers.TryRemove(executionId, out var buffers))
            {
                foreach (var buffer in buffers)
                {
                    await buffer.DisposeAsync();
                }
            }
        }

        /// <summary>
        /// Gets memory usage statistics across all device pools.
        /// </summary>
        /// <returns>Comprehensive memory statistics for all managed devices.</returns>
        public static ExecutionMemoryStatistics GetMemoryStatistics()
        {
            var deviceStats = new Dictionary<string, DeviceMemoryStatistics>();
            long totalAllocated = 0;
            long totalAvailable = 0;

            foreach (var kvp in _devicePools)
            {
                var stats = kvp.Value.GetStatistics();
                deviceStats[kvp.Key] = stats;
                totalAllocated += stats.AllocatedBytes;
                totalAvailable += stats.AvailableBytes;
            }

            return new ExecutionMemoryStatistics
            {
                DeviceStatistics = deviceStats,
                TotalAllocatedBytes = totalAllocated,
                TotalAvailableBytes = totalAvailable,
                ActiveExecutions = _executionBuffers.Count
            };
        }
    }
}
