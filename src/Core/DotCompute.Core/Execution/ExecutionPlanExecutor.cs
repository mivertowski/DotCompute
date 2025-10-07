// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Execution.Types;
using DotCompute.Core.Execution.Plans;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Core.Execution.Metrics;
namespace DotCompute.Core.Execution
{

    /// <summary>
    /// Executes execution plans with proper synchronization, resource management, and performance monitoring.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the ExecutionPlanExecutor class.
    /// </remarks>
    /// <param name="logger">The logger for monitoring and diagnostics.</param>
    /// <param name="performanceMonitor">The performance monitor for tracking execution metrics.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger or performanceMonitor is null.</exception>
    public sealed class ExecutionPlanExecutor(ILogger logger, PerformanceMonitor performanceMonitor) : IAsyncDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly ExecutionCoordinator _coordinator = new(logger);
        private readonly PerformanceMonitor _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        private readonly ResourceTracker _resourceTracker = new(logger);
        private readonly ExecutionProfiler _profiler = new(logger);
        private bool _disposed;

        /// <summary>
        /// Executes a data parallel execution plan by distributing workload across multiple devices.
        /// </summary>
        /// <typeparam name="T">The unmanaged element type for data processing.</typeparam>
        /// <param name="plan">The data parallel execution plan containing device tasks and synchronization requirements.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A parallel execution result containing performance metrics and device-specific results.</returns>
        /// <exception cref="ArgumentNullException">Thrown when plan is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the executor has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when execution fails on one or more devices.</exception>
        public async ValueTask<ParallelExecutionResult> ExecuteDataParallelPlanAsync<T>(
            DataParallelExecutionPlan<T> plan,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(plan);
            ObjectDisposedException.ThrowIf(_disposed, this);

            var executionId = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInfoMessage($"Starting data parallel execution {executionId} for kernel {plan.KernelName} on {plan.Devices.Count} devices");

            try
            {
                // Start performance profiling
                await _profiler.StartProfilingAsync(executionId, ExecutionStrategyType.DataParallel, cancellationToken);

                // Track resource usage
                await _resourceTracker.TrackExecutionStartAsync(plan.Devices, cancellationToken);

                // Execute device tasks in parallel with proper synchronization
                var deviceResults = await ExecuteDataParallelDeviceTasksAsync(plan, executionId, cancellationToken);

                // Wait for all devices to complete
                await _coordinator.WaitForAllEventsAsync(
                    [.. deviceResults.Select(r => r.CompletionEvent)],
                    cancellationToken);

                stopwatch.Stop();

                // Collect results and create execution result
                var result = await CreateDataParallelExecutionResultAsync(
                    plan, deviceResults, stopwatch.Elapsed, cancellationToken);

                // Record performance metrics
                _performanceMonitor.RecordExecution(result);

                // Stop profiling and collect detailed metrics
                var profilingData = await _profiler.StopProfilingAsync(executionId, cancellationToken);

                _logger.LogInfoMessage($"Completed data parallel execution {executionId} in {stopwatch.Elapsed.TotalMilliseconds}ms with {deviceResults.Count(r => r.Success) * 100.0 / deviceResults.Length}% success rate");

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogErrorMessage(ex, $"Failed to execute data parallel plan {executionId}");

                return new ParallelExecutionResult
                {
                    Success = false,
                    TotalExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    Strategy = ExecutionStrategyType.DataParallel,
                    DeviceResults = [],
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                await _resourceTracker.TrackExecutionEndAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Executes a model parallel execution plan.
        /// </summary>
        public async ValueTask<ParallelExecutionResult> ExecuteModelParallelPlanAsync<T>(
            ModelParallelExecutionPlan<T> plan,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(plan);
            ObjectDisposedException.ThrowIf(_disposed, this);

            var executionId = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInfoMessage($"Starting model parallel execution {executionId} for {plan.ModelLayers.Count} layers on {plan.Devices.Count} devices");

            try
            {
                await _profiler.StartProfilingAsync(executionId, ExecutionStrategyType.ModelParallel, cancellationToken);
                await _resourceTracker.TrackExecutionStartAsync(plan.Devices, cancellationToken);

                // Execute layers according to dependency order with communication
                var layerResults = await ExecuteModelParallelLayersAsync(plan, executionId, cancellationToken);

                stopwatch.Stop();

                var result = await CreateModelParallelExecutionResultAsync(
                    plan, layerResults, stopwatch.Elapsed, cancellationToken);

                _performanceMonitor.RecordExecution(result);

                var profilingData = await _profiler.StopProfilingAsync(executionId, cancellationToken);

                _logger.LogInfoMessage($"Completed model parallel execution {executionId} in {stopwatch.Elapsed.TotalMilliseconds}ms");

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogErrorMessage(ex, $"Failed to execute model parallel plan {executionId}");

                return new ParallelExecutionResult
                {
                    Success = false,
                    TotalExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    Strategy = ExecutionStrategyType.ModelParallel,
                    DeviceResults = [],
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                await _resourceTracker.TrackExecutionEndAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Executes a pipeline execution plan.
        /// </summary>
        public async ValueTask<ParallelExecutionResult> ExecutePipelinePlanAsync<T>(
            PipelineExecutionPlan<T> plan,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var executionId = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInfoMessage($"Starting pipeline execution {executionId} with {plan.Stages.Count} stages and {plan.MicrobatchConfig.Count} microbatches");

            try
            {
                await _profiler.StartProfilingAsync(executionId, ExecutionStrategyType.PipelineParallel, cancellationToken);
                await _resourceTracker.TrackExecutionStartAsync(plan.Devices, cancellationToken);

                // Execute pipeline with microbatch scheduling
                var stageResults = await ExecutePipelineStagesAsync(plan, executionId, cancellationToken);

                stopwatch.Stop();

                var result = await CreatePipelineExecutionResultAsync(
                    plan, stageResults, stopwatch.Elapsed, cancellationToken);

                _performanceMonitor.RecordExecution(result);

                var profilingData = await _profiler.StopProfilingAsync(executionId, cancellationToken);

                _logger.LogInfoMessage($"Completed pipeline execution {executionId} in {stopwatch.Elapsed.TotalMilliseconds}ms");

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogErrorMessage(ex, $"Failed to execute pipeline plan {executionId}");

                return new ParallelExecutionResult
                {
                    Success = false,
                    TotalExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    Strategy = ExecutionStrategyType.PipelineParallel,
                    DeviceResults = [],
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                await _resourceTracker.TrackExecutionEndAsync(cancellationToken);
            }
        }

        #region Private Execution Methods

        private async ValueTask<DeviceTaskResult[]> ExecuteDataParallelDeviceTasksAsync<T>(
            DataParallelExecutionPlan<T> plan,
            Guid executionId,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var deviceTasks = plan.DeviceTasks;
            var results = new DeviceTaskResult[deviceTasks.Count];
            var executionTasks = new List<Task<DeviceTaskResult>>();

            // Create completion events for synchronization
            var completionEvents = deviceTasks.Select((_, i) =>
                _coordinator.CreateEvent($"Device_{i}_Complete_{executionId}")).ToArray();

            // Start all device tasks
            for (var i = 0; i < deviceTasks.Count; i++)
            {
                var taskIndex = i;
                var deviceTask = deviceTasks[i];
                var completionEvent = completionEvents[i];

                var task = ExecuteDeviceTaskAsync(deviceTask, taskIndex, completionEvent, executionId, cancellationToken);
                executionTasks.Add(task);
            }

            // Wait for all tasks to complete
            var taskResults = await Task.WhenAll(executionTasks);

            return taskResults;
        }

        private async Task<DeviceTaskResult> ExecuteDeviceTaskAsync<T>(
            DataParallelDeviceTask<T> deviceTask,
            int taskIndex,
            ExecutionEvent completionEvent,
            Guid executionId,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var stopwatch = Stopwatch.StartNew();
            var deviceId = deviceTask.Device.Info.Id;

            try
            {
                _logger.LogTrace("Starting execution on device {DeviceId} for task {TaskIndex}", deviceId, taskIndex);

                // Wait for dependencies if any
                if (deviceTask.Dependencies.Count > 0)
                {
                    var dependencyEvents = deviceTask.Dependencies
                        .Select(depIndex => _coordinator.CreateEvent($"Device_{depIndex}_Complete_{executionId}"))
                        .ToArray();

                    await _coordinator.WaitForAllEventsAsync(dependencyEvents, cancellationToken);
                    _logger.LogTrace("Dependencies satisfied for device task {TaskIndex}", taskIndex);
                }

                // Execute the kernel
                var kernelArgs = CreateKernelArguments(deviceTask);
                await deviceTask.CompiledKernel.Kernel.ExecuteAsync(kernelArgs, cancellationToken);

                // Synchronize device
                await deviceTask.Device.SynchronizeAsync(cancellationToken);

                stopwatch.Stop();

                // Record kernel-specific performance
                _performanceMonitor.RecordKernelExecution(
                    deviceTask.CompiledKernel.Name,
                    deviceId,
                    stopwatch.Elapsed.TotalMilliseconds,
                    CalculateThroughput(deviceTask.ElementCount, stopwatch.Elapsed.TotalMilliseconds));

                // Signal completion
                await _coordinator.SignalEventAsync(completionEvent, cancellationToken);

                var result = new DeviceTaskResult
                {
                    TaskIndex = taskIndex,
                    DeviceId = deviceId,
                    Success = true,
                    ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    ElementsProcessed = deviceTask.ElementCount,
                    CompletionEvent = completionEvent,
                    ThroughputGFLOPS = CalculateThroughput(deviceTask.ElementCount, stopwatch.Elapsed.TotalMilliseconds),
                    MemoryBandwidthGBps = CalculateMemoryBandwidth(deviceTask, stopwatch.Elapsed.TotalMilliseconds)
                };

                _logger.LogTrace("Completed execution on device {DeviceId} for task {TaskIndex} in {ElapsedMs:F2}ms",
                    deviceId, taskIndex, stopwatch.Elapsed.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogErrorMessage(ex, $"Failed execution on device {deviceId} for task {taskIndex}");

                // Signal completion even on failure to avoid deadlocks
                await _coordinator.SignalEventAsync(completionEvent, CancellationToken.None);

                return new DeviceTaskResult
                {
                    TaskIndex = taskIndex,
                    DeviceId = deviceId,
                    Success = false,
                    ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    ElementsProcessed = 0,
                    CompletionEvent = completionEvent,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async ValueTask<LayerExecutionResult[]> ExecuteModelParallelLayersAsync<T>(
            ModelParallelExecutionPlan<T> plan,
            Guid executionId,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var layers = plan.ModelLayers;
            var layerAssignments = plan.LayerAssignments;
            var communicationSchedule = plan.CommunicationSchedule;
            var results = new LayerExecutionResult[layers.Count];

            // Execute communication operations and layers according to schedule
            var executionOrder = GetLayerExecutionOrder(layers);
            var layerEvents = new Dictionary<int, ExecutionEvent>();

            // Create events for each layer
            foreach (var layer in layers)
            {
                layerEvents[layer.LayerId] = _coordinator.CreateEvent($"Layer_{layer.LayerId}_Complete_{executionId}");
            }

            // Execute layers in dependency order
            var layerTasks = new List<Task<LayerExecutionResult>>();

            foreach (var layerId in executionOrder)
            {
                var layer = layers.First(l => l.LayerId == layerId);
                var device = layerAssignments[layerId];
                var completionEvent = layerEvents[layerId];

                var task = ExecuteModelLayerAsync(layer, device, completionEvent, layerEvents, executionId, cancellationToken);
                layerTasks.Add(task);
            }

            var layerResults = await Task.WhenAll(layerTasks);
            return layerResults;
        }

        private async Task<LayerExecutionResult> ExecuteModelLayerAsync<T>(
            ModelLayer<T> layer,
            IAccelerator device,
            ExecutionEvent completionEvent,
            Dictionary<int, ExecutionEvent> allLayerEvents,
            Guid executionId,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogTrace("Starting execution of layer {LayerId} on device {DeviceId}", layer.LayerId, device.Info.Id);

                // Wait for dependencies
                if (layer.Dependencies.Count > 0)
                {
                    var dependencyEvents = layer.Dependencies
                        .Where(allLayerEvents.ContainsKey)
                        .Select(depId => allLayerEvents[depId])
                        .ToArray();

                    if (dependencyEvents.Length > 0)
                    {
                        await _coordinator.WaitForAllEventsAsync(dependencyEvents, cancellationToken);
                    }
                }

                // Execute layer kernel
                var kernelArgs = CreateLayerKernelArguments(layer);
                await layer.Kernel.Kernel.ExecuteAsync(kernelArgs, cancellationToken);
                await device.SynchronizeAsync(cancellationToken);

                stopwatch.Stop();

                // Signal completion
                await _coordinator.SignalEventAsync(completionEvent, cancellationToken);

                var result = new LayerExecutionResult
                {
                    LayerId = layer.LayerId,
                    DeviceId = device.Info.Id,
                    Success = true,
                    ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    ComputeFLOPS = layer.ComputeRequirementFLOPS,
                    MemoryUsageBytes = layer.MemoryRequirementBytes
                };

                _logger.LogTrace("Completed execution of layer {LayerId} in {ElapsedMs:F2}ms",
                    layer.LayerId, stopwatch.Elapsed.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogErrorMessage(ex, $"Failed execution of layer {layer.LayerId}");

                await _coordinator.SignalEventAsync(completionEvent, CancellationToken.None);

                return new LayerExecutionResult
                {
                    LayerId = layer.LayerId,
                    DeviceId = device.Info.Id,
                    Success = false,
                    ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async ValueTask<StageExecutionResult[]> ExecutePipelineStagesAsync<T>(
            PipelineExecutionPlan<T> plan,
            Guid executionId,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var stages = plan.Stages;
            var microbatchConfig = plan.MicrobatchConfig;
            var results = new List<StageExecutionResult>();

            _logger.LogTrace("Executing pipeline with {StageCount} stages and {MicrobatchCount} microbatches using {Strategy} scheduling",
                stages.Count, microbatchConfig.Count, microbatchConfig.SchedulingStrategy);

            // Execute based on scheduling strategy
            switch (microbatchConfig.SchedulingStrategy)
            {
                case MicrobatchSchedulingStrategy.Sequential:
                    results.AddRange(await ExecuteSequentialPipelineAsync(stages, microbatchConfig, executionId, cancellationToken));
                    break;
                case MicrobatchSchedulingStrategy.Interleaved:
                    results.AddRange(await ExecuteInterleavedPipelineAsync(stages, microbatchConfig, executionId, cancellationToken));
                    break;
                case MicrobatchSchedulingStrategy.OneForwardOneBackward:
                    results.AddRange(await ExecuteOneForwardOneBackwardPipelineAsync(stages, microbatchConfig, executionId, cancellationToken));
                    break;
            }

            return [.. results];
        }

        private async ValueTask<StageExecutionResult[]> ExecuteSequentialPipelineAsync<T>(
            PipelineStage<T>[] stages,
            MicrobatchConfiguration microbatchConfig,
            Guid executionId,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var results = new List<StageExecutionResult>();

            for (var microbatch = 0; microbatch < microbatchConfig.Count; microbatch++)
            {
                _logger.LogTrace("Processing microbatch {MicrobatchIndex}/{TotalMicrobatches}",
                    microbatch + 1, microbatchConfig.Count);

                // Process each stage sequentially for this microbatch
                for (var stageIndex = 0; stageIndex < stages.Count; stageIndex++)
                {
                    var stage = stages[stageIndex];
                    var result = await ExecutePipelineStageAsync(stage, microbatch, executionId, cancellationToken);
                    results.Add(result);
                }
            }

            return [.. results];
        }

        private async ValueTask<StageExecutionResult[]> ExecuteInterleavedPipelineAsync<T>(
            PipelineStage<T>[] stages,
            MicrobatchConfiguration microbatchConfig,
            Guid executionId,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var results = new ConcurrentBag<StageExecutionResult>();
            var stageTasks = new List<Task>();

            // Start all stages in parallel, each processing their assigned microbatches
            for (var stageIndex = 0; stageIndex < stages.Count; stageIndex++)
            {
                var stage = stages[stageIndex];
                var localStageIndex = stageIndex;

                var stageTask = Task.Run(async () =>
                {
                    for (var microbatch = 0; microbatch < microbatchConfig.Count; microbatch++)
                    {
                        // Wait for previous stage to complete this microbatch
                        if (localStageIndex > 0)
                        {
                            var dependencyEvent = _coordinator.CreateEvent($"Stage_{localStageIndex - 1}_Microbatch_{microbatch}_Complete_{executionId}");
                            await _coordinator.WaitForEventAsync(dependencyEvent, cancellationToken);
                        }

                        var result = await ExecutePipelineStageAsync(stage, microbatch, executionId, cancellationToken);
                        results.Add(result);

                        // Signal completion for this stage and microbatch
                        var completionEvent = _coordinator.CreateEvent($"Stage_{localStageIndex}_Microbatch_{microbatch}_Complete_{executionId}");
                        await _coordinator.SignalEventAsync(completionEvent, cancellationToken);
                    }
                }, cancellationToken);

                stageTasks.Add(stageTask);
            }

            await Task.WhenAll(stageTasks);
            return [.. results];
        }

        private async ValueTask<StageExecutionResult[]> ExecuteOneForwardOneBackwardPipelineAsync<T>(
            PipelineStage<T>[] stages,
            MicrobatchConfiguration microbatchConfig,
            Guid executionId,
            CancellationToken cancellationToken) where T : unmanaged
            // 1F1B scheduling is more complex and typically used for training
            // For this implementation, we'll use a simplified approach

            => await ExecuteInterleavedPipelineAsync(stages, microbatchConfig, executionId, cancellationToken);

        private async Task<StageExecutionResult> ExecutePipelineStageAsync<T>(
        PipelineStage<T> stage,
        int microbatchIndex,
        Guid executionId,
        CancellationToken cancellationToken) where T : unmanaged
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogTrace("Executing stage {StageId} for microbatch {MicrobatchIndex}",
                    stage.StageId, microbatchIndex);

                // Execute stage kernel for this microbatch
                var kernelArgs = CreateStageKernelArguments(stage, microbatchIndex);
                await stage.Kernel.Kernel.ExecuteAsync(kernelArgs, cancellationToken);
                await stage.Device.SynchronizeAsync(cancellationToken);

                stopwatch.Stop();

                return new StageExecutionResult
                {
                    StageId = stage.StageId,
                    MicrobatchIndex = microbatchIndex,
                    DeviceId = stage.Device.Info.Id,
                    Success = true,
                    ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    StageName = stage.Name
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogErrorMessage(ex, $"Failed execution of stage {stage.StageId} for microbatch {microbatchIndex}");

                return new StageExecutionResult
                {
                    StageId = stage.StageId,
                    MicrobatchIndex = microbatchIndex,
                    DeviceId = stage.Device.Info.Id,
                    Success = false,
                    ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    StageName = stage.Name,
                    ErrorMessage = ex.Message
                };
            }
        }

        #endregion

        #region Result Creation Methods

        private static async ValueTask<ParallelExecutionResult> CreateDataParallelExecutionResultAsync<T>(
            DataParallelExecutionPlan<T> plan,
            DeviceTaskResult[] deviceResults,
            TimeSpan totalElapsed,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var successfulResults = deviceResults.Where(r => r.Success).ToArray();
            var success = successfulResults.Length == deviceResults.Length;

            var deviceExecutionResults = deviceResults.Select(r => new DeviceExecutionResult
            {
                DeviceId = r.DeviceId,
                Success = r.Success,
                ExecutionTimeMs = r.ExecutionTimeMs,
                ElementsProcessed = r.ElementsProcessed,
                ThroughputGFLOPS = r.ThroughputGFLOPS,
                MemoryBandwidthGBps = r.MemoryBandwidthGBps,
                ErrorMessage = r.ErrorMessage
            }).ToArray();

            var totalThroughput = successfulResults.Sum(r => r.ThroughputGFLOPS);
            var avgMemoryBandwidth = successfulResults.Length > 0 ? successfulResults.Average(r => r.MemoryBandwidthGBps) : 0;
            var efficiency = CalculateParallelEfficiency(deviceResults, totalElapsed.TotalMilliseconds);

            await Task.CompletedTask.ConfigureAwait(false);
            return new ParallelExecutionResult
            {
                Success = success,
                TotalExecutionTimeMs = totalElapsed.TotalMilliseconds,
                Strategy = ExecutionStrategyType.DataParallel,
                DeviceResults = deviceExecutionResults,
                ThroughputGFLOPS = totalThroughput,
                MemoryBandwidthGBps = avgMemoryBandwidth,
                EfficiencyPercentage = efficiency,
                ErrorMessage = success ? null : string.Join("; ", deviceResults.Where(r => !r.Success).Select(r => r.ErrorMessage))
            };
        }

        private static async ValueTask<ParallelExecutionResult> CreateModelParallelExecutionResultAsync<T>(
            ModelParallelExecutionPlan<T> plan,
            LayerExecutionResult[] layerResults,
            TimeSpan totalElapsed,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var successfulResults = layerResults.Where(r => r.Success).ToArray();
            var success = successfulResults.Length == layerResults.Length;

            // Group results by device
            var deviceGroups = layerResults.GroupBy(r => r.DeviceId).ToArray();
            var deviceExecutionResults = deviceGroups.Select(g => new DeviceExecutionResult
            {
                DeviceId = g.Key,
                Success = g.All(r => r.Success),
                ExecutionTimeMs = g.Sum(r => r.ExecutionTimeMs),
                ElementsProcessed = g.Count(),
                ThroughputGFLOPS = g.Where(r => r.Success).Sum(r => r.ComputeFLOPS / 1e9),
                MemoryBandwidthGBps = EstimateMemoryBandwidth(g.Where(r => r.Success).Sum(r => r.MemoryUsageBytes), g.Sum(r => r.ExecutionTimeMs)),
                ErrorMessage = success ? null : string.Join("; ", g.Where(r => !r.Success).Select(r => r.ErrorMessage))
            }).ToArray();

            var totalThroughput = successfulResults.Sum(r => r.ComputeFLOPS / 1e9);
            var avgMemoryBandwidth = deviceExecutionResults.Length > 0 ? deviceExecutionResults.Average(r => r.MemoryBandwidthGBps) : 0;
            var efficiency = CalculateModelParallelEfficiency(layerResults, plan.ModelLayers.Count);

            await Task.CompletedTask.ConfigureAwait(false);
            return new ParallelExecutionResult
            {
                Success = success,
                TotalExecutionTimeMs = totalElapsed.TotalMilliseconds,
                Strategy = ExecutionStrategyType.ModelParallel,
                DeviceResults = deviceExecutionResults,
                ThroughputGFLOPS = totalThroughput,
                MemoryBandwidthGBps = avgMemoryBandwidth,
                EfficiencyPercentage = efficiency
            };
        }

        private static async ValueTask<ParallelExecutionResult> CreatePipelineExecutionResultAsync<T>(
            PipelineExecutionPlan<T> plan,
            StageExecutionResult[] stageResults,
            TimeSpan totalElapsed,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var successfulResults = stageResults.Where(r => r.Success).ToArray();
            var success = successfulResults.Length == stageResults.Length;

            // Group results by device
            var deviceGroups = stageResults.GroupBy(r => r.DeviceId).ToArray();
            var deviceExecutionResults = deviceGroups.Select(g => new DeviceExecutionResult
            {
                DeviceId = g.Key,
                Success = g.All(r => r.Success),
                ExecutionTimeMs = g.Sum(r => r.ExecutionTimeMs),
                ElementsProcessed = g.Count(),
                ThroughputGFLOPS = EstimatePipelineThroughput([.. g], plan.MicrobatchConfig),
                MemoryBandwidthGBps = EstimatePipelineMemoryBandwidth([.. g]),
                ErrorMessage = success ? null : string.Join("; ", g.Where(r => !r.Success).Select(r => r.ErrorMessage))
            }).ToArray();

            var efficiency = CalculatePipelineEfficiency(stageResults, plan.Stages.Count, plan.MicrobatchConfig.Count);

            await Task.CompletedTask.ConfigureAwait(false);
            return new ParallelExecutionResult
            {
                Success = success,
                TotalExecutionTimeMs = totalElapsed.TotalMilliseconds,
                Strategy = ExecutionStrategyType.PipelineParallel,
                DeviceResults = deviceExecutionResults,
                ThroughputGFLOPS = deviceExecutionResults.Sum(r => r.ThroughputGFLOPS),
                MemoryBandwidthGBps = deviceExecutionResults.Length > 0 ? deviceExecutionResults.Average(r => r.MemoryBandwidthGBps) : 0,
                EfficiencyPercentage = efficiency
            };
        }

        #endregion

        #region Helper Methods

        private static KernelArguments CreateKernelArguments<T>(DataParallelDeviceTask<T> deviceTask) where T : unmanaged
        {
            // Create kernel arguments from device task inputs and outputs
            var args = new KernelArguments();

            foreach (var buffer in deviceTask.InputBuffers)
            {
                args.Add(buffer);
            }
            foreach (var buffer in deviceTask.OutputBuffers)
            {
                args.Add(buffer);
            }
            args.Add(deviceTask.ElementCount);

            return args;
        }

        private static KernelArguments CreateLayerKernelArguments<T>(ModelLayer<T> layer) where T : unmanaged
        {
            // Create kernel arguments from layer inputs and outputs
            var args = new KernelArguments();

            foreach (var tensor in layer.InputTensors)
            {
                args.Add(tensor.Buffer ?? new object()); // Fallback for null buffers
            }
            foreach (var tensor in layer.OutputTensors)
            {
                args.Add(tensor.Buffer ?? new object());
            }

            return args;
        }

        private static KernelArguments CreateStageKernelArguments<T>(PipelineStage<T> stage, int microbatchIndex) where T : unmanaged
        {
            // Create kernel arguments from stage inputs and outputs for specific microbatch
            var args = new KernelArguments();

            foreach (var buffer in stage.InputBuffers)
            {
                args.Add(buffer);
            }
            foreach (var buffer in stage.OutputBuffers)
            {
                args.Add(buffer);
            }
            args.Add(microbatchIndex);

            return args;
        }

        private static double CalculateThroughput(int elementCount, double executionTimeMs)
        {
            if (executionTimeMs <= 0)
            {
                return 0;
            }

            // Simple throughput calculation: operations per second converted to GFLOPS
            var opsPerSecond = (elementCount / executionTimeMs) * 1000.0;
            return opsPerSecond / 1e9; // Convert to GFLOPS
        }

        private static double CalculateMemoryBandwidth<T>(DataParallelDeviceTask<T> deviceTask, double executionTimeMs) where T : unmanaged
        {
            if (executionTimeMs <= 0)
            {
                return 0;
            }

            var elementSize = global::System.Runtime.InteropServices.Marshal.SizeOf<T>();
            var totalBytes = (deviceTask.InputBuffers.Count + deviceTask.OutputBuffers.Count) * deviceTask.ElementCount * elementSize;
            var bytesPerSecond = (totalBytes / executionTimeMs) * 1000.0;

            return bytesPerSecond / (1024.0 * 1024.0 * 1024.0); // Convert to GB/s
        }

        private static double CalculateParallelEfficiency(DeviceTaskResult[] deviceResults, double totalExecutionTimeMs)
        {
            if (deviceResults.Length == 0 || totalExecutionTimeMs <= 0)
            {
                return 0;
            }

            var successfulResults = deviceResults.Where(r => r.Success).ToArray();
            if (successfulResults.Length == 0)
            {
                return 0;
            }

            // Theoretical best time would be max individual time
            var maxDeviceTime = successfulResults.Max(r => r.ExecutionTimeMs);

            // Efficiency is how close we are to ideal parallel execution
            return Math.Min(100, (maxDeviceTime / totalExecutionTimeMs) * 100);
        }

        private static double CalculateModelParallelEfficiency(LayerExecutionResult[] layerResults, int totalLayers)
        {
            if (layerResults.Length == 0 || totalLayers == 0)
            {
                return 0;
            }

            var successfulResults = layerResults.Where(r => r.Success).ToArray();
            if (successfulResults.Length == 0)
            {
                return 0;
            }

            // Simple efficiency based on successful layer execution rate
            return (successfulResults.Length * 100.0) / totalLayers;
        }

        private static double CalculatePipelineEfficiency(StageExecutionResult[] stageResults, int stageCount, int microbatchCount)
        {
            if (stageResults.Length == 0 || stageCount == 0 || microbatchCount == 0)
            {
                return 0;
            }

            var successfulResults = stageResults.Where(r => r.Success).ToArray();
            var expectedResults = stageCount * microbatchCount;

            return (successfulResults.Length * 100.0) / expectedResults;
        }

        private static double EstimateMemoryBandwidth(long totalBytes, double totalTimeMs)
        {
            if (totalTimeMs <= 0)
            {
                return 0;
            }

            var bytesPerSecond = (totalBytes / totalTimeMs) * 1000.0;
            return bytesPerSecond / (1024.0 * 1024.0 * 1024.0); // Convert to GB/s
        }

        private static double EstimatePipelineThroughput(StageExecutionResult[] stageResults, MicrobatchConfiguration microbatchConfig)
        {
            if (stageResults.Length == 0)
            {
                return 0;
            }

            // Estimate throughput based on microbatch processing rate
            var avgStageTime = stageResults.Where(r => r.Success).Average(r => r.ExecutionTimeMs);
            if (avgStageTime <= 0)
            {
                return 0;
            }

            var microbatchesPerSecond = 1000.0 / avgStageTime;
            var operationsPerMicrobatch = microbatchConfig.Size * 100.0; // Estimated operations

            return (microbatchesPerSecond * operationsPerMicrobatch) / 1e9; // Convert to GFLOPS
        }

        private static double EstimatePipelineMemoryBandwidth(StageExecutionResult[] stageResults)
            // Simplified memory bandwidth estimation for pipeline stages


            => 10.0; // GB/s - placeholder value

        private static List<int> GetLayerExecutionOrder<T>(ModelLayer<T>[] layers) where T : unmanaged
        {
            // Simple topological sort based on layer dependencies
            var visited = new HashSet<int>();
            var result = new List<int>();

            foreach (var layer in layers)
            {
                if (!visited.Contains(layer.LayerId))
                {
                    VisitLayer(layer.LayerId, layers, visited, result);
                }
            }

            return result;
        }

        private static void VisitLayer<T>(int layerId, ModelLayer<T>[] layers, HashSet<int> visited, List<int> result) where T : unmanaged
        {
            if (visited.Contains(layerId))
            {
                return;
            }

            _ = visited.Add(layerId);

            var layer = layers.FirstOrDefault(l => l.LayerId == layerId);
            if (layer != null)
            {
                foreach (var depId in layer.Dependencies)
                {
                    VisitLayer(depId, layers, visited, result);
                }
            }

            result.Add(layerId);
        }
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        #endregion

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _logger.LogInfoMessage("Disposing ExecutionPlanExecutor");

            await _coordinator.DisposeAsync();
            await _resourceTracker.DisposeAsync();
            await _profiler.DisposeAsync();

            _disposed = true;
            _logger.LogInfoMessage("ExecutionPlanExecutor disposed");
        }
    }
}
