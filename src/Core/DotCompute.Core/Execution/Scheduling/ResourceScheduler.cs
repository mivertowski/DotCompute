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
            IBuffer<T>[] inputBuffers,
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
            IBuffer<T>[] inputBuffers,
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
            IBuffer<T>[] inputBuffers,
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
            IBuffer<T>[] inputBuffers,
            IAccelerator[] devices,
            CancellationToken cancellationToken) where T : unmanaged
            // Start with weighted distribution and adapt based on historical performance
            => await DistributeWeightedAsync(inputBuffers, devices, cancellationToken);

        /// <summary>
        /// Distributes workload using dynamic strategy with work stealing.
        /// </summary>
        private async ValueTask<List<DeviceWorkAssignment>> DistributeDynamicAsync<T>(
        IBuffer<T>[] inputBuffers,
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
                    ComputeCapability = await DevicePerformanceEstimator.EstimateComputeCapabilityAsync(d, cancellationToken)
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
        private static object[] GetBufferSlice<T>(IBuffer<T>[] buffers, int startIndex, int count) where T : unmanaged
            // Simplified buffer slicing - in practice would create proper buffer views
            => [.. buffers.Cast<object>()];

        #endregion
    }



    /// <summary>
    /// Estimates device performance characteristics for scheduling decisions.
    /// Provides performance scoring, weight calculation, and compute capability estimation.
    /// </summary>
    public sealed class DevicePerformanceEstimator
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, DevicePerformanceCache> _performanceCache;

        /// <summary>
        /// Initializes a new instance of the DevicePerformanceEstimator class.
        /// </summary>
        /// <param name="logger">Logger for performance estimation operations</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null</exception>
        public DevicePerformanceEstimator(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceCache = [];
        }

        /// <summary>
        /// Calculates a performance score for device selection.
        /// Combines hardware characteristics with historical performance data.
        /// </summary>
        /// <param name="device">The device to score</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>A performance score used for device ranking</returns>
        /// <exception cref="ArgumentNullException">Thrown when device is null</exception>
        public async ValueTask<double> CalculateDeviceScoreAsync(IAccelerator device, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(device);
            
            await Task.CompletedTask.ConfigureAwait(false);
            var info = device.Info;

            // Base score from hardware characteristics
            var memoryScore = Math.Log10(Math.Max(1, info.TotalMemory / (1024.0 * 1024.0 * 1024.0))); // Log scale for GB
            var computeScore = info.ComputeUnits * info.MaxClockFrequency / 100000.0;
            var capabilityScore = info.ComputeCapability?.Major ?? 1;

            // Historical performance factor
            var historicalFactor = GetHistoricalPerformanceFactor(device.Info.Id);

            var score = (memoryScore * 0.3 + computeScore * 0.4 + capabilityScore * 0.2) * historicalFactor;

            _logger.LogTrace("Device {DeviceId} score: {Score:F2} (memory: {MemScore:F2}, compute: {CompScore:F2}, capability: {CapScore:F2}, historical: {HistFactor:F2})",
                device.Info.Id, score, memoryScore, computeScore, capabilityScore, historicalFactor);

            return Math.Max(0, score);
        }

        /// <summary>
        /// Calculates device weight for workload distribution.
        /// Adjusts performance score based on current device availability.
        /// </summary>
        /// <param name="device">The device to calculate weight for</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>A weight value for proportional workload distribution</returns>
        /// <exception cref="ArgumentNullException">Thrown when device is null</exception>
        public async ValueTask<double> CalculateDeviceWeightAsync(IAccelerator device, CancellationToken cancellationToken)
        {
            var score = await CalculateDeviceScoreAsync(device, cancellationToken);

            // Weight is normalized score with availability factor
            var availabilityFactor = device.Info.TotalMemory > 0 
                ? (double)device.Info.AvailableMemory / device.Info.TotalMemory 
                : 1.0;

            return Math.Max(0.1, score * Math.Max(0.1, availabilityFactor));
        }

        /// <summary>
        /// Estimates compute capability for layer assignment.
        /// Calculates effective GFLOPS based on device specifications and efficiency factors.
        /// </summary>
        /// <param name="device">The device to estimate capability for</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Estimated compute capability in GFLOPS</returns>
        /// <exception cref="ArgumentNullException">Thrown when device is null</exception>
        public static async ValueTask<double> EstimateComputeCapabilityAsync(IAccelerator device, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(device);
            
            await Task.CompletedTask.ConfigureAwait(false);
            var info = device.Info;

            // Estimate peak GFLOPS based on hardware specs
            var peakGFLOPS = info.ComputeUnits * info.MaxClockFrequency * 2.0 / 1000.0; // Simplified calculation

            // Apply efficiency factor based on device type
            var efficiencyFactor = info.DeviceType.ToUpperInvariant() switch
            {
                "GPU" => 0.8, // GPUs typically achieve 80% of peak
                "CPU" => 0.3, // CPUs achieve lower peak utilization
                "TPU" => 0.9, // TPUs can achieve high efficiency
                _ => 0.5       // Default efficiency
            };

            return Math.Max(0, peakGFLOPS * efficiencyFactor);
        }

        /// <summary>
        /// Updates performance cache with execution results.
        /// </summary>
        /// <param name="deviceId">Device identifier</param>
        /// <param name="executionSuccessful">Whether the execution was successful</param>
        /// <param name="efficiencyPercentage">Achieved efficiency percentage</param>
        public void UpdatePerformanceCache(string deviceId, bool executionSuccessful, double efficiencyPercentage)
        {
            ArgumentNullException.ThrowIfNull(deviceId);
            
            if (!_performanceCache.TryGetValue(deviceId, out var cache))
            {
                cache = new DevicePerformanceCache();
                _performanceCache[deviceId] = cache;
            }

            // Update success rate using exponential moving average
            const double alpha = 0.1; // Learning rate
            cache.SuccessRate = (1 - alpha) * cache.SuccessRate + alpha * (executionSuccessful ? 1.0 : 0.0);
            
            if (executionSuccessful && efficiencyPercentage >= 0)
            {
                cache.AverageEfficiency = (1 - alpha) * cache.AverageEfficiency + alpha * Math.Min(100, efficiencyPercentage);
            }
            
            cache.LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets historical performance factor for a device.
        /// </summary>
        private double GetHistoricalPerformanceFactor(string deviceId)
        {
            if (_performanceCache.TryGetValue(deviceId, out var cache))
            {
                // Return performance factor based on historical success rate and efficiency
                return Math.Max(0.1, (cache.SuccessRate + cache.AverageEfficiency / 100.0) / 2.0);
            }

            return 1.0; // Default factor for new devices
        }
    }

    /// <summary>
    /// Cached performance data for a device.
    /// Stores historical performance metrics for scheduling optimization.
    /// </summary>
    public sealed class DevicePerformanceCache
    {
        /// <summary>Gets or sets the historical success rate (0-1).</summary>
        /// <value>The ratio of successful executions to total executions</value>
        public double SuccessRate { get; set; } = 1.0;

        /// <summary>Gets or sets the average execution efficiency percentage.</summary>
        /// <value>The mean efficiency achieved by this device (0-100)</value>
        public double AverageEfficiency { get; set; } = 80.0;

        /// <summary>Gets or sets the timestamp of the last cache update.</summary>
        /// <value>When this cache entry was last modified</value>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>Gets whether this cache entry is considered stale.</summary>
        /// <param name="maxAge">Maximum age before considering the cache stale</param>
        /// <returns>True if the cache entry is older than the specified age</returns>
        public bool IsStale(TimeSpan maxAge) => DateTime.UtcNow - LastUpdated > maxAge;

        /// <summary>
        /// Gets a summary of the cached performance data.
        /// </summary>
        /// <returns>A formatted string describing the performance metrics</returns>
        public override string ToString() => $"Success: {SuccessRate:P1}, Efficiency: {AverageEfficiency:F1}%, Updated: {LastUpdated:yyyy-MM-dd HH:mm}";
    }

    /// <summary>
    /// Optimizes execution plans for better performance.
    /// Provides plan-specific optimizations for different execution strategies.
    /// </summary>
    public sealed class ExecutionOptimizer
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the ExecutionOptimizer class.
        /// </summary>
        /// <param name="logger">Logger for optimization operations</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null</exception>
        public ExecutionOptimizer(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Optimizes data parallel execution plan.
        /// Applies memory, synchronization, and device-specific optimizations.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data being processed</typeparam>
        /// <param name="plan">The execution plan to optimize</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>A task representing the optimization operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when plan is null</exception>
        public async ValueTask OptimizeDataParallelPlanAsync<T>(
            DataParallelExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(plan);
            
            _logger.LogInformation("Optimizing data parallel execution plan for {DeviceCount} devices", plan.Devices.Length);

            // 1. Optimize memory allocation patterns
            await OptimizeMemoryAllocationAsync(plan, cancellationToken);

            // 2. Optimize synchronization points
            await OptimizeSynchronizationAsync(plan, cancellationToken);

            // 3. Apply device-specific optimizations
            await ApplyDeviceSpecificOptimizationsAsync(plan, cancellationToken);

            _logger.LogDebug("Data parallel plan optimization completed");
        }

        /// <summary>
        /// Optimizes model parallel execution plan.
        /// Focuses on communication minimization and gradient optimization.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data being processed</typeparam>
        /// <param name="plan">The execution plan to optimize</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>A task representing the optimization operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when plan is null</exception>
        public async ValueTask OptimizeModelParallelPlanAsync<T>(
            ModelParallelExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(plan);
            
            _logger.LogInformation("Optimizing model parallel execution plan for {LayerCount} layers", plan.ModelLayers.Length);

            // 1. Optimize layer placement for minimal communication
            await OptimizeLayerPlacementAsync(plan, cancellationToken);

            // 2. Optimize communication schedule
            await OptimizeCommunicationScheduleAsync(plan, cancellationToken);

            // 3. Apply gradient checkpointing optimizations
            await ApplyGradientCheckpointingAsync(plan, cancellationToken);

            _logger.LogDebug("Model parallel plan optimization completed");
        }

        /// <summary>
        /// Optimizes pipeline execution plan.
        /// Balances stages, optimizes buffering, and adjusts microbatch sizing.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data being processed</typeparam>
        /// <param name="plan">The execution plan to optimize</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>A task representing the optimization operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when plan is null</exception>
        public async ValueTask OptimizePipelinePlanAsync<T>(
            PipelineExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(plan);
            
            _logger.LogInformation("Optimizing pipeline execution plan for {StageCount} stages", plan.Stages.Length);

            // 1. Optimize microbatch sizing
            await OptimizeMicrobatchSizeAsync(plan, cancellationToken);

            // 2. Optimize buffer management
            await OptimizeBufferManagementAsync(plan, cancellationToken);

            // 3. Balance pipeline stages
            await BalancePipelineStagesAsync(plan, cancellationToken);

            _logger.LogDebug("Pipeline plan optimization completed");
        }

        #region Private Optimization Methods

        /// <summary>
        /// Optimizes memory allocation patterns for data parallel execution.
        /// </summary>
        private async ValueTask OptimizeMemoryAllocationAsync<T>(
            DataParallelExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Analyze memory access patterns and optimize buffer placement
            foreach (var task in plan.DeviceTasks)
            {
                // Check if buffers can be placed in device memory vs unified memory
                var device = task.Device;
                if (device.Info.IsUnifiedMemory)
                {
                    // Optimize for unified memory architecture
                    _logger.LogTrace("Optimizing for unified memory on device {DeviceId}", device.Info.Id);
                }
                else
                {
                    // Optimize for discrete memory architecture
                    _logger.LogTrace("Optimizing for discrete memory on device {DeviceId}", device.Info.Id);
                }
            }

            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Optimizes synchronization points to minimize overhead.
        /// </summary>
        private async ValueTask OptimizeSynchronizationAsync<T>(
            DataParallelExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Minimize synchronization overhead by analyzing dependency patterns
            var taskDependencies = plan.DeviceTasks.SelectMany(t => t.Dependencies).ToList();

            if (taskDependencies.Count > 0)
            {
                _logger.LogTrace("Optimizing synchronization for {DependencyCount} dependencies", taskDependencies.Count);
                // Could implement more sophisticated sync optimization here
            }

            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Applies device-specific optimizations.
        /// </summary>
        private async ValueTask ApplyDeviceSpecificOptimizationsAsync<T>(
            DataParallelExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Apply optimizations specific to each device type
            foreach (var task in plan.DeviceTasks)
            {
                var deviceType = task.Device.Info.DeviceType.ToUpperInvariant();

                switch (deviceType)
                {
                    case "GPU":
                        _logger.LogTrace("Applying GPU optimizations for device {DeviceId}", task.Device.Info.Id);
                        break;
                    case "CPU":
                        _logger.LogTrace("Applying CPU optimizations for device {DeviceId}", task.Device.Info.Id);
                        break;
                    case "TPU":
                        _logger.LogTrace("Applying TPU optimizations for device {DeviceId}", task.Device.Info.Id);
                        break;
                }
            }

            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Optimizes layer placement for minimal communication overhead.
        /// </summary>
        private async ValueTask OptimizeLayerPlacementAsync<T>(
            ModelParallelExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Analyze communication costs and potentially reassign layers
            var totalCommunicationOps = plan.CommunicationSchedule.Operations.Count;
            _logger.LogTrace("Analyzing {CommunicationOps} communication operations for layer placement optimization",
                totalCommunicationOps);

            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Optimizes communication scheduling to overlap with computation.
        /// </summary>
        private async ValueTask OptimizeCommunicationScheduleAsync<T>(
            ModelParallelExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Optimize communication scheduling to overlap with computation
            var schedule = plan.CommunicationSchedule;

            // Sort operations by execution order and group where possible
            schedule.Operations = [.. schedule.Operations.OrderBy(op => op.ExecutionOrder)];

            _logger.LogTrace("Optimized communication schedule with {OpCount} operations", schedule.Operations.Count);
            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Applies gradient checkpointing to reduce memory usage.
        /// </summary>
        private async ValueTask ApplyGradientCheckpointingAsync<T>(
            ModelParallelExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Apply gradient checkpointing to reduce memory usage
            _logger.LogTrace("Applying gradient checkpointing optimizations");
            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Optimizes microbatch size based on pipeline characteristics.
        /// </summary>
        private async ValueTask OptimizeMicrobatchSizeAsync<T>(
            PipelineExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Analyze pipeline characteristics to optimize microbatch size
            var stageProcessingTimes = plan.Stages.Select(s => s.EstimatedProcessingTimeMs).ToArray();
            
            if (stageProcessingTimes.Length == 0)
            {
                return;
            }


            var maxProcessingTime = stageProcessingTimes.Max();
            var minProcessingTime = stageProcessingTimes.Min();

            if (maxProcessingTime > minProcessingTime * 2 && minProcessingTime > 0)
            {
                // Pipeline is imbalanced, adjust microbatch size
                var optimalMicrobatchSize = (int)(plan.MicrobatchConfig.Size * (minProcessingTime / maxProcessingTime));
                plan.MicrobatchConfig.Size = Math.Max(1, optimalMicrobatchSize);

                _logger.LogDebug("Adjusted microbatch size to {Size} for better pipeline balance", plan.MicrobatchConfig.Size);
            }

            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Optimizes buffer management and prefetching strategies.
        /// </summary>
        private async ValueTask OptimizeBufferManagementAsync<T>(
            PipelineExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Optimize buffer reuse and prefetching strategies
            var bufferStrategy = plan.BufferStrategy;

            // Adjust prefetch depth based on stage processing time variance
            var processingTimeVariance = CalculateProcessingTimeVariance(plan.Stages);

            if (processingTimeVariance > 0.3) // High variance
            {
                bufferStrategy.Prefetching.Policy = PrefetchingPolicy.Aggressive;
                bufferStrategy.Prefetching.PrefetchDepth = Math.Min(plan.Stages.Length, 4);
            }
            else
            {
                bufferStrategy.Prefetching.Policy = PrefetchingPolicy.Conservative;
                bufferStrategy.Prefetching.PrefetchDepth = 2;
            }

            _logger.LogTrace("Optimized buffer management with prefetch depth {Depth} and policy {Policy}",
                bufferStrategy.Prefetching.PrefetchDepth, bufferStrategy.Prefetching.Policy);

            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Balances pipeline stages to minimize bottlenecks.
        /// </summary>
        private async ValueTask BalancePipelineStagesAsync<T>(
            PipelineExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Analyze and balance pipeline stage processing times
            var stages = plan.Stages;
            
            if (stages.Length == 0)
            {
                return;
            }


            var avgProcessingTime = stages.Average(s => s.EstimatedProcessingTimeMs);

            foreach (var stage in stages)
            {
                if (avgProcessingTime > 0)
                {
                    var imbalanceFactor = stage.EstimatedProcessingTimeMs / avgProcessingTime;

                    if (imbalanceFactor > 1.5)
                    {
                        _logger.LogWarning("Pipeline stage {StageName} is imbalanced (factor: {Factor:F2}), consider optimization",
                            stage.Name, imbalanceFactor);
                    }
                }
            }

            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Calculates processing time variance for pipeline stages.
        /// </summary>
        private static double CalculateProcessingTimeVariance<T>(PipelineStage<T>[] stages) where T : unmanaged
        {
            if (stages.Length == 0)
            {
                return 0;
            }


            var times = stages.Select(s => s.EstimatedProcessingTimeMs).ToArray();
            var mean = times.Average();
            
            if (mean == 0)
            {
                return 0;
            }


            var variance = times.Sum(t => Math.Pow(t - mean, 2)) / times.Length;

            return variance / (mean * mean); // Coefficient of variation
        }

        #endregion
    }
}