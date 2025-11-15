// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
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
}
