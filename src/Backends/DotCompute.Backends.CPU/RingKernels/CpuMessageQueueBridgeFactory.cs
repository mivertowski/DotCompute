// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.Messaging;
using DotCompute.Core.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CPU.RingKernels;

/// <summary>
/// Factory for creating message queue bridges between host-side named queues and CPU-resident buffers.
/// </summary>
/// <remarks>
/// CPU implementation of the message bridge infrastructure. Unlike the GPU version which transfers
/// to GPU-resident memory, this version transfers to CPU memory buffers that simulate GPU queues.
/// Uses the same MessageQueueBridge infrastructure for consistency across backends.
/// </remarks>
internal static class CpuMessageQueueBridgeFactory
{
    /// <summary>
    /// Simple CPU buffer wrapper for simulating GPU memory.
    /// </summary>
    private sealed class CpuByteBuffer : IAsyncDisposable
    {
        private readonly byte[] _buffer;
        private readonly ILogger _logger;
        private bool _disposed;

        public byte[] Buffer => _buffer;
        public long SizeBytes { get; }

        public CpuByteBuffer(long sizeBytes, ILogger logger)
        {
            SizeBytes = sizeBytes;
            _buffer = new byte[sizeBytes];
            _logger = logger;
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            _logger.LogDebug("Disposed CPU buffer of {Size} bytes", SizeBytes);

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Creates a message queue bridge for IRingKernelMessage types.
    /// </summary>
    [RequiresDynamicCode("Creates bridge using reflection which requires runtime code generation")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic bridge creation is required for ring kernels")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Message type must be accessible for serialization")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Bridge type instantiation requires dynamic type")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Ring kernel bridges require dynamic type creation")]
    public static async Task<(object NamedQueue, object Bridge, object CpuBuffer)> CreateBridgeForMessageTypeAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type messageType,
        string queueName,
        MessageQueueOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Step 1: Create host-side named queue
        var namedQueue = await CreateNamedQueueAsync(messageType, queueName, options, cancellationToken);

        // Step 2: Allocate CPU memory buffer for serialized messages
        const int maxSerializedSize = 65536 + 256; // Header + MaxPayload (same as CUDA)
        var cpuBufferSize = options.Capacity * maxSerializedSize;

        var cpuBuffer = new CpuByteBuffer(cpuBufferSize, logger);

