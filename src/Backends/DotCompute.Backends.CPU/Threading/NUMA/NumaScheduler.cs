// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// NUMA-aware task scheduling and work distribution.
/// </summary>
public sealed class NumaScheduler : IDisposable
{
    private readonly NumaTopology _topology;
    private readonly NumaAffinityManager _affinityManager;
    private readonly ConcurrentQueue<ScheduledTask>[] _nodeQueues;
    private readonly SemaphoreSlim[] _nodeSignals;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task[] _schedulerTasks;
    private readonly object _lock = new();
    private long _nextTaskId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the NumaScheduler class.
    /// </summary>
    /// <param name="topology">NUMA topology information.</param>
    /// <param name="affinityManager">Affinity manager for thread binding.</param>
    public NumaScheduler(NumaTopology topology, NumaAffinityManager? affinityManager = null)
    {
        _topology = topology ?? throw new ArgumentNullException(nameof(topology));
        _affinityManager = affinityManager ?? new NumaAffinityManager(topology);

        _nodeQueues = new ConcurrentQueue<ScheduledTask>[topology.NodeCount];
        _nodeSignals = new SemaphoreSlim[topology.NodeCount];
        _schedulerTasks = new Task[topology.NodeCount];
        _cancellationTokenSource = new CancellationTokenSource();

        for (var i = 0; i < topology.NodeCount; i++)
        {
            _nodeQueues[i] = new ConcurrentQueue<ScheduledTask>();
            _nodeSignals[i] = new SemaphoreSlim(0);
        }

        StartSchedulerTasks();
    }

    /// <summary>
    /// Schedules a task to run on a specific NUMA node.
    /// </summary>
    /// <param name="action">Action to execute.</param>
    /// <param name="nodeId">Target NUMA node ID.</param>
    /// <param name="priority">Task priority.</param>
    /// <returns>Task representing the scheduled work.</returns>
    public Task ScheduleOnNodeAsync(Action action, int nodeId, TaskPriority priority = TaskPriority.Normal)
    {
        ThrowIfDisposed();

        if (action == null)
        {

            throw new ArgumentNullException(nameof(action));
        }


        if (nodeId < 0 || nodeId >= _topology.NodeCount)
        {

            throw new ArgumentOutOfRangeException(nameof(nodeId), $"Node ID must be between 0 and {_topology.NodeCount - 1}");
        }


        return ScheduleOnNodeAsync(() => { action(); return Task.CompletedTask; }, nodeId, priority);
    }

    /// <summary>
    /// Schedules a task to run on a specific NUMA node.
    /// </summary>
    /// <typeparam name="T">Return type.</typeparam>
    /// <param name="function">Function to execute.</param>
    /// <param name="nodeId">Target NUMA node ID.</param>
    /// <param name="priority">Task priority.</param>
    /// <returns>Task representing the scheduled work.</returns>
    public Task<T> ScheduleOnNodeAsync<T>(Func<T> function, int nodeId, TaskPriority priority = TaskPriority.Normal)
    {
        ThrowIfDisposed();

        if (function == null)
        {

            throw new ArgumentNullException(nameof(function));
        }


        if (nodeId < 0 || nodeId >= _topology.NodeCount)
        {

            throw new ArgumentOutOfRangeException(nameof(nodeId), $"Node ID must be between 0 and {_topology.NodeCount - 1}");
        }


        return ScheduleOnNodeAsync(() => Task.FromResult(function()), nodeId, priority);
    }

