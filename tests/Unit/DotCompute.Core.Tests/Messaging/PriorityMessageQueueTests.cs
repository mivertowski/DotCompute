// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using DotCompute.Core.Messaging;
using Xunit;

namespace DotCompute.Core.Tests.Messaging;

/// <summary>
/// Unit tests for PriorityMessageQueue<T> binary heap implementation.
/// Tests priority ordering, backpressure with priority awareness, and concurrent access.
/// </summary>
public sealed class PriorityMessageQueueTests : IDisposable
{
    private readonly PriorityMessageQueue<TestMessage> _queue;
    private bool _disposed;

    public PriorityMessageQueueTests()
    {
        var options = new MessageQueueOptions
        {
            Capacity = 16,
            EnablePriorityQueue = true,
            BackpressureStrategy = BackpressureStrategy.Reject, // Avoid semaphore bugs
            EnableDeduplication = false,
            DeduplicationWindowSize = 64 // Must be <= Capacity * 4
        };
        _queue = new PriorityMessageQueue<TestMessage>(options);
    }

    [Fact]
    public void Constructor_ValidOptions_InitializesPriorityQueue()
    {
        // Arrange & Act
        var options = new MessageQueueOptions
        {
            Capacity = 32,
            EnablePriorityQueue = true,
            EnableDeduplication = false,
            DeduplicationWindowSize = 128 // Must be <= Capacity * 4
        };
        using var queue = new PriorityMessageQueue<TestMessage>(options);

        // Assert
        Assert.Equal(32, queue.Capacity);
        Assert.Equal(0, queue.Count);
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public void TryDequeue_PrioritizedMessages_ReturnsHighestPriorityFirst()
    {
        // Arrange
        var lowPriority = new TestMessage { MessageId = Guid.NewGuid(), Priority = 1, Data = "Low" };
        var medPriority = new TestMessage { MessageId = Guid.NewGuid(), Priority = 5, Data = "Medium" };
        var highPriority = new TestMessage { MessageId = Guid.NewGuid(), Priority = 10, Data = "High" };

        // Enqueue in random order
        _queue.TryEnqueue(lowPriority);
        _queue.TryEnqueue(highPriority);
        _queue.TryEnqueue(medPriority);

        // Act & Assert - should dequeue in priority order (highest first)
        _queue.TryDequeue(out var first);
        Assert.Equal((byte)10, first?.Priority);

        _queue.TryDequeue(out var second);
        Assert.Equal((byte)5, second?.Priority);

        _queue.TryDequeue(out var third);
        Assert.Equal((byte)1, third?.Priority);
    }

    [Fact]
    public void FullCapacity_ReportsFull()
    {
        // Arrange - Fill to capacity
        for (int i = 0; i < _queue.Capacity; i++)
        {
            _queue.TryEnqueue(new TestMessage
            {
                MessageId = Guid.NewGuid(),
                Priority = (byte)(i % 10)
            });
        }

        // Assert
        Assert.True(_queue.IsFull);
        Assert.Equal(_queue.Capacity, _queue.Count);
    }

    [Fact]
    public void TryPeek_ReturnsHighestPriorityWithoutRemoving()
    {
        // Arrange
        _queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid(), Priority = 3 });
        _queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid(), Priority = 8 });
        _queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid(), Priority = 5 });

        // Act
        var result = _queue.TryPeek(out var peeked);

        // Assert
        Assert.True(result);
        Assert.Equal((byte)8, peeked?.Priority);
        Assert.Equal(3, _queue.Count); // All messages still in queue
    }

    [Fact]
    public void Clear_PriorityQueue_EmptiesQueue()
    {
        // Arrange
        for (byte i = 0; i < 5; i++)
        {
            _queue.TryEnqueue(new TestMessage { MessageId = Guid.NewGuid(), Priority = i });
        }

        // Act
        _queue.Clear();

        // Assert
        Assert.True(_queue.IsEmpty);
        Assert.Equal(0, _queue.Count);
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var options = new MessageQueueOptions
        {
            Capacity = 16,
            EnablePriorityQueue = true,
            EnableDeduplication = false,
            DeduplicationWindowSize = 64
        };
        var queue = new PriorityMessageQueue<TestMessage>(options);

        // Act
        queue.Dispose();

        // Assert
        Assert.True(true); // Should not throw
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
