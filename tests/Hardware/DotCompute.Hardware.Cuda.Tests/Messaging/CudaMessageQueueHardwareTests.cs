// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using DotCompute.Backends.CUDA.Messaging;
using DotCompute.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Hardware.Cuda.Tests.Messaging;

/// <summary>
/// Hardware tests for CUDA message queue with GPU device memory.
/// Validates lock-free atomic operations, message integrity, and concurrent access patterns.
/// </summary>
[Collection("CUDA Hardware")]
public class CudaMessageQueueHardwareTests : IAsyncLifetime
{
    private CudaMessageQueue<TestMessage>? _queue;
    private bool _disposed;

    public async Task InitializeAsync()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        var options = new MessageQueueOptions
        {
            Capacity = 256,
            BackpressureStrategy = BackpressureStrategy.Reject,
            EnableDeduplication = false,
            EnablePriorityQueue = false,
            DeduplicationWindowSize = 256  // Explicitly set to match capacity
        };

        var logger = NullLogger<CudaMessageQueue<TestMessage>>.Instance;
        _queue = new CudaMessageQueue<TestMessage>(options, logger);
        await _queue.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_queue is not null)
        {
            _queue.Dispose();
        }

        _disposed = true;
        await Task.CompletedTask;
    }

    #region Basic Enqueue/Dequeue Tests

    [SkippableFact(DisplayName = "Enqueue should store message in GPU memory")]
    public void Enqueue_ShouldStoreMessageInGpuMemory()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        var message = new TestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 128,
            Payload = "Test message for GPU storage"
        };

        // Act
        var result = _queue!.TryEnqueue(message);

        // Assert
        result.Should().BeTrue();
        _queue.Count.Should().Be(1);
        _queue.IsEmpty.Should().BeFalse();
    }

    [SkippableFact(DisplayName = "Dequeue should retrieve message from GPU memory")]
    public void Dequeue_ShouldRetrieveMessageFromGpuMemory()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        var originalMessage = new TestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 128,
            Payload = "Test dequeue from GPU"
        };
        _queue!.TryEnqueue(originalMessage);

        // Act
        var result = _queue.TryDequeue(out var dequeuedMessage);

        // Assert
        result.Should().BeTrue();
        dequeuedMessage.Should().NotBeNull();
        dequeuedMessage!.MessageId.Should().Be(originalMessage.MessageId);
        dequeuedMessage.Payload.Should().Be(originalMessage.Payload);
        _queue.Count.Should().Be(0);
    }

    [SkippableFact(DisplayName = "Peek should read without dequeuing")]
    public void Peek_ShouldReadWithoutDequeuing()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        var message = new TestMessage { MessageId = Guid.NewGuid(), Payload = "Peek test" };
        _queue!.TryEnqueue(message);

        // Act
        var result = _queue.TryPeek(out var peekedMessage);

        // Assert
        result.Should().BeTrue();
        peekedMessage.Should().NotBeNull();
        peekedMessage!.MessageId.Should().Be(message.MessageId);
        _queue.Count.Should().Be(1); // Should not dequeue
    }

    #endregion

    #region Queue State Tests

    [SkippableFact(DisplayName = "IsEmpty should return true for empty queue")]
    public void IsEmpty_EmptyQueue_ShouldReturnTrue()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Assert
        _queue!.IsEmpty.Should().BeTrue();
        _queue.Count.Should().Be(0);
    }

    [SkippableFact(DisplayName = "IsFull should return true when capacity reached")]
    public void IsFull_FullQueue_ShouldReturnTrue()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange - Fill queue to capacity (256 messages)
        for (int i = 0; i < _queue!.Capacity; i++)
        {
            var message = new TestMessage
            {
                MessageId = Guid.NewGuid(),
                Payload = $"Message {i}"
            };
            _queue.TryEnqueue(message).Should().BeTrue();
        }

        // Assert
        _queue.IsFull.Should().BeTrue();
        _queue.Count.Should().Be(_queue.Capacity);
    }

    [SkippableFact(DisplayName = "Enqueue on full queue should reject with BackpressureStrategy.Reject")]
    public void Enqueue_FullQueueWithReject_ShouldReturnFalse()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange - Fill queue to capacity
        for (int i = 0; i < _queue!.Capacity; i++)
        {
            _queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid() }).Should().BeTrue();
        }

        _queue.Count.Should().Be(_queue.Capacity);

        // Act - Try to enqueue one more message (should be rejected)
        var result = _queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid() });

        // Assert
        result.Should().BeFalse();
        _queue.Count.Should().Be(_queue.Capacity);
    }

    [SkippableFact(DisplayName = "Dequeue on empty queue should return false")]
    public void Dequeue_EmptyQueue_ShouldReturnFalse()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Act
        var result = _queue!.TryDequeue(out var message);

        // Assert
        result.Should().BeFalse();
        message.Should().BeNull();
    }

    #endregion

    #region FIFO Ordering Tests

    [SkippableFact(DisplayName = "Messages should be dequeued in FIFO order")]
    public void Dequeue_MultipleMessages_ShouldMaintainFifoOrder()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        var messageIds = new List<Guid>();
        for (int i = 0; i < 10; i++)
        {
            var id = Guid.NewGuid();
            messageIds.Add(id);
            _queue!.TryEnqueue(new TestMessage
            {
                MessageId = id,
                Payload = $"Message {i}"
            });
        }

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            _queue!.TryDequeue(out var message).Should().BeTrue();
            message.Should().NotBeNull();
            message!.MessageId.Should().Be(messageIds[i]);
        }
    }

    #endregion

    #region Wrap-Around Tests

    [SkippableFact(DisplayName = "Queue should wrap around correctly at capacity boundary")]
    public void WrapAround_ShouldHandleCircularBufferCorrectly()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange - Fill queue
        for (int i = 0; i < _queue!.Capacity; i++)
        {
            _queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid(), Payload = $"Fill {i}" });
        }

        // Dequeue half
        for (int i = 0; i < _queue.Capacity / 2; i++)
        {
            _queue.TryDequeue(out _);
        }

        // Act - Enqueue more (causing wrap-around)
        var wrapAroundIds = new List<Guid>();
        for (int i = 0; i < _queue.Capacity / 2; i++)
        {
            var id = Guid.NewGuid();
            wrapAroundIds.Add(id);
            _queue.TryEnqueue(new TestMessage { MessageId = id, Payload = $"Wrap {i}" }).Should().BeTrue();
        }

        // Assert - Dequeue remaining from first batch
        for (int i = 0; i < _queue.Capacity / 2; i++)
        {
            _queue.TryDequeue(out _).Should().BeTrue();
        }

        // Assert - Dequeue wrap-around messages (FIFO order maintained)
        for (int i = 0; i < wrapAroundIds.Count; i++)
        {
            _queue.TryDequeue(out var message).Should().BeTrue();
            message!.MessageId.Should().Be(wrapAroundIds[i]);
        }

        _queue.IsEmpty.Should().BeTrue();
    }

    #endregion

    #region Message Integrity Tests

    [SkippableFact(DisplayName = "Large payloads should serialize/deserialize correctly")]
    public void LargePayload_ShouldMaintainIntegrity()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        var largePayload = new string('X', 1000); // 1KB payload
        var message = new TestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 200,
            Payload = largePayload,
            CorrelationId = Guid.NewGuid()
        };

        // Act
        _queue!.TryEnqueue(message).Should().BeTrue();
        _queue.TryDequeue(out var dequeued).Should().BeTrue();

        // Assert
        dequeued.Should().NotBeNull();
        dequeued!.MessageId.Should().Be(message.MessageId);
        dequeued.Priority.Should().Be(message.Priority);
        dequeued.Payload.Should().Be(largePayload);
        dequeued.CorrelationId.Should().Be(message.CorrelationId);
    }

    [SkippableFact(DisplayName = "Multiple enqueue/dequeue cycles should maintain message integrity")]
    public void MultipleCycles_ShouldMaintainIntegrity()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const int cycles = 5;
        const int messagesPerCycle = 50;

        for (int cycle = 0; cycle < cycles; cycle++)
        {
            var messageIds = new List<Guid>();

            // Enqueue
            for (int i = 0; i < messagesPerCycle; i++)
            {
                var id = Guid.NewGuid();
                messageIds.Add(id);
                _queue!.TryEnqueue(new TestMessage
                {
                    MessageId = id,
                    Payload = $"Cycle {cycle}, Message {i}"
                }).Should().BeTrue();
            }

            // Dequeue and verify
            for (int i = 0; i < messagesPerCycle; i++)
            {
                _queue!.TryDequeue(out var message).Should().BeTrue();
                message!.MessageId.Should().Be(messageIds[i]);
            }

            _queue!.IsEmpty.Should().BeTrue();
        }
    }

    #endregion

    #region Concurrent Access Tests

    [SkippableFact(DisplayName = "Concurrent enqueue from multiple threads should be safe")]
    public async Task ConcurrentEnqueue_ShouldBeSafe()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const int threadCount = 10;
        const int messagesPerThread = 20;
        var tasks = new List<Task>();

        // Act - Multiple threads enqueuing concurrently
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t; // Capture for lambda
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < messagesPerThread; i++)
                {
                    var message = new TestMessage
                    {
                        MessageId = Guid.NewGuid(),
                        Payload = $"Thread {threadId}, Message {i}"
                    };
                    _queue!.TryEnqueue(message);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        _queue!.Count.Should().BeLessThanOrEqualTo(threadCount * messagesPerThread);
        _queue.Count.Should().BeGreaterThan(0); // At least some messages enqueued
    }

    #endregion

    #region Test Message Type

    private sealed class TestMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; } = Guid.NewGuid();
        public string MessageType => "TestMessage";
        public byte Priority { get; set; } = 128;
        public Guid? CorrelationId { get; set; }
        public string Payload { get; set; } = string.Empty;

        public int PayloadSize => 16 + 1 + 16 + 4 + System.Text.Encoding.UTF8.GetByteCount(Payload ?? string.Empty); // MessageId + Priority + CorrelationId + Length + Payload UTF8

        public ReadOnlySpan<byte> Serialize()
        {
            var buffer = new byte[PayloadSize];
            int offset = 0;

            // MessageId (16 bytes)
            MessageId.TryWriteBytes(buffer.AsSpan(offset, 16));
            offset += 16;

            // Priority (1 byte)
            buffer[offset] = Priority;
            offset += 1;

            // CorrelationId (16 bytes, nullable)
            if (CorrelationId.HasValue)
            {
                CorrelationId.Value.TryWriteBytes(buffer.AsSpan(offset, 16));
            }
            offset += 16;

            // Payload length (4 bytes)
            BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), Payload.Length);
            offset += 4;

            // Payload (variable length, UTF8)
            if (!string.IsNullOrEmpty(Payload))
            {
                var payloadBytes = System.Text.Encoding.UTF8.GetBytes(Payload);
                Array.Resize(ref buffer, offset + payloadBytes.Length);
                payloadBytes.CopyTo(buffer, offset);
            }

            return buffer;
        }

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < 37)
            {
                return; // Minimum: 16 (MessageId) + 1 (Priority) + 16 (CorrelationId) + 4 (length)
            }

            int offset = 0;

            // MessageId
            MessageId = new Guid(data.Slice(offset, 16));
            offset += 16;

            // Priority
            Priority = data[offset];
            offset += 1;

            // CorrelationId
            var correlationBytes = data.Slice(offset, 16);
            if (!correlationBytes.SequenceEqual(new byte[16]))
            {
                CorrelationId = new Guid(correlationBytes);
            }
            offset += 16;

            // Payload length
            int payloadLength = BitConverter.ToInt32(data.Slice(offset, 4));
            offset += 4;

            // Payload
            if (payloadLength > 0 && offset + payloadLength <= data.Length)
            {
                Payload = System.Text.Encoding.UTF8.GetString(data.Slice(offset, payloadLength));
            }
        }
    }

    #endregion
}
