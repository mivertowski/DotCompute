// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution;

/// <summary>
/// Coordinates work-stealing execution across multiple devices for dynamic load balancing.
/// </summary>
public sealed class WorkStealingCoordinator<T> : IAsyncDisposable where T : unmanaged
{
    private readonly IAccelerator[] _devices;
    private readonly WorkStealingWorkload<T> _workload;
    private readonly MultiGpuMemoryManager _memoryManager;
    private readonly ILogger _logger;
    private readonly DeviceWorkQueue<T>[] _deviceQueues;
    private readonly ConcurrentDictionary<int, WorkItemStatus<T>> _workItemStatuses;
    private readonly StealingCoordinator _stealingCoordinator;
    private readonly LoadBalancer _loadBalancer;
    private readonly Random _random;
    private volatile bool _executionActive;
    private bool _disposed;

    public WorkStealingCoordinator(
        IAccelerator[] devices,
        WorkStealingWorkload<T> workload,
        MultiGpuMemoryManager memoryManager,
        ILogger logger)
    {
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _workload = workload ?? throw new ArgumentNullException(nameof(workload));
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _deviceQueues = _devices.Select((device, index) => 
            new DeviceWorkQueue<T>(device, index, logger)).ToArray();
        
        _workItemStatuses = new ConcurrentDictionary<int, WorkItemStatus<T>>();
        _stealingCoordinator = new StealingCoordinator(_devices.Length, logger);
        _loadBalancer = new LoadBalancer(_devices, logger);
        _random = new Random();

        InitializeWorkItems();
    }

    /// <summary>
    /// Executes the workload using work-stealing strategy.
    /// </summary>
    public async ValueTask<DeviceExecutionResult[]> ExecuteAsync(
        KernelManager kernelManager,
        WorkStealingOptions options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting work-stealing execution with {WorkItemCount} work items across {DeviceCount} devices",
            _workload.WorkItems.Count, _devices.Length);

        _executionActive = true;
        var executionTasks = new List<Task<DeviceExecutionResult>>();

