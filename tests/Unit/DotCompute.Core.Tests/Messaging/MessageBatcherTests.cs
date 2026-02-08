// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using DotCompute.Core.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="MessageBatcher"/>.
/// </summary>
public sealed class MessageBatcherTests : IAsyncDisposable
{
    private readonly Mock<ILogger<MessageBatcher>> _loggerMock = new();
    private MessageBatcher? _sut;

    public async ValueTask DisposeAsync()
    {
        if (_sut != null)
        {
            await _sut.DisposeAsync();
        }
    }

    private MessageBatcher CreateBatcher(BatchingOptions? options = null)
    {
        _sut = new MessageBatcher(options ?? BatchingOptions.Default, _loggerMock.Object);
        return _sut;
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_SingleMessage_IncreasesBatchSize()
    {
        // Arrange
        var batcher = CreateBatcher();
        var message = new TestMessage { MessageId = Guid.NewGuid() };

        // Act
        await batcher.AddAsync(message);

        // Assert
        batcher.CurrentBatchSize.Should().Be(1);
        batcher.CurrentPayloadSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AddAsync_MultipleMessages_AccumulatesInBatch()
    {
        // Arrange
        var batcher = CreateBatcher();

        // Act
        for (var i = 0; i < 5; i++)
        {
            var message = new TestMessage { MessageId = Guid.NewGuid() };
            await batcher.AddAsync(message);
        }

        // Assert
        batcher.CurrentBatchSize.Should().Be(5);
    }

    [Fact]
    public async Task AddAsync_ReachesMaxBatchSize_ReturnsTrueForFlush()
    {
        // Arrange
        var options = new BatchingOptions { MaxBatchSize = 3, EnableTimeBasedFlushing = false };
        var batcher = CreateBatcher(options);

        // Act
        var result1 = await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() });
        var result2 = await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() });
        var result3 = await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() });

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeFalse();
        result3.Should().BeTrue(); // Should signal ready to flush
    }

    [Fact]
    public async Task AddAsync_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var batcher = CreateBatcher();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await batcher.AddAsync<TestMessage>(null!));
    }

    [Fact]
    public async Task AddAsync_DisposedBatcher_ThrowsObjectDisposedException()
    {
        // Arrange
        var batcher = CreateBatcher();
        await batcher.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() }));
    }

    #endregion

    #region FlushAsync Tests

    [Fact]
    public async Task FlushAsync_WithMessages_ReturnsBatchWithCorrectMetadata()
    {
        // Arrange
        var options = new BatchingOptions { EnableTimeBasedFlushing = false };
        var batcher = CreateBatcher(options);
        var message1 = new TestMessage { MessageId = Guid.NewGuid() };
        var message2 = new TestMessage { MessageId = Guid.NewGuid() };
        await batcher.AddAsync(message1);
        await batcher.AddAsync(message2);

        // Act
        var batch = await batcher.FlushAsync();

        // Assert
        batch.Should().NotBeNull();
        batch.MessageCount.Should().Be(2);
        batch.BatchId.Should().NotBeEmpty();
        batch.PayloadSize.Should().BeGreaterThan(0);
        batch.MessageInfos.Should().HaveCount(2);
        batch.MessageInfos[0].MessageId.Should().Be(message1.MessageId);
        batch.MessageInfos[1].MessageId.Should().Be(message2.MessageId);
    }

    [Fact]
    public async Task FlushAsync_ResetsCurrentBatch()
    {
        // Arrange
        var options = new BatchingOptions { EnableTimeBasedFlushing = false };
        var batcher = CreateBatcher(options);
        await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() });

        // Act
        await batcher.FlushAsync();

        // Assert
        batcher.CurrentBatchSize.Should().Be(0);
        batcher.CurrentPayloadSize.Should().Be(0);
    }

    [Fact]
    public async Task FlushAsync_EmptyBatch_ReturnsEmptyBatch()
    {
        // Arrange
        var options = new BatchingOptions { EnableTimeBasedFlushing = false };
        var batcher = CreateBatcher(options);

        // Act
        var batch = await batcher.FlushAsync();

        // Assert
        batch.MessageCount.Should().Be(0);
        batch.PayloadSize.Should().Be(0);
    }

    [Fact]
    public async Task FlushAsync_DisposedBatcher_ThrowsObjectDisposedException()
    {
        // Arrange
        var batcher = CreateBatcher();
        await batcher.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await batcher.FlushAsync());
    }

    #endregion

    #region BatchFlushed Event Tests

    [Fact]
    public async Task FlushAsync_RaisesEvent()
    {
        // Arrange
        var options = new BatchingOptions { EnableTimeBasedFlushing = false };
        var batcher = CreateBatcher(options);
        BatchFlushedEventArgs? eventArgs = null;
        batcher.BatchFlushed += (_, args) => eventArgs = args;
        await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() });

        // Act
        await batcher.FlushAsync();

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.Batch.MessageCount.Should().Be(1);
        eventArgs.Reason.Should().Be(BatchFlushReason.Manual);
    }

    #endregion

    #region Time-Based Flushing Tests

    [Fact]
    public async Task TimeBasedFlushing_TriggersAfterDelay()
    {
        // Arrange
        var options = new BatchingOptions
        {
            MaxBatchDelay = TimeSpan.FromMilliseconds(50),
            EnableTimeBasedFlushing = true
        };
        var batcher = CreateBatcher(options);
        BatchFlushedEventArgs? eventArgs = null;
        batcher.BatchFlushed += (_, args) => eventArgs = args;
        await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() });

        // Act - wait for timeout
        await Task.Delay(150);

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.Reason.Should().Be(BatchFlushReason.TimeoutExpired);
        batcher.CurrentBatchSize.Should().Be(0);
    }

    #endregion

    #region IsReadyToFlush Tests

    [Fact]
    public async Task IsReadyToFlush_BelowThreshold_ReturnsFalse()
    {
        // Arrange
        var options = new BatchingOptions { MaxBatchSize = 10, EnableTimeBasedFlushing = false };
        var batcher = CreateBatcher(options);
        await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() });

        // Assert
        batcher.IsReadyToFlush.Should().BeFalse();
    }

    [Fact]
    public async Task IsReadyToFlush_AtMaxBatchSize_ReturnsTrue()
    {
        // Arrange
        var options = new BatchingOptions { MaxBatchSize = 2, EnableTimeBasedFlushing = false };
        var batcher = CreateBatcher(options);
        await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() });
        await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() });

        // Assert
        batcher.IsReadyToFlush.Should().BeTrue();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task DisposeAsync_FlushesRemainingMessages()
    {
        // Arrange
        var options = new BatchingOptions { EnableTimeBasedFlushing = false };
        var batcher = CreateBatcher(options);
        BatchFlushedEventArgs? eventArgs = null;
        batcher.BatchFlushed += (_, args) => eventArgs = args;
        await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() });

        // Act
        await batcher.DisposeAsync();
        _sut = null; // Prevent double dispose

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.Reason.Should().Be(BatchFlushReason.Disposal);
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_IsIdempotent()
    {
        // Arrange
        var batcher = CreateBatcher();

        // Act & Assert - should not throw
        await batcher.DisposeAsync();
        await batcher.DisposeAsync();
        await batcher.DisposeAsync();
        _sut = null; // Prevent double dispose
    }

    #endregion

    #region MessageBatch Tests

    [Fact]
    public async Task MessageBatch_PayloadContainsSerializedMessages()
    {
        // Arrange
        var options = new BatchingOptions { EnableTimeBasedFlushing = false };
        var batcher = CreateBatcher(options);
        var message = new TestMessage { MessageId = Guid.NewGuid() };
        await batcher.AddAsync(message);

        // Act
        var batch = await batcher.FlushAsync();

        // Assert
        batch.Payload.Length.Should().BeGreaterThan(0);
        batch.MessageInfos[0].Offset.Should().Be(0);
        batch.MessageInfos[0].Length.Should().Be(batch.Payload.Length);
    }

    [Fact]
    public async Task MessageBatch_CreatedAtAndFlushedAt_AreSet()
    {
        // Arrange
        var options = new BatchingOptions { EnableTimeBasedFlushing = false };
        var batcher = CreateBatcher(options);
        var beforeCreate = DateTimeOffset.UtcNow.AddMilliseconds(-1); // Add tolerance for clock resolution
        await batcher.AddAsync(new TestMessage { MessageId = Guid.NewGuid() });
        await Task.Delay(10);

        // Act
        var batch = await batcher.FlushAsync();
        var afterFlush = DateTimeOffset.UtcNow.AddMilliseconds(1); // Add tolerance for clock resolution

        // Assert
        batch.CreatedAt.Should().BeOnOrAfter(beforeCreate);
        batch.FlushedAt.Should().BeOnOrAfter(batch.CreatedAt);
        batch.FlushedAt.Should().BeOnOrBefore(afterFlush);
    }

    #endregion

    #region Test Message Type

    private sealed class TestMessage : IRingKernelMessage
    {
        private readonly byte[] _serializedData;

        public TestMessage()
        {
            // Create a simple serialized representation
            _serializedData = new byte[34];
            MessageId.ToByteArray().CopyTo(_serializedData, 0);
            _serializedData[16] = Priority;
        }

        public Guid MessageId { get; set; } = Guid.NewGuid();
        public string MessageType => nameof(TestMessage);
        public byte Priority { get; set; } = 128;
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 34;

        public ReadOnlySpan<byte> Serialize() => _serializedData;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    #endregion
}
