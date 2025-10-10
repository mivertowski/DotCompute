// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
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
    public sealed partial class ExecutionPlanExecutor(ILogger logger, PerformanceMonitor performanceMonitor) : IAsyncDisposable
    {
        // LoggerMessage delegates - Event ID range 23200-23235 for ExecutionPlanExecutor
        private static readonly Action<ILogger, Guid, string, int, Exception?> _logDataParallelStarted =
            LoggerMessage.Define<Guid, string, int>(
                LogLevel.Information,
                new EventId(23200, nameof(LogDataParallelStarted)),
                "Starting data parallel execution {ExecutionId} for kernel {KernelName} on {DeviceCount} devices");

        private static void LogDataParallelStarted(ILogger logger, Guid executionId, string kernelName, int deviceCount)
            => _logDataParallelStarted(logger, executionId, kernelName, deviceCount, null);

        private static readonly Action<ILogger, Guid, double, double, Exception?> _logDataParallelCompleted =
            LoggerMessage.Define<Guid, double, double>(
                LogLevel.Information,
                new EventId(23201, nameof(LogDataParallelCompleted)),
                "Completed data parallel execution {ExecutionId} in {DurationMs}ms with {SuccessRate}% success rate");

        private static void LogDataParallelCompleted(ILogger logger, Guid executionId, double durationMs, double successRate)
            => _logDataParallelCompleted(logger, executionId, durationMs, successRate, null);

        private static readonly Action<ILogger, Guid, Exception?> _logDataParallelFailed =
            LoggerMessage.Define<Guid>(
                LogLevel.Error,
                new EventId(23202, nameof(LogDataParallelFailed)),
                "Failed to execute data parallel plan {ExecutionId}");

        private static void LogDataParallelFailed(ILogger logger, Guid executionId, Exception exception)
            => _logDataParallelFailed(logger, executionId, exception);

        private static readonly Action<ILogger, Guid, int, int, Exception?> _logModelParallelStarted =
            LoggerMessage.Define<Guid, int, int>(
                LogLevel.Information,
                new EventId(23203, nameof(LogModelParallelStarted)),
                "Starting model parallel execution {ExecutionId} for {LayerCount} layers on {DeviceCount} devices");

        private static void LogModelParallelStarted(ILogger logger, Guid executionId, int layerCount, int deviceCount)
            => _logModelParallelStarted(logger, executionId, layerCount, deviceCount, null);

        private static readonly Action<ILogger, Guid, double, Exception?> _logModelParallelCompleted =
            LoggerMessage.Define<Guid, double>(
                LogLevel.Information,
                new EventId(23204, nameof(LogModelParallelCompleted)),
                "Completed model parallel execution {ExecutionId} in {DurationMs}ms");

        private static void LogModelParallelCompleted(ILogger logger, Guid executionId, double durationMs)
            => _logModelParallelCompleted(logger, executionId, durationMs, null);

        private static readonly Action<ILogger, Guid, Exception?> _logModelParallelFailed =
            LoggerMessage.Define<Guid>(
                LogLevel.Error,
                new EventId(23205, nameof(LogModelParallelFailed)),
                "Failed to execute model parallel plan {ExecutionId}");

        private static void LogModelParallelFailed(ILogger logger, Guid executionId, Exception exception)
            => _logModelParallelFailed(logger, executionId, exception);

        private static readonly Action<ILogger, Guid, int, int, Exception?> _logPipelineStarted =
            LoggerMessage.Define<Guid, int, int>(
                LogLevel.Information,
                new EventId(23206, nameof(LogPipelineStarted)),
                "Starting pipeline execution {ExecutionId} with {StageCount} stages and {MicrobatchCount} microbatches");

        private static void LogPipelineStarted(ILogger logger, Guid executionId, int stageCount, int microbatchCount)
            => _logPipelineStarted(logger, executionId, stageCount, microbatchCount, null);

        private static readonly Action<ILogger, Guid, double, Exception?> _logPipelineCompleted =
            LoggerMessage.Define<Guid, double>(
                LogLevel.Information,
                new EventId(23207, nameof(LogPipelineCompleted)),
                "Completed pipeline execution {ExecutionId} in {DurationMs}ms");

        private static void LogPipelineCompleted(ILogger logger, Guid executionId, double durationMs)
            => _logPipelineCompleted(logger, executionId, durationMs, null);

        private static readonly Action<ILogger, Guid, Exception?> _logPipelineFailed =
            LoggerMessage.Define<Guid>(
                LogLevel.Error,
                new EventId(23208, nameof(LogPipelineFailed)),
                "Failed to execute pipeline plan {ExecutionId}");

        private static void LogPipelineFailed(ILogger logger, Guid executionId, Exception exception)
            => _logPipelineFailed(logger, executionId, exception);

        private static readonly Action<ILogger, string, int, Exception?> _logDeviceTaskStarting =
            LoggerMessage.Define<string, int>(
                LogLevel.Trace,
                new EventId(23209, nameof(LogDeviceTaskStarting)),
                "Starting execution on device {DeviceId} for task {TaskIndex}");

        private static void LogDeviceTaskStarting(ILogger logger, string deviceId, int taskIndex)
            => _logDeviceTaskStarting(logger, deviceId, taskIndex, null);

        private static readonly Action<ILogger, int, Exception?> _logDependenciesSatisfied =
            LoggerMessage.Define<int>(
                LogLevel.Trace,
                new EventId(23210, nameof(LogDependenciesSatisfied)),
                "Dependencies satisfied for device task {TaskIndex}");

        private static void LogDependenciesSatisfied(ILogger logger, int taskIndex)
            => _logDependenciesSatisfied(logger, taskIndex, null);

        private static readonly Action<ILogger, string, int, double, Exception?> _logDeviceTaskCompleted =
            LoggerMessage.Define<string, int, double>(
                LogLevel.Trace,
                new EventId(23211, nameof(LogDeviceTaskCompleted)),
                "Completed execution on device {DeviceId} for task {TaskIndex} in {ElapsedMs}ms");

        private static void LogDeviceTaskCompleted(ILogger logger, string deviceId, int taskIndex, double elapsedMs)
            => _logDeviceTaskCompleted(logger, deviceId, taskIndex, elapsedMs, null);

        private static readonly Action<ILogger, string, int, Exception?> _logDeviceTaskFailed =
            LoggerMessage.Define<string, int>(
                LogLevel.Error,
                new EventId(23212, nameof(LogDeviceTaskFailed)),
                "Failed execution on device {DeviceId} for task {TaskIndex}");

        private static void LogDeviceTaskFailed(ILogger logger, string deviceId, int taskIndex, Exception exception)
            => _logDeviceTaskFailed(logger, deviceId, taskIndex, exception);

        private static readonly Action<ILogger, string, string, Exception?> _logLayerExecutionStarting =
            LoggerMessage.Define<string, string>(
                LogLevel.Trace,
                new EventId(23213, nameof(LogLayerExecutionStarting)),
                "Starting execution of layer {LayerId} on device {DeviceId}");

        private static void LogLayerExecutionStarting(ILogger logger, string layerId, string deviceId)
            => _logLayerExecutionStarting(logger, layerId, deviceId, null);

        private static readonly Action<ILogger, string, double, Exception?> _logLayerExecutionCompleted =
            LoggerMessage.Define<string, double>(
                LogLevel.Trace,
                new EventId(23214, nameof(LogLayerExecutionCompleted)),
                "Completed execution of layer {LayerId} in {ElapsedMs}ms");

        private static void LogLayerExecutionCompleted(ILogger logger, string layerId, double elapsedMs)
            => _logLayerExecutionCompleted(logger, layerId, elapsedMs, null);

        private static readonly Action<ILogger, string, Exception?> _logLayerExecutionFailed =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(23215, nameof(LogLayerExecutionFailed)),
                "Failed execution of layer {LayerId}");

        private static void LogLayerExecutionFailed(ILogger logger, string layerId, Exception exception)
            => _logLayerExecutionFailed(logger, layerId, exception);

        private static readonly Action<ILogger, int, int, string, Exception?> _logPipelineExecuting =
            LoggerMessage.Define<int, int, string>(
                LogLevel.Trace,
                new EventId(23216, nameof(LogPipelineExecuting)),
                "Executing pipeline with {StageCount} stages and {MicrobatchCount} microbatches using {Strategy} scheduling");

        private static void LogPipelineExecuting(ILogger logger, int stageCount, int microbatchCount, string strategy)
            => _logPipelineExecuting(logger, stageCount, microbatchCount, strategy, null);

        private static readonly Action<ILogger, int, int, Exception?> _logProcessingMicrobatch =
            LoggerMessage.Define<int, int>(
                LogLevel.Trace,
                new EventId(23217, nameof(LogProcessingMicrobatch)),
                "Processing microbatch {MicrobatchIndex}/{TotalMicrobatches}");

        private static void LogProcessingMicrobatch(ILogger logger, int microbatchIndex, int totalMicrobatches)
            => _logProcessingMicrobatch(logger, microbatchIndex, totalMicrobatches, null);

        private static readonly Action<ILogger, string, int, Exception?> _logExecutingStage =
            LoggerMessage.Define<string, int>(
                LogLevel.Trace,
                new EventId(23218, nameof(LogExecutingStage)),
                "Executing stage {StageId} for microbatch {MicrobatchIndex}");

        private static void LogExecutingStage(ILogger logger, string stageId, int microbatchIndex)
            => _logExecutingStage(logger, stageId, microbatchIndex, null);

        private static readonly Action<ILogger, string, int, Exception?> _logStageExecutionFailed =
            LoggerMessage.Define<string, int>(
                LogLevel.Error,
                new EventId(23219, nameof(LogStageExecutionFailed)),
                "Failed execution of stage {StageId} for microbatch {MicrobatchIndex}");

        private static void LogStageExecutionFailed(ILogger logger, string stageId, int microbatchIndex, Exception exception)
            => _logStageExecutionFailed(logger, stageId, microbatchIndex, exception);

        private static readonly Action<ILogger, Exception?> _logExecutorDisposing =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(23220, nameof(LogExecutorDisposing)),
                "Disposing ExecutionPlanExecutor");

        private static void LogExecutorDisposing(ILogger logger)
            => _logExecutorDisposing(logger, null);

        private static readonly Action<ILogger, Exception?> _logExecutorDisposed =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(23221, nameof(LogExecutorDisposed)),
                "ExecutionPlanExecutor disposed");

        private static void LogExecutorDisposed(ILogger logger)
            => _logExecutorDisposed(logger, null);

        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly ExecutionCoordinator _coordinator = new(logger);
