// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using DotCompute.Backends.CUDA.RingKernels;

using System;
using DotCompute.Tests.Common.Specialized;
using DotCompute.Tests.Common;
using FluentAssertions;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

// Simple test message for GPU ring buffer tests
[MemoryPackable]
internal sealed partial class TestGpuMessage : IRingKernelMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public string MessageType => "TestGpuMessage";
    public byte Priority { get; set; }
    public Guid? CorrelationId { get; set; }

    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public float Contribution { get; set; }
    public int Iteration { get; set; }

    // Binary format: MessageId(16) + Priority(1) + CorrelationId(1+16) + SourceId(4) + TargetId(4) + Value(4) + Iteration(4) = 50 bytes
    public int PayloadSize => 50;

    public ReadOnlySpan<byte> Serialize()
    {
        return MemoryPackSerializer.Serialize(this);
    }

    public void Deserialize(ReadOnlySpan<byte> data)
    {
        var deserialized = MemoryPackSerializer.Deserialize<TestGpuMessage>(data);
        if (deserialized != null)
        {
            MessageId = deserialized.MessageId;
            Priority = deserialized.Priority;
            CorrelationId = deserialized.CorrelationId;
            SourceId = deserialized.SourceId;
            TargetId = deserialized.TargetId;
            Contribution = deserialized.Contribution;
            Iteration = deserialized.Iteration;
        }
    }
}

/// <summary>
/// Hardware tests for GPU ring buffer bridge implementation.
/// Tests both unified memory (non-WSL2) and device memory + DMA (WSL2) modes.
/// </summary>
[Collection("CUDA Hardware Tests")]
[Trait("Category", "Hardware")]
[Trait("Backend", "CUDA")]
[Trait("Component", "GpuRingBuffer")]
public class GpuRingBufferBridgeTests : CudaTestBase
{
    public GpuRingBufferBridgeTests(ITestOutputHelper output) : base(output)
    {
    }

    [SkippableFact]
    [Trait("Test", "GpuRingBuffer.Allocation")]
    public void GpuRingBuffer_DeviceMemoryMode_AllocatesSuccessfully()
    {
        // Arrange
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(5, 0), "CUDA Compute Capability 5.0+ required");

        const int capacity = 16; // Power of 2
        const int messageSize = 256;

        // Act & Assert - Device memory mode (WSL2)
        using var gpuBuffer = new GpuRingBuffer<TestGpuMessage>(
            deviceId: 0,
            capacity: capacity,
            messageSize: messageSize,
            useUnifiedMemory: false, // Device memory
            logger: null);

        // Assert
        gpuBuffer.Should().NotBeNull();
        gpuBuffer.Capacity.Should().Be(capacity);
        gpuBuffer.MessageSize.Should().Be(messageSize);
        gpuBuffer.IsUnifiedMemory.Should().BeFalse();
        gpuBuffer.DeviceHeadPtr.Should().NotBe(IntPtr.Zero, "head pointer should be allocated");
        gpuBuffer.DeviceTailPtr.Should().NotBe(IntPtr.Zero, "tail pointer should be allocated");
        gpuBuffer.DeviceBufferPtr.Should().NotBe(IntPtr.Zero, "buffer pointer should be allocated");

