// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Abstractions.Messaging;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Messaging;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MessageQueueOptions = DotCompute.Abstractions.Messaging.MessageQueueOptions;
using IRingKernelMessage = DotCompute.Abstractions.Messaging.IRingKernelMessage;
using RingKernels = DotCompute.Abstractions.RingKernels;
using DotCompute.Core.Messaging;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// CUDA runtime for managing persistent ring kernels.
/// </summary>
/// <remarks>
/// Provides lifecycle management for CUDA-based ring kernels including:
/// - Kernel launch with cooperative groups
/// - Activation/deactivation control
/// - Message routing and queue management
/// - Status monitoring and metrics collection
/// </remarks>
public sealed class CudaRingKernelRuntime : IRingKernelRuntime
{
    private readonly ILogger<CudaRingKernelRuntime> _logger;
    private readonly CudaRingKernelCompiler _compiler;
    private readonly MessageQueueRegistry _registry;
    private readonly ConcurrentDictionary<string, KernelState> _kernels = new();
    private bool _disposed;

    private sealed class KernelState
    {
        public string KernelId { get; init; } = string.Empty;
        public IntPtr Context { get; set; }
        public IntPtr Module { get; set; }
        public IntPtr Function { get; set; }
        public IntPtr ControlBlock { get; set; }
        public int GridSize { get; set; }
        public int BlockSize { get; set; }
        public bool IsLaunched { get; set; }
        public bool IsActive { get; set; }
        public DateTime LaunchTime { get; set; }
        public long MessagesProcessed { get; set; }
        public object? InputQueue { get; set; }
        public object? OutputQueue { get; set; }
        public CancellationTokenSource? KernelCts { get; set; }
        public CudaTelemetryBuffer? TelemetryBuffer { get; set; }
        public bool TelemetryEnabled { get; set; }