    /// <summary>
    /// Schedules an async task to run on a specific NUMA node.
    /// </summary>
    /// <typeparam name="T">Return type.</typeparam>
    /// <param name="asyncFunction">Async function to execute.</param>
    /// <param name="nodeId">Target NUMA node ID.</param>
    /// <param name="priority">Task priority.</param>
    /// <returns>Task representing the scheduled work.</returns>
    public Task<T> ScheduleOnNodeAsync<T>(Func<Task<T>> asyncFunction, int nodeId, TaskPriority priority = TaskPriority.Normal)
    {
        ThrowIfDisposed();

        if (asyncFunction == null)
        {

            throw new ArgumentNullException(nameof(asyncFunction));
        }


        if (nodeId < 0 || nodeId >= _topology.NodeCount)
        {

            throw new ArgumentOutOfRangeException(nameof(nodeId), $"Node ID must be between 0 and {_topology.NodeCount - 1}");
        }


        var taskCompletionSource = new TaskCompletionSource<T>();
        var taskId = Interlocked.Increment(ref _nextTaskId);

        var scheduledTask = new ScheduledTask
        {
            Id = taskId,
            Priority = priority,
            NodeId = nodeId,
            ScheduledTime = DateTime.UtcNow,
            ExecuteAsync = async () =>
            {
                try
                {
                    var result = await asyncFunction().ConfigureAwait(false);
                    taskCompletionSource.SetResult(result);
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            }
        };

        _nodeQueues[nodeId].Enqueue(scheduledTask);
        _ = _nodeSignals[nodeId].Release();

        return taskCompletionSource.Task;
    }

    /// <summary>
    /// Schedules a task on the optimal node based on processor affinity.
    /// </summary>
    /// <param name="action">Action to execute.</param>
    /// <param name="processorIds">Preferred processor IDs.</param>
    /// <param name="priority">Task priority.</param>
    /// <returns>Task representing the scheduled work.</returns>
    public Task ScheduleOptimalAsync(Action action, IEnumerable<int>? processorIds = null, TaskPriority priority = TaskPriority.Normal)
    {
        ThrowIfDisposed();

        var optimalNode = processorIds != null
            ? _topology.GetOptimalNodeForProcessors(processorIds)
            : GetLeastLoadedNode();

        return ScheduleOnNodeAsync(action, optimalNode, priority);
    }

    /// <summary>
    /// Schedules a task on the optimal node based on processor affinity.
    /// </summary>
    /// <typeparam name="T">Return type.</typeparam>
    /// <param name="function">Function to execute.</param>
    /// <param name="processorIds">Preferred processor IDs.</param>
    /// <param name="priority">Task priority.</param>
    /// <returns>Task representing the scheduled work.</returns>
    public Task<T> ScheduleOptimalAsync<T>(Func<T> function, IEnumerable<int>? processorIds = null, TaskPriority priority = TaskPriority.Normal)
    {
        ThrowIfDisposed();

        var optimalNode = processorIds != null
            ? _topology.GetOptimalNodeForProcessors(processorIds)
            : GetLeastLoadedNode();

        return ScheduleOnNodeAsync(function, optimalNode, priority);
    }

    /// <summary>
    /// Schedules tasks in parallel across multiple NUMA nodes.
    /// </summary>
    /// <param name="actions">Actions to execute in parallel.</param>
    /// <param name="strategy">Load balancing strategy.</param>
    /// <returns>Task representing all parallel work.</returns>
    public Task ScheduleParallelAsync(IEnumerable<Action> actions, LoadBalancingStrategy strategy = LoadBalancingStrategy.RoundRobin)
    {
        ThrowIfDisposed();

        var actionList = actions.ToList();
        if (actionList.Count == 0)
        {

            return Task.CompletedTask;
        }


        var tasks = new List<Task>(actionList.Count);

        for (var i = 0; i < actionList.Count; i++)
        {
            var nodeId = strategy switch
            {
                LoadBalancingStrategy.RoundRobin => i % _topology.NodeCount,
                LoadBalancingStrategy.LeastLoaded => GetLeastLoadedNode(),
                LoadBalancingStrategy.Random => Random.Shared.Next(_topology.NodeCount),
                LoadBalancingStrategy.LocalNode => GetCurrentThreadNode(),
                _ => i % _topology.NodeCount
            };

            tasks.Add(ScheduleOnNodeAsync(actionList[i], nodeId));
        }

        return Task.WhenAll(tasks);
    }

    /// <summary>
    /// Gets scheduling statistics for monitoring.
    /// </summary>
    /// <returns>Scheduling statistics.</returns>
    public SchedulingStatistics GetStatistics()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            var queueLengths = new int[_topology.NodeCount];
            var totalQueued = 0;

            for (var i = 0; i < _topology.NodeCount; i++)
            {
                queueLengths[i] = _nodeQueues[i].Count;
                totalQueued += queueLengths[i];
            }

            return new SchedulingStatistics
            {
                TotalQueuedTasks = totalQueued,
                QueueLengthsByNode = queueLengths,
                AverageQueueLength = totalQueued > 0 ? (double)totalQueued / _topology.NodeCount : 0.0,
                LoadImbalance = CalculateLoadImbalance(queueLengths),
                NextTaskId = _nextTaskId
            };
        }
    }

    /// <summary>
    /// Drains all pending tasks and waits for completion.
    /// </summary>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>True if all tasks completed within timeout.</returns>
    public async Task<bool> DrainAsync(TimeSpan timeout)
    {
        ThrowIfDisposed();

        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            var stats = GetStatistics();
            if (stats.TotalQueuedTasks == 0)
            {
                return true;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        return false;
    }

    private void StartSchedulerTasks()
    {
        for (var nodeId = 0; nodeId < _topology.NodeCount; nodeId++)
        {
            var capturedNodeId = nodeId;
            _schedulerTasks[nodeId] = Task.Run(async () => await RunSchedulerForNodeAsync(capturedNodeId).ConfigureAwait(false));
        }
    }

    private async Task RunSchedulerForNodeAsync(int nodeId)
    {
        var token = _cancellationTokenSource.Token;

        // Set thread affinity to this node
        _ = _affinityManager.RunCurrentThreadOnNode(nodeId);

        try
        {
            while (!token.IsCancellationRequested)
            {
                await _nodeSignals[nodeId].WaitAsync(token).ConfigureAwait(false);

                while (_nodeQueues[nodeId].TryDequeue(out var scheduledTask))
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }


                    try
                    {
                        scheduledTask.StartTime = DateTime.UtcNow;
                        await scheduledTask.ExecuteAsync().ConfigureAwait(false);
                        scheduledTask.CompletedTime = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Task {scheduledTask.Id} failed on node {nodeId}: {ex.Message}");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Scheduler for node {nodeId} encountered error: {ex.Message}");
        }
    }

    private int GetLeastLoadedNode()
    {
        var minQueueLength = int.MaxValue;
        var leastLoadedNode = 0;

        for (var i = 0; i < _topology.NodeCount; i++)
        {
            var queueLength = _nodeQueues[i].Count;
            if (queueLength < minQueueLength)
            {
                minQueueLength = queueLength;
                leastLoadedNode = i;
            }
        }

        return leastLoadedNode;
    }

    private int GetCurrentThreadNode()
        // In a full implementation, this would check current thread affinity
        => Environment.CurrentManagedThreadId % _topology.NodeCount;

    private static double CalculateLoadImbalance(int[] queueLengths)
    {
        if (queueLengths.Length == 0)
        {
            return 0.0;
        }


        var total = queueLengths.Sum();
        if (total == 0)
        {
            return 0.0;
        }


        var average = (double)total / queueLengths.Length;
        var variance = queueLengths.Select(length => Math.Pow(length - average, 2)).Average();

        return Math.Sqrt(variance) / average;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NumaScheduler));
        }
    }

    /// <summary>
    /// Disposes of the scheduler and stops all worker tasks.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cancellationTokenSource.Cancel();

            // Wait for all scheduler tasks to complete
            try
            {
                _ = Task.WaitAll(_schedulerTasks, TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Ignore cancellation exceptions
            }

            foreach (var signal in _nodeSignals)
            {
                signal.Dispose();
            }

            _cancellationTokenSource.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a scheduled task with NUMA awareness.
/// </summary>
internal sealed class ScheduledTask
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public required long Id { get; init; }
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    /// <value>The priority.</value>
    public required TaskPriority Priority { get; init; }
    /// <summary>
    /// Gets or sets the node identifier.
    /// </summary>
    /// <value>The node id.</value>
    public required int NodeId { get; init; }
    /// <summary>
    /// Gets or sets the scheduled time.
    /// </summary>
    /// <value>The scheduled time.</value>
    public required DateTime ScheduledTime { get; init; }
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTime StartTime { get; set; }
    /// <summary>
    /// Gets or sets the completed time.
    /// </summary>
    /// <value>The completed time.</value>
    public DateTime CompletedTime { get; set; }
    /// <summary>
    /// Gets or sets the execute async.
    /// </summary>
    /// <value>The execute async.</value>
    public required Func<Task> ExecuteAsync { get; init; }
    /// <summary>
    /// Gets or sets the wait time.
    /// </summary>
    /// <value>The wait time.</value>

    public TimeSpan WaitTime => StartTime - ScheduledTime;
    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    /// <value>The execution time.</value>
    public TimeSpan ExecutionTime => CompletedTime - StartTime;
    /// <summary>
    /// Gets or sets a value indicating whether completed.
    /// </summary>
    /// <value>The is completed.</value>
    public bool IsCompleted => CompletedTime != default;
}

