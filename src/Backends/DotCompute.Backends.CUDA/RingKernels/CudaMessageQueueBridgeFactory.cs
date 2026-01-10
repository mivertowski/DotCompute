// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DotCompute.Abstractions.Messaging;
using DotCompute.Abstractions.Attributes;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Core.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Factory for creating message queue bridges between host-side named queues and GPU-resident buffers.
/// </summary>
/// <remarks>
/// Simplified implementation that allocates raw GPU memory and transfers serialized bytes directly.
/// Uses MemoryPack for ultra-fast serialization (2-5x faster than JSON).
/// </remarks>
internal static class CudaMessageQueueBridgeFactory
{
    /// <summary>
    /// Simplified GPU buffer wrapper for raw CUDA memory.
    /// </summary>
    private sealed class GpuByteBuffer : IAsyncDisposable
    {
        private readonly IntPtr _devicePtr;
        private readonly long _sizeBytes;
        private readonly IntPtr _context;
        private readonly ILogger _logger;
        private bool _disposed;

        public IntPtr DevicePtr => _devicePtr;
        public long SizeBytes => _sizeBytes;

        public GpuByteBuffer(IntPtr devicePtr, long sizeBytes, IntPtr context, ILogger logger)
        {
            _devicePtr = devicePtr;
            _sizeBytes = sizeBytes;
            _context = context;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Set CUDA context before cleanup
            CudaRuntime.cuCtxSetCurrent(_context);

            // Free GPU memory
            var result = CudaApi.cuMemFree(_devicePtr);
            if (result != CudaError.Success)
            {
                _logger.LogError("Failed to free GPU memory: {Error}", result);
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Creates a message queue bridge for IRingKernelMessage types (Host → Device).
    /// </summary>
    [RequiresDynamicCode("Creates bridge using reflection which requires runtime code generation")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic bridge creation is required for ring kernels")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Message type must be accessible for serialization")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Bridge type instantiation requires dynamic type")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Ring kernel bridges require dynamic type creation")]
    public static async Task<(object NamedQueue, object Bridge, object GpuBuffer)> CreateBridgeForMessageTypeAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type messageType,
        string queueName,
        MessageQueueOptions options,
        IntPtr cudaContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Step 1: Create host-side named queue
        var namedQueue = await CreateNamedQueueAsync(messageType, queueName, options, cancellationToken);

        // Step 2: Allocate raw GPU memory for serialized messages
        // Each message can be up to MaxSerializedSize bytes (default 64KB + 256 byte header)
        const int maxSerializedSize = 65536 + 256; // Header + MaxPayload
        var gpuBufferSize = options.Capacity * maxSerializedSize;

        // Set CUDA context before allocation
        CudaRuntime.cuCtxSetCurrent(cudaContext);

        // Allocate GPU memory
        var devicePtr = IntPtr.Zero;
        var result = CudaApi.cuMemAlloc(ref devicePtr, (nuint)gpuBufferSize);
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to allocate GPU memory: {result}");
        }

        // CRITICAL: Zero-initialize GPU memory to prevent garbage data issues
        // Without this, the kernel may read uninitialized memory containing garbage values
        var memsetResult = CudaApi.cuMemsetD8(devicePtr, 0, (nuint)gpuBufferSize);
        if (memsetResult != CudaError.Success)
        {
            // Free allocated memory on failure
            CudaApi.cuMemFree(devicePtr);
            throw new InvalidOperationException($"Failed to zero-initialize GPU memory: {memsetResult}");
        }

        logger.LogDebug(
            "Zero-initialized GPU buffer for {MessageType}: {Size} bytes at 0x{Address:X}",
            messageType.Name, gpuBufferSize, devicePtr.ToInt64());

        var gpuBuffer = new GpuByteBuffer(devicePtr, gpuBufferSize, cudaContext, logger);

        // Step 3: Create transfer function that copies from staging buffer to GPU
        Task<bool> GpuTransferFuncAsync(ReadOnlyMemory<byte> serializedBatch)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Set CUDA context for this thread
                    CudaRuntime.cuCtxSetCurrent(cudaContext);