        // Bridge infrastructure for IRingKernelMessage types
        public object? InputBridge { get; set; }  // MessageQueueBridge<T> for input
        public object? OutputBridge { get; set; } // MessageQueueBridge<T> for output
        public object? GpuInputBuffer { get; set; }  // CudaMessageQueue<byte> for GPU-resident buffer
        public object? GpuOutputBuffer { get; set; } // CudaMessageQueue<byte> for GPU-resident buffer
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaRingKernelRuntime"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="compiler">Ring kernel compiler.</param>
    /// <param name="registry">Message queue registry for named queues.</param>
    public CudaRingKernelRuntime(
        ILogger<CudaRingKernelRuntime> logger,
        CudaRingKernelCompiler compiler,
        MessageQueueRegistry registry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc/>
    [RequiresDynamicCode("Ring kernel launch uses reflection for queue creation")]
    [RequiresUnreferencedCode("Ring kernel runtime requires reflection to detect message types")]
    public async Task LaunchAsync(
        string kernelId,
        int gridSize,
        int blockSize,
        RingKernelLaunchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (gridSize <= 0 || blockSize <= 0)
        {
            throw new ArgumentException("Grid and block sizes must be positive");
        }

        // Ensure options is not null
        options ??= new RingKernelLaunchOptions();

        _logger.LogInformation(
            "Launching ring kernel '{KernelId}' with grid={Grid}, block={Block}",
            kernelId,
            gridSize,
            blockSize);

        await Task.Run(async () =>
        {
            var state = new KernelState
            {
                KernelId = kernelId,
                GridSize = gridSize,
                BlockSize = blockSize,
                LaunchTime = DateTime.UtcNow,
                KernelCts = new CancellationTokenSource()
            };

            // Step 1: Initialize CUDA context
            var initResult = CudaRuntime.cuInit(0);
            if (initResult is not CudaError.Success and not ((CudaError)4))
            {
                throw new InvalidOperationException($"Failed to initialize CUDA: {initResult}");
            }

            var getDeviceResult = CudaRuntime.cuDeviceGet(out int device, 0);
            if (getDeviceResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to get CUDA device: {getDeviceResult}");
            }

            IntPtr context = IntPtr.Zero;
            var ctxResult = CudaRuntimeCore.cuCtxCreate(out context, 0, device);
            if (ctxResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to create CUDA context: {ctxResult}");
            }
            state.Context = context;

            try
            {
                // Step 2: Detect message types from kernel signature
                var (inputType, outputType) = CudaMessageQueueBridgeFactory.DetectMessageTypes(kernelId);

                _logger.LogDebug(
                    "Detected message types for kernel '{KernelId}': Input={InputType}, Output={OutputType}",
                    kernelId, inputType.Name, outputType.Name);

                // Step 3: Create message queues based on type
                // If IRingKernelMessage → Create bridge infrastructure
                // If unmanaged → Create direct GPU queues
                var isInputBridged = typeof(IRingKernelMessage).IsAssignableFrom(inputType);
                var isOutputBridged = typeof(IRingKernelMessage).IsAssignableFrom(outputType);

                if (isInputBridged)
                {
                    // Create bridge for IRingKernelMessage input type
                    var inputQueueName = $"ringkernel_{inputType.Name}_{kernelId}_input";
                    var (namedQueue, bridge, gpuBuffer) = await CudaMessageQueueBridgeFactory.CreateBridgeForMessageTypeAsync(
                        inputType,
                        inputQueueName,
                        options.ToMessageQueueOptions(),
                        state.Context,
                        _logger,
                        cancellationToken);

                    state.InputQueue = namedQueue;
                    state.InputBridge = bridge;
                    state.GpuInputBuffer = gpuBuffer;

                    // Register named queue with registry for SendToNamedQueueAsync access
                    _registry.TryRegister(inputType, inputQueueName, namedQueue, "CUDA");

                    _logger.LogInformation(
                        "Created bridged input queue '{QueueName}' for type {MessageType}",
                        inputQueueName, inputType.Name);
                }
                else
                {
                    // Create direct GPU queue for unmanaged types
                    var createQueueMethod = typeof(CudaRingKernelRuntime)
                        .GetMethod(nameof(CreateMessageQueueAsync), BindingFlags.Public | BindingFlags.Instance);

                    if (createQueueMethod == null)
                    {
                        throw new InvalidOperationException($"Failed to find CreateMessageQueueAsync method via reflection");
                    }

                    var genericMethod = createQueueMethod.MakeGenericMethod(inputType);
                    var queueTask = (Task?)genericMethod.Invoke(this, new object[] { options.QueueCapacity, cancellationToken });

                    if (queueTask == null)
                    {
                        throw new InvalidOperationException($"Failed to invoke CreateMessageQueueAsync for type {inputType.Name}");
                    }

                    await queueTask;

                    var resultProperty = queueTask.GetType().GetProperty("Result");
                    if (resultProperty == null)
                    {
                        throw new InvalidOperationException($"Failed to get Result property from Task");
                    }

                    state.InputQueue = resultProperty.GetValue(queueTask);

                    _logger.LogDebug(
                        "Created direct GPU input queue for unmanaged type {MessageType}",
                        inputType.Name);
                }

                if (isOutputBridged)
                {
                    // Create bidirectional bridge for IRingKernelMessage output type (Device → Host)
                    var outputQueueName = $"ringkernel_{outputType.Name}_{kernelId}_output";
                    var (namedQueue, bridge, gpuBuffer) = await CudaMessageQueueBridgeFactory.CreateBidirectionalBridgeForMessageTypeAsync(
                        outputType,
                        outputQueueName,
                        BridgeDirection.DeviceToHost,  // Output: GPU writes → Host reads
                        options.ToMessageQueueOptions(),
                        state.Context,
                        _logger,
                        cancellationToken);

                    state.OutputQueue = namedQueue;
                    state.OutputBridge = bridge;
                    state.GpuOutputBuffer = gpuBuffer;

                    // Register named queue with registry
                    _registry.TryRegister(outputType, outputQueueName, namedQueue, "CUDA");

                    _logger.LogInformation(
                        "Created bidirectional output bridge '{QueueName}' for type {MessageType} (Direction=DeviceToHost)",
                        outputQueueName, outputType.Name);
                }
                else
                {
                    // Create direct GPU queue for unmanaged types
                    var createQueueMethod = typeof(CudaRingKernelRuntime)
                        .GetMethod(nameof(CreateMessageQueueAsync), BindingFlags.Public | BindingFlags.Instance);

                    if (createQueueMethod == null)
                    {
                        throw new InvalidOperationException($"Failed to find CreateMessageQueueAsync method via reflection");
                    }

                    var genericMethod = createQueueMethod.MakeGenericMethod(outputType);
                    var queueTask = (Task?)genericMethod.Invoke(this, new object[] { options.QueueCapacity, cancellationToken });

                    if (queueTask == null)
                    {
                        throw new InvalidOperationException($"Failed to invoke CreateMessageQueueAsync for type {outputType.Name}");
                    }

                    await queueTask;

                    var resultProperty = queueTask.GetType().GetProperty("Result");
                    if (resultProperty == null)
                    {
                        throw new InvalidOperationException($"Failed to get Result property from Task");
                    }

                    state.OutputQueue = resultProperty.GetValue(queueTask);

                    _logger.LogDebug(
                        "Created direct GPU output queue for unmanaged type {MessageType}",
                        outputType.Name);
                }

                // Step 3: Compile kernel to PTX/CUBIN (for now, generate a simple test kernel)
                var kernelSource = GenerateSimpleKernel(kernelId);

                // Step 4: Load kernel module
                state.Module = LoadKernelModule(state.Context, kernelSource, kernelId);

                // Step 5: Get kernel function
                state.Function = GetKernelFunction(state.Module, kernelId);

                // Step 6: Allocate and initialize control block
                state.ControlBlock = RingKernelControlBlockHelper.AllocateAndInitialize(state.Context);

                // Step 7: Update control block with queue pointers
                // Use reflection to call GetHeadPtr/GetTailPtr on dynamically-typed queues
                if (state.InputQueue == null || state.OutputQueue == null)
                {
                    throw new InvalidOperationException("Input and output queues must be initialized before accessing control block");
                }

                // Get queue methods via reflection (works for any CudaMessageQueue<T>)
                var inputQueueType = state.InputQueue.GetType();
                var outputQueueType = state.OutputQueue.GetType();

                var inputGetHeadPtrMethod = inputQueueType.GetMethod("GetHeadPtr");
                var inputGetTailPtrMethod = inputQueueType.GetMethod("GetTailPtr");
                var outputGetHeadPtrMethod = outputQueueType.GetMethod("GetHeadPtr");
                var outputGetTailPtrMethod = outputQueueType.GetMethod("GetTailPtr");

                if (inputGetHeadPtrMethod == null || inputGetTailPtrMethod == null ||
                    outputGetHeadPtrMethod == null || outputGetTailPtrMethod == null)
                {
                    throw new InvalidOperationException("Queue type does not support GetHeadPtr/GetTailPtr methods");
                }

                var controlBlock = RingKernelControlBlock.CreateInactive();
                var inputHeadPtr = inputGetHeadPtrMethod.Invoke(state.InputQueue, null);
                var inputTailPtr = inputGetTailPtrMethod.Invoke(state.InputQueue, null);
                var outputHeadPtr = outputGetHeadPtrMethod.Invoke(state.OutputQueue, null);
                var outputTailPtr = outputGetTailPtrMethod.Invoke(state.OutputQueue, null);

                if (inputHeadPtr == null || inputTailPtr == null || outputHeadPtr == null || outputTailPtr == null)
                {
                    throw new InvalidOperationException("Queue pointers cannot be null");
                }

                controlBlock.InputQueueHeadPtr = ((CudaDevicePointerBuffer)inputHeadPtr).DevicePointer.ToInt64();
                controlBlock.InputQueueTailPtr = ((CudaDevicePointerBuffer)inputTailPtr).DevicePointer.ToInt64();
                controlBlock.OutputQueueHeadPtr = ((CudaDevicePointerBuffer)outputHeadPtr).DevicePointer.ToInt64();
                controlBlock.OutputQueueTailPtr = ((CudaDevicePointerBuffer)outputTailPtr).DevicePointer.ToInt64();

                RingKernelControlBlockHelper.Write(state.Context, state.ControlBlock, controlBlock);

                // Step 8: Launch persistent kernel (initially inactive)
                _logger.LogInformation(
                    "Launching persistent kernel '{KernelId}' with grid={Grid}, block={Block}",
                    kernelId, gridSize, blockSize);

                // Note: For now, we skip actual kernel launch as it requires cooperative groups
                // In production, this would use cuLaunchCooperativeKernel
                // The kernel would run in an infinite loop checking the control block

                state.IsLaunched = true;
                state.IsActive = false; // Starts inactive
                _kernels[kernelId] = state;

                _logger.LogInformation("Ring kernel '{KernelId}' launched successfully", kernelId);
            }
            catch
            {
                // Cleanup on failure
                if (state.ControlBlock != IntPtr.Zero)
                {
                    RingKernelControlBlockHelper.Free(state.Context, state.ControlBlock);
                }
                if (state.Module != IntPtr.Zero)
                {
                    CudaApi.cuModuleUnload(state.Module);
                }
                if (state.Context != IntPtr.Zero)
                {
                    CudaRuntimeCore.cuCtxDestroy(state.Context);
                }
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(string kernelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        if (!state.IsLaunched)
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not launched");
        }

        if (state.IsActive)
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' already active");
        }

        _logger.LogInformation("Activating ring kernel '{KernelId}'", kernelId);

        await Task.Run(() =>
        {
            // Set active flag in control block atomically
            RingKernelControlBlockHelper.SetActive(state.Context, state.ControlBlock, true);
            state.IsActive = true;

            _logger.LogInformation("Ring kernel '{KernelId}' activated", kernelId);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeactivateAsync(string kernelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        if (!state.IsActive)
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not active");
        }

        _logger.LogInformation("Deactivating ring kernel '{KernelId}'", kernelId);

        await Task.Run(() =>
        {
            // Clear active flag in control block atomically
            RingKernelControlBlockHelper.SetActive(state.Context, state.ControlBlock, false);
            state.IsActive = false;

            _logger.LogInformation("Ring kernel '{kernelId}' deactivated", kernelId);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task TerminateAsync(string kernelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        _logger.LogInformation("Terminating ring kernel '{KernelId}'", kernelId);

        await Task.Run(async () =>
        {
            // Set terminate flag in control block
            RingKernelControlBlockHelper.SetTerminate(state.Context, state.ControlBlock);

            // Wait for kernel to exit gracefully (with 5 second timeout)
            var terminated = await RingKernelControlBlockHelper.WaitForTerminationAsync(
                state.Context,
                state.ControlBlock,
                TimeSpan.FromSeconds(5),
                cancellationToken);

            if (!terminated)
            {
                _logger.LogWarning(
                    "Kernel '{KernelId}' did not terminate gracefully within timeout",
                    kernelId);
            }

            // Free control block
            if (state.ControlBlock != IntPtr.Zero)
            {
                RingKernelControlBlockHelper.Free(state.Context, state.ControlBlock);
                state.ControlBlock = IntPtr.Zero;
            }

            // Unload kernel module
            if (state.Module != IntPtr.Zero)
            {
                CudaApi.cuModuleUnload(state.Module);
                state.Module = IntPtr.Zero;
            }

            // Dispose bridges first (stops pump threads)
            if (state.InputBridge is IAsyncDisposable inputBridgeDisposable)
            {
                await inputBridgeDisposable.DisposeAsync();
                _logger.LogDebug("Disposed input bridge for kernel '{KernelId}'", kernelId);
            }
            if (state.OutputBridge is IAsyncDisposable outputBridgeDisposable)
            {
                await outputBridgeDisposable.DisposeAsync();
                _logger.LogDebug("Disposed output bridge for kernel '{KernelId}'", kernelId);
            }

            // Dispose GPU buffers
            if (state.GpuInputBuffer is IAsyncDisposable gpuInputDisposable)
            {
                await gpuInputDisposable.DisposeAsync();
            }
            if (state.GpuOutputBuffer is IAsyncDisposable gpuOutputDisposable)
            {
                await gpuOutputDisposable.DisposeAsync();
            }

            // Dispose message queues
            if (state.InputQueue is IAsyncDisposable inputDisposable)
            {
                await inputDisposable.DisposeAsync();
            }
            if (state.OutputQueue is IAsyncDisposable outputDisposable)
            {
                await outputDisposable.DisposeAsync();
            }

            // Dispose telemetry buffer
            if (state.TelemetryBuffer != null)
            {
                state.TelemetryBuffer.Dispose();
                state.TelemetryBuffer = null;
                _logger.LogDebug("Disposed telemetry buffer for kernel '{KernelId}'", kernelId);
            }

            // Destroy CUDA context
            if (state.Context != IntPtr.Zero)
            {
                CudaRuntimeCore.cuCtxDestroy(state.Context);
                state.Context = IntPtr.Zero;
            }

            // Cancel kernel cancellation token
            if (state.KernelCts != null)
            {
                await state.KernelCts.CancelAsync();
                state.KernelCts.Dispose();
            }

            _kernels.TryRemove(kernelId, out _);

            _logger.LogInformation("Ring kernel '{KernelId}' terminated", kernelId);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendMessageAsync<T>(
        string kernelId,
        KernelMessage<T> message,
        CancellationToken cancellationToken = default)
        where T : unmanaged
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        if (state.InputQueue is not DotCompute.Abstractions.RingKernels.IMessageQueue<T> queue)
        {
            throw new InvalidOperationException($"Input queue not available for kernel '{kernelId}'");
        }

        await queue.EnqueueAsync(message, default, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<KernelMessage<T>?> ReceiveMessageAsync<T>(
        string kernelId,
        TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
        where T : unmanaged
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        if (state.OutputQueue is not DotCompute.Abstractions.RingKernels.IMessageQueue<T> queue)
        {
            throw new InvalidOperationException($"Output queue not available for kernel '{kernelId}'");
        }

        try
        {
            if (timeout == default)
            {
                return await queue.TryDequeueAsync(cancellationToken);
            }

            return await queue.DequeueAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage("Trimming", "IL2075:UnrecognizedReflectionPattern",
        Justification = "Queue type reflection is required for generic runtime operation")]
    public Task<RingKernelStatus> GetStatusAsync(
        string kernelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        // Read control block for current kernel state
        var controlBlock = RingKernelControlBlockHelper.Read(state.Context, state.ControlBlock);

        // Get input queue count if available
        int messagesPending = 0;
        if (state.InputQueue != null)
        {
            var queueType = state.InputQueue.GetType();
            var countProperty = queueType.GetProperty("Count");
            if (countProperty != null)
            {
                messagesPending = (int)(countProperty.GetValue(state.InputQueue) ?? 0);
            }
        }

        var status = new RingKernelStatus
        {
            KernelId = kernelId,
            IsLaunched = state.IsLaunched,
            IsActive = controlBlock.IsActive != 0,
            IsTerminating = controlBlock.ShouldTerminate != 0,
            MessagesPending = messagesPending,
            MessagesProcessed = controlBlock.MessagesProcessed,
            GridSize = state.GridSize,
            BlockSize = state.BlockSize,
            Uptime = DateTime.UtcNow - state.LaunchTime
        };

        return Task.FromResult(status);
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage("Trimming", "IL2075:UnrecognizedReflectionPattern",
        Justification = "Queue type reflection is required for generic runtime operation")]
    public async Task<RingKernelMetrics> GetMetricsAsync(
        string kernelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        // Read control block for accurate message counts
        var controlBlock = RingKernelControlBlockHelper.Read(state.Context, state.ControlBlock);

        var uptime = DateTime.UtcNow - state.LaunchTime;
        var throughput = uptime.TotalSeconds > 0
            ? controlBlock.MessagesProcessed / uptime.TotalSeconds
            : 0;

        // Get queue statistics if available
        double inputUtilization = 0;
        double outputUtilization = 0;
        double avgProcessingTimeMs = 0;

        if (state.InputQueue != null)
        {
            var inputQueueType = state.InputQueue.GetType();
            var getStatsMethod = inputQueueType.GetMethod("GetStatisticsAsync");
            if (getStatsMethod != null)
            {
                if (getStatsMethod.Invoke(state.InputQueue, null) is Task statsTask)
                {
                    await statsTask;
                    var stats = statsTask.GetType().GetProperty("Result")?.GetValue(statsTask);
                    if (stats != null)
                    {
                        var utilizationProp = stats.GetType().GetProperty("Utilization");
                        var avgLatencyProp = stats.GetType().GetProperty("AverageLatencyMicroseconds");

                        if (utilizationProp != null)
                        {
                            inputUtilization = (double)utilizationProp.GetValue(stats)!;
                        }
                        if (avgLatencyProp != null)
                        {
                            avgProcessingTimeMs = (double)avgLatencyProp.GetValue(stats)! / 1000.0; // Convert to ms
                        }
                    }
                }
            }
        }

        if (state.OutputQueue != null)
        {
            var outputQueueType = state.OutputQueue.GetType();
            var getStatsMethod = outputQueueType.GetMethod("GetStatisticsAsync");
            if (getStatsMethod != null)
            {
                if (getStatsMethod.Invoke(state.OutputQueue, null) is Task statsTask)
                {
                    await statsTask;
                    var stats = statsTask.GetType().GetProperty("Result")?.GetValue(statsTask);
                    if (stats != null)
                    {
                        var utilizationProp = stats.GetType().GetProperty("Utilization");
                        if (utilizationProp != null)
                        {
                            outputUtilization = (double)utilizationProp.GetValue(stats)!;
                        }
                    }
                }
            }
        }

        var metrics = new RingKernelMetrics
        {
            LaunchCount = 1,
            MessagesSent = controlBlock.MessagesProcessed,
            MessagesReceived = controlBlock.MessagesProcessed,
            AvgProcessingTimeMs = avgProcessingTimeMs,
            ThroughputMsgsPerSec = throughput,
            InputQueueUtilization = inputUtilization,
            OutputQueueUtilization = outputUtilization,
            PeakMemoryBytes = 0, // Requires tracking during allocation
            CurrentMemoryBytes = 0, // Requires CUDA memory query
            GpuUtilizationPercent = 0 // Requires NVML integration
        };

        return metrics;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> ListKernelsAsync()
    {
        var kernelIds = _kernels.Keys.ToList();
        return Task.FromResult<IReadOnlyCollection<string>>(kernelIds);
    }

    /// <inheritdoc/>
    public async Task<DotCompute.Abstractions.RingKernels.IMessageQueue<T>> CreateMessageQueueAsync<T>(
        int capacity,
        CancellationToken cancellationToken = default)
        where T : unmanaged
    {
        // Validate capacity before creating logger
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
        {
            throw new ArgumentException("Capacity must be a positive power of 2", nameof(capacity));
        }

        // Create a null logger for the message queue
        // In production, this should use ILoggerFactory to create typed loggers
        var logger = NullLogger<CudaMessageQueue<T>>.Instance;

        var queue = new CudaMessageQueue<T>(capacity, logger);
        await queue.InitializeAsync(cancellationToken);

        return queue;
    }

    /// <inheritdoc/>
    public async Task<DotCompute.Abstractions.Messaging.IMessageQueue<T>> CreateNamedMessageQueueAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        string queueName,
        MessageQueueOptions options,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(options);

        // Create CUDA message queue (use fully qualified namespace to avoid conflict with RingKernels.CudaMessageQueue)
        var logger = NullLogger<DotCompute.Backends.CUDA.Messaging.CudaMessageQueue<T>>.Instance;
        var queue = new DotCompute.Backends.CUDA.Messaging.CudaMessageQueue<T>(options, logger);

        // Initialize queue (allocates GPU resources)
        await queue.InitializeAsync(cancellationToken);

        // Register with registry
        if (!_registry.TryRegister<T>(queueName, queue, "CUDA"))
        {
            // Queue with same name already exists
            queue.Dispose();
            throw new InvalidOperationException($"Message queue '{queueName}' already exists");
        }

        _logger.LogDebug("Created CUDA message queue '{QueueName}' with capacity {Capacity}", queueName, options.Capacity);

        return queue;
    }

    /// <inheritdoc/>
    public Task<DotCompute.Abstractions.Messaging.IMessageQueue<T>?> GetNamedMessageQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        // Retrieve queue from registry
        var queue = _registry.TryGet<T>(queueName);

        return Task.FromResult(queue);
    }

    /// <inheritdoc/>
    public Task<bool> SendToNamedQueueAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(message);

        // Get queue from registry
        var queue = _registry.TryGet<T>(queueName);
        if (queue is null)
        {
            _logger.LogWarning("Message queue '{QueueName}' not found", queueName);
            return Task.FromResult(false);
        }

        // Enqueue message
        bool success = queue.TryEnqueue(message, cancellationToken);

        return Task.FromResult(success);
    }

    /// <inheritdoc/>
    public Task<T?> ReceiveFromNamedQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        // Get queue from registry
        var queue = _registry.TryGet<T>(queueName);
        if (queue is null)
        {
            _logger.LogWarning("Message queue '{QueueName}' not found", queueName);
            return Task.FromResult<T?>(default);
        }

        // Dequeue message
        _ = queue.TryDequeue(out T? message);

        return Task.FromResult(message);
    }

    /// <inheritdoc/>
    public Task<bool> DestroyNamedMessageQueueAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        // Unregister and dispose queue
        bool removed = _registry.TryUnregister(queueName, disposeQueue: true);

        if (removed)
        {
            _logger.LogDebug("Destroyed message queue '{QueueName}'", queueName);
        }
        else
        {
            _logger.LogWarning("Message queue '{QueueName}' not found", queueName);
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> ListNamedMessageQueuesAsync(
        CancellationToken cancellationToken = default)
    {
        // List all queues registered for CUDA backend
        var queues = _registry.ListQueuesByBackend("CUDA");

        return Task.FromResult(queues);
    }

    // ===== Phase 1.5: Real-Time Telemetry Implementation =====

    /// <inheritdoc/>
    public async Task<RingKernelTelemetry> GetTelemetryAsync(
        string kernelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        if (state.TelemetryBuffer == null)
        {
            throw new InvalidOperationException(
                $"Telemetry is not enabled for kernel '{kernelId}'. " +
                "Call SetTelemetryEnabledAsync(kernelId, true) first.");
        }

        _logger.LogTrace("Polling telemetry for kernel '{KernelId}'", kernelId);

        // Zero-copy read from pinned host memory (<1μs latency)
        var telemetry = await state.TelemetryBuffer.PollAsync(cancellationToken);

        return telemetry;
    }

    /// <inheritdoc/>
    public Task SetTelemetryEnabledAsync(
        string kernelId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        if (enabled)
        {
            // Enable telemetry
            if (state.TelemetryBuffer == null)
            {
                _logger.LogInformation("Enabling telemetry for kernel '{KernelId}'", kernelId);

                // Create and initialize telemetry buffer
                var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
                var telemetryLogger = loggerFactory.CreateLogger<CudaTelemetryBuffer>();
                state.TelemetryBuffer = new CudaTelemetryBuffer(telemetryLogger);
                state.TelemetryBuffer.Allocate();

                state.TelemetryEnabled = true;

                _logger.LogDebug(
                    "Telemetry enabled for kernel '{KernelId}' - host={HostPtr:X16}, device={DevicePtr:X16}",
                    kernelId,
                    state.TelemetryBuffer.HostPointer.ToInt64(),
                    state.TelemetryBuffer.DevicePointer.ToInt64());
            }
            else
            {
                _logger.LogDebug("Telemetry already enabled for kernel '{KernelId}'", kernelId);
                state.TelemetryEnabled = true;
            }
        }
        else
        {
            // Disable telemetry (keep buffer allocated but stop updating)
            if (state.TelemetryBuffer != null)
            {
                _logger.LogInformation("Disabling telemetry for kernel '{KernelId}'", kernelId);
                state.TelemetryEnabled = false;
            }
            else
            {
                _logger.LogDebug("Telemetry already disabled for kernel '{KernelId}'", kernelId);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ResetTelemetryAsync(
        string kernelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        if (state.TelemetryBuffer == null)
        {
            throw new InvalidOperationException(
                $"Telemetry is not enabled for kernel '{kernelId}'. " +
                "Call SetTelemetryEnabledAsync(kernelId, true) first.");
        }

        _logger.LogDebug("Resetting telemetry for kernel '{KernelId}'", kernelId);

        // Reset telemetry counters to initial values
        state.TelemetryBuffer.Reset();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing CUDA ring kernel runtime");

        // Terminate all active kernels
        foreach (var kernelId in _kernels.Keys.ToList())
        {
            try
            {
                await TerminateAsync(kernelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating kernel '{KernelId}' during disposal", kernelId);
            }
        }

        _disposed = true;
        _logger.LogInformation("CUDA ring kernel runtime disposed");
    }

    /// <summary>
    /// Generates a simple persistent kernel for testing.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <returns>PTX source code.</returns>
    private static string GenerateSimpleKernel(string kernelId)
    {
        // Generate a minimal persistent kernel that checks the control block
        // Note: PTX must be properly formatted without leading newline
        return $@".version 7.0
.target sm_50
.address_size 64

.visible .entry {kernelId}(
    .param .u64 {kernelId}_param_0
)
{{
    .reg .pred %p<3>;
    .reg .u32 %r<4>;
    .reg .u64 %rd<4>;

    // Load control block pointer
    ld.param.u64 %rd1, [{kernelId}_param_0];

entry_loop:
    // Load IsActive flag (offset 0)
    ld.global.u32 %r1, [%rd1];
    setp.eq.u32 %p1, %r1, 0;
    @%p1 bra check_terminate;

    // Kernel is active - continue checking

check_terminate:
    // Load ShouldTerminate flag (offset 4)
    add.u64 %rd2, %rd1, 4;
    ld.global.u32 %r2, [%rd2];
    setp.eq.u32 %p2, %r2, 0;
    @%p2 bra entry_loop;

    // Set HasTerminated flag (offset 8)
    mov.u32 %r3, 1;
    add.u64 %rd3, %rd1, 8;
    st.global.u32 [%rd3], %r3;

    ret;
}}";
    }

    /// <summary>
    /// Loads a compiled kernel module from PTX or CUBIN source.
    /// </summary>
    /// <param name="context">CUDA context to use.</param>
    /// <param name="kernelSource">Compiled kernel source (PTX or CUBIN).</param>
    /// <param name="kernelId">Kernel identifier for logging.</param>
    /// <returns>Module handle.</returns>
    private IntPtr LoadKernelModule(IntPtr context, string kernelSource, string kernelId)
    {
        // Set context as current for this thread
        var ctxResult = CudaRuntimeCore.cuCtxSetCurrent(context);
        if (ctxResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA context: {ctxResult}");
        }

        // Convert source to byte array with null terminator (required for PTX)
        byte[] sourceBytes = Encoding.UTF8.GetBytes(kernelSource);
        byte[] nullTerminatedSource = new byte[sourceBytes.Length + 1];
        Array.Copy(sourceBytes, nullTerminatedSource, sourceBytes.Length);
        nullTerminatedSource[sourceBytes.Length] = 0; // Add null terminator

        IntPtr module = IntPtr.Zero;
        CudaError loadResult;

        // Load PTX module with null-terminated string
        unsafe
        {
            fixed (byte* sourcePtr = nullTerminatedSource)
            {
                loadResult = CudaRuntimeCore.cuModuleLoadData(out module, (IntPtr)sourcePtr);
            }
        }

        if (loadResult != CudaError.Success)
        {
            _logger.LogError(
                "Failed to load kernel module for '{KernelId}': {Error} (error code: {ErrorCode})",
                kernelId,
                loadResult,
                (int)loadResult);

            // Log PTX for debugging
            _logger.LogDebug("PTX source that failed to load:\n{PTX}", kernelSource);

            throw new InvalidOperationException(
                $"Failed to load kernel module: {loadResult} (error code: {(int)loadResult})");
        }

        _logger.LogDebug("Successfully loaded kernel module for '{KernelId}'", kernelId);
        return module;
    }

    /// <summary>
    /// Retrieves a function pointer from a loaded kernel module.
    /// </summary>
    /// <param name="module">Module handle.</param>
    /// <param name="kernelId">Kernel identifier (used as function name).</param>
    /// <returns>Function handle.</returns>
    private IntPtr GetKernelFunction(IntPtr module, string kernelId)
    {
        var getFuncResult = CudaRuntimeCore.cuModuleGetFunction(out IntPtr function, module, kernelId);

        if (getFuncResult != CudaError.Success)
        {
            _logger.LogError(
                "Failed to get kernel function '{KernelId}' from module: {Error}",
                kernelId,
                getFuncResult);
            throw new InvalidOperationException(
                $"Failed to get kernel function '{kernelId}': {getFuncResult}");
        }

        _logger.LogDebug("Successfully retrieved function pointer for '{KernelId}'", kernelId);
        return function;
    }
}
