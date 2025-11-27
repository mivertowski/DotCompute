// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DotCompute.Abstractions.Messaging;
using DotCompute.Core.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="DeadLetterQueue"/>.
/// </summary>
public sealed class DeadLetterQueueTests : IDisposable
{
    private readonly Mock<ILogger<DeadLetterQueue>> _loggerMock = new();
    private DeadLetterQueue? _sut;

    public void Dispose()
    {
        _sut?.Dispose();
    }

    private DeadLetterQueue CreateQueue(
        string name = "test-dlq",
        DeadLetterQueueOptions? options = null)
    {
        _sut = new DeadLetterQueue(
            name,
            options ?? new DeadLetterQueueOptions { EnableAutoCleanup = false },
            _loggerMock.Object);
        return _sut;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        // Arrange & Act
        var queue = CreateQueue("my-queue");

        // Assert
        queue.Name.Should().Be("my-queue");
    }

    [Fact]
    public void Constructor_InitializesEmptyQueue()
    {
        // Arrange & Act
        var queue = CreateQueue();

        // Assert
        queue.Count.Should().Be(0);
        queue.TotalDeadLettered.Should().Be(0);
    }

    [Fact]
    public void Constructor_NullName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DeadLetterQueue(null!, DeadLetterQueueOptions.Default, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DeadLetterQueue("test", null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DeadLetterQueue("test", DeadLetterQueueOptions.Default, null!));
    }

    #endregion

    #region EnqueueAsync Tests

    [Fact]
    public async Task EnqueueAsync_SingleMessage_IncreasesCount()
    {
        // Arrange
        var queue = CreateQueue();
        var message = new TestMessage { MessageId = Guid.NewGuid() };

        // Act
        await queue.EnqueueAsync(message, DeadLetterReason.Expired);

        // Assert
        queue.Count.Should().Be(1);
        queue.TotalDeadLettered.Should().Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_ReturnsEntryWithCorrectMetadata()
    {
        // Arrange
        var queue = CreateQueue();
        var messageId = Guid.NewGuid();
        var message = new TestMessage { MessageId = messageId };

        // Act
        var entry = await queue.EnqueueAsync(
            message,
            DeadLetterReason.ProcessingError,
            "Something went wrong",
            attemptCount: 3,
            sourceQueue: "source-queue");

        // Assert
        entry.Should().NotBeNull();
        entry.OriginalMessageId.Should().Be(messageId);
        entry.MessageType.Should().Be(nameof(TestMessage));
        entry.Reason.Should().Be(DeadLetterReason.ProcessingError);
        entry.ErrorMessage.Should().Be("Something went wrong");
        entry.AttemptCount.Should().Be(3);
        entry.SourceQueue.Should().Be("source-queue");
        entry.DeadLetteredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task EnqueueAsync_RaisesEvent()
    {
        // Arrange
        var queue = CreateQueue();
        DeadLetterEventArgs? eventArgs = null;
        queue.MessageDeadLettered += (_, args) => eventArgs = args;
        var message = new TestMessage { MessageId = Guid.NewGuid() };

        // Act
        await queue.EnqueueAsync(message, DeadLetterReason.MaxRetriesExceeded);

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.Entry.Reason.Should().Be(DeadLetterReason.MaxRetriesExceeded);
        eventArgs.QueueName.Should().Be("test-dlq");
    }

    [Fact]
    public async Task EnqueueAsync_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = CreateQueue();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            queue.EnqueueAsync<TestMessage>(null!, DeadLetterReason.Expired));
    }

