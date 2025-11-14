// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using DotCompute.Backends.OpenCL;
using DotCompute.Backends.OpenCL.Factory;
using DotCompute.Backends.OpenCL.Messaging;
using DotCompute.Tests.Common;
using DotCompute.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Xunit;

namespace DotCompute.Hardware.OpenCL.Tests.Messaging;

/// <summary>
/// Hardware tests for OpenCL message queue implementation.
/// Validates lock-free atomic operations on real OpenCL devices.
/// </summary>
[Trait("Category", "Hardware")]
[Trait("Category", "OpenCL")]
[Trait("Category", "MessageQueue")]
public sealed class OpenCLMessageQueueHardwareTests : IAsyncLifetime
{
    private OpenCLMessageQueue<TestMessage>? _queue;
    private bool _disposed;

    public async Task InitializeAsync()
    {
        Skip.IfNot(HardwareDetection.IsOpenCLAvailable(), "OpenCL device not available");

        var options = new MessageQueueOptions
        {
            Capacity = 256,
            BackpressureStrategy = BackpressureStrategy.Reject,
            EnableDeduplication = false,
            EnablePriorityQueue = false,
            DeduplicationWindowSize = 256
        };

        var logger = NullLogger<OpenCLMessageQueue<TestMessage>>.Instance;
        _queue = new OpenCLMessageQueue<TestMessage>(options, logger);

        // Initialize with OpenCL context
        var factory = new OpenCLAcceleratorFactory(NullLogger<OpenCLAcceleratorFactory>.Instance);
        var devices = factory.GetAvailableDevices();
        var device = devices.FirstOrDefault() ?? throw new InvalidOperationException("No OpenCL devices available");
        var contextLogger = NullLogger<OpenCLContext>.Instance;
        var context = new OpenCLContext(device, contextLogger);
        await _queue.InitializeAsync(context);
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_queue is not null)
        {
            await _queue.DisposeAsync();
        }

