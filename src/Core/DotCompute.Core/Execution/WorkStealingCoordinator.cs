// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Execution.Configuration;
using DotCompute.Core.Execution.Metrics;
using DotCompute.Core.Execution.Models;
using DotCompute.Core.Execution.Types;
using DotCompute.Core.Execution.Workload;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using EventId = Microsoft.Extensions.Logging.EventId;

namespace DotCompute.Core.Execution
{

    /// <summary>
    /// Coordinates work-stealing execution across multiple devices for dynamic load balancing.
    /// </summary>
    public sealed partial class WorkStealingCoordinator<T> : IAsyncDisposable where T : unmanaged
    {
        // LoggerMessage delegates - Event ID range 23000-23099 for WorkStealingCoordinator (Execution module)
        private static readonly Action<ILogger, Exception?> _logWorkItemInitError =
            LoggerMessage.Define(
                MsLogLevel.Error,
                new EventId(23000, nameof(LogWorkItemInitError)),
                "Cannot initialize work items: workload or work items is null");

        [LoggerMessage(EventId = 23001, Level = MsLogLevel.Trace, Message = "Device {ThiefDevice} stole work item {WorkItemId} from device {VictimDevice}")]
        private static partial void LogWorkItemStolen(ILogger logger, string thiefDevice, int workItemId, string victimDevice);

        [LoggerMessage(EventId = 23002, Level = MsLogLevel.Trace, Message = "Work item {WorkItemId} executed on device {DeviceId} in {ExecutionTimeMs:F2}ms")]
        private static partial void LogWorkItemExecuted(ILogger logger, int workItemId, string deviceId, double executionTimeMs);

        // Wrapper method
        private static void LogWorkItemInitError(ILogger logger)
            => _logWorkItemInitError(logger, null);

        private readonly IAccelerator[] _devices;
        private readonly WorkStealingWorkload<T> _workload;
        private readonly IUnifiedMemoryManager _memoryManager;
        private readonly ILogger _logger;
        private readonly DeviceWorkScheduler<T>[] _deviceQueues;
        private readonly ConcurrentDictionary<int, WorkItemStatus<T>> _workItemStatuses;
        private readonly StealingCoordinator _stealingCoordinator;
        private readonly LoadBalancer _loadBalancer;
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
            ArgumentNullException.ThrowIfNull(devices);
            ArgumentNullException.ThrowIfNull(workload);
            ArgumentNullException.ThrowIfNull(memoryManager);
            ArgumentNullException.ThrowIfNull(logger);
            _devices = devices;
            _workload = workload;
            _memoryManager = memoryManager;
            _logger = logger;

            _deviceQueues = [.. _devices.Select((device, index) =>

            new DeviceWorkScheduler<T>(device, index, logger))];

            _workItemStatuses = new ConcurrentDictionary<int, WorkItemStatus<T>>();
            _stealingCoordinator = new StealingCoordinator(_devices.Length, logger);
            _loadBalancer = new LoadBalancer(_devices, logger);

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
            _logger.LogInfoMessage($"Starting work-stealing execution with {_workload.WorkItems.Count} work items across {_devices.Length} devices");

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

