// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Compilation;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Interfaces.Kernels;
using InterfaceKernelArgument = DotCompute.Abstractions.Interfaces.Kernels.KernelArgument;
using KernelArgument = DotCompute.Abstractions.Kernels.KernelArgument;
using DotCompute.Backends.CUDA.Types.Native;
using CudaBottleneckType = DotCompute.Abstractions.Types.BottleneckType;

namespace DotCompute.Backends.CUDA.Execution
{

    /// <summary>
    /// High-performance CUDA kernel executor with advanced features for RTX 2000 Ada GPU
    /// </summary>
    public sealed partial class CudaKernelExecutor : IKernelExecutor, IDisposable, IAsyncDisposable
    {
        private readonly IAccelerator _accelerator;
        private readonly CudaContext _context;
        private readonly CudaStreamManager _streamManager;
        private readonly CudaEventManager _eventManager;
        private readonly CudaKernelLauncher _launcher;
        private readonly ILogger _logger;
        private readonly CudaDeviceProperties _deviceProperties;
        private readonly ConcurrentDictionary<Guid, CudaKernelExecution> _activeExecutions;
        private readonly SemaphoreSlim _executionSemaphore;
        private bool _disposed;
        /// <summary>
        /// Initializes a new instance of the CudaKernelExecutor class.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="context">The context.</param>
        /// <param name="streamManager">The stream manager.</param>
        /// <param name="eventManager">The event manager.</param>
        /// <param name="logger">The logger.</param>

        public CudaKernelExecutor(
            IAccelerator accelerator,
            CudaContext context,
            CudaStreamManager streamManager,
            CudaEventManager eventManager,
            ILogger<CudaKernelExecutor> logger)
        {
            _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
            _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _launcher = new CudaKernelLauncher(context, logger);
            _activeExecutions = new ConcurrentDictionary<Guid, CudaKernelExecution>();
            _executionSemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);

            // Cache device properties for optimization
            _deviceProperties = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref _deviceProperties, context.DeviceId);
            CudaRuntime.CheckError(result, "getting device properties");

