// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable XFIX003 // Use LoggerMessage.Define - will be refactored with proper implementation

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Scheduling;
using DotCompute.Abstractions.Temporal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Scheduling;

/// <summary>
/// CUDA implementation of hierarchical task queue with priority-based scheduling and HLC temporal ordering.
/// Provides CPU-based implementation with lock-free operations per priority level.
/// </summary>
/// <remarks>
/// <para><b>Architecture:</b></para>
/// <list type="bullet">
/// <item>Three priority queues (High, Normal, Low) using sorted lists</item>
/// <item>HLC-based temporal ordering within each priority level</item>
/// <item>Lock-free reads with atomic updates for statistics</item>
/// <item>Periodic rebalancing based on age and system load</item>
/// </list>
///
/// <para><b>Implementation Details:</b></para>
/// <list type="bullet">
/// <item>Uses <see cref="SortedSet{T}"/> for O(log n) insertion with HLC ordering</item>
/// <item>Reader-writer locks for minimal contention</item>
/// <item>Atomic counters for statistics tracking</item>
/// <item>Starvation prevention via age-based promotion</item>
/// </list>
/// </remarks>
public sealed class CudaHierarchicalTaskQueue : IHierarchicalTaskQueue
{
    private readonly ILogger<CudaHierarchicalTaskQueue> _logger;
    private readonly string _queueId;
    private readonly IHybridLogicalClock _hlc;

    // Three priority-level queues (High, Normal, Low)
    private readonly SortedSet<PrioritizedTask>[] _priorityQueues;
    private readonly ReaderWriterLockSlim[] _queueLocks;

    // Statistics (atomic for lock-free reads)
    private long _totalEnqueued;
    private long _totalDequeued;
    private long _totalStolen;
    private readonly object _statsLock = new();

    // Wait time tracking per priority
    private readonly List<TimeSpan>[] _waitTimesByPriority;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaHierarchicalTaskQueue"/> class.
    /// </summary>
    /// <param name="queueId">Unique identifier for this queue.</param>
    /// <param name="hlc">Hybrid Logical Clock for timestamping.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null.</exception>
    public CudaHierarchicalTaskQueue(
        string queueId,
        IHybridLogicalClock hlc,
        ILogger<CudaHierarchicalTaskQueue>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueId);
        ArgumentNullException.ThrowIfNull(hlc);

        _logger = logger ?? NullLogger<CudaHierarchicalTaskQueue>.Instance;
        _queueId = queueId;
        _hlc = hlc;

        // Initialize three priority queues
        _priorityQueues = new SortedSet<PrioritizedTask>[3];
        _queueLocks = new ReaderWriterLockSlim[3];
        _waitTimesByPriority = new List<TimeSpan>[3];

        for (var i = 0; i < 3; i++)
        {
            _priorityQueues[i] = new SortedSet<PrioritizedTask>();
            _queueLocks[i] = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _waitTimesByPriority[i] = new List<TimeSpan>();
        }

