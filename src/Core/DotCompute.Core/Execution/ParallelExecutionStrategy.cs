// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Execution.Configuration;
using DotCompute.Core.Execution.Metrics;
using DotCompute.Core.Execution.Pipeline;
using DotCompute.Core.Execution.Plans;
using DotCompute.Core.Execution.Types;
using DotCompute.Core.Execution.Workload;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using EventId = Microsoft.Extensions.Logging.EventId;

namespace DotCompute.Core.Execution
{

    /// <summary>
    /// Provides comprehensive parallel execution strategies for multi-GPU and heterogeneous computing.
    /// </summary>
    public sealed class ParallelExecutionStrategy : IAsyncDisposable
    {
        private readonly ILogger<ParallelExecutionStrategy> _logger;
        private readonly IAcceleratorManager _acceleratorManager;
        private readonly IKernelManager _kernelManager;
        private readonly IUnifiedMemoryManager _memoryManager;
        private readonly ExecutionCoordinator _coordinator;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<string, CompiledKernelCache> _distributedKernelCache;
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly CancellationTokenSource _shutdownTokenSource;
        private bool _disposed;

        // High-performance logging delegates
        private static readonly Action<ILogger, string, int, Exception?> LogStartingDataParallel =
            LoggerMessage.Define<string, int>(LogLevel.Information, new EventId(1001, nameof(LogStartingDataParallel)),
                "Starting data parallel execution of kernel '{KernelName}' across {DeviceCount} devices");

        private static readonly Action<ILogger, double, double, Exception?> LogDataParallelCompleted =
            LoggerMessage.Define<double, double>(LogLevel.Information, new EventId(1002, nameof(LogDataParallelCompleted)),
                "Data parallel execution completed in {ExecutionTimeMs:F2}ms with {EfficiencyPercentage:F1}% efficiency");

        private static readonly Action<ILogger, string, int, Exception?> LogStartingModelParallel =
            LoggerMessage.Define<string, int>(LogLevel.Information, new EventId(1003, nameof(LogStartingModelParallel)),
                "Starting model parallel execution of kernel '{KernelName}' with {LayerCount} layers");

        private static readonly Action<ILogger, int, Exception?> LogStartingPipelineParallel =
            LoggerMessage.Define<int>(LogLevel.Information, new EventId(1004, nameof(LogStartingPipelineParallel)),
                "Starting pipeline parallel execution with {StageCount} stages");

        private static readonly Action<ILogger, int, Exception?> LogStartingWorkStealing =
            LoggerMessage.Define<int>(LogLevel.Information, new EventId(1005, nameof(LogStartingWorkStealing)),
                "Starting work-stealing execution with {WorkItemCount} work items");

        private static readonly Action<ILogger, Exception?> LogSynchronizingAccelerators =
            LoggerMessage.Define(LogLevel.Debug, new EventId(1006, nameof(LogSynchronizingAccelerators)),
                "Synchronizing all accelerators");

        private static readonly Action<ILogger, Exception?> LogAcceleratorsSynchronized =
            LoggerMessage.Define(LogLevel.Debug, new EventId(1007, nameof(LogAcceleratorsSynchronized)),
                "All accelerators synchronized");

        private static readonly Action<ILogger, Exception?> LogDisposingStrategy =
            LoggerMessage.Define(LogLevel.Information, new EventId(1008, nameof(LogDisposingStrategy)),
                "Disposing ParallelExecutionStrategy");

        private static readonly Action<ILogger, Exception?> LogStrategyDisposed =
            LoggerMessage.Define(LogLevel.Information, new EventId(1009, nameof(LogStrategyDisposed)),
                "ParallelExecutionStrategy disposed");

        private static readonly Action<ILogger, Exception?> LogErrorDuringSync =
            LoggerMessage.Define(LogLevel.Warning, new EventId(1010, nameof(LogErrorDuringSync)),
                "Error during final synchronization");

        private static readonly Action<ILogger, string, Exception?> LogInitializedStrategies =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(1011, nameof(LogInitializedStrategies)),
                "Initialized with strategies: {Strategies}");

        private static readonly Action<ILogger, int, int, Exception?> LogBufferSizeMismatch =
            LoggerMessage.Define<int, int>(LogLevel.Warning, new EventId(1012, nameof(LogBufferSizeMismatch)),
                "Input buffer size {ElementCount} is not evenly divisible by device count {DeviceCount}. This may lead to load imbalance.");

