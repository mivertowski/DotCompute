// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Backends.Metal.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.Utilities;

/// <summary>
/// Comprehensive tests for SimpleRetryPolicy and SimpleRetryPolicy&lt;T&gt;.
/// </summary>
public sealed class SimpleRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_SuccessfulOperation_NoRetries()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy(maxRetries: 3, delay: TimeSpan.FromMilliseconds(10), logger: logger.Object);
        var executionCount = 0;

        Func<Task> action = () =>
        {
            executionCount++;
            return Task.CompletedTask;
        };

        // Act
        await policy.ExecuteAsync(action);

        // Assert
        Assert.Equal(1, executionCount);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_FailsOnce_RetriesSuccessfully()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy(maxRetries: 3, delay: TimeSpan.FromMilliseconds(10), logger: logger.Object);
        var executionCount = 0;

        Func<Task> action = () =>
        {
            executionCount++;
            if (executionCount == 1)
            {
                throw new InvalidOperationException("Transient failure");
            }
            return Task.CompletedTask;
        };

        // Act
        await policy.ExecuteAsync(action);

        // Assert
        Assert.Equal(2, executionCount);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_FailsMultipleTimes_RetriesUntilSuccess()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy(maxRetries: 5, delay: TimeSpan.FromMilliseconds(10), logger: logger.Object);
        var executionCount = 0;

        Func<Task> action = () =>
        {
            executionCount++;
            if (executionCount <= 3)
            {
                throw new InvalidOperationException("Transient failure");
            }
            return Task.CompletedTask;
        };

        // Act
        await policy.ExecuteAsync(action);

        // Assert
        Assert.Equal(4, executionCount);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ExecuteAsync_ExceedsMaxRetries_ThrowsException()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy(maxRetries: 3, delay: TimeSpan.FromMilliseconds(10), logger: logger.Object);
        var executionCount = 0;

        Func<Task> action = () =>
        {
            executionCount++;
            throw new InvalidOperationException("Persistent failure");
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await policy.ExecuteAsync(action));
        Assert.Equal(4, executionCount); // Initial + 3 retries
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy(maxRetries: 3, delay: TimeSpan.FromMilliseconds(100), logger: logger.Object);
        var cts = new CancellationTokenSource();
        var executionCount = 0;

        Func<Task> action = () =>
        {
            executionCount++;
            if (executionCount == 1)
            {
                cts.Cancel();
                throw new InvalidOperationException("Failure before cancellation");
            }
            return Task.CompletedTask;
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await policy.ExecuteAsync(action, cts.Token));
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task ExecuteAsync_RespectsDelay_HasMinimumTimeBetweenRetries()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var delayMs = 50;
        var policy = new SimpleRetryPolicy(maxRetries: 2, delay: TimeSpan.FromMilliseconds(delayMs), logger: logger.Object);
        var executionCount = 0;
        var stopwatch = Stopwatch.StartNew();

        Func<Task> action = () =>
        {
            executionCount++;
            if (executionCount <= 2)
            {
                throw new InvalidOperationException("Transient failure");
            }
            return Task.CompletedTask;
        };

        // Act
        await policy.ExecuteAsync(action);
        stopwatch.Stop();

        // Assert
        Assert.Equal(3, executionCount);
        // Should take at least 2 * delayMs (2 retries with delay)
        Assert.True(stopwatch.ElapsedMilliseconds >= 2 * delayMs);
    }

    [Fact]
    public async Task GenericExecuteAsync_SuccessfulOperation_ReturnsValue()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy<int>(maxRetries: 3, delay: TimeSpan.FromMilliseconds(10), logger: logger.Object);
        var executionCount = 0;

        Func<Task<int>> operation = () =>
        {
            executionCount++;
            return Task.FromResult(42);
        };

        // Act
        var result = await policy.ExecuteAsync(operation);

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task GenericExecuteAsync_FailsOnce_RetriesAndReturnsValue()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy<string>(maxRetries: 3, delay: TimeSpan.FromMilliseconds(10), logger: logger.Object);
        var executionCount = 0;

        Func<Task<string>> operation = () =>
        {
            executionCount++;
            if (executionCount == 1)
            {
                throw new InvalidOperationException("Transient failure");
            }
            return Task.FromResult("Success");
        };

        // Act
        var result = await policy.ExecuteAsync(operation);

        // Assert
        Assert.Equal("Success", result);
        Assert.Equal(2, executionCount);
    }

    [Fact]
    public async Task GenericExecuteAsync_ExceedsMaxRetries_ThrowsException()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy<bool>(maxRetries: 2, delay: TimeSpan.FromMilliseconds(10), logger: logger.Object);
        var executionCount = 0;

        Func<Task<bool>> operation = () =>
        {
            executionCount++;
            throw new InvalidOperationException("Persistent failure");
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await policy.ExecuteAsync(operation));
        Assert.Equal(3, executionCount); // Initial + 2 retries
    }

    [Fact]
    public async Task NonGenericObjectExecuteAsync_SuccessfulOperation_ReturnsValue()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy(maxRetries: 3, delay: TimeSpan.FromMilliseconds(10), logger: logger.Object) as IAsyncPolicy<object>;
        var executionCount = 0;

        Func<Task<object>> operation = () =>
        {
            executionCount++;
            return Task.FromResult<object>("test value");
        };

        // Act
        var result = await policy.ExecuteAsync(operation);

        // Assert
        Assert.Equal("test value", result);
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task Constructor_DefaultDelay_Uses100Milliseconds()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy(maxRetries: 2, logger: logger.Object);
        var executionCount = 0;
        var stopwatch = Stopwatch.StartNew();

        Func<Task> action = () =>
        {
            executionCount++;
            if (executionCount <= 2)
            {
                throw new InvalidOperationException("Transient failure");
            }
            return Task.CompletedTask;
        };

        // Act
        await policy.ExecuteAsync(action);
        stopwatch.Stop();

        // Assert
        Assert.Equal(3, executionCount);
        // Should take at least 2 * 100ms (default delay)
        Assert.True(stopwatch.ElapsedMilliseconds >= 200);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentExceptionTypes_RetriesForAll()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy(maxRetries: 3, delay: TimeSpan.FromMilliseconds(10), logger: logger.Object);
        var executionCount = 0;

        Func<Task> action = () =>
        {
            executionCount++;
            return executionCount switch
            {
                1 => throw new InvalidOperationException("First failure"),
                2 => throw new ArgumentException("Second failure"),
                3 => throw new TimeoutException("Third failure"),
                _ => Task.CompletedTask
            };
        };

        // Act
        await policy.ExecuteAsync(action);

        // Assert
        Assert.Equal(4, executionCount);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ExecuteAsync_NoLogger_DoesNotThrow()
    {
        // Arrange
        var policy = new SimpleRetryPolicy(maxRetries: 2, delay: TimeSpan.FromMilliseconds(10), logger: null);
        var executionCount = 0;

        Func<Task> action = () =>
        {
            executionCount++;
            if (executionCount == 1)
            {
                throw new InvalidOperationException("Transient failure");
            }
            return Task.CompletedTask;
        };

        // Act
        await policy.ExecuteAsync(action);

        // Assert
        Assert.Equal(2, executionCount);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroMaxRetries_NoRetries()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy(maxRetries: 0, delay: TimeSpan.FromMilliseconds(10), logger: logger.Object);
        var executionCount = 0;

        Func<Task> action = () =>
        {
            executionCount++;
            throw new InvalidOperationException("Failure");
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await policy.ExecuteAsync(action));
        Assert.Equal(1, executionCount);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task GenericExecuteAsync_Cancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy<int>(maxRetries: 3, delay: TimeSpan.FromMilliseconds(100), logger: logger.Object);
        var cts = new CancellationTokenSource();
        var executionCount = 0;

        Func<Task<int>> operation = () =>
        {
            executionCount++;
            if (executionCount == 1)
            {
                cts.Cancel();
                throw new InvalidOperationException("Failure before cancellation");
            }
            return Task.FromResult(42);
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await policy.ExecuteAsync(operation, cts.Token));
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentExecution_HandlesMultipleOperations()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var policy = new SimpleRetryPolicy(maxRetries: 2, delay: TimeSpan.FromMilliseconds(10), logger: logger.Object);
        var successCount = 0;

        Func<Task> action = () =>
        {
            var random = new Random();
            if (random.Next(2) == 0)
            {
                throw new InvalidOperationException("Random failure");
            }
            Interlocked.Increment(ref successCount);
            return Task.CompletedTask;
        };

        // Act
        var tasks = Enumerable.Range(0, 10).Select(_ => policy.ExecuteAsync(action));
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, successCount);
    }
}
