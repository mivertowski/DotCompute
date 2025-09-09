// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using DotCompute.Core.Execution.Workload;
using DotCompute.Core.Execution.Types;
using DotCompute.Core.Execution.Configuration;
using DotCompute.Core.Execution.Metrics;
using Microsoft.Extensions.Logging;

using System;
namespace DotCompute.Core.Execution
{

    /// <summary>
    /// Coordinates work-stealing execution across multiple devices for dynamic load balancing.
    /// </summary>
    public sealed class WorkStealingCoordinator<T> : IAsyncDisposable where T : unmanaged
    {
        private readonly IAccelerator[] _devices;
        private readonly WorkStealingWorkload<T> _workload;
        private readonly IUnifiedMemoryManager _memoryManager;
        private readonly ILogger _logger;
        private readonly DeviceWorkQueue<T>[] _deviceQueues;
        private readonly ConcurrentDictionary<int, WorkItemStatus<T>> _workItemStatuses;
        private readonly StealingCoordinator _stealingCoordinator;
        private readonly LoadBalancer _loadBalancer;
        private readonly Random _random;
        private volatile bool _executionActive;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkStealingCoordinator{T}"/> class.
        /// </summary>
        /// <param name="devices">The devices.</param>
        /// <param name="workload">The workload.</param>
        /// <param name="memoryManager">The memory manager.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">
        /// devices
        /// or
        /// workload
        /// or
        /// memoryManager
        /// or
        /// logger
        /// </exception>
        public WorkStealingCoordinator(
            IAccelerator[] devices,
            WorkStealingWorkload<T> workload,
            IUnifiedMemoryManager memoryManager,
            ILogger logger)
        {
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
            _workload = workload ?? throw new ArgumentNullException(nameof(workload));
            _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _deviceQueues = [.. _devices.Select((device, index) =>

            new DeviceWorkQueue<T>(device, index, logger))];

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
            IKernelManager kernelManager,
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
                for (var deviceIndex = 0; deviceIndex < _devices.Length; deviceIndex++)
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
                DeviceStatistics = [.. _deviceQueues.Select(q => q.Statistics)],
                StealingStatistics = _stealingCoordinator.Statistics
            };

            return stats;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or
        /// resetting unmanaged resources asynchronously.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous dispose operation.
        /// </returns>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

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
            if (_workload?.WorkItems == null)
            {
                _logger.LogError("Cannot initialize work items: workload or work items is null");
                return;
            }

            // Initialize work item statuses
            foreach (var workItem in _workload.WorkItems)
            {
                if (workItem == null)
                {
                    _logger.LogWarning("Skipping null work item during initialization");
                    continue;
                }

                _workItemStatuses[workItem.Id] = new WorkItemStatus<T>
                {
                    WorkItem = workItem,
                    Status = WorkStatus.Pending,
                    AssignedDeviceIndex = -1,
                    StartTime = null,
                    EndTime = null
                };
            }

            try
            {
                // Distribute work items to device queues using load balancer
                _loadBalancer.DistributeWorkItems(_workload.WorkItems, _deviceQueues);

                _logger.LogDebug("Initialized {WorkItemCount} work items across {DeviceCount} devices",
                    _workload.WorkItems.Count, _devices.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to distribute work items during initialization");
                throw new InvalidOperationException("Failed to initialize work items for work stealing execution", ex);
            }
        }

