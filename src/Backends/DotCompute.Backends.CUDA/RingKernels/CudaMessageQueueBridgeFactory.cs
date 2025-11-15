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
    /// Creates a message queue bridge for IRingKernelMessage types.
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
        IntPtr devicePtr = IntPtr.Zero;
        var result = CudaApi.cuMemAlloc(ref devicePtr, (nuint)gpuBufferSize);
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to allocate GPU memory: {result}");
        }

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
    /// Creates a named message queue using reflection.
    /// </summary>
    [RequiresDynamicCode("Creates queue using reflection which requires runtime code generation")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic queue creation is required")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Message type must be accessible")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Queue type instantiation requires dynamic type")]
    private static async Task<object> CreateNamedQueueAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type messageType,
        string queueName,
        MessageQueueOptions options,
        CancellationToken cancellationToken)
    {
        // Use fully qualified type to avoid conflicts
        var cudaQueueType = typeof(DotCompute.Backends.CUDA.Messaging.CudaMessageQueue<>).MakeGenericType(messageType);

        // Create logger using NullLoggerFactory (safe and reliable)
        var loggerFactory = new NullLoggerFactory();

        // Create generic logger type ILogger<CudaMessageQueue<T>> using reflection
        var loggerType = typeof(ILogger<>).MakeGenericType(cudaQueueType);
        var createLoggerMethod = typeof(ILoggerFactory).GetMethod(nameof(ILoggerFactory.CreateLogger), 1, Type.EmptyTypes);
        var genericCreateLoggerMethod = createLoggerMethod!.MakeGenericMethod(cudaQueueType);
        var logger = genericCreateLoggerMethod.Invoke(loggerFactory, null);

        // Create instance
        var queue = Activator.CreateInstance(cudaQueueType, options, logger)
            ?? throw new InvalidOperationException($"Failed to create message queue for type {messageType.Name}");

        // Initialize (allocate GPU resources)
        var initializeMethod = cudaQueueType.GetMethod("InitializeAsync");
        if (initializeMethod != null)
        {
            var initTask = (Task)initializeMethod.Invoke(queue, [cancellationToken])!;
            await initTask;
        }

        return queue;
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