            LogExecutorInitialized(_logger, context.DeviceId, _deviceProperties.DeviceName);
        }
        /// <summary>
        /// Gets or sets the accelerator.
        /// </summary>
        /// <value>The accelerator.</value>

        public IAccelerator Accelerator => _accelerator;

        /// <summary>
        /// Executes a compiled kernel with automatic optimization
        /// </summary>
        public async ValueTask<KernelExecutionResult> ExecuteAsync(
            CompiledKernel kernel,
            InterfaceKernelArgument[] arguments,
            KernelExecutionConfig executionConfig,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var executionHandle = EnqueueExecution(kernel, arguments, executionConfig);
            return await WaitForCompletionAsync(executionHandle, cancellationToken).ConfigureAwait(false);
        }
        /// <summary>
        /// Executes a kernel and waits for completion with enhanced error handling
        /// </summary>
        public async ValueTask<KernelExecutionResult> ExecuteAndWaitAsync(
            CompiledKernel kernel,
            InterfaceKernelArgument[] arguments,
            KernelExecutionConfig executionConfig,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _executionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _context.MakeCurrent();

                var execution = new CudaKernelExecution
                {
                    Id = Guid.NewGuid(),
                    KernelName = kernel.Id.ToString(),
                    SubmittedAt = DateTimeOffset.UtcNow,
                    Stream = IntPtr.Zero, // Will use default stream for now
                    StartEvent = _eventManager.CreateEvent(),
                    EndEvent = _eventManager.CreateEvent()
                };

                try
                {
                    // Convert arguments to CUDA format
                    var cudaArgs = ConvertArgumentsToCuda(arguments);

                    // Get optimal launch configuration
                    var launchConfig = GetOptimalLaunchConfig(kernel, executionConfig);

                    // Record start event
                    _eventManager.RecordEventFast(execution.StartEvent, execution.Stream);

                    // Launch kernel
                    await LaunchKernelAsync(kernel, cudaArgs, launchConfig, execution.Stream, cancellationToken)
                        .ConfigureAwait(false);

                    // Record end event
                    _eventManager.RecordEventFast(execution.EndEvent, execution.Stream);

                    // Wait for completion if synchronous
                    if (!executionConfig.Flags.HasFlag(KernelExecutionFlags.None))
                    {
                        // Synchronize with the stream
                        var result = CudaRuntime.cudaStreamSynchronize(execution.Stream);
                        CudaRuntime.CheckError(result, "stream synchronization");
                    }

                    execution.CompletedAt = DateTimeOffset.UtcNow;
                    execution.IsCompleted = true;

                    var timings = executionConfig.CaptureTimings
                        ? await CaptureTimingsAsync(execution).ConfigureAwait(false)
                        : null;

                    return new KernelExecutionResult
                    {
                        Success = true,
                        Handle = CreateExecutionHandle(execution),
                        Timings = timings
                    };
                }
                catch (Exception ex)
                {
                    LogKernelExecutionFailed(_logger);
                    execution.IsCompleted = true;
                    execution.CompletedAt = DateTimeOffset.UtcNow;
                    return new KernelExecutionResult
                    {
                        Success = false,
                        Handle = CreateExecutionHandle(execution),
                        ErrorMessage = ex.Message
                    };
                }
                finally
                {
                    // Clean up events
                    _eventManager.DestroyEvent(execution.StartEvent);
                    _eventManager.DestroyEvent(execution.EndEvent);
                }
            }
            finally
            {
                _ = _executionSemaphore.Release();
            }
        }

        /// <summary>
        /// Enqueues a kernel for asynchronous execution
        /// </summary>
        public KernelExecutionHandle EnqueueExecution(
            CompiledKernel kernel,
            InterfaceKernelArgument[] arguments,
            KernelExecutionConfig executionConfig)
        {
            ThrowIfDisposed();

            var execution = new CudaKernelExecution
            {
                Id = Guid.NewGuid(),
                KernelName = kernel.Id.ToString(),
                SubmittedAt = DateTimeOffset.UtcNow,
                Stream = IntPtr.Zero, // Will use default stream for now
                StartEvent = _eventManager.CreateEvent(),
                EndEvent = _eventManager.CreateEvent()
            };

            _activeExecutions[execution.Id] = execution;

            // Start execution on background thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await _executionSemaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        _context.MakeCurrent();

                        var cudaArgs = ConvertArgumentsToCuda(arguments);
                        var launchConfig = GetOptimalLaunchConfig(kernel, executionConfig);

                        _eventManager.RecordEventFast(execution.StartEvent, execution.Stream);
                        await LaunchKernelAsync(kernel, cudaArgs, launchConfig, execution.Stream)
                            .ConfigureAwait(false);
                        _eventManager.RecordEventFast(execution.EndEvent, execution.Stream);

                        execution.CompletedAt = DateTimeOffset.UtcNow;
                        execution.IsCompleted = true;
                    }
                    finally
                    {
                        _ = _executionSemaphore.Release();
                    }
                }
                catch (Exception ex)
                {
                    LogAsyncExecutionFailed(_logger);
                    execution.Error = ex;
                    execution.IsCompleted = true;
                    execution.CompletedAt = DateTimeOffset.UtcNow;
                }
            });

            return CreateExecutionHandle(execution);
        }
        /// <summary>
        /// Waits for kernel execution completion
        /// </summary>
        public async ValueTask<KernelExecutionResult> WaitForCompletionAsync(
            KernelExecutionHandle handle,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!_activeExecutions.TryGetValue(handle.Id, out var execution))
            {
                throw new InvalidOperationException($"Execution handle {handle.Id} not found");
            }

            // Wait for completion with cancellation support
            while (!execution.IsCompleted && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Remove from active executions
            _ = _activeExecutions.TryRemove(handle.Id, out _);

            if (execution.Error != null)
            {
                return new KernelExecutionResult
                {
                    Success = false,
                    Handle = handle,
                    ErrorMessage = execution.Error.Message
                };
            }

            var timings = await CaptureTimingsAsync(execution).ConfigureAwait(false);


            return new KernelExecutionResult
            {

                Success = true,

                Handle = CreateExecutionHandle(execution),
                Timings = timings
            };
        }

        /// <summary>
        /// Gets optimal execution configuration with RTX 2000 Ada specific optimizations
        /// </summary>
        public KernelExecutionConfig GetOptimalExecutionConfig(CompiledKernel kernel, int[] problemSize)
        {
            ThrowIfDisposed();

            var dimensions = problemSize.Length;
            var totalElements = problemSize.Aggregate(1, (a, b) => a * b);

            // Calculate optimal block size based on device properties
            var optimalBlockSize = CalculateOptimalBlockSize(kernel);

            int[] localWorkSize;
            int[] globalWorkSize;

            switch (dimensions)
            {
                case 1:
                    localWorkSize = [optimalBlockSize];
                    globalWorkSize = [RoundUpToMultiple(problemSize[0], optimalBlockSize)];
                    break;

                case 2:
                    // Optimize for RTX 2000 Ada's memory hierarchy
                    var blockX = Math.Min(16, (int)Math.Sqrt(optimalBlockSize));
                    var blockY = optimalBlockSize / blockX;
                    localWorkSize = [blockX, blockY];
                    globalWorkSize = [
                        RoundUpToMultiple(problemSize[0], blockX),
                    RoundUpToMultiple(problemSize[1], blockY)
                    ];
                    break;

                case 3:
                    var blockSize3D = (int)Math.Pow(optimalBlockSize, 1.0 / 3.0);
                    blockSize3D = Math.Max(4, Math.Min(blockSize3D, 8));
                    localWorkSize = [blockSize3D, blockSize3D, blockSize3D];
                    globalWorkSize = [
                        RoundUpToMultiple(problemSize[0], blockSize3D),
                    RoundUpToMultiple(problemSize[1], blockSize3D),
                    RoundUpToMultiple(problemSize[2], blockSize3D)
                    ];
                    break;

                default:
                    throw new NotSupportedException($"Problem dimensions > 3 not supported: {dimensions}");
            }

            return new KernelExecutionConfig
            {
                GlobalWorkSize = globalWorkSize,
                LocalWorkSize = localWorkSize,
                CaptureTimings = true,
                Flags = DetermineOptimalFlags(kernel, totalElements)
            };
        }

        /// <summary>
        /// Profiles kernel execution with comprehensive metrics
        /// </summary>
        public async ValueTask<KernelProfilingResult> ProfileAsync(
            CompiledKernel kernel,
            InterfaceKernelArgument[] arguments,
            KernelExecutionConfig executionConfig,
            int iterations = 100,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var timings = new List<double>();
            var throughputSamples = new List<double>();

            // Warm up
            for (var i = 0; i < Math.Min(10, iterations / 10); i++)
            {
                _ = await ExecuteAndWaitAsync(kernel, arguments, executionConfig, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Profile iterations
            for (var i = 0; i < iterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await ExecuteAndWaitAsync(kernel, arguments, executionConfig, cancellationToken)
                    .ConfigureAwait(false);

                if (result.Success && result.Timings != null)
                {
                    timings.Add(result.Timings.KernelTimeMs);
                    throughputSamples.Add(result.Timings.EffectiveComputeThroughputGFLOPS);
                }
            }

            if (timings.Count == 0)
            {
                throw new InvalidOperationException("No successful executions for profiling");
            }

            timings.Sort();

            var avgTime = timings.Average();
            var minTime = timings.Min();
            var maxTime = timings.Max();
            var medianTime = timings[timings.Count / 2];
            var stdDev = Math.Sqrt(timings.Select(t => Math.Pow(t - avgTime, 2)).Average());

            var percentiles = new Dictionary<int, double>
            {
                [50] = medianTime,
                [90] = timings[(int)(timings.Count * 0.9)],
                [95] = timings[(int)(timings.Count * 0.95)],
                [99] = timings[(int)(timings.Count * 0.99)]
            };

            var bottleneck = AnalyzeBottlenecks(timings, throughputSamples);
            var suggestions = GenerateOptimizationSuggestions(bottleneck, executionConfig);

            return new KernelProfilingResult
            {
                Iterations = iterations,
                AverageTimeMs = avgTime,
                MinTimeMs = minTime,
                MaxTimeMs = maxTime,
                StdDevMs = stdDev,
                MedianTimeMs = medianTime,
                PercentileTimingsMs = percentiles,
                AchievedOccupancy = CalculateAchievedOccupancy(executionConfig),
                MemoryThroughputGBps = throughputSamples.Count > 0 ? throughputSamples.Average() : 0,
                ComputeThroughputGFLOPS = throughputSamples.Count > 0 ? throughputSamples.Average() : 0,
                Bottleneck = bottleneck,
                OptimizationSuggestions = suggestions
            };
        }

        private CudaLaunchConfig GetOptimalLaunchConfig(CompiledKernel kernel, KernelExecutionConfig executionConfig)
        {
            var globalSize = executionConfig.GlobalWorkSize;
            var localSize = executionConfig.LocalWorkSize;

            if (localSize == null || localSize.Count == 0)
            {
                // Auto-calculate optimal local size
                var blockSize = CalculateOptimalBlockSize(kernel);
                localSize = [blockSize];
            }

            return globalSize.Count switch
            {
                1 => CudaLaunchConfig.Create1D(globalSize[0], localSize[0]),
                2 => CudaLaunchConfig.Create2D(globalSize[0], globalSize[1], localSize[0], localSize[1]),
                3 => CudaLaunchConfig.Create3D(globalSize[0], globalSize[1], globalSize[2],
                                        localSize[0], localSize[1], localSize[2]),
                _ => throw new NotSupportedException($"Dimensions > 3 not supported: {globalSize.Count}"),
            };

        }

        private int CalculateOptimalBlockSize(CompiledKernel kernel)
        {
            // Use CUDA occupancy calculator for optimal block size
            var deviceProps = _deviceProperties;
            var warpSize = deviceProps.WarpSize;
            var maxThreadsPerBlock = deviceProps.MaxThreadsPerBlock;
            var maxThreadsPerSM = deviceProps.MaxThreadsPerMultiProcessor;
            var major = deviceProps.Major;
            var minor = deviceProps.Minor;

            // RTX 2000 Ada specific optimizations (compute capability 8.9)
            if (major == 8 && minor == 9)
            {
                // Ada architecture has 24 SMs, 1536 threads per SM
                // Optimal configuration for RTX 2000: 512 threads per block
                var optimalBlockSize = 512;

                // Validate against device limits
                optimalBlockSize = Math.Min(optimalBlockSize, maxThreadsPerBlock);

                // Ensure it's a multiple of warp size
                optimalBlockSize = (optimalBlockSize / warpSize) * warpSize;

                LogOptimalBlockSizeCalculated(_logger);
                return optimalBlockSize;
            }

            // Target 75% occupancy for Ampere+ architectures
            var targetOccupancy = major >= 8 ? 0.75 : 0.5;
            var maxBlocksPerSM = Math.Max(1, (int)(maxThreadsPerSM * targetOccupancy / 256));
            var blockSize = Math.Min(512, maxThreadsPerSM / maxBlocksPerSM);

            // Round to nearest multiple of warp size
            blockSize = (blockSize / warpSize) * warpSize;

            // Clamp to device limits
            return Math.Max(warpSize, Math.Min(blockSize, maxThreadsPerBlock));
        }

        private static KernelArguments ConvertArgumentsToCuda(InterfaceKernelArgument[] arguments) => [.. arguments.Select(arg => new KernelArgument(arg.Name, arg.Type, arg.Value))];

        private async Task LaunchKernelAsync(
        CompiledKernel kernel,
        KernelArguments arguments,
        CudaLaunchConfig launchConfig,
        IntPtr stream,
        CancellationToken cancellationToken = default)
        {
            // Convert CompiledKernel struct to CudaCompiledKernel
            var cudaKernel = CudaCompiledKernel.FromCompiledKernel(kernel) ?? throw new ArgumentException("Kernel must be a valid CUDA kernel", nameof(kernel));

            // Set CUDA context
            _context.MakeCurrent();

            // Use the kernel launcher for execution
            await _launcher.LaunchKernelAsync(cudaKernel.FunctionHandle, arguments, launchConfig, cancellationToken)
                .ConfigureAwait(false);
        }

        private Task<KernelExecutionTimings> CaptureTimingsAsync(CudaKernelExecution execution)
        {
            // Wait for events to complete
            var result = CudaRuntime.cudaEventSynchronize(execution.EndEvent);
            CudaRuntime.CheckError(result, "event synchronization");

            // Calculate kernel execution time
            var kernelTimeMs = _eventManager.ElapsedTime(execution.StartEvent, execution.EndEvent);
            var totalTimeMs = (execution.CompletedAt!.Value - execution.SubmittedAt).TotalMilliseconds;

            return Task.FromResult(new KernelExecutionTimings
            {
                KernelTimeMs = kernelTimeMs,
                TotalTimeMs = totalTimeMs,
                QueueWaitTimeMs = Math.Max(0, totalTimeMs - kernelTimeMs),
                EffectiveMemoryBandwidthGBps = CalculateMemoryBandwidth(kernelTimeMs),
                EffectiveComputeThroughputGFLOPS = CalculateComputeThroughput(kernelTimeMs)
            });
        }

        private double CalculateMemoryBandwidth(double kernelTimeMs)
        {
            // Simplified calculation - would need actual memory transfer sizes
            var deviceMemoryBandwidth = _deviceProperties.MemoryClockRate * _deviceProperties.MemoryBusWidth / 8.0 * 2.0 / 1e6;
            return deviceMemoryBandwidth * 0.8; // Assume 80% efficiency
        }

        private double CalculateComputeThroughput(double kernelTimeMs)
        {
            // Simplified calculation based on device peak performance
            var sm_count = _deviceProperties.MultiProcessorCount;
            var clock_ghz = _deviceProperties.ClockRate / 1e6;
            var peak_gflops = sm_count * clock_ghz * 128; // Approximate for Ada architecture
            return peak_gflops * 0.3; // Assume 30% efficiency for typical workloads
        }

        private BottleneckAnalysis AnalyzeBottlenecks(List<double> timings, List<double> throughputs)
        {
            var avgThroughput = throughputs.Count > 0 ? throughputs.Average() : 0;
            var peakThroughput = CalculateComputeThroughput(1.0); // Peak for 1ms
            var utilization = avgThroughput / peakThroughput;

            CudaBottleneckType type;
            double severity;
            string details;

            if (utilization < 0.3)
            {
                type = CudaBottleneckType.Compute;
                severity = 1.0 - utilization;
                details = "Low compute utilization suggests the kernel is not using GPU cores effectively";
            }
            else if (utilization > 0.8)
            {
                type = CudaBottleneckType.MemoryBandwidth;
                severity = utilization - 0.8;
                details = "High utilization may indicate memory bandwidth limitations";
            }
            else
            {
                type = CudaBottleneckType.None;
                severity = 0.0;
                details = "No significant bottleneck detected";
            }

            return new BottleneckAnalysis
            {
                Type = type switch
                {
                    CudaBottleneckType.None => CudaBottleneckType.None,
                    CudaBottleneckType.Compute => CudaBottleneckType.GPU,
                    CudaBottleneckType.MemoryBandwidth => CudaBottleneckType.Memory,
                    _ => CudaBottleneckType.None
                },
                Severity = Math.Min(1.0, severity),
                Details = details,
                ResourceUtilization = new Dictionary<string, double>
                {
                    ["Compute"] = utilization,
                    ["Memory"] = avgThroughput / CalculateMemoryBandwidth(1.0)
                }
            };
        }

        private static List<string> GenerateOptimizationSuggestions(BottleneckAnalysis? bottleneck, KernelExecutionConfig config)
        {
            var suggestions = new List<string>();

            if (bottleneck?.Type == CudaBottleneckType.GPU)
            {
                suggestions.Add("Consider increasing occupancy by reducing register usage or shared memory");
                suggestions.Add("Optimize thread divergence to improve warp utilization");
            }
            else if (bottleneck?.Type == CudaBottleneckType.Memory)
            {
                suggestions.Add("Improve memory coalescing by ensuring contiguous access patterns");
                suggestions.Add("Consider using shared memory to reduce global memory accesses");
            }

            return suggestions;
        }

        private double CalculateAchievedOccupancy(KernelExecutionConfig config)
        {
            var localWorkSize = config.LocalWorkSize?[0] ?? 256;
            var maxThreadsPerSM = _deviceProperties.MaxThreadsPerMultiProcessor;
            var blocksPerSM = maxThreadsPerSM / localWorkSize;
            return Math.Min(1.0, (double)blocksPerSM * localWorkSize / maxThreadsPerSM);
        }

        private KernelExecutionFlags DetermineOptimalFlags(CompiledKernel kernel, int totalElements)
        {
            var flags = KernelExecutionFlags.None;

            // Use heuristics based on problem size and kernel characteristics
            if (totalElements > 1_000_000)
            {
                flags |= KernelExecutionFlags.OptimizeForThroughput;
            }

            // RTX 2000 Ada specific optimizations
            if (_deviceProperties.Major >= 8) // Ada Lovelace and newer
            {
                flags |= KernelExecutionFlags.PreferSharedMemory;
            }

            return flags;
        }

        private static int RoundUpToMultiple(int value, int multiple) => ((value + multiple - 1) / multiple) * multiple;

        private static KernelExecutionHandle CreateExecutionHandle(CudaKernelExecution execution)
        {
            // Since KernelExecutionHandle properties have internal setters,
            // we need to use reflection to set them from outside the Core assembly
            var handle = new KernelExecutionHandle
            {
                Id = execution.Id,
                KernelName = execution.KernelName,
                SubmittedAt = execution.SubmittedAt,
                EventHandle = execution.EndEvent
            };

            // Use reflection to set internal properties
            var handleType = typeof(KernelExecutionHandle);
            var isCompletedProperty = handleType.GetProperty(nameof(KernelExecutionHandle.IsCompleted));
            var completedAtProperty = handleType.GetProperty(nameof(KernelExecutionHandle.CompletedAt));

            isCompletedProperty?.SetValue(handle, execution.IsCompleted);
            completedAtProperty?.SetValue(handle, execution.CompletedAt);

            return handle;
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
        /// <summary>
        /// Performs dispose.
        /// </summary>
#pragma warning disable VSTHRD002 // Synchronously waiting on tasks - required in synchronous Dispose path
        public void Dispose() => DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

        /// <summary>
        /// Performs async dispose.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                // Wait for all active executions to complete
                var completionTasks = _activeExecutions.Values
                    .Where(e => !e.IsCompleted)
                    .Select(async e =>
                    {
                        while (!e.IsCompleted)
                        {
                            await Task.Delay(10).ConfigureAwait(false);
                        }
                    });

                try
                {
                    await Task.WhenAll(completionTasks).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogDisposalTimeoutWarning(_logger, ex);
                }

                // Clean up remaining executions
                foreach (var execution in _activeExecutions.Values)
                {
                    try
                    {
                        _eventManager.DestroyEvent(execution.StartEvent);
                        _eventManager.DestroyEvent(execution.EndEvent);
                    }
                    catch (Exception ex)
                    {
                        LogEventCleanupError(_logger, ex);
                    }
                }

                _activeExecutions.Clear();
                _executionSemaphore.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Internal execution tracking
    /// </summary>
    internal sealed class CudaKernelExecution
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public Guid Id { get; set; }
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        /// <value>The kernel name.</value>
        public string KernelName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the submitted at.
        /// </summary>
        /// <value>The submitted at.</value>
        public DateTimeOffset SubmittedAt { get; set; }
        /// <summary>
        /// Gets or sets the completed at.
        /// </summary>
        /// <value>The completed at.</value>
        public DateTimeOffset? CompletedAt { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether completed.
        /// </summary>
        /// <value>The is completed.</value>
        public bool IsCompleted { get; set; }
        /// <summary>
        /// Gets or sets the stream.
        /// </summary>
        /// <value>The stream.</value>
        public IntPtr Stream { get; set; }
        /// <summary>
        /// Gets or sets the start event.
        /// </summary>
        /// <value>The start event.</value>
        public IntPtr StartEvent { get; set; }
        /// <summary>
        /// Gets or sets the end event.
        /// </summary>
        /// <value>The end event.</value>
        public IntPtr EndEvent { get; set; }
        /// <summary>
        /// Gets or sets the error.
        /// </summary>
        /// <value>The error.</value>
        public Exception? Error { get; set; }
    }
}