        private async Task<DeviceExecutionResult> ExecuteDeviceWorkAsync(
            int deviceIndex,
            IKernelManager kernelManager,
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

                    // No work in own queue, try to steal from other devices
                    workItem ??= await TryStealWorkAsync(deviceIndex, options, cancellationToken);

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

            for (var i = 0; i < _devices.Length; i++)
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
            // Real implementation would consider NUMA topology - TODO
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
            // Implement hierarchical stealing based on device hierarchy
            // For now, fallback to richest victim strategy - TODO


            => SelectRichestVictim(thiefIndex);

        private async Task<double> ExecuteWorkItemAsync(
        WorkItem<T> workItem,
        IAccelerator device,
        IKernelManager kernelManager,
        CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(workItem);

            ArgumentNullException.ThrowIfNull(device);

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
                // Validate work item has valid buffers before execution
                if (workItem.InputBuffers == null || workItem.OutputBuffers == null)
                {
                    throw new InvalidOperationException($"Work item {workItem.Id} has null buffers");
                }

                // Check for cancellation before expensive operations
                cancellationToken.ThrowIfCancellationRequested();

                // Create kernel arguments from work item buffers with validation
                var validInputBuffers = workItem.InputBuffers.Where(b => b != null).ToArray();
                var validOutputBuffers = workItem.OutputBuffers.Where(b => b != null).ToArray();

                if (validInputBuffers.Length == 0 && validOutputBuffers.Length == 0)
                {
                    _logger.LogWarning("Work item {WorkItemId} has no valid buffers, simulating execution", workItem.Id);
                    // Simulate execution time for testing purposes
                    await Task.Delay((int)workItem.EstimatedProcessingTimeMs, cancellationToken);
                    return workItem.EstimatedProcessingTimeMs;
                }

                var kernelArgs = validInputBuffers
                    .Concat(validOutputBuffers)
                    .Select((buffer, index) => new KernelArgument
                    {
                        Name = $"arg_{index}",
                        Value = buffer,
                        Type = typeof(AbstractionsMemory.IUnifiedMemoryBuffer<T>),
                        IsDeviceMemory = true,
                        MemoryBuffer = buffer as IUnifiedMemoryBuffer
                    })
                    .ToArray();

                // Check for cancellation before kernel compilation
                cancellationToken.ThrowIfCancellationRequested();

                // Get or compile kernel for this device
                var compiledKernel = await kernelManager.GetOrCompileOperationKernelAsync(
                    "work_item_kernel", // This would be determined by the work item type - TODO
                    [typeof(T)],
                    typeof(T),
                    device,
                    null,
                    null,
                    cancellationToken);

                // Check for cancellation before kernel execution
                cancellationToken.ThrowIfCancellationRequested();

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
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Work item {WorkItemId} execution cancelled on device {DeviceId}",
                    workItem.Id, device.Info.Id);

                if (status != null)
                {
                    status.Status = WorkStatus.Failed;
                    status.EndTime = DateTimeOffset.UtcNow;
                }

                throw;
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
                var loadImbalance = LoadBalancer.CalculateLoadImbalance(_deviceQueues);

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
        private readonly Lock _statsLock = new();
        private readonly DeviceQueueStatistics _statistics;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceWorkQueue{T}"/> class.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="deviceIndex">Index of the device.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">
        /// device
        /// or
        /// logger
        /// </exception>
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
            if (_disposed)
            {
                return;
            }

            if (workItem == null)
            {
                _logger.LogWarning("Attempted to enqueue null work item to device {DeviceId}", _device.Info.Id);
                return;
            }

            // Validate work item has valid buffers
            if (workItem.InputBuffers == null || workItem.OutputBuffers == null)
            {
                _logger.LogWarning("Work item {WorkItemId} has null buffers, skipping", workItem.Id);
                return;
            }

            try
            {
                _workQueue.Enqueue(workItem);
                _ = _workAvailable.Release();

                lock (_statsLock)
                {
                    _statistics.TotalEnqueued++;
                }

                _logger.LogTrace("Enqueued work item {WorkItemId} to device {DeviceId}",
                    workItem.Id, _device.Info.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue work item {WorkItemId} to device {DeviceId}",
                    workItem.Id, _device.Info.Id);
            }
        }

        /// <summary>
        /// Dequeues a work item from this device queue.
        /// </summary>
        public async ValueTask<WorkItem<T>?> DequeueWorkAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return null;
            }

