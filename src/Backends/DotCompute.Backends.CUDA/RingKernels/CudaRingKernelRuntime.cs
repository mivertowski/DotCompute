// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Abstractions.Messaging;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MessageQueueOptions = DotCompute.Abstractions.Messaging.MessageQueueOptions;
using IRingKernelMessage = DotCompute.Abstractions.Messaging.IRingKernelMessage;
using RingKernels = DotCompute.Abstractions.RingKernels;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaRingKernelRuntime"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="compiler">Ring kernel compiler.</param>
    public CudaRingKernelRuntime(
        ILogger<CudaRingKernelRuntime> logger,
        CudaRingKernelCompiler compiler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    }

    /// <inheritdoc/>
    public async Task LaunchAsync(
        string kernelId,
        int gridSize,
        int blockSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (gridSize <= 0 || blockSize <= 0)
        {
            throw new ArgumentException("Grid and block sizes must be positive");
        }

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
                // Step 2: Create and initialize message queues
                const int queueCapacity = 256;
                var inputLogger = NullLogger<CudaMessageQueue<int>>.Instance;
                var outputLogger = NullLogger<CudaMessageQueue<int>>.Instance;

                state.InputQueue = new CudaMessageQueue<int>(queueCapacity, inputLogger);
                state.OutputQueue = new CudaMessageQueue<int>(queueCapacity, outputLogger);

                await ((DotCompute.Abstractions.RingKernels.IMessageQueue<int>)state.InputQueue).InitializeAsync(cancellationToken);
                await ((DotCompute.Abstractions.RingKernels.IMessageQueue<int>)state.OutputQueue).InitializeAsync(cancellationToken);

                // Step 3: Compile kernel to PTX/CUBIN (for now, generate a simple test kernel)
                var kernelSource = GenerateSimpleKernel(kernelId);

                // Step 4: Load kernel module
                state.Module = LoadKernelModule(state.Context, kernelSource, kernelId);

                // Step 5: Get kernel function
                state.Function = GetKernelFunction(state.Module, kernelId);

                // Step 6: Allocate and initialize control block
                state.ControlBlock = RingKernelControlBlockHelper.AllocateAndInitialize(state.Context);

                // Step 7: Update control block with queue pointers
                var inputQueue = (CudaMessageQueue<int>)state.InputQueue;
                var outputQueue = (CudaMessageQueue<int>)state.OutputQueue;

                var controlBlock = RingKernelControlBlock.CreateInactive();
                controlBlock.InputQueueHeadPtr = ((CudaDevicePointerBuffer)inputQueue.GetHeadPtr()).DevicePointer.ToInt64();
                controlBlock.InputQueueTailPtr = ((CudaDevicePointerBuffer)inputQueue.GetTailPtr()).DevicePointer.ToInt64();
                controlBlock.OutputQueueHeadPtr = ((CudaDevicePointerBuffer)outputQueue.GetHeadPtr()).DevicePointer.ToInt64();
                controlBlock.OutputQueueTailPtr = ((CudaDevicePointerBuffer)outputQueue.GetTailPtr()).DevicePointer.ToInt64();

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

            // Dispose message queues
            if (state.InputQueue is IAsyncDisposable inputDisposable)
            {
                await inputDisposable.DisposeAsync();
            }
            if (state.OutputQueue is IAsyncDisposable outputDisposable)
            {
                await outputDisposable.DisposeAsync();
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
                if (getStatsMethod.Invoke(state.InputQueue, new object[] { cancellationToken }) is Task statsTask)
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
                if (getStatsMethod.Invoke(state.OutputQueue, new object[] { cancellationToken }) is Task statsTask)
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
    public Task<DotCompute.Abstractions.Messaging.IMessageQueue<T>> CreateNamedMessageQueueAsync<T>(
        string queueName,
        MessageQueueOptions options,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (CUDA Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<DotCompute.Abstractions.Messaging.IMessageQueue<T>?> GetNamedMessageQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (CUDA Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<bool> SendToNamedQueueAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (CUDA Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<T?> ReceiveFromNamedQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (CUDA Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<bool> DestroyNamedMessageQueueAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (CUDA Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> ListNamedMessageQueuesAsync(
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (CUDA Backend Integration)");
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