        try
        {
            // Start execution tasks for each device
            for (int deviceIndex = 0; deviceIndex < _devices.Length; deviceIndex++)
            {
                var task = ExecuteDeviceWorkAsync(
                    deviceIndex, kernelManager, options, cancellationToken);
                executionTasks.Add(task);
            }

            // Start work stealing coordination
            var stealingTask = CoordinateWorkStealingAsync(options, cancellationToken);

            // Wait for all tasks to complete
            var results = await Task.WhenAll(executionTasks).ConfigureAwait(false);
            await stealingTask.ConfigureAwait(false);

            _logger.LogInformation("Work-stealing execution completed. Processed {CompletedItems} work items",
                _workItemStatuses.Count(kvp => kvp.Value.Status == WorkStatus.Completed));

            return results;
        }
        finally
        {
            _executionActive = false;
        }
    }

    /// <summary>
    /// Gets current work-stealing statistics.
    /// </summary>
    public WorkStealingStatistics GetStatistics()
    {
        var stats = new WorkStealingStatistics
        {
            TotalWorkItems = _workload.WorkItems.Count,
            CompletedWorkItems = _workItemStatuses.Count(kvp => kvp.Value.Status == WorkStatus.Completed),
            InProgressWorkItems = _workItemStatuses.Count(kvp => kvp.Value.Status == WorkStatus.InProgress),
            PendingWorkItems = _workItemStatuses.Count(kvp => kvp.Value.Status == WorkStatus.Pending),
            DeviceStatistics = _deviceQueues.Select(q => q.GetStatistics()).ToArray(),
            StealingStatistics = _stealingCoordinator.GetStatistics()
        };

        return stats;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _executionActive = false;

        // Dispose device queues
        var disposeTasks = _deviceQueues.Select(queue => queue.DisposeAsync()).ToArray();
        foreach (var task in disposeTasks)
        {
            await task.ConfigureAwait(false);
        }

        _workItemStatuses.Clear();
        _disposed = true;
    }

    #region Private Methods

    private void InitializeWorkItems()
    {
        // Initialize work item statuses
        foreach (var workItem in _workload.WorkItems)
        {
            _workItemStatuses[workItem.Id] = new WorkItemStatus<T>
            {
                WorkItem = workItem,
                Status = WorkStatus.Pending,
                AssignedDeviceIndex = -1,
                StartTime = null,
                EndTime = null
            };
        }

        // Distribute work items to device queues using load balancer
        _loadBalancer.DistributeWorkItems(_workload.WorkItems, _deviceQueues);

        _logger.LogDebug("Initialized {WorkItemCount} work items across {DeviceCount} devices",
            _workload.WorkItems.Count, _devices.Length);
    }

    private async Task<DeviceExecutionResult> ExecuteDeviceWorkAsync(
        int deviceIndex,
        KernelManager kernelManager,
        WorkStealingOptions options,
        CancellationToken cancellationToken)
    {
        var device = _devices[deviceIndex];
        var deviceQueue = _deviceQueues[deviceIndex];
        var result = new DeviceExecutionResult
        {
            DeviceId = device.Info.Id,
            Success = true
        };

        var totalExecutionTime = 0.0;
        var processedItems = 0;

        try
        {
            while (_executionActive && !cancellationToken.IsCancellationRequested)
            {
                // Try to get work from own queue first
                var workItem = await deviceQueue.DequeueWorkAsync(cancellationToken);
                
                if (workItem == null)
                {
                    // No work in own queue, try to steal from other devices
                    workItem = await TryStealWorkAsync(deviceIndex, options, cancellationToken);
                }

                if (workItem == null)
                {
                    // No work available anywhere, wait a bit and try again
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                // Execute the work item
                var executionTime = await ExecuteWorkItemAsync(
                    workItem, device, kernelManager, cancellationToken);
                
                totalExecutionTime += executionTime;
                processedItems++;

                // Mark work item as completed
                if (_workItemStatuses.TryGetValue(workItem.Id, out var status))
                {
                    status.Status = WorkStatus.Completed;
                    status.EndTime = DateTimeOffset.UtcNow;
                }
            }

            result.ExecutionTimeMs = totalExecutionTime;
            result.ElementsProcessed = processedItems;

            _logger.LogDebug("Device {DeviceId} processed {ItemCount} work items in {ExecutionTimeMs:F2}ms",
                device.Info.Id, processedItems, totalExecutionTime);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Device {DeviceId} work execution cancelled", device.Info.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing work on device {DeviceId}", device.Info.Id);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<WorkItem<T>?> TryStealWorkAsync(
        int deviceIndex,
        WorkStealingOptions options,
        CancellationToken cancellationToken)
    {
        var victimIndex = SelectVictimDevice(deviceIndex, options.StealingStrategy);
        if (victimIndex == -1)
        {
            return null;
        }

        var victimQueue = _deviceQueues[victimIndex];
        var stolenWork = await victimQueue.StealWorkAsync(cancellationToken);

        if (stolenWork != null)
        {
            _stealingCoordinator.RecordSuccessfulSteal(deviceIndex, victimIndex);
            _logger.LogTrace("Device {ThiefDevice} stole work item {WorkItemId} from device {VictimDevice}",
                _devices[deviceIndex].Info.Id, stolenWork.Id, _devices[victimIndex].Info.Id);
        }
        else
        {
            _stealingCoordinator.RecordFailedSteal(deviceIndex, victimIndex);
        }

        return stolenWork;
    }

    private int SelectVictimDevice(int thiefIndex, StealingStrategy strategy)
    {
        return strategy switch
        {
            StealingStrategy.RandomVictim => SelectRandomVictim(thiefIndex),
            StealingStrategy.RichestVictim => SelectRichestVictim(thiefIndex),
            StealingStrategy.NearestVictim => SelectNearestVictim(thiefIndex),
            StealingStrategy.Hierarchical => SelectHierarchicalVictim(thiefIndex),
            _ => SelectRandomVictim(thiefIndex)
        };
    }

    private int SelectRandomVictim(int thiefIndex)
    {
        var availableVictims = Enumerable.Range(0, _devices.Length)
            .Where(i => i != thiefIndex && _deviceQueues[i].HasWork)
            .ToArray();

        return availableVictims.Length > 0 ? availableVictims[_random.Next(availableVictims.Length)] : -1;
    }

    private int SelectRichestVictim(int thiefIndex)
    {
        var richestVictim = -1;
        var maxWorkCount = 0;

        for (int i = 0; i < _devices.Length; i++)
        {
            if (i != thiefIndex)
            {
                var workCount = _deviceQueues[i].WorkCount;
                if (workCount > maxWorkCount)
                {
                    maxWorkCount = workCount;
                    richestVictim = i;
                }
            }
        }

        return maxWorkCount > 1 ? richestVictim : -1; // Only steal if victim has more than 1 work item
    }

    private int SelectNearestVictim(int thiefIndex)
    {
        // For simplicity, use adjacent device indices as "nearest"
        // Real implementation would consider NUMA topology
        var candidates = new[]
        {
            thiefIndex == 0 ? _devices.Length - 1 : thiefIndex - 1,
            thiefIndex == _devices.Length - 1 ? 0 : thiefIndex + 1
        };

        foreach (var candidate in candidates)
        {
            if (_deviceQueues[candidate].HasWork)
            {
                return candidate;
            }
        }

        return SelectRandomVictim(thiefIndex);
    }

    private int SelectHierarchicalVictim(int thiefIndex)
    {
        // Implement hierarchical stealing based on device hierarchy
        // For now, fallback to richest victim strategy
        return SelectRichestVictim(thiefIndex);
    }

    private async Task<double> ExecuteWorkItemAsync(
        WorkItem<T> workItem,
        IAccelerator device,
        KernelManager kernelManager,
        CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        // Update work item status
        if (_workItemStatuses.TryGetValue(workItem.Id, out var status))
        {
            status.Status = WorkStatus.InProgress;
            status.StartTime = startTime;
            status.AssignedDeviceIndex = Array.IndexOf(_devices, device);
        }

        try
        {
            // Create kernel arguments from work item buffers
            var kernelArgs = workItem.InputBuffers
                .Concat(workItem.OutputBuffers)
                .Select((buffer, index) => new KernelArgument
                {
                    Name = $"arg_{index}",
                    Value = buffer,
                    Type = typeof(AbstractionsMemory.IBuffer<T>),
                    IsDeviceMemory = true,
                    MemoryBuffer = buffer as AbstractionsMemory.IMemoryBuffer
                })
                .ToArray();

            // Get or compile kernel for this device
            var compiledKernel = await kernelManager.GetOrCompileOperationKernelAsync(
                "work_item_kernel", // This would be determined by the work item type
                new[] { typeof(T) },
                typeof(T),
                device,
                null,
                null,
                cancellationToken);

            // Execute kernel
            var executionResult = await kernelManager.ExecuteKernelAsync(
                compiledKernel,
                kernelArgs,
                device,
                null,
                cancellationToken);

            if (!executionResult.Success)
            {
                throw new InvalidOperationException($"Kernel execution failed: {executionResult.ErrorMessage}");
            }

            var endTime = DateTimeOffset.UtcNow;
            var executionTimeMs = (endTime - startTime).TotalMilliseconds;

            _logger.LogTrace("Work item {WorkItemId} executed on device {DeviceId} in {ExecutionTimeMs:F2}ms",
                workItem.Id, device.Info.Id, executionTimeMs);

            return executionTimeMs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute work item {WorkItemId} on device {DeviceId}",
                workItem.Id, device.Info.Id);
            
            if (status != null)
            {
                status.Status = WorkStatus.Failed;
                status.EndTime = DateTimeOffset.UtcNow;
            }
            
            throw;
        }
    }

    private async Task CoordinateWorkStealingAsync(WorkStealingOptions options, CancellationToken cancellationToken)
    {
        while (_executionActive && !cancellationToken.IsCancellationRequested)
        {
            // Monitor load balance and trigger rebalancing if needed
            var loadImbalance = _loadBalancer.CalculateLoadImbalance(_deviceQueues);
            
            if (loadImbalance > 0.3) // 30% imbalance threshold
            {
                _logger.LogDebug("Load imbalance detected: {ImbalancePercentage:F1}%. Triggering rebalancing.",
                    loadImbalance * 100);
                
                await _loadBalancer.RebalanceWorkAsync(_deviceQueues, cancellationToken);
            }

            await Task.Delay(100, cancellationToken); // Check every 100ms
        }
    }

    #endregion
}

