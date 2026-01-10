// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Messaging;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.OpenCL.Interop;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;
using MessageQueueOptions = DotCompute.Abstractions.Messaging.MessageQueueOptions;
using IRingKernelMessage = DotCompute.Abstractions.Messaging.IRingKernelMessage;
using RingKernels = DotCompute.Abstractions.RingKernels;

namespace DotCompute.Backends.OpenCL.RingKernels;

/// <summary>
/// OpenCL runtime for managing persistent ring kernels.
/// </summary>
/// <remarks>
/// Provides lifecycle management for OpenCL-based ring kernels including:
/// - Kernel compilation and loading via clBuildProgram
/// - Activation/deactivation control with atomic operations
/// - Message routing and queue management
/// - Status monitoring and metrics collection
/// </remarks>
public sealed class OpenCLRingKernelRuntime : IRingKernelRuntime
{
    private readonly ILogger<OpenCLRingKernelRuntime> _logger;
    private readonly OpenCLRingKernelCompiler _compiler;
    private readonly OpenCLContext _context;
    private readonly ConcurrentDictionary<string, KernelState> _kernels = new();
    private bool _disposed;

    private sealed class KernelState
    {
        public required string KernelId { get; init; }
        public Program Program { get; set; }
        public Kernel Kernel { get; set; }
        public MemObject ControlBuffer { get; set; }
        public IntPtr HostControl { get; set; }
        public nuint[] GlobalWorkSize { get; set; } = Array.Empty<nuint>();
        public nuint[]? LocalWorkSize { get; set; }
        public bool IsLaunched { get; set; }
        public bool IsActive { get; set; }
        public DateTime LaunchTime { get; set; }
        public long MessagesProcessed { get; set; }
        public object? InputQueue { get; set; }
        public object? OutputQueue { get; set; }
        public CancellationTokenSource? KernelCts { get; set; }
        public OpenCLTelemetryBuffer? TelemetryBuffer { get; set; }
        public bool TelemetryEnabled { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLRingKernelRuntime"/> class.
    /// </summary>
    /// <param name="context">OpenCL context.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="compiler">Ring kernel compiler.</param>
    public OpenCLRingKernelRuntime(
        OpenCLContext context,
        ILogger<OpenCLRingKernelRuntime> logger,
        OpenCLRingKernelCompiler compiler)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
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
            "Launching ring kernel '{KernelId}' with globalSize={Grid}, localSize={Block}",
            kernelId,
            gridSize,
            blockSize);