        _logger.LogInformation(
            "Created hierarchical task queue '{QueueId}' with HLC integration",
            _queueId);
    }

    /// <inheritdoc/>
    public string QueueId => _queueId;

    /// <inheritdoc/>
    public int TotalTaskCount
    {
        get
        {
            var total = 0;
            for (var i = 0; i < 3; i++)
            {
                _queueLocks[i].EnterReadLock();
                try
                {
                    total += _priorityQueues[i].Count;
                }
                finally
                {
                    _queueLocks[i].ExitReadLock();
                }
            }
            return total;
        }
    }

    /// <inheritdoc/>
    public TaskPriorityCounts PriorityCounts
    {
        get
        {
            int high = 0, normal = 0, low = 0;

            _queueLocks[0].EnterReadLock();
            try
            {
                high = _priorityQueues[0].Count;
            }
            finally
            {
                _queueLocks[0].ExitReadLock();
            }

            _queueLocks[1].EnterReadLock();
            try
            {
                normal = _priorityQueues[1].Count;
            }
            finally
            {
                _queueLocks[1].ExitReadLock();
            }

            _queueLocks[2].EnterReadLock();
            try
            {
                low = _priorityQueues[2].Count;
            }
            finally
            {
                _queueLocks[2].ExitReadLock();
            }

            return new TaskPriorityCounts
            {
                HighPriority = high,
                NormalPriority = normal,
                LowPriority = low
            };
        }
    }

    /// <inheritdoc/>
    public bool TryEnqueue(PrioritizedTask task, TaskPriority priority, HlcTimestamp timestamp)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var queueIndex = (int)priority;

        _queueLocks[queueIndex].EnterWriteLock();
        try
        {
            // Update task with enqueue timestamp if not already set
            var enqueuedTask = task with { EnqueueTimestamp = timestamp, Priority = priority };

            var added = _priorityQueues[queueIndex].Add(enqueuedTask);

            if (added)
            {
                Interlocked.Increment(ref _totalEnqueued);

                _logger.LogDebug(
                    "Enqueued task {TaskId} to {Priority} priority queue (HLC: {Timestamp})",
                    task.TaskId, priority, timestamp);
            }

            return added;
        }
        finally
        {
            _queueLocks[queueIndex].ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public bool TryDequeue(out PrioritizedTask task)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Try queues in priority order: High → Normal → Low
        for (var priority = 0; priority < 3; priority++)
        {
            _queueLocks[priority].EnterWriteLock();
            try
            {
                if (_priorityQueues[priority].Count > 0)
                {
                    // Get first task (earliest HLC timestamp at this priority)
                    task = _priorityQueues[priority].Min!;
                    _priorityQueues[priority].Remove(task);

                    Interlocked.Increment(ref _totalDequeued);

                    // Track wait time
                    var currentTime = _hlc.GetCurrent();
                    var waitTime = TimeSpan.FromTicks(
                        (currentTime.PhysicalTimeNanos - task.EnqueueTimestamp.PhysicalTimeNanos) / 100);

                    lock (_statsLock)
                    {
                        _waitTimesByPriority[priority].Add(waitTime);
                    }

                    _logger.LogDebug(
                        "Dequeued task {TaskId} from {Priority} priority queue (wait: {WaitTimeUs}μs)",
                        task.TaskId, (TaskPriority)priority, waitTime.TotalMicroseconds);

                    return true;
                }
            }
            finally
            {
                _queueLocks[priority].ExitWriteLock();
            }
        }

        task = default;
        return false;
    }

    /// <inheritdoc/>
    public bool TrySteal(out PrioritizedTask task, TaskPriority preferredPriority = TaskPriority.Normal)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Try preferred priority first, then others
        var startPriority = (int)preferredPriority;

        for (var offset = 0; offset < 3; offset++)
        {
            var priority = (startPriority + offset) % 3;

            _queueLocks[priority].EnterWriteLock();
            try
            {
                if (_priorityQueues[priority].Count > 0)
                {
                    // Steal last task (work-stealing takes from tail)
                    task = _priorityQueues[priority].Max!;

                    // Only steal if task is marked as stealable
                    if (!task.Flags.HasFlag(TaskFlags.Stealable))
                    {
                        continue;
                    }

                    _priorityQueues[priority].Remove(task);

                    Interlocked.Increment(ref _totalStolen);

                    _logger.LogDebug(
                        "Stole task {TaskId} from {Priority} priority queue",
                        task.TaskId, (TaskPriority)priority);

                    return true;
                }
            }
            finally
            {
                _queueLocks[priority].ExitWriteLock();
            }
        }

        task = default;
        return false;
    }

    /// <inheritdoc/>
    public bool TryPeek(out PrioritizedTask task)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Peek at highest-priority queue with tasks
        for (var priority = 0; priority < 3; priority++)
        {
            _queueLocks[priority].EnterReadLock();
            try
            {
                if (_priorityQueues[priority].Count > 0)
                {
                    task = _priorityQueues[priority].Min!;
                    return true;
                }
            }
            finally
            {
                _queueLocks[priority].ExitReadLock();
            }
        }

        task = default;
        return false;
    }

    /// <inheritdoc/>
    public int PromoteAgedTasks(HlcTimestamp currentTime, TimeSpan ageThreshold = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (ageThreshold == default)
        {
            ageThreshold = TimeSpan.FromSeconds(1);
        }

        var promotedCount = 0;

        // Promote from Low → Normal
        promotedCount += PromoteTasksBetweenQueues(
            TaskPriority.Low,
            TaskPriority.Normal,
            currentTime,
            ageThreshold);

        // Promote from Normal → High (with 2x threshold)
        promotedCount += PromoteTasksBetweenQueues(
            TaskPriority.Normal,
            TaskPriority.High,
            currentTime,
            ageThreshold * 2);

        if (promotedCount > 0)
        {
            _logger.LogInformation(
                "Promoted {Count} aged tasks in queue '{QueueId}'",
                promotedCount, _queueId);
        }

        return promotedCount;
    }

    /// <inheritdoc/>
    public RebalanceResult Rebalance(double loadFactor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var stopwatch = Stopwatch.StartNew();
        var promoted = 0;
        var demoted = 0;

        if (loadFactor > 0.8)
        {
            // High load: demote normal → low to reduce contention
            demoted = DemoteTasksBetweenQueues(TaskPriority.Normal, TaskPriority.Low, maxCount: 10);
        }
        else if (loadFactor < 0.2)
        {
            // Low load: promote low → normal to increase throughput
            var currentTime = _hlc.GetCurrent();
            promoted = PromoteTasksBetweenQueues(
                TaskPriority.Low,
                TaskPriority.Normal,
                currentTime,
                TimeSpan.Zero); // Promote all eligible
        }

        stopwatch.Stop();

        return new RebalanceResult
        {
            PromotedCount = promoted,
            DemotedCount = demoted,
            Duration = stopwatch.Elapsed,
            NewCounts = PriorityCounts
        };
    }

    /// <inheritdoc/>
    public HierarchicalQueueStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var counts = PriorityCounts;

        TimeSpan highWait = default, normalWait = default, lowWait = default;
        HlcTimestamp oldest = default, newest = default;

        lock (_statsLock)
        {
            highWait = CalculateAverageWaitTime(0);
            normalWait = CalculateAverageWaitTime(1);
            lowWait = CalculateAverageWaitTime(2);
        }

        // Find oldest and newest timestamps
        for (var priority = 0; priority < 3; priority++)
        {
            _queueLocks[priority].EnterReadLock();
            try
            {
                if (_priorityQueues[priority].Count > 0)
                {
                    var firstTask = _priorityQueues[priority].Min!;
                    var lastTask = _priorityQueues[priority].Max!;

                    if (oldest == default || firstTask.EnqueueTimestamp < oldest)
                    {
                        oldest = firstTask.EnqueueTimestamp;
                    }

                    if (newest == default || lastTask.EnqueueTimestamp > newest)
                    {
                        newest = lastTask.EnqueueTimestamp;
                    }
                }
            }
            finally
            {
                _queueLocks[priority].ExitReadLock();
            }
        }

        return new HierarchicalQueueStatistics
        {
            Counts = counts,
            TotalEnqueued = Interlocked.Read(ref _totalEnqueued),
            TotalDequeued = Interlocked.Read(ref _totalDequeued),
            TotalStolen = Interlocked.Read(ref _totalStolen),
            HighPriorityWaitTime = highWait,
            NormalPriorityWaitTime = normalWait,
            LowPriorityWaitTime = lowWait,
            OldestTaskTimestamp = oldest,
            NewestTaskTimestamp = newest
        };
    }

    /// <inheritdoc/>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        for (var priority = 0; priority < 3; priority++)
        {
            _queueLocks[priority].EnterWriteLock();
            try
            {
                _priorityQueues[priority].Clear();
            }
            finally
            {
                _queueLocks[priority].ExitWriteLock();
            }
        }

        lock (_statsLock)
        {
            for (var i = 0; i < 3; i++)
            {
                _waitTimesByPriority[i].Clear();
            }
        }

        _logger.LogInformation("Cleared all tasks from queue '{QueueId}'", _queueId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing hierarchical task queue '{QueueId}'", _queueId);

        for (var i = 0; i < 3; i++)
        {
            _queueLocks[i].Dispose();
        }

        _disposed = true;
    }

    // --- Private Helper Methods ---

    private int PromoteTasksBetweenQueues(
        TaskPriority sourcePriority,
        TaskPriority targetPriority,
        HlcTimestamp currentTime,
        TimeSpan ageThreshold)
    {
        var sourceIndex = (int)sourcePriority;
        var targetIndex = (int)targetPriority;
        var promotedCount = 0;

        var tasksToPromote = new List<PrioritizedTask>();

        _queueLocks[sourceIndex].EnterReadLock();
        try
        {
            foreach (var task in _priorityQueues[sourceIndex])
            {
                if (!task.Flags.HasFlag(TaskFlags.PromotionEligible))
                {
                    continue;
                }

                var age = TimeSpan.FromTicks(
                    (currentTime.PhysicalTimeNanos - task.EnqueueTimestamp.PhysicalTimeNanos) / 100);

                if (age >= ageThreshold)
                {
                    tasksToPromote.Add(task);
                }
            }
        }
        finally
        {
            _queueLocks[sourceIndex].ExitReadLock();
        }

        if (tasksToPromote.Count == 0)
        {
            return 0;
        }

        // Remove from source and add to target
        _queueLocks[sourceIndex].EnterWriteLock();
        _queueLocks[targetIndex].EnterWriteLock();
        try
        {
            foreach (var task in tasksToPromote)
            {
                if (_priorityQueues[sourceIndex].Remove(task))
                {
                    var promotedTask = task with { Priority = targetPriority };
                    _priorityQueues[targetIndex].Add(promotedTask);
                    promotedCount++;
                }
            }
        }
        finally
        {
            _queueLocks[targetIndex].ExitWriteLock();
            _queueLocks[sourceIndex].ExitWriteLock();
        }

        return promotedCount;
    }

    private int DemoteTasksBetweenQueues(TaskPriority sourcePriority, TaskPriority targetPriority, int maxCount)
    {
        var sourceIndex = (int)sourcePriority;
        var targetIndex = (int)targetPriority;
        var demotedCount = 0;

        _queueLocks[sourceIndex].EnterWriteLock();
        _queueLocks[targetIndex].EnterWriteLock();
        try
        {
            // Demote up to maxCount tasks from tail (newest tasks)
            var tasksToDemote = _priorityQueues[sourceIndex]
                .Reverse()
                .Take(maxCount)
                .ToList();

            foreach (var task in tasksToDemote)
            {
                if (_priorityQueues[sourceIndex].Remove(task))
                {
                    var demotedTask = task with { Priority = targetPriority };
                    _priorityQueues[targetIndex].Add(demotedTask);
                    demotedCount++;
                }
            }
        }
        finally
        {
            _queueLocks[targetIndex].ExitWriteLock();
            _queueLocks[sourceIndex].ExitWriteLock();
        }

        return demotedCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TimeSpan CalculateAverageWaitTime(int priorityIndex)
    {
        if (_waitTimesByPriority[priorityIndex].Count == 0)
        {
            return TimeSpan.Zero;
        }

        var avgTicks = _waitTimesByPriority[priorityIndex]
            .Average(t => t.Ticks);

        return TimeSpan.FromTicks((long)avgTicks);
    }
}