        // Step 3: Create transfer function that copies from staging buffer to CPU memory
        Task<bool> CpuTransferFuncAsync(ReadOnlyMemory<byte> serializedBatch)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Simple memory copy (CPU to CPU)
                    serializedBatch.Span.CopyTo(cpuBuffer.Buffer.AsSpan());
                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "CPU transfer failed");
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
            (Func<ReadOnlyMemory<byte>, Task<bool>>)CpuTransferFuncAsync, // CPU transfer function
            options,               // MessageQueueOptions
            serializer,            // IMessageSerializer<T> (MemoryPack)
            logger                 // ILogger
        ) ?? throw new InvalidOperationException($"Failed to create MessageQueueBridge for type {messageType.Name}");

        logger.LogInformation(
            "Created MemoryPack bridge for {MessageType}: NamedQueue={QueueName}, CpuBuffer={Capacity} bytes",
            messageType.Name, queueName, cpuBufferSize);

        return (namedQueue, bridge, cpuBuffer);
    }

    /// <summary>
    /// Creates a bidirectional message queue bridge for IRingKernelMessage types (Host ↔ CPU Buffer).
    /// </summary>
    [RequiresDynamicCode("Creates bridge using reflection which requires runtime code generation")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic bridge creation is required for ring kernels")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Message type must be accessible for serialization")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Bridge type instantiation requires dynamic type")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Ring kernel bridges require dynamic type creation")]
    public static async Task<(object NamedQueue, object Bridge, object CpuBuffer)> CreateBidirectionalBridgeForMessageTypeAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type messageType,
        string queueName,
        BridgeDirection direction,
        MessageQueueOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Step 1: Create host-side named queue
        var namedQueue = await CreateNamedQueueAsync(messageType, queueName, options, cancellationToken);

        // Step 2: Allocate CPU memory buffer for serialized messages
        const int maxSerializedSize = 65536 + 256; // Header + MaxPayload (same as CUDA)
        var cpuBufferSize = options.Capacity * maxSerializedSize;

        var cpuBuffer = new CpuByteBuffer(cpuBufferSize, logger);

        // Step 3: Create Host → CPU buffer transfer function
        Func<ReadOnlyMemory<byte>, Task<bool>>? hostToCpuFunc = null;
        if (direction is BridgeDirection.HostToDevice or BridgeDirection.Bidirectional)
        {
            hostToCpuFunc = (ReadOnlyMemory<byte> serializedBatch) =>
            {
                return Task.Run(() =>
                {
                    try
                    {
                        // Simple memory copy (host to CPU buffer)
                        serializedBatch.Span.CopyTo(cpuBuffer.Buffer.AsSpan());
                        return true;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Host→CPU buffer transfer failed");
                        return false;
                    }
                });
            };
        }

        // Step 4: Create CPU buffer → Host transfer function
        Func<Memory<byte>, Task<int>>? cpuToHostFunc = null;
        if (direction is BridgeDirection.DeviceToHost or BridgeDirection.Bidirectional)
        {
            cpuToHostFunc = (Memory<byte> readBuffer) =>
            {
                return Task.Run(() =>
                {
                    try
                    {
                        // Simple memory copy (CPU buffer to host)
                        var bytesToCopy = Math.Min(readBuffer.Length, cpuBuffer.Buffer.Length);
                        cpuBuffer.Buffer.AsSpan(0, bytesToCopy).CopyTo(readBuffer.Span);
                        return bytesToCopy;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "CPU buffer→Host transfer failed");
                        return 0;
                    }
                });
            };
        }

        // Step 5: Create MessageQueueBridge with bidirectional support
        var bridgeType = typeof(MessageQueueBridge<>).MakeGenericType(messageType);

        // Create MemoryPackMessageSerializer for high performance
        var serializerType = typeof(MemoryPackMessageSerializer<>).MakeGenericType(messageType);
        var serializer = Activator.CreateInstance(serializerType);

        var bridge = Activator.CreateInstance(
            bridgeType,
            namedQueue,            // IMessageQueue<T> namedQueue
            direction,             // BridgeDirection
            hostToCpuFunc,         // Func<ReadOnlyMemory<byte>, Task<bool>>
            cpuToHostFunc,         // Func<Memory<byte>, Task<int>>
            options,               // MessageQueueOptions
            serializer,            // IMessageSerializer<T> (MemoryPack)
            logger                 // ILogger
        ) ?? throw new InvalidOperationException($"Failed to create bidirectional MessageQueueBridge for type {messageType.Name}");

        logger.LogInformation(
            "Created MemoryPack bidirectional bridge for {MessageType}: Direction={Direction}, NamedQueue={QueueName}, CpuBuffer={Capacity} bytes",
            messageType.Name, direction, queueName, cpuBufferSize);

        return (namedQueue, bridge, cpuBuffer);
    }

    /// <summary>
    /// Creates a named message queue using reflection.
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
        // Create a simple MessageQueue<T> or PriorityMessageQueue<T>
        Type queueType = options.EnablePriorityQueue
            ? typeof(PriorityMessageQueue<>).MakeGenericType(messageType)
            : typeof(MessageQueue<>).MakeGenericType(messageType);

        // Create instance (both MessageQueue and PriorityMessageQueue take MessageQueueOptions constructor)
        var queue = Activator.CreateInstance(queueType, options)
            ?? throw new InvalidOperationException($"Failed to create message queue for type {messageType.Name}");

        return Task.FromResult(queue);
    }

    /// <summary>
    /// Detects input and output message types from a ring kernel method signature.
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
                                        // Found the kernel - extract message types from Span<T> parameters
                                        var parameters = method.GetParameters();

                                        // Ring kernel signature pattern:
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
