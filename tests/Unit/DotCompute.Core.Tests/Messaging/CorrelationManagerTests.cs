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
/// Unit tests for <see cref="CorrelationManager"/>.
/// </summary>
public sealed class CorrelationManagerTests : IDisposable
{
    private readonly Mock<ILogger<CorrelationManager>> _loggerMock = new();
    private readonly CorrelationManager _sut;

    public CorrelationManagerTests()
    {
        _sut = new CorrelationManager(_loggerMock.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    #region RegisterRequest Tests

    [Fact]
    public void RegisterRequest_ValidRequest_ReturnsCorrelationContext()
    {
        // Arrange
        var request = new TestRequest { MessageId = Guid.NewGuid() };
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        var context = _sut.RegisterRequest(request, timeout);

        // Assert
        context.Should().NotBeNull();
        context.CorrelationId.Should().Be(request.MessageId);
        context.Timeout.Should().Be(timeout);
        context.IsCompleted.Should().BeFalse();
        _sut.PendingRequestCount.Should().Be(1);
        _sut.PendingCorrelationIds.Should().Contain(request.MessageId);
    }

    [Fact]
    public void RegisterRequest_NullCorrelationId_SetsToMessageId()
    {
        // Arrange
        var request = new TestRequest
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = null
        };

        // Act
        var context = _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));

