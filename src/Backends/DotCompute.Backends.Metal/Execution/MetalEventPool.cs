// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Efficient pool for managing Metal events to reduce allocation overhead and improve performance.
/// Follows CUDA event pool patterns with Metal-specific optimizations.
/// </summary>
public sealed class MetalEventPool : IDisposable
{
    private readonly IntPtr _device;
    private readonly ILogger<MetalEventManager> _logger;
    private readonly ConcurrentQueue<IntPtr> _timingEventPool;
    private readonly ConcurrentQueue<IntPtr> _syncEventPool;
    private readonly ConcurrentDictionary<IntPtr, MetalEventPoolInfo> _activeEvents;
    private readonly Timer _maintenanceTimer;
    private readonly Lock _lockObject = new();

    // Pool configuration
    private const int MIN_TIMING_POOL_SIZE = 10;
    private const int MAX_TIMING_POOL_SIZE = 50;
    private const int MIN_SYNC_POOL_SIZE = 10;
    private const int MAX_SYNC_POOL_SIZE = 50;
    private const int POOL_GROWTH_SIZE = 5;

    private int _timingPoolSize;
    private int _syncPoolSize;
    private int _totalCreated;
    private int _totalReused;
    private volatile bool _disposed;

    public MetalEventPool(IntPtr device, ILogger<MetalEventManager> logger)
    {
        _device = device != IntPtr.Zero ? device : throw new ArgumentNullException(nameof(device));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timingEventPool = new ConcurrentQueue<IntPtr>();
        _syncEventPool = new ConcurrentQueue<IntPtr>();
        _activeEvents = new ConcurrentDictionary<IntPtr, MetalEventPoolInfo>();

        Initialize();

        // Setup maintenance timer
        _maintenanceTimer = new Timer(PerformMaintenance, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _logger.LogInformation("Metal Event Pool initialized: timing pool={TimingSize}, sync pool={SyncSize}",
            _timingPoolSize, _syncPoolSize);
    }

    /// <summary>
    /// Acquires a timing event from the pool or creates a new one
    /// </summary>
    public async Task<IntPtr> AcquireTimingEventAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Try to get from pool first
        if (_timingEventPool.TryDequeue(out var eventHandle))
        {
            _ = Interlocked.Decrement(ref _timingPoolSize);
            _ = Interlocked.Increment(ref _totalReused);


            var info = new MetalEventPoolInfo
            {
                EventType = MetalEventType.Timing,
                AcquiredAt = DateTimeOffset.UtcNow,
                IsFromPool = true
            };
            _activeEvents[eventHandle] = info;

            _logger.LogTrace("Reused timing event from pool: {EventHandle}", eventHandle);
            return eventHandle;
        }

        // Create new event
        eventHandle = await CreateTimingEventAsync(cancellationToken).ConfigureAwait(false);
        _ = Interlocked.Increment(ref _totalCreated);

        var newInfo = new MetalEventPoolInfo
        {
            EventType = MetalEventType.Timing,
            AcquiredAt = DateTimeOffset.UtcNow,
            IsFromPool = false
        };
        _activeEvents[eventHandle] = newInfo;

        _logger.LogTrace("Created new timing event: {EventHandle}", eventHandle);
        return eventHandle;
    }

    /// <summary>
    /// Acquires a synchronization event from the pool or creates a new one
    /// </summary>
    public async Task<IntPtr> AcquireSyncEventAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Try to get from pool first
        if (_syncEventPool.TryDequeue(out var eventHandle))
        {
            _ = Interlocked.Decrement(ref _syncPoolSize);
            _ = Interlocked.Increment(ref _totalReused);

            var info = new MetalEventPoolInfo
            {
                EventType = MetalEventType.Synchronization,
                AcquiredAt = DateTimeOffset.UtcNow,
                IsFromPool = true
            };
            _activeEvents[eventHandle] = info;

            _logger.LogTrace("Reused sync event from pool: {EventHandle}", eventHandle);
            return eventHandle;
        }

        // Create new event
        eventHandle = await CreateSyncEventAsync(cancellationToken).ConfigureAwait(false);
        _ = Interlocked.Increment(ref _totalCreated);

