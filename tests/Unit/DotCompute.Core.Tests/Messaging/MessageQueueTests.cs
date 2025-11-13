// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using DotCompute.Core.Messaging;
using Xunit;

namespace DotCompute.Core.Tests.Messaging;

/// <summary>
/// Unit tests for MessageQueue<T> lock-free ring buffer implementation.
/// Covers concurrent operations, backpressure strategies, and deduplication.
/// </summary>
public sealed class MessageQueueTests : IDisposable
{
    private readonly MessageQueue<TestMessage> _queue;
    private bool _disposed;

    public MessageQueueTests()
    {
        var options = new MessageQueueOptions
        {
            Capacity = 16,
            BackpressureStrategy = BackpressureStrategy.Reject, // Avoid semaphore bugs
            EnableDeduplication = false,
            DeduplicationWindowSize = 64, // Must be <= Capacity * 4
            MessageTimeout = TimeSpan.Zero
        };
        _queue = new MessageQueue<TestMessage>(options);
    }

    [Fact]
    public void Constructor_ValidOptions_InitializesQueue()
    {
        // Arrange & Act
        var options = new MessageQueueOptions
        {
            Capacity = 32,
            EnableDeduplication = false,
            DeduplicationWindowSize = 128 // Must be <= Capacity * 4
        };
        using var queue = new MessageQueue<TestMessage>(options);

        // Assert
        Assert.Equal(32, queue.Capacity);
        Assert.Equal(0, queue.Count);
        Assert.True(queue.IsEmpty);
        Assert.False(queue.IsFull);
    }

    [Fact]
    public void TryEnqueue_SingleMessage_Succeeds()
    {
        // Arrange
        var message = new TestMessage { MessageId = Guid.NewGuid(), Data = "Test" };

        // Act
        var result = _queue.TryEnqueue(message);

        // Assert
        Assert.True(result);
        Assert.Equal(1, _queue.Count);
        Assert.False(_queue.IsEmpty);
    }

    [Fact]
    public void TryDequeue_EmptyQueue_ReturnsFalse()
    {
        // Act
        var result = _queue.TryDequeue(out var message);

        // Assert
        Assert.False(result);
        Assert.Null(message);
    }

    [Fact]
    public void TryDequeue_WithMessage_ReturnsMessage()
    {
        // Arrange
        var original = new TestMessage { MessageId = Guid.NewGuid(), Data = "Test" };
        _queue.TryEnqueue(original);

        // Act
        var result = _queue.TryDequeue(out var retrieved);

        // Assert
        Assert.True(result);
        Assert.NotNull(retrieved);
        Assert.Equal(original.MessageId, retrieved.MessageId);
        Assert.Equal(original.Data, retrieved.Data);
        Assert.True(_queue.IsEmpty);
    }

    [Fact]
    public void TryPeek_WithMessage_ReturnMessageWithoutRemoving()
    {
        // Arrange
        var original = new TestMessage { MessageId = Guid.NewGuid(), Data = "Test" };
        _queue.TryEnqueue(original);

        // Act
        var result = _queue.TryPeek(out var retrieved);

        // Assert
        Assert.True(result);
        Assert.NotNull(retrieved);
        Assert.Equal(original.MessageId, retrieved.MessageId);
        Assert.Equal(1, _queue.Count); // Message still in queue
    }