                _logger.LogInfoMessage($"Work-stealing execution completed. Processed {_workItemStatuses.Count(kvp => kvp.Value.Status == WorkStatus.Completed)} work items");

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
        public Models.WorkStealingStatistics GetStatistics()
        {
            var stats = new Models.WorkStealingStatistics
            {
                TotalWorkItems = _workload.WorkItems.Count,
                CompletedWorkItems = _workItemStatuses.Count(kvp => kvp.Value.Status == WorkStatus.Completed),
                InProgressWorkItems = _workItemStatuses.Count(kvp => kvp.Value.Status == WorkStatus.Executing),
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
                LogWorkItemInitError(_logger);
                return;
            }

            // Initialize work item statuses
            foreach (var workItem in _workload.WorkItems)
            {
                if (workItem == null)
                {
                    _logger.LogWarningMessage("Skipping null work item during initialization");
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

                _logger.LogDebugMessage($"Initialized {_workload.WorkItems.Count} work items across {_devices.Length} devices");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Failed to distribute work items during initialization");
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

                _logger.LogDebugMessage($"Device {device.Info.Id} processed {processedItems} work items in {totalExecutionTime}ms");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebugMessage("Device {device.Info.Id} work execution cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Error executing work on device {device.Info.Id}");
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
                LogWorkItemStolen(_logger, _devices[deviceIndex].Info.Id.ToString(), stolenWork.Id, _devices[victimIndex].Info.Id.ToString());
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

#pragma warning disable CA5394 // Random is used for work-stealing load balancing, not security
            return availableVictims.Length > 0 ? availableVictims[Random.Shared.Next(availableVictims.Length)] : -1;
#pragma warning restore CA5394
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
            // Get the NUMA node of the thief device
            var thiefNumaNode = _devices[thiefIndex].Info.NumaNode;

            // If NUMA information is available, prioritize same-NUMA-node devices
            if (thiefNumaNode >= 0)
            {
                // First pass: Look for devices on the same NUMA node with work
                var sameNumaVictims = new List<(int Index, int WorkCount)>();
                for (var i = 0; i < _devices.Length; i++)
                {
                    if (i != thiefIndex &&
                        _devices[i].Info.NumaNode == thiefNumaNode &&
                        _deviceQueues[i].HasWork)
                    {
                        sameNumaVictims.Add((i, _deviceQueues[i].WorkCount));
                    }
                }

                // If we found same-NUMA-node victims, pick the one with most work
                if (sameNumaVictims.Count > 0)
                {
                    return sameNumaVictims.OrderByDescending(v => v.WorkCount).First().Index;
                }

                // Second pass: Look for devices on adjacent NUMA nodes (lower latency than distant nodes)
                var adjacentNumaVictims = new List<(int Index, int WorkCount, int NumaDistance)>();
                for (var i = 0; i < _devices.Length; i++)
                {
                    if (i != thiefIndex && _deviceQueues[i].HasWork)
                    {
                        var victimNumaNode = _devices[i].Info.NumaNode;
                        var numaDistance = victimNumaNode >= 0
                            ? Math.Abs(victimNumaNode - thiefNumaNode)
                            : int.MaxValue; // Unknown NUMA has maximum distance
                        adjacentNumaVictims.Add((i, _deviceQueues[i].WorkCount, numaDistance));
                    }
                }

                // Pick the closest NUMA node with the most work
                if (adjacentNumaVictims.Count > 0)
                {
                    return adjacentNumaVictims
                        .OrderBy(v => v.NumaDistance)
                        .ThenByDescending(v => v.WorkCount)
                        .First().Index;
                }
            }

            // Fallback: Use adjacent device indices as "nearest" (original behavior)
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
            // Hierarchical stealing: Try local NUMA node first, then expand to other nodes
            // This implements a 3-tier hierarchy:
            // 1. Same NUMA node (lowest latency)
            // 2. Adjacent NUMA nodes (medium latency)
            // 3. All other devices (highest latency)

            var thiefNumaNode = _devices[thiefIndex].Info.NumaNode;

            // Group devices by NUMA node
            var devicesByNumaNode = new Dictionary<int, List<(int Index, int WorkCount)>>();
            for (var i = 0; i < _devices.Length; i++)
            {
                if (i == thiefIndex)
                {
                    continue;
                }

                var numaNode = _devices[i].Info.NumaNode;
                var workCount = _deviceQueues[i].WorkCount;

                // Only consider devices that have work to steal (more than 1 item)
                if (workCount <= 1)
                {
                    continue;
                }

                if (!devicesByNumaNode.TryGetValue(numaNode, out var nodeDevices))
                {
                    nodeDevices = [];
                    devicesByNumaNode[numaNode] = nodeDevices;
                }
                nodeDevices.Add((i, workCount));
            }

            // Tier 1: Try same NUMA node first
            if (thiefNumaNode >= 0 && devicesByNumaNode.TryGetValue(thiefNumaNode, out var sameNodeDevices) && sameNodeDevices.Count > 0)
            {
                // Pick the richest device on the same NUMA node
                return sameNodeDevices.OrderByDescending(d => d.WorkCount).First().Index;
            }

            // Tier 2: Try adjacent NUMA nodes (distance 1)
            if (thiefNumaNode >= 0)
            {
                var adjacentNodes = new[] { thiefNumaNode - 1, thiefNumaNode + 1 };
                foreach (var adjNode in adjacentNodes)
                {
                    if (devicesByNumaNode.TryGetValue(adjNode, out var adjNodeDevices) && adjNodeDevices.Count > 0)
                    {
                        return adjNodeDevices.OrderByDescending(d => d.WorkCount).First().Index;
                    }
                }
            }

            // Tier 3: Fall back to richest victim across all remaining nodes
            // Sort NUMA nodes by distance from thief, then pick richest device
            var sortedNodes = devicesByNumaNode
                .Where(kvp => kvp.Value.Count > 0)
                .OrderBy(kvp => thiefNumaNode >= 0 && kvp.Key >= 0 ? Math.Abs(kvp.Key - thiefNumaNode) : int.MaxValue)
                .ThenByDescending(kvp => kvp.Value.Max(d => d.WorkCount))
                .ToList();

            if (sortedNodes.Count > 0)
            {
                return sortedNodes.First().Value.OrderByDescending(d => d.WorkCount).First().Index;
            }

            // Final fallback: Use richest victim strategy
            return SelectRichestVictim(thiefIndex);
        }

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
                status.Status = WorkStatus.Executing;
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
                    _logger.LogWarningMessage("Work item {workItem.Id} has no valid buffers, simulating execution");
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
                        Type = typeof(IUnifiedMemoryBuffer<T>),
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

                LogWorkItemExecuted(_logger, workItem.Id, device.Info.Id.ToString(), executionTimeMs);

                return executionTimeMs;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebugMessage($"Work item {workItem.Id} execution cancelled on device {device.Info.Id}");

                if (status != null)
                {
                    status.Status = WorkStatus.Failed;
                    status.EndTime = DateTimeOffset.UtcNow;
                }

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Failed to execute work item {workItem.Id} on device {device.Info.Id}");

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
                    _logger.LogDebugMessage($"Load imbalance detected: {loadImbalance * 100}%. Triggering rebalancing.");

                    await _loadBalancer.RebalanceWorkAsync(_deviceQueues, cancellationToken);
                }

                await Task.Delay(100, cancellationToken); // Check every 100ms
            }
        }

        #endregion
    }
}
