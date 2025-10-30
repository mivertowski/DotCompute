// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.OpenCL.RingKernels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Backends.OpenCL.Tests.RingKernels;

/// <summary>
/// Unit tests for OpenCLMessageQueue.
/// Tests lock-free message queue operations without requiring OpenCL hardware.
/// </summary>
public class OpenCLMessageQueueTests
{
    private readonly ILogger<OpenCLMessageQueue<int>> _mockLogger;

    public OpenCLMessageQueueTests()
    {
        _mockLogger = Substitute.For<ILogger<OpenCLMessageQueue<int>>>();
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw on invalid capacity (not power of 2)")]
    public void Constructor_WithInvalidCapacity_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();

        // Act & Assert
        Action act1 = () => _ = new OpenCLMessageQueue<int>(0, context, _mockLogger);
        Action act2 = () => _ = new OpenCLMessageQueue<int>(-1, context, _mockLogger);
        Action act3 = () => _ = new OpenCLMessageQueue<int>(100, context, _mockLogger); // Not power of 2

        act1.Should().Throw<ArgumentException>().WithMessage("*power of 2*");
        act2.Should().Throw<ArgumentException>().WithMessage("*power of 2*");
        act3.Should().Throw<ArgumentException>().WithMessage("*power of 2*");
    }

    [Theory(DisplayName = "Constructor should accept valid power-of-2 capacities")]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(256)]
    [InlineData(1024)]
    public void Constructor_WithValidCapacity_ShouldAccept(int capacity)
    {
        // Arrange
        var context = CreateMockContext();

        // Act
        Action act = () => _ = new OpenCLMessageQueue<int>(capacity, context, _mockLogger);

        // Assert
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Constructor should throw on null context")]
    public void Constructor_WithNullContext_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => _ = new OpenCLMessageQueue<int>(256, null!, _mockLogger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact(DisplayName = "Constructor should throw on null logger")]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();

        // Act
        Action act = () => _ = new OpenCLMessageQueue<int>(256, context, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should set capacity property")]
    public void Constructor_ShouldSetCapacity()
    {
        // Arrange
        var context = CreateMockContext();
        const int expectedCapacity = 512;

        // Act
        var queue = new OpenCLMessageQueue<int>(expectedCapacity, context, _mockLogger);

        // Assert
        queue.Capacity.Should().Be(expectedCapacity);
    }

    #endregion

    #region Property Tests

    [Fact(DisplayName = "IsEmpty should return true for new queue")]
    public void IsEmpty_ForNewQueue_ShouldReturnTrue()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);

        // Act & Assert
        queue.IsEmpty.Should().BeTrue();
    }

    [Fact(DisplayName = "IsFull should return false for new queue")]
    public void IsFull_ForNewQueue_ShouldReturnFalse()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);

        // Act & Assert
        queue.IsFull.Should().BeFalse();
    }

    [Fact(DisplayName = "Count should return zero for new queue")]
    public void Count_ForNewQueue_ShouldReturnZero()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);

        // Act & Assert
        queue.Count.Should().Be(0);
    }

    #endregion

    #region Buffer Access Tests

    [Fact(DisplayName = "GetBuffer should throw before initialization")]
    public void GetBuffer_BeforeInit_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);

        // Act
        Action act = () => queue.GetBuffer();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact(DisplayName = "GetHeadPtr should throw before initialization")]
    public void GetHeadPtr_BeforeInit_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);

        // Act
        Action act = () => queue.GetHeadPtr();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact(DisplayName = "GetTailPtr should throw before initialization")]
    public void GetTailPtr_BeforeInit_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);

        // Act
        Action act = () => queue.GetTailPtr();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    #endregion

    #region Enqueue/Dequeue Tests

    [Fact(DisplayName = "TryEnqueueAsync should throw before initialization")]
    public async Task TryEnqueueAsync_BeforeInit_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);
        var message = new KernelMessage<int> { Payload = 42, Timestamp = DateTime.UtcNow.Ticks };

        // Act
        Func<Task> act = async () => await queue.TryEnqueueAsync(message);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact(DisplayName = "TryDequeueAsync should throw before initialization")]
    public async Task TryDequeueAsync_BeforeInit_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);

        // Act
        Func<Task> act = async () => await queue.TryDequeueAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact(DisplayName = "EnqueueAsync should throw before initialization")]
    public async Task EnqueueAsync_BeforeInit_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);
        var message = new KernelMessage<int> { Payload = 42, Timestamp = DateTime.UtcNow.Ticks };

        // Act
        Func<Task> act = async () => await queue.EnqueueAsync(message);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact(DisplayName = "DequeueAsync should throw before initialization")]
    public async Task DequeueAsync_BeforeInit_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);

        // Act
        Func<Task> act = async () => await queue.DequeueAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    #endregion

    #region Statistics Tests

    [Fact(DisplayName = "GetStatisticsAsync should return initial statistics")]
    public async Task GetStatisticsAsync_Initial_ShouldReturnZeros()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);

        // Act
        var stats = await queue.GetStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalEnqueued.Should().Be(0);
        stats.TotalDequeued.Should().Be(0);
        stats.TotalDropped.Should().Be(0);
        stats.Utilization.Should().Be(0);
        stats.EnqueueThroughput.Should().Be(0);
        stats.DequeueThroughput.Should().Be(0);
        stats.AverageLatencyUs.Should().Be(0);
    }

    #endregion

    #region Dispose Tests

    [Fact(DisplayName = "DisposeAsync should complete successfully")]
    public async Task DisposeAsync_ShouldComplete()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);

        // Act
        Func<Task> act = async () => await queue.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "DisposeAsync should be idempotent")]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);

        // Act
        await queue.DisposeAsync();
        Func<Task> act = async () => await queue.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Operations after dispose should throw")]
    public async Task Operations_AfterDispose_ShouldThrow()
    {
        // Arrange
        var context = CreateMockContext();
        var queue = new OpenCLMessageQueue<int>(256, context, _mockLogger);
        await queue.DisposeAsync();

        // Act & Assert
        var message = new KernelMessage<int> { Payload = 42, Timestamp = DateTime.UtcNow.Ticks };

        Func<Task> enqueueAct = async () => await queue.TryEnqueueAsync(message);
        Func<Task> dequeueAct = async () => await queue.TryDequeueAsync();

        await enqueueAct.Should().ThrowAsync<InvalidOperationException>();
        await dequeueAct.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Helper Methods

    private static OpenCLContext CreateMockContext()
    {
        // Note: This is a placeholder. In reality, OpenCLContext requires actual OpenCL initialization
        // Hardware tests will use real OpenCL context
        return null!;
    }

    #endregion
}