        var newInfo = new MetalEventPoolInfo
        {
            EventType = MetalEventType.Synchronization,
            AcquiredAt = DateTimeOffset.UtcNow,
            IsFromPool = false
        };
        _activeEvents[eventHandle] = newInfo;

        _logger.LogTrace("Created new sync event: {EventHandle}", eventHandle);
        return eventHandle;
    }

    /// <summary>
    /// Returns an event to the appropriate pool for reuse
    /// </summary>
    public void Return(IntPtr eventHandle, MetalEventType eventType)
    {
        if (eventHandle == IntPtr.Zero || _disposed)
        {
            return;
        }

        // Remove from active tracking
        if (!_activeEvents.TryRemove(eventHandle, out var info))
        {
            _logger.LogWarning("Attempted to return untracked event: {EventHandle}", eventHandle);
            DestroyEvent(eventHandle);
            return;
        }

        // Determine which pool to return to
        var shouldReturnToPool = eventType switch
        {
            MetalEventType.Timing => _timingPoolSize < MAX_TIMING_POOL_SIZE,
            MetalEventType.Synchronization => _syncPoolSize < MAX_SYNC_POOL_SIZE,
            _ => false
        };

        if (shouldReturnToPool && !IsEventStale(info))
        {
            // Return to appropriate pool
            switch (eventType)
            {
                case MetalEventType.Timing:
                    _timingEventPool.Enqueue(eventHandle);
                    _ = Interlocked.Increment(ref _timingPoolSize);
                    _logger.LogTrace("Returned timing event to pool: {EventHandle}", eventHandle);
                    break;

                case MetalEventType.Synchronization:
                    _syncEventPool.Enqueue(eventHandle);
                    _ = Interlocked.Increment(ref _syncPoolSize);
                    _logger.LogTrace("Returned sync event to pool: {EventHandle}", eventHandle);
                    break;
            }
        }
        else
        {
            // Pool is full or event is stale, destroy it
            DestroyEvent(eventHandle);
            _logger.LogTrace("Destroyed event (pool full or stale): {EventHandle}", eventHandle);
        }
    }

    /// <summary>
    /// Performs maintenance on the event pools
    /// </summary>
    public void PerformMaintenance()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lockObject)
        {
            // Clean timing event pool
            var timingCleaned = CleanPool(_timingEventPool, ref _timingPoolSize, MIN_TIMING_POOL_SIZE);

            // Clean sync event pool

            var syncCleaned = CleanPool(_syncEventPool, ref _syncPoolSize, MIN_SYNC_POOL_SIZE);

            var totalCleaned = timingCleaned + syncCleaned;
            if (totalCleaned > 0)
            {
                _logger.LogDebug("Pool maintenance cleaned {TotalCleaned} events (timing: {TimingCleaned}, sync: {SyncCleaned})", totalCleaned, timingCleaned, syncCleaned);
            }

            // Clean up long-running active events (potential leaks)
            CleanupStaleActiveEvents();

            // Ensure minimum pool sizes
            EnsureMinimumPoolSizes();
        }
    }

    /// <summary>
    /// Gets statistics about the event pools
    /// </summary>
