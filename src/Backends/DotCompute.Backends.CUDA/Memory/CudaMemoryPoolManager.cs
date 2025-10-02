// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

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
        /// <summary>
        /// Initializes a new instance of the CudaMemoryPoolManager class.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="device">The device.</param>
        /// <param name="logger">The logger.</param>

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

            _logger.LogInfoMessage("Memory pool manager initialized with {_pools.Count} size classes");
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
                _logger.LogWarningMessage("Pool for size {poolSize} not found, allocating directly");
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


                _logger.LogDebugMessage($"Allocated new {poolSize} byte block for pool");


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
                    _logger.LogWarningMessage("Failed to zero memory: {result}");
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
                _logger.LogWarningMessage("Failed to free memory block: {result}");
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
                    _logger.LogDebugMessage($"Pool maintenance freed {freedBlocks} blocks ({freedBytes} bytes)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during pool maintenance");
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
            _logger.LogInfoMessage("Cleared all memory pools");
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

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
        private sealed class MemoryPool(int blockSize, int maxBlocks, ILogger logger) : IDisposable
        {
            private readonly ConcurrentBag<MemoryBlock> _availableBlocks = [];
            private readonly HashSet<IntPtr> _allBlocks = [];
            private readonly SemaphoreSlim _lock = new(1, 1);
            /// <summary>
            /// Gets or sets the block size.
            /// </summary>
            /// <value>The block size.</value>

            public int BlockSize { get; } = blockSize;
            /// <summary>
            /// Gets or sets the max blocks.
            /// </summary>
            /// <value>The max blocks.</value>
            public int MaxBlocks { get; } = maxBlocks;
            /// <summary>
            /// Gets or sets the available count.
            /// </summary>
            /// <value>The available count.</value>
            public int AvailableCount => _availableBlocks.Count;
            /// <summary>
            /// Gets or sets the total count.
            /// </summary>
            /// <value>The total count.</value>
            public int TotalCount => _allBlocks.Count;
            /// <summary>
            /// Gets or sets the total bytes.
            /// </summary>
            /// <value>The total bytes.</value>
            public long TotalBytes => (long)TotalCount * BlockSize;
            /// <summary>
            /// Attempts to get async.
            /// </summary>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>true if the operation succeeded; otherwise, false.</returns>

            public Task<MemoryBlock?> TryGetAsync(CancellationToken cancellationToken)
            {
                if (_availableBlocks.TryTake(out var block))
                {
                    return Task.FromResult<MemoryBlock?>(block);
                }


                return Task.FromResult<MemoryBlock?>(null);
            }
            /// <summary>
            /// Returns true if able to return, otherwise false.
            /// </summary>
            /// <param name="block">The block.</param>
            /// <returns>true if the operation succeeded; otherwise, false.</returns>

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
            /// <summary>
            /// Gets trim excess.
            /// </summary>
            /// <returns>The result of the operation.</returns>

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
                        if (_lock.Wait(100)) // Wait with timeout to avoid deadlock
                        {
                            try
                            {
                                _ = _allBlocks.Remove(block.DevicePointer);
                            }
                            finally
                            {
                                _ = _lock.Release();
                            }
                        }
                        else
                        {
                            // Timeout - couldn't acquire lock, skip this cleanup iteration
                            break;
                        }
                    }
                }

                return (freed, freedBytes);
            }
            /// <summary>
            /// Performs clear.
            /// </summary>

            public void Clear()
            {
                while (_availableBlocks.TryTake(out var block))
                {
                    _ = CudaRuntime.cudaFree(block.DevicePointer);
                }


                _allBlocks.Clear();
            }
            /// <summary>
            /// Performs dispose.
            /// </summary>

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
    internal sealed class MemoryBlock(IntPtr devicePointer, long size)
    {
        /// <summary>
        /// Gets or sets the device pointer.
        /// </summary>
        /// <value>The device pointer.</value>
        public IntPtr DevicePointer { get; } = devicePointer;
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>The size.</value>
        public long Size { get; } = size;
    }

    /// <summary>
    /// Interface for pooled memory buffers.
    /// </summary>
    public interface IPooledMemoryBuffer : IDisposable
    {
        /// <summary>
        /// Gets or sets the device pointer.
        /// </summary>
        /// <value>The device pointer.</value>
        public IntPtr DevicePointer { get; }
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>The size.</value>
        public long Size { get; }
        /// <summary>
        /// Gets or sets the actual size.
        /// </summary>
        /// <value>The actual size.</value>
        public long ActualSize { get; }
    }

    /// <summary>
    /// Implementation of pooled memory buffer.
    /// </summary>
    internal sealed class PooledMemoryBuffer(
        CudaMemoryPoolManager manager,
        MemoryBlock block,
        long requestedSize,
        int poolSize) : IPooledMemoryBuffer
    {
        private readonly CudaMemoryPoolManager _manager = manager;
        private readonly MemoryBlock _block = block;
        private readonly int _poolSize = poolSize;
        private bool _disposed;
        /// <summary>
        /// Gets or sets the device pointer.
        /// </summary>
        /// <value>The device pointer.</value>

        public IntPtr DevicePointer => _block.DevicePointer;
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>The size.</value>
        public long Size { get; } = requestedSize;
        /// <summary>
        /// Gets or sets the actual size.
        /// </summary>
        /// <value>The actual size.</value>
        public long ActualSize => _block.Size;
        /// <summary>
        /// Performs dispose.
        /// </summary>

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
        /// <summary>
        /// Gets or sets the total allocations.
        /// </summary>
        /// <value>The total allocations.</value>
        public long TotalAllocations { get; init; }
        /// <summary>
        /// Gets or sets the pool hits.
        /// </summary>
        /// <value>The pool hits.</value>
        public long PoolHits { get; init; }
        /// <summary>
        /// Gets or sets the pool misses.
        /// </summary>
        /// <value>The pool misses.</value>
        public long PoolMisses { get; init; }
        /// <summary>
        /// Gets or sets the hit rate.
        /// </summary>
        /// <value>The hit rate.</value>
        public double HitRate { get; init; }
        /// <summary>
        /// Gets or sets the total bytes allocated.
        /// </summary>
        /// <value>The total bytes allocated.</value>
        public long TotalBytesAllocated { get; init; }
        /// <summary>
        /// Gets or sets the total bytes in pools.
        /// </summary>
        /// <value>The total bytes in pools.</value>
        public long TotalBytesInPools { get; init; }
        /// <summary>
        /// Gets or sets the pool statistics.
        /// </summary>
        /// <value>The pool statistics.</value>
        public IReadOnlyList<PoolSizeStatistics> PoolStatistics { get; init; } = [];
    }

    /// <summary>
    /// Statistics for a specific pool size.
    /// </summary>
    public sealed class PoolSizeStatistics
    {
        /// <summary>
        /// Gets or sets the pool size.
        /// </summary>
        /// <value>The pool size.</value>
        public int PoolSize { get; init; }
        /// <summary>
        /// Gets or sets the available blocks.
        /// </summary>
        /// <value>The available blocks.</value>
        public int AvailableBlocks { get; init; }
        /// <summary>
        /// Gets or sets the total blocks.
        /// </summary>
        /// <value>The total blocks.</value>
        public int TotalBlocks { get; init; }
        /// <summary>
        /// Gets or sets the bytes in pool.
        /// </summary>
        /// <value>The bytes in pool.</value>
        public long BytesInPool { get; init; }
    }
}