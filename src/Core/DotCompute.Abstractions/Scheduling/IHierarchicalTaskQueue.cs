// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions.Temporal;

namespace DotCompute.Abstractions.Scheduling;

/// <summary>
/// Hierarchical task queue with priority-based scheduling and HLC temporal ordering.
/// Extends Phase 3 work-stealing queues with multi-level priority and causal scheduling.
/// </summary>
/// <remarks>
/// <para><b>Architecture:</b></para>
/// <list type="bullet">
/// <item><b>Three Priority Levels:</b> High (0), Normal (1), Low (2)</item>
/// <item><b>HLC-Based Scheduling:</b> Tasks with earlier HLC timestamps execute first within priority level</item>
/// <item><b>Work Stealing:</b> Idle workers can steal from lower-priority queues</item>
/// <item><b>Lock-Free Operations:</b> Chase-Lev deque algorithm per priority level</item>
/// </list>
///
/// <para><b>Performance Targets:</b></para>
/// <list type="bullet">
/// <item>Enqueue: &lt;100ns (lock-free CAS)</item>
/// <item>Dequeue: &lt;150ns (priority scan + HLC comparison)</item>
/// <item>Steal: &lt;200ns (cross-priority steal)</item>
/// <item>Rebalance: &lt;1μs (periodic priority adjustment)</item>
/// </list>
///
/// <para><b>Scheduling Policy:</b></para>
/// <para>
/// Priority-first scheduling with HLC tiebreaking:
/// </para>
/// <list type="number">
/// <item>Check High priority queue (HLC-ordered)</item>
/// <item>Check Normal priority queue (HLC-ordered)</item>
/// <item>Check Low priority queue (HLC-ordered)</item>
/// <item>Steal from other workers (highest priority first)</item>
/// </list>
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Queue is the appropriate name for this data structure")]
public interface IHierarchicalTaskQueue : IDisposable
{
    /// <summary>
    /// Gets the unique identifier for this task queue.
    /// </summary>
    public string QueueId { get; }

    /// <summary>
    /// Gets the total number of tasks across all priority levels.
    /// </summary>
    public int TotalTaskCount { get; }

    /// <summary>
    /// Gets the number of tasks at each priority level.
    /// </summary>
    public TaskPriorityCounts PriorityCounts { get; }

    /// <summary>
    /// Enqueues a task with specified priority and HLC timestamp.
    /// </summary>
    /// <param name="task">Task descriptor to enqueue.</param>
    /// <param name="priority">Priority level (High, Normal, Low).</param>
    /// <param name="timestamp">HLC timestamp for temporal ordering.</param>
    /// <returns>True if task was enqueued successfully; false if queue is full.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="task"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if queue has been disposed.</exception>
    public bool TryEnqueue(PrioritizedTask task, TaskPriority priority, HlcTimestamp timestamp);

    /// <summary>
    /// Dequeues the highest-priority task with earliest HLC timestamp.
    /// </summary>
    /// <param name="task">Dequeued task descriptor.</param>
    /// <returns>True if a task was dequeued; false if queue is empty.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if queue has been disposed.</exception>
    public bool TryDequeue(out PrioritizedTask task);

    /// <summary>
    /// Attempts to steal a task from this queue (used by work-stealing algorithm).
    /// </summary>
    /// <param name="task">Stolen task descriptor.</param>
    /// <param name="preferredPriority">Preferred priority level to steal from.</param>
    /// <returns>True if a task was stolen; false if no stealable tasks.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if queue has been disposed.</exception>
    public bool TrySteal(out PrioritizedTask task, TaskPriority preferredPriority = TaskPriority.Normal);

    /// <summary>
    /// Peeks at the next task without removing it from the queue.
    /// </summary>
    /// <param name="task">Task at the head of the queue.</param>
    /// <returns>True if a task is available; false if queue is empty.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if queue has been disposed.</exception>
    public bool TryPeek(out PrioritizedTask task);

