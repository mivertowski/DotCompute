// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CPU.RingKernels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Backends.CPU.Tests.RingKernels;

/// <summary>
/// Unit tests for CpuMessageQueue.
/// Tests thread-safe message queue operations using BlockingCollection.
/// </summary>
public class CpuMessageQueueTests
{
    #region Constructor Tests

    [Fact(DisplayName = "Constructor should throw on invalid capacity (not power of 2)")]
    public void Constructor_WithInvalidCapacity_ShouldThrow()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;

        // Act & Assert
        Action act1 = () => _ = new CpuMessageQueue<int>(0, logger);
        Action act2 = () => _ = new CpuMessageQueue<int>(-1, logger);
        Action act3 = () => _ = new CpuMessageQueue<int>(100, logger); // Not power of 2

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
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;

        // Act
        Action act = () => _ = new CpuMessageQueue<int>(capacity, logger);

        // Assert
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Constructor should throw on null logger")]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => _ = new CpuMessageQueue<int>(256, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact(DisplayName = "Constructor should set capacity property")]
    public void Constructor_ShouldSetCapacity()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        const int expectedCapacity = 512;

        // Act
        var queue = new CpuMessageQueue<int>(expectedCapacity, logger);

        // Assert
        queue.Capacity.Should().Be(expectedCapacity);
    }

    #endregion

    #region Property Tests

    [Fact(DisplayName = "IsEmpty should return true for new queue")]
    public void IsEmpty_ForNewQueue_ShouldReturnTrue()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);

        // Act & Assert
        queue.IsEmpty.Should().BeTrue();
    }

    [Fact(DisplayName = "IsFull should return false for new queue")]
    public void IsFull_ForNewQueue_ShouldReturnFalse()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);

        // Act & Assert
        queue.IsFull.Should().BeFalse();
    }

    [Fact(DisplayName = "Count should return zero for new queue")]
    public void Count_ForNewQueue_ShouldReturnZero()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);

        // Act & Assert
        queue.Count.Should().Be(0);
    }

    #endregion

    #region Initialization Tests

    [Fact(DisplayName = "InitializeAsync should complete successfully")]
    public async Task InitializeAsync_ShouldComplete()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);

        // Act
        Func<Task> act = async () => await queue.InitializeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "InitializeAsync should be idempotent")]
    public async Task InitializeAsync_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);

        // Act
        await queue.InitializeAsync();
        Func<Task> act = async () => await queue.InitializeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Enqueue Tests

    [Fact(DisplayName = "TryEnqueueAsync should throw before initialization")]
    public async Task TryEnqueueAsync_BeforeInit_ShouldThrow()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        var message = new KernelMessage<int> { Payload = 42, Timestamp = DateTime.UtcNow.Ticks };

        // Act
        Func<Task> act = async () => await queue.TryEnqueueAsync(message);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact(DisplayName = "TryEnqueueAsync should succeed for initialized queue")]
    public async Task TryEnqueueAsync_AfterInit_ShouldSucceed()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        var message = new KernelMessage<int> { Payload = 42, Timestamp = DateTime.UtcNow.Ticks };

        // Act
        var result = await queue.TryEnqueueAsync(message);

        // Assert
        result.Should().BeTrue();
        queue.Count.Should().Be(1);
        queue.IsEmpty.Should().BeFalse();
    }

    [Fact(DisplayName = "TryEnqueueAsync should return false when queue is full")]
    public async Task TryEnqueueAsync_WhenFull_ShouldReturnFalse()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(4, logger); // Small capacity
        await queue.InitializeAsync();

        // Fill queue
        for (var i = 0; i < 4; i++)
        {
            var msg = new KernelMessage<int> { Payload = i, Timestamp = DateTime.UtcNow.Ticks };
            await queue.TryEnqueueAsync(msg);
        }

        var overflowMessage = new KernelMessage<int> { Payload = 999, Timestamp = DateTime.UtcNow.Ticks };

        // Act
        var result = await queue.TryEnqueueAsync(overflowMessage);

        // Assert
        result.Should().BeFalse();
        queue.IsFull.Should().BeTrue();
    }

    [Fact(DisplayName = "EnqueueAsync should block when queue is full")]
    public async Task EnqueueAsync_WhenFull_ShouldBlock()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(4, logger);
        await queue.InitializeAsync();

        // Fill queue
        for (var i = 0; i < 4; i++)
        {
            var msg = new KernelMessage<int> { Payload = i, Timestamp = DateTime.UtcNow.Ticks };
            await queue.EnqueueAsync(msg);
        }

        var overflowMessage = new KernelMessage<int> { Payload = 999, Timestamp = DateTime.UtcNow.Ticks };

        // Act - should block indefinitely
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        Func<Task> act = async () => await queue.EnqueueAsync(overflowMessage, default, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Dequeue Tests

    [Fact(DisplayName = "TryDequeueAsync should throw before initialization")]
    public async Task TryDequeueAsync_BeforeInit_ShouldThrow()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);

        // Act
        Func<Task> act = async () => await queue.TryDequeueAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact(DisplayName = "TryDequeueAsync should return null for empty queue")]
    public async Task TryDequeueAsync_WhenEmpty_ShouldReturnNull()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        // Act
        var result = await queue.TryDequeueAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "TryDequeueAsync should retrieve enqueued message")]
    public async Task TryDequeueAsync_AfterEnqueue_ShouldRetrieveMessage()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        var message = new KernelMessage<int> { Payload = 42, Timestamp = DateTime.UtcNow.Ticks };
        await queue.TryEnqueueAsync(message);

        // Act
        var result = await queue.TryDequeueAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Value.Payload.Should().Be(42);
        queue.Count.Should().Be(0);
        queue.IsEmpty.Should().BeTrue();
    }

    [Fact(DisplayName = "DequeueAsync should block when queue is empty")]
    public async Task DequeueAsync_WhenEmpty_ShouldBlock()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        // Act - should block indefinitely
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        Func<Task> act = async () => await queue.DequeueAsync(default, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "DequeueAsync should retrieve message when available")]
    public async Task DequeueAsync_WithMessage_ShouldRetrieve()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        var message = new KernelMessage<int> { Payload = 42, Timestamp = DateTime.UtcNow.Ticks };
        await queue.EnqueueAsync(message);

        // Act
        var result = await queue.DequeueAsync();

        // Assert
        result.Payload.Should().Be(42);
        queue.Count.Should().Be(0);
    }

    #endregion

    #region FIFO Ordering Tests

    [Fact(DisplayName = "Messages should be dequeued in FIFO order")]
    public async Task Messages_ShouldBeDequeuedInFifoOrder()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        // Enqueue messages in sequence
        for (var i = 0; i < 10; i++)
        {
            var msg = new KernelMessage<int> { Payload = i, Timestamp = DateTime.UtcNow.Ticks };
            await queue.TryEnqueueAsync(msg);
        }

        // Act & Assert - dequeue in same order
        for (var i = 0; i < 10; i++)
        {
            var result = await queue.TryDequeueAsync();
            result.Should().NotBeNull();
            result!.Value.Payload.Should().Be(i);
        }

        queue.IsEmpty.Should().BeTrue();
    }

    #endregion

    #region Statistics Tests

    [Fact(DisplayName = "GetStatisticsAsync should return initial statistics")]
    public async Task GetStatisticsAsync_Initial_ShouldReturnZeros()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        // Act
        var stats = await queue.GetStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalEnqueued.Should().Be(0);
        stats.TotalDequeued.Should().Be(0);
        stats.TotalDropped.Should().Be(0);
        stats.Utilization.Should().Be(0);
    }

    [Fact(DisplayName = "GetStatisticsAsync should track enqueue operations")]
    public async Task GetStatisticsAsync_AfterEnqueue_ShouldTrackOperations()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        // Enqueue 5 messages
        for (var i = 0; i < 5; i++)
        {
            var msg = new KernelMessage<int> { Payload = i, Timestamp = DateTime.UtcNow.Ticks };
            await queue.TryEnqueueAsync(msg);
        }

        // Act
        var stats = await queue.GetStatisticsAsync();

        // Assert
        stats.TotalEnqueued.Should().Be(5);
        stats.TotalDequeued.Should().Be(0);
        queue.Count.Should().Be(5);
    }

    [Fact(DisplayName = "GetStatisticsAsync should track dequeue operations")]
    public async Task GetStatisticsAsync_AfterDequeue_ShouldTrackOperations()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        // Enqueue and dequeue messages
        for (var i = 0; i < 5; i++)
        {
            var msg = new KernelMessage<int> { Payload = i, Timestamp = DateTime.UtcNow.Ticks };
            await queue.TryEnqueueAsync(msg);
        }

        for (var i = 0; i < 3; i++)
        {
            await queue.TryDequeueAsync();
        }

        // Act
        var stats = await queue.GetStatisticsAsync();

        // Assert
        stats.TotalEnqueued.Should().Be(5);
        stats.TotalDequeued.Should().Be(3);
        queue.Count.Should().Be(2);
    }

    [Fact(DisplayName = "GetStatisticsAsync should track dropped messages")]
    public async Task GetStatisticsAsync_WhenFull_ShouldTrackDropped()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(4, logger);
        await queue.InitializeAsync();

        // Fill queue
        for (var i = 0; i < 4; i++)
        {
            var msg = new KernelMessage<int> { Payload = i, Timestamp = DateTime.UtcNow.Ticks };
            await queue.TryEnqueueAsync(msg);
        }

        // Try to enqueue more (should be dropped)
        for (var i = 0; i < 3; i++)
        {
            var msg = new KernelMessage<int> { Payload = i, Timestamp = DateTime.UtcNow.Ticks };
            await queue.TryEnqueueAsync(msg);
        }

        // Act
        var stats = await queue.GetStatisticsAsync();

        // Assert
        stats.TotalEnqueued.Should().Be(4);
        stats.TotalDropped.Should().Be(3);
    }

    #endregion

    #region Dispose Tests

    [Fact(DisplayName = "DisposeAsync should complete successfully")]
    public async Task DisposeAsync_ShouldComplete()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        // Act
        Func<Task> act = async () => await queue.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "DisposeAsync should be idempotent")]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

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
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();
        await queue.DisposeAsync();

        // Act & Assert
        var message = new KernelMessage<int> { Payload = 42, Timestamp = DateTime.UtcNow.Ticks };

        Func<Task> enqueueAct = async () => await queue.TryEnqueueAsync(message);
        Func<Task> dequeueAct = async () => await queue.TryDequeueAsync();

        await enqueueAct.Should().ThrowAsync<ObjectDisposedException>();
        await dequeueAct.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Buffer Access Tests

    [Fact(DisplayName = "GetBuffer should throw NotSupportedException")]
    public async Task GetBuffer_ShouldThrowNotSupported()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        // Act
        Action act = () => queue.GetBuffer();

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*CPU message queues do not use unified memory buffers*");
    }

    [Fact(DisplayName = "GetHeadPtr should throw NotSupportedException")]
    public async Task GetHeadPtr_ShouldThrowNotSupported()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        // Act
        Action act = () => queue.GetHeadPtr();

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*CPU message queues do not use unified memory buffers*");
    }

    [Fact(DisplayName = "GetTailPtr should throw NotSupportedException")]
    public async Task GetTailPtr_ShouldThrowNotSupported()
    {
        // Arrange
        var logger = NullLogger<CpuMessageQueue<int>>.Instance;
        var queue = new CpuMessageQueue<int>(256, logger);
        await queue.InitializeAsync();

        // Act
        Action act = () => queue.GetTailPtr();

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*CPU message queues do not use unified memory buffers*");
    }

    #endregion
}
