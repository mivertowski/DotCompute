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
/// Unit tests for <see cref="MessageTtlManager"/>.
/// </summary>
public sealed class MessageTtlManagerTests : IAsyncDisposable
{
    private readonly Mock<ILogger<MessageTtlManager>> _loggerMock = new();
    private MessageTtlManager? _sut;

    public async ValueTask DisposeAsync()
    {
        if (_sut != null)
        {
            await _sut.DisposeAsync();
        }
    }

    private MessageTtlManager CreateManager(
        TimeSpan? checkInterval = null,
        TimeSpan? defaultTtl = null)
    {
        _sut = new MessageTtlManager(
            _loggerMock.Object,
            checkInterval ?? TimeSpan.FromMilliseconds(50),
            defaultTtl ?? TimeSpan.FromSeconds(30));
        return _sut;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var manager = CreateManager();

        // Assert
        manager.TrackedCount.Should().Be(0);
        manager.ExpiredCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MessageTtlManager(null!));
    }

    #endregion

    #region IsExpired Tests

    [Fact]
    public void IsExpired_ExpirableMessage_NotExpired_ReturnsFalse()
    {
        // Arrange
        var manager = CreateManager();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TimeToLive = TimeSpan.FromMinutes(5)
        };

        // Act
        var result = manager.IsExpired(message);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ExpirableMessage_Expired_ReturnsTrue()
    {
        // Arrange
        var manager = CreateManager();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            TimeToLive = TimeSpan.FromMinutes(5)
        };

        // Act
        var result = manager.IsExpired(message);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_NonExpirableMessage_ReturnsFalse()
    {
        // Arrange
        var manager = CreateManager();
        var message = new TestMessage { MessageId = Guid.NewGuid() };

        // Act
        var result = manager.IsExpired(message);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_InfiniteTtl_ReturnsFalse()
    {
        // Arrange
        var manager = CreateManager();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-100),
            TimeToLive = Timeout.InfiniteTimeSpan
        };

        // Act
        var result = manager.IsExpired(message);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsExpired_DisposedManager_ThrowsObjectDisposedException()
    {
        // Arrange
        var manager = CreateManager();
        await manager.DisposeAsync();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            manager.IsExpired(new TestMessage { MessageId = Guid.NewGuid() }));
    }

    [Fact]
    public void IsExpired_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var manager = CreateManager();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => manager.IsExpired<TestMessage>(null!));
    }

    #endregion

    #region GetRemainingTtl Tests

    [Fact]
    public void GetRemainingTtl_ExpirableMessage_ReturnsRemainingTime()
    {
        // Arrange
        var manager = CreateManager();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TimeToLive = TimeSpan.FromMinutes(5)
        };

        // Act
        var remaining = manager.GetRemainingTtl(message);

        // Assert
        remaining.Should().NotBeNull();
        remaining!.Value.TotalMinutes.Should().BeApproximately(5, 0.1);
    }

    [Fact]
    public void GetRemainingTtl_ExpiredMessage_ReturnsZero()
    {
        // Arrange
        var manager = CreateManager();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            TimeToLive = TimeSpan.FromMinutes(5)
        };

        // Act
        var remaining = manager.GetRemainingTtl(message);

        // Assert
        remaining.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetRemainingTtl_NonExpirableMessage_ReturnsNull()
    {
        // Arrange
        var manager = CreateManager();
        var message = new TestMessage { MessageId = Guid.NewGuid() };

        // Act
        var remaining = manager.GetRemainingTtl(message);

        // Assert
        remaining.Should().BeNull();
    }

    [Fact]
    public void GetRemainingTtl_InfiniteTtl_ReturnsNull()
    {
        // Arrange
        var manager = CreateManager();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TimeToLive = Timeout.InfiniteTimeSpan
        };

        // Act
        var remaining = manager.GetRemainingTtl(message);

        // Assert
        remaining.Should().BeNull();
    }

    #endregion

    #region TrackAsync Tests

    [Fact]
    public async Task TrackAsync_AddsToTrackedMessages()
    {
        // Arrange
        var manager = CreateManager();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TimeToLive = TimeSpan.FromMinutes(5)
        };

        // Act
        var trackingId = await manager.TrackAsync(message, _ => Task.CompletedTask);

        // Assert
        trackingId.Should().NotBeEmpty();
        manager.TrackedCount.Should().Be(1);
    }

    [Fact]
    public async Task TrackAsync_InfiniteTtl_ReturnsEmptyGuid()
    {
        // Arrange
        var manager = CreateManager();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TimeToLive = Timeout.InfiniteTimeSpan
        };

        // Act
        var trackingId = await manager.TrackAsync(message, _ => Task.CompletedTask);

        // Assert
        trackingId.Should().Be(Guid.Empty);
        manager.TrackedCount.Should().Be(0);
    }

    [Fact]
    public async Task TrackAsync_NonExpirableMessage_UsesDefaultTtl()
    {
        // Arrange
        var defaultTtl = TimeSpan.FromMilliseconds(100);
        var manager = CreateManager(checkInterval: TimeSpan.FromMilliseconds(50), defaultTtl: defaultTtl);
        var message = new TestMessage { MessageId = Guid.NewGuid() };
        var callbackInvoked = false;

        // Act
        await manager.TrackAsync(message, _ =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        // Wait for expiration
        await Task.Delay(250);

        // Assert
        callbackInvoked.Should().BeTrue();
        manager.ExpiredCount.Should().Be(1);
    }

    [Fact]
    public async Task TrackAsync_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var manager = CreateManager();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            manager.TrackAsync<TestMessage>(null!, _ => Task.CompletedTask));
    }

    [Fact]
    public async Task TrackAsync_NullCallback_ThrowsArgumentNullException()
    {
        // Arrange
        var manager = CreateManager();
        var message = new TestMessage { MessageId = Guid.NewGuid() };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            manager.TrackAsync(message, null!));
    }

    #endregion

    #region CancelTracking Tests

    [Fact]
    public async Task CancelTracking_ValidId_ReturnsTrue()
    {
        // Arrange
        var manager = CreateManager();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TimeToLive = TimeSpan.FromMinutes(5)
        };
        var trackingId = await manager.TrackAsync(message, _ => Task.CompletedTask);

        // Act
        var result = manager.CancelTracking(trackingId);

        // Assert
        result.Should().BeTrue();
        manager.TrackedCount.Should().Be(0);
    }

    [Fact]
    public void CancelTracking_InvalidId_ReturnsFalse()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        var result = manager.CancelTracking(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelTracking_PreventsCallback()
    {
        // Arrange
        var manager = CreateManager(checkInterval: TimeSpan.FromMilliseconds(50));
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TimeToLive = TimeSpan.FromMilliseconds(100)
        };
        var callbackInvoked = false;
        var trackingId = await manager.TrackAsync(message, _ =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        // Act - cancel before expiration
        manager.CancelTracking(trackingId);
        await Task.Delay(200);

        // Assert
        callbackInvoked.Should().BeFalse();
    }

    #endregion

    #region Expiration Tests

    [Fact]
    public async Task MessageExpired_RaisesEvent()
    {
        // Arrange
        var manager = CreateManager(checkInterval: TimeSpan.FromMilliseconds(50));
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TimeToLive = TimeSpan.FromMilliseconds(75)
        };
        MessageExpiredEventArgs? eventArgs = null;
        manager.MessageExpired += (_, args) => eventArgs = args;

        // Act
        await manager.TrackAsync(message, _ => Task.CompletedTask);
        await Task.Delay(200);

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.MessageId.Should().Be(message.MessageId);
        eventArgs.MessageType.Should().Be(nameof(ExpirableTestMessage));
    }

    [Fact]
    public async Task ExpirationCallback_ThrowsException_LogsError()
    {
        // Arrange
        var manager = CreateManager(checkInterval: TimeSpan.FromMilliseconds(50));
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TimeToLive = TimeSpan.FromMilliseconds(75)
        };

        // Act - callback throws
        await manager.TrackAsync(message, _ => throw new InvalidOperationException("Test error"));
        await Task.Delay(200);

        // Assert - should not throw, error is logged
        manager.ExpiredCount.Should().Be(1);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task DisposeAsync_MultipleCalls_IsIdempotent()
    {
        // Arrange
        var manager = CreateManager();

        // Act & Assert - should not throw
        await manager.DisposeAsync();
        await manager.DisposeAsync();
        await manager.DisposeAsync();
        _sut = null; // Prevent double dispose
    }

    [Fact]
    public async Task DisposeAsync_ClearsTrackedMessages()
    {
        // Arrange
        var manager = CreateManager();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TimeToLive = TimeSpan.FromMinutes(5)
        };
        await manager.TrackAsync(message, _ => Task.CompletedTask);

        // Act
        await manager.DisposeAsync();
        _sut = null;

        // Assert
        manager.TrackedCount.Should().Be(0);
    }

    #endregion

    #region Test Message Types

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

    private sealed class ExpirableTestMessage : IExpirableMessage
    {
        private readonly byte[] _serializedData = new byte[50];

        public Guid MessageId { get; set; } = Guid.NewGuid();
        public string MessageType => nameof(ExpirableTestMessage);
        public byte Priority { get; set; } = 128;
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 50;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public TimeSpan TimeToLive { get; set; } = TimeSpan.FromSeconds(30);

        public ReadOnlySpan<byte> Serialize() => _serializedData;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    #endregion
}

