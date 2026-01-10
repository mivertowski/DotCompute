// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Utilities;

/// <summary>
/// Thread-safe pool of Metal command queues for concurrent kernel execution.
/// Enables true parallelism by allowing multiple kernels to execute on different queues simultaneously.
/// </summary>
public sealed class MetalCommandQueuePool : IDisposable
{
    private readonly IntPtr _device;
    private readonly ILogger<MetalCommandQueuePool> _logger;
    private readonly ConcurrentQueue<QueueEntry> _availableQueues = new();
    private readonly ConcurrentDictionary<IntPtr, QueueEntry> _allQueues = new();
    private readonly int _maxConcurrency;
    private readonly SemaphoreSlim _queueAvailableSemaphore;
    private int _disposed;
    private int _nextQueueId;

    /// <summary>
    /// Initializes a new instance of the MetalCommandQueuePool.
    /// </summary>
    /// <param name="device">Metal device to create queues from.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent command queues (default: CPU core count).</param>
    public MetalCommandQueuePool(
        IntPtr device,
        ILogger<MetalCommandQueuePool> logger,
        int? maxConcurrency = null)
    {
        _device = device;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxConcurrency = maxConcurrency ?? Environment.ProcessorCount;
        _queueAvailableSemaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

        _logger.LogInformation(
            "Command queue pool initialized with max concurrency: {MaxConcurrency}",
            _maxConcurrency);

        // Pre-create all queues for the pool
        for (var i = 0; i < _maxConcurrency; i++)
        {
            var queue = CreateQueue();
            _availableQueues.Enqueue(queue);
        }

        _logger.LogDebug("Pre-created {Count} command queues", _maxConcurrency);
    }

    /// <summary>
    /// Gets a command queue from the pool for exclusive use.
    /// Blocks if all queues are currently in use.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A command queue entry with queue handle and metadata.</returns>
    public async ValueTask<QueueEntry> AcquireQueueAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        // Wait for a queue to become available
        await _queueAvailableSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Try to get an available queue
            if (_availableQueues.TryDequeue(out var entry))
            {
                entry.LastAcquiredTime = DateTime.UtcNow;
                entry.IsInUse = true;
                _ = Interlocked.Increment(ref entry.UsageCountRef);

                _logger.LogDebug(
                    "Acquired queue {QueueId} (0x{Queue:X}), usage count: {Count}",
                    entry.QueueId,
                    entry.Queue.ToInt64(),
                    entry.UsageCount);

                return entry;
            }

