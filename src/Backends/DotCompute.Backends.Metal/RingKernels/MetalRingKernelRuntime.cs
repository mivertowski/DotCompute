// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Messaging;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.Metal.Messaging;
using DotCompute.Backends.Metal.Native;
using DotCompute.Core.Messaging;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using IRingKernelMessage = DotCompute.Abstractions.Messaging.IRingKernelMessage;
using MessageQueueOptions = DotCompute.Abstractions.Messaging.MessageQueueOptions;
using RingKernels = DotCompute.Abstractions.RingKernels;

namespace DotCompute.Backends.Metal.RingKernels;

/// <summary>
/// Metal runtime for managing persistent ring kernels.
/// </summary>
/// <remarks>
/// Provides lifecycle management for Metal-based ring kernels including:
/// - Kernel compilation to MSL and execution
/// - Activation/deactivation control with atomic operations
/// - Message routing and queue management
/// - Status monitoring and metrics collection
/// </remarks>
public sealed class MetalRingKernelRuntime : IRingKernelRuntime
{
    private readonly ILogger<MetalRingKernelRuntime> _logger;
    private readonly MetalRingKernelCompiler _compiler;
    private readonly MessageQueueRegistry _queueRegistry;
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private readonly ConcurrentDictionary<string, KernelState> _kernels = new();
    private readonly ConcurrentDictionary<string, NamedQueueState> _namedQueues = new();
    private bool _disposed;

    /// <summary>
    /// State for named message queues with GPU bridge support.
    /// </summary>
    private sealed class NamedQueueState : IAsyncDisposable
    {
        public required string QueueName { get; init; }
        public required Type MessageType { get; init; }
        public required object Queue { get; init; }
        public object? Bridge { get; set; }
        public object? GpuBuffer { get; set; }

        public async ValueTask DisposeAsync()
        {
            if (GpuBuffer is IAsyncDisposable gpuDisposable)
            {
                await gpuDisposable.DisposeAsync();
            }

            if (Bridge is IAsyncDisposable bridgeDisposable)
            {
                await bridgeDisposable.DisposeAsync();
            }
            else if (Bridge is IDisposable bridgeSyncDisposable)
            {
                bridgeSyncDisposable.Dispose();
            }

            if (Queue is IAsyncDisposable queueDisposable)
            {
                await queueDisposable.DisposeAsync();
            }
            else if (Queue is IDisposable queueSyncDisposable)
            {
                queueSyncDisposable.Dispose();
            }
        }
    }

