// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Core.Execution.Workload;
using DotCompute.Core.Logging;
using DotCompute.Abstractions.Execution;
using DotCompute.Abstractions.Debugging.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution
{
    /// <summary>
    /// Work queue for a specific device in work-stealing execution.
    /// </summary>
    public sealed class DeviceWorkQueue<T> : IAsyncDisposable where T : unmanaged
    {
        private readonly ILogger _logger;
        private readonly IAccelerator _device;
        private readonly int _deviceIndex;
        private readonly ConcurrentQueue<WorkItem<T>> _workQueue = new();
        private readonly SemaphoreSlim _workAvailable = new(0);
        private readonly object _statsLock = new();
        private DeviceQueueStatistics _statistics = new();
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
                _logger.LogWarningMessage("Attempted to enqueue null work item to device {_device.Info.Id}");
                return;
            }

            // Validate work item has valid buffers
            if (workItem.InputBuffers == null || workItem.OutputBuffers == null)
            {
                _logger.LogWarningMessage("Work item {workItem.Id} has null buffers, skipping");
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
                _logger.LogErrorMessage(ex, $"Failed to enqueue work item {workItem.Id} to device {_device.Info.Id}");
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
}