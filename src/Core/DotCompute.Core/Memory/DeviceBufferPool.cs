// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Memory
{

    /// <summary>
    /// Device-specific buffer pool optimized for P2P operations and memory management.
    /// Provides different allocation strategies for various transfer patterns.
    /// </summary>
    public sealed partial class DeviceBufferPool : IAsyncDisposable
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

        // Pre-compiled LoggerMessage delegates
        private static readonly Action<ILogger, string, double, Exception?> _logPoolCreated =
            LoggerMessage.Define<string, double>(
                MsLogLevel.Debug,
                new EventId(14100, nameof(PoolCreated)),
                "Created buffer pool for device {DeviceName} with P2P bandwidth {P2PBandwidthGBps} GB/s");

        private static readonly Action<ILogger, long, string, Exception?> _logPooledBufferAllocated =
            LoggerMessage.Define<long, string>(
                MsLogLevel.Trace,
                new EventId(14101, nameof(PooledBufferAllocated)),
                "Allocated pooled buffer: {SizeBytes} bytes from pool on {Device}");

        private static readonly Action<ILogger, long, string, Exception?> _logNewBufferAllocated =
            LoggerMessage.Define<long, string>(
                MsLogLevel.Trace,
                new EventId(14102, nameof(NewBufferAllocated)),
                "Allocated new buffer: {SizeBytes} bytes on {Device}");

        private static readonly Action<ILogger, long, string, Exception?> _logBufferAllocationFailed =
            LoggerMessage.Define<long, string>(
                MsLogLevel.Error,
                new EventId(14103, nameof(BufferAllocationFailed)),
                "Failed to allocate buffer of {SizeBytes} bytes on {DeviceName}");

        private static readonly Action<ILogger, long, int, string, Exception?> _logStreamingBufferAllocated =
            LoggerMessage.Define<long, int, string>(
                MsLogLevel.Trace,
                new EventId(14104, nameof(StreamingBufferAllocated)),
                "Allocated streaming buffer: {SizeBytes} bytes with {ChunkSize} chunk size on {Device}");

        private static readonly Action<ILogger, long, string, Exception?> _logStreamingBufferAllocationFailed =
            LoggerMessage.Define<long, string>(
                MsLogLevel.Error,
                new EventId(14105, nameof(StreamingBufferAllocationFailed)),
                "Failed to allocate streaming buffer of {SizeBytes} bytes on {DeviceName}");

        private static readonly Action<ILogger, long, string, Exception?> _logMemoryMappedBufferAllocated =
            LoggerMessage.Define<long, string>(
                MsLogLevel.Trace,
                new EventId(14106, nameof(MemoryMappedBufferAllocated)),
                "Allocated memory-mapped buffer: {SizeBytes} bytes on {Device}");

        private static readonly Action<ILogger, long, string, Exception?> _logMemoryMappedBufferAllocationFailed =
            LoggerMessage.Define<long, string>(
                MsLogLevel.Error,
                new EventId(14107, nameof(MemoryMappedBufferAllocationFailed)),
                "Failed to allocate memory-mapped buffer of {SizeBytes} bytes on {DeviceName}");

        private static readonly Action<ILogger, long, string, Exception?> _logBufferReturnedToPool =
            LoggerMessage.Define<long, string>(
                MsLogLevel.Trace,
                new EventId(14108, nameof(BufferReturnedToPool)),
                "Returned buffer to pool: {SizeBytes} bytes on {Device}");

        private static readonly Action<ILogger, long, string, Exception?> _logBufferDisposedNotPooled =
            LoggerMessage.Define<long, string>(
                MsLogLevel.Trace,
                new EventId(14109, nameof(BufferDisposedNotPooled)),
                "Buffer disposed (not pooled): {SizeBytes} bytes on {Device}");

        private static readonly Action<ILogger, string, Exception?> _logBufferReturnFailed =
            LoggerMessage.Define<string>(
                MsLogLevel.Warning,
                new EventId(14110, nameof(BufferReturnFailed)),
                "Failed to return buffer to pool on {Device}");

        private static readonly Action<ILogger, string, int, long, Exception?> _logPoolCleanupCompleted =
            LoggerMessage.Define<string, int, long>(
                MsLogLevel.Debug,
                new EventId(14111, nameof(PoolCleanupCompleted)),
                "Pool cleanup completed on {DeviceName}: {CleanupCount} buffers released, {ReleasedBytes} bytes freed");

        private static readonly Action<ILogger, string, Exception?> _logPoolCleanupFailed =
            LoggerMessage.Define<string>(
                MsLogLevel.Warning,
                new EventId(14112, nameof(PoolCleanupFailed)),
                "Error during pool cleanup on {Device}");

        private static readonly Action<ILogger, string, Exception?> _logPeriodicCleanupFailed =
            LoggerMessage.Define<string>(
                MsLogLevel.Warning,
                new EventId(14113, nameof(PeriodicCleanupFailed)),
                "Error in periodic pool cleanup on {Device}");

        private static readonly Action<ILogger, string, int, Exception?> _logPoolDisposed =
            LoggerMessage.Define<string, int>(
                MsLogLevel.Debug,
                new EventId(14114, nameof(PoolDisposed)),
                "Disposed buffer pool on {DeviceName}: {DisposeCount} buffers disposed");

        /// <summary>
        /// Initializes a new instance of the DeviceBufferPool class.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="deviceCapabilities">The device capabilities.</param>
        /// <param name="logger">The logger.</param>

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

            PoolCreated(_logger, device.Info.Name, deviceCapabilities.P2PBandwidthGBps, null);
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

                PooledBufferAllocated(_logger, sizeInBytes, _device.Info.Name, null);
                return pooledBuffer;
            }

            // Allocate new buffer
            try
            {
                var options = DetermineOptimalMemoryOptions(sizeInBytes);
                var typedBuffer = await _device.Memory.AllocateAsync<byte>((int)sizeInBytes, options, cancellationToken);
                var buffer = typedBuffer as IUnifiedMemoryBuffer ?? throw new InvalidOperationException($"Failed to allocate buffer on device {_device.Info.Name}");

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

                NewBufferAllocated(_logger, sizeInBytes, _device.Info.Name, null);
                return new PooledMemoryBuffer(buffer, this, sizeInBytes);
            }
            catch (Exception ex)
            {
                lock (_statisticsLock)
                {
                    _statistics.AllocationFailures++;
                }

                BufferAllocationFailed(_logger, sizeInBytes, _device.Info.Name, ex);
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
                var typedBuffer = await _device.Memory.AllocateAsync<byte>((int)sizeInBytes, options, cancellationToken);
                var buffer = typedBuffer as IUnifiedMemoryBuffer ?? throw new InvalidOperationException($"Failed to allocate buffer on device {_device.Info.Name}");

                lock (_statisticsLock)
                {
                    _statistics.StreamingAllocations++;
                    _statistics.AllocationCount++;
                    _statistics.TotalAllocatedBytes += sizeInBytes;
                    _statistics.CurrentAllocatedBytes += sizeInBytes;
                    _statistics.ActiveBuffers++;
                }

                StreamingBufferAllocated(_logger, sizeInBytes, chunkSize, _device.Info.Name, null);

                return new PooledMemoryBuffer(buffer, this, sizeInBytes);
            }
            catch (Exception ex)
            {
                lock (_statisticsLock)
                {
                    _statistics.AllocationFailures++;
                }

                StreamingBufferAllocationFailed(_logger, sizeInBytes, _device.Info.Name, ex);
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
                var typedBuffer = await _device.Memory.AllocateAsync<byte>((int)sizeInBytes, options, cancellationToken);
                var buffer = typedBuffer as IUnifiedMemoryBuffer ?? throw new InvalidOperationException($"Failed to allocate buffer on device {_device.Info.Name}");

                lock (_statisticsLock)
                {
                    _statistics.MemoryMappedAllocations++;
                    _statistics.AllocationCount++;
                    _statistics.TotalAllocatedBytes += sizeInBytes;
                    _statistics.CurrentAllocatedBytes += sizeInBytes;
                    _statistics.ActiveBuffers++;
                }

                MemoryMappedBufferAllocated(_logger, sizeInBytes, _device.Info.Name, null);

                return new PooledMemoryBuffer(buffer, this, sizeInBytes);
            }
            catch (Exception ex)
            {
                lock (_statisticsLock)
                {
                    _statistics.AllocationFailures++;
                }

                MemoryMappedBufferAllocationFailed(_logger, sizeInBytes, _device.Info.Name, ex);
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

                        BufferReturnedToPool(_logger, originalSize, _device.Info.Name, null);
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

                BufferDisposedNotPooled(_logger, originalSize, _device.Info.Name, null);
            }
            catch (Exception ex)
            {
                BufferReturnFailed(_logger, _device.Info.Name, ex);

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
#pragma warning disable CA1024 // Use properties where appropriate - Method creates new object with complex calculations
        public DeviceBufferPoolStatistics GetStatistics()
#pragma warning restore CA1024
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
                    PoolCleanupCompleted(_logger, _device.Info.Name, cleanupCount, releasedBytes, null);
                }
            }
            catch (Exception ex)
            {
                PoolCleanupFailed(_logger, _device.Info.Name, ex);
            }
        }

        /// <summary>
        /// Determines optimal memory options based on buffer size and device capabilities.
        /// </summary>
        private MemoryOptions DetermineOptimalMemoryOptions(long sizeInBytes)
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
                PeriodicCleanupFailed(_logger, _device.Info.Name, ex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

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

            PoolDisposed(_logger, _device.Info.Name, disposeCount, null);
        }

        // LoggerMessage wrapper methods
        private static void PoolCreated(ILogger logger, string deviceName, double p2pBandwidthGBps, Exception? exception)
            => _logPoolCreated(logger, deviceName, p2pBandwidthGBps, exception);

        private static void PooledBufferAllocated(ILogger logger, long sizeBytes, string device, Exception? exception)
            => _logPooledBufferAllocated(logger, sizeBytes, device, exception);

        private static void NewBufferAllocated(ILogger logger, long sizeBytes, string device, Exception? exception)
            => _logNewBufferAllocated(logger, sizeBytes, device, exception);

        private static void BufferAllocationFailed(ILogger logger, long sizeBytes, string deviceName, Exception? exception)
            => _logBufferAllocationFailed(logger, sizeBytes, deviceName, exception);

        private static void StreamingBufferAllocated(ILogger logger, long sizeBytes, int chunkSize, string device, Exception? exception)
            => _logStreamingBufferAllocated(logger, sizeBytes, chunkSize, device, exception);

        private static void StreamingBufferAllocationFailed(ILogger logger, long sizeBytes, string deviceName, Exception? exception)
            => _logStreamingBufferAllocationFailed(logger, sizeBytes, deviceName, exception);

        private static void MemoryMappedBufferAllocated(ILogger logger, long sizeBytes, string device, Exception? exception)
            => _logMemoryMappedBufferAllocated(logger, sizeBytes, device, exception);

        private static void MemoryMappedBufferAllocationFailed(ILogger logger, long sizeBytes, string deviceName, Exception? exception)
            => _logMemoryMappedBufferAllocationFailed(logger, sizeBytes, deviceName, exception);

        private static void BufferReturnedToPool(ILogger logger, long sizeBytes, string device, Exception? exception)
            => _logBufferReturnedToPool(logger, sizeBytes, device, exception);

        private static void BufferDisposedNotPooled(ILogger logger, long sizeBytes, string device, Exception? exception)
            => _logBufferDisposedNotPooled(logger, sizeBytes, device, exception);

        private static void BufferReturnFailed(ILogger logger, string device, Exception? exception)
            => _logBufferReturnFailed(logger, device, exception);

        private static void PoolCleanupCompleted(ILogger logger, string deviceName, int cleanupCount, long releasedBytes, Exception? exception)
            => _logPoolCleanupCompleted(logger, deviceName, cleanupCount, releasedBytes, exception);

        private static void PoolCleanupFailed(ILogger logger, string device, Exception? exception)
            => _logPoolCleanupFailed(logger, device, exception);

        private static void PeriodicCleanupFailed(ILogger logger, string device, Exception? exception)
            => _logPeriodicCleanupFailed(logger, device, exception);

        private static void PoolDisposed(ILogger logger, string deviceName, int disposeCount, Exception? exception)
            => _logPoolDisposed(logger, deviceName, disposeCount, exception);

        /// <summary>
        /// Internal pool statistics tracking.
        /// </summary>
        private sealed class PoolStatistics
        {
            /// <summary>
            /// Gets or sets the allocation count.
            /// </summary>
            /// <value>The allocation count.</value>
            public long AllocationCount { get; set; }
            /// <summary>
            /// Gets or sets the deallocation count.
            /// </summary>
            /// <value>The deallocation count.</value>
            public long DeallocationCount { get; set; }
            /// <summary>
            /// Gets or sets the allocation failures.
            /// </summary>
            /// <value>The allocation failures.</value>
            public long AllocationFailures { get; set; }
            /// <summary>
            /// Gets or sets the total allocated bytes.
            /// </summary>
            /// <value>The total allocated bytes.</value>
            public long TotalAllocatedBytes { get; set; }
            /// <summary>
            /// Gets or sets the current allocated bytes.
            /// </summary>
            /// <value>The current allocated bytes.</value>
            public long CurrentAllocatedBytes { get; set; }
            /// <summary>
            /// Gets or sets the peak allocated bytes.
            /// </summary>
            /// <value>The peak allocated bytes.</value>
            public long PeakAllocatedBytes { get; set; }
            /// <summary>
            /// Gets or sets the active buffers.
            /// </summary>
            /// <value>The active buffers.</value>
            public int ActiveBuffers { get; set; }
            /// <summary>
            /// Gets or sets the pool hits.
            /// </summary>
            /// <value>The pool hits.</value>
            public long PoolHits { get; set; }
            /// <summary>
            /// Gets or sets the pool misses.
            /// </summary>
            /// <value>The pool misses.</value>
            public long PoolMisses { get; set; }
            /// <summary>
            /// Gets or sets the reuse count.
            /// </summary>
            /// <value>The reuse count.</value>
            public long ReuseCount { get; set; }
            /// <summary>
            /// Gets or sets the returned to pool.
            /// </summary>
            /// <value>The returned to pool.</value>
            public long ReturnedToPool { get; set; }
            /// <summary>
            /// Gets or sets the streaming allocations.
            /// </summary>
            /// <value>The streaming allocations.</value>
            public long StreamingAllocations { get; set; }
            /// <summary>
            /// Gets or sets the memory mapped allocations.
            /// </summary>
            /// <value>The memory mapped allocations.</value>
            public long MemoryMappedAllocations { get; set; }
            /// <summary>
            /// Gets or sets the cleanup count.
            /// </summary>
            /// <value>The cleanup count.</value>
            public long CleanupCount { get; set; }
        }
    }

    /// <summary>
    /// Wrapper for memory buffers that automatically returns to pool on disposal.
    /// </summary>
    internal sealed class PooledMemoryBuffer(IUnifiedMemoryBuffer underlyingBuffer, DeviceBufferPool pool, long originalSize) : IUnifiedMemoryBuffer
    {
        private readonly IUnifiedMemoryBuffer _underlyingBuffer = underlyingBuffer;
#pragma warning disable CA2213 // Disposable fields should be disposed - Wrapper doesn't own the pool, returns buffers to it
        private readonly DeviceBufferPool _pool = pool;
#pragma warning restore CA2213
        private readonly long _originalSize = originalSize;
        private bool _disposed;
        /// <summary>
        /// Gets or sets the size in bytes.
        /// </summary>
        /// <value>The size in bytes.</value>

        public long SizeInBytes => _underlyingBuffer.SizeInBytes;
        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public MemoryOptions Options => _underlyingBuffer.Options;
        /// <summary>
        /// Gets or sets a value indicating whether disposed.
        /// </summary>
        /// <value>The is disposed.</value>
        public bool IsDisposed => _disposed;
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public BufferState State => _underlyingBuffer.State;
        /// <summary>
        /// Gets copy from host asynchronously.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            // Since _underlyingBuffer is non-generic, we can't directly call typed methods
            // The actual implementation would depend on the concrete buffer type
            return ValueTask.CompletedTask;
        }
        /// <summary>
        /// Gets copy to host asynchronously.
        /// </summary>
        /// <typeparam name="T">The T type parameter.</typeparam>
        /// <param name="destination">The destination.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            // Since _underlyingBuffer is non-generic, we can't directly call typed methods
            // The actual implementation would depend on the concrete buffer type
            return ValueTask.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pool.ReturnBuffer(_underlyingBuffer, _originalSize);
        }

        /// <summary>
        /// Copies data from source memory to this pooled buffer with offset support.
        /// </summary>
        /// <typeparam name="T">Type of elements to copy.</typeparam>
        /// <param name="source">Source data to copy from.</param>
        /// <param name="destinationOffset">Offset into this buffer in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Completed task when copy finishes.</returns>
        public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long destinationOffset, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            // Call the new interface method we just added
            return _underlyingBuffer.CopyFromAsync(source, destinationOffset, cancellationToken);
        }

        /// <summary>
        /// Copies data from this pooled buffer to destination memory with offset support.
        /// </summary>
        /// <typeparam name="T">Type of elements to copy.</typeparam>
        /// <param name="destination">Destination memory to copy to.</param>
        /// <param name="sourceOffset">Offset into this buffer in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Completed task when copy finishes.</returns>
        public ValueTask CopyToAsync<T>(Memory<T> destination, long sourceOffset, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ThrowIfDisposed();
            // Call the new interface method we just added
            return _underlyingBuffer.CopyToAsync(destination, sourceOffset, cancellationToken);
        }
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

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