    private sealed class KernelState
    {
        public required string KernelId { get; init; }
        public IntPtr Library { get; set; }
        public IntPtr Function { get; set; }
        public IntPtr PipelineState { get; set; }
        public IntPtr ControlBuffer { get; set; }
        public IntPtr HostControl { get; set; }
        public int GridSize { get; set; }
        public int BlockSize { get; set; }
        public bool IsLaunched { get; set; }
        public bool IsActive { get; set; }
        public DateTime LaunchTime { get; set; }
        public long MessagesProcessed { get; set; }
        public object? InputQueue { get; set; }
        public object? OutputQueue { get; set; }
        public CancellationTokenSource? KernelCts { get; set; }
        public MetalTelemetryBuffer? TelemetryBuffer { get; set; }
        public bool TelemetryEnabled { get; set; }
        public Task? ExecutionTask { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalRingKernelRuntime"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="compiler">Ring kernel compiler.</param>
    /// <param name="queueRegistry">Optional message queue registry for named queue support.</param>
    public MetalRingKernelRuntime(
        ILogger<MetalRingKernelRuntime> logger,
        MetalRingKernelCompiler compiler,
        MessageQueueRegistry? queueRegistry = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _queueRegistry = queueRegistry ?? new MessageQueueRegistry(NullLogger<MessageQueueRegistry>.Instance);

        // Initialize Metal device and command queue
        _device = MetalNative.CreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal device");
        }

        _commandQueue = MetalNative.CreateCommandQueue(_device);
        if (_commandQueue == IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
            throw new InvalidOperationException("Failed to create Metal command queue");
        }

        _logger.LogInformation("Metal Ring Kernel Runtime initialized with device {DeviceId:X}", _device.ToInt64());
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

        _logger.LogInformation(
            "Launching ring kernel '{KernelId}' with gridSize={Grid}, threadgroupSize={Block}",
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

            try
            {
                // Step 1: Create and initialize message queues with proper types
                const int queueCapacity = 256;

                // Discover the input/output message type for this kernel
                Type inputType = GetKernelInputType(kernelId);
                Type outputType = GetKernelOutputType(kernelId);

                // Create properly-typed message queues using reflection
                state.InputQueue = CreateMessageQueue(inputType, queueCapacity);
                state.OutputQueue = CreateMessageQueue(outputType, queueCapacity);

                // Initialize queues using reflection
                var initMethod = state.InputQueue.GetType().GetMethod("InitializeAsync");
                await ((Task)initMethod!.Invoke(state.InputQueue, new object[] { cancellationToken })!).ConfigureAwait(false);

                initMethod = state.OutputQueue.GetType().GetMethod("InitializeAsync");
                await ((Task)initMethod!.Invoke(state.OutputQueue, new object[] { cancellationToken })!).ConfigureAwait(false);

                // Step 2: Generate MSL kernel code
                string mslSource;
                if (IsPageRankKernel(kernelId))
                {
                    // Use PageRank-specific transpiler
                    var config = new RingKernelConfig
                    {
                        KernelId = kernelId,
                        Mode = RingKernelMode.Persistent,
                        Domain = RingKernelDomain.GraphAnalytics,
                        Capacity = gridSize
                    };
                    // Create a kernel definition for PageRank
                    var kernelDef = new KernelDefinition(kernelId, string.Empty, $"{kernelId}_kernel");
                    mslSource = _compiler.CompileToMSL(kernelDef, string.Empty, config);
                    _logger.LogDebug("Generated PageRank-specific MSL for kernel '{KernelId}'", kernelId);
                }
                else
                {
                    // Fall back to simple test kernel
                    mslSource = GenerateSimpleKernel(kernelId);
                    _logger.LogDebug("Generated simple test MSL for kernel '{KernelId}'", kernelId);
                }

                // Step 3: Compile MSL to Metal library
                state.Library = CompileMSLLibrary(mslSource, kernelId);

                // Step 4: Get kernel function
                state.Function = MetalNative.GetFunction(state.Library, $"{kernelId}_kernel");
                if (state.Function == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Failed to get kernel function '{kernelId}_kernel' from library");
                }

                // Step 5: Create compute pipeline state
                var error = IntPtr.Zero;
                state.PipelineState = MetalNative.CreateComputePipelineState(_device, state.Function, ref error);
                if (state.PipelineState == IntPtr.Zero)
                {
                    var errorMsg = "Unknown error";
                    if (error != IntPtr.Zero)
                    {
                        var errorPtr = MetalNative.GetErrorLocalizedDescription(error);
                        if (errorPtr != IntPtr.Zero)
                        {
                            errorMsg = Marshal.PtrToStringAuto(errorPtr) ?? errorMsg;
                        }
                        MetalNative.ReleaseError(error);
                    }
                    throw new InvalidOperationException($"Failed to create compute pipeline state: {errorMsg}");
                }

                // Step 6: Allocate and initialize control block
                state.ControlBuffer = MetalNative.CreateBuffer(_device, (nuint)64, MetalStorageMode.Shared);
                state.HostControl = Marshal.AllocHGlobal(64);

                // Initialize control block to inactive state
                var controlData = new int[16]; // 64 bytes = 16 ints
                controlData[0] = 0; // IsActive = 0
                controlData[1] = 0; // ShouldTerminate = 0
                controlData[2] = 0; // HasTerminated = 0

                Marshal.Copy(controlData, 0, state.HostControl, 16);
                var bufferContents = MetalNative.GetBufferContents(state.ControlBuffer);
                Marshal.Copy(controlData, 0, bufferContents, 16);
                MetalNative.DidModifyRange(state.ControlBuffer, 0, 64);

                // Step 7: Queue pointers will be accessed via reflection in ExecutionTask

                // Step 8: Launch persistent kernel on GPU in background task
                _logger.LogDebug("Dispatching persistent kernel '{KernelId}' to GPU...", kernelId);
                state.ExecutionTask = Task.Run(() =>
                {
                    try
                    {
                        _logger.LogDebug("Creating command buffer for kernel '{KernelId}'", kernelId);

                        // Create command buffer
                        var commandBuffer = MetalNative.CreateCommandBuffer(_commandQueue);
                        if (commandBuffer == IntPtr.Zero)
                        {
                            throw new InvalidOperationException($"Failed to create command buffer for kernel '{kernelId}'");
                        }

                        // Create compute encoder
                        var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                        if (encoder == IntPtr.Zero)
                        {
                            throw new InvalidOperationException($"Failed to create compute encoder for kernel '{kernelId}'");
                        }

                        _logger.LogDebug("Setting pipeline state for kernel '{KernelId}'", kernelId);

                        // Set pipeline state
                        MetalNative.SetComputePipelineState(encoder, state.PipelineState);

                        // Bind buffers based on kernel type
                        if (IsPageRankKernel(kernelId))
                        {
                            // PageRank kernels expect 8 buffers:
                            // buffer(0): input_buffer (message data)
                            // buffer(1): input_head (atomic int*)
                            // buffer(2): input_tail (atomic int*)
                            // buffer(3): output_buffer (message data)
                            // buffer(4): output_head (atomic int*)
                            // buffer(5): output_tail (atomic int*)
                            // buffer(6): control (KernelControl*)
                            // buffer(7): queue_capacity (constant int&)

                            // Use reflection to access queue properties (queues are dynamically typed)
                            var inputDataBuffer = (IntPtr)state.InputQueue.GetType().GetProperty("DataBuffer")!.GetValue(state.InputQueue)!;
                            var inputHeadBuffer = (IntPtr)state.InputQueue.GetType().GetProperty("HeadBuffer")!.GetValue(state.InputQueue)!;
                            var inputTailBuffer = (IntPtr)state.InputQueue.GetType().GetProperty("TailBuffer")!.GetValue(state.InputQueue)!;
                            var outputDataBuffer = (IntPtr)state.OutputQueue.GetType().GetProperty("DataBuffer")!.GetValue(state.OutputQueue)!;
                            var outputHeadBuffer = (IntPtr)state.OutputQueue.GetType().GetProperty("HeadBuffer")!.GetValue(state.OutputQueue)!;
                            var outputTailBuffer = (IntPtr)state.OutputQueue.GetType().GetProperty("TailBuffer")!.GetValue(state.OutputQueue)!;

                            MetalNative.SetBuffer(encoder, inputDataBuffer, 0, 0);      // input_buffer
                            MetalNative.SetBuffer(encoder, inputHeadBuffer, 0, 1);      // input_head
                            MetalNative.SetBuffer(encoder, inputTailBuffer, 0, 2);      // input_tail
                            MetalNative.SetBuffer(encoder, outputDataBuffer, 0, 3);     // output_buffer
                            MetalNative.SetBuffer(encoder, outputHeadBuffer, 0, 4);     // output_head
                            MetalNative.SetBuffer(encoder, outputTailBuffer, 0, 5);     // output_tail
                            MetalNative.SetBuffer(encoder, state.ControlBuffer, 0, 6);        // control

                            // Create buffer for queue_capacity constant
                            var capacityBuffer = MetalNative.CreateBuffer(_device, (nuint)sizeof(int), MetalStorageMode.Shared);
                            var capacityPtr = MetalNative.GetBufferContents(capacityBuffer);
                            Marshal.WriteInt32(capacityPtr, queueCapacity);
                            MetalNative.DidModifyRange(capacityBuffer, 0, (long)sizeof(int));
                            MetalNative.SetBuffer(encoder, capacityBuffer, 0, 7);             // queue_capacity

                            _logger.LogDebug("[METAL-DEBUG] PageRank kernel buffers bound:");
                            _logger.LogDebug("  [0] input_buffer: 0x{Ptr:X}", inputDataBuffer.ToInt64());
                            _logger.LogDebug("  [1] input_head: 0x{Ptr:X}", inputHeadBuffer.ToInt64());
                            _logger.LogDebug("  [2] input_tail: 0x{Ptr:X}", inputTailBuffer.ToInt64());
                            _logger.LogDebug("  [3] output_buffer: 0x{Ptr:X}", outputDataBuffer.ToInt64());
                            _logger.LogDebug("  [4] output_head: 0x{Ptr:X}", outputHeadBuffer.ToInt64());
                            _logger.LogDebug("  [5] output_tail: 0x{Ptr:X}", outputTailBuffer.ToInt64());
                            _logger.LogDebug("  [6] control: 0x{Ptr:X}", state.ControlBuffer.ToInt64());
                            _logger.LogDebug("  [7] queue_capacity: {Capacity}", queueCapacity);
                        }
                        else
                        {
                            // Simple test kernels only need control buffer
                            MetalNative.SetBuffer(encoder, state.ControlBuffer, 0, 0);
                        }

                        _logger.LogDebug("Dispatching threadgroups: grid={GridSize}, threadgroup={BlockSize}", gridSize, blockSize);

                        // Dispatch threadgroups
                        var mtlGridSize = new MetalSize { width = (uint)gridSize, height = 1, depth = 1 };
                        var mtlBlockSize = new MetalSize { width = (uint)blockSize, height = 1, depth = 1 };
                        MetalNative.DispatchThreadgroups(encoder, mtlGridSize, mtlBlockSize);

                        // End encoding
                        MetalNative.EndEncoding(encoder);

                        _logger.LogDebug("Committing command buffer for kernel '{KernelId}'", kernelId);

                        // Commit command buffer
                        MetalNative.CommitCommandBuffer(commandBuffer);

                        _logger.LogDebug("Waiting for kernel '{KernelId}' completion...", kernelId);

                        // Wait for completion (blocking)
                        // NOTE: This will block until the kernel terminates via the control buffer
                        MetalNative.WaitUntilCompleted(commandBuffer);

                        _logger.LogInformation("Ring kernel '{KernelId}' execution completed normally", kernelId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ring kernel '{KernelId}' execution failed", kernelId);
                        throw;
                    }
                }, state.KernelCts.Token);

                state.IsLaunched = true;
                state.IsActive = false; // Starts inactive
                _kernels[kernelId] = state;

                _logger.LogInformation("Ring kernel '{KernelId}' launched successfully", kernelId);
            }
            catch
            {
                // Cleanup on failure
                CleanupKernelState(state);
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
            // Set active flag in control buffer atomically
            var bufferPtr = MetalNative.GetBufferContents(state.ControlBuffer);
            Marshal.WriteInt32(bufferPtr, 0, 1); // IsActive = 1
            MetalNative.DidModifyRange(state.ControlBuffer, 0, sizeof(int));

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
            // Clear active flag in control buffer atomically
            var bufferPtr = MetalNative.GetBufferContents(state.ControlBuffer);
            Marshal.WriteInt32(bufferPtr, 0, 0); // IsActive = 0
            MetalNative.DidModifyRange(state.ControlBuffer, 0, sizeof(int));

            state.IsActive = false;

            _logger.LogInformation("Ring kernel '{KernelId}' deactivated", kernelId);
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
            // Set terminate flag in control buffer
            var bufferPtr = MetalNative.GetBufferContents(state.ControlBuffer);
            Marshal.WriteInt32(bufferPtr, sizeof(int), 1); // ShouldTerminate = 1
            MetalNative.DidModifyRange(state.ControlBuffer, sizeof(int), sizeof(int));

            // Wait for kernel to exit gracefully (with 5 second timeout)
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            var terminated = false;

            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                // Check HasTerminated flag
                var hasTerminated = Marshal.ReadInt32(bufferPtr, sizeof(int) * 2);
                if (hasTerminated != 0)
                {
                    terminated = true;
                    break;
                }

                await Task.Delay(10, cancellationToken);
            }

            if (!terminated)
            {
                _logger.LogWarning(
                    "Kernel '{KernelId}' did not terminate gracefully within timeout",
                    kernelId);
            }

            // Wait for GPU execution task to complete (with timeout)
            if (state.ExecutionTask != null)
            {
                try
                {
                    _logger.LogDebug("Waiting for GPU execution task to complete for kernel '{KernelId}'", kernelId);

                    // Wait with 2 second timeout to avoid indefinite hang
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks - We own this task lifecycle
                    await state.ExecutionTask.WaitAsync(cts.Token).ConfigureAwait(false);
#pragma warning restore VSTHRD003
                    _logger.LogDebug("GPU execution task completed for kernel '{KernelId}'", kernelId);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("GPU execution task for kernel '{KernelId}' did not complete within timeout, proceeding with cleanup", kernelId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("GPU execution task for kernel '{KernelId}' did not complete within timeout, proceeding with cleanup", kernelId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GPU execution task for kernel '{KernelId}' threw exception during cleanup", kernelId);
                }
            }

            // Cleanup resources (with timeout to prevent indefinite hang)
            try
            {
                using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await Task.Run(() => CleanupKernelState(state), cleanupCts.Token).ConfigureAwait(false);
                _logger.LogDebug("Kernel '{KernelId}' resources cleaned up successfully", kernelId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Kernel '{KernelId}' cleanup did not complete within timeout, proceeding anyway", kernelId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kernel '{KernelId}' cleanup threw exception, proceeding anyway", kernelId);
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

        if (state.InputQueue == null)
        {
            throw new InvalidOperationException($"Input queue not initialized for kernel '{kernelId}'");
        }

        // Cast to the properly-typed queue interface
        // The queue should have been created with the correct type parameter based on kernel input type
        var queue = (DotCompute.Abstractions.RingKernels.IMessageQueue<T>)(object)state.InputQueue;
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

        if (state.OutputQueue == null)
        {
            throw new InvalidOperationException($"Output queue not initialized for kernel '{kernelId}'");
        }

        // Cast to the properly-typed queue interface
        // The queue should have been created with the correct type parameter based on kernel output type
        var queue = (DotCompute.Abstractions.RingKernels.IMessageQueue<T>)(object)state.OutputQueue;

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

        // Read control buffer for current kernel state
        var bufferPtr = MetalNative.GetBufferContents(state.ControlBuffer);
        var isActive = Marshal.ReadInt32(bufferPtr, 0);
        var shouldTerminate = Marshal.ReadInt32(bufferPtr, sizeof(int));
        var messagesProcessed = Marshal.ReadInt64(bufferPtr, sizeof(int) * 2 + sizeof(int));

        // Get input queue count if available
        var messagesPending = 0;
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
            IsActive = isActive != 0,
            IsTerminating = shouldTerminate != 0,
            MessagesPending = messagesPending,
            MessagesProcessed = messagesProcessed,
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

        // Read control buffer for accurate message counts
        var bufferPtr = MetalNative.GetBufferContents(state.ControlBuffer);
        var messagesProcessed = Marshal.ReadInt64(bufferPtr, sizeof(int) * 2 + sizeof(int));

        var uptime = DateTime.UtcNow - state.LaunchTime;
        var throughput = uptime.TotalSeconds > 0
            ? messagesProcessed / uptime.TotalSeconds
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
                        var avgLatencyProp = stats.GetType().GetProperty("AverageLatencyUs");

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
            MessagesSent = messagesProcessed,
            MessagesReceived = messagesProcessed,
            AvgProcessingTimeMs = avgProcessingTimeMs,
            ThroughputMsgsPerSec = throughput,
            InputQueueUtilization = inputUtilization,
            OutputQueueUtilization = outputUtilization,
            PeakMemoryBytes = 0, // Requires tracking during allocation
            CurrentMemoryBytes = 0, // Requires Metal memory query
            GpuUtilizationPercent = 0 // Requires Metal performance counters
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
        var logger = NullLogger<MetalMessageQueue<T>>.Instance;

        var queue = new MetalMessageQueue<T>(capacity, logger);
        await queue.InitializeAsync(cancellationToken);

        return queue;
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Named queue creation requires dynamic type handling")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Message type must be accessible at runtime")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Ring kernel bridges require dynamic type creation")]
    public async Task<DotCompute.Abstractions.Messaging.IMessageQueue<T>> CreateNamedMessageQueueAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        string queueName,
        MessageQueueOptions options,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation(
            "Creating named message queue '{QueueName}' for type {MessageType} with capacity {Capacity}",
            queueName, typeof(T).Name, options.Capacity);

        // Check if queue already exists
        if (_namedQueues.ContainsKey(queueName))
        {
            throw new InvalidOperationException($"Queue '{queueName}' already exists");
        }

        // Create bridge with GPU buffer support
        var (namedQueue, bridge, gpuBuffer) = await MetalMessageQueueBridgeFactory.CreateBridgeForMessageTypeAsync(
            typeof(T),
            queueName,
            options,
            _device,
            _logger,
            cancellationToken);

        // Create state entry
        var state = new NamedQueueState
        {
            QueueName = queueName,
            MessageType = typeof(T),
            Queue = namedQueue,
            Bridge = bridge,
            GpuBuffer = gpuBuffer
        };

        // Register in local tracking and global registry
        if (!_namedQueues.TryAdd(queueName, state))
        {
            // Cleanup if concurrent add failed
            await state.DisposeAsync();
            throw new InvalidOperationException($"Failed to register queue '{queueName}' - concurrent creation detected");
        }

        // Register in global registry for cross-backend access
        if (!_queueRegistry.TryRegister<T>(queueName, (DotCompute.Abstractions.Messaging.IMessageQueue<T>)namedQueue, "Metal"))
        {
            _logger.LogWarning(
                "Queue '{QueueName}' already registered in global registry, local tracking succeeded",
                queueName);
        }

        _logger.LogInformation(
            "Successfully created named queue '{QueueName}' with GPU buffer ({BufferSize} bytes)",
            queueName, options.Capacity * (65536 + 256));

        return (DotCompute.Abstractions.Messaging.IMessageQueue<T>)namedQueue;
    }

    /// <inheritdoc/>
    public Task<DotCompute.Abstractions.Messaging.IMessageQueue<T>?> GetNamedMessageQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // First check local tracking
        if (_namedQueues.TryGetValue(queueName, out var state))
        {
            if (state.MessageType != typeof(T))
            {
                _logger.LogWarning(
                    "Queue '{QueueName}' exists but has type {ActualType}, expected {ExpectedType}",
                    queueName, state.MessageType.Name, typeof(T).Name);
                return Task.FromResult<DotCompute.Abstractions.Messaging.IMessageQueue<T>?>(null);
            }

            return Task.FromResult<DotCompute.Abstractions.Messaging.IMessageQueue<T>?>(
                (DotCompute.Abstractions.Messaging.IMessageQueue<T>)state.Queue);
        }

        // Fall back to global registry for cross-backend queues
        var queue = _queueRegistry.TryGet<T>(queueName);
        return Task.FromResult(queue);
    }

    /// <inheritdoc/>
    public async Task<bool> SendToNamedQueueAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Get queue
        var queue = await GetNamedMessageQueueAsync<T>(queueName, cancellationToken);
        if (queue == null)
        {
            _logger.LogWarning("Cannot send to non-existent queue '{QueueName}'", queueName);
            return false;
        }

        // Enqueue message
        return queue.TryEnqueue(message, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<T?> ReceiveFromNamedQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Get queue
        var queue = await GetNamedMessageQueueAsync<T>(queueName, cancellationToken);
        if (queue == null)
        {
            _logger.LogWarning("Cannot receive from non-existent queue '{QueueName}'", queueName);
            return default;
        }

        // Dequeue message
        if (queue.TryDequeue(out var message))
        {
            return message;
        }

        return default;
    }

    /// <inheritdoc/>
    public async Task<bool> DestroyNamedMessageQueueAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Destroying named message queue '{QueueName}'", queueName);

        // Remove from local tracking
        if (!_namedQueues.TryRemove(queueName, out var state))
        {
            _logger.LogWarning("Queue '{QueueName}' not found for destruction", queueName);
            return false;
        }

        // Remove from global registry
        _queueRegistry.TryUnregister(queueName, disposeQueue: false);

        // Dispose GPU resources and queue
        await state.DisposeAsync();

        _logger.LogInformation("Successfully destroyed named queue '{QueueName}'", queueName);
        return true;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> ListNamedMessageQueuesAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Return Metal backend queues
        var metalQueues = _namedQueues.Keys.ToList();
        return Task.FromResult<IReadOnlyCollection<string>>(metalQueues);
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

        // Zero-copy read from shared memory (<1μs latency on Apple Silicon)
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
                var telemetryLogger = loggerFactory.CreateLogger<MetalTelemetryBuffer>();
                state.TelemetryBuffer = new MetalTelemetryBuffer(_device, telemetryLogger);
                state.TelemetryBuffer.Allocate();

                state.TelemetryEnabled = true;

                _logger.LogDebug(
                    "Telemetry enabled for kernel '{KernelId}' - buffer={Buffer:X16}, contents={ContentsPtr:X16}",
                    kernelId,
                    state.TelemetryBuffer.BufferObject.ToInt64(),
                    state.TelemetryBuffer.ContentsPointer.ToInt64());
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

        _logger.LogInformation("Disposing Metal ring kernel runtime");

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

        // Cleanup all named message queues
        foreach (var queueName in _namedQueues.Keys.ToList())
        {
            try
            {
                await DestroyNamedMessageQueueAsync(queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error destroying queue '{QueueName}' during disposal", queueName);
            }
        }

        // Release Metal resources
        if (_commandQueue != IntPtr.Zero)
        {
            MetalNative.ReleaseCommandQueue(_commandQueue);
        }

        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }

        _disposed = true;
        _logger.LogInformation("Metal ring kernel runtime disposed");
    }

    /// <summary>
    /// Checks if a kernel ID corresponds to a PageRank kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier to check.</param>
    /// <returns>True if this is a PageRank kernel, false otherwise.</returns>
    private static bool IsPageRankKernel(string kernelId)
    {
        return kernelId == "metal_pagerank_contribution_sender" ||
               kernelId == "metal_pagerank_rank_aggregator" ||
               kernelId == "metal_pagerank_convergence_checker";
    }

    /// <summary>
    /// Discovers the input message type for a given kernel ID.
    /// </summary>
    /// <param name="kernelId">Kernel identifier to look up.</param>
    /// <returns>The message type that this kernel expects as input.</returns>
    private static Type GetKernelInputType(string kernelId)
    {
        // Pattern matching for known PageRank kernels
        // Uses Type.GetType() to avoid direct assembly references
        // NOTE: PageRank types are linked into DotCompute.Backends.Metal.Benchmarks assembly
        Type? resolvedType = kernelId switch
        {
            "metal_pagerank_contribution_sender" =>
                Type.GetType("DotCompute.Samples.RingKernels.PageRank.Metal.MetalGraphNode, DotCompute.Backends.Metal.Benchmarks"),
            "metal_pagerank_rank_aggregator" =>
                Type.GetType("DotCompute.Samples.RingKernels.PageRank.Metal.PageRankContribution, DotCompute.Backends.Metal.Benchmarks"),
            "metal_pagerank_convergence_checker" =>
                Type.GetType("DotCompute.Samples.RingKernels.PageRank.Metal.RankAggregationResult, DotCompute.Backends.Metal.Benchmarks"),
            _ => typeof(int) // Fallback for test kernels
        };

        return resolvedType ?? typeof(int);
    }

    /// <summary>
    /// Gets the output message type for a given kernel.
    /// </summary>
    /// <param name="kernelId">The kernel identifier.</param>
    /// <returns>The output message type for this kernel.</returns>
    private static Type GetKernelOutputType(string kernelId)
    {
        // Pattern matching for known PageRank kernels
        // Maps kernel ID to its output message type
        Type? resolvedType = kernelId switch
        {
            "metal_pagerank_contribution_sender" =>
                Type.GetType("DotCompute.Samples.RingKernels.PageRank.Metal.PageRankContribution, DotCompute.Backends.Metal.Benchmarks"),
            "metal_pagerank_rank_aggregator" =>
                Type.GetType("DotCompute.Samples.RingKernels.PageRank.Metal.RankAggregationResult, DotCompute.Backends.Metal.Benchmarks"),
            "metal_pagerank_convergence_checker" =>
                Type.GetType("DotCompute.Samples.RingKernels.PageRank.Metal.ConvergenceCheckResult, DotCompute.Backends.Metal.Benchmarks"),
            _ => typeof(int) // Fallback for test kernels
        };

        return resolvedType ?? typeof(int);
    }

    /// <summary>
    /// Creates a message queue with the specified message type using reflection.
    /// </summary>
    /// <param name="messageType">The type of messages this queue will handle.</param>
    /// <param name="capacity">Queue capacity (number of message slots).</param>
    /// <param name="loggerFactory">Logger factory for creating typed loggers.</param>
    /// <returns>A MetalMessageQueue instance typed for the specified message type.</returns>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL2060", Justification = "Generic CreateLogger<T> method is guaranteed to be available for all message queue types")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL2071", Justification = "Message types are known at compile time through kernel discovery")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Generic queue types are instantiated for each kernel message type during runtime initialization")]
    private static object CreateMessageQueue([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type messageType, int capacity, ILoggerFactory? loggerFactory = null)
    {
        // Create the generic queue type: MetalMessageQueue<TMessage>
        var queueType = typeof(MetalMessageQueue<>).MakeGenericType(messageType);

        // Create logger using the non-generic CreateLogger method
        // This creates an ILogger which can be used by any ILogger<T>
        var nullLoggerFactory = NullLoggerFactory.Instance;
        var logger = nullLoggerFactory.CreateLogger(queueType.FullName ?? queueType.Name);

        // Create the queue instance
        // Note: MetalMessageQueue expects ILogger<MetalMessageQueue<T>>, but ILogger is covariant-compatible
        return Activator.CreateInstance(queueType, capacity, logger)
            ?? throw new InvalidOperationException($"Failed to create MetalMessageQueue for type {messageType.Name}");
    }

    /// <summary>
    /// Generates a simple persistent kernel for testing.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <returns>MSL source code.</returns>
    private static string GenerateSimpleKernel(string kernelId)
    {
        return $@"#include <metal_stdlib>
#include <metal_atomic>
using namespace metal;

struct KernelControl {{
    atomic_int active;
    atomic_int terminate;
    atomic_int terminated;
}};

kernel void {kernelId}_kernel(
    device KernelControl* control [[buffer(0)]],
    uint thread_id [[thread_position_in_grid]])
{{
    // Persistent kernel loop
    while (true) {{
        // Check for termination
        if (atomic_load_explicit(&control->terminate, memory_order_acquire) == 1) {{
            break;
        }}

        // Wait for activation
        while (atomic_load_explicit(&control->active, memory_order_acquire) == 0) {{
            if (atomic_load_explicit(&control->terminate, memory_order_acquire) == 1) {{
                atomic_store_explicit(&control->terminated, 1, memory_order_release);
                return;
            }}
            threadgroup_barrier(mem_flags::mem_none);
        }}

        // Process (placeholder)
    }}

    // Set terminated flag
    atomic_store_explicit(&control->terminated, 1, memory_order_release);
}}
";
    }

    /// <summary>
    /// Compiles MSL source to a Metal library.
    /// </summary>
    /// <param name="mslSource">MSL source code.</param>
    /// <param name="kernelId">Kernel identifier for logging.</param>
    /// <returns>Metal library handle.</returns>
    private IntPtr CompileMSLLibrary(string mslSource, string kernelId)
    {
        var library = MetalNative.CreateLibraryWithSource(_device, mslSource);

        if (library == IntPtr.Zero)
        {
            _logger.LogError(
                "Failed to compile MSL library for kernel '{KernelId}'",
                kernelId);

            // Log MSL for debugging
            _logger.LogDebug("MSL source that failed to compile:\n{MSL}", mslSource);

            // Save MSL to file for debugging
            var mslPath = $"/tmp/failed_msl_{kernelId}.metal";
            try
            {
                System.IO.File.WriteAllText(mslPath, mslSource);
                _logger.LogError("MSL source saved to: {Path}", mslPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save MSL source to file");
            }

            throw new InvalidOperationException($"Failed to compile MSL library for kernel '{kernelId}'");
        }

        _logger.LogDebug("Successfully compiled MSL library for '{KernelId}'", kernelId);
        return library;
    }

    /// <summary>
    /// Gets the Metal buffer pointer for a kernel's output queue.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <returns>Metal buffer pointer for the output queue.</returns>
    /// <exception cref="ArgumentException">Thrown if kernel not found or not launched.</exception>
    /// <remarks>
    /// This method is used by MetalKernelRoutingTableManager to configure K2K routing.
    /// The returned pointer can be used to bind the output queue to other kernels' routing tables.
    /// </remarks>
    public IntPtr GetOutputQueueBufferPointer(string kernelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        if (!state.IsLaunched)
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' is not launched. Call LaunchAsync first.");
        }

        if (state.OutputQueue is MetalMessageQueue<int> queue)
        {
            // Get the underlying Metal buffer pointer from the queue
            var buffer = queue.GetBuffer();
            if (buffer is MetalDeviceBufferWrapper wrapper)
            {
                return wrapper.MetalBuffer;
            }
        }

        throw new InvalidOperationException($"Output queue for kernel '{kernelId}' is not a MetalMessageQueue or doesn't expose buffer pointer");
    }

    /// <summary>
    /// Cleans up resources for a kernel state.
    /// </summary>
    /// <param name="state">Kernel state to cleanup.</param>
    private static void CleanupKernelState(KernelState state)
    {
        // Free control buffer
        if (state.ControlBuffer != IntPtr.Zero)
        {
            MetalNative.ReleaseBuffer(state.ControlBuffer);
            state.ControlBuffer = IntPtr.Zero;
        }

        if (state.HostControl != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(state.HostControl);
            state.HostControl = IntPtr.Zero;
        }

        // Release pipeline state
        if (state.PipelineState != IntPtr.Zero)
        {
            MetalNative.ReleaseComputePipelineState(state.PipelineState);
            state.PipelineState = IntPtr.Zero;
        }

        // Release function
        if (state.Function != IntPtr.Zero)
        {
            MetalNative.ReleaseFunction(state.Function);
            state.Function = IntPtr.Zero;
        }

        // Release library
        if (state.Library != IntPtr.Zero)
        {
            MetalNative.ReleaseLibrary(state.Library);
            state.Library = IntPtr.Zero;
        }

        // Dispose message queues (async disposal done synchronously for cleanup)
#pragma warning disable VSTHRD002 // Synchronous wait acceptable during cleanup/disposal
        if (state.InputQueue is IAsyncDisposable inputDisposable)
        {
            inputDisposable.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
        }
        if (state.OutputQueue is IAsyncDisposable outputDisposable)
        {
            outputDisposable.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
        }
#pragma warning restore VSTHRD002

        // Dispose telemetry buffer
        if (state.TelemetryBuffer != null)
        {
            state.TelemetryBuffer.Dispose();
            state.TelemetryBuffer = null;
        }
    }
}