    [Fact]
    public async Task EnqueueAsync_DisposedQueue_ThrowsObjectDisposedException()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired));
    }

    [Fact]
    public async Task EnqueueAsync_AtMaxCapacity_RemovesOldest()
    {
        // Arrange
        var options = new DeadLetterQueueOptions { MaxCapacity = 2, EnableAutoCleanup = false };
        var queue = CreateQueue(options: options);

        var msg1 = new TestMessage { MessageId = Guid.NewGuid() };
        var msg2 = new TestMessage { MessageId = Guid.NewGuid() };
        var msg3 = new TestMessage { MessageId = Guid.NewGuid() };

        await queue.EnqueueAsync(msg1, DeadLetterReason.Expired);
        await queue.EnqueueAsync(msg2, DeadLetterReason.Expired);

        // Act
        await queue.EnqueueAsync(msg3, DeadLetterReason.Expired);

        // Assert
        queue.Count.Should().Be(2);
        var entries = queue.GetEntries();
        entries.Should().NotContain(e => e.OriginalMessageId == msg1.MessageId);
        entries.Should().Contain(e => e.OriginalMessageId == msg2.MessageId);
        entries.Should().Contain(e => e.OriginalMessageId == msg3.MessageId);
    }

    #endregion

    #region TryDequeue Tests

    [Fact]
    public async Task TryDequeue_WithMessages_ReturnsTrueAndEntry()
    {
        // Arrange
        var queue = CreateQueue();
        var message = new TestMessage { MessageId = Guid.NewGuid() };
        await queue.EnqueueAsync(message, DeadLetterReason.Expired);

        // Act
        var result = queue.TryDequeue(out var entry);

        // Assert
        result.Should().BeTrue();
        entry.Should().NotBeNull();
        entry!.OriginalMessageId.Should().Be(message.MessageId);
        queue.Count.Should().Be(0);
    }

    [Fact]
    public void TryDequeue_EmptyQueue_ReturnsFalse()
    {
        // Arrange
        var queue = CreateQueue();

        // Act
        var result = queue.TryDequeue(out var entry);

        // Assert
        result.Should().BeFalse();
        entry.Should().BeNull();
    }

    [Fact]
    public void TryDequeue_DisposedQueue_ThrowsObjectDisposedException()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => queue.TryDequeue(out _));
    }

    #endregion

    #region TryPeek Tests

    [Fact]
    public async Task TryPeek_WithMessages_ReturnsTrueAndDoesNotRemove()
    {
        // Arrange
        var queue = CreateQueue();
        var message = new TestMessage { MessageId = Guid.NewGuid() };
        await queue.EnqueueAsync(message, DeadLetterReason.Expired);

        // Act
        var result = queue.TryPeek(out var entry);

        // Assert
        result.Should().BeTrue();
        entry.Should().NotBeNull();
        queue.Count.Should().Be(1); // Still there
    }

    [Fact]
    public void TryPeek_EmptyQueue_ReturnsFalse()
    {
        // Arrange
        var queue = CreateQueue();

        // Act
        var result = queue.TryPeek(out var entry);

        // Assert
        result.Should().BeFalse();
        entry.Should().BeNull();
    }

    #endregion

    #region GetEntries Tests

    [Fact]
    public async Task GetEntries_ReturnsAllEntries()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.ProcessingError);

        // Act
        var entries = queue.GetEntries();

        // Assert
        entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetEntries_WithPredicate_FiltersResults()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.ProcessingError);

        // Act
        var entries = queue.GetEntries(e => e.Reason == DeadLetterReason.Expired);

        // Assert
        entries.Should().HaveCount(1);
        entries[0].Reason.Should().Be(DeadLetterReason.Expired);
    }

    [Fact]
    public async Task GetEntries_RespectsMaxResults()
    {
        // Arrange
        var queue = CreateQueue();
        for (var i = 0; i < 10; i++)
        {
            await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);
        }

        // Act
        var entries = queue.GetEntries(maxResults: 5);

        // Assert
        entries.Should().HaveCount(5);
    }

    #endregion

    #region GetEntriesByReason Tests

    [Fact]
    public async Task GetEntriesByReason_FiltersCorrectly()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.ProcessingError);
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.ProcessingError);

        // Act
        var entries = queue.GetEntriesByReason(DeadLetterReason.ProcessingError);

        // Assert
        entries.Should().HaveCount(2);
        entries.Should().AllSatisfy(e => e.Reason.Should().Be(DeadLetterReason.ProcessingError));
    }

    #endregion

    #region GetEntriesByTimeRange Tests

    [Fact]
    public async Task GetEntriesByTimeRange_FiltersCorrectly()
    {
        // Arrange
        var queue = CreateQueue();
        var before = DateTimeOffset.UtcNow.AddMilliseconds(-1);
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);
        var after = DateTimeOffset.UtcNow.AddMilliseconds(1);

        // Act
        var entries = queue.GetEntriesByTimeRange(before, after);

        // Assert
        entries.Should().HaveCount(1);
    }

    #endregion

    #region Remove Tests

    [Fact]
    public async Task Remove_ExistingEntry_ReturnsTrue()
    {
        // Arrange
        var queue = CreateQueue();
        var entry = await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);

        // Act
        var result = queue.Remove(entry.EntryId);

        // Assert
        result.Should().BeTrue();
        queue.Count.Should().Be(0);
    }

    [Fact]
    public void Remove_NonExistingEntry_ReturnsFalse()
    {
        // Arrange
        var queue = CreateQueue();

        // Act
        var result = queue.Remove(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region RemoveWhere Tests

    [Fact]
    public async Task RemoveWhere_RemovesMatchingEntries()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.ProcessingError);
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.ProcessingError);

        // Act
        var removed = queue.RemoveWhere(e => e.Reason == DeadLetterReason.ProcessingError);

        // Assert
        removed.Should().Be(2);
        queue.Count.Should().Be(1);
    }

    [Fact]
    public async Task RemoveWhere_NullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => queue.RemoveWhere(null!));
    }

    #endregion

    #region Clear Tests

    [Fact]
    public async Task Clear_RemovesAllEntries()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.ProcessingError);

        // Act
        var removed = queue.Clear();

        // Assert
        removed.Should().Be(2);
        queue.Count.Should().Be(0);
    }

    [Fact]
    public void Clear_EmptyQueue_ReturnsZero()
    {
        // Arrange
        var queue = CreateQueue();

        // Act
        var removed = queue.Clear();

        // Assert
        removed.Should().Be(0);
    }

    #endregion

    #region GetStatistics Tests

    [Fact]
    public async Task GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        var options = new DeadLetterQueueOptions { MaxCapacity = 100, EnableAutoCleanup = false };
        var queue = CreateQueue(options: options);

        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.ProcessingError);

        // Act
        var stats = queue.GetStatistics();

        // Assert
        stats.QueueName.Should().Be("test-dlq");
        stats.CurrentCount.Should().Be(3);
        stats.MaxCapacity.Should().Be(100);
        stats.TotalDeadLettered.Should().Be(3);
        stats.CountByReason.Should().ContainKey(DeadLetterReason.Expired).WhoseValue.Should().Be(2);
        stats.CountByReason.Should().ContainKey(DeadLetterReason.ProcessingError).WhoseValue.Should().Be(1);
        stats.OldestEntryTime.Should().NotBeNull();
        stats.NewestEntryTime.Should().NotBeNull();
        stats.CapturedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetStatistics_EmptyQueue_ReturnsEmptyStats()
    {
        // Arrange
        var queue = CreateQueue();

        // Act
        var stats = queue.GetStatistics();

        // Assert
        stats.CurrentCount.Should().Be(0);
        stats.TotalDeadLettered.Should().Be(0);
        stats.CountByReason.Should().BeEmpty();
        stats.OldestEntryTime.Should().BeNull();
        stats.NewestEntryTime.Should().BeNull();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task Dispose_MultipleCalls_IsIdempotent()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.EnqueueAsync(new TestMessage { MessageId = Guid.NewGuid() }, DeadLetterReason.Expired);

        // Act & Assert - should not throw
        queue.Dispose();
        queue.Dispose();
        queue.Dispose();
        _sut = null; // Prevent double dispose in teardown
    }

    #endregion

    #region Test Message Type

    private sealed class TestMessage : IRingKernelMessage
    {
        private readonly byte[] _serializedData = new byte[34];

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

/// <summary>
/// Unit tests for <see cref="DeadLetterQueueFactory"/>.
/// </summary>
public sealed class DeadLetterQueueFactoryTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;

    public DeadLetterQueueFactoryTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
    }

    [Fact]
    public void Create_ReturnsNewQueue()
    {
        // Arrange
        var factory = new DeadLetterQueueFactory(_loggerFactoryMock.Object);

        // Act
        using var queue = factory.Create("test-queue");

        // Assert
        queue.Should().NotBeNull();
        queue.Name.Should().Be("test-queue");
    }

    [Fact]
    public void GetOrCreate_SameName_ReturnsSameInstance()
    {
        // Arrange
        var factory = new DeadLetterQueueFactory(_loggerFactoryMock.Object);

        // Act
        var queue1 = factory.GetOrCreate("source-queue");
        var queue2 = factory.GetOrCreate("source-queue");

        // Assert
        queue1.Should().BeSameAs(queue2);
        queue1.Name.Should().Be("source-queue.dlq");
    }

    [Fact]
    public void GetOrCreate_DifferentNames_ReturnsDifferentInstances()
    {
        // Arrange
        var factory = new DeadLetterQueueFactory(_loggerFactoryMock.Object);

        // Act
        var queue1 = factory.GetOrCreate("source-queue-1");
        var queue2 = factory.GetOrCreate("source-queue-2");

        // Assert
        queue1.Should().NotBeSameAs(queue2);
    }

    [Fact]
    public void Constructor_NullLoggerFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DeadLetterQueueFactory(null!));
    }
}