/// <summary>
/// Work queue for a specific device in work-stealing execution.
/// </summary>
public sealed class DeviceWorkQueue<T> : IAsyncDisposable where T : unmanaged
{
    private readonly IAccelerator _device;
    private readonly int _deviceIndex;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<WorkItem<T>> _workQueue;
    private readonly SemaphoreSlim _workAvailable;
    private readonly object _statsLock = new();
    private DeviceQueueStatistics _statistics;
    private bool _disposed;

    public DeviceWorkQueue(IAccelerator device, int deviceIndex, ILogger logger)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _deviceIndex = deviceIndex;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _workQueue = new ConcurrentQueue<WorkItem<T>>();
        _workAvailable = new SemaphoreSlim(0);
        _statistics = new DeviceQueueStatistics { DeviceIndex = deviceIndex };
    }

    /// <summary>Gets whether the queue has work available.</summary>
    public bool HasWork => !_workQueue.IsEmpty;

    /// <summary>Gets the current work count in the queue.</summary>
    public int WorkCount => _workQueue.Count;

    /// <summary>
    /// Enqueues a work item to this device queue.
    /// </summary>
    public void EnqueueWork(WorkItem<T> workItem)
    {
        if (_disposed) return;

        _workQueue.Enqueue(workItem);
        _workAvailable.Release();

        lock (_statsLock)
        {
            _statistics.TotalEnqueued++;
        }

        _logger.LogTrace("Enqueued work item {WorkItemId} to device {DeviceId}",
            workItem.Id, _device.Info.Id);
    }

    /// <summary>
    /// Dequeues a work item from this device queue.
    /// </summary>
    public async ValueTask<WorkItem<T>?> DequeueWorkAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return null;

        try
        {
            await _workAvailable.WaitAsync(1000, cancellationToken); // Wait up to 1 second
            
            if (_workQueue.TryDequeue(out var workItem))
            {
                lock (_statsLock)
                {
                    _statistics.TotalDequeued++;
                }

                _logger.LogTrace("Dequeued work item {WorkItemId} from device {DeviceId}",
                    workItem.Id, _device.Info.Id);
                
                return workItem;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled or timeout
        }

        return null;
    }

    /// <summary>
    /// Attempts to steal work from this queue (called by other devices).
    /// </summary>
    public async ValueTask<WorkItem<T>?> StealWorkAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _workQueue.Count <= 1) // Don't steal if only one item left
        {
            return null;
        }

        if (_workQueue.TryDequeue(out var workItem))
        {
            // Consume one semaphore count since we took work
            try
            {
                await _workAvailable.WaitAsync(0, cancellationToken);
            }
            catch (TimeoutException)
            {
                // Semaphore count might be zero, that's okay
            }

            lock (_statsLock)
            {
                _statistics.TotalStolen++;
            }

            _logger.LogTrace("Work item {WorkItemId} stolen from device {DeviceId}",
                workItem.Id, _device.Info.Id);
            
            return workItem;
        }

        return null;
    }

    /// <summary>
    /// Gets statistics for this device queue.
    /// </summary>
    public DeviceQueueStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new DeviceQueueStatistics
            {
                DeviceIndex = _statistics.DeviceIndex,
                CurrentQueueSize = _workQueue.Count,
                TotalEnqueued = _statistics.TotalEnqueued,
                TotalDequeued = _statistics.TotalDequeued,
                TotalStolen = _statistics.TotalStolen
            };
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _workAvailable.Dispose();
        
        // Clear remaining work items
        while (_workQueue.TryDequeue(out _))
        {
            // Just drain the queue
        }

        _disposed = true;
        await ValueTask.CompletedTask;
    }
}

