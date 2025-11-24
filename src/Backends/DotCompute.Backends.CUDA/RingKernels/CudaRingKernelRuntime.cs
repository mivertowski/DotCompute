// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Abstractions.Messaging;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
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
    private readonly ConcurrentDictionary<string, Assembly> _registeredAssemblies = new();
    private readonly object _contextLock = new();
    private IntPtr _sharedContext;
    private int _contextRefCount;
    private bool _disposed;

    private sealed class KernelState
    {
        public string KernelId { get; init; } = string.Empty;
        public IntPtr Context { get; set; }
        public IntPtr Module { get; set; }
        public IntPtr Function { get; set; }
        public IntPtr ControlBlock { get; set; }  // Device pointer for GPU access
        public IntPtr ControlBlockHostPtr { get; set; }  // Host pointer for zero-copy reads (pinned/unified memory)
        public bool IsControlBlockUnifiedMemory { get; set; }  // True if using cudaMallocManaged
        public RingKernelControlBlockHelper.AsyncControlBlock? AsyncControlBlock { get; set; }  // WSL2 async control block
        public IntPtr Stream { get; set; }
        public IntPtr ControlStream { get; set; }  // Non-blocking stream for control block operations
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

        // EventDriven mode support (WSL2 compatibility)
        public bool IsEventDrivenMode { get; set; }  // True if using EventDriven mode (finite iterations)
        public DiscoveredRingKernel? DiscoveredKernel { get; set; }  // Kernel metadata for relaunch
        public Task? RelaunchTask { get; set; }  // Background task for kernel relaunching
        public int EventDrivenMaxIterations { get; set; } = 1000;  // Max iterations before kernel exits
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

    /// <summary>
    /// Registers an assembly containing ring kernel definitions for discovery during kernel launch.
    /// </summary>
    /// <param name="assembly">The assembly containing ring kernel classes with [RingKernel] attributes.</param>
    /// <returns>True if the assembly was newly registered; false if it was already registered.</returns>
    public bool RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var added = _registeredAssemblies.TryAdd(assembly.FullName ?? assembly.GetName().Name ?? "unknown", assembly);
        if (added)
        {
            _logger.LogInformation("Registered assembly '{AssemblyName}' for ring kernel discovery", assembly.GetName().Name);
        }
        return added;
    }

    /// <summary>
    /// Gets all assemblies registered for ring kernel discovery.
    /// </summary>
    /// <returns>Array of registered assemblies.</returns>
    public Assembly[] GetRegisteredAssemblies() => _registeredAssemblies.Values.ToArray();

    /// <summary>
    /// Gets or creates the shared CUDA context for this runtime instance.
    /// Uses reference counting to manage context lifecycle across multiple kernels.
    /// </summary>
    private IntPtr GetOrCreateSharedContext()
    {
        lock (_contextLock)
        {
            if (_sharedContext == IntPtr.Zero)
            {
                // Initialize CUDA Driver API (tolerates already initialized)
                var initResult = CudaRuntimeCore.cuInit(0);
                if (initResult is not CudaError.Success and not ((CudaError)4))
                {
                    throw new InvalidOperationException($"Failed to initialize CUDA: {initResult}");
                }

                // IMPORTANT: Do NOT reuse existing contexts from cuCtxGetCurrent()
                // After a previous runtime's context is destroyed, cuCtxGetCurrent() may return
                // a stale/invalid handle causing InvalidContext errors in subsequent operations.
                // Always create a fresh context for each runtime instance to ensure proper isolation.
                {
                    // Get CUDA device using Driver API (consistent with cuCtxCreate)
                    var getDeviceResult = CudaRuntime.cuDeviceGet(out int device, 0);
                    if (getDeviceResult != CudaError.Success)
                    {
                        throw new InvalidOperationException($"Failed to get CUDA device: {getDeviceResult}");
                    }

                    // Create shared context
                    var ctxResult = CudaRuntimeCore.cuCtxCreate(out _sharedContext, 0, device);
                    if (ctxResult != CudaError.Success)
                    {
                        // WSL2 fallback: Use Runtime API to initialize CUDA, which creates a context
                        _logger.LogDebug("cuCtxCreate failed ({Error}), trying Runtime API fallback for WSL2", ctxResult);

                        // cudaSetDevice(0) + cudaFree(0) forces Runtime API context creation
                        var setDeviceResult = CudaRuntime.cudaSetDevice(0);
                        if (setDeviceResult != CudaError.Success)
                        {
                            throw new InvalidOperationException($"Failed to create CUDA context: {ctxResult} (cudaSetDevice also failed: {setDeviceResult})");
                        }

                        // Sync to ensure context is created
                        var syncResult = CudaRuntime.cudaDeviceSynchronize();
                        if (syncResult != CudaError.Success)
                        {
                            _logger.LogWarning("cudaDeviceSynchronize after fallback returned: {Error}", syncResult);
                        }

                        // Now get the current context that Runtime API created
                        var getCurrentResult = CudaRuntimeCore.cuCtxGetCurrent(out _sharedContext);
                        if (getCurrentResult != CudaError.Success || _sharedContext == IntPtr.Zero)
                        {
                            throw new InvalidOperationException($"Failed to create CUDA context: {ctxResult} (fallback also failed: {getCurrentResult})");
                        }

                        _logger.LogDebug("Created CUDA context via Runtime API fallback: 0x{Context:X}", _sharedContext.ToInt64());
                    }
                    else
                    {
                        _logger.LogDebug("Created shared CUDA context: 0x{Context:X}", _sharedContext.ToInt64());
                    }
                }
            }

            // Increment reference count
            _contextRefCount++;

            _logger.LogTrace(
                "Shared context acquired: RefCount={RefCount}, Context=0x{Context:X}",
                _contextRefCount, _sharedContext.ToInt64());

            return _sharedContext;
        }
    }

    /// <summary>
    /// Releases a reference to the shared CUDA context.
    /// Destroys the context when reference count reaches zero.
    /// </summary>
    private void ReleaseSharedContext()
    {
        lock (_contextLock)
        {
            if (_contextRefCount > 0)
            {
                _contextRefCount--;

                _logger.LogTrace(
                    "Shared context released: RefCount={RefCount}, Context=0x{Context:X}",
                    _contextRefCount, _sharedContext.ToInt64());

                if (_contextRefCount == 0 && _sharedContext != IntPtr.Zero)
                {
                    _logger.LogDebug("Destroying shared CUDA context: 0x{Context:X}", _sharedContext.ToInt64());

                    var destroyResult = CudaRuntimeCore.cuCtxDestroy(_sharedContext);
                    if (destroyResult != CudaError.Success)
                    {
                        _logger.LogWarning(
                            "Failed to destroy CUDA context 0x{Context:X}: {Error}",
                            _sharedContext.ToInt64(), destroyResult);
                    }

                    _sharedContext = IntPtr.Zero;
                }
            }
        }
    }

    /// <summary>
    /// Comprehensive cleanup of kernel resources on launch failure.
    /// Ensures all allocated resources are properly disposed.
    /// </summary>
    private void CleanupKernelResourcesOnFailure(KernelState state)
    {
        // Cleanup CUDA streams
        if (state.Stream != IntPtr.Zero)
        {
            try
            {
                CudaApi.cuStreamDestroy(state.Stream);
            }
            catch
            {
                // Ignore cleanup errors during failure recovery
            }

            state.Stream = IntPtr.Zero;
        }

        if (state.ControlStream != IntPtr.Zero)
        {
            try
            {
                CudaApi.cuStreamDestroy(state.ControlStream);
            }
            catch
            {
                // Ignore cleanup errors during failure recovery
            }

            state.ControlStream = IntPtr.Zero;
        }

        // Cleanup async control block (WSL2 mode)
        if (state.AsyncControlBlock != null)
        {
            try
            {
                state.AsyncControlBlock.Dispose();
            }
            catch
            {
                // Ignore cleanup errors during failure recovery
            }

            state.AsyncControlBlock = null;
        }

        // Cleanup control block memory
        if (state.ControlBlockHostPtr != IntPtr.Zero || state.ControlBlock != IntPtr.Zero)
        {
            try
            {
                RingKernelControlBlockHelper.FreePinned(
                    state.ControlBlockHostPtr,
                    state.ControlBlock,
                    state.IsControlBlockUnifiedMemory);
            }
            catch
            {
                // Ignore cleanup errors during failure recovery
            }

            state.ControlBlockHostPtr = IntPtr.Zero;
            state.ControlBlock = IntPtr.Zero;
        }

        // Cleanup telemetry buffer
        if (state.TelemetryBuffer != null)
        {
            try
            {
                state.TelemetryBuffer.Dispose();
            }
            catch
            {
                // Ignore cleanup errors during failure recovery
            }

            state.TelemetryBuffer = null;
        }

        // Cleanup GPU buffers - prefer synchronous IDisposable if available
        state.GpuInputBuffer = CleanupDisposableResource(state.GpuInputBuffer);
        state.GpuOutputBuffer = CleanupDisposableResource(state.GpuOutputBuffer);

        // Cleanup bridges
        state.InputBridge = CleanupDisposableResource(state.InputBridge);
        state.OutputBridge = CleanupDisposableResource(state.OutputBridge);

        // Cleanup queues
        state.InputQueue = CleanupDisposableResource(state.InputQueue);
        state.OutputQueue = CleanupDisposableResource(state.OutputQueue);

        // Cleanup cancellation token source
        if (state.KernelCts != null)
        {
            try
            {
                state.KernelCts.Cancel();
                state.KernelCts.Dispose();
            }
            catch
            {
                // Ignore cleanup errors during failure recovery
            }

            state.KernelCts = null;
        }

        // Unload CUDA module
        if (state.Module != IntPtr.Zero)
        {
            try
            {
                CudaApi.cuModuleUnload(state.Module);
            }
            catch
            {
                // Ignore cleanup errors during failure recovery
            }

            state.Module = IntPtr.Zero;
        }

        // Release context reference
        if (state.Context != IntPtr.Zero)
        {
            try
            {
                ReleaseSharedContext();
            }
            catch
            {
                // Ignore cleanup errors during failure recovery
            }
        }
    }

    /// <summary>
    /// Helper to cleanup a resource that may implement IDisposable or IAsyncDisposable.
    /// Prefers synchronous disposal when available to avoid sync-over-async patterns.
    /// </summary>
    /// <returns>Always returns null for assignment to the resource variable.</returns>
    private static T? CleanupDisposableResource<T>(T? resource) where T : class
    {
        if (resource == null)
        {
            return null;
        }

        try
        {
            // Prefer synchronous IDisposable if available
            if (resource is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (resource is IAsyncDisposable asyncDisposable)
            {
                // Fallback to async disposal with sync wait - suppress VSTHRD002 since this is
                // intentional cleanup code in a synchronous context (exception handling)
#pragma warning disable VSTHRD002 // Synchronously waiting in cleanup is intentional
                asyncDisposable.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
#pragma warning restore VSTHRD002
            }
        }
        catch
        {
            // Ignore cleanup errors during failure recovery
        }

        return null;
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

        // Check if kernel is already launched
        if (_kernels.ContainsKey(kernelId))
        {
            throw new InvalidOperationException(
                $"Kernel '{kernelId}' is already launched. Terminate it before launching again.");
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

            // Step 1: Get or create shared CUDA context
            state.Context = GetOrCreateSharedContext();

            try
            {
                // Step 2: Detect message types from kernel signature (using registered assemblies)
                var assembliesToSearch = !_registeredAssemblies.IsEmpty
                    ? _registeredAssemblies.Values.ToArray()
                    : null;
                var (inputType, outputType) = CudaMessageQueueBridgeFactory.DetectMessageTypes(kernelId, assembliesToSearch);

                _logger.LogDebug(
                    "Detected message types for kernel '{KernelId}': Input={InputType}, Output={OutputType}",
                    kernelId, inputType.Name, outputType.Name);

                // Step 3: Create message queues based on type
                // If IRingKernelMessage → Create bridge infrastructure
                // If unmanaged → Create direct GPU queues
                var isInputBridged = typeof(IRingKernelMessage).IsAssignableFrom(inputType);
                var isOutputBridged = typeof(IRingKernelMessage).IsAssignableFrom(outputType);

                // Detect WSL2 once for both input and output queue configuration
                var isWsl2 = RingKernelControlBlockHelper.IsRunningInWsl2();

                if (isInputBridged)
                {
                    // Create GPU ring buffer bridge for IRingKernelMessage input type
                    var inputQueueName = $"ringkernel_{inputType.Name}_{kernelId}_input";

                    var (namedQueue, gpuBuffer, bridge) = CudaMessageQueueBridgeFactory.CreateGpuRingBufferBridgeForMessageType(
                        messageType: inputType,
                        deviceId: 0,  // TODO: Get from context
                        capacity: options.QueueCapacity,
                        messageSize: 65792,  // Default: ~64KB per message
                        useUnifiedMemory: !isWsl2,  // Unified memory for non-WSL2, device memory for WSL2
                        enableDmaTransfer: isWsl2,  // DMA transfer only on WSL2
                        logger: _logger);

                    state.InputQueue = namedQueue;
                    state.InputBridge = bridge;
                    state.GpuInputBuffer = gpuBuffer;

                    // Register named queue with registry for SendToNamedQueueAsync access
                    _registry.TryRegister(inputType, inputQueueName, namedQueue, "CUDA");

                    _logger.LogInformation(
                        "Created GPU ring buffer bridge '{QueueName}' for type {MessageType} (WSL2={IsWsl2}, UnifiedMem={UseUnified}, DMA={EnableDma})",
                        inputQueueName, inputType.Name, isWsl2, !isWsl2, isWsl2);
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
                    // Create GPU ring buffer bridge for IRingKernelMessage output type (Device → Host)
                    var outputQueueName = $"ringkernel_{outputType.Name}_{kernelId}_output";

                    var (namedQueue, gpuBuffer, bridge) = CudaMessageQueueBridgeFactory.CreateGpuRingBufferBridgeForMessageType(
                        messageType: outputType,
                        deviceId: 0,  // TODO: Get from context
                        capacity: options.QueueCapacity,
                        messageSize: 65792,  // Default: ~64KB per message
                        useUnifiedMemory: !isWsl2,  // Unified memory for non-WSL2, device memory for WSL2
                        enableDmaTransfer: isWsl2,  // DMA transfer only on WSL2
                        logger: _logger);

                    state.OutputQueue = namedQueue;
                    state.OutputBridge = bridge;
                    state.GpuOutputBuffer = gpuBuffer;

                    // Register named queue with registry
                    _registry.TryRegister(outputType, outputQueueName, namedQueue, "CUDA");

                    _logger.LogInformation(
                        "Created GPU ring buffer bridge '{QueueName}' for type {MessageType} (Direction=DeviceToHost, WSL2={IsWsl2}, UnifiedMem={UseUnified}, DMA={EnableDma})",
                        outputQueueName, outputType.Name, isWsl2, !isWsl2, isWsl2);
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

                // Step 3: Compile C# ring kernel to PTX using full compilation pipeline
                _logger.LogDebug("Compiling ring kernel '{KernelId}' with automatic C# to CUDA translation", kernelId);

                // Use the same assembliesToSearch from Step 2 (already computed above)
                var compiledKernel = await _compiler.CompileRingKernelAsync(
                    kernelId,
                    state.Context,
                    options: null,
                    assemblies: assembliesToSearch,
                    cancellationToken);

                if (compiledKernel == null || !compiledKernel.IsValid)
                {
                    var registeredCount = _registeredAssemblies.Count;
                    var hint = registeredCount == 0
                        ? " No assemblies registered - use RegisterAssembly() to add kernel assemblies."
                        : $" {registeredCount} assemblies registered.";
                    throw new InvalidOperationException(
                        $"Failed to compile ring kernel '{kernelId}'.{hint} " +
                        "Ensure the [RingKernel] method exists and has a corresponding message handler.");
                }

                _logger.LogInformation(
                    "Successfully compiled ring kernel '{KernelId}' (PTX size: {PtxSize} bytes, Module: {Module:X}, Function: {Function:X})",
                    kernelId,
                    compiledKernel.PtxBytes.Length,
                    (long)compiledKernel.ModuleHandle,
                    (long)compiledKernel.FunctionPointer);

                // Use the already-loaded module and function from the compiler
                state.Module = compiledKernel.ModuleHandle;
                state.Function = compiledKernel.FunctionPointer;

                // Store discovered kernel metadata for relaunch support
                state.DiscoveredKernel = compiledKernel.DiscoveredKernel;

                // Determine if EventDriven mode should be used (WSL2 auto-detection or explicit)
                // Note: isWsl2 variable already declared earlier for queue configuration
                var explicitEventDriven = compiledKernel.DiscoveredKernel?.Mode == Abstractions.RingKernels.RingKernelMode.EventDriven;
                state.IsEventDrivenMode = isWsl2 || explicitEventDriven;
                state.EventDrivenMaxIterations = compiledKernel.DiscoveredKernel?.EventDrivenMaxIterations ?? 1000;

                if (state.IsEventDrivenMode)
                {
                    _logger.LogInformation(
                        "EventDriven mode enabled for kernel '{KernelId}' (WSL2={IsWsl2}, Explicit={Explicit}, MaxIterations={MaxIterations})",
                        kernelId, isWsl2, explicitEventDriven, state.EventDrivenMaxIterations);
                }

                // Step 6: Allocate and initialize control block using pinned memory for zero-copy reads
                // Pinned memory allows CPU to read control block without blocking on cooperative kernels
                var pinnedControlBlock = RingKernelControlBlockHelper.AllocateAndInitializePinned(state.Context);
                state.ControlBlock = pinnedControlBlock.DevicePointer;
                state.ControlBlockHostPtr = pinnedControlBlock.HostPointer;
                state.IsControlBlockUnifiedMemory = pinnedControlBlock.IsUnifiedMemory;

                // WSL2 Async Mode: If pinned/unified memory failed (HostPointer == 0), use async control block
                // This enables non-blocking control block communication via pinned staging buffer + events
                if (pinnedControlBlock.HostPointer == IntPtr.Zero && RingKernelControlBlockHelper.IsRunningInWsl2())
                {
                    // Free the device-only control block, we'll replace it with async version
                    RingKernelControlBlockHelper.FreePinned(pinnedControlBlock.HostPointer, pinnedControlBlock.DevicePointer, pinnedControlBlock.IsUnifiedMemory);

                    // Allocate async control block with pinned staging buffer and events
                    var asyncBlock = RingKernelControlBlockHelper.AllocateAsyncControlBlock(state.Context, state.ControlStream);
                    state.AsyncControlBlock = asyncBlock;
                    state.ControlBlock = asyncBlock.DevicePointer;
                    state.ControlBlockHostPtr = IntPtr.Zero; // No direct host access
                    state.IsControlBlockUnifiedMemory = false;

                    // WSL2 fix: Restore Driver API context after Runtime API operations
                    // The async control block uses Runtime API (cudaMalloc) which may switch to primary context
                    // We need to restore the Driver API context for kernel launch
                    var ctxRestoreResult = CudaRuntimeCore.cuCtxSetCurrent(state.Context);
                }

                // Step 7: Update control block with queue pointers
                // For bridged queues (host-side MessageQueue), use GPU buffer from bridge
                // For direct GPU queues (CudaMessageQueue), use queue head/tail pointers
                if (state.InputQueue == null || state.OutputQueue == null)
                {
                    throw new InvalidOperationException("Input and output queues must be initialized before accessing control block");
                }

                var controlBlock = RingKernelControlBlock.CreateInactive();

                // Check if we're using GPU ring buffers with head/tail atomics
                var isGpuRingBufferInput = state.GpuInputBuffer is IGpuRingBuffer;
                var isGpuRingBufferOutput = state.GpuOutputBuffer is IGpuRingBuffer;

                if (isGpuRingBufferInput)
                {
                    // NEW: GPU ring buffer with atomic head/tail counters
                    // Set control block pointers to the GPU's head/tail atomics
                    var gpuBuffer = (IGpuRingBuffer)state.GpuInputBuffer!;
                    controlBlock.InputQueueHeadPtr = gpuBuffer.DeviceHeadPtr.ToInt64();
                    controlBlock.InputQueueTailPtr = gpuBuffer.DeviceTailPtr.ToInt64();

                    _logger.LogInformation(
                        "Input queue using GPU ring buffer: head=0x{Head:X}, tail=0x{Tail:X}, " +
                        "capacity={Capacity}, messageSize={MessageSize}, unified={Unified}",
                        controlBlock.InputQueueHeadPtr, controlBlock.InputQueueTailPtr,
                        gpuBuffer.Capacity, gpuBuffer.MessageSize, gpuBuffer.IsUnifiedMemory);
                }
                else if (state.GpuInputBuffer != null)
                {
                    // OLD: Raw byte buffer (legacy bridged queue)
                    // The kernel expects MessageQueue structs with head/tail/buffer/capacity fields,
                    // but the bridge provides raw byte buffers. Setting to 0 ensures the kernel's
                    // null checks prevent invalid memory access. The bridge handles message
                    // transfer via separate mechanism (direct buffer writes with explicit offsets).
                    controlBlock.InputQueueHeadPtr = 0;
                    controlBlock.InputQueueTailPtr = 0;
                    _logger.LogDebug("Using legacy bridged input queue - kernel queue access disabled (nullptr)");
                }
                else
                {
                    // NO GPU BUFFER: Direct GPU queues (CudaMessageQueue<T>)
                    // CudaMessageQueue stores head/tail as separate int* allocations via GetHeadPtr()/GetTailPtr(),
                    // but the kernel expects a contiguous MessageQueue struct with {head, tail, buffer, capacity}.
                    // This is a design mismatch that requires future work to resolve.
                    // For now, setting to 0 ensures the kernel's null checks prevent invalid memory access.
                    // Message passing will use the bridge mechanism once properly implemented for unmanaged types.
                    controlBlock.InputQueueHeadPtr = 0;
                    controlBlock.InputQueueTailPtr = 0;
                    _logger.LogDebug("Using direct GPU input queue - kernel queue access disabled (nullptr) pending MessageQueue struct implementation");
                }

                if (isGpuRingBufferOutput)
                {
                    // NEW: GPU ring buffer with atomic head/tail counters
                    // Set control block pointers to the GPU's head/tail atomics
                    var gpuBuffer = (IGpuRingBuffer)state.GpuOutputBuffer!;
                    controlBlock.OutputQueueHeadPtr = gpuBuffer.DeviceHeadPtr.ToInt64();
                    controlBlock.OutputQueueTailPtr = gpuBuffer.DeviceTailPtr.ToInt64();

                    _logger.LogInformation(
                        "Output queue using GPU ring buffer: head=0x{Head:X}, tail=0x{Tail:X}, " +
                        "capacity={Capacity}, messageSize={MessageSize}, unified={Unified}",
                        controlBlock.OutputQueueHeadPtr, controlBlock.OutputQueueTailPtr,
                        gpuBuffer.Capacity, gpuBuffer.MessageSize, gpuBuffer.IsUnifiedMemory);
                }
                else if (state.GpuOutputBuffer != null)
                {
                    // OLD: Raw byte buffer (legacy bridged queue)
                    // Same reasoning as input - kernel expects MessageQueue structs, not raw buffers.
                    controlBlock.OutputQueueHeadPtr = 0;
                    controlBlock.OutputQueueTailPtr = 0;
                    _logger.LogDebug("Using legacy bridged output queue - kernel queue access disabled (nullptr)");
                }
                else
                {
                    // NO GPU BUFFER: Direct GPU queues (CudaMessageQueue<T>)
                    // Same reasoning as input - kernel expects contiguous MessageQueue struct, not separate int*.
                    controlBlock.OutputQueueHeadPtr = 0;
                    controlBlock.OutputQueueTailPtr = 0;
                    _logger.LogDebug("Using direct GPU output queue - kernel queue access disabled (nullptr) pending MessageQueue struct implementation");
                }

                // Use zero-copy path for pinned/unified memory, otherwise use device copy
                if (state.ControlBlockHostPtr != IntPtr.Zero)
                {
                    RingKernelControlBlockHelper.WritePinned(state.ControlBlockHostPtr, controlBlock);
                }
                else
                {
                    RingKernelControlBlockHelper.Write(state.Context, state.ControlBlock, controlBlock);

                    // WSL2 fix: Restore Driver API context after Write (uses Runtime API internally)
                    var ctxRestoreResult = CudaRuntimeCore.cuCtxSetCurrent(state.Context);
                }

                // Step 8: Create prioritized stream for kernel execution
                var cudaPriority = MapStreamPriority(options.StreamPriority, state.Context);
                IntPtr stream = IntPtr.Zero;
                var streamResult = CudaApi.cuStreamCreateWithPriority(ref stream, 0, cudaPriority);
                if (streamResult != CudaError.Success)
                {
                    throw new InvalidOperationException(
                        $"Failed to create CUDA stream with priority {options.StreamPriority}: {streamResult}");
                }
                state.Stream = stream;

                _logger.LogDebug(
                    "Created CUDA stream for kernel '{KernelId}' with priority {Priority} (CUDA priority={CudaPriority})",
                    kernelId, options.StreamPriority, cudaPriority);

                // Create non-blocking control stream for control block operations
                // Flag 1 = CU_STREAM_NON_BLOCKING - doesn't synchronize with stream 0
                IntPtr controlStream = IntPtr.Zero;
                var controlStreamResult = CudaApi.cuStreamCreate(ref controlStream, 1);
                if (controlStreamResult != CudaError.Success)
                {
                    _logger.LogWarning("Failed to create control stream: {Error}, will use sync operations", controlStreamResult);
                }
                else
                {
                    state.ControlStream = controlStream;
                }

                // Step 9: Validate cooperative kernel support
                var getDevResult = CudaRuntimeCore.cuCtxGetDevice(out int deviceId);
                if (getDevResult != CudaError.Success)
                {
                    throw new InvalidOperationException($"Failed to get current device: {getDevResult}");
                }

                CudaDeviceProperties deviceProps = default;
                var getPropResult = CudaRuntime.cudaGetDeviceProperties(ref deviceProps, deviceId);
                if (getPropResult != CudaError.Success)
                {
                    throw new InvalidOperationException($"Failed to get device properties: {getPropResult}");
                }

                // Check compute capability (cooperative kernels require 6.0+)
                if (deviceProps.Major < 6)
                {
                    throw new NotSupportedException(
                        $"Cooperative kernel launches require compute capability 6.0+, but device has {deviceProps.Major}.{deviceProps.Minor}");
                }

                _logger.LogDebug(
                    "Device validation passed: CC={Major}.{Minor}, CooperativeKernel=Supported",
                    deviceProps.Major, deviceProps.Minor);

                // Step 10: Launch persistent kernel (initially inactive)
                _logger.LogInformation(
                    "Launching persistent kernel '{KernelId}' with grid={Grid}, block={Block}, priority={Priority}",
                    kernelId, gridSize, blockSize, options.StreamPriority);

                // Set CUDA context for kernel launch
                var setCtxResult = CudaRuntimeCore.cuCtxSetCurrent(state.Context);
                if (setCtxResult != CudaError.Success)
                {
                    throw new InvalidOperationException($"Failed to set CUDA context: {setCtxResult}");
                }

                // Marshal kernel parameters: single pointer to the control block
                // The kernel signature is: __global__ void kernel(RingKernelControlBlock* control_block)
                //
                // CRITICAL: CUDA kernel launch requires:
                // 1. Allocate unmanaged memory to hold the pointer value
                // 2. Write the pointer value into that memory
                // 3. Create an array of addresses pointing to the parameter values
                // 4. Pin the array and pass its address to cuLaunchCooperativeKernel
                IntPtr ptrStorage = IntPtr.Zero;
                GCHandle argPtrsHandle = default;

                try
                {
                    unsafe
                    {
                        // Allocate memory to hold the control block pointer
                        ptrStorage = Marshal.AllocHGlobal(sizeof(IntPtr));
                        *(IntPtr*)ptrStorage = state.ControlBlock;

                        // Create array of parameter pointers and pin it
                        var kernelParams = new IntPtr[] { ptrStorage };
                        argPtrsHandle = GCHandle.Alloc(kernelParams, GCHandleType.Pinned);

                        _logger.LogDebug(
                            "Marshaled kernel parameters: ControlBlock=0x{ControlBlock:X}, ParamStorage=0x{ParamStorage:X}",
                            state.ControlBlock.ToInt64(), ptrStorage.ToInt64());

                        // WSL2 workaround: Use regular kernel launch instead of cooperative
                        // In WSL2, cooperative kernels occupy all SMs which blocks memory copies.
                        // Regular kernels allow control block operations to work.
                        bool useNonCooperative = state.ControlBlockHostPtr == IntPtr.Zero; // No zero-copy = WSL2 mode
                        CudaError launchResult;

                        if (useNonCooperative)
                        {
                            // Regular kernel launch - allows memory copies while kernel runs
                            launchResult = CudaRuntime.cuLaunchKernel(
                                state.Function,
                                (uint)gridSize, 1, 1,    // Grid dimensions (1D grid)
                                (uint)blockSize, 1, 1,   // Block dimensions (1D blocks)
                                0,                        // Shared memory bytes
                                state.Stream,             // Stream
                                argPtrsHandle.AddrOfPinnedObject(),  // Parameter array
                                IntPtr.Zero);             // Extra options (null)
                        }
                        else
                        {
                            // Full cooperative kernel with grid sync support
                            launchResult = CudaRuntimeCore.cuLaunchCooperativeKernel(
                                state.Function,
                                (uint)gridSize, 1, 1,    // Grid dimensions (1D grid)
                                (uint)blockSize, 1, 1,   // Block dimensions (1D blocks)
                                0,                        // Shared memory bytes (0 for now)
                                state.Stream,             // Prioritized stream
                                argPtrsHandle.AddrOfPinnedObject());  // Address of pinned parameter array
                        }

                        if (launchResult != CudaError.Success)
                        {
                            throw new InvalidOperationException(
                                $"Failed to launch cooperative kernel '{kernelId}': {launchResult}");
                        }
                    }
                }
                finally
                {
                    // Free the parameter storage
                    if (ptrStorage != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(ptrStorage);
                    }

                    // Free the pinned handle
                    if (argPtrsHandle.IsAllocated)
                    {
                        argPtrsHandle.Free();
                    }
                }

                state.IsLaunched = true;
                state.IsActive = false; // Starts inactive (kernel will loop waiting for IsActive flag)
                _kernels[kernelId] = state;

                // Start EventDriven relaunch loop if needed
                // This background task monitors kernel termination and relaunches automatically
                if (state.IsEventDrivenMode)
                {
                    _logger.LogInformation(
                        "Starting EventDriven relaunch loop for kernel '{KernelId}' (MaxIterations={MaxIterations})",
                        kernelId, state.EventDrivenMaxIterations);

                    state.RelaunchTask = Task.Run(async () =>
                    {
                        await EventDrivenRelaunchLoopAsync(state, state.KernelCts!.Token).ConfigureAwait(false);
                    });
                }

                _logger.LogInformation(
                    "{Mode} kernel '{KernelId}' launched successfully (running in background, inactive)",
                    state.IsEventDrivenMode ? "EventDriven" : "Persistent",
                    kernelId);

                _logger.LogInformation("Ring kernel '{KernelId}' launched successfully", kernelId);
            }
            catch
            {
                // Comprehensive cleanup on failure - dispose all allocated resources
                CleanupKernelResourcesOnFailure(state);
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
            // Set active flag in control block - use appropriate path based on allocation type
            if (state.AsyncControlBlock != null)
            {
                // WSL2 async mode - non-blocking via pinned staging buffer + events
                RingKernelControlBlockHelper.SetActiveNonBlocking(state.AsyncControlBlock, true);
            }
            else if (state.ControlBlockHostPtr != IntPtr.Zero)
            {
                // Zero-copy write - non-blocking, works with cooperative kernels
                RingKernelControlBlockHelper.SetActivePinned(state.ControlBlockHostPtr, true);
            }
            else
            {
                // Fallback to device memory copy (may block on cooperative kernels)
                RingKernelControlBlockHelper.SetActive(state.Context, state.ControlBlock, true);
            }
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
            // Clear active flag in control block - use appropriate path based on allocation type
            if (state.AsyncControlBlock != null)
            {
                // WSL2 async mode - non-blocking via pinned staging buffer + events
                RingKernelControlBlockHelper.SetActiveNonBlocking(state.AsyncControlBlock, false);
            }
            else if (state.ControlBlockHostPtr != IntPtr.Zero)
            {
                // Zero-copy write - non-blocking, works with cooperative kernels
                RingKernelControlBlockHelper.SetActivePinned(state.ControlBlockHostPtr, false);
            }
            else
            {
                // Fallback to device memory copy (may block on cooperative kernels)
                RingKernelControlBlockHelper.SetActive(state.Context, state.ControlBlock, false);
            }
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

        // Stop the EventDriven relaunch loop first (if running)
        if (state.IsEventDrivenMode && state.KernelCts != null)
        {
            _logger.LogDebug("Stopping EventDriven relaunch loop for kernel '{KernelId}'", kernelId);
            try
            {
                await state.KernelCts.CancelAsync();

                // Wait for the relaunch task to complete (with timeout)
                if (state.RelaunchTask != null)
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    try
                    {
                        await state.RelaunchTask.WaitAsync(timeoutCts.Token);
                        _logger.LogDebug("EventDriven relaunch loop stopped for kernel '{KernelId}'", kernelId);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("EventDriven relaunch loop did not stop gracefully for kernel '{KernelId}'", kernelId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping EventDriven relaunch loop for kernel '{KernelId}'", kernelId);
            }
        }

        await Task.Run(async () =>
        {
            // Set terminate flag in control block - use appropriate path based on allocation type
            if (state.AsyncControlBlock != null)
            {
                // WSL2 async mode - non-blocking via pinned staging buffer + events
                RingKernelControlBlockHelper.SetTerminateNonBlocking(state.AsyncControlBlock);
            }
            else if (state.ControlBlockHostPtr != IntPtr.Zero)
            {
                // Zero-copy write - non-blocking, works with cooperative kernels
                RingKernelControlBlockHelper.SetTerminatePinned(state.ControlBlockHostPtr);
            }
            else
            {
                // Fallback to device memory copy (may block on cooperative kernels)
                RingKernelControlBlockHelper.SetTerminate(state.Context, state.ControlBlock);
            }

            // Wait for kernel to exit gracefully (with 5 second timeout)
            // Use appropriate read method based on allocation type
            bool terminated;
            if (state.AsyncControlBlock != null)
            {
                // WSL2 async mode - use async wait
                terminated = await RingKernelControlBlockHelper.WaitForTerminationAsync(
                    state.AsyncControlBlock,
                    TimeSpan.FromSeconds(5),
                    cancellationToken);
            }
            else if (state.ControlBlockHostPtr != IntPtr.Zero)
            {
                terminated = await WaitForTerminationPinnedAsync(state.ControlBlockHostPtr, TimeSpan.FromSeconds(5), cancellationToken);
            }
            else
            {
                terminated = await RingKernelControlBlockHelper.WaitForTerminationAsync(
                    state.Context,
                    state.ControlBlock,
                    TimeSpan.FromSeconds(5),
                    cancellationToken);
            }

            if (!terminated)
            {
                _logger.LogWarning(
                    "Kernel '{KernelId}' did not terminate gracefully within timeout",
                    kernelId);
            }

            // Destroy kernel execution stream
            if (state.Stream != IntPtr.Zero)
            {
                var streamDestroyResult = CudaApi.cuStreamDestroy(state.Stream);
                if (streamDestroyResult != CudaError.Success)
                {
                    _logger.LogWarning(
                        "Failed to destroy stream for kernel '{KernelId}': {Error}",
                        kernelId,
                        streamDestroyResult);
                }
                state.Stream = IntPtr.Zero;
                _logger.LogDebug("Destroyed stream for kernel '{KernelId}'", kernelId);
            }

            // Destroy control stream
            if (state.ControlStream != IntPtr.Zero)
            {
                var controlStreamDestroyResult = CudaApi.cuStreamDestroy(state.ControlStream);
                if (controlStreamDestroyResult != CudaError.Success)
                {
                    _logger.LogWarning(
                        "Failed to destroy control stream for kernel '{KernelId}': {Error}",
                        kernelId,
                        controlStreamDestroyResult);
                }
                state.ControlStream = IntPtr.Zero;
                _logger.LogDebug("Destroyed control stream for kernel '{KernelId}'", kernelId);
            }

            // Dispose async control block if present (WSL2 mode)
            if (state.AsyncControlBlock != null)
            {
                state.AsyncControlBlock.Dispose();
                state.AsyncControlBlock = null;
                _logger.LogDebug("Disposed async control block for kernel '{KernelId}'", kernelId);
            }

            // Free control block (use FreePinned which handles both pinned and fallback modes)
            if (state.ControlBlockHostPtr != IntPtr.Zero || state.ControlBlock != IntPtr.Zero)
            {
                RingKernelControlBlockHelper.FreePinned(state.ControlBlockHostPtr, state.ControlBlock, state.IsControlBlockUnifiedMemory);
                state.ControlBlockHostPtr = IntPtr.Zero;
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

            // Release shared CUDA context reference
            if (state.Context != IntPtr.Zero)
            {
                ReleaseSharedContext();
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

        // Read control block using appropriate method based on allocation type:
        // - AsyncControlBlock: WSL2 mode with async copies and cached last value
        // - Pinned host memory: Zero-copy read (non-blocking while cooperative kernel runs)
        // - Device memory: Synchronous copy (may block on cooperative kernels)
        RingKernelControlBlock controlBlock;
        if (state.AsyncControlBlock != null)
        {
            // WSL2 async mode - returns cached value and initiates new async read
            controlBlock = RingKernelControlBlockHelper.ReadNonBlocking(state.AsyncControlBlock);
        }
        else if (state.ControlBlockHostPtr != IntPtr.Zero)
        {
            controlBlock = RingKernelControlBlockHelper.ReadPinned(state.ControlBlockHostPtr);
        }
        else
        {
            controlBlock = RingKernelControlBlockHelper.Read(state.Context, state.ControlBlock, state.ControlStream);
        }

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

        // Read control block using appropriate method based on allocation type
        RingKernelControlBlock controlBlock;
        if (state.AsyncControlBlock != null)
        {
            // WSL2 async mode - returns cached value and initiates new async read
            controlBlock = RingKernelControlBlockHelper.ReadNonBlocking(state.AsyncControlBlock);
        }
        else if (state.ControlBlockHostPtr != IntPtr.Zero)
        {
            controlBlock = RingKernelControlBlockHelper.ReadPinned(state.ControlBlockHostPtr);
        }
        else
        {
            controlBlock = RingKernelControlBlockHelper.Read(state.Context, state.ControlBlock, state.ControlStream);
        }

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

    /// <summary>
    /// Reads the control block from GPU memory for testing and debugging purposes.
    /// </summary>
    /// <param name="kernelId">The kernel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The control block read from GPU memory.</returns>
    /// <exception cref="ArgumentException">Thrown when kernel is not found.</exception>
    public Task<RingKernelControlBlock> ReadControlBlockAsync(
        string kernelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_kernels.TryGetValue(kernelId, out var state))
        {
            throw new ArgumentException($"Kernel '{kernelId}' not found", nameof(kernelId));
        }

        if (state.ControlBlock == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Control block not allocated for kernel '{kernelId}'");
        }

        // Read control block using appropriate method based on allocation type:
        // - AsyncControlBlock: WSL2 mode with async copies and cached last value
        // - Pinned host memory: Zero-copy read (non-blocking while cooperative kernel runs)
        // - Device memory: Synchronous copy (may block on cooperative kernels)
        var asyncControlBlock = state.AsyncControlBlock;
        var controlBlockHostPtr = state.ControlBlockHostPtr;
        var controlStream = state.ControlStream;
        return Task.Run(() =>
        {
            RingKernelControlBlock controlBlock;
            if (asyncControlBlock != null)
            {
                // WSL2 async mode - returns cached value and initiates new async read
                controlBlock = RingKernelControlBlockHelper.ReadNonBlocking(asyncControlBlock);
            }
            else if (controlBlockHostPtr != IntPtr.Zero)
            {
                controlBlock = RingKernelControlBlockHelper.ReadPinned(controlBlockHostPtr);
            }
            else
            {
                controlBlock = RingKernelControlBlockHelper.Read(state.Context, state.ControlBlock, controlStream);
            }

            _logger.LogDebug(
                "Read control block for kernel '{KernelId}': IsActive={IsActive}, ShouldTerminate={ShouldTerminate}, HasTerminated={HasTerminated}, MessagesProcessed={MessagesProcessed}",
                kernelId,
                controlBlock.IsActive,
                controlBlock.ShouldTerminate,
                controlBlock.HasTerminated,
                controlBlock.MessagesProcessed);
            return controlBlock;
        }, cancellationToken);
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

        // Ensure shared context is destroyed (safety check for ref counting)
        lock (_contextLock)
        {
            if (_sharedContext != IntPtr.Zero)
            {
                _logger.LogWarning(
                    "Shared context still exists during disposal (RefCount={RefCount}), force destroying",
                    _contextRefCount);

                var destroyResult = CudaRuntimeCore.cuCtxDestroy(_sharedContext);
                if (destroyResult != CudaError.Success)
                {
                    _logger.LogError(
                        "Failed to destroy shared CUDA context during disposal: {Error}",
                        destroyResult);
                }

                _sharedContext = IntPtr.Zero;
                _contextRefCount = 0;
            }
        }

        _disposed = true;
        _logger.LogInformation("CUDA ring kernel runtime disposed");
    }

    /// <summary>
    /// Maps RingKernelStreamPriority to CUDA driver priority value.
    /// </summary>
    /// <param name="priority">Ring kernel stream priority.</param>
    /// <param name="context">CUDA context for querying priority range.</param>
    /// <returns>CUDA priority value (lower = higher priority).</returns>
    private static int MapStreamPriority(DotCompute.Abstractions.RingKernels.RingKernelStreamPriority priority, IntPtr context)
    {
        // Query device-supported priority range
        int leastPriority = 0;
        int greatestPriority = 0;
        var rangeResult = CudaApi.cuCtxGetStreamPriorityRange(ref leastPriority, ref greatestPriority);

        if (rangeResult != CudaError.Success)
        {
            // Fallback: Use default priorities if query fails
            // CUDA typically supports priorities in range [0, -2] where -2 is highest
            leastPriority = 0;     // Lowest priority
            greatestPriority = -2;  // Highest priority
        }

        // Map RingKernelStreamPriority to CUDA integer priority
        // Note: In CUDA, lower numerical values = higher execution priority
        return priority switch
        {
            DotCompute.Abstractions.RingKernels.RingKernelStreamPriority.High => greatestPriority,  // Numerically lowest (highest priority)
            DotCompute.Abstractions.RingKernels.RingKernelStreamPriority.Normal => 0,                // Default priority
            DotCompute.Abstractions.RingKernels.RingKernelStreamPriority.Low => leastPriority,      // Numerically highest (lowest priority)
            _ => 0 // Default to normal priority
        };
    }

    /// <summary>
    /// Waits for the kernel to set the HasTerminated flag using zero-copy pinned memory reads.
    /// Non-blocking version that doesn't block on cooperative kernels.
    /// </summary>
    /// <param name="hostPtr">Host pointer to the pinned/unified control block.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if kernel terminated gracefully, false if timeout or cancelled.</returns>
    private static async Task<bool> WaitForTerminationPinnedAsync(
        IntPtr hostPtr,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        try
        {
            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                var controlBlock = RingKernelControlBlockHelper.ReadPinned(hostPtr);
                if (controlBlock.HasTerminated != 0)
                {
                    return true;
                }

                // Poll every 10ms
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            // Cancellation requested - return false
            return false;
        }

        return false;
    }

    // ============================================================================
    // EventDriven Mode Support (WSL2 Compatibility)
    // ============================================================================

    /// <summary>
    /// Determines the effective kernel mode, forcing EventDriven mode on WSL2 for compatibility.
    /// </summary>
    /// <param name="requestedMode">The mode requested by the kernel attribute.</param>
    /// <returns>The effective mode to use for kernel compilation.</returns>
    private static RingKernelMode GetEffectiveKernelMode(RingKernelMode requestedMode)
    {
        // On WSL2, CUDA API calls block while persistent kernels are running
        // Force EventDriven mode to allow control block updates between kernel launches
        if (RingKernelControlBlockHelper.IsRunningInWsl2())
        {
            if (requestedMode == RingKernelMode.Persistent)
            {
                return RingKernelMode.EventDriven;
            }
        }
        return requestedMode;
    }

    /// <summary>
    /// Starts the EventDriven relaunch loop for a kernel.
    /// The loop monitors kernel termination and relaunches it automatically when it exits due to iteration limit.
    /// </summary>
    /// <param name="state">The kernel state to manage.</param>
    /// <param name="cancellationToken">Cancellation token to stop the relaunch loop.</param>
    /// <returns>A task that completes when the kernel is permanently terminated.</returns>
    private async Task EventDrivenRelaunchLoopAsync(KernelState state, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting EventDriven relaunch loop for kernel '{KernelId}'",
            state.KernelId);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Wait for kernel to terminate (either iteration limit or explicit termination)
                var terminated = await WaitForKernelTerminationAsync(state, cancellationToken).ConfigureAwait(false);

                if (!terminated || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // Check termination type by reading has_terminated value
                // has_terminated = 1: Permanent termination (should_terminate was set)
                // has_terminated = 2: Relaunchable exit (iteration limit reached)
                var controlBlock = await ReadControlBlockAsync(state.KernelId, cancellationToken).ConfigureAwait(false);

                if (controlBlock.HasTerminated == 1)
                {
                    // Permanent termination - exit the relaunch loop
                    _logger.LogInformation(
                        "Kernel '{KernelId}' permanently terminated (has_terminated=1)",
                        state.KernelId);
                    break;
                }

                if (controlBlock.HasTerminated == 2)
                {
                    // Relaunchable exit - reset termination flag and relaunch
                    _logger.LogDebug(
                        "Kernel '{KernelId}' exited due to iteration limit, relaunching...",
                        state.KernelId);

                    // Reset has_terminated flag
                    var resetBlock = controlBlock;
                    resetBlock.HasTerminated = 0;

                    // Write control block using appropriate method based on allocation type
                    await WriteControlBlockForRelaunchAsync(state, resetBlock, cancellationToken).ConfigureAwait(false);

                    // Relaunch the kernel
                    await RelaunchKernelAsync(state, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Unknown termination state
                    _logger.LogWarning(
                        "Kernel '{KernelId}' has unknown termination state: has_terminated={HasTerminated}",
                        state.KernelId,
                        controlBlock.HasTerminated);
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error in EventDriven relaunch loop for kernel '{KernelId}'",
                    state.KernelId);
                // Wait before retrying to avoid tight loop on errors
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation(
            "EventDriven relaunch loop ended for kernel '{KernelId}'",
            state.KernelId);
    }

    /// <summary>
    /// Waits for a kernel to terminate by polling its control block.
    /// </summary>
    /// <param name="state">The kernel state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the kernel terminated; false if cancelled.</returns>
    private async Task<bool> WaitForKernelTerminationAsync(KernelState state, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Use the instance method to read the control block
                var controlBlock = await ReadControlBlockAsync(state.KernelId, cancellationToken).ConfigureAwait(false);

                if (controlBlock.HasTerminated != 0)
                {
                    return true;
                }

                // Poll every 10ms
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes the control block to GPU memory for kernel relaunch.
    /// Uses appropriate method based on allocation type (AsyncControlBlock, pinned, or device memory).
    /// </summary>
    /// <param name="state">The kernel state.</param>
    /// <param name="controlBlock">The control block to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private Task WriteControlBlockForRelaunchAsync(
        KernelState state,
        RingKernelControlBlock controlBlock,
        CancellationToken cancellationToken)
    {
        var asyncControlBlock = state.AsyncControlBlock;
        var controlBlockHostPtr = state.ControlBlockHostPtr;
        var context = state.Context;
        var devicePtr = state.ControlBlock;
        var controlStream = state.ControlStream;

        return Task.Run(() =>
        {
            if (asyncControlBlock != null)
            {
                // WSL2 async mode - use non-blocking write
                RingKernelControlBlockHelper.WriteNonBlocking(asyncControlBlock, controlBlock);
            }
            else if (controlBlockHostPtr != IntPtr.Zero)
            {
                // Pinned host memory - direct write
                RingKernelControlBlockHelper.WritePinned(controlBlockHostPtr, controlBlock);
            }
            else
            {
                // Device memory - synchronous copy
                RingKernelControlBlockHelper.Write(context, devicePtr, controlBlock);
            }

            _logger.LogDebug(
                "Wrote control block for kernel relaunch: IsActive={IsActive}, HasTerminated={HasTerminated}",
                controlBlock.IsActive,
                controlBlock.HasTerminated);
        }, cancellationToken);
    }

    /// <summary>
    /// Relaunches an EventDriven kernel after it exits due to iteration limit.
    /// </summary>
    /// <param name="state">The kernel state containing module and function handles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task RelaunchKernelAsync(KernelState state, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Ensure we're using the correct context
            var setResult = CudaRuntimeCore.cuCtxSetCurrent(state.Context);
            if (setResult != CudaError.Success)
            {
                _logger.LogError("Failed to set CUDA context for kernel relaunch: {Error}", setResult);
                return;
            }

            // Marshal kernel parameters like the original launch
            // The kernel signature is: __global__ void kernel(RingKernelControlBlock* control_block)
            IntPtr ptrStorage = IntPtr.Zero;
            GCHandle argPtrsHandle = default;

            try
            {
                unsafe
                {
                    // Allocate memory to hold the control block pointer
                    ptrStorage = Marshal.AllocHGlobal(sizeof(IntPtr));
                    *(IntPtr*)ptrStorage = state.ControlBlock;

                    // Create array of parameter pointers and pin it
                    var kernelParams = new IntPtr[] { ptrStorage };
                    argPtrsHandle = GCHandle.Alloc(kernelParams, GCHandleType.Pinned);

                    // Use non-cooperative kernel launch for WSL2 compatibility
                    var launchResult = CudaRuntime.cuLaunchKernel(
                        state.Function,
                        (uint)state.GridSize, 1, 1,    // Grid dimensions (1D grid)
                        (uint)state.BlockSize, 1, 1,   // Block dimensions (1D blocks)
                        0,                              // Shared memory bytes
                        state.Stream,                   // Stream
                        argPtrsHandle.AddrOfPinnedObject(),  // Parameter array
                        IntPtr.Zero);                   // Extra options (null)

                    if (launchResult != CudaError.Success)
                    {
                        _logger.LogError(
                            "Failed to relaunch EventDriven kernel '{KernelId}': {Error}",
                            state.KernelId,
                            launchResult);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Successfully relaunched EventDriven kernel '{KernelId}'",
                            state.KernelId);
                    }
                }
            }
            finally
            {
                // Clean up allocated resources
                if (argPtrsHandle.IsAllocated)
                {
                    argPtrsHandle.Free();
                }

                if (ptrStorage != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ptrStorage);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the device pointer from a GPU queue (CudaMessageQueue).
    /// </summary>
    /// <remarks>
    /// This method uses reflection to get the DevicePointer property from a CudaMessageQueue.
    /// The reflection is necessary because the queue type is stored as object in KernelState.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:Value passed to implicit this parameter is not compatible with the target parameter",
        Justification = "CudaMessageQueue types are preserved by the build system")]
    private static IntPtr GetGpuQueueDevicePointer(object? gpuBuffer)
    {
        if (gpuBuffer == null)
        {
            return IntPtr.Zero;
        }

        // Use reflection to get device pointer from CudaMessageQueue<byte>
        var bufferType = gpuBuffer.GetType();
        var devicePtrProperty = bufferType.GetProperty("DevicePointer");
        if (devicePtrProperty != null)
        {
            return (IntPtr)(devicePtrProperty.GetValue(gpuBuffer) ?? IntPtr.Zero);
        }

        return IntPtr.Zero;
    }
}
