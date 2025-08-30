// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution
{

    /// <summary>
    /// High-performance CUDA stream pool with priority-based allocation and automatic rebalancing
    /// </summary>
    internal sealed class CudaStreamPool : IDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger<CudaStreamPool> _logger;
        private readonly int _leastPriority;
        private readonly int _greatestPriority;

        // Priority-based pools
        private readonly ConcurrentQueue<PooledStream> _highPriorityStreams;
        private readonly ConcurrentQueue<PooledStream> _normalPriorityStreams;
        private readonly ConcurrentQueue<PooledStream> _lowPriorityStreams;

        private readonly SemaphoreSlim _poolSemaphore;
        private readonly Timer _cleanupTimer;
        private readonly object _lockObject = new();

        // Pool configuration
        private const int INITIAL_HIGH_PRIORITY_COUNT = 4;
        private const int INITIAL_NORMAL_PRIORITY_COUNT = 8;
        private const int INITIAL_LOW_PRIORITY_COUNT = 4;
        private const int MAX_POOL_SIZE_PER_PRIORITY = 16;
        private const int MIN_POOL_SIZE_PER_PRIORITY = 2;

        private volatile bool _disposed;
        private long _totalAcquired;
        private long _totalReturned;

        public CudaStreamPool(CudaContext context, ILogger<CudaStreamPool> logger, int leastPriority, int greatestPriority)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _leastPriority = leastPriority;
            _greatestPriority = greatestPriority;

            _highPriorityStreams = new ConcurrentQueue<PooledStream>();
            _normalPriorityStreams = new ConcurrentQueue<PooledStream>();
            _lowPriorityStreams = new ConcurrentQueue<PooledStream>();

            var totalPoolSize = INITIAL_HIGH_PRIORITY_COUNT + INITIAL_NORMAL_PRIORITY_COUNT + INITIAL_LOW_PRIORITY_COUNT;
            _poolSemaphore = new SemaphoreSlim(totalPoolSize * 2, totalPoolSize * 2);

            Initialize();

            // Setup cleanup timer
            _cleanupTimer = new Timer(PerformCleanup, null,
                TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(3));

            _logger.LogDebug("CUDA Stream Pool initialized with {High}H/{Normal}N/{Low}L streams",
                INITIAL_HIGH_PRIORITY_COUNT, INITIAL_NORMAL_PRIORITY_COUNT, INITIAL_LOW_PRIORITY_COUNT);
        }

        /// <summary>
        /// Acquires a stream from the pool with specified priority
        /// </summary>
        public async Task<CudaStreamHandle> AcquireAsync(
            CudaStreamPriority priority,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _poolSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var pooledStream = GetStreamFromPool(priority);
                pooledStream.AcquiredAt = DateTimeOffset.UtcNow;
                pooledStream.AcquireCount++;

                _ = Interlocked.Increment(ref _totalAcquired);

                _logger.LogTrace("Acquired stream {Stream} from {Priority} priority pool (acquired {Count} times)",
                    pooledStream.Handle, priority, pooledStream.AcquireCount);

                return new PooledCudaStreamHandle(StreamId.New(), pooledStream.Handle, this, pooledStream);
            }
            catch
            {
                _ = _poolSemaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// Returns a stream to the appropriate priority pool
        /// </summary>
        public void Return(IntPtr stream, CudaStreamPriority priority)
        {
            if (_disposed || stream == IntPtr.Zero)
            {
                _ = _poolSemaphore.Release();
                return;
            }

            var pooledStream = new PooledStream
            {
                Handle = stream,
                Priority = priority,
                CreatedAt = DateTimeOffset.UtcNow, // Reset creation time for pool management
                AcquiredAt = null,
                AcquireCount = 0
            };

            var targetQueue = GetQueueForPriority(priority);

            // Check pool size limits
            if (targetQueue.Count < MAX_POOL_SIZE_PER_PRIORITY)
            {
                targetQueue.Enqueue(pooledStream);
                _ = Interlocked.Increment(ref _totalReturned);

                _logger.LogTrace("Returned stream {Stream} to {Priority} priority pool",
                    stream, priority);
            }
            else
            {
                // Pool is full, destroy the stream
                DestroyPooledStream(pooledStream);
                _logger.LogTrace("Destroyed excess stream {Stream} from {Priority} priority pool",
                    stream, priority);
            }

            _ = _poolSemaphore.Release();
        }

        /// <summary>
        /// Gets comprehensive pool statistics
        /// </summary>
        public CudaStreamPoolStatistics GetStatistics()
        {
            ThrowIfDisposed();

            var highCount = _highPriorityStreams.Count;
            var normalCount = _normalPriorityStreams.Count;
            var lowCount = _lowPriorityStreams.Count;

            return new CudaStreamPoolStatistics
            {
                HighPriorityStreams = highCount,
                NormalPriorityStreams = normalCount,
                LowPriorityStreams = lowCount,
                TotalPooledStreams = highCount + normalCount + lowCount,
                TotalAcquired = _totalAcquired,
                TotalReturned = _totalReturned,
                ActiveStreams = _totalAcquired - _totalReturned,
                PoolUtilization = CalculatePoolUtilization(),
                AverageAcquireCount = CalculateAverageAcquireCount()
            };
        }

        /// <summary>
        /// Performs maintenance tasks like rebalancing and cleanup
        /// </summary>
        public void PerformMaintenance()
        {
            if (_disposed)
            {
                return;
            }

            lock (_lockObject)
            {
                try
                {
                    // Rebalance pools based on usage patterns
                    RebalancePools();

                    // Clean up old streams
                    CleanupOldStreams();

                    // Ensure minimum pool sizes
                    EnsureMinimumPoolSizes();

                    _logger.LogTrace("Stream pool maintenance completed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during stream pool maintenance");
                }
            }
        }

        private void Initialize()
        {
            _context.MakeCurrent();

            // Pre-allocate high priority streams
            for (var i = 0; i < INITIAL_HIGH_PRIORITY_COUNT; i++)
            {
                var stream = CreatePooledStream(CudaStreamPriority.High);
                if (stream != null)
                {
                    _highPriorityStreams.Enqueue(stream);
                }
            }

            // Pre-allocate normal priority streams  
            for (var i = 0; i < INITIAL_NORMAL_PRIORITY_COUNT; i++)
            {
                var stream = CreatePooledStream(CudaStreamPriority.Normal);
                if (stream != null)
                {
                    _normalPriorityStreams.Enqueue(stream);
                }
            }

            // Pre-allocate low priority streams
            for (var i = 0; i < INITIAL_LOW_PRIORITY_COUNT; i++)
            {
                var stream = CreatePooledStream(CudaStreamPriority.Low);
                if (stream != null)
                {
                    _lowPriorityStreams.Enqueue(stream);
                }
            }

            var totalCreated = _highPriorityStreams.Count + _normalPriorityStreams.Count + _lowPriorityStreams.Count;
            _logger.LogDebug("Pre-allocated {TotalStreams} streams in pool", totalCreated);
        }

        private PooledStream GetStreamFromPool(CudaStreamPriority priority)
        {
            var primaryQueue = GetQueueForPriority(priority);

            // Try primary queue first
            if (primaryQueue.TryDequeue(out var stream))
            {
                return stream;
            }

            // Try other queues as fallback (normal -> high -> low priority order)
            var fallbackQueues = priority switch
            {
                CudaStreamPriority.High => new[] { _normalPriorityStreams, _lowPriorityStreams },
                CudaStreamPriority.Normal => new[] { _highPriorityStreams, _lowPriorityStreams },
                CudaStreamPriority.Low => new[] { _normalPriorityStreams, _highPriorityStreams },
                _ => new[] { _normalPriorityStreams, _highPriorityStreams }
            };

            foreach (var queue in fallbackQueues)
            {
                if (queue.TryDequeue(out stream))
                {
                    _logger.LogTrace("Using fallback stream from different priority pool for {Priority} request", priority);
                    return stream;
                }
            }

            // No streams available, create a new one
            var newStream = CreatePooledStream(priority);
            if (newStream == null)
            {
                throw new InvalidOperationException("Failed to create new stream for pool");
            }

            _logger.LogTrace("Created new stream {Stream} for {Priority} priority pool",
                newStream.Handle, priority);

            return newStream;
        }

        private ConcurrentQueue<PooledStream> GetQueueForPriority(CudaStreamPriority priority) => priority switch
        {
            CudaStreamPriority.High => _highPriorityStreams,
            CudaStreamPriority.Low => _lowPriorityStreams,
            _ => _normalPriorityStreams
        };

        private PooledStream? CreatePooledStream(CudaStreamPriority priority)
        {
            try
            {
                _context.MakeCurrent();

                var stream = IntPtr.Zero;
                var cudaPriority = ConvertToCudaPriority(priority);
                var flags = 0x01; // NonBlocking

                CudaError result;
                if (priority != CudaStreamPriority.Normal)
                {
                    result = Native.CudaRuntime.cudaStreamCreateWithPriority(ref stream, (uint)flags, cudaPriority);
                }
                else
                {
                    result = Native.CudaRuntime.cudaStreamCreateWithFlags(ref stream, (uint)flags);
                }

                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to create pooled stream with priority {Priority}: {Error}",
                        priority, Native.CudaRuntime.GetErrorString(result));
                    return null;
                }

                return new PooledStream
                {
                    Handle = stream,
                    Priority = priority,
                    CreatedAt = DateTimeOffset.UtcNow,
                    AcquiredAt = null,
                    AcquireCount = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating pooled stream with priority {Priority}", priority);
                return null;
            }
        }

        private void DestroyPooledStream(PooledStream pooledStream)
        {
            try
            {
                _context.MakeCurrent();
                var result = Native.CudaRuntime.cudaStreamDestroy(pooledStream.Handle);
                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to destroy pooled stream {Stream}: {Error}",
                        pooledStream.Handle, Native.CudaRuntime.GetErrorString(result));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception destroying pooled stream {Stream}", pooledStream.Handle);
            }
        }

        private void RebalancePools()
        {
            // Move streams between pools based on demand patterns
            var stats = GetStatistics();

            // If high priority pool is empty but others have streams, promote some
            if (stats.HighPriorityStreams == 0 && stats.NormalPriorityStreams > MIN_POOL_SIZE_PER_PRIORITY)
            {
                if (_normalPriorityStreams.TryDequeue(out var stream))
                {
                    stream.Priority = CudaStreamPriority.High;
                    _highPriorityStreams.Enqueue(stream);
                    _logger.LogTrace("Promoted stream to high priority pool during rebalancing");
                }
            }

            // Similar logic for other priorities could be added based on usage patterns
        }

        private void CleanupOldStreams()
        {
            var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-10);

            CleanupQueueOldStreams(_highPriorityStreams, cutoffTime, "high");
            CleanupQueueOldStreams(_normalPriorityStreams, cutoffTime, "normal");
            CleanupQueueOldStreams(_lowPriorityStreams, cutoffTime, "low");
        }

        private void CleanupQueueOldStreams(ConcurrentQueue<PooledStream> queue, DateTimeOffset cutoffTime, string queueName)
        {
            var streamsToKeep = new List<PooledStream>();
            var destroyedCount = 0;

            // Dequeue all streams and decide which to keep
            while (queue.TryDequeue(out var stream))
            {
                if (stream.CreatedAt > cutoffTime || streamsToKeep.Count < MIN_POOL_SIZE_PER_PRIORITY)
                {
                    streamsToKeep.Add(stream);
                }
                else
                {
                    DestroyPooledStream(stream);
                    destroyedCount++;
                }
            }

            // Re-enqueue streams we're keeping
            foreach (var stream in streamsToKeep)
            {
                queue.Enqueue(stream);
            }

            if (destroyedCount > 0)
            {
                _logger.LogDebug("Cleaned up {Count} old streams from {Queue} priority pool",
                    destroyedCount, queueName);
            }
        }

        private void EnsureMinimumPoolSizes()
        {
            EnsureMinimumPoolSize(_highPriorityStreams, CudaStreamPriority.High, "high");
            EnsureMinimumPoolSize(_normalPriorityStreams, CudaStreamPriority.Normal, "normal");
            EnsureMinimumPoolSize(_lowPriorityStreams, CudaStreamPriority.Low, "low");
        }

        private void EnsureMinimumPoolSize(ConcurrentQueue<PooledStream> queue, CudaStreamPriority priority, string queueName)
        {
            var currentSize = queue.Count;
            var needed = MIN_POOL_SIZE_PER_PRIORITY - currentSize;

            if (needed > 0)
            {
                for (var i = 0; i < needed; i++)
                {
                    var stream = CreatePooledStream(priority);
                    if (stream != null)
                    {
                        queue.Enqueue(stream);
                    }
                }

                _logger.LogTrace("Added {Count} streams to maintain minimum size for {Queue} priority pool",
                    needed, queueName);
            }
        }

        private double CalculatePoolUtilization()
        {
            var totalPoolSize = _highPriorityStreams.Count + _normalPriorityStreams.Count + _lowPriorityStreams.Count;
            var maxPoolSize = MAX_POOL_SIZE_PER_PRIORITY * 3;

            return totalPoolSize > 0 ? (double)totalPoolSize / maxPoolSize : 0.0;
        }

        private double CalculateAverageAcquireCount()
        {
            var totalAcquireCount = 0L;
            var streamCount = 0L;

            foreach (var stream in GetAllPooledStreams())
            {
                totalAcquireCount += stream.AcquireCount;
                streamCount++;
            }

            return streamCount > 0 ? (double)totalAcquireCount / streamCount : 0.0;
        }

        private IEnumerable<PooledStream> GetAllPooledStreams() => _highPriorityStreams.Concat(_normalPriorityStreams).Concat(_lowPriorityStreams);

        private int ConvertToCudaPriority(CudaStreamPriority priority) => priority switch
        {
            CudaStreamPriority.High => _greatestPriority,
            CudaStreamPriority.Low => _leastPriority,
            _ => (_leastPriority + _greatestPriority) / 2
        };

        private void PerformCleanup(object? state)
        {
            if (!_disposed)
            {
                PerformMaintenance();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CudaStreamPool));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _cleanupTimer?.Dispose();

                // Destroy all pooled streams
                DestroyAllStreamsInQueue(_highPriorityStreams, "high");
                DestroyAllStreamsInQueue(_normalPriorityStreams, "normal");
                DestroyAllStreamsInQueue(_lowPriorityStreams, "low");

                _poolSemaphore?.Dispose();

                _logger.LogDebug("CUDA Stream Pool disposed");
            }
        }

        private void DestroyAllStreamsInQueue(ConcurrentQueue<PooledStream> queue, string queueName)
        {
            var destroyedCount = 0;

            while (queue.TryDequeue(out var stream))
            {
                DestroyPooledStream(stream);
                destroyedCount++;
            }

            if (destroyedCount > 0)
            {
                _logger.LogDebug("Destroyed {Count} streams from {Queue} priority pool during disposal",
                    destroyedCount, queueName);
            }
        }
    }

    /// <summary>
    /// Represents a pooled CUDA stream with metadata
    /// </summary>
    internal sealed class PooledStream
    {
        public IntPtr Handle { get; set; }
        public CudaStreamPriority Priority { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? AcquiredAt { get; set; }
        public long AcquireCount { get; set; }
    }

    /// <summary>
    /// Statistics for the CUDA stream pool
    /// </summary>
    public sealed class CudaStreamPoolStatistics
    {
        /// <summary>
        /// Gets or sets the high priority streams.
        /// </summary>
        /// <value>
        /// The high priority streams.
        /// </value>
        public int HighPriorityStreams { get; set; }

        /// <summary>
        /// Gets or sets the normal priority streams.
        /// </summary>
        /// <value>
        /// The normal priority streams.
        /// </value>
        public int NormalPriorityStreams { get; set; }

        /// <summary>
        /// Gets or sets the low priority streams.
        /// </summary>
        /// <value>
        /// The low priority streams.
        /// </value>
        public int LowPriorityStreams { get; set; }

        /// <summary>
        /// Gets or sets the total pooled streams.
        /// </summary>
        /// <value>
        /// The total pooled streams.
        /// </value>
        public int TotalPooledStreams { get; set; }

        /// <summary>
        /// Gets or sets the total acquired.
        /// </summary>
        /// <value>
        /// The total acquired.
        /// </value>
        public long TotalAcquired { get; set; }

        /// <summary>
        /// Gets or sets the total returned.
        /// </summary>
        /// <value>
        /// The total returned.
        /// </value>
        public long TotalReturned { get; set; }

        /// <summary>
        /// Gets or sets the active streams.
        /// </summary>
        /// <value>
        /// The active streams.
        /// </value>
        public long ActiveStreams { get; set; }

        /// <summary>
        /// Gets or sets the pool utilization.
        /// </summary>
        /// <value>
        /// The pool utilization.
        /// </value>
        public double PoolUtilization { get; set; }

        /// <summary>
        /// Gets or sets the average acquire count.
        /// </summary>
        /// <value>
        /// The average acquire count.
        /// </value>
        public double AverageAcquireCount { get; set; }
    }

    /// <summary>
    /// Handle for pooled CUDA streams with automatic return to pool
    /// </summary>
    internal sealed class PooledCudaStreamHandle : CudaStreamHandle
    {
        private readonly CudaStreamPool _pool;
        private readonly PooledStream _pooledStream;
        private readonly bool _poolDisposed;

        internal PooledCudaStreamHandle(StreamId streamId, IntPtr stream, CudaStreamPool pool, PooledStream pooledStream)
            : base(streamId, stream, new PoolReturnManager(pool, pooledStream))
        {
            _pool = pool;
            _pooledStream = pooledStream;
            _poolDisposed = false;
        }

        protected override void ReturnToManager()
        {
            if (!_poolDisposed)
            {
                _pool.Return(_pooledStream.Handle, _pooledStream.Priority);
            }
        }
    }

    /// <summary>
    /// Manages returning streams to the pool
    /// </summary>
    internal sealed class PoolReturnManager : IStreamReturnManager
    {
        private readonly CudaStreamPool _pool;
        private readonly PooledStream _pooledStream;

        internal PoolReturnManager(CudaStreamPool pool, PooledStream pooledStream)
        {
            _pool = pool;
            _pooledStream = pooledStream;
        }

        public void ReturnStreamToPool(StreamId streamId) => _pool.Return(_pooledStream.Handle, _pooledStream.Priority);
    }

    /// <summary>
    /// Interface for stream return management
    /// </summary>
    internal interface IStreamReturnManager
    {
        public void ReturnStreamToPool(StreamId streamId);
    }
}