/// <summary>
/// Coordinates stealing statistics and decisions.
/// </summary>
public class StealingCoordinator
{
    private readonly int _deviceCount;
    private readonly ILogger _logger;
    private readonly int[,] _successfulSteals;
    private readonly int[,] _failedSteals;
    private readonly object _statsLock = new();

    public StealingCoordinator(int deviceCount, ILogger logger)
    {
        _deviceCount = deviceCount;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _successfulSteals = new int[deviceCount, deviceCount];
        _failedSteals = new int[deviceCount, deviceCount];
    }

    public void RecordSuccessfulSteal(int thiefIndex, int victimIndex)
    {
        lock (_statsLock)
        {
            _successfulSteals[thiefIndex, victimIndex]++;
        }
    }

    public void RecordFailedSteal(int thiefIndex, int victimIndex)
    {
        lock (_statsLock)
        {
            _failedSteals[thiefIndex, victimIndex]++;
        }
    }

    public StealingStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            var totalSuccessful = 0;
            var totalFailed = 0;

            for (int i = 0; i < _deviceCount; i++)
            {
                for (int j = 0; j < _deviceCount; j++)
                {
                    totalSuccessful += _successfulSteals[i, j];
                    totalFailed += _failedSteals[i, j];
                }
            }

            return new StealingStatistics
            {
                TotalStealAttempts = totalSuccessful + totalFailed,
                SuccessfulSteals = totalSuccessful,
                FailedSteals = totalFailed,
                StealSuccessRate = totalSuccessful + totalFailed > 0 
                    ? (double)totalSuccessful / (totalSuccessful + totalFailed) * 100 
                    : 0
            };
        }
    }
}

