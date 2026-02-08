// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.Messaging;
using DotCompute.Backends.Metal.Native;
using DotCompute.Core.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.Metal.RingKernels;

/// <summary>
/// Factory for creating message queue bridges between host-side named queues and GPU-resident buffers on Metal.
/// </summary>
/// <remarks>
/// Simplified implementation that allocates raw GPU memory and transfers serialized bytes directly.
/// Uses MemoryPack for ultra-fast serialization (2-5x faster than JSON).
/// </remarks>
internal static class MetalMessageQueueBridgeFactory
{
    /// <summary>
    /// Simplified GPU buffer wrapper for raw Metal memory.
    /// </summary>
    private sealed class GpuByteBuffer : IAsyncDisposable
    {
        private readonly IntPtr _deviceBuffer;
        private readonly long _sizeBytes;
        private readonly ILogger _logger;
        private bool _disposed;

        public IntPtr DevicePtr => _deviceBuffer;
        public long SizeBytes => _sizeBytes;

        public GpuByteBuffer(IntPtr deviceBuffer, long sizeBytes, ILogger logger)
        {
            _deviceBuffer = deviceBuffer;
            _sizeBytes = sizeBytes;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            await Task.Run(() =>
            {
                // Release Metal buffer
                if (_deviceBuffer != IntPtr.Zero)
                {
                    MetalNative.ReleaseBuffer(_deviceBuffer);
                }
            });
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
        IntPtr metalDevice,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Step 1: Create host-side named queue
        var namedQueue = await CreateNamedQueueAsync(messageType, queueName, options, cancellationToken);

        // Step 2: Allocate raw GPU memory for serialized messages
        // Each message can be up to MaxSerializedSize bytes (default 64KB + 256 byte header)
        const int maxSerializedSize = 65536 + 256; // Header + MaxPayload
        var gpuBufferSize = options.Capacity * maxSerializedSize;

        // Allocate Metal buffer (StorageModeShared for CPU/GPU access on Apple Silicon)
        var deviceBuffer = MetalNative.CreateBuffer(metalDevice, (nuint)gpuBufferSize, MetalStorageMode.Shared);
        if (deviceBuffer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to allocate Metal buffer");
        }

        // Zero-initialize the buffer
        var bufferContents = MetalNative.GetBufferContents(deviceBuffer);
        unsafe
        {
            new Span<byte>((void*)bufferContents, gpuBufferSize).Clear();
        }

        logger.LogDebug(
            "Zero-initialized Metal GPU buffer for {MessageType}: {Size} bytes at 0x{Address:X}",
            messageType.Name, gpuBufferSize, deviceBuffer.ToInt64());

        var gpuBuffer = new GpuByteBuffer(deviceBuffer, gpuBufferSize, logger);

        // Step 3: Create transfer function that copies from staging buffer to GPU
        Task<bool> GpuTransferFuncAsync(ReadOnlyMemory<byte> serializedBatch)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Get buffer contents pointer (Metal shared memory - direct CPU access)
                    var destPtr = MetalNative.GetBufferContents(deviceBuffer);
                    if (destPtr == IntPtr.Zero)
                    {
                        logger.LogError("Metal buffer contents pointer is null");
                        return false;
                    }

                    // Copy data to shared Metal buffer
                    using var handle = serializedBatch.Pin();
                    unsafe
                    {
                        var sourcePtr = handle.Pointer;
                        Buffer.MemoryCopy(sourcePtr, (void*)destPtr, gpuBufferSize, serializedBatch.Length);
                    }

                    // Notify Metal that buffer was modified by CPU
                    MetalNative.DidModifyRange(deviceBuffer, 0, serializedBatch.Length);

                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Metal GPU transfer failed");
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
        IntPtr metalDevice,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Step 1: Create host-side named queue
        var namedQueue = await CreateNamedQueueAsync(messageType, queueName, options, cancellationToken);

        // Step 2: Allocate raw GPU memory for serialized messages
        const int maxSerializedSize = 65536 + 256; // Header + MaxPayload
        var gpuBufferSize = options.Capacity * maxSerializedSize;

        // Allocate Metal buffer
        var deviceBuffer = MetalNative.CreateBuffer(metalDevice, (nuint)gpuBufferSize, MetalStorageMode.Shared);
        if (deviceBuffer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to allocate Metal buffer");
        }

        // Zero-initialize
        var bufferContents = MetalNative.GetBufferContents(deviceBuffer);
        unsafe
        {
            new Span<byte>((void*)bufferContents, gpuBufferSize).Clear();
        }

        logger.LogDebug(
            "Zero-initialized bidirectional Metal GPU buffer for {MessageType}: {Size} bytes at 0x{Address:X}",
            messageType.Name, gpuBufferSize, deviceBuffer.ToInt64());

        var gpuBuffer = new GpuByteBuffer(deviceBuffer, gpuBufferSize, logger);

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
                        var destPtr = MetalNative.GetBufferContents(deviceBuffer);
                        if (destPtr == IntPtr.Zero)
                        {
                            logger.LogError("Metal buffer contents pointer is null");
                            return false;
                        }

                        using var handle = serializedBatch.Pin();
                        unsafe
                        {
                            var sourcePtr = handle.Pointer;
                            Buffer.MemoryCopy(sourcePtr, (void*)destPtr, gpuBufferSize, serializedBatch.Length);
                        }

                        MetalNative.DidModifyRange(deviceBuffer, 0, serializedBatch.Length);
                        return true;
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
                        var srcPtr = MetalNative.GetBufferContents(deviceBuffer);
                        if (srcPtr == IntPtr.Zero)
                        {
                            logger.LogError("Metal buffer contents pointer is null");
                            return 0;
                        }

                        using var handle = readBuffer.Pin();
                        unsafe
                        {
                            var destPtr = handle.Pointer;
                            Buffer.MemoryCopy((void*)srcPtr, destPtr, readBuffer.Length, readBuffer.Length);
                        }

                        return readBuffer.Length;
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
        // Use host-side MessageQueue from Core (not MetalMessageQueue which creates its own device context)
        // The bridge handles GPU memory transfers separately with the passed Metal device
        var hostQueueType = typeof(DotCompute.Core.Messaging.MessageQueue<>).MakeGenericType(messageType);

        // Create NullLogger<T> instance using reflection for type-safe logging
        var nullLoggerType = typeof(NullLogger<>).MakeGenericType(hostQueueType);
        var logger = Activator.CreateInstance(nullLoggerType)
            ?? throw new InvalidOperationException("Failed to create NullLogger instance");

        // Create instance - host-side queue doesn't need async initialization
        var queue = Activator.CreateInstance(hostQueueType, options, logger)
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
        return DetectMessageTypesCore(kernelId, assemblies);
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
                                            var inputType = parameters[1].ParameterType;
                                            var outputType = inputType;
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
}