#pragma warning disable CA1721 // Property name conflicts with method - both exist for API compatibility
    public MetalEventPoolStatistics Statistics => new MetalEventPoolStatistics
    {
        TimingPoolSize = _timingPoolSize,
        SyncPoolSize = _syncPoolSize,
        ActiveEvents = _activeEvents.Count,
        TotalCreated = _totalCreated,
        TotalReused = _totalReused,
        ReuseRatio = _totalCreated > 0 ? (double)_totalReused / (_totalCreated + _totalReused) : 0.0,
        MaxTimingPoolSize = MAX_TIMING_POOL_SIZE,
        MaxSyncPoolSize = MAX_SYNC_POOL_SIZE
    };

    private void Initialize()
    {
        // Pre-populate pools with minimum sizes
        _ = Task.Run(async () =>
        {
            try
            {
                // Create initial timing events
                for (var i = 0; i < MIN_TIMING_POOL_SIZE; i++)
                {
                    var eventHandle = await CreateTimingEventAsync().ConfigureAwait(false);
                    _timingEventPool.Enqueue(eventHandle);
                    _ = Interlocked.Increment(ref _timingPoolSize);
                }

                // Create initial sync events
                for (var i = 0; i < MIN_SYNC_POOL_SIZE; i++)
                {
                    var eventHandle = await CreateSyncEventAsync().ConfigureAwait(false);
                    _syncEventPool.Enqueue(eventHandle);
                    _ = Interlocked.Increment(ref _syncPoolSize);
                }

                _logger.LogDebug("Initialized event pools: timing={TimingPoolSize}, sync={SyncPoolSize}", _timingPoolSize, _syncPoolSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing event pools");
            }
        });
    }

    // Monotonic counters for mock event handles (thread-safe)
    private static long s_timingEventCounter = 1000;
    private static long s_syncEventCounter = 100000;

    private static async Task<IntPtr> CreateTimingEventAsync(CancellationToken cancellationToken = default)
    {
        // For Metal, this would create an MTLSharedEvent via Metal.NET or Objective-C interop
        // Mock implementation uses monotonic counter for predictable, unique handles
        // In production, replace with: device.newSharedEvent() or similar Metal API
        await Task.CompletedTask; // Async signature for future Metal API compatibility

        var handleValue = Interlocked.Increment(ref s_timingEventCounter);
        return new IntPtr(handleValue);
    }

    private static async Task<IntPtr> CreateSyncEventAsync(CancellationToken cancellationToken = default)
    {
        // For Metal, this would create an MTLSharedEvent for synchronization
        // Mock implementation uses monotonic counter for predictable, unique handles
        // In production, replace with: device.newSharedEvent() or similar Metal API
        await Task.CompletedTask; // Async signature for future Metal API compatibility

        var handleValue = Interlocked.Increment(ref s_syncEventCounter);
        return new IntPtr(handleValue);
    }

    private void DestroyEvent(IntPtr eventHandle)
    {
        try
        {
            // In a real implementation, this would release the Metal event
            // For now, this is a placeholder
            _logger.LogTrace("Destroyed event: {EventHandle}", eventHandle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error destroying event {EventHandle}", eventHandle);
        }
    }

    private static bool IsEventStale(MetalEventPoolInfo info)
    {
        // Consider events older than 2 minutes as stale
        var age = DateTimeOffset.UtcNow - info.AcquiredAt;
        return age.TotalMinutes > 2;
    }

    private int CleanPool(ConcurrentQueue<IntPtr> pool, ref int poolSize, int minSize)
    {
        var cleaned = 0;
        var currentSize = poolSize;

        // Only clean if we have more than minimum

        if (currentSize <= minSize)
        {
            return 0;
        }

        var excessCount = currentSize - minSize;
        for (var i = 0; i < excessCount; i++)
        {
            if (pool.TryDequeue(out var eventHandle))
            {
                DestroyEvent(eventHandle);
                _ = Interlocked.Decrement(ref poolSize);
                cleaned++;
            }
            else
            {
                break;
            }
        }

        return cleaned;
    }

    private void CleanupStaleActiveEvents()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10); // 10 minute threshold
        var staleEvents = _activeEvents
            .Where(kvp => kvp.Value.AcquiredAt < cutoff)
            .Take(20) // Limit cleanup batch
            .ToList();

        foreach (var (eventHandle, info) in staleEvents)
        {
            if (_activeEvents.TryRemove(eventHandle, out _))
            {
                DestroyEvent(eventHandle);
                _logger.LogWarning("Cleaned up stale active event: {EventHandle}, age: {Age}",
                    eventHandle, DateTimeOffset.UtcNow - info.AcquiredAt);
            }
        }
    }

    private void EnsureMinimumPoolSizes()
    {
        // Ensure timing pool has minimum size
        var timingDeficit = MIN_TIMING_POOL_SIZE - _timingPoolSize;
        if (timingDeficit > 0)
        {
            _ = Task.Run(async () =>
            {
                for (var i = 0; i < Math.Min(timingDeficit, POOL_GROWTH_SIZE); i++)
                {
                    try
                    {
                        var eventHandle = await CreateTimingEventAsync().ConfigureAwait(false);
                        _timingEventPool.Enqueue(eventHandle);
                        _ = Interlocked.Increment(ref _timingPoolSize);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error creating timing event for minimum pool size");
                        break;
                    }
                }
            });
        }

        // Ensure sync pool has minimum size
        var syncDeficit = MIN_SYNC_POOL_SIZE - _syncPoolSize;
        if (syncDeficit > 0)
        {
            _ = Task.Run(async () =>
            {
                for (var i = 0; i < Math.Min(syncDeficit, POOL_GROWTH_SIZE); i++)
                {
                    try
                    {
                        var eventHandle = await CreateSyncEventAsync().ConfigureAwait(false);
                        _syncEventPool.Enqueue(eventHandle);
                        _ = Interlocked.Increment(ref _syncPoolSize);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error creating sync event for minimum pool size");
                        break;
                    }
                }
            });
        }
    }

    private void PerformMaintenance(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            PerformMaintenance();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during event pool maintenance");
        }
    }