        // Assert
        context.CorrelationId.Should().Be(request.MessageId);
        request.CorrelationId.Should().Be(request.MessageId);
    }

    [Fact]
    public void RegisterRequest_EmptyCorrelationId_SetsToMessageId()
    {
        // Arrange
        var request = new TestRequest
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.Empty
        };

        // Act
        var context = _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));

        // Assert
        context.CorrelationId.Should().Be(request.MessageId);
    }

    [Fact]
    public void RegisterRequest_ExistingCorrelationId_PreservesId()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var request = new TestRequest
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = correlationId
        };

        // Act
        var context = _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));

        // Assert
        context.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void RegisterRequest_DuplicateCorrelationId_ThrowsException()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var request1 = new TestRequest { MessageId = correlationId };
        var request2 = new TestRequest { MessageId = correlationId };
        _sut.RegisterRequest(request1, TimeSpan.FromSeconds(30));

        // Act & Assert
        var act = () => _sut.RegisterRequest(request2, TimeSpan.FromSeconds(30));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{correlationId}*already pending*");
    }

    [Fact]
    public void RegisterRequest_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _sut.RegisterRequest<TestResponse>(null!, TimeSpan.FromSeconds(30));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterRequest_DisposedManager_ThrowsObjectDisposedException()
    {
        // Arrange
        _sut.Dispose();
        var request = new TestRequest { MessageId = Guid.NewGuid() };

        // Act & Assert
        var act = () => _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region TryCompleteRequest Tests

    [Fact]
    public async Task TryCompleteRequest_MatchingResponse_CompletesContextAndReturnsTrue()
    {
        // Arrange
        var request = new TestRequest { MessageId = Guid.NewGuid() };
        var context = _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));
        var response = new TestResponse
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = request.MessageId,
            IsSuccess = true
        };

        // Act
        var result = _sut.TryCompleteRequest(response);

        // Assert
        result.Should().BeTrue();
        context.IsCompleted.Should().BeTrue();
        _sut.PendingRequestCount.Should().Be(0);

        var receivedResponse = await context.ResponseTask;
        receivedResponse.Should().Be(response);
    }

    [Fact]
    public void TryCompleteRequest_NoMatchingRequest_ReturnsFalse()
    {
        // Arrange
        var response = new TestResponse
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(), // No matching request
            IsSuccess = true
        };

        // Act
        var result = _sut.TryCompleteRequest(response);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryCompleteRequest_NullCorrelationId_ReturnsFalse()
    {
        // Arrange
        var response = new TestResponse
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = null,
            IsSuccess = true
        };

        // Act
        var result = _sut.TryCompleteRequest(response);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryCompleteRequest_EmptyCorrelationId_ReturnsFalse()
    {
        // Arrange
        var response = new TestResponse
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.Empty,
            IsSuccess = true
        };

        // Act
        var result = _sut.TryCompleteRequest(response);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryCompleteRequest_NullResponse_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _sut.TryCompleteRequest(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryCompleteRequest_DisposedManager_ThrowsObjectDisposedException()
    {
        // Arrange
        _sut.Dispose();
        var response = new TestResponse
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            IsSuccess = true
        };

        // Act & Assert
        var act = () => _sut.TryCompleteRequest(response);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void TryCompleteRequest_TypeMismatch_ReturnsFalse()
    {
        // Arrange
        var request = new TestRequest { MessageId = Guid.NewGuid() };
        _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));
        var wrongTypeResponse = new DifferentResponse
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = request.MessageId,
            IsSuccess = true
        };

        // Act
        var result = _sut.TryCompleteRequest(wrongTypeResponse);

        // Assert
        result.Should().BeFalse(); // TrySetResultUntyped returns false for type mismatch
    }

    #endregion

    #region CancelRequest Tests

    [Fact]
    public async Task CancelRequest_ExistingRequest_CancelsAndReturnsTrue()
    {
        // Arrange
        var request = new TestRequest { MessageId = Guid.NewGuid() };
        var context = _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));

        // Act
        var result = _sut.CancelRequest(request.MessageId);

        // Assert
        result.Should().BeTrue();
        context.IsCompleted.Should().BeTrue();
        _sut.PendingRequestCount.Should().Be(0);

        // Verify task is cancelled
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await context.ResponseTask);
    }

    [Fact]
    public void CancelRequest_NonExistingRequest_ReturnsFalse()
    {
        // Act
        var result = _sut.CancelRequest(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CancelRequest_DisposedManager_ThrowsObjectDisposedException()
    {
        // Arrange
        _sut.Dispose();

        // Act & Assert
        var act = () => _sut.CancelRequest(Guid.NewGuid());
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public async Task Timeout_ExpiredRequest_CompletesWithTimeoutException()
    {
        // Arrange
        var request = new TestRequest { MessageId = Guid.NewGuid() };
        var timeout = TimeSpan.FromMilliseconds(50);
        var context = _sut.RegisterRequest(request, timeout);

        // Act - wait for timeout
        await Task.Delay(100);

        // Assert
        context.IsCompleted.Should().BeTrue();
        _sut.PendingRequestCount.Should().Be(0);

        var ex = await Assert.ThrowsAsync<CorrelationTimeoutException>(
            async () => await context.ResponseTask);
        ex.CorrelationId.Should().Be(request.MessageId);
        ex.TimeoutDuration.Should().Be(timeout);
    }

    [Fact]
    public async Task Timeout_ExpiredRequest_RaisesTimeoutEvent()
    {
        // Arrange
        var request = new TestRequest { MessageId = Guid.NewGuid() };
        var timeout = TimeSpan.FromMilliseconds(50);
        CorrelationTimeoutEventArgs? eventArgs = null;
        _sut.RequestTimedOut += (_, args) => eventArgs = args;
        _sut.RegisterRequest(request, timeout);

        // Act - wait for timeout
        await Task.Delay(150);

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.CorrelationId.Should().Be(request.MessageId);
        eventArgs.TimeoutDuration.Should().Be(timeout);
    }

    #endregion

    #region CancellationToken Tests

    [Fact]
    public async Task CancellationToken_Cancelled_CompletesWithCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var request = new TestRequest { MessageId = Guid.NewGuid() };
        var context = _sut.RegisterRequest(request, TimeSpan.FromSeconds(30), cts.Token);

        // Act
        await cts.CancelAsync();
        await Task.Delay(10); // Give time for cancellation to process

        // Assert
        context.IsCompleted.Should().BeTrue();

        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await context.ResponseTask);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task Dispose_PendingRequests_CancelsAll()
    {
        // Arrange
        var request1 = new TestRequest { MessageId = Guid.NewGuid() };
        var request2 = new TestRequest { MessageId = Guid.NewGuid() };
        var context1 = _sut.RegisterRequest(request1, TimeSpan.FromSeconds(30));
        var context2 = _sut.RegisterRequest(request2, TimeSpan.FromSeconds(30));

        // Act
        _sut.Dispose();

        // Assert
        context1.IsCompleted.Should().BeTrue();
        context2.IsCompleted.Should().BeTrue();
        _sut.PendingRequestCount.Should().Be(0);

        await Assert.ThrowsAsync<TaskCanceledException>(async () => await context1.ResponseTask);
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await context2.ResponseTask);
    }

    [Fact]
    public void Dispose_MultipleCalls_IsIdempotent()
    {
        // Act & Assert - should not throw
        _sut.Dispose();
        _sut.Dispose();
        _sut.Dispose();
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task ConcurrentOperations_MultipleThreads_HandlesCorrectly()
    {
        // Arrange
        var tasks = new List<Task>();
        var completedCount = 0;

        for (var i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var request = new TestRequest { MessageId = Guid.NewGuid() };
                var context = _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));

                var response = new TestResponse
                {
                    MessageId = Guid.NewGuid(),
                    CorrelationId = request.MessageId,
                    IsSuccess = true
                };

                if (_sut.TryCompleteRequest(response))
                {
                    var receivedResponse = await context.ResponseTask;
                    if (receivedResponse.IsSuccess)
                    {
                        Interlocked.Increment(ref completedCount);
                    }
                }
            }));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert
        completedCount.Should().Be(100);
        _sut.PendingRequestCount.Should().Be(0);
    }

    #endregion

    #region CorrelationContext Tests

    [Fact]
    public void CorrelationContext_CreatedAt_IsSetCorrectly()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;
        var request = new TestRequest { MessageId = Guid.NewGuid() };

        // Act
        var context = _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));
        var after = DateTimeOffset.UtcNow;

        // Assert
        context.CreatedAt.Should().BeOnOrAfter(before);
        context.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public async Task CorrelationContext_TrySetResult_OnlyFirstCallSucceeds()
    {
        // Arrange
        var request = new TestRequest { MessageId = Guid.NewGuid() };
        var context = _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));
        var response1 = new TestResponse
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = request.MessageId,
            IsSuccess = true
        };

        // Act - first completion
        var result1 = context.TrySetResult(response1);

        // Create response2 after first completion
        var response2 = new TestResponse
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = request.MessageId,
            IsSuccess = false
        };

        var result2 = context.TrySetResult(response2);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse();

        var receivedResponse = await context.ResponseTask;
        receivedResponse.Should().Be(response1);
    }

    [Fact]
    public async Task CorrelationContext_TrySetException_CompletesWithException()
    {
        // Arrange
        var request = new TestRequest { MessageId = Guid.NewGuid() };
        var context = _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));
        var expectedException = new InvalidOperationException("Test error");

        // Act
        var result = context.TrySetException(expectedException);

        // Assert
        result.Should().BeTrue();
        context.IsCompleted.Should().BeTrue();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await context.ResponseTask);
        ex.Should().Be(expectedException);
    }

    [Fact]
    public void CorrelationContext_Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var request = new TestRequest { MessageId = Guid.NewGuid() };
        var context = _sut.RegisterRequest(request, TimeSpan.FromSeconds(30));

        // Act & Assert - should not throw
        context.Dispose();
        context.Dispose();
        context.Dispose();
    }

    #endregion

    #region Test Message Types

    private sealed class TestRequest : IRequestMessage<TestResponse>
    {
        private readonly byte[] _serializedData = Array.Empty<byte>();

        public Guid MessageId { get; set; } = Guid.NewGuid();
        public string MessageType => nameof(TestRequest);
        public byte Priority { get; set; } = 128;
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 34;

        public ReadOnlySpan<byte> Serialize() => _serializedData;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    private sealed class TestResponse : IResponseMessage
    {
        private readonly byte[] _serializedData = Array.Empty<byte>();

        public Guid MessageId { get; set; } = Guid.NewGuid();
        public string MessageType => nameof(TestResponse);
        public byte Priority { get; set; } = 128;
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 36;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }

        public ReadOnlySpan<byte> Serialize() => _serializedData;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    private sealed class DifferentResponse : IResponseMessage
    {
        private readonly byte[] _serializedData = Array.Empty<byte>();

        public Guid MessageId { get; set; } = Guid.NewGuid();
        public string MessageType => nameof(DifferentResponse);
        public byte Priority { get; set; } = 128;
        public Guid? CorrelationId { get; set; }
        public int PayloadSize => 36;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }

        public ReadOnlySpan<byte> Serialize() => _serializedData;
        public void Deserialize(ReadOnlySpan<byte> data) { }
    }

    #endregion
}