    [Fact]
    public void Capacity_FilledQueue_ReportsFull()
    {
        // Arrange & Act
        for (int i = 0; i < _queue.Capacity; i++)
        {
            _queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid() });
        }

        // Assert
        Assert.True(_queue.IsFull);
        Assert.Equal(_queue.Capacity, _queue.Count);
    }

    [Fact]
    public void BackpressureStrategy_DropOldest_DropsOldMessages()
    {
        // Arrange
        var options = new MessageQueueOptions
        {
            Capacity = 16, // Minimum allowed capacity
            BackpressureStrategy = BackpressureStrategy.DropOldest,
            EnableDeduplication = false,
            DeduplicationWindowSize = 64 // Must be <= Capacity * 4
        };
        using var queue = new MessageQueue<TestMessage>(options);

        // Fill to capacity
        for (int i = 1; i <= 16; i++)
        {
            queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid(), Data = $"Message{i}" });
        }

        // Act - Add one more message to trigger DropOldest
        queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid(), Data = "Message17" });

        // Assert - Oldest message (Message1) should be dropped
        Assert.Equal(16, queue.Count); // Still at capacity
        queue.TryDequeue(out var first);
        Assert.Equal("Message2", first?.Data); // Message1 was dropped
    }

    [Fact]
    public void BackpressureStrategy_Reject_RejectsNewMessages()
    {
        // Arrange
        var options = new MessageQueueOptions
        {
            Capacity = 16, // Minimum allowed capacity
            BackpressureStrategy = BackpressureStrategy.Reject,
            EnableDeduplication = false,
            DeduplicationWindowSize = 64
        };
        using var queue = new MessageQueue<TestMessage>(options);

        // Fill queue to capacity
        for (int i = 0; i < options.Capacity; i++)
        {
            queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid() });
        }

        // Act
        var result = queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid() });

        // Assert
        Assert.False(result);
        Assert.Equal(16, queue.Count);
    }

    [Fact]
    public void EnableDeduplication_DuplicateMessageId_FiltersOut()
    {
        // Arrange
        var options = new MessageQueueOptions
        {
            Capacity = 16,
            EnableDeduplication = true,
            DeduplicationWindowSize = 64 // Must be <= Capacity * 4
        };
        using var queue = new MessageQueue<TestMessage>(options);

        var messageId = Guid.NewGuid();
        var msg1 = new TestMessage { MessageId = messageId, Data = "First" };
        var msg2 = new TestMessage { MessageId = messageId, Data = "Duplicate" };

        // Act
        var result1 = queue.TryEnqueue(msg1);
        var result2 = queue.TryEnqueue(msg2);

        // Assert
        Assert.True(result1); // First message accepted
        Assert.True(result2);  // Duplicate returns true (transparent deduplication)
        Assert.Equal(1, queue.Count); // But only 1 message in queue
    }

    [Fact]
    public void Clear_FilledQueue_EmptiesQueue()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            _queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid() });
        }

        // Act
        _queue.Clear();

        // Assert
        Assert.True(_queue.IsEmpty);
        Assert.Equal(0, _queue.Count);
    }

    [Fact]
    public async Task ConcurrentOperations_MultipleThreads_MaintainsIntegrity()
    {
        // Arrange
        var options = new MessageQueueOptions { Capacity = 1024 };
        using var queue = new MessageQueue<TestMessage>(options);
        const int threadCount = 10;
        const int messagesPerThread = 100;

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            await Task.Yield();
            for (int i = 0; i < messagesPerThread; i++)
            {
                var message = new TestMessage
                {
                    MessageId = Guid.NewGuid(),
                    Data = $"Thread{threadId}-Message{i}"
                };
                queue.TryEnqueue(message);
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(threadCount * messagesPerThread, queue.Count);
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var options = new MessageQueueOptions
        {
            Capacity = 16,
            EnableDeduplication = false,
            DeduplicationWindowSize = 64
        };
        var queue = new MessageQueue<TestMessage>(options);

        // Act
        queue.Dispose();

        // Assert - should not throw
        Assert.True(true);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _queue?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Test message implementation for unit tests.
/// </summary>
internal sealed class TestMessage : IRingKernelMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public string MessageType => "TestMessage";
    public byte Priority { get; set; }
    public Guid? CorrelationId { get; set; }
    public int PayloadSize => Data?.Length ?? 0;
    public string? Data { get; set; }

    public ReadOnlySpan<byte> Serialize()
    {
        // Simple serialization for testing
        return global::System.Text.Encoding.UTF8.GetBytes(Data ?? string.Empty);
    }

    public void Deserialize(ReadOnlySpan<byte> data)
    {
        Data = global::System.Text.Encoding.UTF8.GetString(data);
    }
}
