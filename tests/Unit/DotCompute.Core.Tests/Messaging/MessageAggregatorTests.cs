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
/// Unit tests for <see cref="MessageAggregator{TResponse,TResult}"/>.
/// </summary>
public sealed class MessageAggregatorTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;

    public MessageAggregatorTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
    }

    #region AddResponseAsync Tests

    [Fact]
    public async Task AddResponseAsync_FirstResponse_IncreasesCount()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 3,
            timeout: TimeSpan.FromSeconds(30));

        // Act
        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true });

        // Assert
        aggregator.ReceivedCount.Should().Be(1);
        aggregator.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task AddResponseAsync_AllResponses_CompletesAggregation()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 2,
            timeout: TimeSpan.FromSeconds(30));

        // Act
        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true });
        var isComplete = await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true });

        // Assert
        isComplete.Should().BeTrue();
        aggregator.IsComplete.Should().BeTrue();
        aggregator.ReceivedCount.Should().Be(2);
    }

    [Fact]
    public async Task AddResponseAsync_AllResponses_ResultContainsAllResponses()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 3,
            timeout: TimeSpan.FromSeconds(30));

        var response1 = new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true };
        var response2 = new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true };
        var response3 = new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true };

        // Act
        await aggregator.AddResponseAsync(response1);
        await aggregator.AddResponseAsync(response2);
        await aggregator.AddResponseAsync(response3);

        var result = await aggregator.AggregatedResult;

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(response1);
        result.Should().Contain(response2);
        result.Should().Contain(response3);
    }

    [Fact]
    public async Task AddResponseAsync_WrongType_ReturnsFalse()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 2,
            timeout: TimeSpan.FromSeconds(30));

        // Act
        var result = await aggregator.AddResponseAsync(new DifferentResponse { MessageId = Guid.NewGuid(), IsSuccess = true });

        // Assert
        result.Should().BeFalse();
        aggregator.ReceivedCount.Should().Be(0);
    }

    [Fact]
    public async Task AddResponseAsync_AfterComplete_ReturnsFalse()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 1,
            timeout: TimeSpan.FromSeconds(30));

        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true });

        // Act
        var result = await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true });

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CreateAggregator Tests

    [Fact]
    public async Task CreateAggregator_CustomFunction_AppliesFunction()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateAggregator<TestResponse, int>(
            expectedCount: 3,
            aggregateFunc: responses => responses.Count(r => r.IsSuccess),
            timeout: TimeSpan.FromSeconds(30));

        // Act
        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true });
        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = false });
        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true });

        var result = await aggregator.AggregatedResult;

        // Assert
        result.Should().Be(2); // 2 successes
    }

    #endregion

    #region CreateFirstSuccess Tests

    [Fact]
    public async Task CreateFirstSuccess_ReturnsFirstSuccessfulResponse()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateFirstSuccess<TestResponse>(
            expectedCount: 3,
            timeout: TimeSpan.FromSeconds(30));

        var failedResponse = new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = false };
        var successResponse = new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true };

        // Act
        await aggregator.AddResponseAsync(failedResponse);
        await aggregator.AddResponseAsync(successResponse);
        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true });

        var result = await aggregator.AggregatedResult;

        // Assert
        result.Should().Be(successResponse);
    }

    [Fact]
    public async Task CreateFirstSuccess_AllFailed_ReturnsNull()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateFirstSuccess<TestResponse>(
            expectedCount: 2,
            timeout: TimeSpan.FromSeconds(30));

        // Act
        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = false });
        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = false });

        var result = await aggregator.AggregatedResult;

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region MarkFailed Tests

    [Fact]
    public async Task MarkFailed_CountsTowardTotal()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 2,
            timeout: TimeSpan.FromSeconds(30));

        // Act
        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true });
#pragma warning disable CA2201 // Test intentionally uses generic Exception
        aggregator.MarkFailed(Guid.NewGuid(), new Exception("Test error"));
#pragma warning restore CA2201

        // Assert
        aggregator.ReceivedCount.Should().Be(2);
        aggregator.IsComplete.Should().BeTrue();
    }

    #endregion

    #region Cancel Tests

    [Fact]
    public async Task Cancel_WithPartialResults_ReturnsPartial()
    {
        // Arrange
        var options = new AggregationOptions { IncludePartialResults = true };
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object, options);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 3,
            timeout: TimeSpan.FromSeconds(30));

        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true });

        // Act
        aggregator.Cancel();
        var result = await aggregator.AggregatedResult;

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Cancel_WithoutPartialResults_ThrowsCancellation()
    {
        // Arrange
        var options = new AggregationOptions { IncludePartialResults = false };
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object, options);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 3,
            timeout: TimeSpan.FromSeconds(30));

        // Act
        aggregator.Cancel();

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await aggregator.AggregatedResult);
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public async Task Timeout_WithPartialResults_ReturnsPartial()
    {
        // Arrange
        var options = new AggregationOptions
        {
            DefaultTimeout = TimeSpan.FromMilliseconds(50),
            IncludePartialResults = true
        };
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object, options);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 3,
            timeout: TimeSpan.FromMilliseconds(50));

        await aggregator.AddResponseAsync(new TestResponse { MessageId = Guid.NewGuid(), IsSuccess = true });

        // Act - wait for timeout
        var result = await aggregator.AggregatedResult;

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Timeout_NoResponses_ThrowsTimeoutException()
    {
        // Arrange
        var options = new AggregationOptions
        {
            DefaultTimeout = TimeSpan.FromMilliseconds(50),
            IncludePartialResults = false
        };
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object, options);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 3,
            timeout: TimeSpan.FromMilliseconds(50));

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(
            async () => await aggregator.AggregatedResult);
    }

    #endregion

    #region CancellationToken Tests

    [Fact]
    public async Task CancellationToken_Cancelled_CompletesWithCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 3,
            timeout: TimeSpan.FromSeconds(30),
            cancellationToken: cts.Token);

        // Act
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await aggregator.AggregatedResult);
    }

    #endregion

    #region ExpectedCount Tests

    [Fact]
    public void Constructor_ZeroExpectedCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            factory.CreateWaitAll<TestResponse>(expectedCount: 0, timeout: TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Constructor_NegativeExpectedCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            factory.CreateWaitAll<TestResponse>(expectedCount: -1, timeout: TimeSpan.FromSeconds(30)));
    }

    #endregion

    #region Property Tests

    [Fact]
    public async Task ExpectedCount_ReturnsConfiguredValue()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 5,
            timeout: TimeSpan.FromSeconds(30));

        // Assert
        aggregator.ExpectedCount.Should().Be(5);
    }

    [Fact]
    public async Task ReceivedCount_InitiallyZero()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 5,
            timeout: TimeSpan.FromSeconds(30));

        // Assert
        aggregator.ReceivedCount.Should().Be(0);
    }

    [Fact]
    public async Task IsComplete_InitiallyFalse()
    {
        // Arrange
        var factory = new MessageAggregatorFactory(_loggerFactoryMock.Object);
        await using var aggregator = factory.CreateWaitAll<TestResponse>(
            expectedCount: 5,
            timeout: TimeSpan.FromSeconds(30));

        // Assert
        aggregator.IsComplete.Should().BeFalse();
    }

    #endregion

    #region Test Response Types

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
