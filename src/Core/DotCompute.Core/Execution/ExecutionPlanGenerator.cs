// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using global::System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Models;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Execution.Types;
using DotCompute.Core.Execution.Configuration;
using DotCompute.Core.Execution.Workload;
using DotCompute.Core.Execution.Scheduling;
using DotCompute.Core.Execution.Plans;
using DotCompute.Core.Execution.Pipeline;
using DotCompute.Core.Execution.Analysis;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using ExecutionPerformanceMonitor = DotCompute.Core.Execution.PerformanceMonitor;
using DotCompute.Core.Execution.Optimization;

using System;
namespace DotCompute.Core.Execution
{
    /// <summary>
    /// Advanced execution plan generator with dependency analysis and optimization.
    /// Creates optimized execution plans for different parallelization strategies including
    /// data parallel, model parallel, and pipeline parallel execution.
    /// </summary>
    public sealed class ExecutionPlanGenerator
    {
        private readonly ILogger _logger;
        private readonly ExecutionPerformanceMonitor _performanceMonitor;
        private readonly DependencyAnalyzer _dependencyAnalyzer;
        private readonly ResourceScheduler _resourceScheduler;
        private readonly ExecutionOptimizer _executionOptimizer;

        /// <summary>
        /// Initializes a new instance of the ExecutionPlanGenerator class.
        /// </summary>
        /// <param name="logger">Logger for generator operations</param>
        /// <param name="performanceMonitor">Performance monitor for execution estimation</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        public ExecutionPlanGenerator(ILogger logger, ExecutionPerformanceMonitor performanceMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _dependencyAnalyzer = new DependencyAnalyzer(logger);
            _resourceScheduler = new ResourceScheduler(logger);
            _executionOptimizer = new ExecutionOptimizer(logger);
        }