            // This should never happen if semaphore works correctly
            throw new InvalidOperationException("Failed to acquire queue from pool despite semaphore signal");
        }
        catch
        {
            // Release semaphore on failure
            _ = _queueAvailableSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Returns a command queue to the pool for reuse.
    /// </summary>
    /// <param name="entry">The queue entry to return.</param>
    public void ReleaseQueue(QueueEntry entry)
    {
        if (entry == null || _disposed > 0)
        {
            return;
        }

        if (!entry.IsInUse)
        {
            _logger.LogWarning(
                "Attempted to release queue {QueueId} that was not marked as in use",
                entry.QueueId);
            return;
        }

        entry.LastReleasedTime = DateTime.UtcNow;
        entry.IsInUse = false;

        _availableQueues.Enqueue(entry);

        _logger.LogDebug(
            "Released queue {QueueId} (0x{Queue:X}), total usage: {Count}",
            entry.QueueId,
            entry.Queue.ToInt64(),
            entry.UsageCount);

        // Signal that a queue is now available
        _ = _queueAvailableSemaphore.Release();
    }

    /// <summary>
    /// Gets statistics about the command queue pool.
    /// </summary>
    public CommandQueuePoolStats Stats
    {
        get
        {
            var availableCount = _availableQueues.Count;
            var inUseCount = _allQueues.Values.Count(q => q.IsInUse);

            return new CommandQueuePoolStats
            {
                TotalQueues = _allQueues.Count,
                AvailableQueues = availableCount,
                InUseQueues = inUseCount,
                MaxConcurrency = _maxConcurrency,
                TotalUsageCount = _allQueues.Values.Sum(q => q.UsageCount)
            };
        }
    }

    /// <summary>
    /// Creates a new command queue entry.
    /// </summary>
    private QueueEntry CreateQueue()
    {
        var queue = MetalNative.CreateCommandQueue(_device);
        if (queue == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal command queue");
        }

        var queueId = Interlocked.Increment(ref _nextQueueId);
        var entry = new QueueEntry
        {
            Queue = queue,
            QueueId = queueId,
            CreatedTime = DateTime.UtcNow,
            IsInUse = false
        };

        if (!_allQueues.TryAdd(queue, entry))
        {
            MetalNative.ReleaseCommandQueue(queue);
            throw new InvalidOperationException($"Failed to track command queue {queueId}");
        }

        _logger.LogDebug(
            "Created command queue {QueueId} (0x{Queue:X})",
            queueId,
            queue.ToInt64());

        return entry;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _logger.LogDebug("Command queue pool disposing...");

        // Log final statistics
        var stats = Stats;
        _logger.LogInformation(
            "Command queue pool statistics: Total={Total}, InUse={InUse}, TotalUsage={Usage}",
            stats.TotalQueues,
            stats.InUseQueues,
            stats.TotalUsageCount);

        // Release all queues
        foreach (var entry in _allQueues.Values)
        {
            try
            {
                MetalNative.ReleaseCommandQueue(entry.Queue);
                _logger.LogDebug("Released queue {QueueId}", entry.QueueId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release queue {QueueId}", entry.QueueId);
            }
        }

        _allQueues.Clear();
        _queueAvailableSemaphore.Dispose();

        _logger.LogDebug("Command queue pool disposed");
    }

    /// <summary>
    /// Entry representing a command queue in the pool with metadata.
    /// Nested type is intentional for tight coupling with pool lifecycle.
    /// </summary>
#pragma warning disable CA1034 // Nested type is tightly coupled to MetalCommandQueuePool lifecycle
    public sealed class QueueEntry
#pragma warning restore CA1034
    {
        private int _usageCount;

        /// <summary>
        /// Gets the Metal command queue handle.
        /// </summary>
        public required IntPtr Queue { get; init; }

        /// <summary>
        /// Gets the unique queue ID for tracking.
        /// </summary>
        public required int QueueId { get; init; }

        /// <summary>
        /// Gets the time this queue was created.
        /// </summary>
        public required DateTime CreatedTime { get; init; }

        /// <summary>
        /// Gets or sets the last time this queue was acquired.
        /// </summary>
        public DateTime LastAcquiredTime { get; set; }

        /// <summary>
        /// Gets or sets the last time this queue was released.
        /// </summary>
        public DateTime LastReleasedTime { get; set; }

        /// <summary>
        /// Gets or sets whether this queue is currently in use.
        /// </summary>
        public bool IsInUse { get; set; }

        /// <summary>
        /// Gets the total number of times this queue has been used.
        /// Thread-safe via Interlocked operations.
        /// </summary>
        public int UsageCount => _usageCount;

        /// <summary>
        /// Gets a reference to the usage count for Interlocked operations.
        /// </summary>
        internal ref int UsageCountRef => ref _usageCount;
    }
}

/// <summary>
/// Statistics about a command queue pool.
/// </summary>
public sealed class CommandQueuePoolStats
{
    /// <summary>
    /// Total number of queues in the pool.
    /// </summary>
    public int TotalQueues { get; init; }

    /// <summary>
    /// Number of queues currently available for use.
    /// </summary>
    public int AvailableQueues { get; init; }

    /// <summary>
    /// Number of queues currently in use.
    /// </summary>
    public int InUseQueues { get; init; }

    /// <summary>
    /// Maximum concurrency level (max queues that can be used simultaneously).
    /// </summary>
    public int MaxConcurrency { get; init; }

    /// <summary>
    /// Total number of times queues have been acquired across all queues.
    /// </summary>
    public long TotalUsageCount { get; init; }

    /// <summary>
    /// Utilization percentage of the pool.
    /// </summary>
    public double Utilization => MaxConcurrency > 0
        ? (double)InUseQueues / MaxConcurrency * 100.0
        : 0.0;
}
