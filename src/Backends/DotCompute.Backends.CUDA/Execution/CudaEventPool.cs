// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution
{

    /// <summary>
    /// High-performance CUDA event pool with separate pools for timing and synchronization events
    /// </summary>
    internal sealed class CudaEventPool : IDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger _logger;

        // Separate pools for different event types
        private readonly ConcurrentQueue<PooledEvent> _timingEvents;
        private readonly ConcurrentQueue<PooledEvent> _syncEvents;

        private readonly SemaphoreSlim _poolSemaphore;
        private readonly Timer _maintenanceTimer;
        private readonly object _lockObject = new();

        // Pool configuration
        private const int INITIAL_TIMING_EVENTS = 50;
        private const int INITIAL_SYNC_EVENTS = 50;
        private const int MAX_TIMING_EVENTS = 200;
        private const int MAX_SYNC_EVENTS = 200;
        private const int MIN_TIMING_EVENTS = 10;
        private const int MIN_SYNC_EVENTS = 10;

        // CUDA event flags
        private const uint CUDA_EVENT_DEFAULT = 0x00;
        private const uint CUDA_EVENT_DISABLE_TIMING = 0x02;

        private volatile bool _disposed;
        private long _totalTimingEventsCreated;
        private long _totalSyncEventsCreated;
        private long _totalEventsAcquired;
        private long _totalEventsReturned;

        public CudaEventPool(CudaContext context, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _timingEvents = new ConcurrentQueue<PooledEvent>();
            _syncEvents = new ConcurrentQueue<PooledEvent>();

            var totalPoolSize = INITIAL_TIMING_EVENTS + INITIAL_SYNC_EVENTS;
            _poolSemaphore = new SemaphoreSlim(totalPoolSize * 2, totalPoolSize * 2);

            Initialize();

            // Setup maintenance timer
            _maintenanceTimer = new Timer(PerformCleanup, null,
                TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));

            _logger.LogDebug("CUDA Event Pool initialized: {TimingEvents} timing events, {SyncEvents} sync events",
                _timingEvents.Count, _syncEvents.Count);
        }

        /// <summary>
        /// Acquires a timing event from the pool (enables timing)
        /// </summary>
        public async Task<IntPtr> AcquireTimingEventAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _poolSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var pooledEvent = GetTimingEventFromPool();
                pooledEvent.AcquiredAt = DateTimeOffset.UtcNow;
                pooledEvent.AcquireCount++;

                _ = Interlocked.Increment(ref _totalEventsAcquired);

                _logger.LogTrace("Acquired timing event {Event} (acquired {Count} times)",
                    pooledEvent.Handle, pooledEvent.AcquireCount);

                return pooledEvent.Handle;
            }
            catch
            {
                _ = _poolSemaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// Acquires a synchronization event from the pool (disables timing for performance)
        /// </summary>
        public async Task<IntPtr> AcquireSyncEventAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _poolSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var pooledEvent = GetSyncEventFromPool();
                pooledEvent.AcquiredAt = DateTimeOffset.UtcNow;
                pooledEvent.AcquireCount++;

                _ = Interlocked.Increment(ref _totalEventsAcquired);

                _logger.LogTrace("Acquired sync event {Event} (acquired {Count} times)",
                    pooledEvent.Handle, pooledEvent.AcquireCount);

                return pooledEvent.Handle;
            }
            catch
            {
                _ = _poolSemaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// Returns an event to the appropriate pool
        /// </summary>
        public void Return(IntPtr eventHandle, CudaEventType eventType)
        {
            if (_disposed || eventHandle == IntPtr.Zero)
            {
                _ = _poolSemaphore.Release();
                return;
            }

            var pooledEvent = new PooledEvent
            {
                Handle = eventHandle,
                Type = eventType,
                CreatedAt = DateTimeOffset.UtcNow, // Reset for pool management
                AcquiredAt = null,
                AcquireCount = 0
            };

            var targetQueue = eventType == CudaEventType.Timing ? _timingEvents : _syncEvents;
            var maxPoolSize = eventType == CudaEventType.Timing ? MAX_TIMING_EVENTS : MAX_SYNC_EVENTS;

            // Check pool size limits
            if (targetQueue.Count < maxPoolSize)
            {
                targetQueue.Enqueue(pooledEvent);
                _ = Interlocked.Increment(ref _totalEventsReturned);

                _logger.LogTrace("Returned {Type} event {Event} to pool",
                    eventType, eventHandle);
            }
            else
            {
                // Pool is full, destroy the event
                DestroyPooledEvent(pooledEvent);
                _logger.LogTrace("Destroyed excess {Type} event {Event}",
                    eventType, eventHandle);
            }

            _ = _poolSemaphore.Release();
        }

        /// <summary>
        /// Gets comprehensive pool statistics
        /// </summary>
        public CudaEventPoolStatistics GetStatistics()
        {
            ThrowIfDisposed();

            var timingCount = _timingEvents.Count;
            var syncCount = _syncEvents.Count;

            return new CudaEventPoolStatistics
            {
                TimingEvents = timingCount,
                SyncEvents = syncCount,
                TotalPooledEvents = timingCount + syncCount,
                TotalTimingEventsCreated = _totalTimingEventsCreated,
                TotalSyncEventsCreated = _totalSyncEventsCreated,
                TotalEventsAcquired = _totalEventsAcquired,
                TotalEventsReturned = _totalEventsReturned,
                ActiveEvents = _totalEventsAcquired - _totalEventsReturned,
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
                    // Clean up old events
                    CleanupOldEvents();

                    // Ensure minimum pool sizes
                    EnsureMinimumPoolSizes();

                    // Rebalance pools if needed
                    RebalancePools();

                    _logger.LogTrace("Event pool maintenance completed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during event pool maintenance");
                }
            }
        }

        private void Initialize()
        {
            _context.MakeCurrent();

            // Pre-allocate timing events
            for (var i = 0; i < INITIAL_TIMING_EVENTS; i++)
            {
                var timingEvent = CreateTimingEvent();
                if (timingEvent != null)
                {
                    _timingEvents.Enqueue(timingEvent);
                }
            }

            // Pre-allocate sync events
            for (var i = 0; i < INITIAL_SYNC_EVENTS; i++)
            {
                var syncEvent = CreateSyncEvent();
                if (syncEvent != null)
                {
                    _syncEvents.Enqueue(syncEvent);
                }
            }

            var totalCreated = _timingEvents.Count + _syncEvents.Count;
            _logger.LogDebug("Pre-allocated {TotalEvents} events in pool " +
                            "({TimingEvents} timing, {SyncEvents} sync)",
                totalCreated, _timingEvents.Count, _syncEvents.Count);
        }

        private PooledEvent GetTimingEventFromPool()
        {
            // Try to get from timing events pool first
            if (_timingEvents.TryDequeue(out var timingEvent))
            {
                return timingEvent;
            }

            // No timing events available, create a new one
            var newEvent = CreateTimingEvent();
            if (newEvent == null)
            {
                throw new InvalidOperationException("Failed to create new timing event for pool");
            }

            _logger.LogTrace("Created new timing event {Event} for pool", newEvent.Handle);
            return newEvent;
        }

        private PooledEvent GetSyncEventFromPool()
        {
            // Try to get from sync events pool first
            if (_syncEvents.TryDequeue(out var syncEvent))
            {
                return syncEvent;
            }

            // No sync events available, create a new one
            var newEvent = CreateSyncEvent();
            if (newEvent == null)
            {
                throw new InvalidOperationException("Failed to create new sync event for pool");
            }

            _logger.LogTrace("Created new sync event {Event} for pool", newEvent.Handle);
            return newEvent;
        }

        private PooledEvent? CreateTimingEvent()
        {
            try
            {
                _context.MakeCurrent();

                var eventHandle = IntPtr.Zero;
                var result = Native.CudaRuntime.cudaEventCreateWithFlags(ref eventHandle, CUDA_EVENT_DEFAULT);

                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to create timing event: {Error}",
                        Native.CudaRuntime.GetErrorString(result));
                    return null;
                }

                _ = Interlocked.Increment(ref _totalTimingEventsCreated);

                return new PooledEvent
                {
                    Handle = eventHandle,
                    Type = CudaEventType.Timing,
                    CreatedAt = DateTimeOffset.UtcNow,
                    AcquiredAt = null,
                    AcquireCount = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating timing event");
                return null;
            }
        }

        private PooledEvent? CreateSyncEvent()
        {
            try
            {
                _context.MakeCurrent();

                var eventHandle = IntPtr.Zero;
                var result = Native.CudaRuntime.cudaEventCreateWithFlags(ref eventHandle, CUDA_EVENT_DISABLE_TIMING);

                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to create sync event: {Error}",
                        Native.CudaRuntime.GetErrorString(result));
                    return null;
                }

                _ = Interlocked.Increment(ref _totalSyncEventsCreated);

                return new PooledEvent
                {
                    Handle = eventHandle,
                    Type = CudaEventType.Synchronization,
                    CreatedAt = DateTimeOffset.UtcNow,
                    AcquiredAt = null,
                    AcquireCount = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating sync event");
                return null;
            }
        }

        private void DestroyPooledEvent(PooledEvent pooledEvent)
        {
            try
            {
                _context.MakeCurrent();
                var result = Native.CudaRuntime.cudaEventDestroy(pooledEvent.Handle);
                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to destroy pooled event {Event}: {Error}",
                        pooledEvent.Handle, Native.CudaRuntime.GetErrorString(result));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception destroying pooled event {Event}", pooledEvent.Handle);
            }
        }

        private void CleanupOldEvents()
        {
            var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-10);

            CleanupQueueOldEvents(_timingEvents, cutoffTime, "timing", MIN_TIMING_EVENTS);
            CleanupQueueOldEvents(_syncEvents, cutoffTime, "sync", MIN_SYNC_EVENTS);
        }

        private void CleanupQueueOldEvents(ConcurrentQueue<PooledEvent> queue, DateTimeOffset cutoffTime,
                                         string queueName, int minSize)
        {
            var eventsToKeep = new List<PooledEvent>();
            var destroyedCount = 0;

            // Dequeue all events and decide which to keep
            while (queue.TryDequeue(out var pooledEvent))
            {
                if (pooledEvent.CreatedAt > cutoffTime || eventsToKeep.Count < minSize)
                {
                    eventsToKeep.Add(pooledEvent);
                }
                else
                {
                    DestroyPooledEvent(pooledEvent);
                    destroyedCount++;
                }
            }

            // Re-enqueue events we're keeping
            foreach (var pooledEvent in eventsToKeep)
            {
                queue.Enqueue(pooledEvent);
            }

            if (destroyedCount > 0)
            {
                _logger.LogDebug("Cleaned up {Count} old events from {Queue} pool",
                    destroyedCount, queueName);
            }
        }

        private void EnsureMinimumPoolSizes()
        {
            EnsureMinimumPoolSize(_timingEvents, CudaEventType.Timing, MIN_TIMING_EVENTS, "timing");
            EnsureMinimumPoolSize(_syncEvents, CudaEventType.Synchronization, MIN_SYNC_EVENTS, "sync");
        }

        private void EnsureMinimumPoolSize(ConcurrentQueue<PooledEvent> queue, CudaEventType eventType,
                                         int minSize, string queueName)
        {
            var currentSize = queue.Count;
            var needed = minSize - currentSize;

            if (needed > 0)
            {
                for (var i = 0; i < needed; i++)
                {
                    var pooledEvent = eventType == CudaEventType.Timing ? CreateTimingEvent() : CreateSyncEvent();
                    if (pooledEvent != null)
                    {
                        queue.Enqueue(pooledEvent);
                    }
                }

                _logger.LogTrace("Added {Count} events to maintain minimum size for {Queue} pool",
                    needed, queueName);
            }
        }

        private void RebalancePools()
        {
            // Basic rebalancing logic - could be enhanced based on usage patterns
            var timingCount = _timingEvents.Count;
            var syncCount = _syncEvents.Count;

            // If one pool is significantly larger than the other and usage suggests rebalancing
            if (Math.Abs(timingCount - syncCount) > 20)
            {
                _logger.LogTrace("Pool imbalance detected: timing={TimingCount}, sync={SyncCount}",
                    timingCount, syncCount);

                // More sophisticated rebalancing could be implemented here based on actual usage patterns
            }
        }

        private double CalculatePoolUtilization()
        {
            var totalPoolSize = _timingEvents.Count + _syncEvents.Count;
            var maxPoolSize = MAX_TIMING_EVENTS + MAX_SYNC_EVENTS;

            return totalPoolSize > 0 ? (double)totalPoolSize / maxPoolSize : 0.0;
        }

        private double CalculateAverageAcquireCount()
        {
            var totalAcquireCount = 0L;
            var eventCount = 0L;

            foreach (var pooledEvent in GetAllPooledEvents())
            {
                totalAcquireCount += pooledEvent.AcquireCount;
                eventCount++;
            }

            return eventCount > 0 ? (double)totalAcquireCount / eventCount : 0.0;
        }

        private IEnumerable<PooledEvent> GetAllPooledEvents() => _timingEvents.Concat(_syncEvents);

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
                throw new ObjectDisposedException(nameof(CudaEventPool));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _maintenanceTimer?.Dispose();

                // Destroy all pooled events
                DestroyAllEventsInQueue(_timingEvents, "timing");
                DestroyAllEventsInQueue(_syncEvents, "sync");

                _poolSemaphore?.Dispose();

                _logger.LogDebug("CUDA Event Pool disposed: created {TimingEvents} timing events, " +
                               "{SyncEvents} sync events, served {Acquisitions} acquisitions",
                    _totalTimingEventsCreated, _totalSyncEventsCreated, _totalEventsAcquired);
            }
        }

        private void DestroyAllEventsInQueue(ConcurrentQueue<PooledEvent> queue, string queueName)
        {
            var destroyedCount = 0;

            while (queue.TryDequeue(out var pooledEvent))
            {
                DestroyPooledEvent(pooledEvent);
                destroyedCount++;
            }

            if (destroyedCount > 0)
            {
                _logger.LogDebug("Destroyed {Count} events from {Queue} pool during disposal",
                    destroyedCount, queueName);
            }
        }
    }

    /// <summary>
    /// Represents a pooled CUDA event with metadata
    /// </summary>
    internal sealed class PooledEvent
    {
        public IntPtr Handle { get; set; }
        public CudaEventType Type { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? AcquiredAt { get; set; }
        public long AcquireCount { get; set; }
    }

    /// <summary>
    /// Statistics for the CUDA event pool
    /// </summary>
    public sealed class CudaEventPoolStatistics
    {
        public int TimingEvents { get; set; }
        public int SyncEvents { get; set; }
        public int TotalPooledEvents { get; set; }
        public long TotalTimingEventsCreated { get; set; }
        public long TotalSyncEventsCreated { get; set; }
        public long TotalEventsAcquired { get; set; }
        public long TotalEventsReturned { get; set; }
        public long ActiveEvents { get; set; }
        public double PoolUtilization { get; set; }
        public double AverageAcquireCount { get; set; }
    }
}