        /// <summary>
        /// Generates an optimized execution plan for data parallel workloads.
        /// Performs dependency analysis, device selection, workload distribution, and optimization.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data being processed</typeparam>
        /// <param name="kernelName">Name of the kernel to execute</param>
        /// <param name="devices">Available accelerator devices</param>
        /// <param name="inputBuffers">Input data buffers</param>
        /// <param name="outputBuffers">Output data buffers</param>
        /// <param name="options">Data parallelism configuration options</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>An optimized data parallel execution plan</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        /// <exception cref="InvalidOperationException">Thrown when plan generation fails</exception>
        public async ValueTask<DataParallelExecutionPlan<T>> GenerateDataParallelPlanAsync<T>(
            string kernelName,
            IAccelerator[] devices,
            IUnifiedMemoryBuffer<T>[] inputBuffers,
            IUnifiedMemoryBuffer<T>[] outputBuffers,
            DataParallelismOptions options,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(kernelName);
            ArgumentNullException.ThrowIfNull(devices);
            ArgumentNullException.ThrowIfNull(inputBuffers);
            ArgumentNullException.ThrowIfNull(outputBuffers);
            ArgumentNullException.ThrowIfNull(options);

            var stopwatch = Stopwatch.StartNew();
            _logger.LogInfoMessage($"Generating data parallel execution plan for kernel {kernelName} on {devices.Length} devices");

            try
            {
                // 1. Analyze dependencies and constraints
                var dependencyGraph = await _dependencyAnalyzer.AnalyzeDataDependenciesAsync(
                    inputBuffers, outputBuffers, cancellationToken);

                // 2. Select optimal devices based on performance characteristics
                var selectedDevices = await _resourceScheduler.SelectOptimalDevicesAsync(
                    devices, options, cancellationToken);

                // 3. Distribute workload across selected devices
                var workloadDistribution = await _resourceScheduler.DistributeWorkloadAsync(
                    inputBuffers, selectedDevices, options.LoadBalancing, cancellationToken);

                // 4. Create device-specific tasks with proper synchronization
                var deviceTasks = await CreateDataParallelDeviceTasksAsync<T>(
                    kernelName, workloadDistribution, dependencyGraph, cancellationToken);

                // 5. Estimate execution time based on performance history
                var deviceTypes = selectedDevices.Select(d => Enum.Parse<AcceleratorType>(d.DeviceType)).ToArray();
                var totalElements = inputBuffers.Sum(b => (long)b.Length);
                var estimatedExecutionTime = EstimateDataParallelExecutionTime(
                    kernelName, deviceTypes, totalElements);



                var plan = new DataParallelExecutionPlan<T>
                {
                    KernelName = kernelName,
                    Devices = selectedDevices,
                    StrategyType = DotCompute.Core.Execution.Types.ExecutionStrategyType.DataParallel,
                    InputBuffers = inputBuffers,
                    OutputBuffers = outputBuffers,
                    DeviceTasks = deviceTasks,
                    EstimatedExecutionTimeMs = estimatedExecutionTime,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                // 6. Apply execution-specific optimizations
                await _executionOptimizer.OptimizeDataParallelPlanAsync(plan, cancellationToken);

                stopwatch.Stop();
                _logger.LogInfoMessage($"Generated data parallel execution plan in {stopwatch.Elapsed.TotalMilliseconds}ms with {selectedDevices.Length} devices, estimated execution time: {estimatedExecutionTime}ms");

                return plan;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Failed to generate data parallel execution plan for kernel {kernelName}");
                throw;
            }
        }

        /// <summary>
        /// Generates an execution plan for model parallel workloads with layer partitioning.
        /// Handles layer dependency analysis, device assignment, and communication scheduling.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data being processed</typeparam>
        /// <param name="workload">The model parallel workload specification</param>
        /// <param name="devices">Available accelerator devices</param>
        /// <param name="options">Model parallelism configuration options</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>An optimized model parallel execution plan</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        /// <exception cref="InvalidOperationException">Thrown when plan generation fails</exception>
        public async ValueTask<ModelParallelExecutionPlan<T>> GenerateModelParallelPlanAsync<T>(
            ModelParallelWorkload<T> workload,
            IAccelerator[] devices,
            ModelParallelismOptions options,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(workload);
            ArgumentNullException.ThrowIfNull(devices);
            ArgumentNullException.ThrowIfNull(options);

            var stopwatch = Stopwatch.StartNew();
            _logger.LogInfoMessage($"Generating model parallel execution plan for {workload.ModelLayers.Count} layers on {devices.Length} devices");

            try
            {
                // 1. Analyze layer dependencies and memory requirements
                var layerDependencies = await _dependencyAnalyzer.AnalyzeLayerDependenciesAsync(
                    workload.ModelLayers, cancellationToken);

                // 2. Assign layers to devices based on memory and compute requirements
                var layerAssignments = await _resourceScheduler.AssignLayersToDevicesAsync(
                    workload.ModelLayers, devices, options, cancellationToken);

                // 3. Create communication schedule for inter-layer data transfers
                var communicationSchedule = await CreateCommunicationScheduleAsync(
                    workload.ModelLayers, layerAssignments, layerDependencies, cancellationToken);

                // 4. Estimate execution time for the model
                var estimatedExecutionTime = EstimateModelParallelExecutionTime(
                    workload.ModelLayers.Count, layerAssignments.Values.ToArray());



                var plan = new ModelParallelExecutionPlan<T>
                {
                    KernelName = $"ModelParallel_{workload.ModelLayers.Count}Layers",
                    Devices = devices,
                    StrategyType = DotCompute.Core.Execution.Types.ExecutionStrategyType.ModelParallel,
                    ModelLayers = [.. workload.ModelLayers],
                    LayerAssignments = layerAssignments,
                    CommunicationSchedule = communicationSchedule,
                    EstimatedExecutionTimeMs = estimatedExecutionTime,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                // 5. Optimize the model parallel plan
                await _executionOptimizer.OptimizeModelParallelPlanAsync(plan, cancellationToken);

                stopwatch.Stop();
                _logger.LogInfoMessage($"Generated model parallel execution plan in {stopwatch.Elapsed.TotalMilliseconds}ms, estimated execution time: {estimatedExecutionTime}ms");

                return plan;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Failed to generate model parallel execution plan");
                throw;
            }
        }

        /// <summary>
        /// Generates a pipeline execution plan with microbatch scheduling.
        /// Creates pipeline stages, configures microbatching, and optimizes buffer management.
        /// </summary>
        /// <typeparam name="T">The unmanaged type of data being processed</typeparam>
        /// <param name="pipelineDefinition">The pipeline structure and stage definitions</param>
        /// <param name="devices">Available accelerator devices</param>
        /// <param name="options">Pipeline parallelism configuration options</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>An optimized pipeline execution plan</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        /// <exception cref="InvalidOperationException">Thrown when plan generation fails</exception>
        public async ValueTask<PipelineExecutionPlan<T>> GeneratePipelinePlanAsync<T>(
            PipelineDefinition<T> pipelineDefinition,
            IAccelerator[] devices,
            PipelineParallelismOptions options,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(pipelineDefinition);
            ArgumentNullException.ThrowIfNull(devices);
            ArgumentNullException.ThrowIfNull(options);

            var stopwatch = Stopwatch.StartNew();
            _logger.LogInfoMessage($"Generating pipeline execution plan for {pipelineDefinition.Stages.Count} stages with {options.MicrobatchSize} microbatches");

            try
            {
                // 1. Analyze stage dependencies
                var stageDependencies = await _dependencyAnalyzer.AnalyzeStageDependenciesAsync(
                    pipelineDefinition.Stages, cancellationToken);

                // 2. Assign stages to devices
                var stageAssignments = await _resourceScheduler.AssignStagesToDevicesAsync(
                    pipelineDefinition.Stages, devices, options, cancellationToken);

                // 3. Create pipeline stages with buffer management
                var pipelineStages = await CreatePipelineStagesAsync(
                    pipelineDefinition, stageAssignments, stageDependencies, cancellationToken);

                // 4. Configure microbatch settings
                var microbatchConfig = new MicrobatchConfiguration
                {
                    Size = options.MicrobatchSize,
                    Count = Math.Max(1, (int)(pipelineDefinition.InputSpec.Tensors.Sum(t => t.ElementCount) / options.MicrobatchSize)),
                    SchedulingStrategy = MapSchedulingStrategy(options.SchedulingStrategy)
                };

                // 5. Create buffer strategy for efficient memory management
                var bufferStrategy = await CreatePipelineBufferStrategyAsync(
                    pipelineStages, options, cancellationToken);

                var estimatedExecutionTime = EstimatePipelineExecutionTime(
                    pipelineStages.Length, microbatchConfig);



                var plan = new PipelineExecutionPlan<T>
                {
                    KernelName = $"Pipeline_{pipelineDefinition.Stages.Count}Stages",
                    Devices = devices,
                    StrategyType = DotCompute.Core.Execution.Types.ExecutionStrategyType.PipelineParallel,
                    Stages = pipelineStages,
                    MicrobatchConfig = microbatchConfig,
                    BufferStrategy = bufferStrategy,
                    EstimatedExecutionTimeMs = estimatedExecutionTime,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                // 6. Optimize pipeline execution
                await _executionOptimizer.OptimizePipelinePlanAsync(plan, cancellationToken);

                stopwatch.Stop();
                _logger.LogInfoMessage($"Generated pipeline execution plan in {stopwatch.Elapsed.TotalMilliseconds}ms, estimated execution time: {estimatedExecutionTime}ms");

                return plan;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Failed to generate pipeline execution plan");
                throw;
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Creates device-specific tasks for data parallel execution.
        /// </summary>
        private static async ValueTask<DataParallelDeviceTask<T>[]> CreateDataParallelDeviceTasksAsync<T>(
            string kernelName,
            WorkloadDistribution workloadDistribution,
            DependencyGraph dependencyGraph,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var tasks = new List<DataParallelDeviceTask<T>>();
            var compilationTasks = new List<Task<ManagedCompiledKernel>>();

            // Compile kernels in parallel for all devices
            foreach (var assignment in workloadDistribution.DeviceAssignments)
            {
                compilationTasks.Add(Task.FromResult(CompileKernelForDeviceAsync(kernelName, assignment.Device, cancellationToken)));
            }

            var compiledKernels = await Task.WhenAll(compilationTasks);

            // Create device tasks with proper dependency relationships
            for (var i = 0; i < workloadDistribution.DeviceAssignments.Count; i++)
            {
                var assignment = workloadDistribution.DeviceAssignments[i];
                var compiledKernel = compiledKernels[i];

                var deviceTask = new DataParallelDeviceTask<T>
                {
                    Device = assignment.Device,
                    CompiledKernel = compiledKernel,
                    InputBuffers = [.. assignment.InputBuffers.Cast<IUnifiedMemoryBuffer<T>>()],
                    OutputBuffers = [.. assignment.OutputBuffers.Cast<IUnifiedMemoryBuffer<T>>()],
                    StartIndex = assignment.StartIndex,
                    ElementCount = assignment.ElementCount,
                    Dependencies = dependencyGraph.GetDependencies(i)
                };

                tasks.Add(deviceTask);
            }

            return [.. tasks];
        }

        /// <summary>
        /// Creates communication schedule for model parallel execution.
        /// </summary>
        private static async ValueTask<CommunicationSchedule<T>> CreateCommunicationScheduleAsync<T>(
            List<ModelLayer<T>> layers,
            Dictionary<int, IAccelerator> layerAssignments,
            DependencyGraph dependencies,
            CancellationToken cancellationToken) where T : unmanaged
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var operations = new List<CommunicationOperation<T>>();
            var syncPoints = new List<SynchronizationPoint>();
            var operationId = 0;
            var syncId = 0;

            // Create communication operations based on layer dependencies
            var sortedLayers = TopologicalSort(layers, dependencies);

            for (var i = 0; i < sortedLayers.Count - 1; i++)
            {
                var currentLayer = sortedLayers[i];
                var nextLayer = sortedLayers[i + 1];

                var sourceDevice = layerAssignments[currentLayer.LayerId];
                var destDevice = layerAssignments[nextLayer.LayerId];

                // Only create communication if layers are on different devices
                if (!sourceDevice.Equals(destDevice))
                {
                    foreach (var tensor in currentLayer.OutputTensors)
                    {
                        operations.Add(new CommunicationOperation<T>
                        {
                            OperationId = operationId++,
                            SourceDevice = sourceDevice,
                            DestinationDevice = destDevice,
                            Tensor = tensor,
                            OperationType = CommunicationOperationType.PointToPoint,
                            ExecutionOrder = i
                        });
                    }

                    // Add synchronization point after communication
                    syncPoints.Add(new SynchronizationPoint
                    {
                        SyncId = syncId++,
                        ParticipatingDevices = [sourceDevice, destDevice],
                        SyncType = SynchronizationType.Event
                    });
                }
            }

            return new CommunicationSchedule<T>
            {
                Operations = operations,
                SynchronizationPoints = syncPoints
            };
        }

        /// <summary>
        /// Creates pipeline stages with proper compilation and buffer setup.
        /// </summary>
        private static async ValueTask<PipelineStage<T>[]> CreatePipelineStagesAsync<T>(
            PipelineDefinition<T> definition,
            Dictionary<string, IAccelerator> stageAssignments,
            DependencyGraph dependencies,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var stages = new List<PipelineStage<T>>();
            var compilationTasks = new List<Task<(string stageName, ManagedCompiledKernel kernel)>>();

            // Compile kernels for each stage in parallel
            foreach (var stageDef in definition.Stages)
            {
                var device = stageAssignments[stageDef.Name];
                compilationTasks.Add(CompileStageKernelAsync(stageDef.KernelName, stageDef.Name, device, cancellationToken));
            }

            var compiledKernels = await Task.WhenAll(compilationTasks);
            var kernelLookup = compiledKernels.ToDictionary(ck => ck.stageName, ck => ck.kernel);

            // Create pipeline stages
            for (var i = 0; i < definition.Stages.Count; i++)
            {
                var stageDef = definition.Stages[i];
                var device = stageAssignments[stageDef.Name];
                var kernel = kernelLookup[stageDef.Name];

                // Estimate processing time based on stage complexity and device performance
                var estimatedProcessingTime = EstimateStageProcessingTime(
                    stageDef.KernelName, Enum.Parse<AcceleratorType>(device.DeviceType));



                stages.Add(new PipelineStage<T>
                {
                    StageId = i,
                    Name = stageDef.Name,
                    Device = device,
                    Kernel = kernel,
                    InputBuffers = await CreateStageInputBuffersAsync<T>(stageDef, device, cancellationToken),
                    OutputBuffers = await CreateStageOutputBuffersAsync<T>(stageDef, device, cancellationToken),
                    EstimatedProcessingTimeMs = estimatedProcessingTime
                });
            }

            return [.. stages];
        }

        /// <summary>
        /// Creates buffer strategy for pipeline execution.
        /// </summary>
        private static async ValueTask<PipelineBufferStrategy<T>> CreatePipelineBufferStrategyAsync<T>(
            PipelineStage<T>[] stages,
            PipelineParallelismOptions options,
            CancellationToken cancellationToken) where T : unmanaged
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var bufferPool = new BufferPool<T>();

            var doubleBuffering = new DoubleBufferingConfig
            {
                Enabled = options.BufferDepth >= 2,
                SwapStrategy = BufferSwapStrategy.Automatic
            };

            var prefetching = new PrefetchingStrategy
            {
                Enabled = true,
                PrefetchDepth = Math.Min(options.BufferDepth, stages.Length - 1),
                Policy = PrefetchingPolicy.Balanced
            };

            return new PipelineBufferStrategy<T>
            {
                BufferPool = bufferPool,
                DoubleBuffering = doubleBuffering,
                Prefetching = prefetching
            };
        }

        /// <summary>
        /// Compiles a kernel for a specific device.
        /// </summary>
        private static ManagedCompiledKernel CompileKernelForDeviceAsync(string kernelName, IAccelerator device, CancellationToken cancellationToken)
        {
            // Compile kernel for the specific device type with appropriate optimizations
            var compilationOptions = new KernelCompilationOptions
            {
                OptimizationLevel = DotCompute.Abstractions.Types.OptimizationLevel.Balanced,
                TargetArchitecture = device.DeviceType.ToString(),
                EnableAutoVectorization = device.DeviceType.ToString() == nameof(AcceleratorType.CPU),
                GenerateDebugInfo = false
            };

            // Create a mock compiled kernel with realistic properties
            var compiledKernel = new CompiledKernel
            {
                Name = kernelName,
                EntryPoint = "main",
                CompilationTime = TimeSpan.Zero,
                TargetDevice = device.DeviceType.ToString()
            };

            return new ManagedCompiledKernel(kernelName, device, compiledKernel);
        }

        /// <summary>
        /// Compiles a kernel for a pipeline stage.
        /// </summary>
        private static async Task<(string stageName, ManagedCompiledKernel kernel)> CompileStageKernelAsync(string kernelName, string stageName, IAccelerator device, CancellationToken cancellationToken)
        {
            var kernel = new ManagedCompiledKernel(
                kernelName,
                device,
                new CompiledKernel { Name = kernelName });
            await Task.CompletedTask.ConfigureAwait(false);
            return (stageName, kernel);
        }

        /// <summary>
        /// Creates input buffers for a pipeline stage.
        /// </summary>
        private static async ValueTask<IUnifiedMemoryBuffer<T>[]> CreateStageInputBuffersAsync<T>(
            PipelineStageDefinition stageDef, IAccelerator device, CancellationToken cancellationToken) where T : unmanaged
        {
            await Task.CompletedTask.ConfigureAwait(false);

            // Create input buffers based on stage definition and device capabilities
            var buffers = new List<IUnifiedMemoryBuffer<T>>();

            foreach (var inputSpec in stageDef.InputSpecs)
            {
                var bufferSize = inputSpec.ElementCount * Unsafe.SizeOf<T>();
                var memoryOptions = DotCompute.Abstractions.Memory.MemoryOptions.None;

                // Adjust memory options based on device type
                if (device.DeviceType.ToString() == nameof(AcceleratorType.CPU))
                {
                    memoryOptions |= DotCompute.Abstractions.Memory.MemoryOptions.HostVisible;
                }
                else if (device.DeviceType.ToString() == nameof(AcceleratorType.GPU))
                {
                    memoryOptions |= DotCompute.Abstractions.Memory.MemoryOptions.DeviceLocal;
                }

                // Use device's memory manager to allocate buffer
                var buffer = await device.MemoryManager.AllocateAsync<T>(
                    (int)inputSpec.ElementCount, memoryOptions, cancellationToken);

                buffers.Add(buffer);
            }

            return buffers.ToArray();
        }

        /// <summary>
        /// Creates output buffers for a pipeline stage.
        /// </summary>
        private static async ValueTask<IUnifiedMemoryBuffer<T>[]> CreateStageOutputBuffersAsync<T>(
            PipelineStageDefinition stageDef, IAccelerator device, CancellationToken cancellationToken) where T : unmanaged
        {
            await Task.CompletedTask.ConfigureAwait(false);

            // Create output buffers based on stage definition and device capabilities
            var buffers = new List<IUnifiedMemoryBuffer<T>>();

            foreach (var outputSpec in stageDef.OutputSpecs)
            {
                var bufferSize = outputSpec.ElementCount * Unsafe.SizeOf<T>();
                var memoryOptions = DotCompute.Abstractions.Memory.MemoryOptions.None;

                // Adjust memory options based on device type
                if (device.DeviceType.ToString() == nameof(AcceleratorType.CPU))
                {
                    memoryOptions |= DotCompute.Abstractions.Memory.MemoryOptions.HostVisible;
                }
                else if (device.DeviceType.ToString() == nameof(AcceleratorType.GPU))
                {
                    memoryOptions |= DotCompute.Abstractions.Memory.MemoryOptions.DeviceLocal;
                }

                // Use device's memory manager to allocate buffer
                var buffer = await device.MemoryManager.AllocateAsync<T>(
                    (int)outputSpec.ElementCount, memoryOptions, cancellationToken);

                buffers.Add(buffer);
            }

            return buffers.ToArray();
        }

        /// <summary>
        /// Maps pipeline scheduling strategy to microbatch scheduling strategy.
        /// </summary>
        private static MicrobatchSchedulingStrategy MapSchedulingStrategy(PipelineSchedulingStrategy strategy)
        {
            return strategy switch
            {
                PipelineSchedulingStrategy.FillDrain => MicrobatchSchedulingStrategy.Sequential,
                PipelineSchedulingStrategy.OneForwardOneBackward => MicrobatchSchedulingStrategy.OneForwardOneBackward,
                PipelineSchedulingStrategy.Interleaved => MicrobatchSchedulingStrategy.Interleaved,
                _ => MicrobatchSchedulingStrategy.Sequential
            };
        }

        /// <summary>
        /// Performs topological sort on model layers.
        /// </summary>
        private static List<ModelLayer<T>> TopologicalSort<T>(List<ModelLayer<T>> layers, DependencyGraph dependencies) where T : unmanaged
        {
            var sorted = new List<ModelLayer<T>>();
            var visited = new HashSet<int>();
            var visiting = new HashSet<int>();

            foreach (var layer in layers)
            {
                if (!visited.Contains(layer.LayerId))
                {
                    TopologicalSortVisit(layer.LayerId, layers, dependencies, visited, visiting, sorted);
                }
            }

            return sorted;
        }

        /// <summary>
        /// Visits a node during topological sort.
        /// </summary>
        private static void TopologicalSortVisit<T>(int layerId, List<ModelLayer<T>> layers,
            DependencyGraph dependencies, HashSet<int> visited, HashSet<int> visiting, List<ModelLayer<T>> sorted) where T : unmanaged
        {
            if (visiting.Contains(layerId))
            {
                throw new InvalidOperationException("Circular dependency detected in model layers");
            }

            if (visited.Contains(layerId))
            {
                return;
            }

            _ = visiting.Add(layerId);

            var deps = dependencies.GetDependencies(layerId);
            foreach (var dep in deps)
            {
                TopologicalSortVisit(dep, layers, dependencies, visited, visiting, sorted);
            }

            _ = visiting.Remove(layerId);
            _ = visited.Add(layerId);

            var layer = layers.First(l => l.LayerId == layerId);
            sorted.Add(layer);
        }

        /// <summary>
        /// Estimates execution time for data parallel workloads.
        /// </summary>
        private static double EstimateDataParallelExecutionTime(
            string kernelName,
            AcceleratorType[] deviceTypes,
            long totalElements)
        {
            // Base execution time estimation based on kernel complexity and device performance
            var baseTime = kernelName.ToLowerInvariant() switch
            {
                var name when name.Contains("add") => 1.0,
                var name when name.Contains("multiply") => 1.5,
                var name when name.Contains("matrix") => 5.0,
                var name when name.Contains("reduce") => 3.0,
                var name when name.Contains("conv") => 8.0,
                _ => 2.0
            };

            // Adjust for device performance characteristics
            var deviceMultiplier = deviceTypes.Min(dt => dt switch
            {
                AcceleratorType.GPU => 0.1,     // GPUs are ~10x faster for parallel workloads
                AcceleratorType.CPU => 1.0,     // Base performance
                _ => 1.5                       // Conservative estimate for unknown devices
            });

            // Scale by data size (logarithmic scaling for parallel efficiency)
            var dataScaleFactor = Math.Log10(Math.Max(1, totalElements / 1000.0));

            return baseTime * deviceMultiplier * dataScaleFactor;
        }

        /// <summary>
        /// Estimates execution time for model parallel workloads.
        /// </summary>
        private static double EstimateModelParallelExecutionTime(
            int layerCount,
            IAccelerator[] devices)
        {
            // Model parallel execution time depends on layer complexity and communication overhead
            var baseTimePerLayer = 2.0; // Base time per layer in milliseconds
            // Communication overhead for inter-device communication is accounted for in multiplier

            // Adjust for device types and their relative performance
            var avgDevicePerformance = devices.Average(d => d.DeviceType switch
            {
                nameof(AcceleratorType.GPU) => 0.2,     // GPUs are faster for neural network operations
                nameof(AcceleratorType.CPU) => 1.0,     // Base performance
                _ => 1.2                   // Conservative estimate
            });

            // Communication overhead increases with more devices
            var communicationMultiplier = 1.0 + (devices.Length - 1) * 0.1;

            return layerCount * baseTimePerLayer * avgDevicePerformance * communicationMultiplier;
        }

        /// <summary>
        /// Estimates execution time for pipeline parallel workloads.
        /// </summary>
        private static double EstimatePipelineExecutionTime(
            int stageCount,
            MicrobatchConfiguration microbatchConfig)
        {
            // Pipeline execution time depends on stage latency and microbatch scheduling
            var baseTimePerStage = 3.0; // Base time per stage in milliseconds
            var pipelineOverhead = 0.5; // Overhead for pipeline coordination

            // Microbatch scheduling affects overall throughput
            var batchingEfficiency = microbatchConfig.SchedulingStrategy switch
            {
                MicrobatchSchedulingStrategy.Sequential => 1.0,
                MicrobatchSchedulingStrategy.OneForwardOneBackward => 0.8,
                MicrobatchSchedulingStrategy.Interleaved => 0.6,
                _ => 1.0
            };

            // Pipeline parallelism allows overlapping of stages
            var pipelineEfficiency = Math.Max(0.3, 1.0 / stageCount);

            return stageCount * baseTimePerStage * batchingEfficiency * pipelineEfficiency + pipelineOverhead;
        }

        /// <summary>
        /// Estimates processing time for individual pipeline stages.
        /// </summary>
        private static double EstimateStageProcessingTime(
            string kernelName,
            AcceleratorType deviceType)
        {
            // Stage processing time based on kernel complexity and device type
            var baseTime = kernelName.ToLowerInvariant() switch
            {
                var name when name.Contains("linear") => 2.0,
                var name when name.Contains("conv") => 5.0,
                var name when name.Contains("attention") => 8.0,
                var name when name.Contains("norm") => 1.5,
                var name when name.Contains("activate") => 1.0,
                _ => 3.0
            };

            var deviceMultiplier = deviceType switch
            {
                AcceleratorType.GPU => 0.3,     // GPUs excel at neural network operations
                AcceleratorType.CPU => 1.0,     // Base performance
                _ => 1.5                        // Conservative estimate
            };

            return baseTime * deviceMultiplier;
        }

        #endregion
    }
}