#pragma warning disable CA1024 // Method form intentional for API compatibility with callers expecting method syntax
    /// <summary>
    /// Gets statistics about the event pool (API compatibility method).
    /// </summary>
    public MetalEventPoolStatistics GetStatistics()
    {
        var total = _totalCreated;
        var reused = _totalReused;
        return new MetalEventPoolStatistics
        {
            TimingPoolSize = _timingPoolSize,
            SyncPoolSize = _syncPoolSize,
            ActiveEvents = _activeEvents.Count,
            TotalCreated = total,
            TotalReused = reused,
            ReuseRatio = total > 0 ? (double)reused / total : 0.0,
            MaxTimingPoolSize = MAX_TIMING_POOL_SIZE,
            MaxSyncPoolSize = MAX_SYNC_POOL_SIZE
        };
    }
#pragma warning restore CA1024
#pragma warning restore CA1721

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _maintenanceTimer?.Dispose();

        _logger.LogDebug("Disposing Metal Event Pool...");

        // Destroy all events in timing pool
        while (_timingEventPool.TryDequeue(out var timingEvent))
        {
            DestroyEvent(timingEvent);
        }

        // Destroy all events in sync pool
        while (_syncEventPool.TryDequeue(out var syncEvent))
        {
            DestroyEvent(syncEvent);
        }

        // Destroy all active events
        foreach (var eventHandle in _activeEvents.Keys.ToList())
        {
            if (_activeEvents.TryRemove(eventHandle, out _))
            {
                DestroyEvent(eventHandle);
            }
        }

        var stats = GetStatistics();
        _logger.LogInformation(
            "Metal Event Pool disposed: created={Created}, reused={Reused}, reuse ratio={ReuseRatio:P2}",
            stats.TotalCreated, stats.TotalReused, stats.ReuseRatio);
    }
}

/// <summary>
/// Information about an event in the pool
/// </summary>
internal sealed class MetalEventPoolInfo
{
    public MetalEventType EventType { get; set; }
    public DateTimeOffset AcquiredAt { get; set; }
    public bool IsFromPool { get; set; }
}

/// <summary>
/// Statistics about the Metal event pool
/// </summary>
public sealed class MetalEventPoolStatistics
{
    public int TimingPoolSize { get; set; }
    public int SyncPoolSize { get; set; }
    public int ActiveEvents { get; set; }
    public int TotalCreated { get; set; }
    public int TotalReused { get; set; }
    public double ReuseRatio { get; set; }
    public int MaxTimingPoolSize { get; set; }
    public int MaxSyncPoolSize { get; set; }

    /// <summary>
    /// Overall pool utilization percentage
    /// </summary>
    public double Utilization => (double)(TimingPoolSize + SyncPoolSize) / (MaxTimingPoolSize + MaxSyncPoolSize) * 100.0;

    /// <summary>
    /// Pool efficiency based on reuse ratio
    /// </summary>
    public double Efficiency => ReuseRatio * 100.0;
}
