// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Execution.Types;
using DotCompute.Core.Execution.Configuration;
using DotCompute.Core.Execution.Pipeline;
using DotCompute.Core.Execution.Plans;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution.Scheduling
{
    /// <summary>
    /// Schedules resources and assigns work to devices based on performance characteristics.
    /// Provides intelligent device selection, workload distribution, and layer assignment
    /// for different parallelization strategies.
    /// </summary>
    public sealed class ResourceScheduler
    {
        private readonly ILogger _logger;
        private readonly DevicePerformanceEstimator _performanceEstimator;

        /// <summary>
        /// Initializes a new instance of the ResourceScheduler class.
        /// </summary>
        /// <param name="logger">Logger for scheduler operations</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null</exception>
        public ResourceScheduler(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceEstimator = new DevicePerformanceEstimator(logger);
        }

        /// <summary>
        /// Selects optimal devices based on performance characteristics and options.
        /// Evaluates devices using performance scoring and applies filtering based on constraints.
        /// </summary>
        /// <param name="availableDevices">All available accelerator devices</param>
        /// <param name="options">Data parallelism options with device preferences</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Array of selected optimal devices in performance order</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        /// <exception cref="InvalidOperationException">Thrown when no suitable devices are found</exception>
        public async ValueTask<IAccelerator[]> SelectOptimalDevicesAsync(
            IAccelerator[] availableDevices,
            DataParallelismOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(availableDevices);
            ArgumentNullException.ThrowIfNull(options);

            var deviceCandidates = availableDevices.ToList();

            // Filter by target devices if specified
            if (options.TargetDevices != null && options.TargetDevices.Length > 0)
            {
                deviceCandidates = [.. deviceCandidates.Where(d => options.TargetDevices.Contains(d.Info.Id))];
                
                if (deviceCandidates.Count == 0)
                {
                    throw new InvalidOperationException("No available devices match the target device list");
                }
            }

            // Limit by max devices
            if (options.MaxDevices.HasValue)
            {
                deviceCandidates = [.. deviceCandidates.Take(options.MaxDevices.Value)];
            }

            // Score and rank devices
            var deviceScores = await Task.WhenAll(
                deviceCandidates.Select(async d => new
                {
                    Device = d,
                    Score = await _performanceEstimator.CalculateDeviceScoreAsync(d, cancellationToken)
                }));

            var selectedDevices = deviceScores
                .OrderByDescending(ds => ds.Score)
                .Select(ds => ds.Device)
                .ToArray();

            if (selectedDevices.Length == 0)
            {
                throw new InvalidOperationException("No suitable devices available for execution");
            }

            _logger.LogInformation("Selected {SelectedCount} optimal devices from {AvailableCount} available",
                selectedDevices.Length, availableDevices.Length);

            return selectedDevices;
        }

        /// <summary>
        /// Distributes workload across selected devices using the specified load balancing strategy.
        /// Creates device work assignments with appropriate data partitioning.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data being distributed</typeparam>
        /// <param name="inputBuffers">Input data buffers to distribute</param>
        /// <param name="devices">Selected devices for workload distribution</param>
        /// <param name="strategy">Load balancing strategy to apply</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Workload distribution with device assignments</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        /// <exception cref="ArgumentException">Thrown when no devices are provided</exception>
        public async ValueTask<WorkloadDistribution> DistributeWorkloadAsync<T>(
            IUnifiedMemoryBuffer<T>[] inputBuffers,
            IAccelerator[] devices,
            LoadBalancingStrategy strategy,
            CancellationToken cancellationToken) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(inputBuffers);
            ArgumentNullException.ThrowIfNull(devices);

            if (devices.Length == 0)
            {
                throw new ArgumentException("At least one device must be provided", nameof(devices));
            }

            var totalElements = inputBuffers.Sum(b => (int)b.Length);
            var deviceAssignments = new List<DeviceWorkAssignment>();

            deviceAssignments = strategy switch
            {
                LoadBalancingStrategy.RoundRobin => await DistributeRoundRobinAsync(inputBuffers, devices, cancellationToken),
                LoadBalancingStrategy.Weighted => await DistributeWeightedAsync(inputBuffers, devices, cancellationToken),
                LoadBalancingStrategy.Adaptive => await DistributeAdaptiveAsync(inputBuffers, devices, cancellationToken),
                LoadBalancingStrategy.Dynamic => await DistributeDynamicAsync(inputBuffers, devices, cancellationToken),
                _ => await DistributeRoundRobinAsync(inputBuffers, devices, cancellationToken),
            };
            
            _logger.LogInformation("Distributed {TotalElements} elements across {DeviceCount} devices using {Strategy} strategy",
                totalElements, devices.Length, strategy);

            return new WorkloadDistribution
            {
                DeviceAssignments = deviceAssignments,
                Strategy = strategy,
                TotalElements = totalElements
            };
        }

        /// <summary>
        /// Assigns model layers to devices based on memory and compute requirements.
        /// Optimizes layer placement to minimize communication overhead and balance load.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data in model layers</typeparam>
        /// <param name="layers">Model layers to assign</param>
        /// <param name="devices">Available devices for layer assignment</param>
        /// <param name="options">Model parallelism options</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Dictionary mapping layer IDs to assigned devices</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        /// <exception cref="ArgumentException">Thrown when no layers or devices are provided</exception>
        public async ValueTask<Dictionary<int, IAccelerator>> AssignLayersToDevicesAsync<T>(
            List<ModelLayer<T>> layers,
            IAccelerator[] devices,
            ModelParallelismOptions options,
            CancellationToken cancellationToken) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(layers);
            ArgumentNullException.ThrowIfNull(devices);
            ArgumentNullException.ThrowIfNull(options);

            if (layers.Count == 0)
            {
                throw new ArgumentException("At least one layer must be provided", nameof(layers));
            }

            if (devices.Length == 0)
            {
                throw new ArgumentException("At least one device must be provided", nameof(devices));
            }

            var assignments = new Dictionary<int, IAccelerator>();

            assignments = options.LayerAssignment switch
            {
                LayerAssignmentStrategy.Sequential => await AssignLayersSequentiallyAsync(layers, devices, cancellationToken),
                LayerAssignmentStrategy.Interleaved => await AssignLayersInterleavedAsync(layers, devices, cancellationToken),
                LayerAssignmentStrategy.Automatic => await AssignLayersAutomaticallyAsync(layers, devices, cancellationToken),
                _ => await AssignLayersSequentiallyAsync(layers, devices, cancellationToken),
            };
            
            _logger.LogInformation("Assigned {LayerCount} layers across {DeviceCount} devices using {Strategy} strategy",
                layers.Count, devices.Length, options.LayerAssignment);

            return assignments;
        }

        /// <summary>
        /// Assigns pipeline stages to devices for optimal pipeline execution.
        /// Uses round-robin assignment to distribute stages evenly across devices.
        /// </summary>
        /// <param name="stages">Pipeline stage definitions to assign</param>
        /// <param name="devices">Available devices for stage assignment</param>
        /// <param name="options">Pipeline parallelism options</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Dictionary mapping stage names to assigned devices</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        /// <exception cref="ArgumentException">Thrown when no stages or devices are provided</exception>
        public async ValueTask<Dictionary<string, IAccelerator>> AssignStagesToDevicesAsync(
            List<PipelineStageDefinition> stages,
            IAccelerator[] devices,
            PipelineParallelismOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stages);
            ArgumentNullException.ThrowIfNull(devices);
            ArgumentNullException.ThrowIfNull(options);

            if (stages.Count == 0)
            {
                throw new ArgumentException("At least one stage must be provided", nameof(stages));
            }

            if (devices.Length == 0)
            {
                throw new ArgumentException("At least one device must be provided", nameof(devices));
            }

            await Task.CompletedTask.ConfigureAwait(false);
            var assignments = new Dictionary<string, IAccelerator>();

            // Simple round-robin assignment for pipeline stages
            for (var i = 0; i < stages.Count; i++)
            {
                var deviceIndex = i % devices.Length;
                assignments[stages[i].Name] = devices[deviceIndex];
            }

            _logger.LogInformation("Assigned {StageCount} pipeline stages across {DeviceCount} devices",
                stages.Count, devices.Length);

            return assignments;
        }

        #region Private Helper Methods

        /// <summary>
        /// Distributes workload using round-robin strategy.
        /// </summary>
        private static async ValueTask<List<DeviceWorkAssignment>> DistributeRoundRobinAsync<T>(
            IUnifiedMemoryBuffer<T>[] inputBuffers,
            IAccelerator[] devices,
            CancellationToken cancellationToken) where T : unmanaged
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var assignments = new List<DeviceWorkAssignment>();
            var totalElements = inputBuffers.Sum(b => (int)b.Length);
            var elementsPerDevice = totalElements / devices.Length;
            var remainingElements = totalElements % devices.Length;

            var currentIndex = 0;
            for (var i = 0; i < devices.Length; i++)
            {
                var elementCount = elementsPerDevice + (i < remainingElements ? 1 : 0);

                if (elementCount > 0)
                {
                    assignments.Add(new DeviceWorkAssignment
                    {
                        Device = devices[i],
                        StartIndex = currentIndex,
                        ElementCount = elementCount,
                        InputBuffers = GetBufferSlice(inputBuffers, currentIndex, elementCount),
                        OutputBuffers = GetBufferSlice(inputBuffers, currentIndex, elementCount) // Simplified
                    });

                    currentIndex += elementCount;
                }
            }

            return assignments;
        }

        /// <summary>
        /// Distributes workload using weighted strategy based on device performance.
        /// </summary>
        private async ValueTask<List<DeviceWorkAssignment>> DistributeWeightedAsync<T>(
            IUnifiedMemoryBuffer<T>[] inputBuffers,
            IAccelerator[] devices,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var assignments = new List<DeviceWorkAssignment>();
            var totalElements = inputBuffers.Sum(b => (int)b.Length);

            // Calculate device weights based on performance characteristics
            var deviceWeights = await Task.WhenAll(
                devices.Select(async d => await _performanceEstimator.CalculateDeviceWeightAsync(d, cancellationToken)).ToList());

            var totalWeight = deviceWeights.Sum();
            if (totalWeight <= 0)
            {
                return await DistributeRoundRobinAsync(inputBuffers, devices, cancellationToken);
            }

            var currentIndex = 0;
            for (var i = 0; i < devices.Length; i++)
            {
                var elementCount = Math.Max(1, (int)(totalElements * (deviceWeights[i] / totalWeight)));

                // Ensure we don't exceed total elements
                if (currentIndex + elementCount > totalElements)
                {
                    elementCount = totalElements - currentIndex;
                }

                if (elementCount > 0)
                {
                    assignments.Add(new DeviceWorkAssignment
                    {
                        Device = devices[i],
                        StartIndex = currentIndex,
                        ElementCount = elementCount,
                        InputBuffers = GetBufferSlice(inputBuffers, currentIndex, elementCount),
                        OutputBuffers = GetBufferSlice(inputBuffers, currentIndex, elementCount)
                    });

                    currentIndex += elementCount;
                }

                if (currentIndex >= totalElements)
                {
                    break;
                }

            }

            return assignments;
        }

        /// <summary>
        /// Distributes workload using adaptive strategy.
        /// </summary>
        private async ValueTask<List<DeviceWorkAssignment>> DistributeAdaptiveAsync<T>(
            IUnifiedMemoryBuffer<T>[] inputBuffers,
            IAccelerator[] devices,
            CancellationToken cancellationToken) where T : unmanaged
            // Start with weighted distribution and adapt based on historical performance
            => await DistributeWeightedAsync(inputBuffers, devices, cancellationToken);

        /// <summary>
        /// Distributes workload using dynamic strategy with work stealing.
        /// </summary>
        private async ValueTask<List<DeviceWorkAssignment>> DistributeDynamicAsync<T>(
        IUnifiedMemoryBuffer<T>[] inputBuffers,
        IAccelerator[] devices,
        CancellationToken cancellationToken) where T : unmanaged
        {
            // Dynamic distribution with work stealing capability
            var assignments = await DistributeWeightedAsync(inputBuffers, devices, cancellationToken);

            // Mark as work-stealing enabled
            foreach (var assignment in assignments)
            {
                assignment.EnableWorkStealing = true;
            }

            return assignments;
        }

        /// <summary>
        /// Assigns layers sequentially to devices in round-robin fashion.
        /// </summary>
        private static async ValueTask<Dictionary<int, IAccelerator>> AssignLayersSequentiallyAsync<T>(
            List<ModelLayer<T>> layers,
            IAccelerator[] devices,
            CancellationToken cancellationToken) where T : unmanaged
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var assignments = new Dictionary<int, IAccelerator>();

            for (var i = 0; i < layers.Count; i++)
            {
                var deviceIndex = i % devices.Length;
                assignments[layers[i].LayerId] = devices[deviceIndex];
            }

            return assignments;
        }

        /// <summary>
        /// Assigns layers in interleaved fashion across devices.
        /// </summary>
        private static async ValueTask<Dictionary<int, IAccelerator>> AssignLayersInterleavedAsync<T>(
            List<ModelLayer<T>> layers,
            IAccelerator[] devices,
            CancellationToken cancellationToken) where T : unmanaged
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var assignments = new Dictionary<int, IAccelerator>();
            var layersPerDevice = layers.Count / devices.Length;
            var remainingLayers = layers.Count % devices.Length;

            var layerIndex = 0;
            for (var deviceIndex = 0; deviceIndex < devices.Length; deviceIndex++)
            {
                var layerCount = layersPerDevice + (deviceIndex < remainingLayers ? 1 : 0);

                for (var i = 0; i < layerCount && layerIndex < layers.Count; i++)
                {
                    assignments[layers[layerIndex].LayerId] = devices[deviceIndex];
                    layerIndex++;
                }
            }

            return assignments;
        }

        /// <summary>
        /// Assigns layers automatically based on resource requirements and device capabilities.
        /// </summary>
        private async ValueTask<Dictionary<int, IAccelerator>> AssignLayersAutomaticallyAsync<T>(
            List<ModelLayer<T>> layers,
            IAccelerator[] devices,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var assignments = new Dictionary<int, IAccelerator>();
            var deviceCapabilities = await Task.WhenAll(
                devices.Select(async d => new
                {
                    Device = d,
                    MemoryCapacity = d.Info.AvailableMemory,
                    ComputeCapability = await _performanceEstimator.EstimateComputeCapabilityAsync(d, cancellationToken)
                }));

            // Sort devices by capability
            var sortedDevices = deviceCapabilities
                .OrderByDescending(dc => dc.ComputeCapability)
                .ThenByDescending(dc => dc.MemoryCapacity)
                .ToArray();

            // Assign layers based on memory and compute requirements
            var deviceLoads = new Dictionary<IAccelerator, (long memory, double compute)>();
            foreach (var deviceInfo in sortedDevices)
            {
                deviceLoads[deviceInfo.Device] = (0, 0);
            }

            // Sort layers by resource requirements (descending)
            var sortedLayers = layers
                .OrderByDescending(l => l.MemoryRequirementBytes)
                .ThenByDescending(l => l.ComputeRequirementFLOPS)
                .ToList();

            foreach (var layer in sortedLayers)
            {
                // Find device with least current load that can handle this layer
                var bestDevice = deviceLoads
                    .Where(kvp => kvp.Key.Info.AvailableMemory >= kvp.Value.memory + layer.MemoryRequirementBytes)
                    .OrderBy(kvp => kvp.Value.memory + kvp.Value.compute)
                    .FirstOrDefault();

                if (bestDevice.Key != null)
                {
                    assignments[layer.LayerId] = bestDevice.Key;
                    deviceLoads[bestDevice.Key] = (
                        bestDevice.Value.memory + layer.MemoryRequirementBytes,
                        bestDevice.Value.compute + layer.ComputeRequirementFLOPS
                    );
                }
                else
                {
                    // Fallback to device with most capacity
                    var fallbackDevice = sortedDevices.First().Device;
                    assignments[layer.LayerId] = fallbackDevice;
                    
                    // Update the fallback device's load
                    if (deviceLoads.ContainsKey(fallbackDevice))
                    {
                        var currentLoad = deviceLoads[fallbackDevice];
                        deviceLoads[fallbackDevice] = (
                            currentLoad.memory + layer.MemoryRequirementBytes,
                            currentLoad.compute + layer.ComputeRequirementFLOPS
                        );
                    }
                }
            }

            return assignments;
        }

        /// <summary>
        /// Creates a slice of buffers for workload distribution.
        /// </summary>
        private static object[] GetBufferSlice<T>(IUnifiedMemoryBuffer<T>[] buffers, int startIndex, int count) where T : unmanaged
            // Simplified buffer slicing - in practice would create proper buffer views - TODO
            => [.. buffers.Cast<object>()];

        #endregion
    }
}
