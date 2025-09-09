// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory
{
    /// <summary>
    /// Manages memory pools for efficient allocation and reuse of CUDA memory.
    /// Reduces allocation overhead and memory fragmentation.
    /// </summary>
    public sealed class CudaMemoryPoolManager : IDisposable
    {
        private readonly CudaContext _context;
        private readonly CudaDevice _device;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<int, MemoryPool> _pools;
        private readonly SemaphoreSlim _allocationSemaphore;
        private readonly Timer _maintenanceTimer;
        private bool _disposed;

        // Pool configuration
        private const int MIN_POOL_SIZE = 256;                    // 256 bytes
        private const int MAX_POOL_SIZE = 256 * 1024 * 1024;     // 256 MB
        private const int POOL_SIZE_MULTIPLIER = 2;              // Size classes double
        private const int MAX_BLOCKS_PER_POOL = 100;             // Maximum blocks per size class
        private const int MAINTENANCE_INTERVAL_SECONDS = 60;     // Cleanup interval

        // Statistics
        private long _totalAllocations;
        private long _poolHits;
        private long _poolMisses;
        private long _totalBytesAllocated;
        private long _totalBytesInPools;

        public CudaMemoryPoolManager(CudaContext context, CudaDevice device, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pools = new ConcurrentDictionary<int, MemoryPool>();
            _allocationSemaphore = new SemaphoreSlim(1, 1);

            InitializePools();

            // Setup maintenance timer for periodic cleanup
            _maintenanceTimer = new Timer(
                PerformMaintenance,
                null,
                TimeSpan.FromSeconds(MAINTENANCE_INTERVAL_SECONDS),
                TimeSpan.FromSeconds(MAINTENANCE_INTERVAL_SECONDS));

            _logger.LogInformation("Memory pool manager initialized with {PoolCount} size classes", _pools.Count);
        }

        /// <summary>
        /// Gets the pool hit rate (efficiency metric).
        /// </summary>
        public double PoolHitRate => _totalAllocations > 0 ? (double)_poolHits / _totalAllocations : 0;

        /// <summary>
        /// Gets the total bytes currently in pools.
        /// </summary>
        public long TotalBytesInPools => _totalBytesInPools;

        /// <summary>
        /// Gets the total bytes allocated through the pool manager.
        /// </summary>
        public long TotalBytesAllocated => _totalBytesAllocated;

        private void InitializePools()
        {
            // Create pools for power-of-2 sizes
            var size = MIN_POOL_SIZE;
            while (size <= MAX_POOL_SIZE)
            {
                _pools[size] = new MemoryPool(size, MAX_BLOCKS_PER_POOL, _logger);
                size *= POOL_SIZE_MULTIPLIER;
            }
        }

        /// <summary>
        /// Allocates memory from the pool or creates new allocation if pool is empty.
        /// </summary>
        public async Task<IPooledMemoryBuffer> AllocateAsync(
            long sizeInBytes,
            bool zeroMemory = false,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _ = Interlocked.Increment(ref _totalAllocations);

            // Find appropriate pool size (round up to nearest power of 2)
            var poolSize = GetPoolSize(sizeInBytes);


            if (poolSize > MAX_POOL_SIZE)
            {
                // Too large for pool, allocate directly
                _ = Interlocked.Increment(ref _poolMisses);
                return await AllocateDirectAsync(sizeInBytes, zeroMemory, cancellationToken);
            }

            if (!_pools.TryGetValue(poolSize, out var pool))
            {
                // Should not happen with proper initialization
                _logger.LogWarning("Pool for size {Size} not found, allocating directly", poolSize);
                _ = Interlocked.Increment(ref _poolMisses);
                return await AllocateDirectAsync(sizeInBytes, zeroMemory, cancellationToken);
            }

            // Try to get from pool
            var buffer = await pool.TryGetAsync(cancellationToken);


            if (buffer != null)
            {
                _ = Interlocked.Increment(ref _poolHits);
                _ = Interlocked.Add(ref _totalBytesAllocated, poolSize);


                if (zeroMemory)
                {
                    await ZeroMemoryAsync(buffer.DevicePointer, poolSize, cancellationToken);
                }


                _logger.LogTrace("Allocated {Size} bytes from pool (hit rate: {HitRate:P2})",

                    poolSize, PoolHitRate);


                return new PooledMemoryBuffer(this, buffer, sizeInBytes, poolSize);
            }

            // Pool is empty, allocate new block
            _ = Interlocked.Increment(ref _poolMisses);
            return await AllocateNewPoolBlockAsync(pool, sizeInBytes, zeroMemory, cancellationToken);
        }

        /// <summary>
        /// Returns memory to the pool for reuse.
        /// </summary>
        internal void ReturnToPool(MemoryBlock block, int poolSize)
        {
            if (_disposed || block == null)
            {
                return;
            }


            if (_pools.TryGetValue(poolSize, out var pool))
            {
                if (!pool.TryReturn(block))
                {
                    // Pool is full, free the memory
                    FreeMemoryBlock(block);
                }
                else
                {
                    _logger.LogTrace("Returned {Size} bytes to pool", poolSize);
                }
            }
            else
            {
                // Pool not found, free directly
                FreeMemoryBlock(block);
            }
        }

        private async Task<IPooledMemoryBuffer> AllocateDirectAsync(
            long sizeInBytes,
            bool zeroMemory,
            CancellationToken cancellationToken)
        {
            var devicePtr = IntPtr.Zero;


            await Task.Run(() =>
            {
                var result = CudaRuntime.cudaMalloc(ref devicePtr, (ulong)sizeInBytes);
                CudaRuntime.CheckError(result, "allocating memory directly");


                if (zeroMemory)
                {
                    result = CudaRuntime.cudaMemset(devicePtr, 0, (nuint)sizeInBytes);
                    CudaRuntime.CheckError(result, "zeroing memory");
                }
            }, cancellationToken);

            _ = Interlocked.Add(ref _totalBytesAllocated, sizeInBytes);


            var block = new MemoryBlock(devicePtr, sizeInBytes);
            return new PooledMemoryBuffer(this, block, sizeInBytes, -1); // -1 indicates no pool
        }

        private async Task<IPooledMemoryBuffer> AllocateNewPoolBlockAsync(
            MemoryPool pool,
            long requestedSize,
            bool zeroMemory,
            CancellationToken cancellationToken)
        {
            await _allocationSemaphore.WaitAsync(cancellationToken);
            try
            {
                var devicePtr = IntPtr.Zero;
                var poolSize = pool.BlockSize;


                await Task.Run(() =>
                {
                    var result = CudaRuntime.cudaMalloc(ref devicePtr, (ulong)poolSize);
                    CudaRuntime.CheckError(result, $"allocating {poolSize} bytes for pool");


                    if (zeroMemory)
                    {
                        result = CudaRuntime.cudaMemset(devicePtr, 0, (nuint)poolSize);
                        CudaRuntime.CheckError(result, "zeroing pool memory");
                    }
                }, cancellationToken);

                _ = Interlocked.Add(ref _totalBytesAllocated, poolSize);
                _ = Interlocked.Add(ref _totalBytesInPools, poolSize);


                var block = new MemoryBlock(devicePtr, poolSize);


                _logger.LogDebug("Allocated new {Size} byte block for pool", poolSize);


                return new PooledMemoryBuffer(this, block, requestedSize, poolSize);
            }
            finally
            {
                _ = _allocationSemaphore.Release();
            }
        }

        private async Task ZeroMemoryAsync(IntPtr ptr, long size, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var result = CudaRuntime.cudaMemset(ptr, 0, (nuint)size);
                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to zero memory: {Error}", result);
                }
            }, cancellationToken);
        }

        private void FreeMemoryBlock(MemoryBlock block)
        {
            if (block.DevicePointer == IntPtr.Zero)
            {
                return;
            }


            var result = CudaRuntime.cudaFree(block.DevicePointer);
            if (result == CudaError.Success)
            {
                _ = Interlocked.Add(ref _totalBytesInPools, -block.Size);
                _logger.LogTrace("Freed {Size} byte memory block", block.Size);
            }
            else
            {
                _logger.LogWarning("Failed to free memory block: {Error}", result);
            }
        }

        private static int GetPoolSize(long requestedSize)
        {
            // Round up to nearest power of 2
            if (requestedSize <= MIN_POOL_SIZE)
            {

                return MIN_POOL_SIZE;
            }


            var size = MIN_POOL_SIZE;
            while (size < requestedSize && size < MAX_POOL_SIZE)
            {
                size *= POOL_SIZE_MULTIPLIER;
            }


            return size;
        }

        private void PerformMaintenance(object? state)
        {
            if (_disposed)
            {
                return;
            }


            try
            {
                var freedBlocks = 0;
                var freedBytes = 0L;

                foreach (var pool in _pools.Values)
                {
                    var (blocks, bytes) = pool.TrimExcess();
                    freedBlocks += blocks;
                    freedBytes += bytes;
                }

                if (freedBlocks > 0)
                {
                    _ = Interlocked.Add(ref _totalBytesInPools, -freedBytes);
                    _logger.LogDebug("Pool maintenance freed {Blocks} blocks ({Bytes:N0} bytes)",

                        freedBlocks, freedBytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during pool maintenance");
            }
        }

        /// <summary>
        /// Gets pool statistics for monitoring.
        /// </summary>
        public MemoryPoolStatistics GetStatistics()
        {
            var poolStats = new List<PoolSizeStatistics>();


            foreach (var kvp in _pools.OrderBy(p => p.Key))
            {
                var pool = kvp.Value;
                poolStats.Add(new PoolSizeStatistics
                {
                    PoolSize = kvp.Key,
                    AvailableBlocks = pool.AvailableCount,
                    TotalBlocks = pool.TotalCount,
                    BytesInPool = pool.TotalBytes
                });
            }

            return new MemoryPoolStatistics
            {
                TotalAllocations = _totalAllocations,
                PoolHits = _poolHits,
                PoolMisses = _poolMisses,
                HitRate = PoolHitRate,
                TotalBytesAllocated = _totalBytesAllocated,
                TotalBytesInPools = _totalBytesInPools,
                PoolStatistics = poolStats
            };
        }

        /// <summary>
        /// Clears all pools and frees memory.
        /// </summary>
        public void ClearPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }


            _totalBytesInPools = 0;
            _logger.LogInformation("Cleared all memory pools");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            _maintenanceTimer?.Dispose();

            // Clear and dispose all pools

            foreach (var pool in _pools.Values)
            {
                pool.Dispose();
            }


            _pools.Clear();
            _allocationSemaphore?.Dispose();
            _disposed = true;

            _logger.LogInformation(
                "Disposed memory pool manager. Stats: {Allocations} allocations, " +
                "{HitRate:P2} hit rate, {BytesAllocated:N0} bytes allocated",
                _totalAllocations, PoolHitRate, _totalBytesAllocated);
        }

        /// <summary>
        /// Internal memory pool for a specific size class.
        /// </summary>
        private sealed class MemoryPool : IDisposable
        {
            private readonly ConcurrentBag<MemoryBlock> _availableBlocks;
            private readonly HashSet<IntPtr> _allBlocks;
            private readonly SemaphoreSlim _lock;
            private readonly ILogger _logger;


            public int BlockSize { get; }
            public int MaxBlocks { get; }
            public int AvailableCount => _availableBlocks.Count;
            public int TotalCount => _allBlocks.Count;
            public long TotalBytes => (long)TotalCount * BlockSize;

            public MemoryPool(int blockSize, int maxBlocks, ILogger logger)
            {
                BlockSize = blockSize;
                MaxBlocks = maxBlocks;
                _logger = logger;
                _availableBlocks = [];
                _allBlocks = [];
                _lock = new SemaphoreSlim(1, 1);
            }

            public Task<MemoryBlock?> TryGetAsync(CancellationToken cancellationToken)
            {
                if (_availableBlocks.TryTake(out var block))
                {
                    return Task.FromResult<MemoryBlock?>(block);
                }


                return Task.FromResult<MemoryBlock?>(null);
            }

            public bool TryReturn(MemoryBlock block)
            {
                if (_availableBlocks.Count >= MaxBlocks)
                {
                    // Pool is full
                    return false;
                }

                _availableBlocks.Add(block);
                return true;
            }

            public (int blocks, long bytes) TrimExcess()
            {
                var targetCount = MaxBlocks / 2;
                var freed = 0;
                var freedBytes = 0L;

                while (_availableBlocks.Count > targetCount && _availableBlocks.TryTake(out var block))
                {
                    var result = CudaRuntime.cudaFree(block.DevicePointer);
                    if (result == CudaError.Success)
                    {
                        freed++;
                        freedBytes += block.Size;
                        _lock.Wait();
                        try
                        {
                            _ = _allBlocks.Remove(block.DevicePointer);
                        }
                        finally
                        {
                            _ = _lock.Release();
                        }
                    }
                }

                return (freed, freedBytes);
            }

            public void Clear()
            {
                while (_availableBlocks.TryTake(out var block))
                {
                    _ = CudaRuntime.cudaFree(block.DevicePointer);
                }


                _allBlocks.Clear();
            }

            public void Dispose()
            {
                Clear();
                _lock?.Dispose();
            }
        }
    }

    /// <summary>
    /// Represents a block of device memory.
    /// </summary>
    internal sealed class MemoryBlock
    {
        public IntPtr DevicePointer { get; }
        public long Size { get; }

        public MemoryBlock(IntPtr devicePointer, long size)
        {
            DevicePointer = devicePointer;
            Size = size;
        }
    }

    /// <summary>
    /// Interface for pooled memory buffers.
    /// </summary>
    public interface IPooledMemoryBuffer : IDisposable
    {
        public IntPtr DevicePointer { get; }
        public long Size { get; }
        public long ActualSize { get; }
    }

    /// <summary>
    /// Implementation of pooled memory buffer.
    /// </summary>
    internal sealed class PooledMemoryBuffer : IPooledMemoryBuffer
    {
        private readonly CudaMemoryPoolManager _manager;
        private readonly MemoryBlock _block;
        private readonly int _poolSize;
        private bool _disposed;

        public IntPtr DevicePointer => _block.DevicePointer;
        public long Size { get; }
        public long ActualSize => _block.Size;

        public PooledMemoryBuffer(
            CudaMemoryPoolManager manager,
            MemoryBlock block,
            long requestedSize,
            int poolSize)
        {
            _manager = manager;
            _block = block;
            Size = requestedSize;
            _poolSize = poolSize;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            if (_poolSize > 0)
            {
                _manager.ReturnToPool(_block, _poolSize);
            }
            else
            {
                // Direct allocation, free immediately
                _ = CudaRuntime.cudaFree(_block.DevicePointer);
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Statistics for memory pool usage.
    /// </summary>
    public sealed class MemoryPoolStatistics
    {
        public long TotalAllocations { get; init; }
        public long PoolHits { get; init; }
        public long PoolMisses { get; init; }
        public double HitRate { get; init; }
        public long TotalBytesAllocated { get; init; }
        public long TotalBytesInPools { get; init; }
        public List<PoolSizeStatistics> PoolStatistics { get; init; } = [];
    }

    /// <summary>
    /// Statistics for a specific pool size.
    /// </summary>
    public sealed class PoolSizeStatistics
    {
        public int PoolSize { get; init; }
        public int AvailableBlocks { get; init; }
        public int TotalBlocks { get; init; }
        public long BytesInPool { get; init; }
    }
}