        await Task.Run(async () =>
        {
            var state = new KernelState
            {
                KernelId = kernelId,
                GlobalWorkSize = new nuint[] { (nuint)gridSize },
                LocalWorkSize = new nuint[] { (nuint)blockSize },
                LaunchTime = DateTime.UtcNow,
                KernelCts = new CancellationTokenSource()
            };

            try
            {
                // Step 1: Create and initialize message queues
                const int queueCapacity = 256;
                var inputLogger = NullLogger<OpenCLMessageQueue<int>>.Instance;
                var outputLogger = NullLogger<OpenCLMessageQueue<int>>.Instance;

                state.InputQueue = new OpenCLMessageQueue<int>(queueCapacity, _context, inputLogger);
                state.OutputQueue = new OpenCLMessageQueue<int>(queueCapacity, _context, outputLogger);

                await ((DotCompute.Abstractions.RingKernels.IMessageQueue<int>)state.InputQueue).InitializeAsync(cancellationToken);
                await ((DotCompute.Abstractions.RingKernels.IMessageQueue<int>)state.OutputQueue).InitializeAsync(cancellationToken);

                // Step 2: Generate kernel source code
                var kernelDef = new KernelDefinition
                {
                    Name = kernelId
                };

                var config = new RingKernelConfig
                {
                    KernelId = kernelId,
                    Capacity = queueCapacity,
                    InputQueueSize = queueCapacity,
                    OutputQueueSize = queueCapacity,
                    Mode = RingKernelMode.Persistent,
                    MessagingStrategy = MessagePassingStrategy.SharedMemory,
                    Domain = RingKernelDomain.General,
                    GlobalWorkSize = new[] { gridSize },
                    LocalWorkSize = new[] { blockSize },
                    UseLocalMemory = true,
                    LocalMemorySize = 256
                };

                var kernelSource = _compiler.CompileToOpenCLC(kernelDef, string.Empty, config);

                // Step 3: Create OpenCL program
                state.Program = CreateProgram(kernelSource);

                // Step 4: Build program
                BuildProgram(state.Program, kernelId);

                // Step 5: Create kernel
                state.Kernel = CreateKernel(state.Program, $"{SanitizeKernelName(kernelId)}_kernel");

                // Step 6: Allocate and initialize control block
                var controlSize = (nuint)(sizeof(int) * 3 + sizeof(long)); // active, terminate, padding, msg_count
                state.ControlBuffer = _context.CreateBuffer(MemoryFlags.ReadWrite, controlSize);
                state.HostControl = Marshal.AllocHGlobal((int)controlSize);

                // Initialize control block (all zeros)
                unsafe
                {
                    new Span<byte>(state.HostControl.ToPointer(), (int)controlSize).Clear();
                }

                // Write initial control state to device
                var writeResult = OpenCLNative.clEnqueueWriteBuffer(
                    _context.CommandQueue,
                    state.ControlBuffer,
                    1u, // blocking
                    UIntPtr.Zero,
                    controlSize,
                    state.HostControl,
                    0,
                    null,
                    out _);

                OpenCLException.ThrowIfError((OpenCLError)writeResult, "Write control block");

                // Step 7: Set kernel arguments
                var inputQueue = (OpenCLMessageQueue<int>)state.InputQueue;
                var outputQueue = (OpenCLMessageQueue<int>)state.OutputQueue;

                SetKernelArguments(
                    state.Kernel,
                    inputQueue,
                    outputQueue,
                    state.ControlBuffer,
                    config);

                // Step 8: Launch persistent kernel (initially inactive)
                _logger.LogInformation(
                    "Launching persistent kernel '{KernelId}' with globalSize={Global}, localSize={Local}",
                    kernelId, state.GlobalWorkSize[0], state.LocalWorkSize?[0] ?? 0);

                // Note: For persistent mode, we would launch the kernel in a background task
                // For now, we mark it as launched but not actively executing
                // In production, this would continuously call clEnqueueNDRangeKernel

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
            _logger.LogWarning("Kernel '{KernelId}' already active", kernelId);
            return;
        }

        _logger.LogInformation("Activating ring kernel '{KernelId}'", kernelId);

        await Task.Run(() =>
        {
            // Set active flag in control block atomically
            SetControlFlag(state, 0, 1); // offset 0 = active flag
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
            _logger.LogWarning("Kernel '{KernelId}' not active", kernelId);
            return;
        }

        _logger.LogInformation("Deactivating ring kernel '{KernelId}'", kernelId);

        await Task.Run(() =>
        {
            // Clear active flag in control block atomically
            SetControlFlag(state, 0, 0); // offset 0 = active flag
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
            // Set terminate flag in control block
            SetControlFlag(state, sizeof(int), 1); // offset 4 = terminate flag

            // Wait for kernel to exit gracefully (with 5 second timeout)
            var terminated = await WaitForTerminationAsync(state, TimeSpan.FromSeconds(5), cancellationToken);

            if (!terminated)
            {
                _logger.LogWarning(
                    "Kernel '{KernelId}' did not terminate gracefully within timeout",
                    kernelId);
            }

            // Cleanup resources
            CleanupKernelState(state);

            // Dispose message queues
            if (state.InputQueue is IAsyncDisposable inputDisposable)
            {
                await inputDisposable.DisposeAsync();
            }
            if (state.OutputQueue is IAsyncDisposable outputDisposable)
            {
                await outputDisposable.DisposeAsync();
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
        ReadControlBlock(state, out var active, out var terminate, out var messagesProcessed);

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
            IsActive = active != 0,
            IsTerminating = terminate != 0,
            MessagesPending = messagesPending,
            MessagesProcessed = messagesProcessed,
            GridSize = (int)(state.GlobalWorkSize.Length > 0 ? state.GlobalWorkSize[0] : 0),
            BlockSize = (int)(state.LocalWorkSize?.Length > 0 ? state.LocalWorkSize[0] : 0),
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
        ReadControlBlock(state, out _, out _, out var messagesProcessed);

        var uptime = DateTime.UtcNow - state.LaunchTime;
        var throughput = uptime.TotalSeconds > 0
            ? messagesProcessed / uptime.TotalSeconds
            : 0;

        // Get queue statistics if available
        double inputUtilization = 0;
        double outputUtilization = 0;
        double avgLatencyUs = 0;

        if (state.InputQueue != null)
        {
            var statsMethod = state.InputQueue.GetType().GetMethod("GetStatisticsAsync");
            if (statsMethod != null && statsMethod.Invoke(state.InputQueue, null) is Task<MessageQueueStatistics> statsTask)
            {
                var stats = await statsTask;
                inputUtilization = stats.Utilization;
                avgLatencyUs = stats.AverageLatencyUs;
            }
        }

        if (state.OutputQueue != null)
        {
            var statsMethod = state.OutputQueue.GetType().GetMethod("GetStatisticsAsync");
            if (statsMethod != null && statsMethod.Invoke(state.OutputQueue, null) is Task<MessageQueueStatistics> statsTask)
            {
                var stats = await statsTask;
                outputUtilization = stats.Utilization;
            }
        }

        var metrics = new RingKernelMetrics
        {
            LaunchCount = 1, // Single launch per kernel
            MessagesSent = 0, // Output queue sent count
            MessagesReceived = messagesProcessed,
            AvgProcessingTimeMs = avgLatencyUs / 1000.0,
            ThroughputMsgsPerSec = throughput,
            InputQueueUtilization = inputUtilization,
            OutputQueueUtilization = outputUtilization
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
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
        {
            throw new ArgumentException("Capacity must be a positive power of 2", nameof(capacity));
        }

        _logger.LogInformation("Creating OpenCL message queue with capacity {Capacity} for type {Type}",
            capacity, typeof(T).Name);

        var logger = NullLogger<OpenCLMessageQueue<T>>.Instance;
        var queue = new OpenCLMessageQueue<T>(capacity, _context, logger);
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
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (OpenCL Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<DotCompute.Abstractions.Messaging.IMessageQueue<T>?> GetNamedMessageQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (OpenCL Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<bool> SendToNamedQueueAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (OpenCL Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<T?> ReceiveFromNamedQueueAsync<T>(
        string queueName,
        CancellationToken cancellationToken = default)
        where T : IRingKernelMessage
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (OpenCL Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<bool> DestroyNamedMessageQueueAsync(
        string queueName,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (OpenCL Backend Integration)");
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> ListNamedMessageQueuesAsync(
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Named message queues will be implemented in Phase 1.4 (OpenCL Backend Integration)");
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

        // Zero-copy read from mapped pinned memory (<1μs latency)
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
                var telemetryLogger = loggerFactory.CreateLogger<OpenCLTelemetryBuffer>();
                state.TelemetryBuffer = new OpenCLTelemetryBuffer(
                    _context.Context,
                    _context.CommandQueue,
                    telemetryLogger);
                state.TelemetryBuffer.Allocate();

                state.TelemetryEnabled = true;

                _logger.LogDebug(
                    "Telemetry enabled for kernel '{KernelId}' - buffer={Buffer:X16}, mapped={MappedPtr:X16}",
                    kernelId,
                    state.TelemetryBuffer.BufferObject.ToInt64(),
                    state.TelemetryBuffer.MappedPointer.ToInt64());
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

        _logger.LogDebug("Disposing OpenCL ring kernel runtime");

        // Terminate all kernels
        var kernelIds = _kernels.Keys.ToArray();
        foreach (var kernelId in kernelIds)
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
        _logger.LogInformation("OpenCL ring kernel runtime disposed");
    }

    // Helper methods

    private Program CreateProgram(string source)
    {
        var program = OpenCLNative.clCreateProgramWithSource(
            _context.Context,
            1,
            new[] { source },
            new[] { (nuint)source.Length },
            out var result);

        OpenCLException.ThrowIfError((OpenCLError)result, "Create program with source");
        return program;
    }

    private void BuildProgram(Program program, string kernelId)
    {
        var buildResult = OpenCLNative.clBuildProgram(
            program,
            0,
            null,
            null,
            IntPtr.Zero,
            IntPtr.Zero);

        if (buildResult != CLResultCode.Success)
        {
            // Get build log
            var buildLog = GetProgramBuildLog(program);
            _logger.LogError("Failed to build kernel '{KernelId}': {BuildLog}", kernelId, buildLog);
            throw new InvalidOperationException($"Failed to build kernel: {buildResult}\n{buildLog}");
        }

        _logger.LogDebug("Successfully built kernel '{KernelId}'", kernelId);
    }

    private string GetProgramBuildLog(Program program)
    {
        // Get device ID from context
        var getDevicesResult = OpenCLNative.clGetContextInfo(
            _context.Context,
            0x1081, // CL_CONTEXT_DEVICES
            UIntPtr.Zero,
            IntPtr.Zero,
            out var deviceIdsSize);

        if (getDevicesResult != CLResultCode.Success || deviceIdsSize == 0)
        {
            return "Unable to retrieve build log";
        }

        var deviceIdsPtr = Marshal.AllocHGlobal((int)deviceIdsSize);
        try
        {
            OpenCLNative.clGetContextInfo(
                _context.Context,
                0x1081, // CL_CONTEXT_DEVICES
                (UIntPtr)deviceIdsSize,
                deviceIdsPtr,
                out _);

            var deviceId = Marshal.ReadIntPtr(deviceIdsPtr);

            // Get build log size
            var getLogSizeResult = OpenCLNative.clGetProgramBuildInfo(
                program,
                deviceId,
                0x1183, // CL_PROGRAM_BUILD_LOG
                UIntPtr.Zero,
                IntPtr.Zero,
                out var logSize);

            if (getLogSizeResult != CLResultCode.Success || logSize == 0)
            {
                return "Empty build log";
            }

            // Get build log
            var logPtr = Marshal.AllocHGlobal((int)logSize);
            try
            {
                OpenCLNative.clGetProgramBuildInfo(
                    program,
                    deviceId,
                    0x1183, // CL_PROGRAM_BUILD_LOG
                    (UIntPtr)logSize,
                    logPtr,
                    out _);

                return Marshal.PtrToStringAnsi(logPtr) ?? "Unable to read build log";
            }
            finally
            {
                Marshal.FreeHGlobal(logPtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(deviceIdsPtr);
        }
    }

    private static Kernel CreateKernel(Program program, string kernelName)
    {
        var kernel = OpenCLNative.clCreateKernel(program, kernelName, out var result);
        OpenCLException.ThrowIfError((OpenCLError)result, $"Create kernel '{kernelName}'");
        return kernel;
    }

    private void SetKernelArguments(
        Kernel kernel,
        OpenCLMessageQueue<int> inputQueue,
        OpenCLMessageQueue<int> outputQueue,
        MemObject controlBuffer,
        RingKernelConfig config)
    {
        // Note: Kernel argument layout matches generated OpenCL C signature:
        // __kernel void kernel_name(
        //     __global MessageQueue* input_queue,
        //     __global MessageQueue* output_queue,
        //     __global KernelControl* control,
        //     __global void* user_data,
        //     int data_size,
        //     __local char* scratch)

        // For now, we set minimal arguments
        // In production, this would set all queue buffer pointers, control block, etc.
        _logger.LogDebug("Setting kernel arguments (placeholder implementation)");

        // Arg 0: input queue buffer (would need proper queue structure)
        // Arg 1: output queue buffer
        // Arg 2: control buffer
        unsafe
        {
            var controlBufferPtr = (IntPtr)(&controlBuffer);
            var result = OpenCLNative.clSetKernelArg(
                kernel,
                2,
                (nuint)IntPtr.Size,
                controlBufferPtr);

            OpenCLException.ThrowIfError((OpenCLError)result, "Set control buffer argument");
        }
    }

    private void SetControlFlag(KernelState state, int offset, int value)
    {
        unsafe
        {
            var ptr = (int*)state.HostControl.ToPointer();
            ptr[offset / sizeof(int)] = value;
        }

        // Write to device
        var size = (nuint)sizeof(int);
        var writeResult = OpenCLNative.clEnqueueWriteBuffer(
            _context.CommandQueue,
            state.ControlBuffer,
            1u, // blocking
            (UIntPtr)offset,
            size,
            state.HostControl + offset,
            0,
            null,
            out _);

        if (writeResult != CLResultCode.Success)
        {
            _logger.LogError("Failed to set control flag at offset {Offset}: {Error}", offset, writeResult);
        }
    }

    private void ReadControlBlock(KernelState state, out int active, out int terminate, out long messagesProcessed)
    {
        var controlSize = (nuint)(sizeof(int) * 3 + sizeof(long));

        var readResult = OpenCLNative.clEnqueueReadBuffer(
            _context.CommandQueue,
            state.ControlBuffer,
            1u, // blocking
            UIntPtr.Zero,
            controlSize,
            state.HostControl,
            0,
            null,
            out _);

        if (readResult != CLResultCode.Success)
        {
            _logger.LogError("Failed to read control block: {Error}", readResult);
            active = 0;
            terminate = 0;
            messagesProcessed = 0;
            return;
        }

        unsafe
        {
            var ptr = (int*)state.HostControl.ToPointer();
            active = ptr[0];
            terminate = ptr[1];
            messagesProcessed = ((long*)ptr)[2];
        }
    }

    private async Task<bool> WaitForTerminationAsync(
        KernelState state,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            ReadControlBlock(state, out _, out var terminate, out _);
            if (terminate != 0)
            {
                return true;
            }

            await Task.Delay(100, cancellationToken);
        }

        return false;
    }

    private static void CleanupKernelState(KernelState state)
    {
        if (state.HostControl != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(state.HostControl);
            state.HostControl = IntPtr.Zero;
        }

        if (state.ControlBuffer != default)
        {
            OpenCLNative.clReleaseMemObject(state.ControlBuffer);
            state.ControlBuffer = default;
        }

        if (state.Kernel != default)
        {
            OpenCLNative.clReleaseKernel(state.Kernel);
            state.Kernel = default;
        }

        if (state.Program != default)
        {
            OpenCLNative.clReleaseProgram(state.Program);
            state.Program = default;
        }

        // Dispose telemetry buffer
        if (state.TelemetryBuffer != null)
        {
            state.TelemetryBuffer.Dispose();
            state.TelemetryBuffer = null;
        }
    }

    private static string SanitizeKernelName(string kernelId)
    {
        var sanitized = new StringBuilder(kernelId.Length);
        foreach (var c in kernelId)
        {
            if (char.IsLetterOrDigit(c))
            {
                sanitized.Append(c);
            }
            else
            {
                sanitized.Append('_');
            }
        }
        return sanitized.ToString();
    }
}
