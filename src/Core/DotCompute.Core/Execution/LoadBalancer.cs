// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Core.Execution.Workload;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution
{
    /// <summary>
    /// Manages load balancing across devices.
    /// </summary>
    public class LoadBalancer
    {
        private readonly IAccelerator[] _devices;
        private readonly ILogger _logger;
        private readonly Dictionary<string, DeviceQueueStatistics> _statistics = new();
        private readonly object _statsLock = new();

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
        /// <param name="deviceQueues">The device schedulers.</param>
        /// <exception cref="ArgumentNullException">workItems</exception>
        /// <exception cref="ArgumentException">Device schedulers cannot be null or empty - deviceQueues</exception>
        public void DistributeWorkItems<T>(IList<WorkItem<T>> workItems, DeviceWorkScheduler<T>[] deviceQueues) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(workItems);

            if (deviceQueues == null || deviceQueues.Length == 0)
            {
                _logger.LogError("Cannot distribute work items: no device schedulers available");
                throw new ArgumentException("Device schedulers cannot be null or empty", nameof(deviceQueues));
            }

            var validWorkItems = workItems.Where(item => item != null).ToList();
            if (validWorkItems.Count == 0)
            {
                _logger.LogWarningMessage("No valid work items to distribute");
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
                        _logger.LogWarningMessage($"Device queue at index {deviceIndex} is null, skipping work item {validWorkItems[i].Id}");
                    }
                }

                _logger.LogDebugMessage($"Distributed {validWorkItems.Count} work items across {deviceQueues.Length} devices");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error occurred while distributing work items");
                throw;
            }
        }

        /// <summary>
        /// Calculates the load imbalance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="deviceQueues">The device schedulers.</param>
        /// <returns></returns>
        public static double CalculateLoadImbalance<T>(DeviceWorkScheduler<T>[] deviceQueues) where T : unmanaged
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
        /// <param name="deviceQueues">The device schedulers.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async ValueTask RebalanceWorkAsync<T>(DeviceWorkScheduler<T>[] deviceQueues, CancellationToken cancellationToken) where T : unmanaged
        {
            _logger.LogDebugMessage("Rebalancing work across {deviceQueues.Length} devices");

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
}