        _disposed = true;
    }

    /// <summary>
    /// Test 1: Enqueue a message to GPU memory and verify success
    /// </summary>
    [SkippableFact]
    public void Enqueue_Should_Write_To_GPU_Memory()
    {
        // Arrange
        var message = new TestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 1,
            CorrelationId = Guid.NewGuid(),
            Payload = "Test payload for OpenCL GPU memory"
        };

        // Act
        var result = _queue!.TryEnqueue(message);

        // Assert
        result.Should().BeTrue("message should be enqueued successfully to GPU memory");
        _queue.Count.Should().Be(1, "queue should contain exactly one message");
    }

    /// <summary>
    /// Test 2: Dequeue a message from GPU memory and verify contents
    /// </summary>
    [SkippableFact]
    public void Dequeue_Should_Read_From_GPU_Memory()
    {
        // Arrange
        var originalMessage = new TestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 2,
            CorrelationId = Guid.NewGuid(),
            Payload = "Dequeue test payload"
        };

        _ = _queue!.TryEnqueue(originalMessage);

        // Act
        var result = _queue.TryDequeue(out var dequeuedMessage);

        // Assert
        result.Should().BeTrue("message should be dequeued successfully");
        dequeuedMessage.Should().NotBeNull();
        dequeuedMessage!.MessageId.Should().Be(originalMessage.MessageId);
        dequeuedMessage.Priority.Should().Be(originalMessage.Priority);
        dequeuedMessage.CorrelationId.Should().Be(originalMessage.CorrelationId);
        dequeuedMessage.Payload.Should().Be(originalMessage.Payload);
        _queue.Count.Should().Be(0, "queue should be empty after dequeue");
    }

    /// <summary>
    /// Test 3: Peek at message without removing from queue
    /// </summary>
    [SkippableFact]
    public void Peek_Should_Not_Remove_Message()
    {
        // Arrange
        var message = new TestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 3,
            CorrelationId = Guid.NewGuid(),
            Payload = "Peek test"
        };

        _ = _queue!.TryEnqueue(message);

        // Act
        var peekResult = _queue.TryPeek(out var peekedMessage);
        var count = _queue.Count;

        // Assert
        peekResult.Should().BeTrue("peek should succeed");
        peekedMessage.Should().NotBeNull();
        peekedMessage!.MessageId.Should().Be(message.MessageId);
        count.Should().Be(1, "message should still be in queue after peek");
    }

    /// <summary>
    /// Test 4: Verify empty queue detection
    /// </summary>
    [SkippableFact]
    public void IsEmpty_Should_Return_True_For_Empty_Queue()
    {
        // Act & Assert
        _queue!.IsEmpty.Should().BeTrue("newly initialized queue should be empty");
        _queue.Count.Should().Be(0);
    }

    /// <summary>
    /// Test 5: Verify full queue detection
    /// </summary>
    [SkippableFact]
    public void IsFull_Should_Return_True_When_At_Capacity()
    {
        // Arrange - Fill queue to capacity (256 messages)
        for (var i = 0; i < 256; i++)
        {
            var message = new TestMessage
            {
                MessageId = Guid.NewGuid(),
                Priority = 1,
                CorrelationId = Guid.NewGuid(),
                Payload = $"Message {i}"
            };

            _ = _queue!.TryEnqueue(message);
        }

        // Act & Assert
        _queue!.IsFull.Should().BeTrue("queue should be full at capacity");
        _queue.Count.Should().Be(256);
    }

    /// <summary>
    /// Test 6: Verify backpressure rejection when queue is full
    /// </summary>
    [SkippableFact]
    public void Enqueue_Should_Reject_When_Full_With_Reject_Strategy()
    {
        // Arrange - Fill queue to capacity
        for (var i = 0; i < 256; i++)
        {
            var message = new TestMessage
            {
                MessageId = Guid.NewGuid(),
                Priority = 1,
                CorrelationId = Guid.NewGuid(),
                Payload = $"Fill message {i}"
            };

            _ = _queue!.TryEnqueue(message);
        }

        var extraMessage = new TestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 1,
            CorrelationId = Guid.NewGuid(),
            Payload = "Should be rejected"
        };

        // Act
        var result = _queue!.TryEnqueue(extraMessage);

        // Assert
        result.Should().BeFalse("enqueue should be rejected when queue is full");
        _queue.Count.Should().Be(256, "count should remain at capacity");
    }

    /// <summary>
    /// Test 7: Verify dequeue from empty queue returns false
    /// </summary>
    [SkippableFact]
    public void Dequeue_Should_Return_False_For_Empty_Queue()
    {
        // Act
        var result = _queue!.TryDequeue(out var message);

        // Assert
        result.Should().BeFalse("dequeue should fail on empty queue");
        message.Should().BeNull("no message should be returned");
    }

    /// <summary>
    /// Test 8: Verify FIFO ordering with multiple messages
    /// </summary>
    [SkippableFact]
    public void Multiple_Messages_Should_Maintain_FIFO_Order()
    {
        // Arrange
        var messages = new List<TestMessage>();
        for (var i = 0; i < 10; i++)
        {
            var msg = new TestMessage
            {
                MessageId = Guid.NewGuid(),
                Priority = 1,
                CorrelationId = Guid.NewGuid(),
                Payload = $"FIFO test {i}"
            };

            messages.Add(msg);
            _ = _queue!.TryEnqueue(msg);
        }

        // Act & Assert
        for (var i = 0; i < 10; i++)
        {
            var result = _queue!.TryDequeue(out var dequeuedMessage);
            result.Should().BeTrue($"dequeue {i} should succeed");
            dequeuedMessage.Should().NotBeNull();
            dequeuedMessage!.MessageId.Should().Be(messages[i].MessageId, $"message {i} should match FIFO order");
        }

        _queue!.IsEmpty.Should().BeTrue("queue should be empty after all dequeues");
    }

    /// <summary>
    /// Test 9: Verify circular buffer wrap-around handling
    /// </summary>
    [SkippableFact]
    public void Circular_Buffer_Should_Handle_Wraparound()
    {
        // Arrange - Fill queue completely
        for (var i = 0; i < 256; i++)
        {
            var message = new TestMessage
            {
                MessageId = Guid.NewGuid(),
                Priority = 1,
                CorrelationId = Guid.NewGuid(),
                Payload = $"Initial {i}"
            };

            _ = _queue!.TryEnqueue(message);
        }

        // Dequeue half the messages
        for (var i = 0; i < 128; i++)
        {
            _ = _queue!.TryDequeue(out _);
        }

        // Enqueue new messages (should wrap around in circular buffer)
        var wrapMessages = new List<TestMessage>();
        for (var i = 0; i < 128; i++)
        {
            var msg = new TestMessage
            {
                MessageId = Guid.NewGuid(),
                Priority = 1,
                CorrelationId = Guid.NewGuid(),
                Payload = $"Wrap {i}"
            };

            wrapMessages.Add(msg);
            var result = _queue!.TryEnqueue(msg);
            result.Should().BeTrue($"wrap-around enqueue {i} should succeed");
        }

        // Act & Assert - Dequeue remaining original messages
        for (var i = 0; i < 128; i++)
        {
            var result = _queue!.TryDequeue(out var msg);
            result.Should().BeTrue($"dequeue original {i} should succeed");
            msg.Should().NotBeNull();
            msg!.Payload.Should().StartWith("Initial", $"message {i} should be from original batch");
        }

        // Dequeue wrap-around messages
        for (var i = 0; i < 128; i++)
        {
            var result = _queue!.TryDequeue(out var msg);
            result.Should().BeTrue($"dequeue wrap {i} should succeed");
            msg.Should().NotBeNull();
            msg!.MessageId.Should().Be(wrapMessages[i].MessageId);
        }

        _queue!.Count.Should().Be(0, "all messages should be dequeued");
    }

    /// <summary>
    /// Test 10: Verify large payload handling
    /// </summary>
    [SkippableFact]
    public void Large_Payload_Should_Be_Handled_Correctly()
    {
        // Arrange - Create 1KB payload
        var largePayload = new StringBuilder();
        for (var i = 0; i < 1024; i++)
        {
            _ = largePayload.Append((char)('A' + (i % 26)));
        }

        var message = new TestMessage
        {
            MessageId = Guid.NewGuid(),
            Priority = 1,
            CorrelationId = Guid.NewGuid(),
            Payload = largePayload.ToString()
        };

        // Act
        var enqueueResult = _queue!.TryEnqueue(message);
        var dequeueResult = _queue.TryDequeue(out var dequeuedMessage);

        // Assert
        enqueueResult.Should().BeTrue("large payload should be enqueued");
        dequeueResult.Should().BeTrue("large payload should be dequeued");
        dequeuedMessage.Should().NotBeNull();
        dequeuedMessage!.Payload.Should().HaveLength(1024);
        dequeuedMessage.Payload.Should().Be(message.Payload);
    }

    /// <summary>
    /// Test 11: Verify multiple enqueue/dequeue cycles
    /// </summary>
    [SkippableFact]
    public void Multiple_Cycles_Should_Work_Correctly()
    {
        // Arrange & Act
        for (var cycle = 0; cycle < 5; cycle++)
        {
            // Enqueue batch
            for (var i = 0; i < 50; i++)
            {
                var message = new TestMessage
                {
                    MessageId = Guid.NewGuid(),
                    Priority = 1,
                    CorrelationId = Guid.NewGuid(),
                    Payload = $"Cycle {cycle}, Message {i}"
                };

                var result = _queue!.TryEnqueue(message);
                result.Should().BeTrue($"cycle {cycle}, enqueue {i} should succeed");
            }

            _queue!.Count.Should().Be(50, $"cycle {cycle} should have 50 messages");

            // Dequeue batch
            for (var i = 0; i < 50; i++)
            {
                var result = _queue!.TryDequeue(out var msg);
                result.Should().BeTrue($"cycle {cycle}, dequeue {i} should succeed");
                msg.Should().NotBeNull();
            }

            _queue!.Count.Should().Be(0, $"cycle {cycle} should be empty");
        }
    }

    /// <summary>
    /// Test 12: Verify concurrent access from multiple threads
    /// </summary>
    [SkippableFact]
    public void Concurrent_Access_Should_Be_Thread_Safe()
    {
        // Arrange
        const int threadCount = 10;
        const int messagesPerThread = 20;
        var tasks = new Task[threadCount];
        var enqueueSuccessCount = 0;

        // Act - Multiple threads enqueuing concurrently
        for (var t = 0; t < threadCount; t++)
        {
            var threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (var i = 0; i < messagesPerThread; i++)
                {
                    var message = new TestMessage
                    {
                        MessageId = Guid.NewGuid(),
                        Priority = 1,
                        CorrelationId = Guid.NewGuid(),
                        Payload = $"Thread {threadId}, Message {i}"
                    };

                    if (_queue!.TryEnqueue(message))
                    {
                        Interlocked.Increment(ref enqueueSuccessCount);
                    }
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert
        enqueueSuccessCount.Should().BeGreaterThan(0, "some messages should be enqueued");
        _queue!.Count.Should().BeLessThanOrEqualTo(threadCount * messagesPerThread);
        _queue.Count.Should().Be(enqueueSuccessCount, "count should match successful enqueues");

        // Verify all messages can be dequeued
        var dequeueCount = 0;
        while (_queue.TryDequeue(out _))
        {
            dequeueCount++;
        }

        dequeueCount.Should().Be(enqueueSuccessCount, "all enqueued messages should be dequeuable");
    }

    /// <summary>
    /// Test message implementation for hardware tests
    /// </summary>
    private sealed class TestMessage : IRingKernelMessage
    {
        public Guid MessageId { get; set; } = Guid.NewGuid();
        public string MessageType => "TestMessage";
        public byte Priority { get; set; } = 128;
        public Guid? CorrelationId { get; set; }
        public string Payload { get; set; } = string.Empty;

        // Calculate serialized size: MessageId(16) + Priority(1) + CorrelationId(16) + Length(4) + Payload(UTF8)
        public int PayloadSize => 16 + 1 + 16 + 4 + System.Text.Encoding.UTF8.GetByteCount(Payload ?? string.Empty);

        public ReadOnlySpan<byte> Serialize()
        {
            var buffer = new byte[PayloadSize];
            var span = buffer.AsSpan();
            var offset = 0;

            // MessageId (16 bytes)
            MessageId.TryWriteBytes(span[offset..]);
            offset += 16;

            // Priority (1 byte)
            span[offset] = Priority;
            offset += 1;

            // CorrelationId (16 bytes, nullable)
            if (CorrelationId.HasValue)
            {
                CorrelationId.Value.TryWriteBytes(span[offset..]);
            }
            offset += 16;

            // Payload length (4 bytes)
            var payloadBytes = System.Text.Encoding.UTF8.GetBytes(Payload ?? string.Empty);
            BitConverter.TryWriteBytes(span[offset..], payloadBytes.Length);
            offset += 4;

            // Payload (variable length)
            payloadBytes.CopyTo(span[offset..]);

            return buffer;
        }

        public void Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length < 37)
            {
                throw new ArgumentException("Insufficient data for deserialization", nameof(data));
            }

            var offset = 0;

            // MessageId (16 bytes)
            var messageId = new Guid(data.Slice(offset, 16));
            offset += 16;

            // Priority (1 byte)
            var priority = data[offset];
            offset += 1;

            // CorrelationId (16 bytes)
            var correlationId = new Guid(data.Slice(offset, 16));
            offset += 16;

            // Payload length (4 bytes)
            var payloadLength = BitConverter.ToInt32(data.Slice(offset, 4));
            offset += 4;

            // Payload (variable length)
            var payload = System.Text.Encoding.UTF8.GetString(data.Slice(offset, payloadLength));

            // Set properties directly
            MessageId = messageId;
            Priority = priority;
            CorrelationId = correlationId;
            Payload = payload;
        }
    }
}