/// <summary>
/// Task priority levels.
/// </summary>
public enum TaskPriority
{
    /// <summary>Low priority.</summary>
    Low = 0,

    /// <summary>Normal priority.</summary>
    Normal = 1,

    /// <summary>High priority.</summary>
    High = 2,

    /// <summary>Critical priority.</summary>
    Critical = 3
}

/// <summary>
/// Load balancing strategies for parallel execution.
/// </summary>
public enum LoadBalancingStrategy
{
    /// <summary>Round-robin distribution across nodes.</summary>
    RoundRobin,

    /// <summary>Assign to least loaded node.</summary>
    LeastLoaded,

    /// <summary>Random node selection.</summary>
    Random,

    /// <summary>Prefer local node for current thread.</summary>
    LocalNode
}

/// <summary>
/// Scheduling statistics for monitoring.
/// </summary>
public sealed class SchedulingStatistics
{
    /// <summary>Total queued tasks across all nodes.</summary>
    public required int TotalQueuedTasks { get; init; }

    /// <summary>Queue lengths per NUMA node.</summary>
    public required int[] QueueLengthsByNode { get; init; }

    /// <summary>Average queue length across nodes.</summary>
    public required double AverageQueueLength { get; init; }

    /// <summary>Load imbalance metric (0.0 = perfectly balanced).</summary>
    public required double LoadImbalance { get; init; }

    /// <summary>Next task ID to be assigned.</summary>
    public required long NextTaskId { get; init; }

    /// <summary>Gets the most loaded node.</summary>
    public int MostLoadedNode => QueueLengthsByNode.Length > 0
        ? Array.IndexOf(QueueLengthsByNode, QueueLengthsByNode.Max())
        : 0;

    /// <summary>Gets the least loaded node.</summary>
    public int LeastLoadedNode => QueueLengthsByNode.Length > 0
        ? Array.IndexOf(QueueLengthsByNode, QueueLengthsByNode.Min())
        : 0;
}