#pragma warning disable CA2213 // Disposable fields should be disposed - Injected dependency, not owned by this class
        private readonly PerformanceMonitor _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
#pragma warning restore CA2213
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

            LogDataParallelStarted(_logger, executionId, plan.KernelName, plan.Devices.Count);

            try
            {
                // Start performance profiling
                await _profiler.StartProfilingAsync(executionId, ExecutionStrategyType.DataParallel, cancellationToken);

                // Track resource usage
                await _resourceTracker.TrackExecutionStartAsync([.. plan.Devices], cancellationToken);

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

                LogDataParallelCompleted(_logger, executionId, stopwatch.Elapsed.TotalMilliseconds, deviceResults.Count(r => r.Success) * 100.0 / deviceResults.Length);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogDataParallelFailed(_logger, executionId, ex);

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

            LogModelParallelStarted(_logger, executionId, plan.ModelLayers.Count, plan.Devices.Count);

            try
            {
                await _profiler.StartProfilingAsync(executionId, ExecutionStrategyType.ModelParallel, cancellationToken);
                await _resourceTracker.TrackExecutionStartAsync([.. plan.Devices], cancellationToken);

                // Execute layers according to dependency order with communication
                var layerResults = await ExecuteModelParallelLayersAsync(plan, executionId, cancellationToken);

                stopwatch.Stop();

                var result = await CreateModelParallelExecutionResultAsync(
                    plan, layerResults, stopwatch.Elapsed, cancellationToken);

                _performanceMonitor.RecordExecution(result);

                var profilingData = await _profiler.StopProfilingAsync(executionId, cancellationToken);

                LogModelParallelCompleted(_logger, executionId, stopwatch.Elapsed.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogModelParallelFailed(_logger, executionId, ex);

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

            LogPipelineStarted(_logger, executionId, plan.Stages.Count, plan.MicrobatchConfig.Count);

            try
            {
                await _profiler.StartProfilingAsync(executionId, ExecutionStrategyType.PipelineParallel, cancellationToken);
                await _resourceTracker.TrackExecutionStartAsync([.. plan.Devices], cancellationToken);

                // Execute pipeline with microbatch scheduling
                var stageResults = await ExecutePipelineStagesAsync(plan, executionId, cancellationToken);

                stopwatch.Stop();

                var result = await CreatePipelineExecutionResultAsync(
                    plan, stageResults, stopwatch.Elapsed, cancellationToken);

                _performanceMonitor.RecordExecution(result);

                var profilingData = await _profiler.StopProfilingAsync(executionId, cancellationToken);

                LogPipelineCompleted(_logger, executionId, stopwatch.Elapsed.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogPipelineFailed(_logger, executionId, ex);

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
                LogDeviceTaskStarting(_logger, deviceId, taskIndex);

                // Wait for dependencies if any
                if (deviceTask.Dependencies.Count > 0)
                {
                    var dependencyEvents = deviceTask.Dependencies
                        .Select(depIndex => _coordinator.CreateEvent($"Device_{depIndex}_Complete_{executionId}"))
                        .ToArray();

                    await _coordinator.WaitForAllEventsAsync(dependencyEvents, cancellationToken);
                    LogDependenciesSatisfied(_logger, taskIndex);
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

                LogDeviceTaskCompleted(_logger, deviceId, taskIndex, stopwatch.Elapsed.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogDeviceTaskFailed(_logger, deviceId, taskIndex, ex);

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
            var executionOrder = GetLayerExecutionOrder<T>([.. layers]);
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
                LogLayerExecutionStarting(_logger, layer.LayerId.ToString(CultureInfo.InvariantCulture), device.Info.Id);

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

                LogLayerExecutionCompleted(_logger, layer.LayerId.ToString(CultureInfo.InvariantCulture), stopwatch.Elapsed.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogLayerExecutionFailed(_logger, layer.LayerId.ToString(CultureInfo.InvariantCulture), ex);

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

            LogPipelineExecuting(_logger, plan.Stages.Count, plan.MicrobatchConfig.Count, microbatchConfig.SchedulingStrategy.ToString());

            // Execute based on scheduling strategy
            switch (microbatchConfig.SchedulingStrategy)
            {
                case MicrobatchSchedulingStrategy.Sequential:
                    results.AddRange(await ExecuteSequentialPipelineAsync<T>([.. stages], microbatchConfig, executionId, cancellationToken));
                    break;
                case MicrobatchSchedulingStrategy.Interleaved:
                    results.AddRange(await ExecuteInterleavedPipelineAsync<T>([.. stages], microbatchConfig, executionId, cancellationToken));
                    break;
                case MicrobatchSchedulingStrategy.OneForwardOneBackward:
                    results.AddRange(await ExecuteOneForwardOneBackwardPipelineAsync<T>([.. stages], microbatchConfig, executionId, cancellationToken));
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
                LogProcessingMicrobatch(_logger, microbatch, microbatchConfig.Count);

                // Process each stage sequentially for this microbatch
                for (var stageIndex = 0; stageIndex < stages.Length; stageIndex++)
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
            for (var stageIndex = 0; stageIndex < stages.Length; stageIndex++)
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
                LogExecutingStage(_logger, stage.StageId.ToString(CultureInfo.InvariantCulture), microbatchIndex);

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
                LogStageExecutionFailed(_logger, stage.StageId.ToString(CultureInfo.InvariantCulture), microbatchIndex, ex);

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

            LogExecutorDisposing(_logger);

            await _coordinator.DisposeAsync();
            await _resourceTracker.DisposeAsync();
            await _profiler.DisposeAsync();

            _disposed = true;
            LogExecutorDisposed(_logger);
        }
    }
}