    /// <summary>
    /// Promotes low-priority tasks that have been waiting too long.
    /// </summary>
    /// <param name="currentTime">Current HLC timestamp for age calculation.</param>
    /// <param name="ageThreshold">Maximum age before promotion (default: 1 second).</param>
    /// <returns>Number of tasks promoted.</returns>
    /// <remarks>
    /// <para>
    /// Prevents starvation by promoting tasks that have waited longer than threshold.
    /// Low priority → Normal after 1 second, Normal → High after 2 seconds.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown if queue has been disposed.</exception>
    public int PromoteAgedTasks(HlcTimestamp currentTime, TimeSpan ageThreshold = default);

    /// <summary>
    /// Rebalances tasks across priority levels based on system load.
    /// </summary>
    /// <param name="loadFactor">Current system load (0.0 = idle, 1.0 = saturated).</param>
    /// <returns>Rebalancing statistics.</returns>
    /// <remarks>
    /// <para>
    /// Adaptive priority adjustment based on system pressure:
    /// </para>
    /// <list type="bullet">
    /// <item>High load (&gt;0.8): Demote normal → low to reduce contention</item>
    /// <item>Low load (&lt;0.2): Promote low → normal to increase throughput</item>
    /// </list>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown if queue has been disposed.</exception>
    public RebalanceResult Rebalance(double loadFactor);

    /// <summary>
    /// Gets the current queue statistics.
    /// </summary>
    /// <returns>Statistics including counts, wait times, and HLC metrics.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if queue has been disposed.</exception>
    public HierarchicalQueueStatistics GetStatistics();

    /// <summary>
    /// Clears all tasks from the queue.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if queue has been disposed.</exception>
    public void Clear();
}

/// <summary>
/// Task priority levels for hierarchical scheduling.
/// </summary>
public enum TaskPriority
{
    /// <summary>
    /// High priority - executed first, used for latency-critical operations.
    /// </summary>
    High = 0,

    /// <summary>
    /// Normal priority - default priority for most tasks.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Low priority - background tasks, executed when higher priorities are empty.
    /// </summary>
    Low = 2
}