                    // Get pinned source pointer
                    using var handle = serializedBatch.Pin();
                    unsafe
                    {
                        var sourcePtr = new IntPtr(handle.Pointer);

                        // CUDA memcpy from pinned host to device
                        var copyResult = CudaApi.cuMemcpyHtoD(
                            devicePtr,
                            sourcePtr,
                            (nuint)serializedBatch.Length);

                        if (copyResult != CudaError.Success)
                        {
                            logger.LogError("CUDA memcpy failed: {Error}", copyResult);
                            return false;
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "GPU transfer failed");
                    return false;
                }
            });
        }

        // Step 4: Create MessageQueueBridge using reflection
        var bridgeType = typeof(MessageQueueBridge<>).MakeGenericType(messageType);

        // Create MemoryPackMessageSerializer for high performance
        var serializerType = typeof(MemoryPackMessageSerializer<>).MakeGenericType(messageType);
        var serializer = Activator.CreateInstance(serializerType);

        var bridge = Activator.CreateInstance(
            bridgeType,
            namedQueue,            // IMessageQueue<T> namedQueue
            (Func<ReadOnlyMemory<byte>, Task<bool>>)GpuTransferFuncAsync, // GPU transfer function
            options,               // MessageQueueOptions
            serializer,            // IMessageSerializer<T> (MemoryPack)
            logger                 // ILogger
        ) ?? throw new InvalidOperationException($"Failed to create MessageQueueBridge for type {messageType.Name}");

        logger.LogInformation(
            "Created MemoryPack bridge for {MessageType}: NamedQueue={QueueName}, GpuBuffer={Capacity} bytes",
            messageType.Name, queueName, gpuBufferSize);

        return (namedQueue, bridge, gpuBuffer);
    }

    /// <summary>
    /// Creates a bidirectional message queue bridge for IRingKernelMessage types (Host ↔ Device).
    /// </summary>
    [RequiresDynamicCode("Creates bridge using reflection which requires runtime code generation")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic bridge creation is required for ring kernels")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Message type must be accessible for serialization")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Bridge type instantiation requires dynamic type")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Ring kernel bridges require dynamic type creation")]
    public static async Task<(object NamedQueue, object Bridge, object GpuBuffer)> CreateBidirectionalBridgeForMessageTypeAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type messageType,
        string queueName,
        BridgeDirection direction,
        MessageQueueOptions options,
        IntPtr cudaContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Step 1: Create host-side named queue
        var namedQueue = await CreateNamedQueueAsync(messageType, queueName, options, cancellationToken);

        // Step 2: Allocate raw GPU memory for serialized messages
        const int maxSerializedSize = 65536 + 256; // Header + MaxPayload
        var gpuBufferSize = options.Capacity * maxSerializedSize;

        // Set CUDA context before allocation
        CudaRuntime.cuCtxSetCurrent(cudaContext);

        // Allocate GPU memory
        var devicePtr = IntPtr.Zero;
        var result = CudaApi.cuMemAlloc(ref devicePtr, (nuint)gpuBufferSize);
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to allocate GPU memory: {result}");
        }

        // CRITICAL: Zero-initialize GPU memory to prevent garbage data issues
        // Without this, the kernel may read uninitialized memory containing garbage values
        var memsetResult = CudaApi.cuMemsetD8(devicePtr, 0, (nuint)gpuBufferSize);
        if (memsetResult != CudaError.Success)
        {
            // Free allocated memory on failure
            CudaApi.cuMemFree(devicePtr);
            throw new InvalidOperationException($"Failed to zero-initialize GPU memory: {memsetResult}");
        }

        logger.LogDebug(
            "Zero-initialized bidirectional GPU buffer for {MessageType}: {Size} bytes at 0x{Address:X}",
            messageType.Name, gpuBufferSize, devicePtr.ToInt64());

        var gpuBuffer = new GpuByteBuffer(devicePtr, gpuBufferSize, cudaContext, logger);

        // Step 3: Create Host → Device transfer function
        Func<ReadOnlyMemory<byte>, Task<bool>>? hostToDeviceFunc = null;
        if (direction is BridgeDirection.HostToDevice or BridgeDirection.Bidirectional)
        {
            hostToDeviceFunc = (ReadOnlyMemory<byte> serializedBatch) =>
            {
                return Task.Run(() =>
                {
                    try
                    {
                        CudaRuntime.cuCtxSetCurrent(cudaContext);

                        using var handle = serializedBatch.Pin();
                        unsafe
                        {
                            var sourcePtr = new IntPtr(handle.Pointer);
                            var copyResult = CudaApi.cuMemcpyHtoD(
                                devicePtr,
                                sourcePtr,
                                (nuint)serializedBatch.Length);

                            if (copyResult != CudaError.Success)
                            {
                                logger.LogError("CUDA memcpy Host→Device failed: {Error}", copyResult);
                                return false;
                            }

                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Host→Device transfer failed");
                        return false;
                    }
                });
            };
        }

        // Step 4: Create Device → Host transfer function
        Func<Memory<byte>, Task<int>>? deviceToHostFunc = null;
        if (direction is BridgeDirection.DeviceToHost or BridgeDirection.Bidirectional)
        {
            deviceToHostFunc = (Memory<byte> readBuffer) =>
            {
                return Task.Run(() =>
                {
                    try
                    {
                        CudaRuntime.cuCtxSetCurrent(cudaContext);

                        using var handle = readBuffer.Pin();
                        unsafe
                        {
                            var destPtr = new IntPtr(handle.Pointer);
                            var copyResult = CudaApi.cuMemcpyDtoH(
                                destPtr,
                                devicePtr,
                                (nuint)readBuffer.Length);

                            if (copyResult != CudaError.Success)
                            {
                                logger.LogError("CUDA memcpy Device→Host failed: {Error}", copyResult);
                                return 0;
                            }

                            return readBuffer.Length;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Device→Host transfer failed");
                        return 0;
                    }
                });
            };
        }

        // Step 5: Create MessageQueueBridge using reflection with bidirectional support
        var bridgeType = typeof(MessageQueueBridge<>).MakeGenericType(messageType);

        // Create MemoryPackMessageSerializer for high performance
        var serializerType = typeof(MemoryPackMessageSerializer<>).MakeGenericType(messageType);
        var serializer = Activator.CreateInstance(serializerType);

        var bridge = Activator.CreateInstance(
            bridgeType,
            namedQueue,            // IMessageQueue<T> namedQueue
            direction,             // BridgeDirection
            hostToDeviceFunc,      // Func<ReadOnlyMemory<byte>, Task<bool>>
            deviceToHostFunc,      // Func<Memory<byte>, Task<int>>
            options,               // MessageQueueOptions
            serializer,            // IMessageSerializer<T> (MemoryPack)
            logger                 // ILogger
        ) ?? throw new InvalidOperationException($"Failed to create bidirectional MessageQueueBridge for type {messageType.Name}");

        logger.LogInformation(
            "Created MemoryPack bidirectional bridge for {MessageType}: Direction={Direction}, NamedQueue={QueueName}, GpuBuffer={Capacity} bytes",
            messageType.Name, direction, queueName, gpuBufferSize);

        return (namedQueue, bridge, gpuBuffer);
    }

    /// <summary>
    /// Creates a named message queue using reflection.
    /// Uses host-side MessageQueue since the bridge handles GPU transfers separately.
    /// </summary>
    [RequiresDynamicCode("Creates queue using reflection which requires runtime code generation")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic queue creation is required")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Message type must be accessible")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Queue type instantiation requires dynamic type")]
    private static Task<object> CreateNamedQueueAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type messageType,
        string queueName,
        MessageQueueOptions options,
        CancellationToken cancellationToken)
    {
        // Use host-side MessageQueue from Core (not CudaMessageQueue which creates its own GPU context)
        // The bridge handles GPU memory transfers separately with the passed CUDA context
        var hostQueueType = typeof(DotCompute.Core.Messaging.MessageQueue<>).MakeGenericType(messageType);

        // Create instance - host-side queue doesn't need async initialization
        // Note: MessageQueue<T> only takes options parameter (not logger)
        var queue = Activator.CreateInstance(hostQueueType, options)
            ?? throw new InvalidOperationException($"Failed to create host message queue for type {messageType.Name}");

        return Task.FromResult(queue);
    }

    /// <summary>
    /// Detects input and output message types from a ring kernel method signature.
    /// Supports two signature patterns:
    /// 1. Span-based: (Span&lt;long&gt; timestamps, Span&lt;TInput&gt; input, Span&lt;TOutput&gt; output)
    /// 2. RingKernelContext-based: (RingKernelContext ctx, TInput message)
    /// </summary>
    [RequiresUnreferencedCode("Searches all loaded assemblies for ring kernel methods")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Kernel discovery requires reflection over all loaded types")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Ring kernel attributes must be discoverable at runtime")]
    public static (Type InputType, Type OutputType) DetectMessageTypes(string kernelId)
    {
        // Search all loaded assemblies for a method with [RingKernel] attribute matching kernelId
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    try
                    {
                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                        foreach (var method in methods)
                        {
                            try
                            {
                                var ringKernelAttr = method.GetCustomAttribute<RingKernelAttribute>();
                                if (ringKernelAttr != null)
                                {
                                    // Check if this is the kernel we're looking for
                                    var generatedKernelId = $"{type.Name}_{method.Name}";
                                    if (generatedKernelId == kernelId || ringKernelAttr.KernelId == kernelId)
                                    {
                                        // Found the kernel - extract message types based on signature pattern
                                        var parameters = method.GetParameters();

                                        // Pattern 1: RingKernelContext-based signature
                                        // param[0]: RingKernelContext ctx
                                        // param[1]: TInput message  <- INPUT TYPE
                                        // Output type from OutputMessageType attribute property, or InputMessageType as fallback
                                        if (parameters.Length >= 2 &&
                                            parameters[0].ParameterType.Name == "RingKernelContext")
                                        {
                                            var inputType = ringKernelAttr.InputMessageType ?? parameters[1].ParameterType;

                                            // Use OutputMessageType from attribute if specified, otherwise fall back to input type
                                            var outputType = ringKernelAttr.OutputMessageType ?? inputType;

                                            return (inputType, outputType);
                                        }

                                        // Pattern 2: Span-based signature (original)
                                        // param[0]: Span<long> timestamps
                                        // param[1]: Span<TInput> requestQueue  <- INPUT TYPE
                                        // param[2]: Span<TOutput> responseQueue <- OUTPUT TYPE
                                        if (parameters.Length >= 3)
                                        {
                                            var requestQueueParam = parameters[1];
                                            var responseQueueParam = parameters[2];

                                            var inputType = ExtractSpanElementType(requestQueueParam.ParameterType);
                                            var outputType = ExtractSpanElementType(responseQueueParam.ParameterType);

                                            if (inputType != null && outputType != null)
                                            {
                                                return (inputType, outputType);
                                            }
                                        }

                                        // Pattern 3: Single-parameter signature for IRingKernelMessage types
                                        // Example: void TestMessageKernel(SimpleMessage message)
                                        if (parameters.Length == 1)
                                        {
                                            var inputType = ringKernelAttr.InputMessageType ?? parameters[0].ParameterType;
                                            var outputType = ringKernelAttr.OutputMessageType ?? inputType;
                                            return (inputType, outputType);
                                        }

                                        // Pattern 4: Two-parameter signature without RingKernelContext
                                        // Example: void ProcessKernel(InputType input, OutputType output)
                                        if (parameters.Length == 2 &&
                                            parameters[0].ParameterType.Name != "RingKernelContext")
                                        {
                                            var inputType = ringKernelAttr.InputMessageType ?? parameters[0].ParameterType;
                                            var outputType = ringKernelAttr.OutputMessageType ?? parameters[1].ParameterType;
                                            return (inputType, outputType);
                                        }
                                    }
                                }
                            }
                            catch (TypeLoadException)
                            {
                                // Skip methods with attributes that reference unavailable types
                                continue;
                            }
                            catch (FileNotFoundException)
                            {
                                // Skip methods with attributes from missing assemblies
                                continue;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Skip types that fail to reflect
                        continue;
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that fail to load
                continue;
            }
        }

        // Fallback to byte if kernel not found
        return (typeof(byte), typeof(byte));
    }

    /// <summary>
    /// Detects input and output message types for a ring kernel with explicit assemblies.
    /// </summary>
    [RequiresUnreferencedCode("Searches assemblies for ring kernel methods")]
    public static (Type InputType, Type OutputType) DetectMessageTypes(string kernelId, IEnumerable<Assembly>? assemblies)
    {
        // If no explicit assemblies, fall back to AppDomain
        var assembliesToSearch = assemblies ?? AppDomain.CurrentDomain.GetAssemblies();
        return DetectMessageTypesCore(kernelId, assembliesToSearch);
    }

    [RequiresUnreferencedCode("Searches assemblies for ring kernel methods")]
    private static (Type InputType, Type OutputType) DetectMessageTypesCore(string kernelId, IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    try
                    {
                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                        foreach (var method in methods)
                        {
                            try
                            {
                                var ringKernelAttr = method.GetCustomAttribute<RingKernelAttribute>();
                                if (ringKernelAttr != null)
                                {
                                    var generatedKernelId = $"{type.Name}_{method.Name}";
                                    if (generatedKernelId == kernelId || ringKernelAttr.KernelId == kernelId)
                                    {
                                        var parameters = method.GetParameters();

                                        // Pattern 1: RingKernelContext-based signature
                                        if (parameters.Length >= 2 &&
                                            parameters[0].ParameterType.Name == "RingKernelContext")
                                        {
                                            var inputType = ringKernelAttr.InputMessageType ?? parameters[1].ParameterType;
                                            // Use OutputMessageType from attribute if specified, otherwise fall back to input type
                                            var outputType = ringKernelAttr.OutputMessageType ?? inputType;
                                            return (inputType, outputType);
                                        }

                                        // Pattern 2: Span-based signature (original)
                                        if (parameters.Length >= 3)
                                        {
                                            var requestInputType = ExtractSpanElementType(parameters[1].ParameterType);
                                            var responseOutputType = ExtractSpanElementType(parameters[2].ParameterType);

                                            if (requestInputType != null && responseOutputType != null)
                                            {
                                                return (requestInputType, responseOutputType);
                                            }
                                        }

                                        // Pattern 3: Single-parameter signature for IRingKernelMessage types
                                        // Example: void TestMessageKernel(SimpleMessage message)
                                        if (parameters.Length == 1)
                                        {
                                            var inputType = ringKernelAttr.InputMessageType ?? parameters[0].ParameterType;
                                            var outputType = ringKernelAttr.OutputMessageType ?? inputType;
                                            return (inputType, outputType);
                                        }

                                        // Pattern 4: Two-parameter signature without RingKernelContext
                                        // Example: void ProcessKernel(InputType input, OutputType output)
                                        if (parameters.Length == 2 &&
                                            parameters[0].ParameterType.Name != "RingKernelContext")
                                        {
                                            var inputType = ringKernelAttr.InputMessageType ?? parameters[0].ParameterType;
                                            var outputType = ringKernelAttr.OutputMessageType ?? parameters[1].ParameterType;
                                            return (inputType, outputType);
                                        }
                                    }
                                }
                            }
                            catch { /* Skip methods that fail */ }
                        }
                    }
                    catch { /* Skip types that fail */ }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }
        }

        return (typeof(byte), typeof(byte));
    }

    /// <summary>
    /// Extracts the element type T from a Span&lt;T&gt; parameter type.
    /// </summary>
    private static Type? ExtractSpanElementType(Type parameterType)
    {
        if (parameterType.IsGenericType)
        {
            var genericTypeDef = parameterType.GetGenericTypeDefinition();
            if (genericTypeDef.Name == "Span`1")
            {
                return parameterType.GetGenericArguments()[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Detects input and output message sizes from a ring kernel's [RingKernel] attribute.
    /// </summary>
    /// <param name="kernelId">The kernel identifier (either KernelId property or TypeName_MethodName format).</param>
    /// <param name="assemblies">Optional assemblies to search. If null, searches all loaded assemblies.</param>
    /// <returns>A tuple with (MaxInputMessageSizeBytes, MaxOutputMessageSizeBytes). Defaults to (1024, 1024) if not found.</returns>
    [RequiresUnreferencedCode("Searches assemblies for ring kernel methods")]
    public static (int MaxInputMessageSizeBytes, int MaxOutputMessageSizeBytes) DetectMessageSizes(
        string kernelId,
        IEnumerable<Assembly>? assemblies = null)
    {
        var assembliesToSearch = assemblies ?? AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assembliesToSearch)
        {
            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    try
                    {
                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                        foreach (var method in methods)
                        {
                            try
                            {
                                var ringKernelAttr = method.GetCustomAttribute<RingKernelAttribute>();
                                if (ringKernelAttr != null)
                                {
                                    var generatedKernelId = $"{type.Name}_{method.Name}";
                                    if (generatedKernelId == kernelId || ringKernelAttr.KernelId == kernelId)
                                    {
                                        // Found the kernel - extract message sizes from attribute
                                        return (ringKernelAttr.MaxInputMessageSizeBytes, ringKernelAttr.MaxOutputMessageSizeBytes);
                                    }
                                }
                            }
                            catch { /* Skip methods that fail */ }
                        }
                    }
                    catch { /* Skip types that fail */ }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }
        }

        // Default to 1024 bytes if kernel attribute not found
        return (1024, 1024);
    }

    /// <summary>
    /// Creates a GPU ring buffer bridge with atomic head/tail counters for ring kernel message passing.
    /// </summary>
    /// <typeparam name="T">Message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
    /// <param name="deviceId">CUDA device ID.</param>
    /// <param name="capacity">Ring buffer capacity (must be power of 2).</param>
    /// <param name="messageSize">Size of each serialized message in bytes.</param>
    /// <param name="useUnifiedMemory">True for unified memory (non-WSL2), false for device memory (WSL2).</param>
    /// <param name="enableDmaTransfer">True to enable background DMA transfers (WSL2), false for unified memory mode.</param>
    /// <param name="direction">
    /// Direction of data flow. Use <see cref="GpuBridgeDirection.HostToDevice"/> for input bridges
    /// and <see cref="GpuBridgeDirection.DeviceToHost"/> for output bridges.
    /// </param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A tuple containing the host queue, GPU ring buffer, and bridge.</returns>
    public static (IMessageQueue<T> HostQueue, GpuRingBuffer<T> GpuBuffer, GpuRingBufferBridge<T> Bridge) CreateGpuRingBufferBridge<T>(
        int deviceId,
        int capacity,
        int messageSize,
        bool useUnifiedMemory,
        bool enableDmaTransfer,
        GpuBridgeDirection direction = GpuBridgeDirection.Bidirectional,
        ILogger? logger = null)
        where T : IRingKernelMessage
    {
        // Create host-side message queue
        var options = new MessageQueueOptions
        {
            Capacity = capacity,
            BackpressureStrategy = BackpressureStrategy.DropOldest,
            EnableDeduplication = false,
            DeduplicationWindowSize = Math.Max(16, Math.Min(capacity * 4, 4096)) // Within valid range [16, capacity*4]
        };

        var hostQueue = new DotCompute.Core.Messaging.MessageQueue<T>(options);

        // Create GPU ring buffer
        var gpuBuffer = new GpuRingBuffer<T>(
            deviceId,
            capacity,
            messageSize,
            useUnifiedMemory,
            logger);

        // Create bridge with direction
        // CRITICAL: Use correct direction to prevent race conditions:
        // - HostToDevice for INPUT bridges (host writes, kernel reads)
        // - DeviceToHost for OUTPUT bridges (kernel writes, host reads)
        var bridge = new GpuRingBufferBridge<T>(
            hostQueue,
            gpuBuffer,
            enableDmaTransfer,
            direction,
            logger);

        logger?.LogInformation(
            "[Factory] Created GPU ring buffer bridge for {MessageType}: " +
            "device={DeviceId}, capacity={Capacity}, messageSize={MessageSize}, " +
            "unified={Unified}, dma={Dma}, " +
            "headPtr=0x{HeadPtr:X}, tailPtr=0x{TailPtr:X}, bufferPtr=0x{BufferPtr:X}",
            typeof(T).Name, deviceId, capacity, messageSize,
            useUnifiedMemory, enableDmaTransfer,
            gpuBuffer.DeviceHeadPtr.ToInt64(), gpuBuffer.DeviceTailPtr.ToInt64(), gpuBuffer.DeviceBufferPtr.ToInt64());

        return (hostQueue, gpuBuffer, bridge);
    }

    /// <summary>
    /// Creates a GPU ring buffer bridge using reflection for dynamic message type creation.
    /// </summary>
    [RequiresDynamicCode("Creates bridge using reflection which requires runtime code generation")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic bridge creation is required for ring kernels")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Message type must be accessible")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Bridge type instantiation requires dynamic type")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Ring kernel bridges require dynamic type creation")]
    public static (object HostQueue, object GpuBuffer, object Bridge) CreateGpuRingBufferBridgeForMessageType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type messageType,
        int deviceId,
        int capacity,
        int messageSize,
        bool useUnifiedMemory,
        bool enableDmaTransfer,
        GpuBridgeDirection direction = GpuBridgeDirection.Bidirectional,
        ILogger? logger = null)
    {
        logger?.LogDebug(
            "[Factory] Creating GPU ring buffer bridge via reflection for {MessageType} " +
            "(device={DeviceId}, capacity={Capacity}, msgSize={MessageSize}, unified={Unified}, dma={Dma}, direction={Direction})",
            messageType.Name, deviceId, capacity, messageSize, useUnifiedMemory, enableDmaTransfer, direction);

        // Use reflection to call CreateGpuRingBufferBridge<T>
        var method = typeof(CudaMessageQueueBridgeFactory)
            .GetMethod(nameof(CreateGpuRingBufferBridge), BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Failed to find CreateGpuRingBufferBridge method");

        var genericMethod = method.MakeGenericMethod(messageType);

        var result = genericMethod.Invoke(null, new object?[]
        {
            deviceId,
            capacity,
            messageSize,
            useUnifiedMemory,
            enableDmaTransfer,
            direction,
            logger
        });

        if (result == null)
        {
            throw new InvalidOperationException($"Failed to create GPU ring buffer bridge for type {messageType.Name} - result was null");
        }

        // Extract tuple members from the result using ITuple interface
        // The method returns ValueTuple<IMessageQueue<T>, GpuRingBuffer<T>, GpuRingBufferBridge<T>>
        if (result is not System.Runtime.CompilerServices.ITuple tuple || tuple.Length != 3)
        {
            throw new InvalidOperationException($"Failed to create GPU ring buffer bridge for type {messageType.Name} - result is not a 3-item tuple");
        }

        var item1 = tuple[0];
        var item2 = tuple[1];
        var item3 = tuple[2];

        if (item1 == null || item2 == null || item3 == null)
        {
            throw new InvalidOperationException($"Failed to extract tuple members from GPU ring buffer bridge for type {messageType.Name}");
        }

        logger?.LogDebug(
            "[Factory] Successfully created GPU ring buffer bridge via reflection for {MessageType}",
            messageType.Name);

        return (item1, item2, item3);
    }
}
