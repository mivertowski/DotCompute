// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Messaging;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MessageQueueOptions = DotCompute.Abstractions.Messaging.MessageQueueOptions;
using IRingKernelMessage = DotCompute.Abstractions.Messaging.IRingKernelMessage;
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
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private readonly ConcurrentDictionary<string, KernelState> _kernels = new();
    private bool _disposed;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalRingKernelRuntime"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="compiler">Ring kernel compiler.</param>
    public MetalRingKernelRuntime(
        ILogger<MetalRingKernelRuntime> logger,
        MetalRingKernelCompiler compiler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));

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
                // Step 1: Create and initialize message queues
                const int queueCapacity = 256;
                var inputLogger = NullLogger<MetalMessageQueue<int>>.Instance;
                var outputLogger = NullLogger<MetalMessageQueue<int>>.Instance;

                state.InputQueue = new MetalMessageQueue<int>(queueCapacity, inputLogger);
                state.OutputQueue = new MetalMessageQueue<int>(queueCapacity, outputLogger);

                await ((DotCompute.Abstractions.RingKernels.IMessageQueue<int>)state.InputQueue).InitializeAsync(cancellationToken);
                await ((DotCompute.Abstractions.RingKernels.IMessageQueue<int>)state.OutputQueue).InitializeAsync(cancellationToken);

                // Step 2: Generate simple test kernel MSL
                var mslSource = GenerateSimpleKernel(kernelId);

                // Step 3: Compile MSL to Metal library
                state.Library = CompileMSLLibrary(mslSource, kernelId);

                // Step 4: Get kernel function
                state.Function = MetalNative.GetFunction(state.Library, $"{kernelId}_kernel");
                if (state.Function == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Failed to get kernel function '{kernelId}_kernel' from library");
                }

                // Step 5: Create compute pipeline state
                IntPtr error = IntPtr.Zero;
                state.PipelineState = MetalNative.CreateComputePipelineState(_device, state.Function, ref error);
                if (state.PipelineState == IntPtr.Zero)
                {
                    string errorMsg = "Unknown error";
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

                // Step 7: Get queue pointers for Metal buffer binding
                var inputQueue = (MetalMessageQueue<int>)state.InputQueue;
                var outputQueue = (MetalMessageQueue<int>)state.OutputQueue;

                // Note: For now, we don't launch the actual persistent kernel
                // In production, this would dispatch the kernel in a separate thread/command buffer
                // that runs continuously until terminated

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
            bool terminated = false;

            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                // Check HasTerminated flag
                int hasTerminated = Marshal.ReadInt32(bufferPtr, sizeof(int) * 2);
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

            // Cleanup resources
            CleanupKernelState(state);

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

        // Read control buffer for current kernel state
        var bufferPtr = MetalNative.GetBufferContents(state.ControlBuffer);
        int isActive = Marshal.ReadInt32(bufferPtr, 0);
        int shouldTerminate = Marshal.ReadInt32(bufferPtr, sizeof(int));
        long messagesProcessed = Marshal.ReadInt64(bufferPtr, sizeof(int) * 2 + sizeof(int));

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
        long messagesProcessed = Marshal.ReadInt64(bufferPtr, sizeof(int) * 2 + sizeof(int));

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
    public Task<DotCompute.Abstractions.Messaging.IMessageQueue<T>> CreateNamedMessageQueueAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        string queueName,
        MessageQueueOptions options,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (Metal Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<DotCompute.Abstractions.Messaging.IMessageQueue<T>?> GetNamedMessageQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (Metal Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<bool> SendToNamedQueueAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (Metal Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<T?> ReceiveFromNamedQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (Metal Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<bool> DestroyNamedMessageQueueAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (Metal Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> ListNamedMessageQueuesAsync(
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (Metal Backend Integration)");
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

            throw new InvalidOperationException($"Failed to compile MSL library for kernel '{kernelId}'");
        }

        _logger.LogDebug("Successfully compiled MSL library for '{KernelId}'", kernelId);
        return library;
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