            try
            {
                _ = await _workAvailable.WaitAsync(1000, cancellationToken); // Wait up to 1 second

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
                    _ = await _workAvailable.WaitAsync(0, cancellationToken);
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
        public DeviceQueueStatistics Statistics
        {
            get
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
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or
        /// resetting unmanaged resources asynchronously.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous dispose operation.
        /// </returns>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

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
        private readonly Lock _statsLock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="StealingCoordinator"/> class.
        /// </summary>
        /// <param name="deviceCount">The device count.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">logger</exception>
        public StealingCoordinator(int deviceCount, ILogger logger)
        {
            _deviceCount = deviceCount;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _successfulSteals = new int[deviceCount, deviceCount];
            _failedSteals = new int[deviceCount, deviceCount];
        }

        /// <summary>
        /// Records the successful steal.
        /// </summary>
        /// <param name="thiefIndex">Index of the thief.</param>
        /// <param name="victimIndex">Index of the victim.</param>
        public void RecordSuccessfulSteal(int thiefIndex, int victimIndex)
        {
            lock (_statsLock)
            {
                _successfulSteals[thiefIndex, victimIndex]++;
            }
        }

        /// <summary>
        /// Records the failed steal.
        /// </summary>
        /// <param name="thiefIndex">Index of the thief.</param>
        /// <param name="victimIndex">Index of the victim.</param>
        public void RecordFailedSteal(int thiefIndex, int victimIndex)
        {
            lock (_statsLock)
            {
                _failedSteals[thiefIndex, victimIndex]++;
            }
        }

        /// <summary>
        /// Gets the statistics.
        /// </summary>
        /// <returns></returns>
        public StealingStatistics Statistics
        {
            get
            {
                lock (_statsLock)
                {
                    var totalSuccessful = 0;
                    var totalFailed = 0;

                    for (var i = 0; i < _deviceCount; i++)
                    {
                        for (var j = 0; j < _deviceCount; j++)
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
    }

    /// <summary>
    /// Manages load balancing across devices.
    /// </summary>
    public class LoadBalancer
    {
        private readonly IAccelerator[] _devices;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadBalancer"/> class.
        /// </summary>
        /// <param name="devices">The devices.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">
        /// devices
        /// or
        /// logger
        /// </exception>
        public LoadBalancer(IAccelerator[] devices, ILogger logger)
        {
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Distributes the work items.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="workItems">The work items.</param>
        /// <param name="deviceQueues">The device queues.</param>
        /// <exception cref="ArgumentNullException">workItems</exception>
        /// <exception cref="ArgumentException">Device queues cannot be null or empty - deviceQueues</exception>
        public void DistributeWorkItems<T>(List<WorkItem<T>> workItems, DeviceWorkQueue<T>[] deviceQueues) where T : unmanaged
        {
            if (workItems == null)
            {
                _logger.LogError("Cannot distribute null work items");
                throw new ArgumentNullException(nameof(workItems));
            }

            if (deviceQueues == null || deviceQueues.Length == 0)
            {
                _logger.LogError("Cannot distribute work items: no device queues available");
                throw new ArgumentException("Device queues cannot be null or empty", nameof(deviceQueues));
            }

            var validWorkItems = workItems.Where(item => item != null).ToList();
            if (validWorkItems.Count == 0)
            {
                _logger.LogWarning("No valid work items to distribute");
                return;
            }

            try
            {
                // Simple round-robin distribution
                for (var i = 0; i < validWorkItems.Count; i++)
                {
                    var deviceIndex = i % deviceQueues.Length;
                    var targetQueue = deviceQueues[deviceIndex];

                    if (targetQueue != null)
                    {
                        targetQueue.EnqueueWork(validWorkItems[i]);
                    }
                    else
                    {
                        _logger.LogWarning("Device queue at index {DeviceIndex} is null, skipping work item {WorkItemId}",
                            deviceIndex, validWorkItems[i].Id);
                    }
                }

                _logger.LogDebug("Distributed {WorkItemCount} work items across {DeviceCount} devices",
                    validWorkItems.Count, deviceQueues.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while distributing work items");
                throw;
            }
        }

        /// <summary>
        /// Calculates the load imbalance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="deviceQueues">The device queues.</param>
        /// <returns></returns>
        public static double CalculateLoadImbalance<T>(DeviceWorkQueue<T>[] deviceQueues) where T : unmanaged
        {
            var workCounts = deviceQueues.Select(q => q.WorkCount).ToArray();
            var maxWork = workCounts.Max();
            var minWork = workCounts.Min();

            return maxWork > 0 ? (double)(maxWork - minWork) / maxWork : 0;
        }

        /// <summary>
        /// Rebalances the work asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="deviceQueues">The device queues.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async ValueTask RebalanceWorkAsync<T>(DeviceWorkQueue<T>[] deviceQueues, CancellationToken cancellationToken) where T : unmanaged
        {
            _logger.LogDebug("Rebalancing work across {DeviceCount} devices", deviceQueues.Length);

            // Simple rebalancing: move work from rich queues to poor queues
            var workCounts = deviceQueues.Select(q => q.WorkCount).ToArray();
            var avgWork = workCounts.Average();

            for (var i = 0; i < deviceQueues.Length; i++)
            {
                var currentWork = workCounts[i];
                if (currentWork > avgWork * 1.5) // Queue has 50% more work than average
                {
                    var excessWork = (int)(currentWork - avgWork);
                    for (var j = 0; j < excessWork / 2; j++) // Move half the excess
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

    /// <summary>
    /// Work Item Status
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class WorkItemStatus<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the work item.
        /// </summary>
        /// <value>
        /// The work item.
        /// </value>
        public required WorkItem<T> WorkItem { get; set; }

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <value>
        /// The status.
        /// </value>
        public WorkStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the index of the assigned device.
        /// </summary>
        /// <value>
        /// The index of the assigned device.
        /// </value>
        public int AssignedDeviceIndex { get; set; }

        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        /// <value>
        /// The start time.
        /// </value>
        public DateTimeOffset? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        /// <value>
        /// The end time.
        /// </value>
        public DateTimeOffset? EndTime { get; set; }
    }

    /// <summary>
    /// Work Status
    /// </summary>
    public enum WorkStatus
    {
        /// <summary>
        /// The pending
        /// </summary>
        Pending,

        /// <summary>
        /// The in progress
        /// </summary>
        InProgress,

        /// <summary>
        /// The completed
        /// </summary>
        Completed,

        /// <summary>
        /// The failed
        /// </summary>
        Failed
    }

    /// <summary>
    /// Work Stealing Statistics
    /// </summary>
    public class WorkStealingStatistics
    {
        /// <summary>
        /// Gets or sets the total work items.
        /// </summary>
        /// <value>
        /// The total work items.
        /// </value>
        public int TotalWorkItems { get; set; }

        /// <summary>
        /// Gets or sets the completed work items.
        /// </summary>
        /// <value>
        /// The completed work items.
        /// </value>
        public int CompletedWorkItems { get; set; }

        /// <summary>
        /// Gets or sets the in progress work items.
        /// </summary>
        /// <value>
        /// The in progress work items.
        /// </value>
        public int InProgressWorkItems { get; set; }

        /// <summary>
        /// Gets or sets the pending work items.
        /// </summary>
        /// <value>
        /// The pending work items.
        /// </value>
        public int PendingWorkItems { get; set; }

        /// <summary>
        /// Gets or sets the device statistics.
        /// </summary>
        /// <value>
        /// The device statistics.
        /// </value>
        public DeviceQueueStatistics[] DeviceStatistics { get; set; } = [];

        /// <summary>
        /// Gets or sets the stealing statistics.
        /// </summary>
        /// <value>
        /// The stealing statistics.
        /// </value>
        public StealingStatistics StealingStatistics { get; set; } = new();
    }

    /// <summary>
    /// Device Queue Statistics
    /// </summary>
    public class DeviceQueueStatistics
    {
        /// <summary>
        /// Gets or sets the index of the device.
        /// </summary>
        /// <value>
        /// The index of the device.
        /// </value>
        public int DeviceIndex { get; set; }

        /// <summary>
        /// Gets or sets the size of the current queue.
        /// </summary>
        /// <value>
        /// The size of the current queue.
        /// </value>
        public int CurrentQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the total enqueued.
        /// </summary>
        /// <value>
        /// The total enqueued.
        /// </value>
        public long TotalEnqueued { get; set; }

        /// <summary>
        /// Gets or sets the total dequeued.
        /// </summary>
        /// <value>
        /// The total dequeued.
        /// </value>
        public long TotalDequeued { get; set; }

        /// <summary>
        /// Gets or sets the total stolen.
        /// </summary>
        /// <value>
        /// The total stolen.
        /// </value>
        public long TotalStolen { get; set; }
    }

    /// <summary>
    /// Stealing Statistics
    /// </summary>
    public class StealingStatistics
    {
        /// <summary>
        /// Gets or sets the total steal attempts.
        /// </summary>
        /// <value>
        /// The total steal attempts.
        /// </value>
        public long TotalStealAttempts { get; set; }

        /// <summary>
        /// Gets or sets the successful steals.
        /// </summary>
        /// <value>
        /// The successful steals.
        /// </value>
        public long SuccessfulSteals { get; set; }

        /// <summary>
        /// Gets or sets the failed steals.
        /// </summary>
        /// <value>
        /// The failed steals.
        /// </value>
        public long FailedSteals { get; set; }

        /// <summary>
        /// Gets or sets the steal success rate.
        /// </summary>
        /// <value>
        /// The steal success rate.
        /// </value>
        public double StealSuccessRate { get; set; }
    }
}
