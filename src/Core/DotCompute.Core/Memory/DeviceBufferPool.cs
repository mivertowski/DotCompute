// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using global::System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory
{

    /// <summary>
    /// Device-specific buffer pool optimized for P2P operations and memory management.
    /// Provides different allocation strategies for various transfer patterns.
    /// </summary>
    public sealed class DeviceBufferPool : IAsyncDisposable
    {
        private readonly IAccelerator _device;
        private readonly DeviceCapabilities _deviceCapabilities;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<int, ConcurrentQueue<IUnifiedMemoryBuffer>> _sizeBasedPools;
        private readonly ConcurrentQueue<IUnifiedMemoryBuffer> _largeBufferPool;
        private readonly Lock _statisticsLock = new();
        private readonly Timer _cleanupTimer;
        private readonly PoolStatistics _statistics;
        private bool _disposed;

        // Pool configuration
        private const int MaxPooledBufferSize = 64 * 1024 * 1024; // 64MB
        private const int MaxBuffersPerSize = 16;
        private const int LargeBufferPoolSize = 8;
        private const int CleanupIntervalMs = 30000; // 30 seconds

        public DeviceBufferPool(
            IAccelerator device,
            DeviceCapabilities deviceCapabilities,
            ILogger logger)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _deviceCapabilities = deviceCapabilities ?? throw new ArgumentNullException(nameof(deviceCapabilities));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _sizeBasedPools = new ConcurrentDictionary<int, ConcurrentQueue<IUnifiedMemoryBuffer>>();
            _largeBufferPool = new ConcurrentQueue<IUnifiedMemoryBuffer>();
            _statistics = new PoolStatistics();

            // Start periodic cleanup
            _cleanupTimer = new Timer(PeriodicCleanup, null,
                TimeSpan.FromMilliseconds(CleanupIntervalMs),
                TimeSpan.FromMilliseconds(CleanupIntervalMs));

            _logger.LogDebug("Created buffer pool for device {DeviceName} with P2P bandwidth {BandwidthGBps:F1} GB/s",
                device.Info.Name, deviceCapabilities.P2PBandwidthGBps);
        }

        /// <summary>
        /// Allocates a buffer with standard pooling strategy.
        /// </summary>
        public async ValueTask<IUnifiedMemoryBuffer> AllocateAsync(long sizeInBytes, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

            var roundedSize = RoundUpToPowerOfTwo((int)Math.Min(sizeInBytes, int.MaxValue));

            // Try to get from pool first
            if (roundedSize <= MaxPooledBufferSize && TryGetFromPool(roundedSize, out var pooledBuffer))
            {
                lock (_statisticsLock)
                {
                    _statistics.PoolHits++;
                    _statistics.ReuseCount++;
                }

                _logger.LogTrace("Allocated pooled buffer: {SizeBytes} bytes from pool on {Device}",
                    sizeInBytes, _device.Info.Name);
                return pooledBuffer;
            }

            // Allocate new buffer
            try
            {
                var options = DetermineOptimalMemoryOptions(sizeInBytes);
                var buffer = await _device.Memory.AllocateAsync(sizeInBytes, options, cancellationToken);

                lock (_statisticsLock)
                {
                    _statistics.PoolMisses++;
                    _statistics.AllocationCount++;
                    _statistics.TotalAllocatedBytes += sizeInBytes;
                    _statistics.CurrentAllocatedBytes += sizeInBytes;
                    _statistics.ActiveBuffers++;

                    if (_statistics.CurrentAllocatedBytes > _statistics.PeakAllocatedBytes)
                    {
                        _statistics.PeakAllocatedBytes = _statistics.CurrentAllocatedBytes;
                    }
                }

                _logger.LogTrace("Allocated new buffer: {SizeBytes} bytes on {Device}", sizeInBytes, _device.Info.Name);
                return new PooledMemoryBuffer(buffer, this, sizeInBytes);
            }
            catch (Exception ex)
            {
                lock (_statisticsLock)
                {
                    _statistics.AllocationFailures++;
                }

                _logger.LogError(ex, "Failed to allocate buffer of {SizeBytes} bytes on {Device}",
                    sizeInBytes, _device.Info.Name);
                throw;
            }
        }

        /// <summary>
        /// Allocates a buffer optimized for streaming transfers.
        /// </summary>
        public async ValueTask<IUnifiedMemoryBuffer> AllocateStreamingAsync(
            long sizeInBytes,
            int chunkSize,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Use pinned memory for better streaming performance
            var options = DotCompute.Abstractions.Memory.MemoryOptions.HostVisible;

            try
            {
                var buffer = await _device.Memory.AllocateAsync(sizeInBytes, options, cancellationToken);

                lock (_statisticsLock)
                {
                    _statistics.StreamingAllocations++;
                    _statistics.AllocationCount++;
                    _statistics.TotalAllocatedBytes += sizeInBytes;
                    _statistics.CurrentAllocatedBytes += sizeInBytes;
                    _statistics.ActiveBuffers++;
                }

                _logger.LogTrace("Allocated streaming buffer: {SizeBytes} bytes with {ChunkSize} chunk size on {Device}",
                    sizeInBytes, chunkSize, _device.Info.Name);

                return new PooledMemoryBuffer(buffer, this, sizeInBytes);
            }
            catch (Exception ex)
            {
                lock (_statisticsLock)
                {
                    _statistics.AllocationFailures++;
                }

                _logger.LogError(ex, "Failed to allocate streaming buffer of {SizeBytes} bytes on {Device}",
                    sizeInBytes, _device.Info.Name);
                throw;
            }
        }

        /// <summary>
        /// Allocates a memory-mapped buffer for very large datasets.
        /// </summary>
        public async ValueTask<IUnifiedMemoryBuffer> AllocateMemoryMappedAsync(
            long sizeInBytes,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // For very large allocations, use memory mapping strategy
            var options = DotCompute.Abstractions.Memory.MemoryOptions.HostVisible | DotCompute.Abstractions.Memory.MemoryOptions.Cached;

            try
            {
                var buffer = await _device.Memory.AllocateAsync(sizeInBytes, options, cancellationToken);

                lock (_statisticsLock)
                {
                    _statistics.MemoryMappedAllocations++;
                    _statistics.AllocationCount++;
                    _statistics.TotalAllocatedBytes += sizeInBytes;
                    _statistics.CurrentAllocatedBytes += sizeInBytes;
                    _statistics.ActiveBuffers++;
                }

                _logger.LogTrace("Allocated memory-mapped buffer: {SizeBytes} bytes on {Device}",
                    sizeInBytes, _device.Info.Name);

                return new PooledMemoryBuffer(buffer, this, sizeInBytes);
            }
            catch (Exception ex)
            {
                lock (_statisticsLock)
                {
                    _statistics.AllocationFailures++;
                }

                _logger.LogError(ex, "Failed to allocate memory-mapped buffer of {SizeBytes} bytes on {Device}",
                    sizeInBytes, _device.Info.Name);
                throw;
            }
        }

        /// <summary>
        /// Returns a buffer to the pool for reuse.
        /// </summary>
        internal void ReturnBuffer(IUnifiedMemoryBuffer buffer, long originalSize)
        {
            if (_disposed || buffer.IsDisposed)
            {
                return;
            }

            try
            {
                var roundedSize = RoundUpToPowerOfTwo((int)Math.Min(originalSize, int.MaxValue));

                // Only pool reasonably sized buffers
                if (roundedSize <= MaxPooledBufferSize)
                {
                    var pool = _sizeBasedPools.GetOrAdd(roundedSize, _ => new ConcurrentQueue<IUnifiedMemoryBuffer>());

                    // Check pool size limit
                    if (GetPoolSize(pool) < MaxBuffersPerSize)
                    {
                        pool.Enqueue(buffer);

                        lock (_statisticsLock)
                        {
                            _statistics.ReturnedToPool++;
                        }

                        _logger.LogTrace("Returned buffer to pool: {SizeBytes} bytes on {Device}",
                            originalSize, _device.Info.Name);
                        return;
                    }
                }

                // Buffer not pooled - dispose immediately
                buffer.Dispose();

                lock (_statisticsLock)
                {
                    _statistics.DeallocationCount++;
                    _statistics.CurrentAllocatedBytes -= originalSize;
                    _statistics.ActiveBuffers--;
                }

                _logger.LogTrace("Buffer disposed (not pooled): {SizeBytes} bytes on {Device}",
                    originalSize, _device.Info.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to return buffer to pool on {Device}", _device.Info.Name);

                try
                {
                    buffer.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }

        /// <summary>
        /// Gets comprehensive pool statistics.
        /// </summary>
        public DeviceBufferPoolStatistics GetStatistics()
        {
            lock (_statisticsLock)
            {
                var poolEfficiency = _statistics.AllocationCount > 0
                    ? (double)_statistics.ReuseCount / _statistics.AllocationCount
                    : 0.0;

                return new DeviceBufferPoolStatistics
                {
                    DeviceId = _device.Info.Id,
                    DeviceName = _device.Info.Name,
                    TotalAllocatedBytes = _statistics.TotalAllocatedBytes,
                    AvailableBytes = _deviceCapabilities.MemoryBandwidthGBps > 0
                        ? (long)(_deviceCapabilities.MemoryBandwidthGBps * 1_000_000_000) // Estimate available
                        : 1_000_000_000, // 1GB fallback
                    ActiveBuffers = _statistics.ActiveBuffers,
                    AllocationCount = _statistics.AllocationCount,
                    DeallocationCount = _statistics.DeallocationCount,
                    PoolEfficiency = poolEfficiency
                };
            }
        }

        /// <summary>
        /// Forces cleanup of unused pooled buffers.
        /// </summary>
        public void Cleanup()
        {
            if (_disposed)
            {
                return;
            }

            var cleanupCount = 0;
            var releasedBytes = 0L;

            try
            {
                // Clean up size-based pools
                foreach (var kvp in _sizeBasedPools)
                {
                    var queue = kvp.Value;
                    var size = kvp.Key;
                    var targetCount = MaxBuffersPerSize / 2; // Keep only half

                    while (queue.Count > targetCount && queue.TryDequeue(out var buffer))
                    {
                        buffer.Dispose();
                        cleanupCount++;
                        releasedBytes += size;
                    }
                }

                // Clean up large buffer pool
                var largeTargetCount = LargeBufferPoolSize / 2;
                while (_largeBufferPool.Count > largeTargetCount && _largeBufferPool.TryDequeue(out var buffer))
                {
                    releasedBytes += buffer.SizeInBytes;
                    buffer.Dispose();
                    cleanupCount++;
                }

                lock (_statisticsLock)
                {
                    _statistics.CleanupCount++;
                    _statistics.CurrentAllocatedBytes -= releasedBytes;
                    _statistics.DeallocationCount += cleanupCount;
                }

                if (cleanupCount > 0)
                {
                    _logger.LogDebug("Pool cleanup completed on {Device}: {Count} buffers released, {Bytes} bytes freed",
                        _device.Info.Name, cleanupCount, releasedBytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during pool cleanup on {Device}", _device.Info.Name);
            }
        }

        /// <summary>
        /// Determines optimal memory options based on buffer size and device capabilities.
        /// </summary>
        private DotCompute.Abstractions.Memory.MemoryOptions DetermineOptimalMemoryOptions(long sizeInBytes)
        {
            var options = DotCompute.Abstractions.Memory.MemoryOptions.None;

            // For P2P-capable devices, prefer device-only memory for large buffers
            if (_deviceCapabilities.SupportsP2P && sizeInBytes > 1024 * 1024) // > 1MB
            {
                // Device memory is preferred for P2P transfers
                return options;
            }

            // For smaller buffers or non-P2P devices, use host-visible memory
            if (sizeInBytes <= 64 * 1024 || !_deviceCapabilities.SupportsP2P) // <= 64KB
            {
                options |= DotCompute.Abstractions.Memory.MemoryOptions.HostVisible;
            }

            // Enable caching for frequently accessed data
            if (sizeInBytes <= 16 * 1024 * 1024) // <= 16MB
            {
                options |= DotCompute.Abstractions.Memory.MemoryOptions.Cached;
            }

            return options;
        }

        /// <summary>
        /// Tries to get a buffer from the appropriate pool.
        /// </summary>
        private bool TryGetFromPool(int roundedSize, out IUnifiedMemoryBuffer buffer)
        {
            buffer = default!;

            if (_sizeBasedPools.TryGetValue(roundedSize, out var pool) && pool.TryDequeue(out buffer!))
            {
                return true;
            }

            // Try to get from large buffer pool if size is large enough
            if (roundedSize > MaxPooledBufferSize / 2 && _largeBufferPool.TryDequeue(out buffer!))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Rounds up to the nearest power of two for consistent pool sizing.
        /// </summary>
        private static int RoundUpToPowerOfTwo(int value)
        {
            if (value <= 0)
            {
                return 1;
            }

            if ((value & (value - 1)) == 0)
            {
                return value; // Already power of 2
            }

            var result = 1;
            while (result < value)
            {
                result <<= 1;
            }
            return result;
        }

        /// <summary>
        /// Gets the approximate size of a concurrent queue (for pool size checking).
        /// </summary>
        private static int GetPoolSize(ConcurrentQueue<IUnifiedMemoryBuffer> pool) => pool.Count; // This is approximate but sufficient for our needs

        /// <summary>
        /// Periodic cleanup callback.
        /// </summary>
        private void PeriodicCleanup(object? state)
        {
            try
            {
                Cleanup();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in periodic pool cleanup on {Device}", _device.Info.Name);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            await _cleanupTimer.DisposeAsync();

            // Dispose all pooled buffers
            var disposeCount = 0;

            foreach (var pool in _sizeBasedPools.Values)
            {
                while (pool.TryDequeue(out var buffer))
                {
                    buffer.Dispose();
                    disposeCount++;
                }
            }

            while (_largeBufferPool.TryDequeue(out var buffer))
            {
                buffer.Dispose();
                disposeCount++;
            }

            _sizeBasedPools.Clear();

            _logger.LogDebug("Disposed buffer pool on {Device}: {Count} buffers disposed",
                _device.Info.Name, disposeCount);
        }

        /// <summary>
        /// Internal pool statistics tracking.
        /// </summary>
        private sealed class PoolStatistics
        {
            public long AllocationCount { get; set; }
            public long DeallocationCount { get; set; }
            public long AllocationFailures { get; set; }
            public long TotalAllocatedBytes { get; set; }
            public long CurrentAllocatedBytes { get; set; }
            public long PeakAllocatedBytes { get; set; }
            public int ActiveBuffers { get; set; }
            public long PoolHits { get; set; }
            public long PoolMisses { get; set; }
            public long ReuseCount { get; set; }
            public long ReturnedToPool { get; set; }
            public long StreamingAllocations { get; set; }
            public long MemoryMappedAllocations { get; set; }
            public long CleanupCount { get; set; }
        }
    }

    /// <summary>
    /// Wrapper for memory buffers that automatically returns to pool on disposal.
    /// </summary>
    internal sealed class PooledMemoryBuffer : IUnifiedMemoryBuffer
    {
        private readonly IUnifiedMemoryBuffer _underlyingBuffer;
        private readonly DeviceBufferPool _pool;
        private readonly long _originalSize;
        private bool _disposed;

        public PooledMemoryBuffer(IUnifiedMemoryBuffer underlyingBuffer, DeviceBufferPool pool, long originalSize)
        {
            _underlyingBuffer = underlyingBuffer;
            _pool = pool;
            _originalSize = originalSize;
        }

        public long SizeInBytes => _underlyingBuffer.SizeInBytes;
        public DotCompute.Abstractions.Memory.MemoryOptions Options => _underlyingBuffer.Options;
        public bool IsDisposed => _disposed;
        public BufferState State 
        { 
            get => _underlyingBuffer.State;
            set => _underlyingBuffer.State = value;
        }

        public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            return _underlyingBuffer.CopyFromHostAsync(source, offset, cancellationToken);
        }

        public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            return _underlyingBuffer.CopyToHostAsync(destination, offset, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pool.ReturnBuffer(_underlyingBuffer, _originalSize);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pool.ReturnBuffer(_underlyingBuffer, _originalSize);
            await ValueTask.CompletedTask;
        }
    }
}