/// <summary>
/// Unit tests for <see cref="MessageEnvelope{TMessage}"/>.
/// </summary>
public sealed class MessageEnvelopeTests
{
    [Fact]
    public void Constructor_DefaultTtl_Is30Seconds()
    {
        // Arrange
        var message = new TestMessage { MessageId = Guid.NewGuid() };

        // Act
        var envelope = new MessageEnvelope<TestMessage>(message);

        // Assert
        envelope.TimeToLive.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Constructor_CustomTtl_IsApplied()
    {
        // Arrange
        var message = new TestMessage { MessageId = Guid.NewGuid() };
        var customTtl = TimeSpan.FromMinutes(10);

        // Act
        var envelope = new MessageEnvelope<TestMessage>(message, customTtl);

        // Assert
        envelope.TimeToLive.Should().Be(customTtl);
    }

    [Fact]
    public void IsExpired_BeforeTtl_ReturnsFalse()
    {
        // Arrange
        var message = new TestMessage { MessageId = Guid.NewGuid() };
        var envelope = new MessageEnvelope<TestMessage>(message, TimeSpan.FromMinutes(5));

        // Assert
        envelope.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_AfterTtl_ReturnsTrue()
    {
        // Arrange
        var message = new TestMessage { MessageId = Guid.NewGuid() };
        var envelope = new MessageEnvelope<TestMessage>(message, TimeSpan.FromMilliseconds(1))
        {
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-5)
        };

        // Assert
        envelope.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void ExpiresAt_ReturnsCorrectTime()
    {
        // Arrange
        var message = new TestMessage { MessageId = Guid.NewGuid() };
        var envelope = new MessageEnvelope<TestMessage>(message, TimeSpan.FromMinutes(5));

        // Assert
        envelope.ExpiresAt.Should().NotBeNull();
        envelope.ExpiresAt!.Value.Should().BeCloseTo(
            DateTimeOffset.UtcNow.AddMinutes(5),
            TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ExpiresAt_InfiniteTtl_ReturnsNull()
    {
        // Arrange
        var message = new TestMessage { MessageId = Guid.NewGuid() };
        var envelope = new MessageEnvelope<TestMessage>(message, Timeout.InfiniteTimeSpan);

        // Assert
        envelope.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void MessageType_IncludesInnerType()
    {
        // Arrange
        var message = new TestMessage { MessageId = Guid.NewGuid() };
        var envelope = new MessageEnvelope<TestMessage>(message);

        // Assert
        envelope.MessageType.Should().Be("Envelope<TestMessage>");
    }

    [Fact]
    public void Properties_DelegateToInnerMessage()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var message = new TestMessage
        {
            MessageId = messageId,
            Priority = 200,
            CorrelationId = correlationId
        };

        // Act
        var envelope = new MessageEnvelope<TestMessage>(message);

        // Assert
        envelope.MessageId.Should().Be(messageId);
        envelope.Priority.Should().Be(200);
        envelope.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Constructor_NullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MessageEnvelope<TestMessage>(null!));
    }

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
}

/// <summary>
/// Unit tests for <see cref="MessageTtlExtensions"/>.
/// </summary>
public sealed class MessageTtlExtensionTests : IAsyncDisposable
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private IDeadLetterQueue? _dlq;

    public MessageTtlExtensionTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
    }

    public ValueTask DisposeAsync()
    {
        _dlq?.Dispose();
        return ValueTask.CompletedTask;
    }

    private IDeadLetterQueue CreateDlq()
    {
        var factory = new DeadLetterQueueFactory(_loggerFactoryMock.Object);
        _dlq = factory.Create("test-dlq", new DeadLetterQueueOptions { EnableAutoCleanup = false });
        return _dlq;
    }

    [Fact]
    public void WithTtl_WrapsMessageWithSpecifiedTtl()
    {
        // Arrange
        var message = new TestMessage { MessageId = Guid.NewGuid() };

        // Act
        var envelope = message.WithTtl(TimeSpan.FromMinutes(10));

        // Assert
        envelope.TimeToLive.Should().Be(TimeSpan.FromMinutes(10));
        envelope.Message.Should().BeSameAs(message);
    }

    [Fact]
    public void WithNoExpiration_WrapsMessageWithInfiniteTtl()
    {
        // Arrange
        var message = new TestMessage { MessageId = Guid.NewGuid() };

        // Act
        var envelope = message.WithNoExpiration();

        // Assert
        envelope.TimeToLive.Should().Be(Timeout.InfiniteTimeSpan);
        envelope.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task DeadLetterIfExpiredAsync_ExpiredMessage_ReturnsEntry()
    {
        // Arrange
        var dlq = CreateDlq();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            TimeToLive = TimeSpan.FromMinutes(5)
        };

        // Act
        var entry = await dlq.DeadLetterIfExpiredAsync(message, "source-queue");

        // Assert
        entry.Should().NotBeNull();
        entry!.Reason.Should().Be(DeadLetterReason.Expired);
        entry.SourceQueue.Should().Be("source-queue");
    }

    [Fact]
    public async Task DeadLetterIfExpiredAsync_NotExpiredMessage_ReturnsNull()
    {
        // Arrange
        var dlq = CreateDlq();
        var message = new ExpirableTestMessage
        {
            MessageId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TimeToLive = TimeSpan.FromMinutes(5)
        };

        // Act
        var entry = await dlq.DeadLetterIfExpiredAsync(message);

        // Assert
        entry.Should().BeNull();
    }

    [Fact]
    public async Task DeadLetterOnErrorAsync_CreatesEntryWithException()
    {
        // Arrange
        var dlq = CreateDlq();
        var message = new TestMessage { MessageId = Guid.NewGuid() };
        var exception = new InvalidOperationException("Test error");

        // Act
        var entry = await dlq.DeadLetterOnErrorAsync(message, exception, attemptCount: 2);

        // Assert
        entry.Should().NotBeNull();
        entry.Reason.Should().Be(DeadLetterReason.ProcessingError);
        entry.ErrorMessage.Should().Be("Test error");
        entry.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task DeadLetterOnMaxRetriesAsync_CreatesEntryWithCorrectReason()
    {
        // Arrange
        var dlq = CreateDlq();
        var message = new TestMessage { MessageId = Guid.NewGuid() };

        // Act
        var entry = await dlq.DeadLetterOnMaxRetriesAsync(message, maxRetries: 5);

        // Assert
        entry.Should().NotBeNull();
        entry.Reason.Should().Be(DeadLetterReason.MaxRetriesExceeded);
        entry.ErrorMessage.Should().Contain("5");
        entry.AttemptCount.Should().Be(5);
    }

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

    private sealed class ExpirableTestMessage : IExpirableMessage
    {
        private readonly byte[] _serializedData = new byte[50];

        public Guid MessageId { get; set; } = Guid.NewGuid();
        public string MessageType => nameof(ExpirableTestMessage);
        public byte Priority { get; set; } = 128;
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 50;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public TimeSpan TimeToLive { get; set; } = TimeSpan.FromSeconds(30);

        public ReadOnlySpan<byte> Serialize() => _serializedData;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }
}
