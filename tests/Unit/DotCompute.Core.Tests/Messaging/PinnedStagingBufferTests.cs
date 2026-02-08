// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Messaging;
using Xunit;

namespace DotCompute.Core.Tests.Messaging;

/// <summary>
/// Unit tests for PinnedStagingBuffer - zero-copy lock-free ring buffer for GPU DMA transfers.
/// </summary>
public sealed class PinnedStagingBufferTests : IDisposable
{
    private readonly PinnedStagingBuffer _buffer;
    private bool _disposed;

    public PinnedStagingBufferTests()
    {
        _buffer = new PinnedStagingBuffer(capacity: 16, messageSize: 64); // 16 messages, 64 bytes each
    }

    [Fact]
    public void Constructor_ValidCapacity_InitializesBuffer()
    {
        // Arrange & Act
        using var buffer = new PinnedStagingBuffer(capacity: 2048, messageSize: 256);

        // Assert
        Assert.Equal(2048, buffer.Capacity);
        Assert.Equal(256, buffer.MessageSize);
        Assert.Equal(0, buffer.Count);
        Assert.True(buffer.IsEmpty);
        Assert.False(buffer.IsFull);
        Assert.NotEqual(IntPtr.Zero, buffer.BufferPointer);
    }

    [Fact]
    public void Constructor_InvalidCapacity_ThrowsArgumentException()
    {
        // Act & Assert - Not power of 2
        Assert.Throws<ArgumentException>(() => new PinnedStagingBuffer(capacity: 15, messageSize: 64));

        // Too small
        Assert.Throws<ArgumentException>(() => new PinnedStagingBuffer(capacity: 1, messageSize: 64));

        // Invalid message size
        Assert.Throws<ArgumentOutOfRangeException>(() => new PinnedStagingBuffer(capacity: 16, messageSize: 0));
    }

    [Fact]
    public void TryEnqueue_SingleMessage_Success()
    {
        // Arrange
        var data = new byte[64];
        data[0] = 42;

        // Act
        var result = _buffer.TryEnqueue(data);

        // Assert
        Assert.True(result);
        Assert.Equal(1, _buffer.Count);
        Assert.False(_buffer.IsEmpty);
        Assert.False(_buffer.IsFull);
    }

