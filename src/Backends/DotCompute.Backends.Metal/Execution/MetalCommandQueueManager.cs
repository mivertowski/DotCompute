// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.Metal.Native;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Production-grade command queue manager with priority support and queue pooling.
/// Optimizes concurrent queue usage and tracks performance metrics.
/// </summary>
public sealed class MetalCommandQueueManager : IDisposable
{
    private readonly IntPtr _device;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<QueuePriority, QueuePool> _queuePools;
    private readonly Timer _cleanupTimer;
    private readonly Lock _statsLock = new();
    private long _totalQueuesCreated;
    private long _totalQueuesReused;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalCommandQueueManager"/> class.
    /// </summary>
    /// <param name="device">The Metal device.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="maxPoolSize">Maximum queue pool size per priority. Default is 8.</param>
    public MetalCommandQueueManager(IntPtr device, ILogger logger, int maxPoolSize = 8)
    {
        if (device == IntPtr.Zero)
        {
            throw new ArgumentException("Device handle cannot be zero", nameof(device));
        }

        _device = device;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _queuePools = new ConcurrentDictionary<QueuePriority, QueuePool>();
        foreach (QueuePriority priority in Enum.GetValues(typeof(QueuePriority)))
        {
            _queuePools[priority] = new QueuePool(maxPoolSize);
        }

        // Setup periodic cleanup timer (every 30 seconds)
        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        _logger.LogInformation("MetalCommandQueueManager initialized with max pool size {MaxPoolSize}", maxPoolSize);
    }

    /// <summary>
    /// Gets or creates a command queue with the specified priority.
    /// Reuses pooled queues when available for better performance.
    /// </summary>
    /// <param name="priority">The queue priority.</param>
    /// <returns>A command queue handle.</returns>
    public IntPtr GetQueue(QueuePriority priority = QueuePriority.Normal)
    {
        ThrowIfDisposed();

        var pool = _queuePools[priority];

        // Try to get from pool first
        if (pool.TryGet(out var queue))
        {
            _ = Interlocked.Increment(ref _totalQueuesReused);
            _logger.LogTrace("Reused command queue from {Priority} pool", priority);
            return queue;
        }

        // Create new queue
        queue = MetalNative.CreateCommandQueue(_device);
        if (queue == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to create command queue with priority {priority}");
        }

        _ = Interlocked.Increment(ref _totalQueuesCreated);
        _logger.LogTrace("Created new command queue for {Priority} priority", priority);

        return queue;
    }

    /// <summary>
    /// Returns a command queue to the pool for reuse.
    /// </summary>
    /// <param name="queue">The command queue to return.</param>
    /// <param name="priority">The queue priority.</param>
    public void ReturnQueue(IntPtr queue, QueuePriority priority = QueuePriority.Normal)
    {
        if (_disposed || queue == IntPtr.Zero)
        {
            return;
        }

        var pool = _queuePools[priority];
        if (!pool.TryReturn(queue))
        {
            // Pool is full, release the queue
            MetalNative.ReleaseCommandQueue(queue);
            _logger.LogTrace("Released command queue (pool full) for {Priority} priority", priority);
        }
        else
        {
            _logger.LogTrace("Returned command queue to {Priority} pool", priority);
        }
    }

    /// <summary>
    /// Gets performance statistics for the queue manager.
    /// </summary>
    public QueueManagerStats GetStats()
    {
        ThrowIfDisposed();

        lock (_statsLock)
        {
            var stats = new QueueManagerStats
            {
                TotalQueuesCreated = _totalQueuesCreated,
                TotalQueuesReused = _totalQueuesReused,
                PoolStats = []
            };

            foreach (var kvp in _queuePools)
            {
                var pool = kvp.Value;
                stats.PoolStats[kvp.Key] = new QueuePoolStats
                {
                    AvailableQueues = pool.Count,
                    MaxPoolSize = pool.MaxSize,
                    Utilization = pool.MaxSize > 0 ? (1.0 - (double)pool.Count / pool.MaxSize) * 100.0 : 0.0
                };
            }

            return stats;
        }
    }

    /// <summary>
    /// Performs cleanup of idle resources.
    /// </summary>
    public void Cleanup()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var pool in _queuePools.Values)
        {
            pool.Cleanup();
        }

        _logger.LogTrace("Performed queue manager cleanup");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Collect stats BEFORE marking as disposed (GetStats checks disposal status)
        var stats = GetStats();

        _disposed = true;

        _cleanupTimer.Dispose();

        // Release all pooled queues
        foreach (var pool in _queuePools.Values)
        {
            pool.Dispose();
        }
        _logger.LogInformation(
            "MetalCommandQueueManager disposed - Created: {Created}, Reused: {Reused}, Reuse Rate: {ReuseRate:F1}%",
            stats.TotalQueuesCreated,
            stats.TotalQueuesReused,
            stats.TotalQueuesCreated > 0 ? (double)stats.TotalQueuesReused / stats.TotalQueuesCreated * 100.0 : 0.0);
    }

    private void PerformCleanup(object? state)
    {
        try
        {
            Cleanup();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during queue manager cleanup");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MetalCommandQueueManager));
        }
    }

    private sealed class QueuePool : IDisposable
    {
        private readonly ConcurrentBag<IntPtr> _queues = [];
        private readonly int _maxSize;

        public int MaxSize => _maxSize;
        public int Count => _queues.Count;

        public QueuePool(int maxSize)
        {
            _maxSize = maxSize;
        }

        public bool TryGet(out IntPtr queue) => _queues.TryTake(out queue);

        public bool TryReturn(IntPtr queue)
        {
            if (_queues.Count >= _maxSize)
            {
                return false;
            }

            _queues.Add(queue);
            return true;
        }

        public void Cleanup()
        {
            // Could implement aging logic here if needed
        }

        public void Dispose()
        {
            while (_queues.TryTake(out var queue))
            {
                MetalNative.ReleaseCommandQueue(queue);
            }
        }
    }
}

/// <summary>
/// Queue priority levels.
/// </summary>
public enum QueuePriority
{
    /// <summary>Low priority queue for background operations.</summary>
    Low = 0,
    /// <summary>Normal priority queue for regular operations.</summary>
    Normal = 1,
    /// <summary>High priority queue for time-critical operations.</summary>
    High = 2
}

/// <summary>
/// Queue manager statistics.
/// </summary>
public sealed class QueueManagerStats
{
    /// <summary>Gets or sets the total number of queues created.</summary>
    public long TotalQueuesCreated { get; set; }

    /// <summary>Gets or sets the total number of queues reused.</summary>
    public long TotalQueuesReused { get; set; }

    /// <summary>Gets or sets the pool statistics per priority.</summary>
    public required Dictionary<QueuePriority, QueuePoolStats> PoolStats { get; set; }
}

/// <summary>
/// Queue pool statistics.
/// </summary>
public sealed class QueuePoolStats
{
    /// <summary>Gets or sets the number of available queues.</summary>
    public int AvailableQueues { get; set; }

    /// <summary>Gets or sets the maximum pool size.</summary>
    public int MaxPoolSize { get; set; }

    /// <summary>Gets or sets the pool utilization percentage.</summary>
    public double Utilization { get; set; }
}