/// <summary>
/// Prioritized task descriptor with HLC timestamp for temporal ordering.
/// </summary>
public readonly struct PrioritizedTask : IEquatable<PrioritizedTask>, IComparable<PrioritizedTask>
{
    /// <summary>
    /// Unique task identifier.
    /// </summary>
    public required Guid TaskId { get; init; }

    /// <summary>
    /// Task priority level.
    /// </summary>
    public required TaskPriority Priority { get; init; }

    /// <summary>
    /// HLC timestamp when task was enqueued.
    /// </summary>
    public required HlcTimestamp EnqueueTimestamp { get; init; }

    /// <summary>
    /// Task data pointer (GPU memory address or CPU buffer).
    /// </summary>
    public required IntPtr DataPointer { get; init; }

    /// <summary>
    /// Size of task data in bytes.
    /// </summary>
    public required int DataSize { get; init; }

    /// <summary>
    /// Optional kernel identifier for GPU tasks.
    /// </summary>
    public int KernelId { get; init; }

    /// <summary>
    /// Task flags (see TaskFlags enum).
    /// </summary>
    public TaskFlags Flags { get; init; }

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is PrioritizedTask other && Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(PrioritizedTask other)
    {
        // Must be consistent with CompareTo: same priority and timestamp means equal
        return Priority == other.Priority &&
               EnqueueTimestamp.Equals(other.EnqueueTimestamp) &&
               TaskId.Equals(other.TaskId);
    }

    /// <inheritdoc/>
    public readonly override int GetHashCode()
    {
        return HashCode.Combine(Priority, EnqueueTimestamp, TaskId);
    }

    /// <summary>
    /// Compares tasks by priority then HLC timestamp (for sorting).
    /// </summary>
    public readonly int CompareTo(PrioritizedTask other)
    {
        // First compare by priority (lower value = higher priority)
        int priorityComparison = Priority.CompareTo(other.Priority);
        if (priorityComparison != 0)
        {
            return priorityComparison;
        }

        // Within same priority, compare by HLC timestamp (earlier timestamp first)
        return EnqueueTimestamp.CompareTo(other.EnqueueTimestamp);
    }

    /// <summary>
    /// Determines whether two tasks are equal.
    /// </summary>
    public static bool operator ==(PrioritizedTask left, PrioritizedTask right) => left.Equals(right);

    /// <summary>
    /// Determines whether two tasks are not equal.
    /// </summary>
    public static bool operator !=(PrioritizedTask left, PrioritizedTask right) => !left.Equals(right);

    /// <summary>
    /// Determines whether one task has higher priority than another.
    /// </summary>
    public static bool operator <(PrioritizedTask left, PrioritizedTask right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether one task has equal or higher priority than another.
    /// </summary>
    public static bool operator <=(PrioritizedTask left, PrioritizedTask right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether one task has lower priority than another.
    /// </summary>
    public static bool operator >(PrioritizedTask left, PrioritizedTask right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether one task has equal or lower priority than another.
    /// </summary>
    public static bool operator >=(PrioritizedTask left, PrioritizedTask right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Task flags for controlling behavior.
/// </summary>
[Flags]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Flags is the correct name for an enum with [Flags] attribute")]
public enum TaskFlags
{
    /// <summary>
    /// No special flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// Task can be stolen by work-stealing algorithm.
    /// </summary>
    Stealable = 1 << 0,

    /// <summary>
    /// Task should be executed ASAP (bypasses HLC ordering within priority).
    /// </summary>
    Urgent = 1 << 1,

    /// <summary>
    /// Task is eligible for priority promotion if aged.
    /// </summary>
    PromotionEligible = 1 << 2,

    /// <summary>
    /// Task requires GPU execution.
    /// </summary>
    RequiresGpu = 1 << 3,

    /// <summary>
    /// Task has dependencies that must complete first.
    /// </summary>
    HasDependencies = 1 << 4
}

/// <summary>
/// Task counts by priority level.
/// </summary>
public readonly struct TaskPriorityCounts : IEquatable<TaskPriorityCounts>
{
    /// <summary>
    /// Number of high-priority tasks.
    /// </summary>
    public int HighPriority { get; init; }

    /// <summary>
    /// Number of normal-priority tasks.
    /// </summary>
    public int NormalPriority { get; init; }

    /// <summary>
    /// Number of low-priority tasks.
    /// </summary>
    public int LowPriority { get; init; }

    /// <summary>
    /// Total task count across all priorities.
    /// </summary>
    public readonly int Total => HighPriority + NormalPriority + LowPriority;

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is TaskPriorityCounts other && Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(TaskPriorityCounts other)
    {
        return HighPriority == other.HighPriority &&
            NormalPriority == other.NormalPriority &&
            LowPriority == other.LowPriority;
    }

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(HighPriority, NormalPriority, LowPriority);

    /// <summary>
    /// Determines whether two counts are equal.
    /// </summary>
    public static bool operator ==(TaskPriorityCounts left, TaskPriorityCounts right) => left.Equals(right);

    /// <summary>
    /// Determines whether two counts are not equal.
    /// </summary>
    public static bool operator !=(TaskPriorityCounts left, TaskPriorityCounts right) => !left.Equals(right);
}

/// <summary>
/// Result of queue rebalancing operation.
/// </summary>
public readonly struct RebalanceResult : IEquatable<RebalanceResult>
{
    /// <summary>
    /// Number of tasks promoted to higher priority.
    /// </summary>
    public int PromotedCount { get; init; }

    /// <summary>
    /// Number of tasks demoted to lower priority.
    /// </summary>
    public int DemotedCount { get; init; }

    /// <summary>
    /// Time taken to perform rebalancing.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Priority counts after rebalancing.
    /// </summary>
    public TaskPriorityCounts NewCounts { get; init; }

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is RebalanceResult other && Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(RebalanceResult other)
    {
        return PromotedCount == other.PromotedCount &&
            DemotedCount == other.DemotedCount &&
            Duration == other.Duration &&
            NewCounts.Equals(other.NewCounts);
    }

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(PromotedCount, DemotedCount, Duration, NewCounts);

    /// <summary>
    /// Determines whether two results are equal.
    /// </summary>
    public static bool operator ==(RebalanceResult left, RebalanceResult right) => left.Equals(right);

    /// <summary>
    /// Determines whether two results are not equal.
    /// </summary>
    public static bool operator !=(RebalanceResult left, RebalanceResult right) => !left.Equals(right);
}

/// <summary>
/// Statistics for hierarchical task queue.
/// </summary>
public readonly struct HierarchicalQueueStatistics : IEquatable<HierarchicalQueueStatistics>
{
    /// <summary>
    /// Current task counts by priority.
    /// </summary>
    public required TaskPriorityCounts Counts { get; init; }

    /// <summary>
    /// Total number of tasks enqueued (lifetime).
    /// </summary>
    public long TotalEnqueued { get; init; }

    /// <summary>
    /// Total number of tasks dequeued (lifetime).
    /// </summary>
    public long TotalDequeued { get; init; }

    /// <summary>
    /// Total number of tasks stolen (lifetime).
    /// </summary>
    public long TotalStolen { get; init; }

    /// <summary>
    /// Average wait time for high-priority tasks.
    /// </summary>
    public TimeSpan HighPriorityWaitTime { get; init; }

    /// <summary>
    /// Average wait time for normal-priority tasks.
    /// </summary>
    public TimeSpan NormalPriorityWaitTime { get; init; }

    /// <summary>
    /// Average wait time for low-priority tasks.
    /// </summary>
    public TimeSpan LowPriorityWaitTime { get; init; }

    /// <summary>
    /// Oldest task HLC timestamp in the queue.
    /// </summary>
    public HlcTimestamp OldestTaskTimestamp { get; init; }

    /// <summary>
    /// Newest task HLC timestamp in the queue.
    /// </summary>
    public HlcTimestamp NewestTaskTimestamp { get; init; }

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is HierarchicalQueueStatistics other && Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(HierarchicalQueueStatistics other)
    {
        return Counts.Equals(other.Counts) &&
            TotalEnqueued == other.TotalEnqueued &&
            TotalDequeued == other.TotalDequeued &&
            TotalStolen == other.TotalStolen &&
            HighPriorityWaitTime == other.HighPriorityWaitTime &&
            NormalPriorityWaitTime == other.NormalPriorityWaitTime &&
            LowPriorityWaitTime == other.LowPriorityWaitTime &&
            OldestTaskTimestamp.Equals(other.OldestTaskTimestamp) &&
            NewestTaskTimestamp.Equals(other.NewestTaskTimestamp);
    }

    /// <inheritdoc/>
    public readonly override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Counts);
        hash.Add(TotalEnqueued);
        hash.Add(TotalDequeued);
        hash.Add(TotalStolen);
        hash.Add(HighPriorityWaitTime);
        hash.Add(NormalPriorityWaitTime);
        hash.Add(LowPriorityWaitTime);
        hash.Add(OldestTaskTimestamp);
        hash.Add(NewestTaskTimestamp);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Determines whether two statistics are equal.
    /// </summary>
    public static bool operator ==(HierarchicalQueueStatistics left, HierarchicalQueueStatistics right) => left.Equals(right);

    /// <summary>
    /// Determines whether two statistics are not equal.
    /// </summary>
    public static bool operator !=(HierarchicalQueueStatistics left, HierarchicalQueueStatistics right) => !left.Equals(right);
}
