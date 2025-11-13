// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using DotCompute.Core.Messaging;
using Xunit;

namespace DotCompute.Core.Tests.Messaging;

/// <summary>
/// Unit tests for MessageQueueFactory helper methods.
/// Tests factory creation patterns and convenience methods.
/// </summary>
public sealed class MessageQueueFactoryTests
{
    [Fact]
    public void CreateDefault_ReturnsStandardQueue()
    {
        // Act
        var queue = MessageQueueFactory.CreateDefault<TestMessage>();

        // Assert
        Assert.NotNull(queue);
        Assert.IsType<MessageQueue<TestMessage>>(queue);
        Assert.Equal(1024, queue.Capacity); // Default capacity
    }

    [Fact]
    public void CreatePriority_ReturnsPriorityQueue()
    {
        // Act
        var queue = MessageQueueFactory.CreatePriority<TestMessage>();

        // Assert
        Assert.NotNull(queue);
        Assert.IsType<PriorityMessageQueue<TestMessage>>(queue);
    }

    [Fact]
    public void CreateHighThroughput_OptimizedConfiguration()
    {
        // Act
        var queue = MessageQueueFactory.CreateHighThroughput<TestMessage>(capacity: 4096);

        // Assert
        Assert.NotNull(queue);
        Assert.Equal(4096, queue.Capacity);
    }

    [Fact]
    public void Create_WithOptions_UsesConfiguration()
    {
        // Arrange
        var options = new MessageQueueOptions
        {
            Capacity = 2048,
            EnableDeduplication = true,
            BackpressureStrategy = BackpressureStrategy.Reject
        };

        // Act
        var queue = MessageQueueFactory.Create<TestMessage>(options);

        // Assert
        Assert.NotNull(queue);
        Assert.Equal(2048, queue.Capacity);
    }
}