/// <summary>
/// Manages load balancing across devices.
/// </summary>
public class LoadBalancer
{
    private readonly IAccelerator[] _devices;
    private readonly ILogger _logger;

    public LoadBalancer(IAccelerator[] devices, ILogger logger)
    {
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void DistributeWorkItems<T>(List<WorkItem<T>> workItems, DeviceWorkQueue<T>[] deviceQueues) where T : unmanaged
    {
        // Simple round-robin distribution
        for (int i = 0; i < workItems.Count; i++)
        {
            var deviceIndex = i % deviceQueues.Length;
            deviceQueues[deviceIndex].EnqueueWork(workItems[i]);
        }

        _logger.LogDebug("Distributed {WorkItemCount} work items across {DeviceCount} devices",
            workItems.Count, deviceQueues.Length);
    }

    public double CalculateLoadImbalance<T>(DeviceWorkQueue<T>[] deviceQueues) where T : unmanaged
    {
        var workCounts = deviceQueues.Select(q => q.WorkCount).ToArray();
        var maxWork = workCounts.Max();
        var minWork = workCounts.Min();
        
        return maxWork > 0 ? (double)(maxWork - minWork) / maxWork : 0;
    }

    public async ValueTask RebalanceWorkAsync<T>(DeviceWorkQueue<T>[] deviceQueues, CancellationToken cancellationToken) where T : unmanaged
    {
        _logger.LogDebug("Rebalancing work across {DeviceCount} devices", deviceQueues.Length);
        
        // Simple rebalancing: move work from rich queues to poor queues
        var workCounts = deviceQueues.Select(q => q.WorkCount).ToArray();
        var avgWork = workCounts.Average();

        for (int i = 0; i < deviceQueues.Length; i++)
        {
            var currentWork = workCounts[i];
            if (currentWork > avgWork * 1.5) // Queue has 50% more work than average
            {
                var excessWork = (int)(currentWork - avgWork);
                for (int j = 0; j < excessWork / 2; j++) // Move half the excess
                {
                    var workItem = await deviceQueues[i].StealWorkAsync(cancellationToken);
                    if (workItem != null)
                    {
                        // Find a queue with less work
                        var targetIndex = Array.IndexOf(workCounts, workCounts.Min());
                        deviceQueues[targetIndex].EnqueueWork(workItem);
                    }
                }
            }
        }

        await ValueTask.CompletedTask;
    }
}

// Supporting data structures
public class WorkItemStatus<T> where T : unmanaged
{
    public required WorkItem<T> WorkItem { get; set; }
    public WorkStatus Status { get; set; }
    public int AssignedDeviceIndex { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
}

public enum WorkStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public class WorkStealingStatistics
{
    public int TotalWorkItems { get; set; }
    public int CompletedWorkItems { get; set; }
    public int InProgressWorkItems { get; set; }
    public int PendingWorkItems { get; set; }
    public DeviceQueueStatistics[] DeviceStatistics { get; set; } = Array.Empty<DeviceQueueStatistics>();
    public StealingStatistics StealingStatistics { get; set; } = new();
}

public class DeviceQueueStatistics
{
    public int DeviceIndex { get; set; }
    public int CurrentQueueSize { get; set; }
    public long TotalEnqueued { get; set; }
    public long TotalDequeued { get; set; }
    public long TotalStolen { get; set; }
}

public class StealingStatistics
{
    public long TotalStealAttempts { get; set; }
    public long SuccessfulSteals { get; set; }
    public long FailedSteals { get; set; }
    public double StealSuccessRate { get; set; }
}