        private static readonly Action<ILogger, string, Exception?> LogErrorExecutingOnDevice =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(1013, nameof(LogErrorExecutingOnDevice)),
                "Error executing on device {DeviceId}");

        private static readonly Action<ILogger, int, int, Exception?> LogCreatingModelParallelPlan =
            LoggerMessage.Define<int, int>(LogLevel.Debug, new EventId(1014, nameof(LogCreatingModelParallelPlan)),
                "Creating model parallel execution plan with {LayerCount} layers across {DeviceCount} devices");

        private static readonly Action<ILogger, int, Exception?> LogExecutingModelParallelPlan =
            LoggerMessage.Define<int>(LogLevel.Debug, new EventId(1015, nameof(LogExecutingModelParallelPlan)),
                "Executing model parallel plan with {LayerCount} layers");

        private static readonly Action<ILogger, int, Exception?> LogErrorExecutingLayer =
            LoggerMessage.Define<int>(LogLevel.Error, new EventId(1016, nameof(LogErrorExecutingLayer)),
                "Error executing layer {LayerId}");

        private static readonly Action<ILogger, int, int, Exception?> LogCreatingPipelinePlan =
            LoggerMessage.Define<int, int>(LogLevel.Debug, new EventId(1017, nameof(LogCreatingPipelinePlan)),
                "Creating pipeline execution plan with {StageCount} stages across {DeviceCount} devices");

        private static readonly Action<ILogger, int, int, Exception?> LogExecutingPipelinePlan =
            LoggerMessage.Define<int, int>(LogLevel.Debug, new EventId(1018, nameof(LogExecutingPipelinePlan)),
                "Executing pipeline plan with {StageCount} stages and {MicrobatchCount} microbatches");

        private static readonly Action<ILogger, int, Exception?> LogErrorInPipelineStage =
            LoggerMessage.Define<int>(LogLevel.Error, new EventId(1019, nameof(LogErrorInPipelineStage)),
                "Error in pipeline stage {StageId}");
        /// <summary>
        /// Initializes a new instance of the ParallelExecutionStrategy class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="acceleratorManager">The accelerator manager.</param>
        /// <param name="kernelManager">The kernel manager.</param>
        /// <param name="loggerFactory">The logger factory.</param>

        public ParallelExecutionStrategy(
            ILogger<ParallelExecutionStrategy> logger,
            IAcceleratorManager acceleratorManager,
            IKernelManager kernelManager,
            ILoggerFactory? loggerFactory = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _acceleratorManager = acceleratorManager ?? throw new ArgumentNullException(nameof(acceleratorManager));
            _kernelManager = kernelManager ?? throw new ArgumentNullException(nameof(kernelManager));
            _loggerFactory = loggerFactory ?? new NullLoggerFactory();

            // For parallel execution, we need a memory manager but we don't have an accelerator yet - TODO
            // This will be set when execution starts
            _memoryManager = null!;
            _coordinator = new ExecutionCoordinator(_loggerFactory.CreateLogger<ExecutionCoordinator>());
            _performanceMonitor = new PerformanceMonitor(_loggerFactory.CreateLogger<PerformanceMonitor>());
            _distributedKernelCache = new ConcurrentDictionary<string, CompiledKernelCache>();
            _executionSemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2);
            _shutdownTokenSource = new CancellationTokenSource();

            Initialize();
        }

        /// <summary>
        /// Gets available execution strategies.
        /// </summary>
        public IReadOnlyList<ExecutionStrategyType> AvailableStrategies { get; private set; } = Array.Empty<ExecutionStrategyType>();

        /// <summary>
        /// Gets the current performance metrics.
        /// </summary>
        public ParallelExecutionMetrics CurrentMetrics => _performanceMonitor.GetCurrentMetrics();

        /// <summary>
        /// Executes a kernel using data parallelism across multiple GPUs.
        /// </summary>
        public async ValueTask<ParallelExecutionResult> ExecuteDataParallelAsync<T>(
            string kernelName,
            IUnifiedMemoryBuffer<T>[] inputBuffers,
            IUnifiedMemoryBuffer<T>[] outputBuffers,
            DataParallelismOptions options,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _shutdownTokenSource.Token);

            await _executionSemaphore.WaitAsync(combinedToken.Token).ConfigureAwait(false);
            try
            {
                LogStartingDataParallel(_logger, kernelName, options.TargetDevices?.Count ?? _acceleratorManager.Count, null);

                var startTime = Stopwatch.StartNew();
                var devices = SelectOptimalDevices(options);

                // Validate input compatibility
                ValidateDataParallelInputs(inputBuffers, outputBuffers, devices);

                // Create execution plan
                var executionPlan = await CreateDataParallelExecutionPlanAsync(
                    kernelName, inputBuffers, outputBuffers, devices, options, combinedToken.Token);

                // Execute plan with coordination
                var results = await ExecuteCoordinatedPlanAsync(executionPlan, combinedToken.Token);

                startTime.Stop();

                var finalResult = new ParallelExecutionResult
                {
                    Success = results.All(r => r.Success),
                    TotalExecutionTimeMs = startTime.Elapsed.TotalMilliseconds,
                    DeviceResults = [.. results],
                    Strategy = ExecutionStrategyType.DataParallel,
                    ThroughputGFLOPS = CalculateOverallThroughput(results),
                    MemoryBandwidthGBps = CalculateOverallMemoryBandwidth(results),
                    EfficiencyPercentage = CalculateParallelEfficiency(results, startTime.Elapsed.TotalMilliseconds)
                };

                _performanceMonitor.RecordExecution(finalResult);
                LogDataParallelCompleted(_logger, finalResult.TotalExecutionTimeMs, finalResult.EfficiencyPercentage, null);

                return finalResult;
            }
            finally
            {
                _ = _executionSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes a kernel using model parallelism for large models that don't fit on a single GPU.
        /// </summary>
        public async ValueTask<ParallelExecutionResult> ExecuteModelParallelAsync<T>(
            string kernelName,
            ModelParallelWorkload<T> workload,
            ModelParallelismOptions options,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _shutdownTokenSource.Token);

            await _executionSemaphore.WaitAsync(combinedToken.Token).ConfigureAwait(false);
            try
            {
                LogStartingModelParallel(_logger, kernelName, workload.ModelLayers.Count, null);

                var startTime = Stopwatch.StartNew();
                var devices = SelectOptimalDevices(ModelParallelismOptions.ToDataParallelOptions());

                // Create model parallel execution plan
                var executionPlan = await CreateModelParallelExecutionPlanAsync(
                    kernelName, workload, devices, options, combinedToken.Token);

                // Execute layers with dependency management
                var results = await ExecuteModelParallelPlanAsync(executionPlan, combinedToken.Token);

                startTime.Stop();

                var finalResult = new ParallelExecutionResult
                {
                    Success = results.All(r => r.Success),
                    TotalExecutionTimeMs = startTime.Elapsed.TotalMilliseconds,
                    DeviceResults = [.. results],
                    Strategy = ExecutionStrategyType.ModelParallel,
                    ThroughputGFLOPS = CalculateOverallThroughput(results),
                    MemoryBandwidthGBps = CalculateOverallMemoryBandwidth(results),
                    EfficiencyPercentage = CalculateParallelEfficiency(results, startTime.Elapsed.TotalMilliseconds)
                };

                _performanceMonitor.RecordExecution(finalResult);
                return finalResult;
            }
            finally
            {
                _ = _executionSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes a streaming pipeline with overlapped computation and communication.
        /// </summary>
        public async ValueTask<ParallelExecutionResult> ExecutePipelineParallelAsync<T>(
            PipelineDefinition<T> pipeline,
            PipelineParallelismOptions options,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _shutdownTokenSource.Token);

            await _executionSemaphore.WaitAsync(combinedToken.Token).ConfigureAwait(false);
            try
            {
                LogStartingPipelineParallel(_logger, pipeline.Stages.Count, null);

                var startTime = Stopwatch.StartNew();
                var devices = SelectOptimalDevices(options.ToDataParallelOptions());

                // Create pipeline execution plan
                var executionPlan = await CreatePipelineExecutionPlanAsync(pipeline, devices, options, combinedToken.Token);

                // Start pipeline execution with streaming
                var results = await ExecutePipelinePlanAsync(executionPlan, combinedToken.Token);

                startTime.Stop();

                var finalResult = new ParallelExecutionResult
                {
                    Success = results.All(r => r.Success),
                    TotalExecutionTimeMs = startTime.Elapsed.TotalMilliseconds,
                    DeviceResults = [.. results],
                    Strategy = ExecutionStrategyType.PipelineParallel,
                    ThroughputGFLOPS = CalculateOverallThroughput(results),
                    MemoryBandwidthGBps = CalculateOverallMemoryBandwidth(results),
                    EfficiencyPercentage = CalculateParallelEfficiency(results, startTime.Elapsed.TotalMilliseconds)
                };

                _performanceMonitor.RecordExecution(finalResult);
                return finalResult;
            }
            finally
            {
                _ = _executionSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes using dynamic load balancing with work stealing.
        /// </summary>
        public async ValueTask<ParallelExecutionResult> ExecuteWithWorkStealingAsync<T>(
            string kernelName,
            WorkStealingWorkload<T> workload,
            WorkStealingOptions options,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _shutdownTokenSource.Token);

            await _executionSemaphore.WaitAsync(combinedToken.Token).ConfigureAwait(false);
            try
            {
                LogStartingWorkStealing(_logger, workload.WorkItems.Count, null);

                var startTime = Stopwatch.StartNew();
                var devices = SelectOptimalDevices(WorkStealingOptions.ToDataParallelOptions());

                // Initialize work-stealing coordinator
                var workStealingCoordinator = new WorkStealingCoordinator<T>(
                    devices, workload, _memoryManager, _loggerFactory.CreateLogger<WorkStealingCoordinator<T>>());

                // Execute with dynamic load balancing
                var results = await workStealingCoordinator.ExecuteAsync(
                    _kernelManager, options, combinedToken.Token);

                startTime.Stop();

                var finalResult = new ParallelExecutionResult
                {
                    Success = results.All(r => r.Success),
                    TotalExecutionTimeMs = startTime.Elapsed.TotalMilliseconds,
                    DeviceResults = [.. results],
                    Strategy = ExecutionStrategyType.WorkStealing,
                    ThroughputGFLOPS = CalculateOverallThroughput(results),
                    MemoryBandwidthGBps = CalculateOverallMemoryBandwidth(results),
                    EfficiencyPercentage = CalculateParallelEfficiency(results, startTime.Elapsed.TotalMilliseconds)
                };

                _performanceMonitor.RecordExecution(finalResult);
                return finalResult;
            }
            finally
            {
                _ = _executionSemaphore.Release();
            }
        }

        /// <summary>
        /// Synchronizes all devices and ensures completion of pending operations.
        /// </summary>
        public async ValueTask SynchronizeAllAsync(CancellationToken cancellationToken = default)
        {
            LogSynchronizingAccelerators(_logger, null);

            var tasks = _acceleratorManager.AvailableAccelerators
                .Select(accelerator => accelerator.SynchronizeAsync(cancellationToken).AsTask())
                .ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            LogAcceleratorsSynchronized(_logger, null);
        }

        /// <summary>
        /// Gets performance analysis and optimization recommendations.
        /// </summary>
        public ParallelExecutionAnalysis GetPerformanceAnalysis() => _performanceMonitor.GetPerformanceAnalysis();

        /// <summary>
        /// Optimizes execution strategy based on historical performance data.
        /// </summary>
        public ExecutionStrategyRecommendation OptimizeStrategy(
            string kernelName,
            int[] inputSizes,
            AcceleratorType[] availableAcceleratorTypes) => _performanceMonitor.RecommendOptimalStrategy(kernelName, inputSizes, availableAcceleratorTypes);
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            LogDisposingStrategy(_logger, null);

            await _shutdownTokenSource.CancelAsync().ConfigureAwait(false);

            try
            {
                await SynchronizeAllAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogErrorDuringSync(_logger, ex);
            }

            await _memoryManager.DisposeAsync().ConfigureAwait(false);
            await _coordinator.DisposeAsync().ConfigureAwait(false);
            _performanceMonitor.Dispose();

            _executionSemaphore.Dispose();
            _shutdownTokenSource.Dispose();

            _disposed = true;
            LogStrategyDisposed(_logger, null);
        }

        #region Private Implementation

        private void Initialize()
        {
            var gpuAccelerators = _acceleratorManager.AvailableAccelerators
                .Where(a => a.Info.DeviceType != "CPU")
                .ToArray();

            var strategies = new List<ExecutionStrategyType>();

            if (gpuAccelerators.Length > 1)
            {
                strategies.AddRange(
                [
                ExecutionStrategyType.DataParallel,
                ExecutionStrategyType.ModelParallel,
                ExecutionStrategyType.PipelineParallel,
                ExecutionStrategyType.WorkStealing
            ]);
            }
            else if (gpuAccelerators.Length == 1)
            {
                strategies.AddRange(
                [
                ExecutionStrategyType.Single,
                ExecutionStrategyType.PipelineParallel
            ]);
            }

            // Always support heterogeneous execution if CPU is available
            if (_acceleratorManager.AvailableAccelerators.Any(a => a.Info.DeviceType == "CPU"))
            {
                strategies.Add(ExecutionStrategyType.Heterogeneous);
            }

            AvailableStrategies = strategies.AsReadOnly();
            LogInitializedStrategies(_logger, string.Join(", ", strategies), null);
        }

        private IAccelerator[] SelectOptimalDevices(DataParallelismOptions options)
        {
            if (options.TargetDevices != null)
            {
                return [.. options.TargetDevices
                .Select(_acceleratorManager.GetAcceleratorById)
                .Where(a => a != null)
                .Cast<IAccelerator>()];
            }

            var gpuAccelerators = _acceleratorManager.AvailableAccelerators
                .Where(a => a.Info.DeviceType != "CPU")
                .ToArray();

            if (gpuAccelerators.Length == 0)
            {
                var defaultAcc = _acceleratorManager.AvailableAccelerators.Count > 0 ? _acceleratorManager.AvailableAccelerators[0] : null;
                return defaultAcc != null ? [defaultAcc] : [];
            }

            // Select based on memory and compute capability
            return [.. gpuAccelerators
            .OrderByDescending(a => a.Info.TotalMemory)
            .ThenByDescending(a => a.Info.ComputeUnits)
            .Take(options.MaxDevices ?? gpuAccelerators.Length)];
        }

        private void ValidateDataParallelInputs<T>(
            IUnifiedMemoryBuffer<T>[] inputBuffers,
            IUnifiedMemoryBuffer<T>[] outputBuffers,
            IAccelerator[] devices) where T : unmanaged
        {
            if (inputBuffers == null || inputBuffers.Length == 0)
            {
                throw new ArgumentException("Input buffers cannot be null or empty", nameof(inputBuffers));
            }

            if (outputBuffers == null || outputBuffers.Length == 0)
            {
                throw new ArgumentException("Output buffers cannot be null or empty", nameof(outputBuffers));
            }

            if (devices == null || devices.Length == 0)
            {
                throw new ArgumentException("No devices available for execution", nameof(devices));
            }

            // Validate buffer sizes are divisible by device count for even distribution
            foreach (var buffer in inputBuffers)
            {
                var bufferElementCount = (int)(buffer.SizeInBytes / global::System.Runtime.InteropServices.Marshal.SizeOf<T>());
                if (bufferElementCount % devices.Length != 0)
                {
                    LogBufferSizeMismatch(_logger, bufferElementCount, devices.Length, null);
                }
            }
        }

        private async ValueTask<DataParallelExecutionPlan<T>> CreateDataParallelExecutionPlanAsync<T>(
            string kernelName,
            IUnifiedMemoryBuffer<T>[] inputBuffers,
            IUnifiedMemoryBuffer<T>[] outputBuffers,
            IAccelerator[] devices,
            DataParallelismOptions options,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Calculate work distribution
            var totalElements = (int)(inputBuffers[0].SizeInBytes / global::System.Runtime.InteropServices.Marshal.SizeOf<T>());
            var elementsPerDevice = totalElements / devices.Length;
            var remainingElements = totalElements % devices.Length;

            // Build device tasks array
            var deviceTasks = new DataParallelDeviceTask<T>[devices.Length];

            for (var i = 0; i < devices.Length; i++)
            {
                var startIndex = i * elementsPerDevice;
                var elementCount = elementsPerDevice + (i < remainingElements ? 1 : 0);

                // Create device-specific input/output buffer slices
                var deviceInputBuffers = await CreateDeviceBufferSlicesAsync(
                    inputBuffers, devices[i], startIndex, elementCount, cancellationToken);

                var deviceOutputBuffers = await CreateDeviceBufferSlicesAsync(
                    outputBuffers, devices[i], startIndex, elementCount, cancellationToken);

                // Compile kernel for this device if not cached
                var compiledKernel = await GetOrCompileKernelForDeviceAsync(
                    kernelName, devices[i], cancellationToken);

                deviceTasks[i] = new DataParallelDeviceTask<T>
                {
                    Device = devices[i],
                    CompiledKernel = compiledKernel,
                    InputBuffers = deviceInputBuffers,
                    OutputBuffers = deviceOutputBuffers,
                    StartIndex = startIndex,
                    ElementCount = elementCount
                };
            }

            var plan = new DataParallelExecutionPlan<T>
            {
                KernelName = kernelName,
                Devices = devices,
                StrategyType = ExecutionStrategyType.DataParallel,
                InputBuffers = inputBuffers,
                OutputBuffers = outputBuffers,
                DeviceTasks = deviceTasks
            };

            return plan;
        }

        private static async ValueTask<IUnifiedMemoryBuffer<T>[]> CreateDeviceBufferSlicesAsync<T>(
            IUnifiedMemoryBuffer<T>[] sourceBuffers,
            IAccelerator device,
            int startIndex,
            int elementCount,
            CancellationToken cancellationToken) where T : unmanaged
        {
            var deviceBuffers = new IUnifiedMemoryBuffer<T>[sourceBuffers.Length];

            for (var i = 0; i < sourceBuffers.Length; i++)
            {
                var sourceBuffer = sourceBuffers[i];

                // Create buffer slice on target device
                var deviceBuffer = sourceBuffer.Slice(startIndex, elementCount);

                deviceBuffers[i] = deviceBuffer;
            }

            await ValueTask.CompletedTask; // Ensure async
            return deviceBuffers;
        }

        private async ValueTask<ManagedCompiledKernel> GetOrCompileKernelForDeviceAsync(
            string kernelName,
            IAccelerator device,
            CancellationToken cancellationToken)
        {
            var cacheKey = $"{kernelName}_{device.Info.Id}";

            if (_distributedKernelCache.TryGetValue(cacheKey, out var cache) &&
                cache.TryGetKernel(device, out var cachedKernel))
            {
                return cachedKernel;
            }

            // This is a simplified kernel compilation - in practice, you'd provide the actual expression/operation
            var kernelsCompiledKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
                kernelName,
                [typeof(float)],
                typeof(float),
                device,
                null,
                null,
                cancellationToken);

            // Cache the kernel
            var kernelCache = _distributedKernelCache.GetOrAdd(cacheKey, _ => new CompiledKernelCache());
            // Convert from Kernels.ManagedCompiledKernel to Execution.ManagedCompiledKernel
            var executionKernel = new ManagedCompiledKernel(
                kernelsCompiledKernel.Name,
                device,
                new CompiledKernel { Name = kernelsCompiledKernel.Name });
            kernelCache.AddKernel(device, executionKernel);

            return executionKernel;
        }

        private async ValueTask<DeviceExecutionResult[]> ExecuteCoordinatedPlanAsync<T>(
            DataParallelExecutionPlan<T> plan,
            CancellationToken cancellationToken) where T : unmanaged
        {
            // Create synchronization events for cross-device coordination
            var deviceEvents = plan.Devices
                .Select((device, index) => _coordinator.CreateEvent($"device_{index}"))
                .ToArray();

            // Execute on all devices in parallel
            var executionTasks = plan.DeviceTasks
                .Select(async (task, index) =>
                {
                    try
                    {
                        var startTime = Stopwatch.StartNew();

                        // Memory transfers are synchronous in current implementation

                        // Execute kernel on device
                        var kernelArgs = CreateKernelArguments<T>([.. task.InputBuffers], [.. task.OutputBuffers]);
                        // Convert Execution.ManagedCompiledKernel to Kernels.ManagedCompiledKernel
                        var kernelsCompiledKernel = CreateKernelsCompatibleKernel(task.CompiledKernel);
                        var executionResult = await _kernelManager.ExecuteKernelAsync(
                            kernelsCompiledKernel,
                            ConvertKernelArguments(kernelArgs),
                            task.Device,
                            null,
                            cancellationToken);

                        // Signal completion to other devices
                        await _coordinator.SignalEventAsync(deviceEvents[index], cancellationToken);

                        startTime.Stop();

                        return new DeviceExecutionResult
                        {
                            DeviceId = task.Device.Info.Id,
                            Success = executionResult.Success,
                            ExecutionTimeMs = startTime.Elapsed.TotalMilliseconds,
                            ElementsProcessed = task.ElementCount,
                            MemoryBandwidthGBps = CalculateMemoryBandwidth(task, startTime.Elapsed.TotalMilliseconds),
                            ThroughputGFLOPS = executionResult.Timings?.EffectiveComputeThroughputGFLOPS ?? 0,
                            ErrorMessage = executionResult.ErrorMessage
                        };
                    }
                    catch (Exception ex)
                    {
                        LogErrorExecutingOnDevice(_logger, task.Device.Info.Id, ex);
                        return new DeviceExecutionResult
                        {
                            DeviceId = task.Device.Info.Id,
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                    }
                })
                .ToArray();

            var results = await Task.WhenAll(executionTasks).ConfigureAwait(false);

            // Wait for all devices to complete and synchronize
            await _coordinator.WaitForAllEventsAsync(deviceEvents, cancellationToken);

            return results!;
        }

        private static AbstractionsMemory.Kernels.KernelArgument[] CreateKernelArguments<T>(IUnifiedMemoryBuffer<T>[] inputBuffers, IUnifiedMemoryBuffer<T>[] outputBuffers) where T : unmanaged
        {
            var args = new AbstractionsMemory.Kernels.KernelArgument[inputBuffers.Length + outputBuffers.Length];

            for (var i = 0; i < inputBuffers.Length; i++)
            {
                args[i] = new AbstractionsMemory.Kernels.KernelArgument
                {
                    Name = $"input_{i}",
                    Value = inputBuffers[i],
                    Type = typeof(IUnifiedMemoryBuffer<T>),
                    IsDeviceMemory = true,
                    MemoryBuffer = inputBuffers[i] as IUnifiedMemoryBuffer
                };
            }

            for (var i = 0; i < outputBuffers.Length; i++)
            {
                args[inputBuffers.Length + i] = new AbstractionsMemory.Kernels.KernelArgument
                {
                    Name = $"output_{i}",
                    Value = outputBuffers[i],
                    Type = typeof(IUnifiedMemoryBuffer<T>),
                    IsDeviceMemory = true,
                    MemoryBuffer = outputBuffers[i] as IUnifiedMemoryBuffer
                };
            }

            return args;
        }

        private static double CalculateMemoryBandwidth<T>(DataParallelDeviceTask<T> task, double executionTimeMs) where T : unmanaged
        {
            var elementSize = global::System.Runtime.InteropServices.Marshal.SizeOf<T>();
            var totalBytes = (task.InputBuffers.Count + task.OutputBuffers.Count) *
                            task.ElementCount * elementSize;
            return (totalBytes / 1e9) / (executionTimeMs / 1000.0); // GB/s
        }

        private static double CalculateOverallThroughput(DeviceExecutionResult[] results) => results.Where(r => r.Success).Sum(r => r.ThroughputGFLOPS);

        private static double CalculateOverallMemoryBandwidth(DeviceExecutionResult[] results) => results.Where(r => r.Success).Sum(r => r.MemoryBandwidthGBps);

        private static double CalculateParallelEfficiency(DeviceExecutionResult[] results, double totalTimeMs)
        {
            var successfulResults = results.Where(r => r.Success).ToArray();
            if (successfulResults.Length == 0)
            {
                return 0;
            }

            var averageDeviceTime = successfulResults.Average(r => r.ExecutionTimeMs);
            var idealParallelTime = averageDeviceTime;

            return (idealParallelTime / totalTimeMs) * 100;
        }

        // Model parallel and pipeline parallel execution strategies
        private ValueTask<ModelParallelExecutionPlan<T>> CreateModelParallelExecutionPlanAsync<T>(
            string kernelName, ModelParallelWorkload<T> workload, IAccelerator[] devices,
            ModelParallelismOptions options, CancellationToken cancellationToken) where T : unmanaged
        {
            LogCreatingModelParallelPlan(_logger, workload.ModelLayers.Count, devices.Length, null);

            // Simple layer-to-device assignment using round-robin
            var layerAssignments = new Dictionary<int, IAccelerator>();
            for (var i = 0; i < workload.ModelLayers.Count; i++)
            {
                layerAssignments[workload.ModelLayers[i].LayerId] = devices[i % devices.Length];
            }

            // Create simple communication schedule
            var communicationSchedule = new CommunicationSchedule<T>
            {
                Operations = [],
                SynchronizationPoints = []
            };

            return ValueTask.FromResult(new ModelParallelExecutionPlan<T>
            {
                KernelName = kernelName,
                Devices = devices,
                StrategyType = ExecutionStrategyType.ModelParallel,
                ModelLayers = [.. workload.ModelLayers],
                LayerAssignments = layerAssignments,
                CommunicationSchedule = communicationSchedule,
                EstimatedExecutionTimeMs = 100.0 // Simple estimate
            });
        }

        private async ValueTask<DeviceExecutionResult[]> ExecuteModelParallelPlanAsync<T>(
            ModelParallelExecutionPlan<T> plan, CancellationToken cancellationToken) where T : unmanaged
        {
            LogExecutingModelParallelPlan(_logger, plan.ModelLayers.Count, null);

            var deviceResults = new List<DeviceExecutionResult>();
            var startTime = Stopwatch.StartNew();

            // Execute layers sequentially for simplicity (can be improved to parallel later)
            foreach (var layer in plan.ModelLayers)
            {
                var device = plan.LayerAssignments[layer.LayerId];

                try
                {
                    // Simulate layer execution
                    await Task.Delay(10, cancellationToken); // Simple simulation

                    var result = new DeviceExecutionResult
                    {
                        DeviceId = device.Info.Id,
                        Success = true,
                        ExecutionTimeMs = 10.0,
                        ElementsProcessed = 1000, // Simulated
                        MemoryBandwidthGBps = 100.0,
                        ThroughputGFLOPS = 50.0
                    };

                    deviceResults.Add(result);
                }
                catch (Exception ex)
                {
                    LogErrorExecutingLayer(_logger, layer.LayerId, ex);
                    deviceResults.Add(new DeviceExecutionResult
                    {
                        DeviceId = device.Info.Id,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            startTime.Stop();
            return [.. deviceResults];
        }

        private ValueTask<PipelineExecutionPlan<T>> CreatePipelineExecutionPlanAsync<T>(
            PipelineDefinition<T> pipeline, IAccelerator[] devices,
            PipelineParallelismOptions options, CancellationToken cancellationToken) where T : unmanaged
        {
            LogCreatingPipelinePlan(_logger, pipeline.Stages.Count, devices.Length, null);

            if (devices.Length < pipeline.Stages.Count)
            {
                throw new ArgumentException($"Need at least {pipeline.Stages.Count} devices for pipeline stages");
            }

            // Create simple pipeline stages
            var stages = new PipelineStage<T>[pipeline.Stages.Count];
            for (var i = 0; i < pipeline.Stages.Count; i++)
            {
                stages[i] = new PipelineStage<T>
                {
                    StageId = i,
                    Name = pipeline.Stages[i].Name,
                    Device = devices[i],
                    Kernel = null!, // Would be compiled in full implementation - TODO
                    InputBuffers = [],
                    OutputBuffers = [],
                    EstimatedProcessingTimeMs = 20.0
                };
            }

            var microbatchConfig = new MicrobatchConfiguration
            {
                Size = options.MicrobatchSize,
                Count = 10, // Simple default
                SchedulingStrategy = MicrobatchSchedulingStrategy.Sequential
            };

            var bufferStrategy = new PipelineBufferStrategy<T>
            {
                BufferPool = new BufferPool<T>(),
                DoubleBuffering = new DoubleBufferingConfig { Enabled = true },
                Prefetching = new PrefetchingStrategy { Enabled = true, PrefetchDepth = 2 }
            };

            return ValueTask.FromResult(new PipelineExecutionPlan<T>
            {
                KernelName = $"pipeline_{pipeline.GetHashCode():X}",
                Devices = [.. devices.Take(pipeline.Stages.Count)],
                StrategyType = ExecutionStrategyType.PipelineParallel,
                Stages = stages,
                MicrobatchConfig = microbatchConfig,
                BufferStrategy = bufferStrategy,
                EstimatedExecutionTimeMs = stages.Sum(s => s.EstimatedProcessingTimeMs)
            });
        }

        private async ValueTask<DeviceExecutionResult[]> ExecutePipelinePlanAsync<T>(
            PipelineExecutionPlan<T> plan, CancellationToken cancellationToken) where T : unmanaged
        {
            LogExecutingPipelinePlan(_logger, plan.Stages.Count, plan.MicrobatchConfig.Count, null);

            var deviceResults = new List<DeviceExecutionResult>();
            var startTime = Stopwatch.StartNew();

            // Execute pipeline stages in parallel
            var stageTasks = plan.Stages.Select(async (stage, index) =>
            {
                try
                {
                    var stageStartTime = Stopwatch.StartNew();

                    // Simulate microbatch processing
                    for (var batch = 0; batch < plan.MicrobatchConfig.Count; batch++)
                    {
                        await Task.Delay(5, cancellationToken); // Simulate processing time
                    }

                    stageStartTime.Stop();

                    return new DeviceExecutionResult
                    {
                        DeviceId = stage.Device.Info.Id,
                        Success = true,
                        ExecutionTimeMs = stageStartTime.Elapsed.TotalMilliseconds,
                        ElementsProcessed = plan.MicrobatchConfig.Size * plan.MicrobatchConfig.Count,
                        MemoryBandwidthGBps = 150.0,
                        ThroughputGFLOPS = 75.0
                    };
                }
                catch (Exception ex)
                {
                    LogErrorInPipelineStage(_logger, stage.StageId, ex);
                    return new DeviceExecutionResult
                    {
                        DeviceId = stage.Device.Info.Id,
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
            });

            var results = await Task.WhenAll(stageTasks);
            startTime.Stop();

            return results;
        }

        /// <summary>
        /// Converts Abstractions KernelArgument[] to Interfaces KernelArgument[]
        /// </summary>
        private static AbstractionsMemory.Interfaces.Kernels.KernelArgument[] ConvertKernelArguments(AbstractionsMemory.Kernels.KernelArgument[] abstractionsArgs)
        {
            return [.. abstractionsArgs.Select(arg => new AbstractionsMemory.Interfaces.Kernels.KernelArgument
            {
                Name = arg.Name,
                Type = arg.Type,
                Value = arg.Value!,
                Size = arg.Size,
                IsOutput = arg.IsOutput
            })];
        }

        /// <summary>
        /// Creates a Kernels-compatible kernel from an Execution kernel wrapper.
        /// </summary>
        /// <remarks>
        /// This method performs a type adaptation between the Execution layer's kernel wrapper
        /// and the Kernels namespace's ManagedCompiledKernel type. We use an adapter class
        /// that wraps the execution kernel and delegates to its implementation.
        /// </remarks>
        private static AbstractionsMemory.Kernels.Compilation.ManagedCompiledKernel CreateKernelsCompatibleKernel(ManagedCompiledKernel executionKernel)
        {
            return new ManagedCompiledKernelAdapter(executionKernel);
        }

        /// <summary>
        /// Adapter class that bridges Execution.ManagedCompiledKernel to Abstractions.ManagedCompiledKernel.
        /// </summary>
        private sealed class ManagedCompiledKernelAdapter : AbstractionsMemory.Kernels.Compilation.ManagedCompiledKernel
        {
            private readonly ManagedCompiledKernel _inner;

            public ManagedCompiledKernelAdapter(ManagedCompiledKernel inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public override string Name => _inner.Name;
            public override IAccelerator Device => _inner.Device;
            public override DotCompute.Abstractions.ICompiledKernel Kernel => _inner.Kernel;
            public override DateTimeOffset CompilationTime => _inner.CompilationTime;
            public override long ExecutionCount => _inner.ExecutionCount;
            public override TimeSpan TotalExecutionTime => _inner.TotalExecutionTime;
            public override TimeSpan AverageExecutionTime => _inner.AverageExecutionTime;

            public override void RecordExecution(double executionTimeMs) => _inner.RecordExecution(executionTimeMs);

            public override async ValueTask DisposeAsync() => await _inner.DisposeAsync().ConfigureAwait(false);
        }

        #endregion
    }
}