        Output.WriteLine($"GPU ring buffer allocated: head=0x{gpuBuffer.DeviceHeadPtr:X}, tail=0x{gpuBuffer.DeviceTailPtr:X}");
    }

    [SkippableFact]
    [Trait("Test", "GpuRingBuffer.Allocation")]
    public void GpuRingBuffer_UnifiedMemoryMode_AllocatesSuccessfully()
    {
        // Arrange
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(5, 0), "CUDA Compute Capability 5.0+ required");

        // Skip on WSL2 due to unified memory limitations

        const int capacity = 16; // Power of 2
        const int messageSize = 256;

        // Act & Assert - Unified memory mode (non-WSL2)
        using var gpuBuffer = new GpuRingBuffer<TestGpuMessage>(
            deviceId: 0,
            capacity: capacity,
            messageSize: messageSize,
            useUnifiedMemory: true, // Unified memory
            logger: null);

        // Assert
        gpuBuffer.Should().NotBeNull();
        gpuBuffer.Capacity.Should().Be(capacity);
        gpuBuffer.MessageSize.Should().Be(messageSize);
        gpuBuffer.IsUnifiedMemory.Should().BeTrue();
        gpuBuffer.DeviceHeadPtr.Should().NotBe(IntPtr.Zero);
        gpuBuffer.DeviceTailPtr.Should().NotBe(IntPtr.Zero);
        gpuBuffer.DeviceBufferPtr.Should().NotBe(IntPtr.Zero);

        Output.WriteLine($"Unified memory ring buffer allocated: head=0x{gpuBuffer.DeviceHeadPtr:X}, tail=0x{gpuBuffer.DeviceTailPtr:X}");
    }

    [SkippableFact]
    [Trait("Test", "GpuRingBuffer.ReadWrite")]
    public void GpuRingBuffer_WriteAndReadMessage_DeviceMemoryMode()
    {
        // Arrange
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(5, 0), "CUDA Compute Capability 5.0+ required");

        const int capacity = 16;
        var messageSize = MemoryPackSerializer.Serialize(new TestGpuMessage()).Length;

        using var gpuBuffer = new GpuRingBuffer<TestGpuMessage>(
            deviceId: 0,
            capacity: capacity,
            messageSize: messageSize,
            useUnifiedMemory: false,
            logger: null);

        var testMessage = new TestGpuMessage
        {
            SourceId = 42,
            TargetId = 100,
            Contribution = 0.123f,
            Iteration = 5
        };

        // Act - Write message
        gpuBuffer.WriteMessage(testMessage, index: 0);

        // Act - Read message
        var readMessage = gpuBuffer.ReadMessage(index: 0);

        // Assert
        readMessage.SourceId.Should().Be(testMessage.SourceId);
        readMessage.TargetId.Should().Be(testMessage.TargetId);
        readMessage.Contribution.Should().BeApproximately(testMessage.Contribution, 0.0001f);
        readMessage.Iteration.Should().Be(testMessage.Iteration);

        Output.WriteLine($"Message write/read successful: Source={readMessage.SourceId}, Target={readMessage.TargetId}");
    }

    [SkippableFact]
    [Trait("Test", "GpuRingBuffer.Atomics")]
    public void GpuRingBuffer_HeadTailCounters_InitializeToZero()
    {
        // Arrange
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(5, 0), "CUDA Compute Capability 5.0+ required");

        using var gpuBuffer = new GpuRingBuffer<TestGpuMessage>(
            deviceId: 0,
            capacity: 16,
            messageSize: 256,
            useUnifiedMemory: false,
            logger: null);

        // Act
        var head = gpuBuffer.ReadHead();
        var tail = gpuBuffer.ReadTail();

        // Assert
        head.Should().Be(0u, "head should initialize to 0");
        tail.Should().Be(0u, "tail should initialize to 0");
    }

    [SkippableFact]
    [Trait("Test", "GpuRingBuffer.Atomics")]
    public void GpuRingBuffer_HeadTailCounters_WriteAndRead()
    {
        // Arrange
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(5, 0), "CUDA Compute Capability 5.0+ required");

        using var gpuBuffer = new GpuRingBuffer<TestGpuMessage>(
            deviceId: 0,
            capacity: 16,
            messageSize: 256,
            useUnifiedMemory: false,
            logger: null);

        // Act - Write counters
        gpuBuffer.WriteHead(5u);
        gpuBuffer.WriteTail(10u);

        // Act - Read counters
        var head = gpuBuffer.ReadHead();
        var tail = gpuBuffer.ReadTail();

        // Assert
        head.Should().Be(5u);
        tail.Should().Be(10u);
    }

    [SkippableFact]
    [Trait("Test", "GpuRingBufferBridge.DMA")]
    public async Task GpuRingBufferBridge_DmaTransferMode_TransfersMessagesHostToGpu()
    {
        // Arrange
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(5, 0), "CUDA Compute Capability 5.0+ required");

        const int capacity = 16;
        var messageSize = MemoryPackSerializer.Serialize(new TestGpuMessage()).Length;

        var (hostQueue, gpuBuffer, bridge) = CudaMessageQueueBridgeFactory.CreateGpuRingBufferBridge<TestGpuMessage>(
            deviceId: 0,
            capacity: capacity,
            messageSize: messageSize,
            useUnifiedMemory: false,
            enableDmaTransfer: true, // Enable DMA for WSL2 mode
            logger: null);

        using (gpuBuffer)
        using (bridge)
        {
            // Start DMA transfer tasks
            bridge.Start();

            var testMessage = new TestGpuMessage
            {
                SourceId = 1,
                TargetId = 2,
                Contribution = 0.5f,
                Iteration = 1
            };

            // Act - Enqueue to host
            var enqueued = hostQueue.TryEnqueue(testMessage);
            enqueued.Should().BeTrue("message should enqueue successfully");

            Output.WriteLine($"Enqueued message to host queue: Source={testMessage.SourceId}");

            // Wait for DMA transfer (Host→GPU)
            await Task.Delay(200);

            // Assert - Check GPU tail was incremented
            var tail = gpuBuffer.ReadTail();
            tail.Should().BeGreaterThan(0u, "DMA should have transferred message to GPU");

            Output.WriteLine($"GPU tail counter after DMA: {tail}");

            // Stop bridge
            await bridge.StopAsync();
        }
    }

    [SkippableFact]
    [Trait("Test", "GpuRingBufferBridge.DMA")]
    public async Task GpuRingBufferBridge_DmaTransferMode_TransfersMessagesGpuToHost()
    {
        // Arrange
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(5, 0), "CUDA Compute Capability 5.0+ required");

        const int capacity = 16;
        var messageSize = MemoryPackSerializer.Serialize(new TestGpuMessage()).Length;

        var (hostQueue, gpuBuffer, bridge) = CudaMessageQueueBridgeFactory.CreateGpuRingBufferBridge<TestGpuMessage>(
            deviceId: 0,
            capacity: capacity,
            messageSize: messageSize,
            useUnifiedMemory: false,
            enableDmaTransfer: true,
            logger: null);

        using (gpuBuffer)
        using (bridge)
        {
            // Start DMA transfer tasks
            bridge.Start();

            var testMessage = new TestGpuMessage
            {
                SourceId = 10,
                TargetId = 20,
                Contribution = 0.75f,
                Iteration = 3
            };

            // Act - Write message directly to GPU buffer (simulate GPU kernel output)
            gpuBuffer.WriteMessage(testMessage, index: 0);

            // Advance GPU head to simulate kernel consumption
            gpuBuffer.WriteHead(0u);
            gpuBuffer.WriteTail(1u); // One message available

            Output.WriteLine($"Wrote message to GPU buffer: Source={testMessage.SourceId}");

            // Wait for DMA transfer (GPU→Host)
            await Task.Delay(200);

            // Assert - Check if message was transferred to host queue
            var success = hostQueue.TryDequeue(out var receivedMessage);
            success.Should().BeTrue("DMA should have transferred message from GPU to host");

            receivedMessage.Should().NotBeNull();
            receivedMessage!.SourceId.Should().Be(testMessage.SourceId);
            receivedMessage.TargetId.Should().Be(testMessage.TargetId);
            receivedMessage.Contribution.Should().BeApproximately(testMessage.Contribution, 0.0001f);

            Output.WriteLine($"Received message from host queue: Source={receivedMessage.SourceId}");

            // Stop bridge
            await bridge.StopAsync();
        }
    }

    [SkippableFact]
    [Trait("Test", "GpuRingBufferBridge.Bidirectional")]
    public async Task GpuRingBufferBridge_DmaMode_BidirectionalMessageFlow()
    {
        // Arrange
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(5, 0), "CUDA Compute Capability 5.0+ required");

        const int capacity = 16;
        var messageSize = MemoryPackSerializer.Serialize(new TestGpuMessage()).Length;

        var (hostQueue, gpuBuffer, bridge) = CudaMessageQueueBridgeFactory.CreateGpuRingBufferBridge<TestGpuMessage>(
            deviceId: 0,
            capacity: capacity,
            messageSize: messageSize,
            useUnifiedMemory: false,
            enableDmaTransfer: true,
            logger: null);

        using (gpuBuffer)
        using (bridge)
        {
            bridge.Start();

            // Act 1 - Send message Host→GPU
            var outgoingMessage = new TestGpuMessage
            {
                SourceId = 100,
                TargetId = 200,
                Contribution = 0.9f,
                Iteration = 10
            };

            hostQueue.TryEnqueue(outgoingMessage);
            Output.WriteLine($"Sent Host→GPU: Source={outgoingMessage.SourceId}");

            // Wait for Host→GPU transfer
            await Task.Delay(100);

            var gpuTail = gpuBuffer.ReadTail();
            gpuTail.Should().BeGreaterThan(0u, "message should be in GPU buffer");

            // Clear host queue (GPU→Host loop may have transferred message back)
            while (hostQueue.TryDequeue(out _))
            {
                // Empty - just draining the queue
            }
            Output.WriteLine("Cleared host queue before Part 2");

            // Act 2 - Send message GPU→Host (simulate kernel processing)
            var returnMessage = new TestGpuMessage
            {
                SourceId = 200,
                TargetId = 100,
                Contribution = 0.95f,
                Iteration = 10
            };

            gpuBuffer.WriteMessage(returnMessage, index: 0);
            gpuBuffer.WriteHead(0u);
            gpuBuffer.WriteTail(1u);

            Output.WriteLine($"Sent GPU→Host: Source={returnMessage.SourceId}");

            // Wait for GPU→Host transfer
            await Task.Delay(200);

            // Assert - Check bidirectional flow
            var receivedFromGpu = hostQueue.TryDequeue(out var message);
            receivedFromGpu.Should().BeTrue("should receive message from GPU");

            message.Should().NotBeNull();
            message.SourceId.Should().Be(returnMessage.SourceId);

            Output.WriteLine($"Received from GPU: Source={message.SourceId}");

            await bridge.StopAsync();
        }
    }

    [SkippableFact]
    [Trait("Test", "GpuRingBufferBridge.Factory")]
    public void GpuRingBufferBridgeFactory_CreateBridge_ReturnsValidComponents()
    {
        // Arrange
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(5, 0), "CUDA Compute Capability 5.0+ required");

        const int capacity = 16;
        var messageSize = MemoryPackSerializer.Serialize(new TestGpuMessage()).Length;

        // Act
        var (hostQueue, gpuBuffer, bridge) = CudaMessageQueueBridgeFactory.CreateGpuRingBufferBridge<TestGpuMessage>(
            deviceId: 0,
            capacity: capacity,
            messageSize: messageSize,
            useUnifiedMemory: false,
            enableDmaTransfer: true,
            logger: null);

        using (gpuBuffer)
        using (bridge)
        {
            // Assert
            hostQueue.Should().NotBeNull();
            hostQueue.Capacity.Should().Be(capacity);

            gpuBuffer.Should().NotBeNull();
            gpuBuffer.Capacity.Should().Be(capacity);
            gpuBuffer.MessageSize.Should().Be(messageSize);

            bridge.Should().NotBeNull();
            bridge.HostQueue.Should().Be(hostQueue);
            bridge.GpuBuffer.Should().Be(gpuBuffer);
            bridge.IsDmaTransferEnabled.Should().BeTrue();
        }
    }

    [SkippableFact]
    [Trait("Test", "GpuRingBuffer.Interface")]
    public void GpuRingBuffer_ImplementsIGpuRingBuffer_ExposesRequiredProperties()
    {
        // Arrange
        Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(5, 0), "CUDA Compute Capability 5.0+ required");

        using var gpuBuffer = new GpuRingBuffer<TestGpuMessage>(
            deviceId: 0,
            capacity: 16,
            messageSize: 256,
            useUnifiedMemory: false,
            logger: null);

        // Act - Cast to interface
        IGpuRingBuffer interfaceRef = gpuBuffer;

        // Assert - Interface properties are accessible
        interfaceRef.DeviceHeadPtr.Should().NotBe(IntPtr.Zero);
        interfaceRef.DeviceTailPtr.Should().NotBe(IntPtr.Zero);
        interfaceRef.Capacity.Should().Be(16);
        interfaceRef.MessageSize.Should().Be(256);
        interfaceRef.IsUnifiedMemory.Should().BeFalse();

        Output.WriteLine($"Interface properties: head=0x{interfaceRef.DeviceHeadPtr:X}, tail=0x{interfaceRef.DeviceTailPtr:X}");
    }
}