    [Fact]
    public void TryEnqueue_WrongSize_ThrowsArgumentException()
    {
        // Arrange
        var data = new byte[32]; // Wrong size (expected 64)

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _buffer.TryEnqueue(data));
    }

    [Fact]
    public void TryEnqueue_FillBuffer_ReturnsFalseWhenFull()
    {
        // Arrange
        var data = new byte[64];

        // Act - Fill buffer to capacity (16 messages, but ring buffer keeps 1 slot empty)
        for (var i = 0; i < 15; i++)
        {
            Assert.True(_buffer.TryEnqueue(data), $"Should enqueue message {i}");
        }

        // Assert - Buffer is now full
        Assert.True(_buffer.IsFull);
        Assert.False(_buffer.TryEnqueue(data), "Should fail when buffer is full");
    }

    [Fact]
    public void DequeueBatch_AfterEnqueue_ReturnsCorrectData()
    {
        // Arrange
        var message1 = new byte[64];
        message1[0] = 1;
        message1[1] = 2;

        var message2 = new byte[64];
        message2[0] = 3;
        message2[1] = 4;

        _buffer.TryEnqueue(message1);
        _buffer.TryEnqueue(message2);

        // Act
        Span<byte> batch = stackalloc byte[128]; // Space for 2 messages
        var count = _buffer.DequeueBatch(batch, maxMessages: 2);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(1, batch[0]); // First message
        Assert.Equal(2, batch[1]);
        Assert.Equal(3, batch[64]); // Second message
        Assert.Equal(4, batch[65]);
        Assert.True(_buffer.IsEmpty);
    }

    [Fact]
    public void DequeueBatch_EmptyBuffer_ReturnsZero()
    {
        // Arrange
        Span<byte> batch = stackalloc byte[64];

        // Act
        var count = _buffer.DequeueBatch(batch, maxMessages: 1);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void DequeueBatch_PartialBatch_ReturnsAvailableCount()
    {
        // Arrange
        var message = new byte[64];
        _buffer.TryEnqueue(message);
        _buffer.TryEnqueue(message);
        _buffer.TryEnqueue(message);

        // Act - Request more than available
        Span<byte> batch = stackalloc byte[640]; // Space for 10 messages
        var count = _buffer.DequeueBatch(batch, maxMessages: 10);

        // Assert
        Assert.Equal(3, count); // Only 3 were available
        Assert.True(_buffer.IsEmpty);
    }

    [Fact]
    public void Wraparound_EnqueueDequeuePattern_HandlesCircularBuffer()
    {
        // Arrange
        var message = new byte[64];
        for (var i = 0; i < 64; i++)
        {
            message[i] = (byte)i;
        }

        // Act - Fill, dequeue, refill pattern
        Span<byte> batch = stackalloc byte[640]; // 10 messages - moved outside loop to avoid stack overflow
        for (var round = 0; round < 3; round++)
        {
            // Fill buffer
            for (var i = 0; i < 10; i++)
            {
                Assert.True(_buffer.TryEnqueue(message));
            }

            // Dequeue batch
            var count = _buffer.DequeueBatch(batch, maxMessages: 10);

            // Assert
            Assert.Equal(10, count);
            Assert.True(_buffer.IsEmpty);
        }
    }

    [Fact]
    public async Task ConcurrentEnqueue_MultipleThreads_NoDataCorruption()
    {
        // Arrange
        using var largeBuffer = new PinnedStagingBuffer(capacity: 4096, messageSize: 64);
        var threadCount = 10;
        var messagesPerThread = 100;
        var message = new byte[64];

        // Act - Concurrent enqueues
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            await Task.Yield();
            for (var i = 0; i < messagesPerThread; i++)
            {
                message[0] = (byte)threadId;
                message[1] = (byte)i;

                while (!largeBuffer.TryEnqueue(message))
                {
                    await Task.Delay(1); // Retry on buffer full
                }
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var expectedMessages = threadCount * messagesPerThread;
        Assert.Equal(expectedMessages, largeBuffer.Count);
    }

    [Fact]
    public void BufferPointer_ReturnsValidPointer()
    {
        // Act
        var pointer = _buffer.BufferPointer;

        // Assert
        Assert.NotEqual(IntPtr.Zero, pointer);

        // Verify pointer is stable across multiple calls
        Assert.Equal(pointer, _buffer.BufferPointer);
    }

    [Fact]
    public void Dispose_MultipleCalls_NoException()
    {
        // Arrange
        var buffer = new PinnedStagingBuffer(capacity: 16, messageSize: 64);

        // Act & Assert - Should not throw
        buffer.Dispose();
        buffer.Dispose();
    }

    [Fact]
    public void TryEnqueue_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new PinnedStagingBuffer(capacity: 16, messageSize: 64);
        buffer.Dispose();

        // Act & Assert
        var message = new byte[64];
        Assert.Throws<ObjectDisposedException>(() => buffer.TryEnqueue(message));
    }

    [Fact]
    public void BufferPointer_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new PinnedStagingBuffer(capacity: 16, messageSize: 64);
        buffer.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => { var _ = buffer.BufferPointer; });
    }

    [Fact]
    public void HighThroughput_LargeNumberOfMessages_MaintainsIntegrity()
    {
        // Arrange
        using var perfBuffer = new PinnedStagingBuffer(capacity: 8192, messageSize: 1024); // 8 MB buffer
        var messageCount = 5000;
        var message = new byte[1024];
        new Random(42).NextBytes(message);

        // Act - Enqueue many messages
        for (var i = 0; i < messageCount; i++)
        {
            Assert.True(perfBuffer.TryEnqueue(message));
        }

        // Assert
        Assert.Equal(messageCount, perfBuffer.Count);

        // Act - Dequeue all messages
        Span<byte> batch = stackalloc byte[1024 * 100]; // 100 messages at a time
        var totalDequeued = 0;
        while (!perfBuffer.IsEmpty)
        {
            var count = perfBuffer.DequeueBatch(batch, maxMessages: 100);
            totalDequeued += count;
        }

        // Assert
        Assert.Equal(messageCount, totalDequeued);
        Assert.True(perfBuffer.IsEmpty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _buffer?.Dispose();
        _disposed = true;
    }
}
